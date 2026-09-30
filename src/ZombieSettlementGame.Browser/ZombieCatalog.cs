using Yaeger.Graphics;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// Rendering data for zombies. The terrain sheet (see <see cref="TileSheet"/>) has no monster or
/// character sprites, so zombies are drawn from Kenney's "Roguelike Characters" pack
/// (CC0, https://kenney.nl/assets/roguelike-characters) — a 16x16-tile sheet, 1px margin between
/// tiles. See <c>wwwroot/assets/kenney/roguelike-characters/LICENSE.txt</c>.
/// </summary>
public static class ZombieCatalog
{
    public const string SheetPath = "assets/kenney/roguelike-characters/roguelikeChar_transparent.png";
    public const int Columns = 54;
    public const int Rows = 12;

    /// <summary>Row 3, column 0 of the sheet: a green-skinned, tusked brute.</summary>
    public const int Frame = 3 * Columns;

    /// <summary>The frame is already the right colour, so it's drawn untinted.</summary>
    public static readonly Color Tint = Color.White;
}
