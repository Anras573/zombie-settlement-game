using Microsoft.JSInterop;
using Yaeger.Browser;
using Yaeger.ECS;
using Yaeger.Platform;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// First real scene: a static ground tilemap plus a handful of placed buildings sitting on it,
/// all drawn from the same Kenney sheet. Owns the ECS world and drives the game loop; each tick
/// is invoked by JavaScript's <c>requestAnimationFrame</c> via <see cref="Tick"/>. The scene
/// itself, placement rules, the resource economy, the camera, input handling, and rendering each
/// live in their own type (<see cref="SettlementScene"/>, <see cref="BuildingPlacement"/>,
/// <see cref="SettlementStockpile"/>, <see cref="SettlementCamera"/>,
/// <see cref="PlacementController"/>, <see cref="SettlementRenderer"/>) — this class just wires
/// them together each tick and exposes what the host Razor page needs.
/// </summary>
public sealed class GameController
{
    private readonly World _world;
    private readonly BrowserRenderSurface _renderSurface;
    private readonly BrowserTimeSource _timeSource = new();
    private readonly IInputState _input = new BrowserInputState();
    private readonly SettlementStockpile _stockpile = new();
    private readonly PlacementController _placement = new();

    public GameController(BrowserRenderSurface renderSurface)
    {
        _renderSurface = renderSurface;
        _world = new World();
        SettlementScene.Build(_world);
    }

    /// <summary>
    /// Called once per frame by the JavaScript <c>requestAnimationFrame</c> pump.
    /// The <paramref name="timestampMs"/> is the <c>DOMHighResTimeStamp</c> value from the browser;
    /// <paramref name="aspectRatio"/> is the canvas' current CSS width / height.
    /// </summary>
    [JSInvokable]
    public void Tick(double timestampMs, double aspectRatio)
    {
        _timeSource.Advance(timestampMs);
        SettlementCamera.UpdateZoom(_world, (float)aspectRatio);
        _stockpile.UpdateFoodProduction(_world, _timeSource.DeltaTime);
        _stockpile.UpdateWoodProduction(_world, _timeSource.DeltaTime);
        _placement.HandleInput(_world, _input, (float)aspectRatio, _stockpile);
        SettlementRenderer.Render(_world, _renderSurface, (float)aspectRatio);
    }

    /// <summary>Which <see cref="BuildingKind"/> the next click/tap places; mirrors the host
    /// page's on-screen building picker (mobile has no 1/2/3 keys).</summary>
    public BuildingKind SelectedKind => _placement.SelectedKind;

    /// <summary>Sets which <see cref="BuildingKind"/> a click/tap places next. Called from the
    /// host page's on-screen building picker buttons; the 1/2/3/4 keyboard shortcuts set the same
    /// selection directly in <see cref="PlacementController.HandleInput"/>.</summary>
    public void SelectBuilding(BuildingKind kind) => _placement.SelectBuilding(kind);

    /// <summary>Wood currently in the stockpile; read by the host page's HUD.</summary>
    public int Wood => _stockpile.Wood;

    /// <summary>Food harvested so far; read by the host page's HUD.</summary>
    public int Food => _stockpile.Food;

    /// <summary>Wood a building of the given <paramref name="kind"/> costs to place; read by the
    /// host page's HUD to grey out buttons the settlement can't currently afford.</summary>
    public static int WoodCost(BuildingKind kind) => BuildingCatalog.WoodCostFor(kind);
}
