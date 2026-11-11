namespace GHelper.AutoGameMode;

internal sealed class GameModeState
{
    public DateTimeOffset? SwitchAt { get; private set; }
    private GamePerformanceMode? pendingMode;

    public GamePerformanceMode? Evaluate(GamePerformanceMode targetMode, GamePerformanceMode currentMode,
        DateTimeOffset now, bool immediate = false)
    {
        if (immediate || targetMode.GetPriority() >= currentMode.GetPriority())
        {
            Reset();
            return targetMode;
        }

        if (pendingMode != targetMode)
        {
            pendingMode = targetMode;
            SwitchAt = now.AddSeconds(5);
        }
        if (now < SwitchAt.Value) return null;
        Reset();
        return targetMode;
    }

    public void Reset()
    {
        SwitchAt = null;
        pendingMode = null;
    }
}
