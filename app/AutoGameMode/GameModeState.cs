namespace GHelper.AutoGameMode;

internal sealed class GameModeState
{
    public DateTimeOffset? QuietAt { get; private set; }

    public bool? Evaluate(bool hasRunningGames, DateTimeOffset now, bool immediate = false)
    {
        if (hasRunningGames || immediate)
        {
            QuietAt = null;
            return hasRunningGames;
        }

        QuietAt ??= now.AddSeconds(5);
        return now >= QuietAt.Value ? false : null;
    }

    public void Reset() => QuietAt = null;
}
