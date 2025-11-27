using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KaSe_Controller
{
    public class FlashOptions
    {
        public int Baud { get; set; } = 460800;
        public string FlashMode { get; set; } = "dio";
        public string FlashFreq { get; set; } = "40m";
        public string FlashSize { get; set; } = "detect";
        public string Address { get; set; } = "0x1000"; // default for many firmwares
        public bool Compress { get; set; } = true; // -z
        public int TimeoutSeconds { get; set; } = 600; // default 10 minutes
        public string ExtraArgs { get; set; } = string.Empty;
    }

    public class FlashResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string? Message { get; set; }
        public string? OutputLog { get; set; }
    }

    public class EspFlasher
    {
        // Runs esptool via Python: tries "python" then "python3"
        // Provides simple checks and runs erase_flash then write_flash.

        // Regex to extract percentage like '12%' or '(12 %)' or '12 %'
        private static readonly Regex PercentRegex = new Regex(@"(?<!\d)(\d{1,3})\s*%", RegexOptions.Compiled);

        public async Task<FlashResult> FlashFirmwareAsync(
            string port,
            string firmwarePath,
            FlashOptions? options = null,
            IProgress<string>? progressText = null,
            IProgress<int>? progressPercent = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new FlashOptions();

            var sbLog = new StringBuilder();
            void LogText(string s)
            {
                sbLog.AppendLine(s);
                progressText?.Report(s);
            }

            if (string.IsNullOrWhiteSpace(port))
            {
                return new FlashResult { Success = false, Message = "Port invalide" };
            }

            if (!File.Exists(firmwarePath))
            {
                return new FlashResult { Success = false, Message = "Fichier firmware introuvable: " + firmwarePath };
            }

            // Find python executable to use (try python then python3)
            string? pythonExe = await FindPythonExeAsync(progressText, cancellationToken);
            if (pythonExe == null)
            {
                return new FlashResult { Success = false, Message = "Python non trouvé (essayez d'installer Python 3)" };
            }
            LogText("Python executable: " + pythonExe);

            // Check esptool available
            try
            {
                var checkEsptool = await RunProcessAsync(pythonExe, "-m esptool --version", progressText, progressPercent, cancellationToken);
                LogText("esptool: " + checkEsptool.Output);
            }
            catch (Exception ex)
            {
                return new FlashResult { Success = false, Message = "esptool.py non trouvé. Installez via `pip install esptool` : " + ex.Message };
            }

            // 1) erase_flash
            string eraseArgs = $"-m esptool --chip esp32 --port \"{port}\" --baud {Math.Max(115200, Math.Min(options.Baud, 921600))} erase_flash";
            LogText("Lancement erase_flash...");
            var eraseRes = await RunProcessAsync(pythonExe, eraseArgs, progressText, progressPercent, cancellationToken, TimeSpan.FromSeconds(options.TimeoutSeconds));
            LogText(eraseRes.Output);
            if (eraseRes.ExitCode != 0)
            {
                return new FlashResult { Success = false, ExitCode = eraseRes.ExitCode, Message = "erase_flash a échoué", OutputLog = sbLog.ToString() };
            }

            // 2) write_flash
            var zArg = options.Compress ? "-z" : string.Empty;
            var writeArgs = $"-m esptool --chip esp32 --port \"{port}\" --baud {options.Baud} write_flash {zArg} --flash_mode {options.FlashMode} --flash_freq {options.FlashFreq} --flash_size {options.FlashSize} {options.Address} \"{firmwarePath}\" {options.ExtraArgs}";
            LogText("Lancement write_flash...");
            var writeRes = await RunProcessAsync(pythonExe, writeArgs, progressText, progressPercent, cancellationToken, TimeSpan.FromSeconds(options.TimeoutSeconds));
            LogText(writeRes.Output);
            if (writeRes.ExitCode != 0)
            {
                return new FlashResult { Success = false, ExitCode = writeRes.ExitCode, Message = "write_flash a échoué", OutputLog = sbLog.ToString() };
            }

            return new FlashResult { Success = true, ExitCode = 0, Message = "Flash réussi", OutputLog = sbLog.ToString() };
        }

        private async Task<string?> FindPythonExeAsync(IProgress<string>? progressText, CancellationToken cancellationToken)
        {
            // Try python then python3
            string[] candidates = new[] { "python", "python3" };
            foreach (var exe in candidates)
            {
                try
                {
                    var res = await RunProcessAsync(exe, "--version", progressText, null, cancellationToken, TimeSpan.FromSeconds(5));
                    if (res.ExitCode == 0 && !string.IsNullOrWhiteSpace(res.Output))
                        return exe;
                }
                catch
                {
                    // ignore and try next
                }
            }
            return null;
        }

        private async Task<(int ExitCode, string Output)> RunProcessAsync(string fileName, string arguments, IProgress<string>? progressText, IProgress<int>? progressPercent, CancellationToken cancellationToken, TimeSpan? timeout = null)
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

            var outputSb = new StringBuilder();

            using (var process = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        outputSb.AppendLine(e.Data);
                        progressText?.Report(e.Data);
                        TryReportPercent(e.Data, progressPercent);
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        outputSb.AppendLine(e.Data);
                        progressText?.Report(e.Data);
                        TryReportPercent(e.Data, progressPercent);
                    }
                };

                try
                {
                    if (!process.Start())
                        throw new InvalidOperationException("Impossible de démarrer le process: " + fileName);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Échec démarrage process: " + ex.Message, ex);
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
                        try
                        {
                            if (!process.HasExited)
                                process.Kill(entireProcessTree: true);
                        }
                        catch
                        {
                        }

                        throw new OperationCanceledException("Process annulé ou timeout dépassé");
                    }
                }

                // ensure streams flushed
                await Task.Delay(20);

                return (process.ExitCode, outputSb.ToString());
            }
        }

        private void TryReportPercent(string line, IProgress<int>? progressPercent)
        {
            if (progressPercent == null || string.IsNullOrWhiteSpace(line))
                return;

            var m = PercentRegex.Match(line);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int p))
            {
                if (p < 0) p = 0;
                if (p > 100) p = 100;
                progressPercent.Report(p);
            }
        }
    }
}
