using System.Numerics;
using Yaeger.Browser;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Platform;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// Draws one frame: the ground tilemap, any layered tilemaps (the forest overlay), then every
/// placed building on top, all through the same batched <see cref="BrowserRenderSurface"/> quad
/// path.
/// </summary>
public static class SettlementRenderer
{
    public static void Render(World world, BrowserRenderSurface renderSurface, float aspectRatio)
    {
        renderSurface.BeginFrame();

        // UnifiedRenderSystem picks the first Camera2D found and falls back to an identity view
        // when none exists (see Yaeger.Graphics.Camera2D's remarks); mirrored here since that
        // system itself isn't available in the WASM build.
        if (SettlementCamera.TryGetCamera(world, out var camera))
            renderSurface.SetCamera(camera.ViewProjection(aspectRatio));

        // Ground strictly before every other tilemap layer (so a layered decoration like the
        // forest overlay draws its transparent-cornered tiles over grass, not the empty canvas)
        // and before the buildings below, which draw on top of the cells they occupy. Looked up
        // by tag rather than relying on Query's enumeration order, which Dictionary<TKey,TValue>
        // doesn't contractually guarantee to match insertion order.
        var groundEntity = world.GetEntity("ground");
        RenderTilemap(
            renderSurface,
            world.GetComponent<Tilemap>(groundEntity),
            world.GetComponent<Transform2D>(groundEntity)
        );

        foreach (var (entity, tilemap, transform) in world.Query<Tilemap, Transform2D>())
        {
            if (entity == groundEntity)
                continue;
            RenderTilemap(renderSurface, tilemap, transform);
        }

        foreach (
            var (entity, building, sheet, transform) in world.Query<
                Building,
                SpriteSheet,
                Transform2D
            >()
        )
        {
            var (uvMin, uvMax) = sheet.GetFrameUv(BuildingCatalog.FrameFor(building.Kind));
            renderSurface.SubmitQuad(
                transform.TransformMatrix,
                sheet.TexturePath,
                uvMin,
                uvMax,
                DamageTint(world, entity, sheet)
            );
        }

        foreach (
            var (entity, _, sheet, transform) in world.Query<Zombie, SpriteSheet, Transform2D>()
        )
        {
            var (uvMin, uvMax) = sheet.GetFrameUv(ZombieCatalog.Frame);
            renderSurface.SubmitQuad(
                transform.TransformMatrix,
                sheet.TexturePath,
                uvMin,
                uvMax,
                ZombieDamageTint(world, entity, sheet)
            );
        }

        foreach (var (_, shot, _) in world.Query<ShotFlash, Transform2D>())
            ((IRenderSurface)renderSurface).SubmitLine(
                shot.From,
                shot.To,
                ShotThickness,
                ShotColor
            );

        renderSurface.EndFrame();
    }

    /// <summary>World-unit width and colour of a watchtower's shot line.</summary>
    private const float ShotThickness = 0.06f;

    private static readonly Vector4 ShotColor = new(1f, 0.9f, 0.3f, 1f);

    /// <summary>A building's tint bleeds toward this the more damaged it is (see
    /// <see cref="DamageTint"/>) — a cheap at-a-glance signal for which building needs
    /// <see cref="BuildingRepair"/> most, since nothing else marks health on screen.</summary>
    private static readonly Vector4 DamagedColor = new(0.85f, 0.1f, 0.1f, 1f);

    /// <summary>Blends <paramref name="sheet"/>'s own tint toward <see cref="DamagedColor"/> in
    /// proportion to how much of <paramref name="entity"/>'s <see cref="BuildingHealth"/> is
    /// missing — full health renders unchanged, a building on its last hit point renders almost
    /// entirely <see cref="DamagedColor"/>.</summary>
    private static Vector4 DamageTint(World world, Entity entity, SpriteSheet sheet)
    {
        var tint = sheet.Tint.ToVector4();
        if (!world.TryGetComponent<BuildingHealth>(entity, out var health) || health.Max <= 0)
            return tint;

        var missingFraction = 1f - (float)health.Current / health.Max;
        return Vector4.Lerp(tint, DamagedColor, missingFraction);
    }

    /// <summary>Like <see cref="DamageTint"/> but for a zombie's <see cref="ZombieHealth"/>, so a
    /// tower's hits are visible on the zombie it's wearing down.</summary>
    private static Vector4 ZombieDamageTint(World world, Entity entity, SpriteSheet sheet)
    {
        var tint = sheet.Tint.ToVector4();
        if (!world.TryGetComponent<ZombieHealth>(entity, out var health) || health.Max <= 0)
            return tint;

        var missingFraction = 1f - (float)health.Current / health.Max;
        return Vector4.Lerp(tint, DamagedColor, missingFraction);
    }

    /// <summary>
    /// Draws every non-empty cell of <paramref name="map"/> as one quad, mirroring the per-tile
    /// transform math of <c>Yaeger.Systems.UnifiedRenderSystem.SubmitTilemap</c> — that system
    /// isn't available here since its camera support pulls in <c>Yaeger</c>'s native/Silk.NET
    /// dependency, which the WASM build can't reference.
    /// </summary>
    private static void RenderTilemap(
        BrowserRenderSurface renderSurface,
        Tilemap map,
        Transform2D transform
    )
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

            renderSurface.SubmitQuad(
                local * transform.TransformMatrix,
                map.Tileset.TexturePath,
                uvMin,
                uvMax,
                map.Tint.ToVector4()
            );
        }
    }
}
