using System.Numerics;
using Yaeger.Browser;
using Yaeger.ECS;
using Yaeger.Graphics;

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

        foreach (var (_, building, sheet, transform) in world.Query<Building, SpriteSheet, Transform2D>())
        {
            var (uvMin, uvMax) = sheet.GetFrameUv(BuildingCatalog.FrameFor(building.Kind));
            renderSurface.SubmitQuad(
                transform.TransformMatrix,
                sheet.TexturePath,
                uvMin,
                uvMax,
                sheet.Tint.ToVector4()
            );
        }

        renderSurface.EndFrame();
    }

    /// <summary>
    /// Draws every non-empty cell of <paramref name="map"/> as one quad, mirroring the per-tile
    /// transform math of <c>Yaeger.Systems.UnifiedRenderSystem.SubmitTilemap</c> — that system
    /// isn't available here since its camera support pulls in <c>Yaeger</c>'s native/Silk.NET
    /// dependency, which the WASM build can't reference.
    /// </summary>
    private static void RenderTilemap(BrowserRenderSurface renderSurface, Tilemap map, Transform2D transform)
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
