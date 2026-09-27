namespace ZombieSettlementGame.Browser;

/// <summary>
/// Kenney's "Roguelike/RPG pack" (CC0, https://kenney.nl/assets/roguelike-rpg-pack) — a single
/// 16x16-tile sheet, 1px margin between tiles, every ground tile and building sprite in the
/// settlement is drawn from. See <c>wwwroot/assets/kenney/roguelike-rpg-pack/LICENSE.txt</c>.
/// </summary>
public static class TileSheet
{
    public const string SheetPath = "assets/kenney/roguelike-rpg-pack/roguelikeSheet_transparent.png";
    public const int Columns = 57;
    public const int Rows = 31;

    /// <summary>Row 0, column 5 of the sheet: plain grass, used to fill the ground.</summary>
    public const int GrassTile = 5;

    /// <summary>Row 2, column 5 of the sheet: a brick wall, used as the settlement's boundary.</summary>
    public const int WallTile = 2 * Columns + 5;

    /// <summary>Row 9, column 18 of the sheet: a pine tree, used for the forest patch a sawmill
    /// must be built next to.</summary>
    public const int ForestTile = 9 * Columns + 18;
}
