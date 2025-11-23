using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Runtime.CompilerServices;

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
    c_ping_t,
    c_debug_t,

};
public class SerialPortManager : INotifyPropertyChanged
{
    #region event
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    #endregion event
    private SerialPort _serialPort;
    private List<byte> data = new List<byte>();
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
                    for (int r = 0; r < rows; r++)
                    {
                        App.Keys[layerTmp].Add(new ObservableCollection<K_Keys>());
                        for (int c = 0; c < cols; c++)
                        {
                           App.Keys[layerTmp][r].Add((K_Keys)(commandData[index] | (commandData[index + 1] << 8)));
                            index += 2;
                        }
                    }
                    App.UpdateKey();
                    break;
                case cdc_command_type.c_get_name_layer_t :
                    break;
                case cdc_command_type.c_ping_t :
                    Console.WriteLine("Ping received");
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
        //byte[] com = new byte[4 + length];
        List<byte> com = new List<byte>();
        com.Add((byte)'C');
        com.Add((byte)'>');
        com.Add((byte)cmd);
        //com[0] = (byte)'C';
        //com[1] = (byte)'>';
        //com[2] = (byte)cmd;
        if (data != null)
        {
            com.Add((byte)(length & 0xFF));
            com.Add((byte)((length >> 8) & 0xFF));
            //com[3] = (byte)(length & 0xFF);
            //com[4] = (byte)((length >> 8) & 0xFF);    
        }
        else
        {
            com.Add(0);
            com.Add(0);
            //com[3] = 0;
            //com[4] = 0;    
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
        // byte[] com = new byte[6];
        // com[0] = (byte)'C';
        // com[1] = (byte)'>';
        // com[2] = (byte)cdc_command_type.c_get_name_layer_t;
        // com[3] = 0;
        // com[4] = 1;
        // com[5] = (byte)layer;
    }
    private void SendCommand(string command)
    {
        if (!IsPortOpen)
            return;
        
        byte[] com = System.Text.Encoding.ASCII.GetBytes(command + "\n");
        _serialPort.Write(com, 0, com.Length);
    }
    public void GetKeymap(int layer)
    {
        Console.WriteLine("Get Keymap " + layer);
        //Send_Command(cdc_command_type.c_get_layer_t, new byte[]{(byte)layer});
        SendCommand($"KEYMAP{layer}");
    }
    public void SetKey(int layer, int row, int col, K_Keys key)
    {
        Console.WriteLine("Set Key " + layer + " " + row + " " + col + " " + key + " " + Convert.ToInt16(key).ToString("X"));
        SendCommand($"SETKEY {layer},{row},{col},{Convert.ToInt16(key).ToString("X")}");
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
    
}