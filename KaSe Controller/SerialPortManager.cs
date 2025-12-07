using System;
using System.Linq;
using System.Management;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace KaSe_Controller;


enum CommandStat
{
    Start,
    GetLength,
    GetData
}
enum cdc_command_type
{
    c_nope_t = 0,
    c_get_layer_t,
    c_get_current_layer_index_t,
    c_get_name_layer_t,
    c_get_macros_t,
    c_get_all_layout_names_t,
    c_ping_t,
    c_debug_t,

};
public class SerialPortManager : INotifyPropertyChanged
{
    private const int MacroSlotCount = 20;
       
    #region event
    public event PropertyChangedEventHandler PropertyChanged;
    public event Action<string> RawDataReceived;
    protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    #endregion event
    private SerialPort _serialPort;
    private List<byte> data = new List<byte>();

    // Indicateur simple pour l'UI lorsqu'un flash est en cours
    private bool _isFlashing = false;
    public bool IsFlashing
    {
        get => _isFlashing;
        private set { _isFlashing = value; OnPropertyChanged(nameof(IsFlashing)); }
    }

    public SerialPortManager()
    {
        
    }

    public List<string> ListAvailablePorts()
    {
        return new List<string>(SerialPort.GetPortNames());
    }

    public bool OpenPort(string portName)
    {
        try
        {
            _serialPort = new SerialPort(portName, 115200);
            _serialPort.DataReceived += SerialPortOnDataReceived; 
            _serialPort.Open();
            OnPropertyChanged(nameof(IsPortOpen));
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }
    
    private void SerialPortOnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        SerialPort sp = (SerialPort)sender;
        
        while (sp.BytesToRead > 0)
        {
            
            int byteRead = sp.ReadByte();
            if (byteRead != -1)
            {
                data.Add((byte)byteRead);
            }
        }

        // Ajout pour afficher les données brutes reçues
        if (data.Count > 0)
        {
            string rawData = System.Text.Encoding.UTF8.GetString(data.ToArray());
            Console.WriteLine("RAW DATA: " + rawData);
            RawDataReceived?.Invoke(rawData);
        }

        parseData();
        data.Clear();
        //Console.WriteLine($"Data Received: {data}");
    }

    private async void parseData()
    {
        if( data.Count == 0)
            return;
        Console.WriteLine(data.Count);
        byte previousRxByte = 0;
        //CommandStat commandStat = CommandStat.Start;
        cdc_command_type cmd = cdc_command_type.c_nope_t; 
        List<byte> commandData = new List<byte>();
        int i = 0;
        while (i>=0 && i < data.Count)
        {
            previousRxByte = 0;
            cmd = cdc_command_type.c_nope_t;
            commandData.Clear();
            for (; i < data.Count; i++)
            {

                if (data[i] == 62 && previousRxByte == 67 && data.Count - i > 2)
                { 
                    i ++;
                    cmd = (cdc_command_type)data[i];
                    break;
                
                }
                previousRxByte = data[i]; 
            }
            if (cmd == cdc_command_type.c_nope_t)
                return;
            i++;
            int dataLength =  data[i] | (data[i + 1] << 8);
            i += 2;
            if (dataLength + i > data.Count)
                return; 
            for (int j = 0; j < dataLength; j++)
            {
                commandData.Add(data[j+i]); 
            }
            i += dataLength;

            switch (cmd)
            {
                case cdc_command_type.c_get_current_layer_index_t :
                    if (commandData.Count > 0)
                    {
                        int layer = commandData[0];
                        Console.WriteLine("Current Layer: " + layer);
                        //App.CurrentLayer = layer;
                    }
                    break;
                case cdc_command_type.c_get_layer_t :
                    int layerTmp =  commandData[0] | (commandData[1] << 8);
                    
                    Console.WriteLine("Layer Data: " + System.Text.Encoding.ASCII.GetString(commandData.ToArray()));
                    int rows = 5;
                    int cols = 13;
                    //layerTmp = commandData[2];
                    Console.WriteLine("Layer: " + layerTmp);
                    App.Keys[layerTmp] = new ObservableCollection<ObservableCollection<K_Keys>>();
                    int index = 2;
                    var receivedList = new List<int>(rows * cols);
                    for (int r = 0; r < rows; r++)
                    {
                        App.Keys[layerTmp].Add(new ObservableCollection<K_Keys>());
                        for (int c = 0; c < cols; c++)
                        {
                           App.Keys[layerTmp][r].Add((K_Keys)(commandData[index] | (commandData[index + 1] << 8)));
                          receivedList.Add(commandData[index] | (commandData[index + 1] << 8));
                            index += 2;
                        }
                    }
                    // store raw received values for external verification
                    App.UpdateKey();
                    break;
                case cdc_command_type.c_get_all_layout_names_t :
                        Console.WriteLine("get name layers");
                        List<string> names = new List<string>();
                        string allNames = System.Text.Encoding.ASCII.GetString(commandData.ToArray());
                        names = allNames.Split(';').ToList();
                        for (int n = 0; n < names.Count && n < App.MaxLayers; n++)
                        {
                            App.LayoutsName[n] = names[n].Substring(1); // Remove possible leading character
                            Console.WriteLine("Layer " + n + " Name: " + names[n]);
                        } 
                    break;
                case cdc_command_type.c_ping_t :
                    Console.WriteLine("Ping received");
                    break;
                case cdc_command_type.c_get_macros_t :
                    // Binary payload from MCU (cmd_list_macros):
                    // [count (1 byte)]
                    // For each macro:
                    //   [index (1 byte)]
                    //   [keycode (2 bytes, LE)]
                    //   [nameLen (1 byte)]
                    //   [name (nameLen bytes)]
                    //   [keysLen (1 byte)]
                    //   [keys (keysLen bytes)]
                    Console.WriteLine("Macros payload received (" + commandData.Count + " bytes)");
                    ParseMacrosPayload(commandData);
                    break;
                case cdc_command_type.c_debug_t :
                    Console.WriteLine("Debug: " + System.Text.Encoding.ASCII.GetString(commandData.ToArray()));
                    break;
                default:
                    Console.WriteLine("Unknown command");
                    break;
            }
            
        }
        //data.Clear();
    }
    
