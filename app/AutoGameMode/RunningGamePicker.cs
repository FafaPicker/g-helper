using GHelper.UI;
using System.Management;

namespace GHelper.AutoGameMode;

internal sealed class RunningGamePicker : RForm
{
    private readonly TextBox search = new() { Dock = DockStyle.Fill, PlaceholderText = "搜索进程名或程序路径" };
    private readonly ListView list = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
        MultiSelect = false, HideSelection = false
    };
    private readonly Label status = new() { AutoSize = true };
    private readonly System.Windows.Forms.Timer refreshDelay = new() { Interval = 300 };
    private readonly List<ManagementEventWatcher> watchers = new();
    private List<GameEntry> entries = new();
    private bool closing;

    public GameEntry? SelectedGame => list.SelectedItems.Count == 1 ? (GameEntry)list.SelectedItems[0].Tag : null;

    public RunningGamePicker()
    {
        SuspendLayout();
        Text = "选择实际游戏进程";
        ClientSize = new Size(740, 420);
        MinimumSize = new Size(560, 300);
        StartPosition = FormStartPosition.CenterParent;
        Font = SystemFonts.MessageBoxFont;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var searchRow = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var refresh = new Button { Text = "刷新", AutoSize = true };
        search.AccessibleName = "搜索运行进程";
        searchRow.Controls.Add(search, 0, 0);
        searchRow.Controls.Add(refresh, 1, 0);
        list.AccessibleName = "运行进程列表";
        list.Columns.Add("进程", 190);
        list.Columns.Add("程序路径", 510);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var select = new Button { Text = "添加", AutoSize = true, Enabled = false, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        footer.Controls.AddRange(new Control[] { select, cancel });
        layout.Controls.Add(searchRow, 0, 0);
        layout.Controls.Add(list, 0, 1);
        layout.Controls.Add(status, 0, 2);
        layout.Controls.Add(footer, 0, 3);
        Controls.Add(layout);
        search.TextChanged += (_, _) => FilterEntries();
        refresh.Click += (_, _) => RefreshEntries();
        refreshDelay.Tick += (_, _) =>
        {
            refreshDelay.Stop();
            RefreshEntries();
        };
        list.SelectedIndexChanged += (_, _) => select.Enabled = SelectedGame != null;
        list.DoubleClick += (_, _) =>
        {
            if (SelectedGame != null) DialogResult = DialogResult.OK;
        };
        list.Resize += (_, _) =>
        {
            list.Columns[0].Width = (int)(180 * DeviceDpi / 96f);
            list.Columns[1].Width = Math.Max(100, list.ClientSize.Width - list.Columns[0].Width - SystemInformation.VerticalScrollBarWidth - 4);
        };
        Shown += (_, _) =>
        {
            StartWatching();
            RefreshEntries();
            search.Focus();
        };
        FormClosed += (_, _) =>
        {
            closing = true;
            foreach (var watcher in watchers) watcher.Dispose();
            refreshDelay.Dispose();
        };
        AcceptButton = select;
        CancelButton = cancel;
        ResumeLayout(true);
        InitTheme();
        search.BackColor = buttonMain;
        search.ForeColor = foreMain;
        list.BackColor = buttonMain;
        list.ForeColor = foreMain;
    }

    private void StartWatching()
    {
        try
        {
            using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            foreach (string eventClass in new[] { "Win32_ProcessStartTrace", "Win32_ProcessStopTrace" })
            {
                // Stop traces are only a refresh hint: their snapshot is always filtered to this session.
                string filter = eventClass == "Win32_ProcessStartTrace" ? " WHERE SessionID = " + currentProcess.SessionId : "";
                var watcher = new ManagementEventWatcher("root\\CIMV2", "SELECT * FROM " + eventClass + filter);
                watchers.Add(watcher);
                watcher.EventArrived += (_, args) =>
                {
                    args.NewEvent.Dispose();
                    if (closing || !IsHandleCreated) return;
                    try
                    {
                        BeginInvoke((Action)(() =>
                        {
                            if (closing) return;
                            refreshDelay.Stop();
                            refreshDelay.Start();
                        }));
                    }
                    catch (InvalidOperationException) { /* The picker has closed. */ }
                };
                watcher.Start();
            }
            status.Text = "进程启动或退出后自动刷新";
        }
        catch (Exception exception) when (exception is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            foreach (var watcher in watchers) watcher.Dispose();
            watchers.Clear();
            status.Text = "自动刷新不可用；请以管理员身份运行，或点击刷新";
        }
    }

    private void RefreshEntries()
    {
        entries = GameEntry.GetRunningExecutables();
        FilterEntries();
    }

    private void FilterEntries()
    {
        string selectedPath = SelectedGame?.ExecutablePath;
        string query = search.Text.Trim();
        list.BeginUpdate();
        try
        {
            list.Items.Clear();
            foreach (var entry in entries.Where(entry => entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                entry.ExecutablePath.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                var item = new ListViewItem(entry.Name) { Tag = entry };
                item.SubItems.Add(entry.ExecutablePath);
                list.Items.Add(item);
                if (string.Equals(selectedPath, entry.ExecutablePath, StringComparison.OrdinalIgnoreCase)) item.Selected = true;
            }
        }
        finally { list.EndUpdate(); }
    }
}
