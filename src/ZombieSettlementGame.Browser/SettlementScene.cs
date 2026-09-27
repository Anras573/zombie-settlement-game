using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// Builds the settlement's starting scene: the ground tilemap, the forest overlay, the three free
/// starter buildings, and the camera. Runs once, from <see cref="GameController"/>'s constructor.
/// </summary>
public static class SettlementScene
{
    /// <summary>Interior columns/rows (inclusive) of the forest patch a sawmill must be built next
    /// to — a 3x3 block clear of the starter buildings at (2,2)/(4,4)/(7,7).</summary>
    private const int ForestMinColumn = 1;
    private const int ForestMaxColumn = 3;
    private const int ForestMinRow = 6;
    private const int ForestMaxRow = 8;

    public static void Build(World world)
    {
        var tileset = new Tileset(TileSheet.SheetPath, TileSheet.Columns, TileSheet.Rows);
        var tilemap = new Tilemap(
            tileset,
            SettlementGrid.Width,
            SettlementGrid.Height,
            tileSize: new Vector2(SettlementGrid.TileWorldSize, SettlementGrid.TileWorldSize)
        );
        for (var row = 0; row < SettlementGrid.Height; row++)
        for (var column = 0; column < SettlementGrid.Width; column++)
        {
            var onBorder =
                row == 0
                || row == SettlementGrid.Height - 1
                || column == 0
                || column == SettlementGrid.Width - 1;
            tilemap.SetTile(column, row, onBorder ? TileSheet.WallTile : TileSheet.GrassTile);
        }

        var ground = world.CreateEntity("ground");
        world.AddComponent(ground, new Transform2D(SettlementGrid.Origin));
        world.AddComponent(ground, tilemap);

        // A second, sparse tilemap layered over the grass rather than replacing it: the tree
        // sprite's canopy doesn't fill its 16x16 cell, so drawing it directly on the ground tile
        // (as WallTile/GrassTile itself do) would leave the transparent corners showing the empty
        // canvas instead of grass. Rendered strictly after "ground" — see SettlementRenderer.Render.
        var forestTilemap = new Tilemap(
            tileset,
            SettlementGrid.Width,
            SettlementGrid.Height,
            tileSize: new Vector2(SettlementGrid.TileWorldSize, SettlementGrid.TileWorldSize)
        );
        for (var row = ForestMinRow; row <= ForestMaxRow; row++)
        for (var column = ForestMinColumn; column <= ForestMaxColumn; column++)
            forestTilemap.SetTile(column, row, TileSheet.ForestTile);

        var forest = world.CreateEntity("forest");
        world.AddComponent(forest, new Transform2D(SettlementGrid.Origin));
        world.AddComponent(forest, forestTilemap);

        BuildingPlacement.PlaceBuilding(world, BuildingKind.Farm, column: 4, row: 4, tag: "building-farm");
        BuildingPlacement.PlaceBuilding(world, BuildingKind.House, column: 2, row: 2, tag: "building-house");
        BuildingPlacement.PlaceBuilding(world, BuildingKind.Fence, column: 7, row: 7, tag: "building-fence");

        var gridCenter = new Vector2(SettlementGrid.Width / 2f, SettlementGrid.Height / 2f);
        var camera = world.CreateEntity("camera");
        world.AddComponent(camera, new Camera2D(gridCenter, SettlementCamera.ComputeZoom(aspectRatio: 1f)));
    }
}
