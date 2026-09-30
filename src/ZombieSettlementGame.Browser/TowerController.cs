using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// Drives every <see cref="BuildingKind.Watchtower"/> each tick: accumulate its <see cref="Turret"/>
/// timer and, once per <see cref="Turret.IntervalSeconds"/> crossed, shoot the nearest
/// <see cref="Zombie"/> within <see cref="Turret.Range"/> — the same accumulate-and-carry-over
/// timing <see cref="SettlementStockpile"/> uses for production. A zombie is destroyed the moment
/// its <see cref="ZombieHealth.Current"/> reaches zero.
/// </summary>
public sealed class TowerController
{
    /// <summary>Advances every watchtower's shot timer by <paramref name="deltaTime"/>. Called once
    /// per tick from <see cref="GameController.Tick"/>.</summary>
    public void Update(World world, float deltaTime)
    {
        foreach (var (entity, turret, transform) in world.Query<Turret, Transform2D>())
        {
            var elapsed = turret.Elapsed + deltaTime;
            while (elapsed >= turret.IntervalSeconds)
            {
                // Hold a fully charged shot until a zombie wanders into range, rather than
                // banking several and unloading them all at once.
                if (!TryFindNearestZombie(world, transform.Position, turret.Range, out var target))
                {
                    elapsed = turret.IntervalSeconds;
                    break;
                }

                elapsed -= turret.IntervalSeconds;
                Shoot(world, target, turret.Damage);
            }

            world.AddComponent(entity, turret with { Elapsed = elapsed });
        }
    }

    private static void Shoot(World world, Entity zombie, int damage)
    {
        var health = world.GetComponent<ZombieHealth>(zombie);
        var remaining = health.Current - damage;
        if (remaining <= 0)
            world.DestroyEntity(zombie);
        else
            world.AddComponent(zombie, health with { Current = remaining });
    }

    /// <summary>Finds the <see cref="Zombie"/> closest to <paramref name="fromPosition"/> within
    /// <paramref name="range"/>. Returns <c>false</c> when none is in range.</summary>
    private static bool TryFindNearestZombie(
        World world,
        Vector2 fromPosition,
        float range,
        out Entity nearest
    )
    {
        nearest = default;
        var nearestDistanceSquared = range * range;
        var found = false;

        foreach (var (entity, _, transform) in world.Query<Zombie, Transform2D>())
        {
            var distanceSquared = Vector2.DistanceSquared(fromPosition, transform.Position);
            if (distanceSquared > nearestDistanceSquared)
                continue;

            found = true;
            nearest = entity;
            nearestDistanceSquared = distanceSquared;
        }

        return found;
    }
}
