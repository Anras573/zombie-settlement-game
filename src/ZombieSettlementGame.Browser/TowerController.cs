using System.Collections.Generic;
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
    /// <summary>Seconds a shot line stays on screen after a tower fires.</summary>
    private const float ShotFlashSeconds = 0.12f;

    /// <summary>Advances every watchtower's shot timer by <paramref name="deltaTime"/>. Called once
    /// per tick from <see cref="GameController.Tick"/>.</summary>
    public void Update(World world, float deltaTime)
    {
        FadeShots(world, deltaTime);

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
                Shoot(world, transform.Position, target, turret.Damage);
            }

            world.AddComponent(entity, turret with { Elapsed = elapsed });
        }
    }

    private static void Shoot(World world, Vector2 from, Entity zombie, int damage)
    {
        var to = world.GetComponent<Transform2D>(zombie).Position;
        var shot = world.CreateEntity();
        world.AddComponent(shot, new ShotFlash(from, to, ShotFlashSeconds));
        // Query needs at least two components; the anchor also marks where the shot started.
        world.AddComponent(shot, new Transform2D(from));

        var health = world.GetComponent<ZombieHealth>(zombie);
        var remaining = health.Current - damage;
        if (remaining <= 0)
            world.DestroyEntity(zombie);
        else
            world.AddComponent(zombie, health with { Current = remaining });
    }

    /// <summary>Counts down every <see cref="ShotFlash"/> and removes the expired ones. Expired
    /// entities are collected first so the store isn't mutated while it's being enumerated.</summary>
    private static void FadeShots(World world, float deltaTime)
    {
        List<Entity>? expired = null;
        foreach (var (entity, shot, _) in world.Query<ShotFlash, Transform2D>())
        {
            var remaining = shot.Remaining - deltaTime;
            if (remaining <= 0f)
                (expired ??= []).Add(entity);
            else
                world.AddComponent(entity, shot with { Remaining = remaining });
        }

        if (expired is null)
            return;
        foreach (var entity in expired)
            world.DestroyEntity(entity);
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
