using System;
using System.Numerics;
using Microsoft.JSInterop;
using Yaeger.Browser;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Platform;

namespace ZombieSettlementGame.Browser;

/// <summary>The settlement's placeable building types.</summary>
public enum BuildingKind
{
    Farm,
    House,
    Fence,
}

/// <summary>
/// Tags an entity as a placed building of a given <see cref="BuildingKind"/> occupying grid cell
/// (<see cref="Column"/>, <see cref="Row"/>), distinct from the ground tilemap it sits on. Which
/// sprite-sheet frame represents each kind is a rendering concern (see
/// <see cref="GameController.FrameFor"/>), not part of this component.
/// </summary>
public readonly record struct Building(BuildingKind Kind, int Column, int Row);

/// <summary>
/// First real scene: a static ground tilemap plus a handful of placed buildings sitting on it,
/// all drawn from the same Kenney sheet. Owns the ECS world and drives the game loop; each tick
/// is invoked by JavaScript's <c>requestAnimationFrame</c> via <see cref="Tick"/>.
/// </summary>
public sealed class GameController
{
    /// <summary>
    /// Kenney's "Roguelike/RPG pack" (CC0, https://kenney.nl/assets/roguelike-rpg-pack) — a single
    /// 16x16-tile sheet, 1px margin between tiles, that the rest of the game's tile art will be
    /// drawn from. See <c>wwwroot/assets/kenney/roguelike-rpg-pack/LICENSE.txt</c>.
    /// </summary>
    private const string TileSheetPath = "assets/kenney/roguelike-rpg-pack/roguelikeSheet_transparent.png";
    private const int TileSheetColumns = 57;
    private const int TileSheetRows = 31;

    /// <summary>Row 0, column 5 of the sheet: plain grass, used to fill the ground.</summary>
    private const int GrassTile = 5;

    /// <summary>Row 2, column 5 of the sheet: a brick wall, used as the settlement's boundary.</summary>
    private const int WallTile = 2 * TileSheetColumns + 5;

    /// <summary>Row 6, column 0 of the sheet: a planted crop patch.</summary>
    private const int FarmFrame = 6 * TileSheetColumns;

    /// <summary>Row 7, column 45 of the sheet: a wooden double door.</summary>
    private const int HouseFrame = 7 * TileSheetColumns + 45;

    /// <summary>Row 18, column 48 of the sheet: a wooden fence lattice.</summary>
    private const int FenceFrame = 18 * TileSheetColumns + 48;

    /// <summary>Ground grid size, in tiles.</summary>
    private const int GridWidth = 10;
    private const int GridHeight = 10;

    /// <summary>Size of one tile, in world units — one tile is one unit, unlike the ad hoc
    /// NDC-filling size used before there was a camera.</summary>
    private const float TileWorldSize = 1f;

    /// <summary>Empty world-unit margin kept visible around the grid on every side.</summary>
    private const float CameraMargin = 1f;

    private static readonly Vector2 GroundOrigin = Vector2.Zero;
    private static readonly Vector2 GridCenter = new(GridWidth / 2f, GridHeight / 2f);

    private readonly World _world;
    private readonly BrowserRenderSurface _renderSurface;
    private readonly BrowserTimeSource _timeSource = new();
    private readonly IInputState _input = new BrowserInputState();

    /// <summary>Which <see cref="BuildingKind"/> a click places next; chosen with the 1/2/3 keys.</summary>
    private BuildingKind _selectedKind = BuildingKind.Farm;

    /// <summary>Left-mouse state from the previous tick, so a click places once, not once per
    /// frame the button is held.</summary>
    private bool _wasPlacePressed;

    public GameController(BrowserRenderSurface renderSurface)
    {
        _renderSurface = renderSurface;
        _world = new World();
        BuildScene();
    }

    private void BuildScene()
    {
        var tileset = new Tileset(TileSheetPath, TileSheetColumns, TileSheetRows);
        var tilemap = new Tilemap(
            tileset,
            GridWidth,
            GridHeight,
            tileSize: new Vector2(TileWorldSize, TileWorldSize)
        );
        for (var row = 0; row < GridHeight; row++)
        for (var column = 0; column < GridWidth; column++)
        {
            var onBorder = row == 0 || row == GridHeight - 1 || column == 0 || column == GridWidth - 1;
            tilemap.SetTile(column, row, onBorder ? WallTile : GrassTile);
        }

        var ground = _world.CreateEntity("ground");
        _world.AddComponent(ground, new Transform2D(GroundOrigin));
        _world.AddComponent(ground, tilemap);

        PlaceBuilding(BuildingKind.Farm, column: 4, row: 4, tag: "building-farm");
        PlaceBuilding(BuildingKind.House, column: 2, row: 2, tag: "building-house");
        PlaceBuilding(BuildingKind.Fence, column: 7, row: 7, tag: "building-fence");

        var camera = _world.CreateEntity("camera");
        _world.AddComponent(camera, new Camera2D(GridCenter, ComputeCameraZoom(aspectRatio: 1f)));
    }

