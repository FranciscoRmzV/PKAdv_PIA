using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Linq;

namespace PKPrueba1;

// Crea las tarjetas visuales (con sprite, PS y movimientos) que representan a un Pokémon en pantalla.
public static class PokemonCardFactory
{
    // Construye la tarjeta de un Pokémon. Si showExperience es true, también muestra su experiencia.
    public static Border Create(PokemonData pokemon, bool showExperience)
    {
        var info = new StackPanel { Spacing = 4 };

        // Fila del título: nombre, nivel y chips de tipo.
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        title.Children.Add(new TextBlock
        {
            Text = $"{pokemon.Name}  ·  Nv. {pokemon.Level}",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = UiTheme.Brush("#FFFFFF"),
            VerticalAlignment = VerticalAlignment.Center
        });

        title.Children.Add(UiTheme.TypeChips(pokemon.Species.Types));
        info.Children.Add(title);

        // Barra de PS con color según la vida restante.
        info.Children.Add(new ProgressBar
        {
            Minimum = 0,
            Maximum = Math.Max(1, pokemon.MaxHP),
            Value = Math.Clamp(pokemon.CurrentHP, 0, pokemon.MaxHP),
            Foreground = UiTheme.HpBrush(pokemon.CurrentHP, pokemon.MaxHP),
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Left
        });

        // Texto de PS (o "Debilitado") y, opcionalmente, de experiencia.
        string hpText = pokemon.IsFainted
            ? "Debilitado"
            : $"PS {pokemon.CurrentHP}/{pokemon.MaxHP}";

        if (showExperience && pokemon.Level < 100 && !pokemon.IsFainted)
            hpText += $"  ·  PE {pokemon.Experience}/{pokemon.ExpToNextLevel}";

        info.Children.Add(new TextBlock
        {
            Text = hpText,
            Foreground = UiTheme.Brush(pokemon.IsFainted ? "#F87171" : "#CBD5E1")
        });

        // Fila con los movimientos; cada uno tiene un borde del color de su tipo.
        var moves = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        foreach (PokemonMove move in pokemon.Moves)
        {
            moves.Children.Add(new Border
            {
                Background = UiTheme.Brush("#33FFFFFF"),
                BorderBrush = UiTheme.TypeBrush(move.Type),
                BorderThickness = new Thickness(2, 0, 0, 0),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                Child = new TextBlock
                {
                    Text = move.Name,
                    FontSize = 12,
                    Foreground = UiTheme.Brush("#E2E8F0")
                }
            });
        }

        info.Children.Add(moves);

        // Si lleva un objeto equipado, se muestra.
        if (pokemon.HeldItem != null)
        {
            info.Children.Add(new TextBlock
            {
                Text = "Objeto: " + pokemon.HeldItem,
                FontSize = 12,
                Foreground = UiTheme.Brush("#FDE68A")
            });
        }

        info.Children.Add(new TextBlock
        {
            Text = "Toca para ver las estadísticas",
            FontSize = 11,
            Foreground = UiTheme.Brush("#64748B")
        });

        // Disposición final: sprite a la izquierda e información a la derecha (el sprite se atenúa si está debilitado).
        var layout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };

        layout.Children.Add(new Image
        {
            Width = 84,
            Height = 84,
            Stretch = Stretch.Uniform,
            Opacity = pokemon.IsFainted ? 0.4 : 1.0,
            Source = new BitmapImage(new Uri(pokemon.SpriteUrl))
        });

        layout.Children.Add(info);

        // Envuelve el contenido en una tarjeta y, al tocarla, abre la ventana de estadísticas del Pokémon.
        Border card = UiTheme.Card(layout);

        card.Tapped += async (sender, _) =>
        {
            if (sender is FrameworkElement element && element.XamlRoot != null)
                await PokemonInfo.ShowAsync(element.XamlRoot, pokemon);
        };

        return card;
    }
}
