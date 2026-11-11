using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace GHelper.AutoGameMode;

internal sealed class GameEntry
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    private GamePerformanceMode performanceMode = GamePerformanceMode.Turbo;

    // Entries saved before per-game profiles existed retain their previous Turbo behavior.
    public GamePerformanceMode PerformanceMode
    {
        get => performanceMode;
        set => performanceMode = value is GamePerformanceMode.Silent or GamePerformanceMode.Balanced or GamePerformanceMode.Turbo
            ? value : GamePerformanceMode.Turbo;
    }

    [JsonIgnore]
    public string ProcessName => Path.GetFileNameWithoutExtension(ExecutablePath);

    public static bool IsLauncher(string path)
    {
        return Path.GetFileName(path).ToLowerInvariant() is
            "steam.exe" or "steamwebhelper.exe" or "wegame.exe" or "tgp.exe" or
            "leagueclient.exe" or "leagueclientux.exe" or "leagueclientuxrender.exe" or
            "riotclientservices.exe" or "riotclientux.exe" or "riotclientuxrender.exe";
    }

    public static List<GameEntry> GetRunningExecutables()
    {
        var entries = new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
        using var currentProcess = Process.GetCurrentProcess();
        int sessionId = currentProcess.SessionId;
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.SessionId != sessionId) continue;
                    string path = GameProcessPath.Read(process);
                    if (string.IsNullOrEmpty(path) || IsLauncher(path)) continue;
                    entries.TryAdd(path, new GameEntry
                    {
                        Name = process.ProcessName,
                        ExecutablePath = path
                    });
                }
                catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Protected and already-exited processes cannot be selected reliably.
                }
            }
        }
        return entries.Values.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
