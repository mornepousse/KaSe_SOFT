using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace KaSe_Controller;

public enum KeyboardLayout
{
    QWERTY,
    AZERTY,
    QWERTZ
}

public class KeyConverter : IValueConverter
{
    public static readonly KeyConverter Instance = new();

    // Map of special key names to friendly display strings (logical key meaning)
    private static readonly Dictionary<string, string> SpecialNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "APOSTROPHE", "'" },
        { "APO", "'" },
        { "QUOT", "'" },
        { "COMMA", "," },
        { "COMM", "," },
        { "COMA", "," },
        { "PERIOD", "." },
        { "DOT", "." },
        { "MINUS", "-" },
        { "MIN", "-" },
        { "EQUAL", "=" },
        { "EQL", "=" },
        { "SEMICOLON", ";" },
        { "SCOLON", ";" },
        { "SCLN", ";" },
        { "GRAVE", "`" },
        { "GRV", "`" },
        { "SLASH", "/" },
        { "SLSH", "/" },
        { "BACKSLASH", "\\" }, 
        { "BSLSH", "\\" }, 
        { "BRACKET_LEFT", "[" },
        { "LBRCKT", "[" },
        { "LBRC", "[" },
        { "BRACKET_RIGHT", "]" },
        { "RBRCKT", "]" },
        { "RBRC", "]" },
        { "SPACE", "Space" },
        { "SPC", "Space" },
        { "ENTER", "Enter" },
        { "ENT", "Enter" },
        { "TAB", "Tab" },
        { "KEYPAD_DIVIDE", "KP /" },
        { "KEYPAD_MULTIPLY", "KP *" },
        { "KEYPAD_SUBTRACT", "KP -" },
        { "KEYPAD_ADD", "KP +" },
        { "KEYPAD_DECIMAL", "KP ." },
        { "KEYPAD_COMMA", "KP ," },
        { "BACKSPACE", "Backspace" },
        { "BSPACE", "Backspace" },
        { "BSPC", "Backspace" },
        { "CAPS_LOCK", "Caps Lock" },
        { "CLOCK", "Caps Lock" },
        { "PRINT_SCREEN", "Print Screen" },
        { "PRINT SCREEN", "Print Screen" }, 
        { "PRNT_SCRN", "Print Screen" },
        { "P_SCRN", "Print Screen" },
        { "ESCAPE", "Esc" }
    };

    // Layout-specific visual overrides: when the physical key meaning differs by layout
    private static readonly Dictionary<KeyboardLayout, Dictionary<string, string>> LayoutOverrides = new()
    {
        [KeyboardLayout.QWERTY] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // QWERTY: COMMA -> ',', PERIOD -> '.' etc. (same as default SpecialNames)
        },
        [KeyboardLayout.AZERTY] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // On many AZERTY layouts, the key positions differ: show visual according to AZERTY
            // For example, logical HID_KEY_COMMA might be shown as ';' depending on physical position.
            // We'll start with sensible overrides for common punctuation.
            { "COMMA", ";" }, // on AZERTY the physical key often produces ';' without shift
            { "SEMICOLON", "," },
            { "PERIOD", ":" },
            { "SLASH", "!" },
            { "M", "," }, // visual swap
            { "W", "Z"  },  // visual swap
            { "Z", "W"  },  // visual swap
            { "Q", "A"  },
            { "A", "Q"  },
            { ",", ";"  },
            { ".", ":"  },
            { ";", "M"  },
            { "-", ")"  },
            { "BRACKET_LEFT", "^" },
            { "LBRCKT", "^" },
            { "LBRC", "^" },
            { "[", "^"  },
            { "]", "$"  },
            { "BRACKET_RIGHT", "$" },
            { "RBRCKT", "$" },
            { "RBRC", "$" },
            { "\\", "<"  },
            { "/", "!"  },
            { "'", "ù"  },
            { "1", "& 1"  },
            { "2", "é 2 ~"  },
            { "3", "\" 3 #"  },
            { "4", "' 4 }"  },
            { "5", "( 5 ["  },
            { "6", "- 6 |"  },
            { "7", "è 7 `"  },
            { "8", "_ 8 \\"  },
            { "9", "ç 9 ^"  },
            { "0", "à 0 @"  },
            



        },
        [KeyboardLayout.QWERTZ] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Y", "Z" }, // visual swap
            { "COMMA", ";" },
            { "SEMICOLON", "," }
        }
    };

    private static KeyboardLayout GetCurrentLayout()
    {
        var layoutName = SettingsManager.Current?.KeyboardLayout ?? "QWERTY";
        if (Enum.TryParse<KeyboardLayout>(layoutName, true, out var parsed))
            return parsed;
        return KeyboardLayout.QWERTY;
    }

    public object? Convert(object? value, Type targetType, object? parameter, 
        CultureInfo culture)
    {
        if (value is K_Keys key)
        {
            // base name without leading enum prefix (K_ or HID_KEY_)
            var name = key.ToString();
            if (name.StartsWith("K_"))
                name = name.Substring(2);
            else if (name.StartsWith("HID_KEY_"))
                name = name.Substring("HID_KEY_".Length);

            // apply layout-specific overrides first
            var layout = GetCurrentLayout();
            if (LayoutOverrides.TryGetValue(layout, out var map) && map.TryGetValue(name, out var layoutValue))
                return layoutValue;

            // direct mapping for well-known special keys
            if (SpecialNames.TryGetValue(name, out var friendly))
                return friendly;

            // some specific token replacements used previously
            name = name.Replace("MO_L", "MO L").Replace("TO_L", "TO L").Replace("MACRO_", "M");

            // fallback: replace underscores with spaces and return a readable label
            var fallback = name.Replace("_", " ").Trim();
            // If it's a single character token (like 'A' or '1'), keep as-is
            if (fallback.Length == 1)
                return fallback;
            return fallback;
        }
        return BindingOperations.DoNothing;
    }

    public object ConvertBack(object? value, Type targetType, 
        object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
