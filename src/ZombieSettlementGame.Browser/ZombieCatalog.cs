using Yaeger.Graphics;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// Rendering data for zombies. The sheet (see <see cref="TileSheet"/>) has no monster or character
/// sprites — it's purely terrain, furniture, and dungeon dressing — so a zombie reuses one of its
/// plant frames, darkened into a shambling shape via <see cref="Tint"/> rather than pulling in a
/// whole second sprite sheet for one enemy.
/// </summary>
public static class ZombieCatalog
{
    /// <summary>Row 9, column 19 of the sheet: a round bush, otherwise bright green — tinted dark
    /// and sickly by <see cref="Tint"/> to read as a shambling horde rather than foliage.</summary>
    public const int Frame = 9 * TileSheet.Columns + 19;

    /// <summary>Dulls the bush frame's usual bright green down to a sickly, corpse-like shade.</summary>
    public static readonly Color Tint = new(70, 90, 60);
}
