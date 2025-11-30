using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KaSe_Controller;

public class MacroInfo : INotifyPropertyChanged
{
    private int _index;
    private K_Keys _keycode;
    private string _name = string.Empty;
    private ObservableCollection<K_Keys> _keys = new();

    #region event
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    #endregion event

    public int Index
    {
        get => _index;
        set { _index = value; OnPropertyChanged(); }
    }

    public K_Keys Keycode
    {
        get => _keycode;
        set { _keycode = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public ObservableCollection<K_Keys> Keys
    {
        get => _keys;
        set { _keys = value; OnPropertyChanged(); OnPropertyChanged(nameof(KeysAsKKeys)); }
    }
    
    public ObservableCollection<string> KeysAsKKeys
    {
        get
        {
            var list = new ObservableCollection<string>();
            foreach (var b in Keys)
            {
                list.Add(((K_Keys)b).ToString().Replace("K_", "").Replace("MO_L", "MO L").Replace("TO_L", "TO L")
                    .Replace("MACRO_", "M").Replace("HID_KEY_","").Replace("_" , " "));
            }
            return list;
        }
        set { OnPropertyChanged(); }
    }
    
    public MacroInfo(int index, K_Keys keycode, string name, ObservableCollection<K_Keys> keys)
    {
        Index = index;
        Keycode = keycode;
        Name = name;
        Keys = keys;
    }

    public MacroInfo()
    {
        
    }
}