using System;
using Yaeger.ECS;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// Spends wood to restore a damaged building's <see cref="BuildingHealth"/> — the player's only
/// counter to a <see cref="ZombieController"/> attack that hasn't finished a building off. Mirrors
/// <see cref="BuildingPlacement"/>'s "try, spend on success" shape.
/// </summary>
public static class BuildingRepair
{
    /// <summary>Wood spent per repair action.</summary>
    public const int WoodCost = 1;

    /// <summary>Hit points restored per repair action, clamped to the building's max.</summary>
    public const int HealthPerRepair = 2;

    /// <summary>
    /// Repairs the building at grid cell (<paramref name="column"/>, <paramref name="row"/>) by
    /// <see cref="HealthPerRepair"/>, spending <see cref="WoodCost"/> wood from
    /// <paramref name="stockpile"/>. Returns <c>false</c> and changes nothing if there's no
    /// building there, it's already at full health, or the stockpile can't afford it — so
    /// repeatedly clicking a healthy building or an empty cell is a harmless no-op.
    /// </summary>
    public static bool TryRepairBuildingAt(
        World world,
        SettlementStockpile stockpile,
        int column,
        int row
    )
    {
        if (!BuildingPlacement.TryGetBuildingAt(world, column, row, out var entity))
            return false;

        var health = world.GetComponent<BuildingHealth>(entity);
        if (health.Current >= health.Max)
            return false;

        if (!stockpile.TrySpendWood(WoodCost))
            return false;

        var repaired = Math.Min(health.Max, health.Current + HealthPerRepair);
        world.AddComponent(entity, health with { Current = repaired });
        return true;
    }
}
