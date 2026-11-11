using GHelper.UI;
using System.ComponentModel;
using System.Globalization;

namespace GHelper.AutoGameMode;

internal sealed class GameListForm : RForm
{
    private readonly BindingList<GameEntry> games;
    private readonly DataGridView grid = new();
    private readonly AutoGameModeController controller;
    private readonly StringComparer gameNameComparer = StringComparer.Create(CultureInfo.GetCultureInfo("zh-CN"), true);
    private bool sorting;

    public GameListForm(AutoGameModeController controller)
    {
        SuspendLayout();
        this.controller = controller;
        games = new BindingList<GameEntry>(controller.Games.Select(game => new GameEntry
        {
            Enabled = game.Enabled,
            Name = game.Name,
            ExecutablePath = game.ExecutablePath,
            PerformanceMode = game.PerformanceMode
        }).ToList());

        Text = "自动模式 · 游戏规则";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 420);
        MinimumSize = new Size(640, 360);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label
        {
            Text = "切换方式：按游戏指定档位。请选择实际游戏进程；LOL 请在进入对局后添加，勿选客户端或启动器。\n无游戏运行时静音；多游戏取最高档。升档立即执行，降档等待 5 秒。",
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            Margin = new Padding(0, 0, 0, 10)
        }, 0, 0);

        grid.Dock = DockStyle.Fill;
        grid.AutoGenerateColumns = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Enabled", HeaderText = "启用", MinimumWidth = 100, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "游戏", AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells });
        grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            DataPropertyName = nameof(GameEntry.PerformanceMode), HeaderText = "运行时档位",
            DataSource = new[]
            {
                new { Value = GamePerformanceMode.Silent, Text = "静音" },
                new { Value = GamePerformanceMode.Balanced, Text = "平衡" },
                new { Value = GamePerformanceMode.Turbo, Text = "增强" }
            },
            ValueMember = "Value", DisplayMember = "Text",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "ExecutablePath", HeaderText = "实际游戏程序路径",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true
        });
        foreach (DataGridViewColumn column in grid.Columns)
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
        grid.DataSource = games;
        // Reorder only after editing ends, so the active dropdown/text editor stays attached to its row.
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewCheckBoxCell or DataGridViewComboBoxCell)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellEndEdit += (_, _) => BeginInvoke((Action)SortGames);
        SortGames();
        layout.Controls.Add(grid, 0, 1);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
        var addFile = new Button { Text = "选择 exe", AutoSize = true };
        var addProcess = new Button { Text = "从运行进程选择", AutoSize = true };
        var remove = new Button { Text = "移除", AutoSize = true };
        var save = new Button { Text = "保存", AutoSize = true };
        var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        addFile.Click += async (_, _) =>
        {
            addFile.Enabled = false;
            try
            {
                string? path = await GameExecutablePicker.SelectAsync(this);
                if (path != null && !IsDisposed)
                    AddGame(new GameEntry { Name = Path.GetFileNameWithoutExtension(path), ExecutablePath = path });
            }
            catch (Exception exception)
            {
                if (!IsDisposed) MessageBox.Show(this, "无法打开文件选择窗口：" + exception.Message, "选择游戏");
            }
            finally
            {
                if (!IsDisposed) addFile.Enabled = true;
            }
        };
        addProcess.Click += (_, _) =>
        {
            var selected = SelectRunningGame();
            if (selected != null) AddGame(selected);
        };
        remove.Click += (_, _) =>
        {
            if (grid.CurrentRow?.DataBoundItem is GameEntry game) games.Remove(game);
        };
        save.Click += (_, _) =>
        {
            grid.EndEdit();
            SortGames();
            controller.SaveGames(games.ToList());
            DialogResult = DialogResult.OK;
            Close();
        };
        actions.Controls.AddRange(new Control[] { addFile, addProcess, remove, save, cancel });
        layout.Controls.Add(actions, 0, 2);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
        ResumeLayout(true);
        InitTheme();
        grid.BackgroundColor = formBack;
        grid.DefaultCellStyle.BackColor = buttonMain;
        grid.DefaultCellStyle.ForeColor = foreMain;
    }

    private void SortGames()
    {
        if (IsDisposed || Disposing || sorting) return;
        var selected = grid.CurrentRow?.DataBoundItem as GameEntry;
        int columnIndex = grid.CurrentCell?.ColumnIndex ?? 0;
        var sorted = games.OrderBy(game => game.PerformanceMode.GetPriority())
            .ThenBy(game => game.Name, gameNameComparer)
            .ThenBy(game => game.ExecutablePath, StringComparer.OrdinalIgnoreCase).ToList();
        if (games.SequenceEqual(sorted)) return;
        sorting = true;
        try
        {
            games.RaiseListChangedEvents = false;
            games.Clear();
            foreach (var game in sorted) games.Add(game);
            games.RaiseListChangedEvents = true;
            games.ResetBindings();
            if (selected != null && games.Contains(selected))
                grid.CurrentCell = grid.Rows[games.IndexOf(selected)].Cells[columnIndex];
        }
        finally
        {
            games.RaiseListChangedEvents = true;
            sorting = false;
        }
    }

    private void AddGame(GameEntry game)
    {
        if (GameEntry.IsLauncher(game.ExecutablePath))
        {
            MessageBox.Show(this, "这是启动器或 LOL 客户端。请进入游戏后选择实际游戏进程。", "选择游戏",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var existing = games.FirstOrDefault(entry => string.Equals(entry.ExecutablePath, game.ExecutablePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Enabled = true;
            games.ResetBindings();
            return;
        }
        games.Add(game);
        SortGames();
    }

    private GameEntry? SelectRunningGame()
    {
        using var picker = new RunningGamePicker();
        return picker.ShowDialog(this) == DialogResult.OK ? picker.SelectedGame : null;
    }
}
