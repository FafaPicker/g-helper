namespace GHelper.AutoGameMode;

// Values match the existing hardware profiles; priority is deliberately separate.
internal enum GamePerformanceMode
{
    Balanced = 0,
    Turbo = 1,
    Silent = 2
}

internal static class GamePerformanceModes
{
    public static int GetPriority(this GamePerformanceMode mode) => mode switch
    {
        GamePerformanceMode.Silent => 0,
        GamePerformanceMode.Balanced => 1,
        _ => 2
    };
}

// Policies recommend a mode only. Process monitoring, delays and hardware writes stay in the controller.
// A future load policy can receive samples separately without adding another hardware controller.
internal interface IGameModePolicy
{
    GamePerformanceMode Evaluate(IReadOnlyCollection<GameEntry> runningGames);
}

internal sealed class PerGameModePolicy : IGameModePolicy
{
    public GamePerformanceMode Evaluate(IReadOnlyCollection<GameEntry> runningGames)
    {
        return runningGames.MaxBy(game => game.PerformanceMode.GetPriority())?.PerformanceMode
            ?? GamePerformanceMode.Silent;
    }
}