    public void ClosePort()
    {
        if (_serialPort != null && _serialPort.IsOpen)
        {
            _serialPort.Close();
            OnPropertyChanged(nameof(IsPortOpen));
        }
    }
    public bool IsPortOpen
    {
        get { return _serialPort != null && _serialPort.IsOpen; }
        set { OnPropertyChanged(); }

    }
    
    private void Send_Command(cdc_command_type cmd, byte[] data = null)
    {
        if (!IsPortOpen)
            return;
        
        int length = data != null ? data.Length : 0;
        List<byte> com = new List<byte>();
        com.Add((byte)'C');
        com.Add((byte)'>');
        com.Add((byte)cmd);
        if (data != null)
        {
            com.Add((byte)(length & 0xFF));
            com.Add((byte)((length >> 8) & 0xFF));
        }
        else
        {
            com.Add(0);
            com.Add(0);
        }

        if (data != null || length > 0)
        {
            foreach (byte b in data)
            {
                com.Add(b);
            }
            _serialPort.Write(com.ToArray(), 0, com.Count);
        }
        else
        {
            _serialPort.Write(com.ToArray(), 0, com.Count);    
        }

    }

    private void SendCommand(string command)
    {
        if (!IsPortOpen)
            return;

        byte[] com = System.Text.Encoding.ASCII.GetBytes(command + "\n");
        _serialPort.Write(com, 0, com.Length);

    }
    public void GetLayers()
    {
        Console.WriteLine("Get Layers");
        SendCommand("L?");
        //Send_Command(cdc_command_type.c_get_current_layer_index_t);
    }
    public void GetHelp()
    {
        Console.WriteLine("Get Help");
        SendCommand("HELP");
    }
    public void GetLayer(int layer)
    {
        Console.WriteLine("Get Layer " + layer);
        SendCommand($"L{layer}");
    }
    public void GetKeymap(int layer)
    {
        Console.WriteLine("Get Keymap " + layer);
        SendCommand($"KEYMAP{layer}");
    }

    public void GetLayersName()
    {
        Console.WriteLine("Get Layers name ");
        SendCommand("LAYOUTS?");
    }
    
    public void GetMacros()
    {
        Console.WriteLine("Get Macros ");
        SendCommand("MACROS?");
    }
    
