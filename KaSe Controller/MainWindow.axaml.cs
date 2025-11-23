using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

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
                OnPropertyChanged();
            }
        }
    }
     
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        KeyboardGrid.Children.Add( KeyboardUiRenderer.LoadDefaultJsonUi());
        App.CurrentLayer = 0;
        
    }
 
    private void TryToConnect()
    {
        if(!App.SerialPortManager.IsPortOpen)  
        {
            App.SerialPortManager.OpenPort(App.SerialPortManager.GetKeyboardPort());
            if (App.SerialPortManager.IsPortOpen)
            {
                App.SerialPortManager.GetKeymap(CurrentLayer);
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
}