namespace GHelper.AutoGameMode;

internal static class GameExecutablePicker
{
    private sealed record DialogOwner(IntPtr Handle) : IWin32Window;

    public static Task<string?> SelectAsync(IWin32Window owner)
    {
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialogOwner = new DialogOwner(owner.Handle);
        var dialogThread = new Thread(() =>
        {
            try
            {
                using var dialog = new OpenFileDialog
                {
                    Title = "选择实际游戏程序",
                    Filter = "游戏程序 (*.exe)|*.exe",
                    CheckFileExists = true,
                    Multiselect = false,
                    RestoreDirectory = true,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                };
                completion.SetResult(dialog.ShowDialog(dialogOwner) == DialogResult.OK ? dialog.FileName : null);
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }) { IsBackground = true };
        // Shell dialogs require STA; the existing application runs its UI on MTA.
        dialogThread.SetApartmentState(ApartmentState.STA);
        dialogThread.Start();
        return completion.Task;
    }
}
