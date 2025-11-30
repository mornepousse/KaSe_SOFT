using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace KaSe_Controller;

public partial class MacrosViewControl : UserControl, INotifyPropertyChanged
{
    #region event
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    #endregion event
    public ObservableCollection<MacroInfo> Macros
    {
        get { return App.Macros; }
        set { App.Macros = value; OnPropertyChanged();
            GetAvailableMacroKeys();
        }
    }
    
    private MacroInfo _selectedMacro = new MacroInfo();
    public MacroInfo SelectedMacro
    {
        get { return _selectedMacro; }
        set
        {
            _selectedMacro = value;
            OnPropertyChanged();
        }
    }
    
    private MacroInfo newMacro = new MacroInfo();
    public MacroInfo NewMacro
    {
        get { return newMacro; }
        set
        {
            newMacro = value;
            OnPropertyChanged();
        }
    }
     
    private ObservableCollection<K_Keys> _availableMacroKeys = new ObservableCollection<K_Keys>();
    
    public ObservableCollection<K_Keys> AvailableMacroKeys
    {
        get { return _availableMacroKeys; }
        set
        {
            _availableMacroKeys = value;
            OnPropertyChanged();
        }
    }
    
    private K_Keys _selectedMacroKeycode;

    public K_Keys SelectedMacroKeycode
    {
        get { return _selectedMacroKeycode; }
        set
        {
            _selectedMacroKeycode = value;
            NewMacro.Keycode = value;
            OnPropertyChanged();
        }
    }
    
    public MacrosViewControl()
    {
        InitializeComponent();
    }
    private void Button_OnClickAddMacro(object? sender, RoutedEventArgs e)
    {
        if (App.SerialPortManager.IsPortOpen)
        {
            if (!string.IsNullOrWhiteSpace(NewMacro.Name) 
                && NewMacro.Name.Length <= 10 
                && (ushort)NewMacro.Keycode >= 256 
                && NewMacro.Keys.Count >= 2
                && NewMacro.Keys.Count <= 5)
            {
                int mcindex = 0;
                for (int i = 0; i < Macros.Count; i++)
                {
                    if(Macros[i].Index == mcindex) 
                    {
                        mcindex++;
                        i = -1;
                    }
                }
                NewMacro.Index = mcindex;
                App.SerialPortManager.AddMacro(NewMacro);
                NewMacro = new MacroInfo();
                App.SerialPortManager.GetMacros();
            }
            else
            {
                
            }
        }
    }

    private async void Button_OnClickAddKey(object? sender, RoutedEventArgs e)
    {
        if (App.SerialPortManager.IsPortOpen)
        {
            if (SelectedMacro != null)
            {
                SelectWindow selectWindow = new SelectWindow();
                await selectWindow.ShowDialog((Window) this.VisualRoot);
                if (selectWindow.IsKeySelected)
                {
                    var keyToAdd = selectWindow.SelectedKey;
                    if (keyToAdd != null && keyToAdd != K_Keys.K_NONE && keyToAdd <= K_Keys.HID_KEY_GUI_RIGHT && !SelectedMacro.Keys.Contains((K_Keys)keyToAdd) && NewMacro.Keys.Count < 5)
                    {
                        NewMacro.Keys.Add((K_Keys)keyToAdd);
                        OnPropertyChanged(nameof(NewMacro));
                        OnPropertyChanged(nameof(NewMacro.KeysAsKKeys));     
                    }
                } 
            }
        }
    }
    
    private void GetAvailableMacroKeys()
    { 
        AvailableMacroKeys = new ObservableCollection<K_Keys>()
        {
            K_Keys.MACRO_1  ,
            K_Keys.MACRO_2  ,
            K_Keys.MACRO_3  ,
            K_Keys.MACRO_4  ,
            K_Keys.MACRO_5  ,
            K_Keys.MACRO_6  ,
            K_Keys.MACRO_7  ,
            K_Keys.MACRO_8  ,
            K_Keys.MACRO_9  ,
            K_Keys.MACRO_10 ,
            K_Keys.MACRO_11 ,
            K_Keys.MACRO_12 ,
            K_Keys.MACRO_13 ,
            K_Keys.MACRO_14 ,
            K_Keys.MACRO_15 ,
            K_Keys.MACRO_16 ,
            K_Keys.MACRO_17 ,
            K_Keys.MACRO_18 ,
            K_Keys.MACRO_19 ,
            K_Keys.MACRO_20 ,
        };
        foreach (var mc in Macros)
        {
            AvailableMacroKeys.Remove(mc.Keycode);
        }

        SelectedMacroKeycode = AvailableMacroKeys[0];
    }

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        GetAvailableMacroKeys();
    }

    private void Button_OnClickdeleteMacro(object? sender, RoutedEventArgs e)
    {
        if (App.SerialPortManager.IsPortOpen)
        {
            if (SelectedMacro != null)
            {
                App.SerialPortManager.DeleteMacro(SelectedMacro);
                App.SerialPortManager.GetMacros();
            }
        }
    }

    private void Button_OnClickRemoveKey(object? sender, RoutedEventArgs e)
    {
        Button btn = sender as Button;
        K_Keys keyToRemove = (K_Keys)btn.DataContext;
        if(keyToRemove == null) return;
        NewMacro.Keys.Remove(keyToRemove);
        OnPropertyChanged(nameof(NewMacro));
        OnPropertyChanged(nameof(NewMacro.KeysAsKKeys));
    }
}