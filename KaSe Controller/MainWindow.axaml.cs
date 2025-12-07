using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace KaSe_Controller;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    #region event
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    #endregion event

    private int progressValue = 0;
    
    public int ProgressValue
    {
        get => progressValue;
        set
        {
            progressValue = value;
            OnPropertyChanged();
        }
    }
    
    private bool isBusy;
    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            isBusy = value;
            OnPropertyChanged();
        }
    }

    private double busyProgress;
    public double BusyProgress
    {
        get => busyProgress;
        private set
        {
            busyProgress = value;
            OnPropertyChanged();
        }
    }

    private string busyMessage = string.Empty;
    public string BusyMessage
    {
        get => busyMessage;
        private set
        {
            busyMessage = value;
            OnPropertyChanged();
        }
    }
    
    public ObservableCollection<ObservableCollection<ObservableCollection<K_Keys>>> Keys
    {
        get
        {
            return App.Keys;
        }
        set
        {
            App.Keys = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<string> LayoutsName
    {
        get { return App.LayoutsName; }
        set { App.LayoutsName = value; OnPropertyChanged();}
    }
    
    private bool isPortOpen = false;
    public bool IsOpened
    {
        get { return isPortOpen;}
        set
        {
            isPortOpen = value;
            OnPropertyChanged();
        }
    }
    
    public int CurrentLayer
    {
        get { return App.CurrentLayer; }
        set
        {
            if (value >= 0 && value < App.Keys.Count)
            {
                App.CurrentLayer = value;
                SelectedLayoutName = App.LayoutsName[App.CurrentLayer];
                OnPropertyChanged();
            }
        }
    }
    
    private string _selectedLayoutName = "QWERTY";
    public string SelectedLayoutName
    {
        get { return _selectedLayoutName; }
        set
        {
            // sanitize incoming layout name: remove special characters, collapse spaces
            _selectedLayoutName = SanitizeLayoutName(value);
            OnPropertyChanged();
        }
    }
    
    
    
    // helper: allow only letters, digits, space, underscore and hyphen; collapse multiple spaces and trim
    private static string SanitizeLayoutName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // remove characters not in the allowed set
        var cleaned = Regex.Replace(input, "[^A-Za-z0-9 _-]", string.Empty);
        // collapse consecutive whitespace to single space
        cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
        return cleaned;
    }
     
    private bool exportInProgress;

    private string[] _layouts = new[] { "QWERTY", "AZERTY", "QWERTZ" };
    public string[] Layouts
    {
        get => _layouts;
        set { _layouts = value; OnPropertyChanged(); }
    }

    private string _selectedLayout = "QWERTY";
    public string SelectedLayout
    {
        get => _selectedLayout;
        set
        {
            if (_selectedLayout == value) return;
            _selectedLayout = value;
            OnPropertyChanged();
            try
            {
                // Persist and notify via SettingsManager helper
                SettingsManager.ApplyKeyboardLayout(_selectedLayout);
            }
            catch { }

            // Refresh the keyboard UI when the layout changes
            try
            {
                RefreshKeyboardGrid();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing keyboard grid: {ex}");
            }
        }
    }

    // Reload the keyboard UI into the KeyboardGrid (on UI thread)
    private void RefreshKeyboardGrid()
    {
        // If called from non-UI thread, dispatch to UI thread
        if (!Dispatcher.UIThread.CheckAccess())
        {
            _ = Dispatcher.UIThread.InvokeAsync(() => RefreshKeyboardGrid());
            return;
        }

        try
        {
            // Ensure KeyboardGrid exists in the visual tree
            var kg = this.FindControl<Grid>("KeyboardGrid");
            if (kg == null)
                return;

            kg.Children.Clear();
            var ui = KeyboardUiRenderer.LoadDefaultJsonUi();
            if (ui != null)
                kg.Children.Add(ui);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RefreshKeyboardGrid failed: {ex}");
        }
    }

    public MainWindow()
    {
        InitializeComponent(); 

        App.SerialPortManager.RawDataReceived += OnRawDataReceived;

        // initialize selected layout from settings
        try
        {
            if (SettingsManager.Current == null)
                SettingsManager.LoadAsync().GetAwaiter().GetResult();
            if (!string.IsNullOrWhiteSpace(SettingsManager.Current?.KeyboardLayout))
                SelectedLayout = SettingsManager.Current!.KeyboardLayout;
        }
        catch { }
    }

    private void OnRawDataReceived(string data)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var terminalOutput = this.FindControl<TextBlock>("TerminalOutput");
            if (terminalOutput != null)
            {
                terminalOutput.Text += data;
            }
        });
    }

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        KeyboardGrid.Children.Add( KeyboardUiRenderer.LoadDefaultJsonUi());
        App.CurrentLayer = 0;

        _ = CheckDependenciesOnFirstRunAsync();
    }

    private async Task CheckDependenciesOnFirstRunAsync()
    {
        try
        {
            var settings = await SettingsManager.LoadAsync();
            if (settings.SkipDependencyCheck)
                return;

            // Check .NET runtime version
            try
            {
                var fw = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription ?? string.Empty;
                int major = 0;
                // extract first number from string
                for (int i = 0; i < fw.Length; i++)
                {
                    if (char.IsDigit(fw[i]))
                    {
                        var num = new System.Text.StringBuilder();
                        int j = i;
                        while (j < fw.Length && (char.IsDigit(fw[j]) || fw[j] == '.')) { num.Append(fw[j]); j++; }
                        if (int.TryParse(num.ToString().Split('.')[0], out major))
                            break;
                    }
                }

                if (major > 0 && major < 9)
                {
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await ShowInfoAsync($"Version .NET détectée: {fw}. Cette application requiert .NET 9 ou supérieur. Certaines fonctionnalités peuvent ne pas fonctionner correctement.", "Dépendance manquante : .NET 9+");
                    });
                }
            }
            catch { }
 
        }
        catch
        {
            // ignore errors on start-up check
        }
    }

    private void TryToConnect()
    {
        if(!App.SerialPortManager.IsPortOpen)  
        {
            App.SerialPortManager.OpenPort(App.SerialPortManager.GetKeyboardPort());
            if (App.SerialPortManager.IsPortOpen)
            {
                App.SerialPortManager.GetKeymap(CurrentLayer);
                App.SerialPortManager.GetLayersName();
                App.SerialPortManager.GetMacros();
                IsOpened = true;
            }
        }
    }

    private void TopLevel_OnClosed(object? sender, EventArgs e)
    {
        App.SerialPortManager.ClosePort();
    }

    private async void ConnectButton_OnClick(object? sender, RoutedEventArgs e)
    {
        TryToConnect();
    } 

    private void SelectingItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (App.SerialPortManager.IsPortOpen)
        {
            App.SerialPortManager.GetKeymap(CurrentLayer);   
        }
    }

    

    private async Task<bool> ConfirmAsync(string message, string title)
    {
        var dlg = new Window
        {
            Title = title,
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var panel = new StackPanel { Margin = new Thickness(8) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Thickness(0,8,0,0) };
        var yes = new Button { Content = "Yes", Width = 80, Margin = new Thickness(8,0,8,0) };
        var no = new Button { Content = "No", Width = 80, Margin = new Thickness(8,0,8,0) };
        buttons.Children.Add(yes);
        buttons.Children.Add(no);
        panel.Children.Add(buttons);

        dlg.Content = panel;

        var tcs = new TaskCompletionSource<bool>();
        yes.Click += (_, __) => { tcs.TrySetResult(true); dlg.Close(); };
        no.Click += (_, __) => { tcs.TrySetResult(false); dlg.Close(); };

        await dlg.ShowDialog(this);
        return await tcs.Task;
    }

    private async Task ShowInfoAsync(string message, string title)
    {
        var dlg = new Window
        {
            Title = title,
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var panel = new StackPanel { Margin = new Thickness(8) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var ok = new Button { Content = "Ok", Width = 80, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Thickness(0,8,0,0) };
        panel.Children.Add(ok);
        dlg.Content = panel;

        var tcs = new TaskCompletionSource<bool>();
        ok.Click += (_, __) => { tcs.TrySetResult(true); dlg.Close(); };

        await dlg.ShowDialog(this);
        await tcs.Task;
    }

    private void BeginBusy(string message)
    {
        BusyMessage = message;
        BusyProgress = 0;
        IsBusy = true;
    }

    private void ReportBusy(double progress, string? message = null)
    {
        BusyProgress = progress;
        if (!string.IsNullOrWhiteSpace(message))
        {
            BusyMessage = message;
        }
    }

    private void EndBusy()
    {
        IsBusy = false;
        BusyProgress = 0;
        BusyMessage = string.Empty;
    }

    private void Button_OnClickSetLayoutName(object? sender, RoutedEventArgs e)
    {
        if (App.SerialPortManager.IsPortOpen)
        {
            if (!string.IsNullOrWhiteSpace(SelectedLayoutName) && SelectedLayoutName.Length <= 16 && SelectedLayoutName.Length >= 3)
            {
                App.LayoutsName[CurrentLayer] = SelectedLayoutName;
                App.SerialPortManager.SetLayerName(CurrentLayer, SelectedLayoutName.ToString());
                App.SerialPortManager.GetLayersName();
            }
            else
            {
                
            }
        }
    }

    private async void ExportLayerBtnOnClick(object? sender, RoutedEventArgs e)
    {
        if (!App.SerialPortManager.IsPortOpen)
            return;

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null)
            return;

        var dto = App.SerialPortManager.ExportLayerSnapshot(CurrentLayer);
        var suggested = $"kase_layer_{CurrentLayer}_{DateTime.Now:yyyyMMdd_HHmmss}.json";

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggested,
            Title = "Exporter la couche",
            DefaultExtension = "json",
            ShowOverwritePrompt = true
        });

        if (file == null)
            return;

        try
        {
            var json = ConfigSerializer.LayerToJson(dto);
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json);
        }
        catch (Exception ex)
        {
            await ShowInfoAsync($"Échec de l'export de couche: {ex.Message}", "Erreur");
        }
    }

    private async void ImportLayerBtnOnClick(object? sender, RoutedEventArgs e)
    {
        if (!App.SerialPortManager.IsPortOpen)
            return;

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null)
            return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importer une couche",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Fichiers JSON")
                {
                    Patterns = new[] { "*.json" }
                }
            }
        });

        var file = files.FirstOrDefault();
        if (file == null)
            return;

        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var dto = ConfigSerializer.LayerFromJson(json);
            App.SerialPortManager.ImportLayerToDevice(CurrentLayer, dto);
            SelectedLayoutName = App.LayoutsName[CurrentLayer];
        }
        catch (Exception ex)
        {
            await ShowInfoAsync($"Échec de l'import de couche: {ex.Message}", "Erreur");
        }
    }

    private async void ExportConfigBtnOnClick(object? sender, RoutedEventArgs e)
    {
        if (!App.SerialPortManager.IsPortOpen || exportInProgress)
            return;

        exportInProgress = true;
        BeginBusy("Export configuration en cours...");
        try
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null)
                return;

            var fileName = $"kase_config_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = fileName,
                Title = "Exporter la configuration",
                DefaultExtension = "json",
                ShowOverwritePrompt = true
            });

            if (file == null)
                return;

            ReportBusy(50, "Sérialisation...");
            var dto = ConfigSerializer.Snapshot();
            var json = ConfigSerializer.ToJson(dto);

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json);
            ReportBusy(100, "Configuration exportée");
        }
        catch (Exception ex)
        {
            await ShowInfoAsync($"Échec de l'export: {ex.Message}", "Erreur");
        }
        finally
        {
            exportInProgress = false;
            EndBusy();
        }
    }

    private async void ImportConfigBtnOnClick(object? sender, RoutedEventArgs e)
    {
        if (!App.SerialPortManager.IsPortOpen)
            return;

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null)
            return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importer une configuration",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Fichiers JSON")
                {
                    Patterns = new[] { "*.json" }
                }
            }
        });

        var file = files.FirstOrDefault();
        if (file == null)
            return;

        if (!await ConfirmAsync("Importer cette configuration ? Vos données actuelles seront remplacées.", "Confirmation"))
            return;

        BeginBusy("Import configuration...");
        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var dto = ConfigSerializer.FromJson(json);
            ConfigSerializer.Apply(dto);
            ReportBusy(70, "Écriture sur le clavier...");
            await App.SerialPortManager.PushConfigAsync(dto, new Progress<double>(p => ReportBusy(p, null)));
            ReportBusy(100, "Configuration importée");
        }
        catch (Exception ex)
        {
            await ShowInfoAsync($"Échec de l'import: {ex.Message}", "Erreur");
        }
        finally
        {
            EndBusy();
        }
    }




}
