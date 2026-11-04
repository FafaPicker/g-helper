using GHelper.AutoGameMode;
using System.Diagnostics;

if (args.Length == 1 && args[0] == "--process-lifetime-probe")
{
    Thread.Sleep(Timeout.Infinite);
    return;
}

var now = DateTimeOffset.UtcNow;
var state = new GameModeState();

if (state.Evaluate(false, now, immediate: true) != false)
    throw new Exception("Enabling auto mode without games must select silent immediately.");
if (state.Evaluate(true, now) != true || state.QuietAt != null)
    throw new Exception("A running game must select turbo immediately.");
if (state.Evaluate(false, now) != null || state.Evaluate(false, now.AddSeconds(4)) != null)
    throw new Exception("The last game exiting must preserve turbo for five seconds.");
if (state.Evaluate(false, now.AddSeconds(5)) != false)
    throw new Exception("The last game exiting must select silent after five seconds.");

state.Reset();
state.Evaluate(false, now);
if (state.Evaluate(true, now.AddSeconds(2)) != true || state.QuietAt != null)
    throw new Exception("Another running game must cancel the pending silent transition.");
state.Evaluate(false, now.AddSeconds(3));
if (state.Evaluate(false, now.AddSeconds(7)) != null || state.Evaluate(false, now.AddSeconds(8)) != false)
    throw new Exception("A later exit must receive a new five-second grace period.");

state.Reset();
if (state.QuietAt != null || state.Evaluate(true, now, immediate: true) != true)
    throw new Exception("Re-enabling must discard pending work and inspect current games.");

foreach (string launcher in new[] { "WeGame.exe", "LeagueClient.exe", "LeagueClientUx.exe", "steam.exe" })
    if (!GameEntry.IsLauncher(launcher)) throw new Exception("Launcher must not trigger turbo: " + launcher);
if (GameEntry.IsLauncher("League of Legends.exe"))
    throw new Exception("The actual LOL game must remain selectable.");

Console.WriteLine("PASS: initial check, immediate turbo, delayed silent, cancellation, re-enable and launcher exclusions.");

using var processTracker = new GameProcessTracker();
using var exitNotification = new AutoResetEvent(false);
processTracker.ProcessExited += () => exitNotification.Set();
var probeIds = new List<int>();
try
{
    for (int index = 0; index < 2; index++)
    {
        var probe = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--process-lifetime-probe")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        probeIds.Add(probe.Id);
        if (!processTracker.Track(probe)) throw new Exception("A new process must be tracked.");
    }
    foreach (int processId in probeIds)
    {
        using var probe = Process.GetProcessById(processId);
        probe.Kill();
        if (!exitNotification.WaitOne(5000))
            throw new Exception("Real process exit must trigger a notification without WMI stop events.");
    }
    Console.WriteLine("PASS: real process exit notifications for two tracked processes.");
}
finally
{
    foreach (int processId in probeIds)
    {
        try
        {
            using var probe = Process.GetProcessById(processId);
            if (!probe.HasExited) probe.Kill();
        }
        catch (ArgumentException) { }
    }
}