    public void SetKey(int layer, int row, int col, K_Keys key)
    {
        Console.WriteLine("Set Key " + layer + " " + row + " " + col + " " + key + " " + Convert.ToInt16(key).ToString("X"));
        SendCommand($"SETKEY {layer},{row},{col},{Convert.ToInt16(key).ToString("X")}");
    }

    // Commande: LAYOUTNAME<layer>:<nouveau_nom>
    // Exemple: LAYOUTNAME0:AZERTY_FR
    public void SetLayerName(int CurrentLayer, string SelectedLayoutName)
    {
        Console.WriteLine($"LAYOUTNAME{CurrentLayer}:{SelectedLayoutName}");
        SendCommand($"LAYOUTNAME{CurrentLayer}:{SelectedLayoutName}");
    }
    
    // Nouvelle méthode pour lancer le flash via EspFlasher
    public async Task<FlashResult> FlashFirmwareAsync(string port, string firmwarePath, FlashOptions? options = null, IProgress<string>? progressText = null, IProgress<int>? progressPercent = null, CancellationToken cancellationToken = default)
    {
        if (IsFlashing)
            return new FlashResult { Success = false, Message = "Un flash est déjà en cours" };

        try
        {
            IsFlashing = true;
            var flasher = new EspFlasher();
            var result = await flasher.FlashFirmwareAsync(port, firmwarePath, options, progressText, progressPercent, cancellationToken);
            return result;
        }
        finally
        {
            IsFlashing = false;
        }
    }
   
    public string GetKeyboardPort()
    {
        var list = ListAvailablePorts();
        foreach (var port in list)
        {
            if (CheckPort(port))
            {
                return port;
            }
        }
        return null;
    }
    
