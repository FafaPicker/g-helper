using GHelper.Helpers;
using GHelper.Mode;
using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Text.Json;

namespace GHelper.AutoGameMode;

internal sealed class AutoGameModeController : IDisposable
{
    private readonly SettingsForm settings;
    private readonly GameModeState state = new();
    private readonly IGameModePolicy policy = new PerGameModePolicy();
    private readonly GameProcessTracker processTracker = new();
    private readonly System.Windows.Forms.Timer exitTimer = new() { Interval = 5000 };
    private ManagementEventWatcher? startWatcher;
    private int subscriptionVersion;
    private bool disposed;
    private readonly int sessionId;

    public bool Enabled { get; private set; }
    public List<GameEntry> Games { get; private set; }

    public AutoGameModeController(SettingsForm settings)
    {
        this.settings = settings;
        using var currentProcess = Process.GetCurrentProcess();
        sessionId = currentProcess.SessionId;
        try
        {
            Games = JsonSerializer.Deserialize<List<GameEntry>>(AppConfig.GetString("auto_game_entries", "[]")) ?? new();
        }
        catch (JsonException exception)
        {
            Logger.WriteLine("Auto game list could not be loaded: " + exception.Message);
            Games = new();
        }
        exitTimer.Tick += (_, _) => Refresh();
        processTracker.ProcessExited += () => Dispatch(() => Refresh());
    }

    public void Enable()
    {
        if (disposed) return;
        StopWatching();
        state.Reset();
        Enabled = true;
        try
        {
            StartWatching();
            AppConfig.Set("auto_game_mode", 1);
            // Subscribe first so a process starting during the initial snapshot is not missed.
            Refresh(immediate: true);
        }
        catch (Exception exception) when (exception is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            string message = exception is UnauthorizedAccessException ||
                exception is ManagementException { ErrorCode: ManagementStatus.AccessDenied }
                ? "Windows 进程通知需要管理员权限。请以管理员身份运行 GHelper 后启用自动模式。"
                : exception.Message;
            Fail(message);
        }
    }

    public void Disable()
    {
        if (settings.InvokeRequired)
        {
            settings.Invoke((Action)Disable);
            return;
        }
        if (!Enabled) return;
        Enabled = false;
        StopWatching();
        state.Reset();
        AppConfig.Set("auto_game_mode", 0);
        settings.ShowMode(Modes.GetCurrent());
    }

    public void SaveGames(List<GameEntry> games)
    {
        Games = games;
        AppConfig.Set("auto_game_entries", JsonSerializer.Serialize(games));
        if (Enabled) Enable();
    }

    private void StartWatching()
    {
        var names = Games.Where(game => game.Enabled && !GameEntry.IsLauncher(game.ExecutablePath))
            .Select(game => Path.GetFileName(game.ExecutablePath))
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => "ProcessName = '" + name.Replace("\\", "\\\\").Replace("'", "\\'") + "'");
        string filter = string.Join(" OR ", names);
        if (filter.Length == 0) return;

        filter = "SessionID = " + sessionId + " AND (" + filter + ")";
        int version = subscriptionVersion;
        startWatcher = CreateWatcher("Win32_ProcessStartTrace", filter, version);
        startWatcher.Start();
    }

    private ManagementEventWatcher CreateWatcher(string eventClass, string filter, int version)
    {
        var watcher = new ManagementEventWatcher("root\\CIMV2", "SELECT * FROM " + eventClass + " WHERE " + filter);
        watcher.EventArrived += (_, args) =>
        {
            args.NewEvent.Dispose();
            Dispatch(() =>
            {
                if (Enabled && subscriptionVersion == version) Refresh();
            });
        };
        watcher.Stopped += (_, args) =>
        {
            if (args.Status == ManagementStatus.NoError) return;
            Dispatch(() =>
            {
                if (Enabled && subscriptionVersion == version) Fail("Process notifications stopped: " + args.Status);
            });
        };
        return watcher;
    }

    private void Dispatch(Action action)
    {
        if (disposed || settings.IsDisposed || !settings.IsHandleCreated) return;
        try { settings.BeginInvoke(action); }
        catch (InvalidOperationException) { /* The settings window is shutting down. */ }
    }

    public void Refresh(bool immediate = false, bool reapply = false)
    {
        if (settings.InvokeRequired)
        {
            Dispatch(() => Refresh(immediate, reapply));
            return;
        }
        if (!Enabled || disposed) return;
        var runningGames = GetRunningGames();
        if (runningGames == null)
        {
            Fail("无法读取同名游戏进程的完整路径。请确认 GHelper 有权限访问该进程。");
            return;
        }
        var currentMode = (GamePerformanceMode)Modes.GetCurrentBase();
        var targetMode = state.Evaluate(policy.Evaluate(runningGames), currentMode, DateTimeOffset.UtcNow, immediate);
        if (targetMode == null)
        {
            exitTimer.Stop();
            exitTimer.Interval = Math.Max(1, (int)Math.Ceiling((state.SwitchAt.Value - DateTimeOffset.UtcNow).TotalMilliseconds));
            exitTimer.Start();
            if (!reapply) return;
            targetMode = currentMode;
        }

        if (state.SwitchAt == null) exitTimer.Stop();
        int mode = (int)targetMode.Value;
        if (reapply || Modes.GetCurrent() != mode)
        {
            Logger.WriteLine("Auto game mode: " + Modes.GetName(mode));
            Program.modeControl.SetPerformanceMode(mode, fromGameAutomation: true);
        }
        else settings.ShowMode(mode);
    }

    private List<GameEntry>? GetRunningGames()
    {
        bool unreadableProcess = false;
        var runningGames = new List<GameEntry>();
        foreach (var group in Games.Where(game => game.Enabled && !GameEntry.IsLauncher(game.ExecutablePath))
            .GroupBy(game => game.ProcessName, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(group.Key)) continue;
            var processes = Process.GetProcessesByName(group.Key);
            foreach (var process in processes)
            {
                bool retained = false;
                try
                {
                    if (process.HasExited || process.SessionId != sessionId) continue;
                    string path = GameProcessPath.Read(process);
                    var matches = group.Where(game => string.Equals(game.ExecutablePath, path, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (matches.Count == 0) continue;
                    retained = processTracker.Track(process);
                    runningGames.AddRange(matches);
                }
                catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // A process that exited during the snapshot does not make its path uncertain.
                    try { unreadableProcess |= !process.HasExited; }
                    catch (InvalidOperationException) { }
                    catch (Win32Exception) { unreadableProcess = true; }
                    Logger.WriteLine("Auto game process could not be inspected: " + exception.Message);
                }
                finally
                {
                    if (!retained) process.Dispose();
                }
            }
        }
        // An unreadable process could require a higher profile than the games already identified.
        return unreadableProcess ? null : runningGames;
    }

    private void Fail(string message)
    {
        Logger.WriteLine("Auto game mode unavailable: " + message);
        Disable();
        MessageBox.Show(settings, "自动模式已关闭，保留当前性能模式。\n" + message,
            "自动模式", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void StopWatching()
    {
        subscriptionVersion++;
        exitTimer.Stop();
        startWatcher?.Dispose();
        processTracker.Clear();
        startWatcher = null;
    }

    public void Dispose()
    {
        disposed = true;
        StopWatching();
        exitTimer.Dispose();
        processTracker.Dispose();
    }
}
