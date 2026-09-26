using System.Numerics;
using Microsoft.JSInterop;
using Yaeger.Browser;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Systems;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// Smallest possible Yaeger scene: one textured tile bouncing around the canvas, proving the
/// texture-loading path (JS <c>Image</c> fetch → WebGL texture, see <c>yaeger-browser.js</c>'s
/// <c>getOrLoadTexture</c>) works end-to-end in the browser, not just flat-tinted quads. Owns
/// the ECS world and drives the game loop; each tick is invoked by JavaScript's
/// <c>requestAnimationFrame</c> via <see cref="Tick"/>.
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

    /// <summary>Row 6, column 0 of the sheet: a planted crop patch.</summary>
    private const int FarmPlotFrame = 6 * TileSheetColumns;

    private readonly World _world;
    private readonly BrowserRenderSurface _renderSurface;
    private readonly BoxBounceSystem _bounceSystem;
    private readonly BrowserTimeSource _timeSource = new();

    public GameController(BrowserRenderSurface renderSurface)
    {
        _renderSurface = renderSurface;
        _world = new World();
        _bounceSystem = new BoxBounceSystem(_world);
        BuildScene();
    }

    private void BuildScene()
    {
        var tile = _world.CreateEntity("tile");
        _world.AddComponent(
            tile,
            new Transform2D(new Vector2(0f, 0f), scale: new Vector2(0.2f, 0.2f))
        );
        _world.AddComponent(tile, new SpriteSheet(TileSheetPath, TileSheetColumns, TileSheetRows));
        _world.AddComponent(tile, new Velocity2D(0.5f, 0.35f));
    }

    /// <summary>
    /// Called once per frame by the JavaScript <c>requestAnimationFrame</c> pump.
    /// The <paramref name="timestampMs"/> is the <c>DOMHighResTimeStamp</c> value from the browser.
    /// </summary>
    [JSInvokable]
    public void Tick(double timestampMs)
    {
        _timeSource.Advance(timestampMs);

        _bounceSystem.Update(_timeSource.DeltaTime);
        Render();
    }

    private void Render()
    {
        _renderSurface.BeginFrame();

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
}

/// <summary>
/// Moves every entity that has a <see cref="Velocity2D"/> and bounces it off the canvas'
/// NDC edges (±1 on both axes).
/// </summary>
internal sealed class BoxBounceSystem(World world) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        foreach (var (entity, velocity, transform) in world.Query<Velocity2D, Transform2D>())
        {
            var pos = transform.Position;
            var vel = velocity.Linear;
            var halfScale = transform.Scale * 0.5f;

            pos += vel * deltaTime;

            if (pos.X - halfScale.X < -1f || pos.X + halfScale.X > 1f)
            {
                vel.X = -vel.X;
                pos.X = Math.Clamp(pos.X, -1f + halfScale.X, 1f - halfScale.X);
            }

            if (pos.Y - halfScale.Y < -1f || pos.Y + halfScale.Y > 1f)
            {
                vel.Y = -vel.Y;
                pos.Y = Math.Clamp(pos.Y, -1f + halfScale.Y, 1f - halfScale.Y);
            }

            var newTransform = transform;
            newTransform.Position = pos;
            world.AddComponent(entity, newTransform);

            var newVelocity = velocity;
            newVelocity.Linear = vel;
            world.AddComponent(entity, newVelocity);
        }
    }
}
