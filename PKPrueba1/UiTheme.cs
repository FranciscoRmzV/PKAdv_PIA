using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Collections.Generic;

namespace PKPrueba1;

// Utilidades de aspecto visual: colores, pinceles, fondos, tarjetas, chips de tipo e iconos.
public static class UiTheme
{
    // Color asociado a cada tipo de Pokémon (formato hexadecimal).
    private static readonly Dictionary<string, string> TypeColors = new()
    {
        { "NOR", "#A8A77A" }, { "FUE", "#EE8130" }, { "AGU", "#6390F0" }, { "PLA", "#7AC74C" },
        { "ELE", "#F7D02C" }, { "HIE", "#96D9D6" }, { "LUC", "#C22E28" }, { "VEN", "#A33EA1" },
        { "TIE", "#E2BF65" }, { "VOL", "#A98FF3" }, { "PSI", "#F95587" }, { "BIC", "#A6B91A" },
        { "ROC", "#B6A136" }, { "FAN", "#735797" }, { "DRA", "#6F35FC" }, { "SIN", "#705746" },
        { "ACE", "#B7B7CE" }, { "HAD", "#EE99AC" }
    };

    // Tipos de color claro, cuyo texto se muestra en oscuro para que se lea bien.
    private static readonly HashSet<string> LightTypes = new() { "ELE", "HIE", "TIE", "ACE", "NOR" };

    // Convierte un color hexadecimal (#RRGGBB o #AARRGGBB) en un objeto Color.
    public static Windows.UI.Color Hex(string hex)
    {
        hex = hex.TrimStart('#');

        byte a = 255;
        int offset = 0;

        if (hex.Length == 8)
        {
            a = System.Convert.ToByte(hex.Substring(0, 2), 16);
            offset = 2;
        }

        return ColorHelper.FromArgb(
            a,
            System.Convert.ToByte(hex.Substring(offset, 2), 16),
            System.Convert.ToByte(hex.Substring(offset + 2, 2), 16),
            System.Convert.ToByte(hex.Substring(offset + 4, 2), 16));
    }

    // Crea un pincel de color sólido a partir de un color hexadecimal.
    public static SolidColorBrush Brush(string hex) => new SolidColorBrush(Hex(hex));

    // Devuelve el color hexadecimal de un tipo (gris si no se conoce).
    public static string TypeColor(string type)
    {
        return TypeColors.TryGetValue(type, out string? c) ? c : "#777777";
    }

    // Devuelve el pincel con el color de un tipo.
    public static SolidColorBrush TypeBrush(string type)
    {
        return Brush(TypeColors.TryGetValue(type, out string? c) ? c : "#777777");
    }

    // Pincel de la barra de vida: verde (más del 50%), amarillo (más del 20%) o rojo.
    public static Brush HpBrush(int current, int max)
    {
        double ratio = max <= 0 ? 0 : (double)current / max;

        if (ratio > 0.5) return Brush("#22C55E");
        if (ratio > 0.2) return Brush("#EAB308");
        return Brush("#EF4444");
    }

    // Crea el degradado diagonal que se usa como fondo de las pantallas.
    public static LinearGradientBrush Backdrop(string top = "#0B1220", string bottom = "#1E1B4B")
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1)
        };

        brush.GradientStops.Add(new GradientStop { Color = Hex(top), Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = Hex(bottom), Offset = 1 });

        return brush;
    }

    // Crea una etiqueta redondeada con el nombre y el color de un tipo.
    public static Border TypeChip(string type)
    {
        return new Border
        {
            Background = TypeBrush(type),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(9, 2, 9, 2),
            Child = new TextBlock
            {
                Text = TypeChart.DisplayName(type),
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = Brush(LightTypes.Contains(type) ? "#1F2937" : "#FFFFFF")
            }
        };
    }

    // Crea una fila con las etiquetas de varios tipos.
    public static StackPanel TypeChips(IEnumerable<string> types, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            HorizontalAlignment = alignment
        };

        foreach (string type in types)
            panel.Children.Add(TypeChip(type));

        return panel;
    }

    // Envuelve un elemento en una tarjeta con fondo translúcido, esquinas redondeadas y borde.
    public static Border Card(UIElement child, string background = "#CC172033", double padding = 12)
    {
        return new Border
        {
            Padding = new Thickness(padding),
            CornerRadius = new CornerRadius(14),
            Background = Brush(background),
            BorderBrush = Brush("#33FFFFFF"),
            BorderThickness = new Thickness(1),
            Child = child
        };
    }

    // URL base de las imágenes de objetos.
    private const string ItemBase =
        "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/";

    // Crea la imagen de un objeto a partir de su nombre de sprite.
    public static Image ItemIcon(string item, double size = 26)
    {
        return new Image
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            Source = new BitmapImage(new System.Uri(ItemBase + item + ".png"))
        };
    }

    // Pone en un botón un icono de objeto seguido de un texto.
    public static void SetIcon(Button button, string item, string text, double size = 26, double fontSize = 0)
    {
        var label = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (fontSize > 0)
            label.FontSize = fontSize;

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        panel.Children.Add(ItemIcon(item, size));
        panel.Children.Add(label);

        button.Content = panel;
    }

    // Da al botón el color de fondo indicado, texto blanco y esquinas redondeadas.
    public static void StyleButton(Button button, string background)
    {
        button.Background = Brush(background);
        button.Foreground = Brush("#FFFFFF");
        button.CornerRadius = new CornerRadius(10);
    }

    // Oscurece un color multiplicando sus componentes por el factor (por ejemplo 0.72 = 28% más oscuro).
    public static string Darken(string hex, double factor)
    {
        var c = Hex(hex);

        return $"#{(byte)(c.R * factor):X2}{(byte)(c.G * factor):X2}{(byte)(c.B * factor):X2}";
    }

    // Estiliza el botón y define los colores al pasar el ratón, al pulsar y cuando está deshabilitado.
    public static void StyleButtonHover(Button button, string background, string? disabledBackground = null)
    {
        StyleButton(button, background);

        // Colores derivados para cada estado del botón.
        string hover = Darken(background, 0.72);
        string pressed = Darken(background, 0.55);
        string disabled = disabledBackground ?? "#334155";

        SolidColorBrush white = Brush("#FFFFFF");
        SolidColorBrush mutedText = Brush("#94A3B8");

        // Sobrescribe los recursos del tema para que WinUI use estos colores en cada estado.
        button.Resources["ButtonBackground"] = Brush(background);
        button.Resources["ButtonBackgroundPointerOver"] = Brush(hover);
        button.Resources["ButtonBackgroundPressed"] = Brush(pressed);
        button.Resources["ButtonBackgroundDisabled"] = Brush(disabled);

        button.Resources["ButtonBorderBrush"] = Brush(background);
        button.Resources["ButtonBorderBrushPointerOver"] = Brush(hover);
        button.Resources["ButtonBorderBrushPressed"] = Brush(pressed);
        button.Resources["ButtonBorderBrushDisabled"] = Brush(disabled);

        button.Resources["ButtonForeground"] = white;
        button.Resources["ButtonForegroundPointerOver"] = white;
        button.Resources["ButtonForegroundPressed"] = white;
        button.Resources["ButtonForegroundDisabled"] = mutedText;
    }
}
