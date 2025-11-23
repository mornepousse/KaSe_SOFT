using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace KaSe_Controller;

public delegate void UpdateKeyHandler();
public partial class App : Application
{
    public static event UpdateKeyHandler? UpdateKeyEvent;
    
    public static void UpdateKey() {
        UpdateKeyEvent?.Invoke();
    }
    
    private static ObservableCollection<ObservableCollection<ObservableCollection<K_Keys>>> keys =
        new ObservableCollection<ObservableCollection<ObservableCollection<K_Keys>>>()
        {
            new ObservableCollection<ObservableCollection<K_Keys>>(){
            new ObservableCollection<K_Keys>(){K_Keys.K_DEL,  K_Keys.K_1,    K_Keys.K_2,    K_Keys.K_3,    K_Keys.K_4,      K_Keys.K_5,     K_Keys.K_LBRC,   K_Keys.K_6,     K_Keys.K_7,     K_Keys.K_8,      K_Keys.K_9,   K_Keys.K_0,    K_Keys.K_EQL},
            new ObservableCollection<K_Keys>(){K_Keys.K_TAB,  K_Keys.K_QUOT, K_Keys.K_COMM, K_Keys.K_DOT,  K_Keys.K_P,      K_Keys.K_Y,      K_Keys.MO_L2,    K_Keys.K_F,     K_Keys.K_G,     K_Keys.K_C,      K_Keys.K_R,   K_Keys.K_L,    K_Keys.K_SLSH},
            new ObservableCollection<K_Keys>(){K_Keys.K_RALT,  K_Keys.K_A,    K_Keys.K_O,    K_Keys.K_E,    K_Keys.K_U,      K_Keys.K_I,     K_Keys.K_RBRC,  K_Keys.K_D,     K_Keys.K_H,     K_Keys.K_T,     K_Keys.K_N,   K_Keys.K_S,   K_Keys.K_MINUS},
            new ObservableCollection<K_Keys>(){K_Keys.K_LCTRL, K_Keys.K_SCLN, K_Keys.K_Q,    K_Keys.K_J,    K_Keys.K_K,      K_Keys.K_X,     K_Keys.K_LWIN,  K_Keys.K_B,     K_Keys.K_M,     K_Keys.K_W,     K_Keys.K_V,   K_Keys.K_Z,   K_Keys.K_GRV},
            new ObservableCollection<K_Keys>(){K_Keys.K_ESC,  K_Keys.K_ENT,   K_Keys.K_LALT, K_Keys.K_LWIN, K_Keys.K_LSHIFT, K_Keys.K_SPACE, K_Keys.MO_L2,    K_Keys.K_BSPACE,K_Keys.K_ENT, K_Keys.K_BSLSH, K_Keys.K_RWIN,K_Keys.K_HELP,K_Keys.TO_L3}
        },

            new ObservableCollection<ObservableCollection<K_Keys>>(){
        new ObservableCollection<K_Keys>(){K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_F2, K_Keys.K_F3, K_Keys.K_F4, K_Keys.K_F5, K_Keys.K_F6, K_Keys.K_F7, K_Keys.K_F8, K_Keys.K_F9, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_L_PARENTHESIS},
        new ObservableCollection<K_Keys>(){K_Keys.K_NO, K_Keys.K_F1, K_Keys.K_HOME, K_Keys.K_NO, K_Keys.K_END, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_F10, K_Keys.K_F11, K_Keys.K_NO},
        new ObservableCollection<K_Keys>(){K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_LEFT, K_Keys.K_UP, K_Keys.K_RIGHT, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_F12, K_Keys.K_R_PARENTHESIS},
        new ObservableCollection<K_Keys>(){K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_DOWN, K_Keys.MACRO_2, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO},
        new ObservableCollection<K_Keys>(){K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO, K_Keys.K_NO}
    },
            new ObservableCollection<ObservableCollection<K_Keys>>(){// dvorak sous layeur custom pour windows/macos
        new ObservableCollection<K_Keys>(){K_Keys.K_ESC, K_Keys.K_ENT, /**/ K_Keys.K_2, K_Keys.K_3, K_Keys.K_4, K_Keys.K_5, K_Keys.K_6, K_Keys.K_7, K_Keys.K_8, K_Keys.K_9, /**/ K_Keys.TO_L1, K_Keys.K_INT3, /**/ K_Keys.K_MINUS},
        new ObservableCollection<K_Keys>(){K_Keys.K_DEL, K_Keys.K_1, K_Keys.K_W, K_Keys.K_E, K_Keys.K_R, K_Keys.K_T, K_Keys.K_Y, K_Keys.K_U, K_Keys.K_I, K_Keys.K_O, K_Keys.K_0, K_Keys.K_RBRC, /**/ K_Keys.MO_L2},
        new ObservableCollection<K_Keys>(){K_Keys.K_TAB, K_Keys.K_Q, K_Keys.K_S, K_Keys.K_D, K_Keys.K_F, K_Keys.K_G, K_Keys.K_H, K_Keys.K_J, K_Keys.K_K, K_Keys.K_L, K_Keys.K_P, K_Keys.K_LBRC, /**/ K_Keys.K_EQUAL},
        new ObservableCollection<K_Keys>(){K_Keys.K_RALT, K_Keys.K_A, K_Keys.K_X, K_Keys.K_C, K_Keys.K_V, K_Keys.K_B, K_Keys.K_N, K_Keys.K_M, K_Keys.K_COMM, K_Keys.K_DOT, K_Keys.K_SCLN, K_Keys.K_QUOT, /**/ K_Keys.K_RSHIFT},
        new ObservableCollection<K_Keys>(){K_Keys.K_LCTRL, K_Keys.K_Z, K_Keys.K_LALT, K_Keys.K_LWIN, K_Keys.K_LSHIFT, K_Keys.K_SPACE, K_Keys.K_BSPACE, K_Keys.K_ENTER, K_Keys.K_BSLSH, K_Keys.K_DELETE, K_Keys.K_SLSH, K_Keys.K_GRV, /**/ K_Keys.K_NO}
    }
        };
    