    /// <summary>
    /// Zoom that fits the whole <see cref="GridWidth"/> x <see cref="GridHeight"/> grid plus a
    /// <see cref="CameraMargin"/> margin inside the viewport on every side, whatever its
    /// <paramref name="aspectRatio"/> — the narrower of a height-fit and a width-fit zoom (see
    /// <see cref="Camera2D.ViewProjection"/> for how <c>Zoom</c> maps to visible half-extents).
    /// A fixed height-only fit (the original approach) crops the left/right edges off-screen on
    /// a portrait phone, where aspect ratio is well under 1.
    /// </summary>
    private static float ComputeCameraZoom(float aspectRatio)
    {
        var zoomForHeight = 1f / (GridHeight / 2f + CameraMargin);
        var zoomForWidth = aspectRatio / (GridWidth / 2f + CameraMargin);
        return MathF.Min(zoomForHeight, zoomForWidth);
    }

    /// <summary>Re-fits the camera's <see cref="Camera2D.Zoom"/> to the current viewport shape;
    /// called once per tick since <paramref name="aspectRatio"/> can change (resize, rotation).</summary>
    private void UpdateCameraZoom(float aspectRatio)
    {
        if (!TryGetCamera(out var camera))
            return;

        camera.Zoom = ComputeCameraZoom(aspectRatio);
        _world.AddComponent(_world.GetEntity("camera"), camera);
    }

    /// <summary>
    /// Places a <see cref="Building"/> of the given <paramref name="kind"/> on grid cell
    /// (<paramref name="column"/>, <paramref name="row"/>), using the same cell-centre math the
    /// tilemap itself uses (see <c>Yaeger.Systems.UnifiedRenderSystem.SubmitTilemap</c>) so it
    /// lines up with the grid exactly one layer above the ground.
    /// </summary>
    private void PlaceBuilding(BuildingKind kind, int column, int row, string? tag = null)
    {
        var building = tag is null ? _world.CreateEntity() : _world.CreateEntity(tag);
        _world.AddComponent(
            building,
            new Transform2D(
                GroundOrigin
                    + new Vector2(column + 0.5f, GridHeight - 1 - row + 0.5f) * TileWorldSize,
                scale: new Vector2(TileWorldSize, TileWorldSize)
            )
        );
        _world.AddComponent(building, new SpriteSheet(TileSheetPath, TileSheetColumns, TileSheetRows));
        _world.AddComponent(building, new Building(kind, column, row));
    }

    /// <summary>Whether grid cell (<paramref name="column"/>, <paramref name="row"/>) already has
    /// a building on it.</summary>
    private bool IsCellOccupied(int column, int row)
    {
        foreach (var (_, building, _) in _world.Query<Building, Transform2D>())
            if (building.Column == column && building.Row == row)
                return true;
        return false;
    }

    private bool TryGetCamera(out Camera2D camera)
    {
        camera = default;
        return _world.TryGetEntity("camera", out var cameraEntity)
            && _world.TryGetComponent(cameraEntity, out camera);
    }

    /// <summary>
    /// Maps a mouse position, given in the same NDC coordinates <see cref="Camera2D.ViewProjection"/>
    /// produces, to the grid cell underneath it by inverting the camera's view-projection.
    /// Returns <c>false</c> when the point falls outside the buildable interior (the outermost
    /// ring is the boundary wall, not placeable ground) or the camera matrix isn't invertible.
    /// </summary>
    private static bool TryScreenToCell(
        Camera2D camera,
        float aspectRatio,
        Vector2 mouseNdc,
        out int column,
        out int row
    )
    {
        column = 0;
        row = 0;

        if (!Matrix4x4.Invert(camera.ViewProjection(aspectRatio), out var inverseViewProjection))
            return false;

        var world = Vector4.Transform(
            new Vector4(mouseNdc.X, mouseNdc.Y, 0f, 1f),
            inverseViewProjection
        );

        column = (int)MathF.Floor((world.X - GroundOrigin.X) / TileWorldSize);
        var rowFromBottom = (int)MathF.Floor((world.Y - GroundOrigin.Y) / TileWorldSize);
        row = GridHeight - 1 - rowFromBottom;

        return column >= 1 && column <= GridWidth - 2 && row >= 1 && row <= GridHeight - 2;
    }

