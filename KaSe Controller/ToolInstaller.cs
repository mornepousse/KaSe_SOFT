using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KaSe_Controller
{
    public static class ToolInstaller
    {
        public static async Task<(bool Success, string Message)> InstallNativeToolsAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report("Démarrage de l'installation des outils...");

            // 1) try python -> pip install esptool
            string[] pythonCandidates = new[] { "python", "python3" };
            foreach (var py in pythonCandidates)
            {
                try
                {
                    var res = await RunProcessAsync(py, "--version", progress, cancellationToken, TimeSpan.FromSeconds(5));
                    if (res.ExitCode == 0 && !string.IsNullOrWhiteSpace(res.Output))
                    {
                        progress?.Report($"Python détecté: {res.Output.Trim()}");
                        progress?.Report("Installation d'esptool via pip (utilisateur)...");
                        // try to install esptool using --user to avoid sudo
                        var installRes = await RunProcessAsync(py, "-m pip install --user esptool", progress, cancellationToken, TimeSpan.FromMinutes(5));
                        progress?.Report(installRes.Output);
                        if (installRes.ExitCode == 0)
                        {
                            return (true, "esptool installé via pip");
                        }
                        else
                        {
                            // pip failed, continue to try next option
                            progress?.Report("pip install a échoué: " + installRes.ExitCode);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report($"Essai {py} failed: {ex.Message}");
                }
            }

            // 2) si python non present ou pip failed, essayer de télécharger espflash binaire
            progress?.Report("Tentative de téléchargement d'un flasher natif (espflash) depuis GitHub releases...");
            string assetName;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                assetName = "espflash-x86_64-pc-windows-msvc.exe";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                assetName = "espflash-x86_64-apple-darwin";
            }
            else
            {
                assetName = "espflash-x86_64-unknown-linux-gnu";
            }

            var downloadUrl = $"https://github.com/esp-rs/espflash/releases/latest/download/{assetName}";
            var toolsDir = Path.Combine(AppContext.BaseDirectory, "tools");
            Directory.CreateDirectory(toolsDir);
            var target = Path.Combine(toolsDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? assetName : "espflash");

            try
            {
                using var http = new HttpClient();
                using var resp = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!resp.IsSuccessStatusCode)
                {
                    progress?.Report($"Téléchargement échoué: {resp.StatusCode}");
                    return (false, "Impossible de télécharger espflash automatiquement. Installez Python+esptool ou téléchargez espflash manuellement.");
                }

                var total = resp.Content.Headers.ContentLength ?? -1L;
                using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
                using var fs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
                var buffer = new byte[81920];
                long read = 0;
                int r;
                while ((r = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, r, cancellationToken);
                    read += r;
                    if (total > 0)
                        progress?.Report($"Téléchargé {read}/{total} bytes");
                    else
                        progress?.Report($"Téléchargé {read} bytes");
                }

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        // chmod +x
                        Process.Start(new ProcessStartInfo { FileName = "/bin/chmod", Arguments = $"+x \"{target}\"", UseShellExecute = false, CreateNoWindow = true });
                    }
                    catch { }
                }

                progress?.Report($"Espflash téléchargé vers: {target}");
                progress?.Report("Installation terminée. Note: si esptool ou espflash ne sont pas trouvés, mettez à jour votre PATH ou utilisez le chemin: " + target);
                return (true, target);
            }
            catch (Exception ex)
            {
                progress?.Report("Erreur lors du téléchargement: " + ex.Message);
                return (false, "Erreur: " + ex.Message);
            }
        }

        public static async Task<bool> RunPublicProbeAsync(string fileName, string arguments)
        {
            try
            {
                var result = await RunProcessAsync(fileName, arguments, null, CancellationToken.None, TimeSpan.FromSeconds(5));
                return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output);
            }
            catch
            {
                return false;
            }
        }

        private static async Task<(int ExitCode, string Output)> RunProcessAsync(string fileName, string arguments, IProgress<string>? progress, CancellationToken cancellationToken, TimeSpan? timeout = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var output = new StringBuilder();
            using (var process = new Process { StartInfo = psi })
            {
                process.OutputDataReceived += (s, e) => { if (e.Data != null) { output.AppendLine(e.Data); progress?.Report(e.Data); } };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) { output.AppendLine(e.Data); progress?.Report(e.Data); } };

                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    // Instead of throwing, return a non-zero exit code and the error message so callers can handle absence of the executable gracefully
                    return (-1, "Impossible de démarrer le process: " + ex.Message);
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    if (timeout.HasValue)
                        cts.CancelAfter(timeout.Value);
                    try
                    {
                        await process.WaitForExitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        try { if (!process.HasExited) process.Kill(); } catch { }
                        throw;
                    }
                }

                return (process.ExitCode, output.ToString());
            }
        }
    }
}
