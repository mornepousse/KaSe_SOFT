using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace KaSe_Controller
{
    public class AppSettings
    {
        public bool SkipDependencyCheck { get; set; } = false;
    }

    public static class SettingsManager
    {
        private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KaSe_Controller");
        private static readonly string FilePath = Path.Combine(Dir, "settings.json");

        public static async Task<AppSettings> LoadAsync()
        {
            try
            {
                if (!Directory.Exists(Dir))
                    Directory.CreateDirectory(Dir);
                if (!File.Exists(FilePath))
                {
                    var s = new AppSettings();
                    await SaveAsync(s);
                    return s;
                }

                using var fs = File.OpenRead(FilePath);
                var settings = await JsonSerializer.DeserializeAsync<AppSettings>(fs);
                return settings ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public static async Task SaveAsync(AppSettings settings)
        {
            try
            {
                if (!Directory.Exists(Dir))
                    Directory.CreateDirectory(Dir);
                using var fs = File.Create(FilePath);
                await JsonSerializer.SerializeAsync(fs, settings, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                // ignore errors writing settings
            }
        }
    }
}

