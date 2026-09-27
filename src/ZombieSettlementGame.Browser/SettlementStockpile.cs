using Yaeger.ECS;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// The settlement's wood and food stockpile: starting wood, what farms and sawmills harvest over
/// time, and what a placed building spends. Wood can run out (see <see cref="TrySpendWood"/>);
/// nothing consumes food yet, so it's a running total.
/// </summary>
public sealed class SettlementStockpile
{
    /// <summary>Wood the settlement starts with, before any player-placed building spends it. The
    /// three starter buildings in <see cref="SettlementScene.Build"/> are free — this only budgets
    /// what the player places afterward.</summary>
    private const int StartingWood = 10;

    /// <summary>Seconds a farm takes to harvest one unit of food.</summary>
    public const float FoodProductionIntervalSeconds = 5f;

    /// <summary>Food yielded by one farm harvest.</summary>
    private const int FoodPerHarvest = 1;

    /// <summary>Seconds a sawmill takes to harvest one unit of wood.</summary>
    public const float WoodProductionIntervalSeconds = 4f;

    /// <summary>Wood yielded by one sawmill harvest.</summary>
    private const int WoodPerHarvest = 1;

    /// <summary>Wood in the settlement's stockpile, spent placing new buildings (see
    /// <see cref="TrySpendWood"/>). The three starter buildings don't draw from this.</summary>
    private int _wood = StartingWood;

    /// <summary>Food harvested by farms so far (see <see cref="FoodProducer"/>); nothing consumes
    /// it yet, so it's a running total rather than a resource that can run out.</summary>
    private int _food;

    /// <summary>Wood currently in the stockpile; read by the host page's HUD.</summary>
    public int Wood => _wood;

    /// <summary>Food harvested so far; read by the host page's HUD.</summary>
    public int Food => _food;

    /// <summary>Deducts <paramref name="cost"/> wood if the stockpile can cover it. Returns
    /// <c>false</c> and leaves the stockpile untouched otherwise.</summary>
    public bool TrySpendWood(int cost)
    {
        if (_wood < cost)
            return false;

        _wood -= cost;
        return true;
    }

    /// <summary>
    /// Advances every farm's harvest timer by <paramref name="deltaTime"/>, crediting
    /// <see cref="FoodPerHarvest"/> food to the stockpile each time a farm's accumulated
    /// <see cref="FoodProducer.Elapsed"/> passes <see cref="FoodProducer.IntervalSeconds"/> — a
    /// <c>while</c>, not an <c>if</c>, so a long stall (e.g. a backgrounded tab) still credits
    /// every harvest it covers instead of losing the surplus.
    /// </summary>
    public void UpdateFoodProduction(World world, float deltaTime)
    {
        foreach (var (entity, producer, _) in world.Query<FoodProducer, Building>())
        {
            var elapsed = producer.Elapsed + deltaTime;
            while (elapsed >= producer.IntervalSeconds)
            {
                elapsed -= producer.IntervalSeconds;
                _food += FoodPerHarvest;
            }

            world.AddComponent(entity, producer with { Elapsed = elapsed });
        }
    }

    /// <summary>Same timer-and-carry-over shape as <see cref="UpdateFoodProduction"/>, but credits
    /// <see cref="WoodPerHarvest"/> wood per sawmill harvest instead.</summary>
    public void UpdateWoodProduction(World world, float deltaTime)
    {
        foreach (var (entity, producer, _) in world.Query<WoodProducer, Building>())
        {
            var elapsed = producer.Elapsed + deltaTime;
            while (elapsed >= producer.IntervalSeconds)
            {
                elapsed -= producer.IntervalSeconds;
                _wood += WoodPerHarvest;
            }

            world.AddComponent(entity, producer with { Elapsed = elapsed });
        }
    }
}
