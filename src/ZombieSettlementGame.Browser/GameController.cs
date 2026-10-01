using Microsoft.JSInterop;
using Yaeger.Browser;
using Yaeger.ECS;
using Yaeger.Input;
using Yaeger.Platform;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// First real scene: a static ground tilemap plus a handful of placed buildings sitting on it,
/// all drawn from the same Kenney sheet. Owns the ECS world and drives the game loop; each tick
/// is invoked by JavaScript's <c>requestAnimationFrame</c> via <see cref="Tick"/>. The scene
/// itself, placement rules, repairs, the resource economy, the camera, input handling, the zombie
/// threat, and rendering each live in their own type (<see cref="SettlementScene"/>,
/// <see cref="BuildingPlacement"/>, <see cref="BuildingRepair"/>, <see cref="SettlementStockpile"/>,
/// <see cref="SettlementPopulation"/>, <see cref="SettlementCamera"/>, <see cref="PlacementController"/>, <see cref="ZombieController"/>,
/// <see cref="TowerController"/>, <see cref="SettlementRenderer"/>)
/// — this class just wires them together each tick and exposes what the host Razor page needs.
/// </summary>
public sealed class GameController
{
    private readonly BrowserRenderSurface _renderSurface;
    private readonly BrowserTimeSource _timeSource = new();
    private readonly IInputState _input = new BrowserInputState();

    // Everything below is per-run state, replaced wholesale by Restart.
    private World _world = new();
    private SettlementStockpile _stockpile = new();
    private SettlementPopulation _population = new();
    private PlacementController _placement = new();
    private ZombieController _zombies = new();
    private TowerController _towers = new();
    private float _survivedSeconds;

    public GameController(BrowserRenderSurface renderSurface)
    {
        _renderSurface = renderSurface;
        SettlementScene.Build(_world);
    }

    /// <summary>Why the game ended, or <see cref="GameOverReason.None"/> while it's running;
    /// read by the host page to show the game-over overlay.</summary>
    public GameOverReason GameOver { get; private set; }

    /// <summary>Seconds the settlement has survived this run; frozen once the game is over.</summary>
    public float SurvivedSeconds => _survivedSeconds;

    /// <summary>Throws away the finished run and starts a fresh settlement. Called from the host
    /// page's Play again button; <b>Space</b> does the same from <see cref="Tick"/>.</summary>
    public void Restart()
    {
        _world = new World();
        _stockpile = new SettlementStockpile();
        _population = new SettlementPopulation();
        _placement = new PlacementController();
        _zombies = new ZombieController();
        _towers = new TowerController();
        _survivedSeconds = 0f;
        GameOver = GameOverReason.None;
        SettlementScene.Build(_world);
    }

    /// <summary>Game over once no house is left standing, or once the settlement had residents and
    /// every one of them has left (starved) — a settlement still waiting on its first newcomer
    /// isn't lost.</summary>
    private GameOverReason CheckGameOver()
    {
        if (SettlementPopulation.HouseCount(_world) == 0)
            return GameOverReason.HousesDestroyed;

        return _population.HasHadResidents && _population.Residents == 0
            ? GameOverReason.Starved
            : GameOverReason.None;
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
        if (GameOver != GameOverReason.None && _input.IsKeyPressed(Keys.Space))
            Restart();

        SettlementCamera.UpdateZoom(_world, (float)aspectRatio);
        if (GameOver == GameOverReason.None)
        {
            var deltaTime = _timeSource.DeltaTime;
            _survivedSeconds += deltaTime;
            _stockpile.UpdateFoodProduction(_world, deltaTime);
            _stockpile.UpdateWoodProduction(_world, deltaTime);
            _population.Update(_world, _stockpile, deltaTime);
            _zombies.Update(_world, deltaTime);
            _towers.Update(_world, deltaTime);
            _placement.HandleInput(_world, _input, (float)aspectRatio, _stockpile);
            GameOver = CheckGameOver();
        }

        SettlementRenderer.Render(_world, _renderSurface, (float)aspectRatio);
    }

    /// <summary>Which <see cref="BuildingKind"/> the next click/tap places; mirrors the host
    /// page's on-screen building picker (mobile has no 1/2/3 keys).</summary>
    public BuildingKind SelectedKind => _placement.SelectedKind;

    /// <summary>Sets which <see cref="BuildingKind"/> a click/tap places next. Called from the
    /// host page's on-screen building picker buttons; the 1/2/3/4/5 keyboard shortcuts set the same
    /// selection directly in <see cref="PlacementController.HandleInput"/>.</summary>
    public void SelectBuilding(BuildingKind kind) => _placement.SelectBuilding(kind);

    /// <summary>Whether a click currently repairs a building instead of placing one; read by the
    /// host page to highlight the Repair button.</summary>
    public bool IsRepairMode => _placement.IsRepairMode;

    /// <summary>Switches a click/tap to repair whichever building it lands on next. Called from
    /// the host page's Repair button; the <b>R</b> key does the same directly in
    /// <see cref="PlacementController.HandleInput"/>.</summary>
    public void SelectRepairMode() => _placement.SelectRepairMode();

    /// <summary>Wood one repair action costs; read by the host page's HUD hint.</summary>
    public static int RepairWoodCost => BuildingRepair.WoodCost;

    /// <summary>Wood currently in the stockpile; read by the host page's HUD.</summary>
    public int Wood => _stockpile.Wood;

    /// <summary>Food currently in the stockpile; read by the host page's HUD.</summary>
    public int Food => _stockpile.Food;

    /// <summary>Residents currently living in the settlement; read by the host page's HUD.</summary>
    public int Residents => _population.Residents;

    /// <summary>Residents the settlement's houses can shelter; read by the host page's HUD.</summary>
    public int PopulationCapacity => _population.Capacity(_world);

    /// <summary>Food gained or lost per second at the current farms and residents; read by the
    /// host page's HUD.</summary>
    public float NetFoodPerSecond => _population.NetFoodPerSecond(_world);

    /// <summary>Zombies currently alive on the grid; read by the host page's HUD.</summary>
    public int ZombieCount => _world.GetStore<Zombie>().Count;

    /// <summary>Wood a building of the given <paramref name="kind"/> costs to place; read by the
    /// host page's HUD to grey out buttons the settlement can't currently afford.</summary>
    public static int WoodCost(BuildingKind kind) => BuildingCatalog.WoodCostFor(kind);
}
