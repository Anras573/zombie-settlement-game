using System;
using Yaeger.ECS;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// The settlement's wood and food stockpile: starting wood, what farms and sawmills harvest over
/// time, and what a placed building spends. Wood can run out (see <see cref="TrySpendWood"/>), and
/// so can food, which the settlement's residents eat (see <see cref="SettlementPopulation"/>).
/// </summary>
public sealed class SettlementStockpile
{
    /// <summary>Wood the settlement starts with, before any player-placed building spends it. The
    /// three starter buildings in <see cref="SettlementScene.Build"/> are free — this only budgets
    /// what the player places afterward.</summary>
    private const int StartingWood = 10;

    /// <summary>Food the settlement starts with, enough to feed the first residents until a farm
    /// harvests.</summary>
    private const int StartingFood = 5;

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

    /// <summary>Food in the stockpile: harvested by farms (see <see cref="FoodProducer"/>) and
    /// eaten by residents (see <see cref="ConsumeFood"/>).</summary>
    private int _food = StartingFood;

    /// <summary>Wood currently in the stockpile; read by the host page's HUD.</summary>
    public int Wood => _wood;

    /// <summary>Food currently in the stockpile; read by the host page's HUD.</summary>
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

    /// <summary>Eats up to <paramref name="amount"/> food and returns how much was actually eaten —
    /// less than asked if the stockpile ran dry.</summary>
    public int ConsumeFood(int amount)
    {
        var eaten = Math.Min(_food, amount);
        _food -= eaten;
        return eaten;
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