    public bool CheckPort(string portName)
    {
        if (OperatingSystem.IsLinux())
        {
            try
            {
                string com = "udevadm info -n" + portName + " | grep 'ID_MODEL=KaSeV2'";
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{com}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process process = Process.Start(psi))
                {
                    string result = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return result.Contains("KaSeV2");
                }
            }
            catch
            {
                return false;
            }
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                const string targetVid = "CAFE";
                const string targetPid = "4001";

                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");

                foreach (var dev in searcher.Get().Cast<ManagementObject>())
                {
                    var name = (dev["Name"] as string) ?? string.Empty;
                    var deviceId = (dev["DeviceID"] as string) ?? string.Empty;

                    Console.WriteLine($"DEBUG PnP: {name} | {deviceId}");

                    var vid = Extract(deviceId, "VID_");
                    var pid = Extract(deviceId, "PID_");

                    bool vidPidMatch =
                        string.Equals(vid, targetVid, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(pid, targetPid, StringComparison.OrdinalIgnoreCase);

                    if (!vidPidMatch && !name.Contains("KaSe CDC", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var com = Between(name, "(COM", ")");
                    if (!string.IsNullOrEmpty(com) &&
                        string.Equals(com, portName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch
            {
                return false;
            }
            return false;
        }
        return false;
    }

    private static string Extract(string source, string token)
    {
        var i = source.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        return i < 0 || i + token.Length + 4 > source.Length
            ? string.Empty
            : source.Substring(i + token.Length, 4);
    }

    private static string Between(string source, string start, string end)
    {
        var i = source.IndexOf(start, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return string.Empty;
        var j = source.IndexOf(end, i, StringComparison.OrdinalIgnoreCase);
        return j <= i ? string.Empty : source.Substring(i + 1, j - i - 1);
    }    

    // Parsed macros list accessible from the app
    

    private void ParseMacrosPayload(List<byte> payload)
    {
        App.Macros.Clear();
        if (payload == null || payload.Count == 0)
        {
            Console.WriteLine("No macro payload.");
            return;
        }

        int offset = 0;
        if (offset + 1 > payload.Count)
        {
            Console.WriteLine("Invalid macro payload: missing count");
            return;
        }

        byte count = payload[offset++];
        Console.WriteLine($"Expected macros count: {count}");

        int parsed = 0;
        while (offset < payload.Count && parsed < count)
        {
            // index
            if (offset + 1 > payload.Count)
            {
                Console.WriteLine("Truncated payload while reading index");
                break;
            }
            byte idx = payload[offset++];

            // keycode (2 bytes LE)
            if (offset + 2 > payload.Count)
            {
                Console.WriteLine("Truncated payload while reading keycode");
                break;
            }
            K_Keys kc = (K_Keys)(payload[offset] | (payload[offset + 1] << 8));
            offset += 2;

            // name length
            if (offset + 1 > payload.Count)
            {
                Console.WriteLine("Truncated payload while reading name length");
                break;
            }
            byte nameLen = payload[offset++];

            if (offset + nameLen > payload.Count)
            {
                Console.WriteLine("Truncated payload while reading name");
                break;
            }
            string name = Encoding.ASCII.GetString(payload.GetRange(offset, nameLen).ToArray());
            offset += nameLen;

            // keys length
            if (offset + 1 > payload.Count)
            {
                Console.WriteLine("Truncated payload while reading keys length");
                break;
            }
            byte keysLen = payload[offset++];

            if (offset + keysLen > payload.Count)
            {
                Console.WriteLine("Truncated payload while reading keys");
                break;
            }

            var keys = new ObservableCollection<K_Keys>();
            for (int k = 0; k < keysLen; k++)
            {
                keys.Add((K_Keys)payload[offset + k]);
            }
            offset += keysLen;

            var mi = new MacroInfo
            {
                Index = idx,
                Keycode = kc,
                Name = name,
                Keys = keys
            };
            App.Macros.Add(mi);

            //Console.WriteLine($"Parsed macro idx={mi.Index}, kc=0x{mi.Keycode:X4}, name='{mi.Name}', keysLen={mi.Keys.Count}");
            parsed++;
        }

        Console.WriteLine($"Total macros parsed: {App.Macros.Count}");
    }
    
    public void AddMacro(MacroInfo macro, bool refreshAfter = true)
    {
        Console.WriteLine("Add Macro " + (macro?.Name ?? "(null)"));
        if (macro == null)
        {
            Console.WriteLine("Macro is null");
            return;
        }

        if (!IsPortOpen)
        {
            Console.WriteLine("Cannot add macro: serial port not open");
            return;
        }

        // Validate slot/index
        int slot = macro.Index;
        if (slot < 0)
        {
            Console.WriteLine("Invalid macro slot: " + slot);
            return;
        }

        // Sanitize name: remove command delimiters and control chars that would break parsing on MCU
        string name = macro.Name ?? string.Empty;
        // Remove semicolons and commas (used as separators) and newlines/carriage returns
        var banned = new char[] { ';', '\n', '\r' };
        foreach (var c in banned)
            name = name.Replace(c, ' ');
        name = name.Trim();

        if (name.Length == 0)
        {
            Console.WriteLine("Macro name is empty after sanitization");
            return;
        }

        // Limit name length to a reasonable size (MCU code limits name length to 255, but keep smaller for safety)
        const int MaxNameLen = 128;
        if (name.Length > MaxNameLen)
            name = name.Substring(0, MaxNameLen);

        // Prepare keys: accept non-zero keys only, up to 6
        var keys = macro.Keys ?? new ObservableCollection<K_Keys>();
        var nonZeroKeys = keys.Where(b => b != 0).ToList();
        if (nonZeroKeys.Count == 0)
        {
            Console.WriteLine("Macro must contain at least one key");
            return;
        }

        if (nonZeroKeys.Count > 6)
        {
            // Trim to 6
            nonZeroKeys = nonZeroKeys.Take(6).ToList();
        }

        // Validate key values are in 0..255 (should be by type)
        foreach (var b in nonZeroKeys)
        {
            if ((byte)b > 0xFF)
            {
                Console.WriteLine("Invalid key value: " + b);
                return;
            }
        }

        // Format keys as comma separated values, using 0xHH hex format so MCU strtoul(base=0) accepts them
        string keysStr = string.Join(",", nonZeroKeys.Select(b => "0x" + ((ushort)b).ToString("X2")));

        // Build final argument: slot;name;hex1,hex2,...
        // Note: MCU expects arguments after command name, so we include a space between command and args
        string arg = $"{slot};{name};{keysStr}";

        // Send the command
        SendCommand($"MACROADD {arg}");

        // Optionally request refreshed macros list after a short delay
        // Fire-and-forget: ask MCU to resend macros; device will respond asynchronously
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(150);
                if (refreshAfter)
                    GetMacros();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while refreshing macros: " + ex.Message);
            }
        });
    }

    public void DeleteMacro(MacroInfo macro)
    {
        if (!IsPortOpen || macro == null)
            return;
        SendCommand($"MACRODEL {macro.Index}");
        Task.Run(async () =>
        {
            await Task.Delay(150);
            GetMacros();
        });
    }

    private async Task ClearDeviceMacrosAsync(Action stepCallback, CancellationToken cancellationToken)
    {
        for (int slot = 0; slot < MacroSlotCount; slot++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendCommand($"MACRODEL {slot}");
            stepCallback();
            await Task.Delay(60, cancellationToken);
        }
    }

    private void SetLayerBulk(int layer, ObservableCollection<ObservableCollection<K_Keys>> rows)
    {
        if (!IsPortOpen || rows == null)
            return;

        int expected = App.Rows * App.Cols;
        var sb = new StringBuilder("SETLAYER");
        sb.Append(layer);
        sb.Append(':');

        int written = 0;
        for (int r = 0; r < App.Rows; r++)
        {
            var row = r < rows.Count ? rows[r] : null;
            for (int c = 0; c < App.Cols; c++)
            {
                if (written > 0)
                    sb.Append(',');

                ushort value = (ushort)(row != null && c < row.Count ? row[c] : K_Keys.K_NO);
                sb.Append("0x");
                sb.Append(value.ToString("X4"));
                written++;
            }
        }

        if (written != expected)
        {
            Console.WriteLine($"SETLAYER payload size mismatch: expected {expected}, written {written}");
        }

        SendCommand(sb.ToString());
    }

    public void SyncLayerToDevice(int layer)
    {
        if (!IsPortOpen)
        {
            Console.WriteLine("Cannot sync layer: serial port closed");
            return;
        }

        if (App.Keys == null || layer < 0 || layer >= App.Keys.Count)
        {
            Console.WriteLine($"Invalid layer index {layer} for sync");
            return;
        }

        SetLayerBulk(layer, App.Keys[layer]);
    }

    public void SyncLayoutName(int layer)
    {
        if (!IsPortOpen)
            return;

        string name = layer >= 0 && layer < App.LayoutsName.Count
            ? App.LayoutsName[layer]
            : $"LAYER{layer}";
        SetLayerName(layer, name);
    }

    public void ImportLayerToDevice(int layer, LayerConfigDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));
        ConfigSerializer.ApplyLayer(layer, dto);
        SyncLayerToDevice(layer);
        SyncLayoutName(layer);
    }

    public LayerConfigDto ExportLayerSnapshot(int layer)
    {
        return ConfigSerializer.SnapshotLayer(layer);
    }

    public void SetLayerFromApp(int layer)
    {
        if (App.Keys == null)
        {
            Console.WriteLine("App.Keys is null");
            return;
        }
        if (layer < 0 || layer >= App.Keys.Count)
        {
            Console.WriteLine($"Invalid layer index for App.Keys: {layer}");
            return;
        }
        SetLayerBulk(layer, App.Keys[layer]);
    }

    public async Task PushConfigAsync(AppConfigDto dto, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));
        if (!IsPortOpen)
            throw new InvalidOperationException("Le clavier n'est pas connecté");

