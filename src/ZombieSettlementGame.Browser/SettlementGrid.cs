using System.Numerics;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// The settlement's ground grid layout, shared by scene building, placement rules, and the
/// camera's screen-to-cell math — the one place all three agree on grid size, world-unit scale,
/// and where the grid sits in world space.
/// </summary>
public static class SettlementGrid
{
    /// <summary>Ground grid size, in tiles.</summary>
    public const int Width = 10;
    public const int Height = 10;

    /// <summary>Size of one tile, in world units — one tile is one unit, unlike the ad hoc
    /// NDC-filling size used before there was a camera.</summary>
    public const float TileWorldSize = 1f;

    public static readonly Vector2 Origin = Vector2.Zero;
}
