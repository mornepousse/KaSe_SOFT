using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace KaSe_Controller;

public class KeyboardUiRenderer
{
    public Control Render(JsonNode node)
    {
        if (node == null) return null;

        foreach (var kv in node.AsObject())
        {
            string type = kv.Key;
            JsonNode value = kv.Value;

            switch (type)
            {
                case "Group":
                    return CreateCanvas(value.AsObject());

                case "Line":
                    return CreateStackPanel(value.AsObject());

                case "Keycap":
                    return CreateKeycap(value.AsObject());

                default:
                    Console.WriteLine($"Type inconnu: {type}");
                    break;
            }
        }

        return null;
    }

    private Canvas CreateCanvas(JsonObject obj)
    {
        var canvas = new Canvas();

        if (obj.TryGetPropertyValue("Margin", out var margin))
            canvas.Margin = ParseThickness(margin.ToString());

        if (obj.TryGetPropertyValue("HorizontalAlignment", out var ha))
            canvas.HorizontalAlignment = ParseHorizontalAlignment(ha.ToString());

        if (obj.TryGetPropertyValue("Children", out var children))
        {
            foreach (var child in children.AsArray())
            {
                var ctrl = Render(child);
                if (ctrl != null)
                    canvas.Children.Add(ctrl);
            }
        }

        return canvas;
    }

    private StackPanel CreateStackPanel(JsonObject obj)
    {
        var sp = new StackPanel();

        if (obj.TryGetPropertyValue("Orientation", out var orient))
            sp.Orientation = orient.ToString() == "Horizontal"
                ? Orientation.Horizontal
                : Orientation.Vertical;

        if (obj.TryGetPropertyValue("Margin", out var margin))
            sp.Margin = ParseThickness(margin.ToString());

        if (obj.TryGetPropertyValue("RenderTransform", out var rt))
            sp.RenderTransform = ParseTransform(rt.AsObject());

        if (obj.TryGetPropertyValue("Children", out var children))
        {
            foreach (var child in children.AsArray())
            {
                var ctrl = Render(child);
                if (ctrl != null)
                    sp.Children.Add(ctrl);
            }
        }

        return sp;
    }

    private Control CreateKeycap(JsonObject obj)
    {
        // Ici j’utilise un simple Button pour représenter un "Keycap"
        var btn = new Keycap() { };

        if (obj.TryGetPropertyValue("Width", out var width))
            btn.Width = double.Parse(width.ToString());

        if (obj.TryGetPropertyValue("Margin", out var margin))
            btn.Margin = ParseThickness(margin.ToString());

        if (obj.TryGetPropertyValue("RenderTransform", out var rt))
            btn.RenderTransform = ParseTransform(rt.AsObject());

        if (obj.TryGetPropertyValue("Column", out var cl))
        {
            btn.Column = int.Parse(cl.ToString());
        }

        if (obj.TryGetPropertyValue("Row", out var rw))
        {
            btn.Row = int.Parse(rw.ToString());
        }

        return btn;
    }

    // === Helpers ===
    private Thickness ParseThickness(string s)
    {
        var parts = s.Split(',');
        return parts.Length switch
        {
            4 => new Thickness(
                double.Parse(parts[0]),
                double.Parse(parts[1]),
                double.Parse(parts[2]),
                double.Parse(parts[3])),
            _ => new Thickness(0)
        };
    }

    private Transform ParseTransform(JsonObject obj)
    {
        if (obj.TryGetPropertyValue("RotateTransform", out var rt))
        {
            var angle = rt.AsObject()["Angle"].GetValue<double>();
            return new RotateTransform(angle);
        }

        return null;
    }

    private HorizontalAlignment ParseHorizontalAlignment(string s) =>
        s switch
        {
            "Left" => HorizontalAlignment.Left,
            "Right" => HorizontalAlignment.Right,
            "Center" => HorizontalAlignment.Center,
            "Stretch" => HorizontalAlignment.Stretch,
            _ => HorizontalAlignment.Stretch
        };

    public static Control LoadDefaultJsonUi()
    {
        string json = File.ReadAllText("default.json");
        var node = JsonNode.Parse(json);
        var interpreter = new KeyboardUiRenderer();
        var ui = interpreter.Render(node);
        return ui;
    }
}