using System.Diagnostics;

namespace GHelper.AutoGameMode;

internal sealed class GameProcessTracker : IDisposable
{
    private readonly object sync = new();
    private readonly Dictionary<int, Process> processes = new();

    public event Action? ProcessExited;

    public bool Track(Process process)
    {
        int processId = process.Id;
        lock (sync)
        {
            if (processes.ContainsKey(processId)) return false;
            processes.Add(processId, process);
            process.Exited += (_, _) => OnExited(processId, process);
            try
            {
                // The OS handle is signaled even if exit happens while this subscription is installed.
                process.EnableRaisingEvents = true;
                return true;
            }
            catch
            {
                processes.Remove(processId);
                throw;
            }
        }
    }

    private void OnExited(int processId, Process process)
    {
        lock (sync)
        {
            if (!processes.TryGetValue(processId, out var tracked) || !ReferenceEquals(tracked, process)) return;
            processes.Remove(processId);
        }
        process.Dispose();
        ProcessExited?.Invoke();
    }

    public void Clear()
    {
        lock (sync)
        {
            foreach (var process in processes.Values) process.Dispose();
            processes.Clear();
        }
    }

    public void Dispose() => Clear();
}
