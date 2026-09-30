namespace ZombieSettlementGame.Browser;

/// <summary>
/// Tags an entity as a shambling zombie, distinct from the static ground/building entities: unlike
/// a <see cref="Building"/>, its <c>Transform2D.Position</c> moves continuously in world space each
/// tick rather than sitting fixed at a grid cell's centre (see <see cref="ZombieController"/>).
/// <see cref="AttackElapsed"/> times its hits once it's in range, the same accumulate-and-carry-over
/// pattern <see cref="FoodProducer.Elapsed"/> uses for a farm's harvest.
/// </summary>
public readonly record struct Zombie(float Speed, float AttackElapsed);

/// <summary>
/// A zombie's remaining/maximum hit points, the mirror of <see cref="BuildingHealth"/>: a
/// <see cref="BuildingKind.Watchtower"/> whittles <see cref="Current"/> down (see
/// <see cref="TowerController"/>) and the zombie is destroyed the moment it reaches zero.
/// </summary>
public readonly record struct ZombieHealth(int Current, int Max);
