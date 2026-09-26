using System.Numerics;
using Microsoft.JSInterop;
using Yaeger.Browser;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Systems;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// Smallest possible Yaeger scene: one box bouncing around the canvas. Owns the ECS world
/// and drives the game loop; each tick is invoked by JavaScript's
/// <c>requestAnimationFrame</c> via <see cref="Tick"/>.
/// </summary>
public sealed class GameController
{
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
        var box = _world.CreateEntity("box");
        _world.AddComponent(
            box,
            new Transform2D(new Vector2(0f, 0f), scale: new Vector2(0.2f, 0.2f))
        );
        _world.AddComponent(box, new Sprite("", new Color(80, 200, 255)));
        _world.AddComponent(box, new Velocity2D(0.5f, 0.35f));
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

        foreach (var (_, sprite, transform) in _world.Query<Sprite, Transform2D>())
            _renderSurface.SubmitQuad(
                transform.TransformMatrix,
                sprite.TexturePath,
                sprite.Tint.ToVector4()
            );

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
