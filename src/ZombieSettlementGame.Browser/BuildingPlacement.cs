using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// Creates building entities and enforces where each <see cref="BuildingKind"/> may be placed:
/// terrain rules (never on forest, a sawmill only beside it), cell occupancy, and — for
/// player-placed buildings — whether the settlement's <see cref="SettlementStockpile"/> can
/// afford it.
/// </summary>
public static class BuildingPlacement
{
    /// <summary>
    /// Places a <see cref="Building"/> of the given <paramref name="kind"/> on grid cell
    /// (<paramref name="column"/>, <paramref name="row"/>), using the same cell-centre math the
    /// tilemap itself uses (see <c>Yaeger.Systems.UnifiedRenderSystem.SubmitTilemap</c>) so it
    /// lines up with the grid exactly one layer above the ground. Free — callers that need to
    /// charge wood for it go through <see cref="TryPlaceBuilding"/> instead.
    /// </summary>
    public static void PlaceBuilding(
        World world,
        BuildingKind kind,
        int column,
        int row,
        string? tag = null
    )
    {
        var building = tag is null ? world.CreateEntity() : world.CreateEntity(tag);
        world.AddComponent(
            building,
            new Transform2D(
                SettlementGrid.Origin
                    + new Vector2(column + 0.5f, SettlementGrid.Height - 1 - row + 0.5f)
                        * SettlementGrid.TileWorldSize,
                scale: new Vector2(SettlementGrid.TileWorldSize, SettlementGrid.TileWorldSize)
            )
        );
        world.AddComponent(
            building,
            new SpriteSheet(TileSheet.SheetPath, TileSheet.Columns, TileSheet.Rows)
        );
        world.AddComponent(building, new Building(kind, column, row));
        var maxHealth = BuildingCatalog.MaxHealthFor(kind);
        world.AddComponent(building, new BuildingHealth(maxHealth, maxHealth));

        if (kind == BuildingKind.Farm)
            world.AddComponent(
                building,
                new FoodProducer(SettlementStockpile.FoodProductionIntervalSeconds, Elapsed: 0f)
            );
        else if (kind == BuildingKind.Sawmill)
            world.AddComponent(
                building,
                new WoodProducer(SettlementStockpile.WoodProductionIntervalSeconds, Elapsed: 0f)
            );
    }

    /// <summary>
    /// Spends the wood <see cref="BuildingCatalog.WoodCostFor"/> a building of <paramref name="kind"/>
    /// costs from <paramref name="stockpile"/> and places it at (<paramref name="column"/>,
    /// <paramref name="row"/>) — or does nothing and returns <c>false</c> if the stockpile can't
    /// cover the cost, so placement is a real economy choice rather than free.
    /// </summary>
    public static bool TryPlaceBuilding(
        World world,
        SettlementStockpile stockpile,
        BuildingKind kind,
        int column,
        int row
    )
    {
        if (!stockpile.TrySpendWood(BuildingCatalog.WoodCostFor(kind)))
            return false;

        PlaceBuilding(world, kind, column, row);
        return true;
    }

    /// <summary>The sparse overlay tilemap forest tiles live on (see
    /// <see cref="SettlementScene.Build"/>) — separate from "ground" so grass shows through a tree
    /// sprite's transparent margins.</summary>
    private static Tilemap GetForestTilemap(World world) =>
        world.GetComponent<Tilemap>(world.GetEntity("forest"));

    /// <summary>Whether grid cell (<paramref name="column"/>, <paramref name="row"/>) is standing
    /// forest — never buildable itself (the trees are in the way), but what a sawmill needs to sit
    /// next to.</summary>
    public static bool IsForestTile(World world, int column, int row) =>
        GetForestTilemap(world).GetTile(column, row) == TileSheet.ForestTile;

    /// <summary>Whether any of the four orthogonal neighbours of (<paramref name="column"/>,
    /// <paramref name="row"/>) is forest — the placement rule for a <see cref="BuildingKind.Sawmill"/>,
    /// which harvests the trees beside it rather than clearing the ground it stands on.</summary>
    public static bool IsAdjacentToForest(World world, int column, int row) =>
        IsForestTile(world, column - 1, row)
        || IsForestTile(world, column + 1, row)
        || IsForestTile(world, column, row - 1)
        || IsForestTile(world, column, row + 1);

    /// <summary>
    /// Whether a building of <paramref name="kind"/> may be placed on grid cell
    /// (<paramref name="column"/>, <paramref name="row"/>) given terrain alone (occupancy and cost
    /// are checked separately): never directly on forest, and a sawmill only next to it.
    /// </summary>
    public static bool CanPlaceOnTerrain(World world, BuildingKind kind, int column, int row) =>
        !IsForestTile(world, column, row)
        && (kind != BuildingKind.Sawmill || IsAdjacentToForest(world, column, row));

    /// <summary>Whether grid cell (<paramref name="column"/>, <paramref name="row"/>) already has
    /// a building on it.</summary>
    public static bool IsCellOccupied(World world, int column, int row) =>
        TryGetBuildingAt(world, column, row, out _);

    /// <summary>Finds the building entity occupying grid cell (<paramref name="column"/>,
    /// <paramref name="row"/>), if any — the shared lookup <see cref="IsCellOccupied"/> and
    /// <see cref="BuildingRepair"/> both need.</summary>
    public static bool TryGetBuildingAt(World world, int column, int row, out Entity entity)
    {
        foreach (var (candidate, building, _) in world.Query<Building, Transform2D>())
        {
            if (building.Column != column || building.Row != row)
                continue;

            entity = candidate;
            return true;
        }

        entity = default;
        return false;
    }
}
