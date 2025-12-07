using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace KaSe_Controller
{
    public class AppSettings
    {
        public bool SkipDependencyCheck { get; set; } = false;
        // Keyboard layout used to display key labels (QWERTY, AZERTY, QWERTZ, ...)
        public string KeyboardLayout { get; set; } = "QWERTY";
    }

    public static class SettingsManager
    {
        private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KaSe_Controller");
        private static readonly string FilePath = Path.Combine(Dir, "settings.json");

        // In-memory current settings (set after LoadAsync)
        public static AppSettings? Current { get; private set; }

        // Event raised when keyboard layout changes
        public static event Action<string>? KeyboardLayoutChanged;

        public static async Task<AppSettings> LoadAsync()
        {
            try
            {
                if (!Directory.Exists(Dir))
                    Directory.CreateDirectory(Dir);
                if (!File.Exists(FilePath))
                {
                    var s = new AppSettings();
                    await SaveAsync(s).ConfigureAwait(false);
                    Current = s;
                    return s;
                }

                await using var fs = File.OpenRead(FilePath);
                var settings = await JsonSerializer.DeserializeAsync<AppSettings>(fs).ConfigureAwait(false);
                Current = settings ?? new AppSettings();
                return Current;
            }
            catch
            {
                Current = new AppSettings();
                return Current;
            }
        }

        public static async Task SaveAsync(AppSettings settings)
        {
            try
            {
                if (!Directory.Exists(Dir))
                    Directory.CreateDirectory(Dir);
                await using var fs = File.Create(FilePath);
                await JsonSerializer.SerializeAsync(fs, settings, new JsonSerializerOptions { WriteIndented = true }).ConfigureAwait(false);
                Current = settings;
            }
            catch
            {
                // ignore errors writing settings
            }
        }

        // Apply a new keyboard layout and notify subscribers (non-blocking save)
        public static void ApplyKeyboardLayout(string layout)
        {
            try
            {
                if (Current == null)
                    LoadAsync().GetAwaiter().GetResult();
                Current!.KeyboardLayout = layout;
                // Save in background
                _ = SaveAsync(Current);
                KeyboardLayoutChanged?.Invoke(layout);
            }
            catch
            {
                // ignore errors
            }
        }
    }
}
