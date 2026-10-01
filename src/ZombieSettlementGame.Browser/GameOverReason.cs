namespace ZombieSettlementGame.Browser;

/// <summary>Why the game ended; <see cref="None"/> while it's still running.</summary>
public enum GameOverReason
{
    None,

    /// <summary>The zombies destroyed every house.</summary>
    HousesDestroyed,

    /// <summary>Every resident left because the settlement ran out of food.</summary>
    Starved,
}
