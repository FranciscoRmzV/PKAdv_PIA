using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKPrueba1;

// Datos de una tarjeta de elección: título, subtítulo, imagen, color de acento, tipos, barra de vida e insignia.
public record ChoiceCard(
    string Title,
    string Subtitle,
    string? ImageUrl,
    string? Accent = null,
    IList<string>? Types = null,
    double? HpRatio = null,
    string? Badge = null);

// Diálogos del juego: mensajes, confirmaciones, elección de tarjetas y aprendizaje de movimientos.
public static class Dialogs
{
    // Ancho de cada tarjeta de elección.
    private const double CardWidth = 190;


    // Aplica el aspecto oscuro del juego a un diálogo y ajusta su ancho según las tarjetas por fila.
    public static void StyleDialog(ContentDialog dialog, int perRow = 3)
    {
        dialog.Background = UiTheme.Brush("#F20F172A");
        dialog.Foreground = UiTheme.Brush("#F1F5F9");
        dialog.CornerRadius = new CornerRadius(18);
        dialog.BorderBrush = UiTheme.Brush("#44FFFFFF");
        dialog.BorderThickness = new Thickness(1);

        double width = Math.Max(520, perRow * (CardWidth + 10) + 110);
        dialog.Resources["ContentDialogMaxWidth"] = width;
        dialog.Resources["ContentDialogMinWidth"] = Math.Min(width, 520);

        // Convierte el título de texto en un TextBlock grande y en negrita.
        if (dialog.Title is string title)
        {
            dialog.Title = new TextBlock
            {
                Text = title,
                FontSize = 24,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            };
        }
    }

    // Construye el contenido de una tarjeta: imagen, título, tipos, barra de vida, subtítulo e insignia.
    private static StackPanel BuildCardContent(ChoiceCard card, double imageSize)
    {
        var content = new StackPanel
        {
            Spacing = 5,
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = CardWidth - 24
        };

        if (!string.IsNullOrWhiteSpace(card.ImageUrl))
        {
            content.Children.Add(new Image
            {
                Width = imageSize,
                Height = imageSize,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                Source = new BitmapImage(new Uri(card.ImageUrl))
            });
        }

        content.Children.Add(new TextBlock
        {
            Text = card.Title,
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Foreground = UiTheme.Brush("#FFFFFF")
        });

        if (card.Types != null && card.Types.Count > 0)
            content.Children.Add(UiTheme.TypeChips(card.Types, HorizontalAlignment.Center));

        if (card.HpRatio.HasValue)
        {
            double ratio = Math.Clamp(card.HpRatio.Value, 0, 1);

            content.Children.Add(new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = ratio * 100,
                Height = 8,
                Foreground = UiTheme.HpBrush((int)(ratio * 100), 100)
            });
        }

        if (!string.IsNullOrEmpty(card.Subtitle))
        {
            content.Children.Add(new TextBlock
            {
                Text = card.Subtitle,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = UiTheme.Brush("#CBD5E1")
            });
        }

        if (!string.IsNullOrEmpty(card.Badge))
        {
            content.Children.Add(new Border
            {
                Background = UiTheme.Brush("#33FFFFFF"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = card.Badge,
                    FontSize = 11,
                    Foreground = UiTheme.Brush("#FDE68A"),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                }
            });
        }