        int layerOps = Math.Min(App.MaxLayers, App.Keys?.Count ?? 0);
        int layoutOps = App.MaxLayers;
        int macroOps = MacroSlotCount + (App.Macros?.Count ?? 0);
        double totalOps = Math.Max(1, layerOps + layoutOps + macroOps);
        double done = 0;
        void ReportProgress()
        {
            double pct = Math.Min(100, done / totalOps * 100);
            progress?.Report(pct);
        }

        for (int layer = 0; layer < layerOps; layer++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetLayerFromApp(layer);
            done++;
            ReportProgress();
            await Task.Delay(500, cancellationToken);
        }

        for (int i = 0; i < layoutOps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = i < App.LayoutsName.Count ? App.LayoutsName[i] : $"LAYER{i}";
            SetLayerName(i, name);
            done++;
            ReportProgress();
            await Task.Delay(80, cancellationToken);
        }

        await ClearDeviceMacrosAsync(() =>
        {
            done++;
            ReportProgress();
        }, cancellationToken);

        if (App.Macros != null)
        {
            foreach (var macro in App.Macros)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddMacro(macro, refreshAfter: false);
                done++;
                ReportProgress();
                await Task.Delay(150, cancellationToken);
            }
        }

        GetMacros();
        progress?.Report(100);
    }
}
