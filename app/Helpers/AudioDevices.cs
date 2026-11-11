using NAudio.CoreAudioApi;

namespace GHelper.Helpers
{
    // Enumerates the active audio render (playback) and capture (microphone)
    // endpoints and switches the default one, for the tray context menu.
    //
    // Enumeration and the current-default lookup use NAudio's MMDeviceEnumerator
    // (already a dependency of the project, see Helpers/Audio.cs). Switching the
    // default uses the undocumented IPolicyConfig COM interface (see
    // IPolicyConfig.cs), which NAudio does not expose.
    internal static class AudioDevices
    {
        // One endpoint: its id (stable across calls), friendly name, and whether
        // it is currently the default for its flow.
        public sealed class Device
        {
            public string Id = "";
            public string Name = "";
            public bool IsDefault;
        }

        // List active devices of the given flow. The default device (if any) is
        // included and flagged. Never throws: a failure yields an empty list so
        // the menu just shows "(no devices)" rather than breaking the tray menu.
        public static List<Device> List(DataFlow flow)
        {
            var result = new List<Device>();

            try
            {
                using var enumerator = new MMDeviceEnumerator();

                string defaultId = "";
                try
                {
                    using var def = enumerator.GetDefaultAudioEndpoint(flow, Role.Console);
                    if (def != null) defaultId = def.ID;
                }
                catch { /* no default device for this flow yet */ }

                foreach (var dev in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
                {
                    using (dev)
                    {
                        result.Add(new Device
                        {
                            Id = dev.ID,
                            Name = string.IsNullOrWhiteSpace(dev.FriendlyName) ? "(unnamed)" : dev.FriendlyName,
                            IsDefault = dev.ID == defaultId,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("AudioDevices.List(" + flow + "): " + ex.Message);
            }

            return result;
        }

        public static List<Device> ListOutput() => List(DataFlow.Render);
        public static List<Device> ListInput() => List(DataFlow.Capture);

        // Make the given endpoint the default for every role. Errors are logged
        // and swallowed: the menu item click should never crash the app.
        public static void SetDefault(string deviceId)
        {
            try
            {
                PolicyConfig.SetDefaultEndpoint(deviceId);
                Logger.WriteLine("AudioDevices.SetDefault: " + deviceId);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("AudioDevices.SetDefault: " + ex.Message);
            }
        }
    }
}
