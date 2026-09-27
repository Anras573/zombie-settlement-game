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
    /// <summary>Whether a click places a new building or repairs the one already on the clicked
    /// cell — toggled by the <b>R</b> key or the host page's Repair button; picking a building
    /// kind (a 1/2/3/4 key or the picker) switches back to <see cref="Mode.Build"/>.</summary>
    private enum Mode
    {
        Build,
        Repair,
    }

    /// <summary>Which <see cref="BuildingKind"/> a click places next; chosen with the 1/2/3/4 keys
    /// or the host page's on-screen building picker.</summary>
    private BuildingKind _selectedKind = BuildingKind.Farm;

    private Mode _mode = Mode.Build;

    /// <summary>Left-mouse state from the previous tick, so a click places once, not once per
    /// frame the button is held.</summary>
    private bool _wasPlacePressed;

    /// <summary>Which <see cref="BuildingKind"/> the next click/tap places; mirrors
    /// <see cref="_selectedKind"/> for the host page's on-screen building picker (mobile has no
    /// 1/2/3 keys).</summary>
    public BuildingKind SelectedKind => _selectedKind;

    /// <summary>Whether a click currently repairs a building instead of placing one; mirrors
    /// <see cref="_mode"/> for the host page's Repair toggle button.</summary>
    public bool IsRepairMode => _mode == Mode.Repair;

    /// <summary>Sets which <see cref="BuildingKind"/> a click/tap places next, and switches back
    /// to placing (out of repair mode). Called from the host page's on-screen building picker
    /// buttons; the 1/2/3/4 keyboard shortcuts do the same directly in <see cref="HandleInput"/>.</summary>
    public void SelectBuilding(BuildingKind kind)
    {
        _selectedKind = kind;
        _mode = Mode.Build;
    }

    /// <summary>Switches a click/tap to repair whichever building it lands on instead of placing
    /// one. Called from the host page's Repair button; the <b>R</b> key does the same directly in
    /// <see cref="HandleInput"/>.</summary>
    public void SelectRepairMode() => _mode = Mode.Repair;

    /// <summary>
    /// Reads the 1/2/3/4 keys to change which <see cref="BuildingKind"/> a click places and the
    /// <b>R</b> key to switch to repairing instead, then acts on a left click over the grid —
    /// edge-detected against <see cref="_wasPlacePressed"/> so a held button acts once, not every
    /// tick. In <see cref="Mode.Build"/> a click places <see cref="_selectedKind"/> on an empty
    /// interior cell whose terrain allows it (see <see cref="BuildingPlacement.CanPlaceOnTerrain"/>);
    /// in <see cref="Mode.Repair"/> it spends wood restoring whatever building is on the clicked
    /// cell via <see cref="BuildingRepair.TryRepairBuildingAt"/>.
    /// </summary>
    public void HandleInput(
        World world,
        IInputState input,
        float aspectRatio,
        SettlementStockpile stockpile
    )
    {
        if (input.IsKeyPressed(Keys.Num1))
            SelectBuilding(BuildingKind.Farm);
        else if (input.IsKeyPressed(Keys.Num2))
            SelectBuilding(BuildingKind.House);
        else if (input.IsKeyPressed(Keys.Num3))
            SelectBuilding(BuildingKind.Fence);
        else if (input.IsKeyPressed(Keys.Num4))
            SelectBuilding(BuildingKind.Sawmill);
        else if (input.IsKeyPressed(Keys.R))
            SelectRepairMode();

        var isPlacePressed = input.IsMouseButtonPressed(MouseButton.Left);
        var justClicked = isPlacePressed && !_wasPlacePressed;
        _wasPlacePressed = isPlacePressed;

        if (
            !justClicked
            || !SettlementCamera.TryGetCamera(world, out var camera)
            || !SettlementCamera.TryScreenToCell(
                camera,
                aspectRatio,
                input.MousePositionNdc,
                out var column,
                out var row
            )
        )
            return;

        if (_mode == Mode.Repair)
        {
            BuildingRepair.TryRepairBuildingAt(world, stockpile, column, row);
            return;
        }

        if (
            !BuildingPlacement.IsCellOccupied(world, column, row)
            && BuildingPlacement.CanPlaceOnTerrain(world, _selectedKind, column, row)
        )
            BuildingPlacement.TryPlaceBuilding(world, stockpile, _selectedKind, column, row);
    }
}
