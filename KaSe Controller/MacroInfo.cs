using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KaSe_Controller;

public class MacroInfo : INotifyPropertyChanged
{
    private int _index;
    private ushort _keycode;
    private string _name = string.Empty;
    private List<byte> _keys = new();

    #region event
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    #endregion event

    public int Index
    {
        get => _index;
        set { _index = value; OnPropertyChanged(); }
    }

    public ushort Keycode
    {
        get => _keycode;
        set { _keycode = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public List<byte> Keys
    {
        get => _keys;
        set { _keys = value; OnPropertyChanged(); }
    }

    public MacroInfo(int index, ushort keycode, string name, List<byte> keys)
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