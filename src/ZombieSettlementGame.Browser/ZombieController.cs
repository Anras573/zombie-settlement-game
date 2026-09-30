using System;
using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace ZombieSettlementGame.Browser;

/// <summary>
/// Spawns zombies on the grid's outer ring on a timer and drives every one of them each tick: walk
/// in a straight line toward the nearest <see cref="Building"/>, then attack it once in range — the
/// same accumulate-and-carry-over timing <see cref="SettlementStockpile"/> uses for farm/sawmill
/// production — until its <see cref="BuildingHealth.Current"/> reaches zero, destroying it and
/// freeing its cell for the player to rebuild. There is no way yet for the player to fight back
/// directly; instead <see cref="BuildingKind.Watchtower"/>s shoot them (see
/// <see cref="TowerController"/>), and since a zombie always attacks whichever building is nearest,
/// a ring of <see cref="BuildingKind.Fence"/> keeps them in the towers' line of fire.
/// </summary>
public sealed class ZombieController
{
    /// <summary>Seconds between one zombie spawn and the next.</summary>
    private const float SpawnIntervalSeconds = 6f;

    /// <summary>Grid cells a zombie crosses per second while no building is in range.</summary>
    private const float Speed = 1f;

    /// <summary>Seconds a zombie takes to land one hit once it's in range.</summary>
    private const float AttackIntervalSeconds = 1f;

    /// <summary>Damage dealt per hit.</summary>
    private const int DamagePerHit = 1;

    /// <summary>How close (world units, same scale as <see cref="SettlementGrid.TileWorldSize"/>) a
    /// zombie must get to a building's centre before it stops walking and starts attacking.</summary>
    private const float AttackRange = 0.6f;

    /// <summary>Hit points a freshly spawned zombie has; see <see cref="TowerController"/>.</summary>
    private const int MaxHealth = 3;

    private readonly Random _random = new();
    private float _spawnElapsed;

    /// <summary>Advances spawning and every live zombie by <paramref name="deltaTime"/>. Called once
    /// per tick from <see cref="GameController.Tick"/>.</summary>
    public void Update(World world, float deltaTime)
    {
        Spawn(world, deltaTime);
        Advance(world, deltaTime);
    }

    /// <summary>Accumulates the spawn timer and spawns one zombie per <see cref="SpawnIntervalSeconds"/>
    /// it crosses — a <c>while</c>, not an <c>if</c>, so a long stall still spawns every zombie it
    /// covers instead of losing the surplus (mirrors <see cref="SettlementStockpile.UpdateFoodProduction"/>).</summary>
    private void Spawn(World world, float deltaTime)
    {
        _spawnElapsed += deltaTime;
        while (_spawnElapsed >= SpawnIntervalSeconds)
        {
            _spawnElapsed -= SpawnIntervalSeconds;
            SpawnZombie(world);
        }
    }

    private void SpawnZombie(World world)
    {
        var (column, row) = PickBorderCell();
        var position =
            SettlementGrid.Origin
            + new Vector2(column + 0.5f, SettlementGrid.Height - 1 - row + 0.5f)
                * SettlementGrid.TileWorldSize;

        var zombie = world.CreateEntity();
        world.AddComponent(
            zombie,
            new Transform2D(
                position,
                scale: new Vector2(SettlementGrid.TileWorldSize, SettlementGrid.TileWorldSize)
            )
        );
        world.AddComponent(
            zombie,
            new SpriteSheet(
                ZombieCatalog.SheetPath,
                ZombieCatalog.Columns,
                ZombieCatalog.Rows,
                tint: ZombieCatalog.Tint
            )
        );
        world.AddComponent(zombie, new Zombie(Speed, AttackElapsed: 0f));
        world.AddComponent(zombie, new ZombieHealth(MaxHealth, MaxHealth));
    }

    /// <summary>Picks a random cell on the grid's outermost ring — the boundary wall, outside where
    /// a player can ever build (see <see cref="SettlementCamera.TryScreenToCell"/>'s interior-only
    /// bounds) — as a zombie's spawn point, representing the horde breaching from beyond the walls.</summary>
    private (int Column, int Row) PickBorderCell()
    {
        if (_random.Next(2) == 0)
        {
            var column = _random.Next(SettlementGrid.Width);
            var row = _random.Next(2) == 0 ? 0 : SettlementGrid.Height - 1;
            return (column, row);
        }

        var oppositeRow = _random.Next(SettlementGrid.Height);
        var oppositeColumn = _random.Next(2) == 0 ? 0 : SettlementGrid.Width - 1;
        return (oppositeColumn, oppositeRow);
    }

    /// <summary>Moves every zombie toward the nearest building, or attacks it once in range.
    /// Zombies with no building left to target (none exist) simply hold their ground.</summary>
    private static void Advance(World world, float deltaTime)
    {
        foreach (var (entity, zombie, transform) in world.Query<Zombie, Transform2D>())
        {
            if (
                !TryFindNearestBuilding(
                    world,
                    transform.Position,
                    out var target,
                    out var targetPosition
                )
            )
                continue;

            var toTarget = targetPosition - transform.Position;
            var distance = toTarget.Length();

            if (distance <= AttackRange)
            {
                Attack(world, entity, zombie, target, deltaTime);
                continue;
            }

            var moved = transform;
            moved.Position += toTarget / distance * Speed * deltaTime;
            world.AddComponent(entity, moved);
        }
    }

    /// <summary>Accumulates <paramref name="zombie"/>'s attack timer and lands <see cref="DamagePerHit"/>
    /// once per <see cref="AttackIntervalSeconds"/> crossed, destroying <paramref name="target"/> the
    /// moment its health runs out.</summary>
    private static void Attack(
        World world,
        Entity zombieEntity,
        Zombie zombie,
        Entity target,
        float deltaTime
    )
    {
        var attackElapsed = zombie.AttackElapsed + deltaTime;
        while (attackElapsed >= AttackIntervalSeconds)
        {
            attackElapsed -= AttackIntervalSeconds;

            var health = world.GetComponent<BuildingHealth>(target);
            var remaining = health.Current - DamagePerHit;
            if (remaining <= 0)
            {
                world.DestroyEntity(target);
                world.AddComponent(zombieEntity, zombie with { AttackElapsed = 0f });
                return;
            }

            world.AddComponent(target, health with { Current = remaining });
        }

        world.AddComponent(zombieEntity, zombie with { AttackElapsed = attackElapsed });
    }

    /// <summary>Finds the <see cref="Building"/> whose <c>Transform2D.Position</c> is closest to
    /// <paramref name="fromPosition"/>. Returns <c>false</c> when no building exists.</summary>
    private static bool TryFindNearestBuilding(
        World world,
        Vector2 fromPosition,
        out Entity nearest,
        out Vector2 nearestPosition
    )
    {
        nearest = default;
        nearestPosition = default;
        var nearestDistanceSquared = float.MaxValue;
        var found = false;

        foreach (var (entity, _, transform) in world.Query<Building, Transform2D>())
        {
            var distanceSquared = Vector2.DistanceSquared(fromPosition, transform.Position);
            if (found && distanceSquared >= nearestDistanceSquared)
                continue;

            found = true;
            nearest = entity;
            nearestPosition = transform.Position;
            nearestDistanceSquared = distanceSquared;
        }

        return found;
    }
}
