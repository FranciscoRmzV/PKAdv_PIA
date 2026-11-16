using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PKPrueba1
{
    // Pantalla del ranking global de la Torre Batalla y de Gimnasios.
    public sealed partial class RankingPage : Page
    {
        // Prepara la pantalla y construye el ranking cuando termina de cargar.
        public RankingPage()
        {
            InitializeComponent();

            Background = UiTheme.Backdrop();

            UiTheme.StyleButton(BackButton, "#334155");
            UiTheme.SetIcon(BackButton, "repel", "MENÚ");

            Loaded += (_, _) => BuildRanking();
        }

        // Carga los récords, actualiza los subtítulos y rellena las dos listas (torre y gimnasios).
        private void BuildRanking()
        {
            List<RankingEntry> entries = RankingManager.Load();

            SubtitleText.Text =
                $"Mejores resultados de cada jugador que ha jugado en este equipo ({entries.Count} " +
                (entries.Count == 1 ? "jugador" : "jugadores") + ").";

            GymSubtitle.Text = $"Combates ganados del desafío (de {GymManager.StageCount})";

            Fill(
                TowerList,
                RankingManager.Rank(entries, e => e.TowerBest),
                value => value == 1 ? "1 combate" : $"{value} combates",
                null);

            Fill(
                GymList,
                RankingManager.Rank(entries, e => e.GymBest),
                value => $"{value}/{GymManager.StageCount}",
                value => value >= GymManager.StageCount ? "Campeón" : null);
        }

        // Devuelve el icono de la medalla según la posición: Master Ball, Ultra Ball, Super Ball y Poké Ball.
        private static string MedalFor(int position)
        {
            return position switch
            {
                1 => "master-ball",
                2 => "ultra-ball",
                3 => "great-ball",
                _ => "poke-ball"
            };
        }

        // Rellena una lista del ranking con una tarjeta por jugador: medalla, posición, nombre, insignia opcional y puntuación.
        private void Fill(
            StackPanel list,
            List<(int Position, RankingEntry Entry, int Value)> ranking,
            Func<int, string> format,
            Func<int, string?>? badge)
        {
            list.Children.Clear();

            // Si nadie tiene récord en este modo se muestra un mensaje.
            if (ranking.Count == 0)
            {
                list.Children.Add(new TextBlock
                {
                    Text = "Todavía no hay récords en este modo.",
                    Foreground = UiTheme.Brush("#94A3B8"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4, 8, 4, 0)
                });

                return;
            }

            foreach (var (position, entry, value) in ranking)
            {
                // Indica si la fila es del jugador conectado (se resalta con "(tú)" y otro color).
                bool current = string.Equals(entry.Username, GameSession.Username, StringComparison.OrdinalIgnoreCase);

                var grid = new Grid { ColumnSpacing = 12 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var icon = UiTheme.ItemIcon(MedalFor(position), position <= 3 ? 30 : 22);
                Grid.SetColumn(icon, 0);
                grid.Children.Add(icon);

                var number = new TextBlock
                {
                    Text = $"#{position}",
                    FontSize = 18,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = UiTheme.Brush(position == 1 ? "#FDE047" : position <= 3 ? "#E2E8F0" : "#94A3B8"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(number, 1);
                grid.Children.Add(number);

                var name = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                name.Children.Add(new TextBlock
                {
                    Text = current ? $"{entry.Username} (tú)" : entry.Username,
                    FontSize = 17,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = UiTheme.Brush("#FFFFFF"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                // Texto extra bajo el nombre, por ejemplo "Campeón" si completó todos los gimnasios.
                string? extra = badge?.Invoke(value);

                if (extra != null)
                {
                    name.Children.Add(new TextBlock
                    {
                        Text = extra,
                        FontSize = 12,
                        Foreground = UiTheme.Brush("#FDE68A")
                    });
                }

                Grid.SetColumn(name, 2);
                grid.Children.Add(name);

                var score = new TextBlock
                {
                    Text = format(value),
                    FontSize = 17,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = UiTheme.Brush("#86EFAC"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(score, 3);
                grid.Children.Add(score);

                list.Children.Add(UiTheme.Card(
                    grid,
                    current ? "#CC1D4ED8" : "#CC1E293B",
                    10));
            }
        }

        // Vuelve a la pantalla anterior o, si no hay, al menú principal.
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(MainMenuPage));
        }
    }
}