        return content;
    }

    // Da a un botón el aspecto de tarjeta con el borde del color de acento.
    private static void StyleCardButton(ButtonBase button, ChoiceCard card)
    {
        button.Width = CardWidth;
        button.MinHeight = 120;
        button.Padding = new Thickness(10);
        button.VerticalAlignment = VerticalAlignment.Top;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.CornerRadius = new CornerRadius(14);
        button.Background = UiTheme.Brush("#1E293B");
        button.BorderBrush = UiTheme.Brush(card.Accent ?? "#475569");
        button.BorderThickness = new Thickness(2);
    }

    // Envuelve el contenido en un área con scroll de altura máxima.
    private static ScrollViewer BuildScroll(StackPanel root)
    {
        return new ScrollViewer
        {
            Content = root,
            MaxHeight = 560,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    // Crea el texto de mensaje que se muestra sobre las tarjetas.
    private static TextBlock Message(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = UiTheme.Brush("#CBD5E1")
        };
    }


    // Muestra un mensaje informativo con un botón "Aceptar".
    public static async Task MessageAsync(XamlRoot xamlRoot, string title, string text)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 15
            },
            CloseButtonText = "Aceptar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };

        StyleDialog(dialog);

        await dialog.ShowAsync();
    }

    // Muestra una pregunta de sí/no. Devuelve true si el jugador acepta.
    public static async Task<bool> ConfirmAsync(
        XamlRoot xamlRoot,
        string title,
        string text,
        string yes = "Sí",
        string no = "No")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 15
            },
            PrimaryButtonText = yes,
            CloseButtonText = no,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        StyleDialog(dialog);

        ContentDialogResult result = await dialog.ShowAsync();

        return result == ContentDialogResult.Primary;
    }


    // Muestra tarjetas y devuelve el índice de la que elija el jugador (-1 si cancela). Sin botón de cancelar, el diálogo se repite hasta que elija.
    public static async Task<int> ChooseAsync(
        XamlRoot xamlRoot,
        string title,
        string? message,
        IList<ChoiceCard> cards,
        string? cancelText = null,
        int perRow = 3)
    {
        // Bucle que reabre el diálogo hasta obtener una elección válida.
        while (true)
        {
            int result = -1;

            var root = new StackPanel { Spacing = 12 };

            if (!string.IsNullOrEmpty(message))
                root.Children.Add(Message(message));

            var dialog = new ContentDialog
            {
                Title = title,
                XamlRoot = xamlRoot
            };

            if (!string.IsNullOrEmpty(cancelText))
                dialog.CloseButtonText = cancelText;

            StackPanel? row = null;

            for (int i = 0; i < cards.Count; i++)
            {
                // Empieza una fila nueva cada "perRow" tarjetas.
                if (i % perRow == 0)
                {
                    row = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10
                    };

                    root.Children.Add(row);
                }

                int index = i;
                ChoiceCard card = cards[i];

                var button = new Button { Content = BuildCardContent(card, 72) };
                StyleCardButton(button, card);

                // Al pulsar la tarjeta se guarda su índice y se cierra el diálogo.
                button.Click += (_, _) =>
                {
                    result = index;
                    dialog.Hide();
                };

                row!.Children.Add(button);
            }

            dialog.Content = BuildScroll(root);
            StyleDialog(dialog, perRow);

            await dialog.ShowAsync();

            if (result >= 0 || !string.IsNullOrEmpty(cancelText))
                return result;
        }
    }

    // Permite elegir exactamente "required" tarjetas (con botones de selección). Devuelve los índices elegidos en orden.
    public static async Task<List<int>> ChooseManyAsync(
        XamlRoot xamlRoot,
        string title,
        string? message,
        IList<ChoiceCard> cards,
        int required,
        int perRow = 3,
        IList<int>? preselected = null)
    {
        // Bucle que reabre el diálogo hasta que se confirme una selección válida.
        while (true)
        {
            var selected = new HashSet<int>(preselected ?? new List<int>());

            var root = new StackPanel { Spacing = 12 };

            if (!string.IsNullOrEmpty(message))
                root.Children.Add(Message(message));

            var dialog = new ContentDialog
            {
                Title = title,
                PrimaryButtonText = $"Confirmar ({selected.Count}/{required})",
                IsPrimaryButtonEnabled = selected.Count == required,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot
            };

            // Actualiza el texto del botón "Confirmar (n/total)" y lo habilita solo cuando se ha elegido el número exacto.
            void UpdateButton()
            {
                dialog.PrimaryButtonText = $"Confirmar ({selected.Count}/{required})";
                dialog.IsPrimaryButtonEnabled = selected.Count == required;
            }

            StackPanel? row = null;

            for (int i = 0; i < cards.Count; i++)
            {
                if (i % perRow == 0)
                {
                    row = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10
                    };

                    root.Children.Add(row);
                }

                int index = i;
                ChoiceCard card = cards[i];

                var toggle = new ToggleButton
                {
                    Content = BuildCardContent(card, 64),
                    IsChecked = selected.Contains(index)
                };

                StyleCardButton(toggle, card);

                // Al marcar una tarjeta: se impide pasar del máximo y se actualiza el botón.
                toggle.Checked += (_, _) =>
                {
                    if (selected.Count >= required)
                    {
                        toggle.IsChecked = false;
                        return;
                    }

                    selected.Add(index);
                    UpdateButton();
                };

                // Al desmarcar una tarjeta se quita de la selección.
                toggle.Unchecked += (_, _) =>
                {
                    selected.Remove(index);
                    UpdateButton();
                };

                row!.Children.Add(toggle);
            }

            dialog.Content = BuildScroll(root);
            StyleDialog(dialog, perRow);

            ContentDialogResult result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && selected.Count == required)
                return selected.OrderBy(x => x).ToList();
        }
    }


    // Texto corto de un movimiento: tipo, potencia y precisión (o "Estado" si no hace daño).
    public static string MoveText(PokemonMove move)
    {
        return move.IsStatus
            ? $"{TypeChart.DisplayName(move.Type)} · Estado · Prec. {(move.Accuracy >= 101 ? "—" : move.Accuracy.ToString())}"
            : $"{TypeChart.DisplayName(move.Type)} · Poder {move.Power} · Prec. {(move.Accuracy >= 101 ? "—" : move.Accuracy.ToString())}";
    }

    // Texto corto de un Pokémon: nivel y PS.
    public static string PokemonText(PokemonData pokemon)
    {
        return $"Nv. {pokemon.Level} · PS {pokemon.CurrentHP}/{pokemon.MaxHP}";
    }

    // Crea la tarjeta de un movimiento; marca con "STAB" si coincide con el tipo del Pokémon.
    public static ChoiceCard MoveCard(PokemonMove move, bool stab = false)
    {
        string accuracy = move.Accuracy >= 101 ? "—" : move.Accuracy.ToString();
        string kind = move.IsStatus ? "Estado" : move.IsSpecial ? "Especial" : "Físico";

        string line = move.IsStatus
            ? $"{kind} · Prec. {accuracy}"
            : $"{kind} · Poder {(move.Effects.FixedLevel || move.Effects.Fixed > 0 ? "fijo" : move.Power.ToString())} · Prec. {accuracy}";

        string effects = move.Effects.Describe();

        return new ChoiceCard(
            move.Name,
            string.IsNullOrEmpty(effects) ? line : line + "\n" + effects,
            null,
            UiTheme.TypeColor(move.Type),
            new List<string> { move.Type },
            null,
            stab && !move.IsStatus ? "★ STAB (+50%)" : null);
    }

    // Crea la tarjeta de un Pokémon (con borde rojo oscuro si está debilitado).
    public static ChoiceCard PokemonCard(PokemonData p, bool showHp = true)
    {
        return new ChoiceCard(
            p.Name,
            showHp ? PokemonText(p) : $"Nv. {p.Level}",
            p.SpriteUrl,
            p.IsFainted ? "#7F1D1D" : null,
            p.Species.Types.ToList(),
            showHp ? (p.MaxHP <= 0 ? 0 : (double)p.CurrentHP / p.MaxHP) : null,
            p.HeldItem != null ? "Objeto: " + p.HeldItem : null);
    }

    // Crea la tarjeta de un objeto, con color e insignia según su tipo y la cantidad si es mayor que 0.
    public static ChoiceCard ItemCard(ItemData item, int count = 0)
    {
        string accent = item.Kind switch
        {
            ItemKind.Heal => "#22C55E",
            ItemKind.Revive => "#EC4899",
            ItemKind.Boost => "#F59E0B",
            ItemKind.Cure => "#14B8A6",
            ItemKind.MegaStone => "#A855F7",
            _ => "#3B82F6"
        };

        string kind = item.Kind switch
        {
            ItemKind.Heal or ItemKind.Revive => "Curación",
            ItemKind.Boost => "Combate",
            ItemKind.Cure => "Curación de estado",
            ItemKind.MegaStone => "Megapiedra",
            _ => "Equipable"
        };

        return new ChoiceCard(
            count > 0 ? $"{item.Name} x{count}" : item.Name,
            item.Description,
            item.SpriteUrl,
            accent,
            null,
            null,
            kind);
    }

    // Convierte una lista de Pokémon en tarjetas.
    public static List<ChoiceCard> PartyCards(IEnumerable<PokemonData> pokemon)
    {
        return pokemon.Select(p => PokemonCard(p)).ToList();
    }

    // Gestiona que un Pokémon aprenda un movimiento: lo añade si hay hueco; si ya sabe 4, deja elegir cuál olvidar o no aprenderlo.
    public static async Task LearnMoveAsync(XamlRoot xamlRoot, PokemonData pokemon, string moveName)
    {
        PokemonMove? move = MoveCatalog.Create(moveName);

        if (move == null || pokemon.Moves.Any(m => m.Name == move.Name))
            return;

        if (pokemon.Moves.Count < 4)
        {
            pokemon.Moves.Add(move);

            await MessageAsync(
                xamlRoot,
                "¡Nuevo movimiento!",
                $"¡{pokemon.Name} aprendió {move.Name}!\n{MoveText(move)}");

            return;
        }

        List<ChoiceCard> cards = pokemon.Moves
            .Select(m => MoveCard(m, pokemon.Species.Types.Contains(m.Type)))
            .ToList();

        int index = await ChooseAsync(
            xamlRoot,
            $"{pokemon.Name} quiere aprender {move.Name}",
            $"Nuevo movimiento: {MoveText(move)}\nElige un movimiento para olvidar o no aprenderlo.",
            cards,
            "No aprender",
            2);

        if (index < 0)
            return;

        string forgotten = pokemon.Moves[index].Name;
        pokemon.Moves[index] = move;

        await MessageAsync(
            xamlRoot,
            "¡Nuevo movimiento!",
            $"{pokemon.Name} olvidó {forgotten} y aprendió {move.Name}.");
    }
}
