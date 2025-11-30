using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Layout;
using Avalonia.Media;

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
     
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        KeyboardGrid.Children.Add( KeyboardUiRenderer.LoadDefaultJsonUi());
        App.CurrentLayer = 0;

        // Start a background check for first-run dependencies (non-blocking UI)
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

            // quick probe: look for esptool or espflash in PATH
            var found = false;
            try
            {
                var res1 = await ToolInstaller.RunPublicProbeAsync("esptool.py", "--version");
                if (res1)
                    found = true;
            }
            catch { }

            try
            {
                var res1b = await ToolInstaller.RunPublicProbeAsync("esptool", "--version");
                if (res1b)
                    found = true;
            }
            catch { }

            try
            {
                var res2 = await ToolInstaller.RunPublicProbeAsync("espflash", "--version");
                if (res2)
                    found = true;
            }
            catch { }

            if (found)
            {
                // nothing to do
                return;
            }

            // else ask user to install tools
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dlg = new Window
                {
                    Title = "Vérification des dépendances",
                    Width = 480,
                    Height = 220,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                };

                var panel = new StackPanel { Margin = new Thickness(8) };
                panel.Children.Add(new TextBlock { Text = "Outils de flashing (esptool/espflash) introuvables. Voulez-vous tenter une installation automatique ?", TextWrapping = Avalonia.Media.TextWrapping.Wrap });

                var skipCheckbox = new CheckBox { Content = "Ne plus demander", Margin = new Thickness(0,8,0,0) };

                var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Thickness(0,12,0,0) };
                var installBtn = new Button { Content = "Installer", Width = 100, Margin = new Thickness(8,0,8,0) };
                var laterBtn = new Button { Content = "Plus tard", Width = 100, Margin = new Thickness(8,0,8,0) };
                buttons.Children.Add(installBtn);
                buttons.Children.Add(laterBtn);

                panel.Children.Add(skipCheckbox);
                panel.Children.Add(buttons);
                dlg.Content = panel;

                var tcs = new TaskCompletionSource<bool>();
                installBtn.Click += async (_, __) =>
                {
                    tcs.TrySetResult(true);
                    dlg.Close();
                };
                laterBtn.Click += (_, __) =>
                {
                    tcs.TrySetResult(false);
                    dlg.Close();
                };

                await dlg.ShowDialog(this);
                var doInstall = await tcs.Task;
                if (skipCheckbox.IsChecked == true)
                {
                    settings.SkipDependencyCheck = true;
                    await SettingsManager.SaveAsync(settings);
                }

                if (doInstall)
                {
                    FlashLog.Text = string.Empty;
                    var cts = new CancellationTokenSource();
                    var progress = new Progress<string>(s => {
                        FlashLog.Text += s + "\n";
                        FlashLog.CaretIndex = FlashLog.Text.Length;
                    });

                    try
                    {
                        var (success, message) = await ToolInstaller.InstallNativeToolsAsync(progress, cts.Token);
                        if (success)
                            await ShowInfoAsync("Installation terminée: " + message, "Installer outils");
                        else
                            await ShowInfoAsync("Échec de l'installation: " + message, "Installer outils");
                    }
                    catch (OperationCanceledException)
                    {
                        await ShowInfoAsync("Installation annulée", "Installer outils");
                    }
                    catch (Exception ex)
                    {
                        await ShowInfoAsync("Erreur: " + ex.Message, "Installer outils");
                    }
                    finally
                    {
                        cts.Dispose();
                    }
                }
            });
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

    private async void FlashButton_OnClick(object? sender, RoutedEventArgs e)
    {
        // Open file dialog to select firmware
        var dlg = new OpenFileDialog();
        dlg.Filters.Add(new FileDialogFilter { Name = "Firmware", Extensions = { "bin", "hex" } });
        dlg.AllowMultiple = false;
        var files = await dlg.ShowAsync(this);
        if (files == null || files.Length == 0)
            return;

        var firmwarePath = files[0];

        // confirmation
        var confirm = await ConfirmAsync($"Flasher le firmware:\n{firmwarePath}\n sur le port detecté ?", "Confirmer flash");
        if (!confirm)
            return;

        // ensure port open
        if (!App.SerialPortManager.IsPortOpen)
        {
            App.SerialPortManager.OpenPort(App.SerialPortManager.GetKeyboardPort());
        }

        var cts = new CancellationTokenSource();

        FlashLog.Text = string.Empty;
        ProgressValue = 0;

        var textProgress = new Progress<string>(s => {
            // append line
            FlashLog.Text += s + "\n";
            FlashLog.CaretIndex = FlashLog.Text.Length;
        });
        var percentProgress = new Progress<int>(p => {
            ProgressValue = p;
        });

        try
        {
            var portToUse = App.SerialPortManager.GetKeyboardPort();
            if (string.IsNullOrWhiteSpace(portToUse))
            {
                await ShowInfoAsync("Port du clavier introuvable", "Erreur");
                return;
            }

            var result = await App.SerialPortManager.FlashFirmwareAsync(portToUse, firmwarePath, null, textProgress, percentProgress, cts.Token);
            if (result.Success)
            {
                await ShowInfoAsync("Flash réussi", "Succès");
            }
            else
            {
                await ShowInfoAsync("Erreur lors du flash: " + result.Message + "\nVoir le log pour les détails.", "Erreur");
            }
        }
        catch (OperationCanceledException)
        {
            await ShowInfoAsync("Flash annulé", "Annulé");
        }
        catch (Exception ex)
        {
            await ShowInfoAsync("Exception: " + ex.Message, "Erreur");
        }
        finally
        {
            cts.Dispose();
            ProgressValue = 0;
        }
    }

    private void HelpButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!App.SerialPortManager.IsPortOpen)
        {
            App.SerialPortManager.OpenPort(App.SerialPortManager.GetKeyboardPort());    
        }
        App.SerialPortManager.GetHelp();
    }

    private void L1Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!App.SerialPortManager.IsPortOpen)
        {
            App.SerialPortManager.OpenPort(App.SerialPortManager.GetKeyboardPort());    
        }
        App.SerialPortManager.GetLayer(1);
    }

    private void K1Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!App.SerialPortManager.IsPortOpen)
        {
            App.SerialPortManager.OpenPort(App.SerialPortManager.GetKeyboardPort());    
        }
        App.SerialPortManager.GetKeymap(1);
    }

    private void KEYSETButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!App.SerialPortManager.IsPortOpen)
        {
            App.SerialPortManager.OpenPort(App.SerialPortManager.GetKeyboardPort());    
        }
        App.SerialPortManager.SetKey(0,0,0, K_Keys.K_A);
    }

    private void SelectingItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (App.SerialPortManager.IsPortOpen)
        {
            App.SerialPortManager.GetKeymap(CurrentLayer);   
        }
    }

    private async void InstallToolsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        FlashLog.Text = string.Empty;
        var cts = new CancellationTokenSource();
        var progress = new Progress<string>(s => {
            FlashLog.Text += s + "\n";
            FlashLog.CaretIndex = FlashLog.Text.Length;
        });

        try
        {
            var (success, message) = await ToolInstaller.InstallNativeToolsAsync(progress, cts.Token);
            if (success)
                await ShowInfoAsync("Installation terminée: " + message, "Installer outils");
            else
                await ShowInfoAsync("Échec de l'installation: " + message, "Installer outils");
        }
        catch (OperationCanceledException)
        {
            await ShowInfoAsync("Installation annulée", "Installer outils");
        }
        catch (Exception ex)
        {
            await ShowInfoAsync("Erreur: " + ex.Message, "Installer outils");
        }
        finally
        {
            cts.Dispose();
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

    
}