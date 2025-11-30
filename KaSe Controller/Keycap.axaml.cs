using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace KaSe_Controller;


public partial class Keycap : UserControl, INotifyPropertyChanged
{
    
    #region event
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    #endregion event
    
    public static readonly StyledProperty<K_Keys> KeyProperty =
        AvaloniaProperty.Register<Keycap, K_Keys>(nameof(Key), defaultValue: K_Keys.K_NO);
    
    public static readonly StyledProperty<int> ColumnProperty =
        AvaloniaProperty.Register<Keycap, int>(nameof(Column), defaultValue: 0);
    
    public static readonly StyledProperty<int> RowProperty =
        AvaloniaProperty.Register<Keycap, int>(nameof(Row), defaultValue: 0);
    
    public K_Keys Key
    {
        get => GetValue(KeyProperty);
        set
        {
            SetValue(KeyProperty, value);
            GetExtraName();
            OnPropertyChanged(nameof(KeyString));
        }
    }

    public int Column
    {
        get => GetValue(ColumnProperty);
        set { SetValue(ColumnProperty, value);
            OnUpdateKey();
        }
    }

    public int Row
    {
        get => GetValue(RowProperty);
        set { SetValue(RowProperty, value);
            OnUpdateKey();
        }
    }

    public string KeyString
    {
        get
        {
            return Key.ToString().Replace("K_", "").Replace("MO_L", "MO L").Replace("TO_L", "TO L")
                .Replace("MACRO_", "M").Replace("HID_KEY_","").Replace("_" , " ");
        }
        set
        {
            
        }
    }
    
    private string extraName = "";

    public string ExtraName
    {
        get => extraName;
        set
        {
            extraName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsExtraNameVisible));
        }
    }
    
    public bool IsExtraNameVisible
    {
        get => !string.IsNullOrEmpty(ExtraName);
        set
        {
            OnPropertyChanged();
        }
    }
    
    DispatcherTimer _timer = new DispatcherTimer();
    public Keycap()
    {
        InitializeComponent();
        App.UpdateKeyEvent += OnUpdateKey;
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.Tick += TimerOnTick;
    }

    private void TimerOnTick(object? sender, EventArgs e)
    {
        if (this.IsLoaded)
        {
            if (App.Keys[App.CurrentLayer].Count <= Row) return;
            if (App.Keys[App.CurrentLayer][Row].Count <= Column) return;
        
            Key = App.Keys[App.CurrentLayer][Row][Column]; 
            OnPropertyChanged(nameof(Key));
            OnPropertyChanged(nameof(KeyString));   
        }
        _timer.Stop();
    }

    private async void OnUpdateKey()
    {
        _timer.Start();
    }

    

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        SelectWindow selectWindow = new SelectWindow();
        await selectWindow.ShowDialog((Window) this.VisualRoot);
        if (selectWindow.IsKeySelected)
        {
            App.Keys[App.CurrentLayer][Row][Column] = selectWindow.SelectedKey;
            Key = selectWindow.SelectedKey;
            App.SerialPortManager.SetKey(App.CurrentLayer, Row, Column, selectWindow.SelectedKey);    
        }
        
    }

    private void GetExtraName()
    {
        string nametmp = Key.ToString();
        if(nametmp.Contains("MACRO_"))
        {
            string indexStr = nametmp.Replace("MACRO_", "");
            if (int.TryParse(indexStr, out int index))
            {
                //var macro = App.SerialPortManager.GetMacroByKeycode((ushort)index);
                MacroInfo macro = null;
                foreach (MacroInfo macroInfo in App.Macros)
                {
                    if (macroInfo.Keycode == (ushort)index)
                    {
                        macro = macroInfo;
                        break;
                    }
                }
                
                if (macro != null)
                {
                    ExtraName = macro.Name;
                }
                else
                {
                    ExtraName = "";
                }
            }
        }
        else
        {
            ExtraName = "";
        }
        if(nametmp.Contains("TO_L") || nametmp.Contains("MO_L"))
        {
            string indexStr = nametmp.Replace("TO_L", "").Replace("MO_L", "");
            if (int.TryParse(indexStr, out int index))
            {
                if (index >= 0 && index < App.LayoutsName.Count)
                {
                    ExtraName = App.LayoutsName[index];
                }
                else
                {
                    ExtraName = "";
                }
            }
        }
    }

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        GetExtraName();
    }
}