using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace KaSe_Controller;

public partial class SelectWindow : Window, INotifyPropertyChanged
{
    #region event
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    #endregion event
    
    public K_Keys SelectedKey { get; set; } = K_Keys.K_NO;
    public bool IsKeySelected { get; set; } = false;
    
    private ObservableCollection<K_Keys> _keys = new ObservableCollection<K_Keys>();
    public ObservableCollection<K_Keys> Keys
    {
        get => _keys;
        set
        {
            _keys = value;
            OnPropertyChanged(); // Uncomment if implementing INotifyPropertyChanged
        }
    }
    public SelectWindow()
    { 
        InitializeComponent();
        foreach (var k in Enum.GetValues(typeof(K_Keys)).Cast<K_Keys>())
        {
            if (!Keys.Contains(k))
                Keys.Add(k);
        }
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is K_Keys key)
        {
            SelectedKey = key;
            IsKeySelected = true;
            this.Close();
        }
    }
}