using System.Numerics;

namespace ZombieSettlementGame.Browser;

/// <summary>The settlement's placeable building types.</summary>
public enum BuildingKind
{
    Farm,
    House,
    Fence,
    Sawmill,
    Watchtower,
}

/// <summary>
/// Tags an entity as a placed building of a given <see cref="BuildingKind"/> occupying grid cell
/// (<see cref="Column"/>, <see cref="Row"/>), distinct from the ground tilemap it sits on. Which
/// sprite-sheet frame represents each kind is a rendering concern (see
/// <see cref="BuildingCatalog.FrameFor"/>), not part of this component.
/// </summary>
public readonly record struct Building(BuildingKind Kind, int Column, int Row);

/// <summary>
/// Marks a farm as harvesting food on a timer: every <see cref="IntervalSeconds"/> of accumulated
/// <see cref="Elapsed"/> yields one unit of food. Only farms carry this component, so food
/// production is driven purely by which buildings exist, matching the read-only-then-overwrite
/// pattern components use throughout this codebase (see
/// <see cref="SettlementStockpile.UpdateFoodProduction"/>).
/// </summary>
public readonly record struct FoodProducer(float IntervalSeconds, float Elapsed);

/// <summary>
/// Marks a sawmill as harvesting wood on a timer, the same shape and update pattern as
/// <see cref="FoodProducer"/> but for wood — kept as its own type rather than a shared generic
/// "resource producer" since only two kinds exist and each already reads clearly on its own.
/// </summary>
public readonly record struct WoodProducer(float IntervalSeconds, float Elapsed);

/// <summary>
/// Marks a watchtower as shooting the nearest zombie within <see cref="Range"/> world units once
/// per <see cref="IntervalSeconds"/> of accumulated <see cref="Elapsed"/>, for <see cref="Damage"/>
/// hit points — the same timer shape as <see cref="FoodProducer"/> (see
/// <see cref="TowerController"/>).
/// </summary>
public readonly record struct Turret(float Range, float IntervalSeconds, int Damage, float Elapsed);

/// <summary>
/// A short-lived line from a watchtower to the zombie it just shot, drawn by
/// <see cref="SettlementRenderer"/> until <see cref="Remaining"/> seconds run out (see
/// <see cref="TowerController"/>).
/// </summary>
public readonly record struct ShotFlash(Vector2 From, Vector2 To, float Remaining);

/// <summary>
/// A building's remaining/maximum hit points. Every placed building carries one (see
/// <see cref="BuildingPlacement.PlaceBuilding"/>, <see cref="BuildingCatalog.MaxHealthFor"/>) so
/// <see cref="ZombieController"/> has something to whittle down; a building is destroyed and its
/// cell freed the moment <see cref="Current"/> reaches zero.
/// </summary>
public readonly record struct BuildingHealth(int Current, int Max);
