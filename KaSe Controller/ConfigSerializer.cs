using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace KaSe_Controller;

public sealed class AppConfigDto
{
    public int SchemaVersion { get; set; } = 1;
    public List<List<List<ushort>>> Keys { get; set; } = new();
    public List<string> LayoutsName { get; set; } = new();
    public List<MacroDto> Macros { get; set; } = new();
}

public sealed class MacroDto
{
    public int Index { get; set; }
    public ushort Keycode { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<ushort> Keys { get; set; } = new();
}

public sealed class LayerConfigDto
{
    public int SchemaVersion { get; set; } = 1;
    public List<List<ushort>> Keys { get; set; } = new();
    public string LayoutName { get; set; } = string.Empty;
}

public static class ConfigSerializer
{
    private const int CurrentSchemaVersion = 1;

    public static AppConfigDto Snapshot()
    {
        var dto = new AppConfigDto
        {
            SchemaVersion = CurrentSchemaVersion,
            Keys = App.Keys.Select(layer =>
                layer.Select(row => row.Select(key => (ushort)key).ToList()).ToList()).ToList(),
            LayoutsName = App.LayoutsName.ToList(),
            Macros = App.Macros.Select(m => new MacroDto
            {
                Index = m.Index,
                Keycode = (ushort)m.Keycode,
                Name = m.Name ?? string.Empty,
                Keys = m.Keys.Select(k => (ushort)k).ToList()
            }).ToList()
        };

        return dto;
    }

    public static string ToJson(AppConfigDto dto)
    {
        return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
    }

    public static AppConfigDto FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<AppConfigDto>(json);
        if (dto == null)
            throw new InvalidDataException("Configuration vide ou invalide");
        if (dto.SchemaVersion <= 0)
            dto.SchemaVersion = 1;
        return dto;
    }

    public static LayerConfigDto SnapshotLayer(int layerIndex)
    {
        EnsureKeysCapacity();
        EnsureLayoutCapacity();

        if (layerIndex < 0 || layerIndex >= App.Keys.Count)
            throw new ArgumentOutOfRangeException(nameof(layerIndex));

        return new LayerConfigDto
        {
            SchemaVersion = CurrentSchemaVersion,
            Keys = App.Keys[layerIndex]
                .Select(row => row.Select(key => (ushort)key).ToList()).ToList(),
            LayoutName = App.LayoutsName[layerIndex]
        };
    }

    public static string LayerToJson(LayerConfigDto dto)
    {
        return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
    }

