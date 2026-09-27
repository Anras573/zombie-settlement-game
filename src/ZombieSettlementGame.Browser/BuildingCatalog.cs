using System;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// Per-<see cref="BuildingKind"/> data: which sprite-sheet frame represents it and how much wood
/// it costs to place. The two live together because both are simple switches over the same enum,
/// not because they're conceptually the same thing.
/// </summary>
public static class BuildingCatalog
{
    /// <summary>Row 6, column 0 of the sheet: a planted crop patch.</summary>
    private const int FarmFrame = 6 * TileSheet.Columns;

    /// <summary>Row 7, column 45 of the sheet: a wooden double door.</summary>
    private const int HouseFrame = 7 * TileSheet.Columns + 45;

    /// <summary>Row 18, column 48 of the sheet: a wooden fence lattice.</summary>
    private const int FenceFrame = 18 * TileSheet.Columns + 48;

    /// <summary>Row 22, column 53 of the sheet: a bundle of cut logs, representing a sawmill.</summary>
    private const int SawmillFrame = 22 * TileSheet.Columns + 53;

    /// <summary>Sprite-sheet frame that represents each <see cref="BuildingKind"/>.</summary>
    public static int FrameFor(BuildingKind kind) =>
        kind switch
        {
            BuildingKind.Farm => FarmFrame,
            BuildingKind.House => HouseFrame,
            BuildingKind.Fence => FenceFrame,
            BuildingKind.Sawmill => SawmillFrame,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, message: null),
        };

    /// <summary>Wood spent placing one building of the given <paramref name="kind"/>.</summary>
    public static int WoodCostFor(BuildingKind kind) =>
        kind switch
        {
            BuildingKind.Farm => 3,
            BuildingKind.House => 5,
            BuildingKind.Fence => 2,
            BuildingKind.Sawmill => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, message: null),
        };
}
