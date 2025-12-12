using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Octokit;

namespace KaSe_Controller;

public class UpdateManager : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    
    private readonly GitHubClient _githubClient;
    private readonly string _owner;
    private readonly string _repo;
    private readonly Version _currentVersion;
    
    private Release? _latestRelease;
    private string _downloadPath = "";
    
    public UpdateManager(string owner, string repo)
    {
        _owner = owner;
        _repo = repo;
        _githubClient = new GitHubClient(new ProductHeaderValue("KaSe_Controller"));
        
        // Get current version from assembly
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        _currentVersion = version ?? new Version(1, 0, 0);
    }
    
    public Version CurrentVersion => _currentVersion;
    
    public async Task<UpdateInfo> CheckForUpdatesAsync()
    {
        try
        {
            _latestRelease = await _githubClient.Repository.Release.GetLatest(_owner, _repo);
            
            // Parse version (removing 'v' prefix if present)
            var tagName = _latestRelease.TagName.TrimStart('v');
            if (Version.TryParse(tagName, out var latestVersion))
            {
                if (latestVersion > _currentVersion)
                {
                    return new UpdateInfo
                    {
                        IsUpdateAvailable = true,
                        LatestVersion = latestVersion,
                        ReleaseNotes = _latestRelease.Body,
                        ReleaseUrl = _latestRelease.HtmlUrl
                    };
                }
            }
            
            return new UpdateInfo { IsUpdateAvailable = false };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking for updates: {ex.Message}");
            return new UpdateInfo { IsUpdateAvailable = false, Error = ex.Message };
        }
    }
    
    public async Task<bool> DownloadAndInstallUpdateAsync(IProgress<DownloadProgress>? progress = null)
    {
        if (_latestRelease == null)
            return false;
        
        try
        {
            // Determine current runtime
            string runtimeIdentifier = GetRuntimeIdentifier();
            
            // Find asset matching the runtime
            var asset = _latestRelease.Assets.FirstOrDefault(a => 
                a.Name.Contains(runtimeIdentifier, StringComparison.OrdinalIgnoreCase) &&
                (a.Name.EndsWith(".zip") || a.Name.EndsWith(".tar.gz"))
            );
            
            if (asset == null)
            {
                Console.WriteLine($"No asset found for runtime: {runtimeIdentifier}");
                return false;
            }
            
            progress?.Report(new DownloadProgress { Status = $"Downloading {asset.Name}...", Percentage = 0 });
            
            // Download archive
            var tempPath = Path.Combine(Path.GetTempPath(), "KaSe_Update");
            Directory.CreateDirectory(tempPath);
            
            _downloadPath = Path.Combine(tempPath, asset.Name);
            
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1;
                
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(_downloadPath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var totalRead = 0L;
                    var buffer = new byte[8192];
                    var isMoreToRead = true;
                    
                    do
                    {
                        var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                        if (read == 0)
                        {
                            isMoreToRead = false;
                        }
                        else
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            totalRead += read;
                            
                            if (canReportProgress)
                            {
                                var percentage = (int)((totalRead * 100) / totalBytes);
                                progress?.Report(new DownloadProgress 
                                { 
                                    Status = $"Downloading: {totalRead / 1024 / 1024} MB / {totalBytes / 1024 / 1024} MB", 
                                    Percentage = percentage 
                                });
                            }
                        }
                    } while (isMoreToRead);
                }
            }
            
            progress?.Report(new DownloadProgress { Status = "Extracting...", Percentage = 90 });
            
            // Extract archive
            var extractPath = Path.Combine(tempPath, "extracted");
            Directory.CreateDirectory(extractPath);
            
            if (asset.Name.EndsWith(".zip"))
            {
                ZipFile.ExtractToDirectory(_downloadPath, extractPath, true);
            }
            
            progress?.Report(new DownloadProgress { Status = "Installing...", Percentage = 95 });
            
            // Create script to replace application after restart
            await CreateUpdateScriptAsync(extractPath);
            
            progress?.Report(new DownloadProgress { Status = "Update ready. Restarting...", Percentage = 100 });
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during download: {ex.Message}");
            progress?.Report(new DownloadProgress { Status = $"Error: {ex.Message}", Percentage = 0 });
            return false;
        }
    }
    
    private async Task CreateUpdateScriptAsync(string extractPath)
    {
        var currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        var currentDir = Path.GetDirectoryName(currentExe) ?? "";
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Batch script for Windows
            var scriptPath = Path.Combine(Path.GetTempPath(), "update_kase.bat");
            var script = $@"@echo off
timeout /t 2 /nobreak > nul
echo Update in progress...
xcopy ""{extractPath}\*"" ""{currentDir}"" /E /Y /I
echo Update complete!
start """" ""{currentExe}""
del ""%~f0""
";
            await File.WriteAllTextAsync(scriptPath, script);
            
            // Launch script
            Process.Start(new ProcessStartInfo
            {
                FileName = scriptPath,
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Bash script for Linux
            var scriptPath = Path.Combine(Path.GetTempPath(), "update_kase.sh");
            var script = $@"#!/bin/bash
sleep 2
echo ""Update in progress...""
cp -r ""{extractPath}""/* ""{currentDir}""/
chmod +x ""{currentExe}""
echo ""Update complete!""
""{currentExe}"" &
rm -- ""$0""
";
            await File.WriteAllTextAsync(scriptPath, script);
            
            // Make script executable
            Process.Start("chmod", $"+x {scriptPath}");
            
            // Launch script
            Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = scriptPath,
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
    }
    
    public void ApplyUpdateAndRestart()
    {
        // Exit application to allow update script to run
        Environment.Exit(0);
    }
    
    private string GetRuntimeIdentifier()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "win-x64" : "win-x86";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "linux-x64" : "linux-arm64";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        }
        
        return "unknown";
    }
    
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class UpdateInfo
{
    public bool IsUpdateAvailable { get; set; }
    public Version? LatestVersion { get; set; }
    public string? ReleaseNotes { get; set; }
    public string? ReleaseUrl { get; set; }
    public string? Error { get; set; }
}

public class DownloadProgress
{
    public string Status { get; set; } = "";
    public int Percentage { get; set; }
}