    private static SerialPortManager _serialPortManager = new SerialPortManager();
    public static SerialPortManager SerialPortManager
    {
        get { return _serialPortManager; }
        set { _serialPortManager = value; }
    }
    public static ObservableCollection<ObservableCollection<ObservableCollection<K_Keys>>> Keys
    {
        get { return keys; }
        set
        {
            keys = value;
        }
    }
    
    private static int currentLayer = 0;

    public static int CurrentLayer
    {
        get { return currentLayer; }
        set
        {
            if (value >= 0 && value < Keys.Count)
            {
                currentLayer = value;
                UpdateKeyHandler? handler = UpdateKeyEvent;
                handler?.Invoke();
            }
        }
    }
    
    private static int _maxLayers = 10;
    public static int MaxLayers
    {
        get { return _maxLayers; }
        set { _maxLayers = value; }
    }
    private static int rows = 5;
    public static int Rows
    {
        get { return rows; }
        set { rows = value; }
    }
    private static int cols = 13;
    public static int Cols
    {
        get { return cols; }
        set { cols = value; }
    }
    
    public override void Initialize()
    {
        if(keys == null || keys.Count == 0)
            keys = new ObservableCollection<ObservableCollection<ObservableCollection<K_Keys>>>();
        for (int l = Keys.Count; l < MaxLayers; l++)
        {
            Keys.Add(new ObservableCollection<ObservableCollection<K_Keys>>());
            for (int r = 0; r < Rows; r++)
            {
                Keys[l].Add(new ObservableCollection<K_Keys>());
                for (int c = 0; c < Cols; c++)
                {
                    Keys[l][r].Add(K_Keys.K_NO);
                }
            }     
        }
       
        
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}