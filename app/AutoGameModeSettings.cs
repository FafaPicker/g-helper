using GHelper.AutoGameMode;
using GHelper.UI;

namespace GHelper;

public partial class SettingsForm
{
    private RButton buttonAutoGame;
    private GameListForm? gameListForm;

    private void InitializeAutoGameMode()
    {
        var autoCell = new Panel { Dock = DockStyle.Fill, Margin = buttonSilent.Margin };
        buttonAutoGame = new RButton
        {
            Name = "buttonAutoGame",
            Text = "自动模式",
            AccessibleName = "自动模式",
            Dock = DockStyle.Fill,
            BorderColor = colorStandard,
            BorderRadius = buttonSilent.BorderRadius,
            Image = Properties.Resources.icons8_game_controller_48,
            ImageAlign = buttonSilent.ImageAlign,
            TextImageRelation = buttonSilent.TextImageRelation,
            UseVisualStyleBackColor = false,
            TabIndex = 3
        };
        var settingsButton = new RButton
        {
            Name = "buttonAutoGameSettings",
            AccessibleName = "自动模式游戏列表",
            Image = new Bitmap(Properties.Resources.icons8_settings_32, new Size(24, 24)),
            Size = new Size(28, 28),
            Location = new Point(0, 0),
            Borderless = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            UseVisualStyleBackColor = false
        };
        buttonAutoGame.Click += (_, _) => Program.autoGameMode?.Enable();
        settingsButton.Click += (_, _) =>
        {
            if (Program.autoGameMode == null) return;
            if (gameListForm != null)
            {
                gameListForm.Activate();
                return;
            }
            using var dialog = new GameListForm(Program.autoGameMode);
            gameListForm = dialog;
            try { dialog.ShowDialog(this); }
            finally { gameListForm = null; }
        };
        autoCell.Controls.Add(buttonAutoGame);
        autoCell.Controls.Add(settingsButton);
        settingsButton.BringToFront();
        autoCell.Resize += (_, _) => settingsButton.Location = new Point(Math.Max(0, autoCell.Width - settingsButton.Width - 14), 14);

        // Keep layout changes here so the button order can be adjusted without touching monitoring.
        tablePerf.SuspendLayout();
        tablePerf.ColumnCount = 5;
        tablePerf.ColumnStyles.Clear();
        for (int column = 0; column < 5; column++)
            tablePerf.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        tablePerf.SetColumn(buttonFans, 4);
        tablePerf.Controls.Add(autoCell, 3, 0);
        tablePerf.ResumeLayout();
    }

    private bool VisualiseAutoGameMode()
    {
        bool enabled = Program.autoGameMode?.Enabled == true;
        buttonAutoGame.Activated = enabled;
        return enabled;
    }
}
