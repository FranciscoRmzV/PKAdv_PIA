using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PKPrueba1;

// Ventanas de información con las estadísticas de un Pokémon o de una especie.
public static class PokemonInfo
{
    // Crea una fila de estadística: nombre, barra de color y valor.
    private static Grid StatRow(string label, int value, int max, string color, string? valueText = null)
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

        var name = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = Math.Max(1, max),
            Value = Math.Clamp(value, 0, Math.Max(1, max)),
            Foreground = UiTheme.Brush(color),
            MinWidth = 180,
            VerticalAlignment = VerticalAlignment.Center
        };
        var number = new TextBlock
        {
            Text = valueText ?? value.ToString(),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        Grid.SetColumn(name, 0);
        Grid.SetColumn(bar, 1);
        Grid.SetColumn(number, 2);

        grid.Children.Add(name);
        grid.Children.Add(bar);
        grid.Children.Add(number);

        return grid;
    }

    // Crea la cabecera con el sprite, el nombre, los tipos y una línea de texto extra.
    private static StackPanel Header(string spriteUrl, string name, PokemonSpecies species, string? extra)
    {
        var info = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };

        info.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold
        });

        info.Children.Add(UiTheme.TypeChips(species.Types));

        if (!string.IsNullOrEmpty(extra))
            info.Children.Add(new TextBlock { Text = extra, Opacity = 0.8 });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };

        header.Children.Add(new Image
        {
            Width = 96,
            Height = 96,
            Stretch = Stretch.Uniform,
            Source = new BitmapImage(new Uri(spriteUrl))
        });

        header.Children.Add(info);

        return header;
    }

    // Muestra el contenido dentro de un diálogo con scroll y botón "Cerrar".
    private static Task ShowAsync(XamlRoot xamlRoot, string title, StackPanel content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                Content = content,
                MaxHeight = 520,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            },
            CloseButtonText = "Cerrar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };

        Dialogs.StyleDialog(dialog, 2);

        return dialog.ShowAsync().AsTask();
    }

    // Muestra la ficha de un Pokémon concreto: nivel, PS, estadísticas, habilidad, naturaleza, objeto y movimientos.
    public static Task ShowAsync(XamlRoot xamlRoot, PokemonData pokemon)
    {
        var root = new StackPanel { Spacing = 8, MinWidth = 420 };

        string extra = $"Nivel {pokemon.Level}" +
            (pokemon.Level < 100 ? $" · PE {pokemon.Experience}/{pokemon.ExpToNextLevel}" : "");

        root.Children.Add(Header(pokemon.CurrentSpriteUrl, pokemon.DisplayName, pokemon.Species, extra));

        // Valor máximo de las barras (crece con el nivel para que se vean proporcionadas).
        int max = Math.Max(100, pokemon.Level * 3 + 40);

        root.Children.Add(StatRow(
            "PS",
            pokemon.CurrentHP,
            Math.Max(1, pokemon.MaxHP),
            pokemon.IsFainted ? "#EF4444" : "#22C55E",
            pokemon.IsFainted ? "Debilitado" : $"{pokemon.CurrentHP}/{pokemon.MaxHP}"));

        root.Children.Add(StatRow("Ataque", pokemon.Attack, max, "#F59E0B"));
        root.Children.Add(StatRow("Defensa", pokemon.Defense, max, "#3B82F6"));
        root.Children.Add(StatRow("Ataque Esp.", pokemon.SpAttack, max, "#EC4899"));
        root.Children.Add(StatRow("Defensa Esp.", pokemon.SpDefense, max, "#10B981"));
        root.Children.Add(StatRow("Velocidad", pokemon.Speed, max, "#A855F7"));

        // Texto de habilidad (avisa si no tiene efecto en el juego), de naturaleza y de objeto equipado.
        AbilityInfo? ability = pokemon.AbilityData;
        NatureInfo? nature = pokemon.NatureData;

        root.Children.Add(new TextBlock
        {
            Text = ability != null
                ? $"Habilidad: {ability.Name} — {ability.Description}" + (Abilities.NoBattleEffect.Contains(ability.Identifier) ? " (sin efecto práctico en este juego)" : "")
                : "Habilidad: ninguna",
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiTheme.Brush("#A7F3D0"),
            Margin = new Thickness(0, 6, 0, 0)
        });

        root.Children.Add(new TextBlock
        {
            Text = nature != null ? $"Naturaleza: {nature.Name} ({nature.Describe()})" : "Naturaleza: neutra",
            Foreground = UiTheme.Brush("#BFDBFE")
        });

        ItemData? held = pokemon.HeldItem == null ? null : ItemCatalog.Get(pokemon.HeldItem);

        root.Children.Add(new TextBlock
        {
            Text = held != null
                ? $"Objeto: {held.Name} — {held.Description}"
                : "Sin objeto equipado",
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiTheme.Brush("#FDE68A"),
            Margin = new Thickness(0, 6, 0, 0)
        });

        root.Children.Add(new TextBlock
        {
            Text = "Movimientos",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Margin = new Thickness(0, 8, 0, 0)
        });

        // Una fila por movimiento: tipo, potencia, precisión, categoría y efectos.
        foreach (PokemonMove move in pokemon.EffectiveMoves)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

            row.Children.Add(UiTheme.TypeChip(move.Type));
            string effects = move.Effects.Describe();
            string kind = move.IsStatus ? "Estado" : move.IsSpecial ? "Especial" : "Físico";
            string power = move.IsStatus ? "" : $" · Poder {(move.Effects.FixedLevel || move.Effects.Fixed > 0 ? "fijo" : move.Power.ToString())}";

            row.Children.Add(new TextBlock
            {
                Text = $"{move.Name}{power} · Prec. {(move.Accuracy >= 101 ? "—" : move.Accuracy.ToString())} · {kind}" +
                       (effects.Length > 0 ? $"\n{effects}" : ""),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            });

            root.Children.Add(row);
        }

        return ShowAsync(xamlRoot, "Estadísticas", root);
    }

    // Muestra la ficha de una especie: estadísticas base y cadena de evolución.
    public static Task ShowSpeciesAsync(XamlRoot xamlRoot, int speciesId)
    {
        PokemonSpecies sp = PokemonDatabase.Get(speciesId);

        var root = new StackPanel { Spacing = 8, MinWidth = 420 };

        root.Children.Add(Header(
            PokemonSprites.GetFrontSprite(sp.Id),
            sp.Name,
            sp,
            $"Nº {sp.Id} · Total base {sp.BaseTotal}"));

        root.Children.Add(StatRow("PS", sp.HP, 255, "#22C55E"));
        root.Children.Add(StatRow("Ataque", sp.Attack, 255, "#F59E0B"));
        root.Children.Add(StatRow("Defensa", sp.Defense, 255, "#3B82F6"));
        root.Children.Add(StatRow("Ataque Esp.", sp.SpAttack, 255, "#EC4899"));
        root.Children.Add(StatRow("Defensa Esp.", sp.SpDefense, 255, "#10B981"));
        root.Children.Add(StatRow("Velocidad", sp.Speed, 255, "#A855F7"));

        // Texto con la preevolución y las evoluciones de la especie.
        string evolution = "";

        if (sp.PreEvolutionId.HasValue)
            evolution += $"Evoluciona de {PokemonDatabase.Get(sp.PreEvolutionId.Value).Name}.\n";

        if (sp.Evolutions.Count > 0)
        {
            evolution += "Evoluciona a: " + string.Join(
                ", ",
                sp.Evolutions.Select(e => $"{PokemonDatabase.Get(e.ToId).Name} (nivel {e.Level})"));
        }
        else
        {
            evolution += "No evoluciona más.";
        }

        root.Children.Add(new TextBlock
        {
            Text = evolution,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        });

        return ShowAsync(xamlRoot, "Estadísticas base", root);
    }
}
