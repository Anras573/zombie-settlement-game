using Yaeger.ECS;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// The settlement's residents: each <see cref="BuildingKind.House"/> shelters up to
/// <see cref="ResidentsPerHouse"/> of them, newcomers arrive while there's room and food, and
/// every resident eats on a timer (see <see cref="SettlementStockpile.ConsumeFood"/>). Residents
/// who can't be fed leave one per missed meal, and a destroyed house takes its residents with it —
/// so farms, houses, and the zombie defense all feed back into one another.
/// </summary>
public sealed class SettlementPopulation
{
    /// <summary>Residents one house can shelter.</summary>
    public const int ResidentsPerHouse = 2;

    /// <summary>Seconds between a new resident arriving, while there's room and food.</summary>
    public const float ArrivalIntervalSeconds = 8f;

    /// <summary>Seconds between meals.</summary>
    public const float MealIntervalSeconds = 10f;

    /// <summary>Food one resident eats per meal.</summary>
    public const int FoodPerResidentPerMeal = 1;

    private float _arrivalElapsed;
    private float _mealElapsed;
    private int _residents;
    private bool _hasHadResidents;

    /// <summary>Residents currently living in the settlement; read by the host page's HUD.</summary>
    public int Residents => _residents;

    /// <summary>Whether anyone has ever lived here — what separates a settlement that starved
    /// from one still waiting on its first newcomer (see <see cref="GameController"/>'s
    /// game-over check).</summary>
    public bool HasHadResidents => _hasHadResidents;

    /// <summary>Houses currently standing.</summary>
    public static int HouseCount(World world)
    {
        var houses = 0;
        foreach (var (_, building, _) in world.Query<Building, BuildingHealth>())
            if (building.Kind == BuildingKind.House)
                houses++;

        return houses;
    }

    /// <summary>Total residents the settlement's houses can shelter right now.</summary>
    public int Capacity(World world) => HouseCount(world) * ResidentsPerHouse;

    /// <summary>Food gained (positive) or lost (negative) per second at the current number of
    /// farms and residents; read by the host page's HUD.</summary>
    public float NetFoodPerSecond(World world)
    {
        var farms = 0;
        foreach (var (_, _, _) in world.Query<FoodProducer, Building>())
            farms++;

        return farms / SettlementStockpile.FoodProductionIntervalSeconds
            - _residents * FoodPerResidentPerMeal / MealIntervalSeconds;
    }

    /// <summary>
    /// Advances arrivals and meals by <paramref name="deltaTime"/>. Evicts residents beyond what
    /// the houses can hold first (a destroyed house), then admits newcomers, then feeds everyone —
    /// the meal loop is a <c>while</c> so a long stall (e.g. a backgrounded tab) still plays out
    /// every meal it covers, the same carry-over pattern as
    /// <see cref="SettlementStockpile.UpdateFoodProduction"/>.
    /// </summary>
    public void Update(World world, SettlementStockpile stockpile, float deltaTime)
    {
        var capacity = Capacity(world);
        if (_residents > capacity)
            _residents = capacity;

        if (_residents < capacity && stockpile.Food > 0)
        {
            _arrivalElapsed += deltaTime;
            while (_arrivalElapsed >= ArrivalIntervalSeconds && _residents < capacity)
            {
                _arrivalElapsed -= ArrivalIntervalSeconds;
                _residents++;
                _hasHadResidents = true;
            }
        }
        else
        {
            _arrivalElapsed = 0f;
        }

        if (_residents == 0)
        {
            _mealElapsed = 0f;
            return;
        }

        _mealElapsed += deltaTime;
        while (_mealElapsed >= MealIntervalSeconds && _residents > 0)
        {
            _mealElapsed -= MealIntervalSeconds;
            var needed = _residents * FoodPerResidentPerMeal;
            if (stockpile.ConsumeFood(needed) < needed)
                _residents--;
        }
    }
}