    public static LayerConfigDto LayerFromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<LayerConfigDto>(json);
        if (dto == null)
            throw new InvalidDataException("Configuration de couche vide ou invalide");
        if (dto.SchemaVersion <= 0)
            dto.SchemaVersion = 1;
        return dto;
    }

    public static void ApplyLayer(int layerIndex, LayerConfigDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        EnsureKeysCapacity();
        EnsureLayoutCapacity();

        if (layerIndex < 0 || layerIndex >= App.Keys.Count)
            throw new ArgumentOutOfRangeException(nameof(layerIndex));

        SyncLayer(App.Keys[layerIndex], dto.Keys);
        App.LayoutsName[layerIndex] = string.IsNullOrWhiteSpace(dto.LayoutName)
            ? $"LAYER{layerIndex}"
            : dto.LayoutName;

        App.UpdateKey();
    }

    public static void Apply(AppConfigDto dto)
    {
        if (dto.Keys == null || dto.Keys.Count == 0)
            throw new InvalidDataException("La configuration ne contient aucune couche");

        EnsureKeysCapacity();
        for (int l = 0; l < App.Keys.Count && l < dto.Keys.Count; l++)
        {
            SyncLayer(App.Keys[l], dto.Keys[l]);
        }

        for (int l = dto.Keys.Count; l < App.Keys.Count; l++)
        {
            FillLayerWithNo(App.Keys[l]);
        }

        SyncLayouts(dto.LayoutsName);
        SyncMacros(dto.Macros);

        App.UpdateKey();
    }

    private static void EnsureKeysCapacity()
    {
        for (int l = App.Keys.Count; l < App.MaxLayers; l++)
        {
            App.Keys.Add(CreateEmptyLayer());
        }
    }

    private static void EnsureLayoutCapacity()
    {
        while (App.LayoutsName.Count < App.MaxLayers)
            App.LayoutsName.Add($"LAYER{App.LayoutsName.Count}");
    }

    private static void SyncLayer(ObservableCollection<ObservableCollection<K_Keys>> target, List<List<ushort>>? source)
    {
        // Ensure correct number of rows without replacing the collection instance
        for (int r = target.Count; r < App.Rows; r++)
            target.Add(new ObservableCollection<K_Keys>(Enumerable.Repeat(K_Keys.K_NO, App.Cols)));
        while (target.Count > App.Rows)
            target.RemoveAt(target.Count - 1);

        for (int r = 0; r < App.Rows; r++)
        {
            var targetRow = target[r];

            // Ensure correct number of columns for each row (modify in place)
            while (targetRow.Count < App.Cols)
                targetRow.Add(K_Keys.K_NO);
            while (targetRow.Count > App.Cols)
                targetRow.RemoveAt(targetRow.Count - 1);

            var sourceRow = source != null && r < source.Count ? source[r] : null;

            // Write values in place to preserve bindings
            for (int c = 0; c < App.Cols; c++)
            {
                ushort value = sourceRow != null && c < sourceRow.Count ? sourceRow[c] : (ushort)K_Keys.K_NO;
                targetRow[c] = (K_Keys)value;
            }
        }
    }

    private static void SyncRow(ObservableCollection<K_Keys> targetRow, List<ushort>? sourceRow)
    {
        while (targetRow.Count < App.Cols)
            targetRow.Add(K_Keys.K_NO);

        for (int c = 0; c < App.Cols; c++)
        {
            ushort value = sourceRow != null && c < sourceRow.Count ? sourceRow[c] : (ushort)K_Keys.K_NO;
            targetRow[c] = (K_Keys)value;
        }
    }

    private static void FillLayerWithNo(ObservableCollection<ObservableCollection<K_Keys>> layer)
    {
        foreach (var row in layer)
        {
            for (int c = 0; c < App.Cols; c++)
            {
                if (c < row.Count)
                    row[c] = K_Keys.K_NO;
                else
                    row.Add(K_Keys.K_NO);
            }
        }
    }

    private static void SyncLayouts(List<string>? layouts)
    {
        EnsureLayoutCapacity();

        for (int i = 0; i < App.LayoutsName.Count; i++)
        {
            if (layouts != null && i < layouts.Count)
                App.LayoutsName[i] = layouts[i] ?? string.Empty;
            else
                App.LayoutsName[i] = $"LAYER{i}";
        }
    }

    private static void SyncMacros(List<MacroDto>? macroDtos)
    {
        App.Macros.Clear();
        if (macroDtos == null)
            return;

        foreach (var macro in macroDtos)
        {
            var macroKeys = macro.Keys?.Select(k => (K_Keys)k).ToList() ?? new List<K_Keys>();
            App.Macros.Add(new MacroInfo
            {
                Index = macro.Index,
                Keycode = (K_Keys)macro.Keycode,
                Name = macro.Name ?? string.Empty,
                Keys = new ObservableCollection<K_Keys>(macroKeys)
            });
        }
    }

    private static ObservableCollection<ObservableCollection<K_Keys>> CreateEmptyLayer()
    {
        var layer = new ObservableCollection<ObservableCollection<K_Keys>>();
        for (int r = 0; r < App.Rows; r++)
        {
            var row = new ObservableCollection<K_Keys>();
            for (int c = 0; c < App.Cols; c++)
            {
                row.Add(K_Keys.K_NO);
            }
            layer.Add(row);
        }
        return layer;
    }
}
