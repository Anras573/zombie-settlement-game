using Yaeger.ECS;
using Yaeger.Input;
using Yaeger.Platform;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// Reads placement input (which <see cref="BuildingKind"/> is selected, and clicks/taps to place
/// it) and turns it into a <see cref="BuildingPlacement.TryPlaceBuilding"/> call. Owns the
/// selection state the host page's HUD mirrors and the click edge-detection state a held mouse
/// button needs.
/// </summary>
public sealed class PlacementController
{
    /// <summary>Which <see cref="BuildingKind"/> a click places next; chosen with the 1/2/3/4 keys
    /// or the host page's on-screen building picker.</summary>
    private BuildingKind _selectedKind = BuildingKind.Farm;

    /// <summary>Left-mouse state from the previous tick, so a click places once, not once per
    /// frame the button is held.</summary>
    private bool _wasPlacePressed;

    /// <summary>Which <see cref="BuildingKind"/> the next click/tap places; mirrors
    /// <see cref="_selectedKind"/> for the host page's on-screen building picker (mobile has no
    /// 1/2/3 keys).</summary>
    public BuildingKind SelectedKind => _selectedKind;

    /// <summary>Sets which <see cref="BuildingKind"/> a click/tap places next. Called from the
    /// host page's on-screen building picker buttons; the 1/2/3/4 keyboard shortcuts set the same
    /// field directly in <see cref="HandleInput"/>.</summary>
    public void SelectBuilding(BuildingKind kind) => _selectedKind = kind;

    /// <summary>
    /// Reads the 1/2/3/4 keys to change which <see cref="BuildingKind"/> a click places, and
    /// places one on a left click over an empty interior cell whose terrain allows it (see
    /// <see cref="BuildingPlacement.CanPlaceOnTerrain"/>) — edge-detected against
    /// <see cref="_wasPlacePressed"/> so a held button places once, not every tick.
    /// </summary>
    public void HandleInput(World world, IInputState input, float aspectRatio, SettlementStockpile stockpile)
    {
        if (input.IsKeyPressed(Keys.Num1))
            _selectedKind = BuildingKind.Farm;
        else if (input.IsKeyPressed(Keys.Num2))
            _selectedKind = BuildingKind.House;
        else if (input.IsKeyPressed(Keys.Num3))
            _selectedKind = BuildingKind.Fence;
        else if (input.IsKeyPressed(Keys.Num4))
            _selectedKind = BuildingKind.Sawmill;

        var isPlacePressed = input.IsMouseButtonPressed(MouseButton.Left);
        var justClicked = isPlacePressed && !_wasPlacePressed;
        _wasPlacePressed = isPlacePressed;

        if (
            justClicked
            && SettlementCamera.TryGetCamera(world, out var camera)
            && SettlementCamera.TryScreenToCell(
                camera,
                aspectRatio,
                input.MousePositionNdc,
                out var column,
                out var row
            )
            && !BuildingPlacement.IsCellOccupied(world, column, row)
            && BuildingPlacement.CanPlaceOnTerrain(world, _selectedKind, column, row)
        )
            BuildingPlacement.TryPlaceBuilding(world, stockpile, _selectedKind, column, row);
    }
}
