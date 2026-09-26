using System.Numerics;
using Microsoft.JSInterop;
using Yaeger.Browser;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// First real scene: a static ground tilemap plus one building entity sitting on it, both drawn
/// from the same Kenney sheet. Owns the ECS world and drives the game loop; each tick is invoked
/// by JavaScript's <c>requestAnimationFrame</c> via <see cref="Tick"/>.
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

    /// <summary>Row 6, column 0 of the sheet: a planted crop patch — the first placed building.</summary>
    private const int FarmPlotFrame = 6 * TileSheetColumns;

    /// <summary>Ground grid size, in tiles.</summary>
    private const int GridWidth = 10;
    private const int GridHeight = 10;

    /// <summary>Grid cell the farm plot is placed on.</summary>
    private const int FarmPlotColumn = 4;
    private const int FarmPlotRow = 4;

    /// <summary>Size of one tile, in world units — one tile is one unit, unlike the ad hoc
    /// NDC-filling size used before there was a camera.</summary>
    private const float TileWorldSize = 1f;

    /// <summary>
    /// Camera zoom that fits the <see cref="GridHeight"/>-tall grid vertically with a
    /// one-tile margin: visible half-height is <c>1 / Zoom</c> world units (see
    /// <see cref="Camera2D.ViewProjection"/>), so this gives <c>GridHeight / 2 + 1</c>.
    /// </summary>
    private const float CameraZoom = 1f / (GridHeight / 2f + 1f);

    private static readonly Vector2 GroundOrigin = Vector2.Zero;
    private static readonly Vector2 GridCenter = new(GridWidth / 2f, GridHeight / 2f);

    private readonly World _world;
    private readonly BrowserRenderSurface _renderSurface;
    private readonly BrowserTimeSource _timeSource = new();

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

        // Uses the same cell-centre math the tilemap itself uses (see
        // Yaeger.Systems.UnifiedRenderSystem.SubmitTilemap), so it lines up with the grid exactly
        // one layer above the ground.
        var farmPlot = _world.CreateEntity("farm-plot");
        _world.AddComponent(
            farmPlot,
            new Transform2D(
                GroundOrigin
                    + new Vector2(
                        FarmPlotColumn + 0.5f,
                        GridHeight - 1 - FarmPlotRow + 0.5f
                    ) * TileWorldSize,
                scale: new Vector2(TileWorldSize, TileWorldSize)
            )
        );
        _world.AddComponent(farmPlot, new SpriteSheet(TileSheetPath, TileSheetColumns, TileSheetRows));

        var camera = _world.CreateEntity("camera");
        _world.AddComponent(camera, new Camera2D(GridCenter, CameraZoom));
    }

    /// <summary>
    /// Called once per frame by the JavaScript <c>requestAnimationFrame</c> pump.
    /// The <paramref name="timestampMs"/> is the <c>DOMHighResTimeStamp</c> value from the browser;
    /// <paramref name="aspectRatio"/> is the canvas' current CSS width / height.
    /// </summary>
    [JSInvokable]
    public void Tick(double timestampMs, double aspectRatio)
    {
        _timeSource.Advance(timestampMs);
        Render((float)aspectRatio);
    }

    private void Render(float aspectRatio)
    {
        _renderSurface.BeginFrame();

        // UnifiedRenderSystem picks the first Camera2D found and falls back to an identity view
        // when none exists (see Yaeger.Graphics.Camera2D's remarks); mirrored here since that
        // system itself isn't available in the WASM build. World has no single-component Query
        // overload, so the lookup goes through the same tag the entity was created with.
        if (
            _world.TryGetEntity("camera", out var cameraEntity)
            && _world.TryGetComponent<Camera2D>(cameraEntity, out var camera)
        )
            _renderSurface.SetCamera(camera.ViewProjection(aspectRatio));

        // Ground first, so the farm plot below draws on top of the cell it occupies.
        foreach (var (_, tilemap, transform) in _world.Query<Tilemap, Transform2D>())
            RenderTilemap(tilemap, transform);

        foreach (var (_, sheet, transform) in _world.Query<SpriteSheet, Transform2D>())
        {
            var (uvMin, uvMax) = sheet.GetFrameUv(FarmPlotFrame);
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