    /// <summary>
    /// Reads the 1/2/3 keys to change which <see cref="BuildingKind"/> a click places, and places
    /// one on a left click over an empty interior cell — edge-detected against
    /// <see cref="_wasPlacePressed"/> so a held button places once, not every tick.
    /// </summary>
    private void HandlePlacementInput(float aspectRatio)
    {
        if (_input.IsKeyPressed(Keys.Num1))
            _selectedKind = BuildingKind.Farm;
        else if (_input.IsKeyPressed(Keys.Num2))
            _selectedKind = BuildingKind.House;
        else if (_input.IsKeyPressed(Keys.Num3))
            _selectedKind = BuildingKind.Fence;

        var isPlacePressed = _input.IsMouseButtonPressed(MouseButton.Left);
        var justClicked = isPlacePressed && !_wasPlacePressed;
        _wasPlacePressed = isPlacePressed;

        if (
            justClicked
            && TryGetCamera(out var camera)
            && TryScreenToCell(camera, aspectRatio, _input.MousePositionNdc, out var column, out var row)
            && !IsCellOccupied(column, row)
        )
            PlaceBuilding(_selectedKind, column, row);
    }

    /// <summary>Sprite-sheet frame that represents each <see cref="BuildingKind"/>.</summary>
    private static int FrameFor(BuildingKind kind) =>
        kind switch
        {
            BuildingKind.Farm => FarmFrame,
            BuildingKind.House => HouseFrame,
            BuildingKind.Fence => FenceFrame,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, message: null),
        };

    /// <summary>
    /// Called once per frame by the JavaScript <c>requestAnimationFrame</c> pump.
    /// The <paramref name="timestampMs"/> is the <c>DOMHighResTimeStamp</c> value from the browser;
    /// <paramref name="aspectRatio"/> is the canvas' current CSS width / height.
    /// </summary>
    [JSInvokable]
    public void Tick(double timestampMs, double aspectRatio)
    {
        _timeSource.Advance(timestampMs);
        UpdateCameraZoom((float)aspectRatio);
        HandlePlacementInput((float)aspectRatio);
        Render((float)aspectRatio);
    }

    /// <summary>Which <see cref="BuildingKind"/> the next click/tap places; mirrors <see cref="_selectedKind"/>
    /// for the host page's on-screen building picker (mobile has no 1/2/3 keys).</summary>
    public BuildingKind SelectedKind => _selectedKind;

    /// <summary>Sets which <see cref="BuildingKind"/> a click/tap places next. Called from the
    /// host page's on-screen building picker buttons; the 1/2/3 keyboard shortcuts set the same
    /// field directly in <see cref="HandlePlacementInput"/>.</summary>
    public void SelectBuilding(BuildingKind kind) => _selectedKind = kind;

    private void Render(float aspectRatio)
    {
        _renderSurface.BeginFrame();

        // UnifiedRenderSystem picks the first Camera2D found and falls back to an identity view
        // when none exists (see Yaeger.Graphics.Camera2D's remarks); mirrored here since that
        // system itself isn't available in the WASM build.
        if (TryGetCamera(out var camera))
            _renderSurface.SetCamera(camera.ViewProjection(aspectRatio));

        // Ground first, so the buildings below draw on top of the cells they occupy.
        foreach (var (_, tilemap, transform) in _world.Query<Tilemap, Transform2D>())
            RenderTilemap(tilemap, transform);

        foreach (var (_, building, sheet, transform) in _world.Query<Building, SpriteSheet, Transform2D>())
        {
            var (uvMin, uvMax) = sheet.GetFrameUv(FrameFor(building.Kind));
            _renderSurface.SubmitQuad(
                transform.TransformMatrix,
                sheet.TexturePath,
                uvMin,
                uvMax,
                sheet.Tint.ToVector4()
            );
        }

        _renderSurface.EndFrame();
    }

    /// <summary>
    /// Draws every non-empty cell of <paramref name="map"/> as one quad, mirroring the per-tile
    /// transform math of <c>Yaeger.Systems.UnifiedRenderSystem.SubmitTilemap</c> — that system
    /// isn't available here since its camera support pulls in <c>Yaeger</c>'s native/Silk.NET
    /// dependency, which the WASM build can't reference.
    /// </summary>
    private void RenderTilemap(Tilemap map, Transform2D transform)
    {
        for (var row = 0; row < map.Height; row++)
        for (var column = 0; column < map.Width; column++)
        {
            var tileIndex = map.GetTile(column, row);
            if (tileIndex == Tilemap.EmptyTile)
                continue;

            var (uvMin, uvMax) = map.Tileset.GetTileUv(tileIndex);
            var local =
                Matrix4x4.CreateScale(map.TileSize.X, map.TileSize.Y, 1f)
                * Matrix4x4.CreateTranslation(
                    (column + 0.5f) * map.TileSize.X,
                    (map.Height - 1 - row + 0.5f) * map.TileSize.Y,
                    0f
                );

            _renderSurface.SubmitQuad(
                local * transform.TransformMatrix,
                map.Tileset.TexturePath,
                uvMin,
                uvMax,
                map.Tint.ToVector4()
            );
        }
    }
}
