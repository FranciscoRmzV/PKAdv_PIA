using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKPrueba1
{
    // Pantalla del modo Gimnasios: primero se arma el equipo (cualquier Pokémon a nivel 100) y luego se afrontan los 8 líderes, el Alto Mando y el Campeón.
    public sealed partial class GymPage : Page
    {
        // true mientras hay un diálogo o proceso en curso; bloquea los botones.
        private bool busy;
        // true mientras se reconstruye la cuadrícula de Pokémon, para ignorar los eventos de selección que eso provoca.
        private bool rebuilding;
        // true mientras se corrige la selección por código (por ejemplo al pasar del máximo de 6).
        private bool adjusting;

        // Ids de las especies elegidas para el equipo.
        private readonly List<int> selectedIds = new();

        // Último equipo recomendado por el asistente de equipos.
        private List<int>? suggestedTeam;
        // true mientras el asistente está respondiendo a una pregunta.
        private bool advisorBusy;

        // Configura iconos y colores de los botones y espera a que la página cargue.
        public GymPage()
        {
            InitializeComponent();

            Background = UiTheme.Backdrop();

            UiTheme.SetIcon(StartButton, "poke-ball", "COMENZAR DESAFÍO");
            UiTheme.SetIcon(FightButton, "x-attack", "COMBATIR");
            UiTheme.SetIcon(ItemButton, "potion", "USAR OBJETO");
            UiTheme.SetIcon(LeadButton, "exp-share", "PRIMER POKÉMON");
            UiTheme.SetIcon(AbandonButton, "escape-rope", "ABANDONAR");
            UiTheme.SetIcon(BackButton, "repel", "MENÚ");

            UiTheme.StyleButton(StartButton, "#16A34A");
            UiTheme.StyleButton(FightButton, "#DC2626");
            UiTheme.StyleButton(ItemButton, "#2563EB");
            UiTheme.StyleButton(LeadButton, "#CA8A04");
            UiTheme.StyleButton(AbandonButton, "#475569");
            UiTheme.StyleButton(BackButton, "#334155");

            UiTheme.StyleButtonHover(TeamAdvisorToggleButton, "#7C3AED");
            UiTheme.StyleButtonHover(ApplyTeamButton, "#16A34A");
            UiTheme.StyleButtonHover(TeamAdvisorSendButton, "#2563EB");

            Loaded += OnLoaded;
        }

        // Al cargar la página: bloquea los botones, procesa mensajes y recompensas pendientes y actualiza la pantalla.
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            busy = true;
            RefreshUi();

            try
            {
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en Gimnasios: {ex}");
            }

            busy = false;
            RefreshUi();
        }

        // Muestra el mensaje de fin de desafío si lo hay; si hay recompensas pendientes de una partida en curso, las ofrece.
        private async Task InitializeAsync()
        {
            GameData data = GameSession.Data;

            if (GymManager.PendingMessage != null)
            {
                string message = GymManager.PendingMessage;
                GymManager.PendingMessage = null;

                RefreshUi();

                await Dialogs.MessageAsync(XamlRoot, "Fin del desafío", message);
                return;
            }

            if (data.GymActive && data.GymRewardItems.Count > 0)
                await RunRewardAsync();
        }


        // Tras cada victoria, el jugador elige varios objetos (RewardPicks) entre los ofrecidos y se añaden a la mochila del desafío.
        private async Task RunRewardAsync()
        {
            GameData data = GameSession.Data;

            while (data.GymActive && data.GymRewardItems.Count > 0)
            {
                List<ChoiceCard> cards = data.GymRewardItems
                    .Select(name =>
                    {
                        ItemData? item = ItemCatalog.Get(name);

                        return item != null
                            ? Dialogs.ItemCard(item)
                            : new ChoiceCard(name, "", null);
                    })
                    .ToList();

                int required = Math.Min(GymManager.RewardPicks, cards.Count);

                List<int> picked = await Dialogs.ChooseManyAsync(
                    XamlRoot,
                    "¡Victoria! Elige tus objetos",
                    $"Elige {required} objetos de {cards.Count}.",
                    cards,
                    required);

                foreach (int index in picked)
                    data.GymInventory.Add(data.GymRewardItems[index]);

                data.GymRewardItems.Clear();
                GameSession.Save();
            }

            RefreshUi();
        }


        // Rellena la cuadrícula con todas las especies (filtradas por el buscador): sprite, nombre, tipos y botón de estadísticas. Marca las ya seleccionadas.
        private void BuildPokemonGrid()
        {
            string filter = (SearchBox.Text ?? "").Trim();

            rebuilding = true;

            PokemonGrid.Items.Clear();

            foreach (PokemonSpecies species in PokemonDatabase.AllSpecies)
            {
                if (filter.Length > 0 &&
                    species.Name.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) < 0)
                    continue;

                var content = new StackPanel
                {
                    Width = 112,
                    Spacing = 3,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                content.Children.Add(new Image
                {
                    Width = 64,
                    Height = 64,
                    Stretch = Stretch.Uniform,
                    Source = new BitmapImage(new Uri(PokemonSprites.GetFrontSprite(species.Id)))
                });

                content.Children.Add(new TextBlock
                {
                    Text = species.Name,
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Colors.White)
                });

                content.Children.Add(UiTheme.TypeChips(species.Types, HorizontalAlignment.Center));

                int speciesId = species.Id;

                var statsButton = new Button
                {
                    Padding = new Thickness(6, 2, 6, 2),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                UiTheme.SetIcon(statsButton, "scope-lens", "Stats", 18, 11);

                statsButton.Click += async (_, _) =>
                    await PokemonInfo.ShowSpeciesAsync(XamlRoot, speciesId);

                content.Children.Add(statsButton);

                var item = new GridViewItem
                {
                    Content = content,
                    Tag = species.Id
                };

                PokemonGrid.Items.Add(item);

                if (selectedIds.Contains(species.Id))
                    PokemonGrid.SelectedItems.Add(item);
            }

            rebuilding = false;
        }

        // Mantiene la lista de ids seleccionados y no permite elegir más de 6 Pokémon.
        private void PokemonGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (rebuilding || adjusting)
                return;

            foreach (object removed in e.RemovedItems)
            {
                if (removed is GridViewItem item && item.Tag is int id)
                    selectedIds.Remove(id);
            }

            foreach (object added in e.AddedItems)
            {
                if (added is not GridViewItem item || item.Tag is not int id)
                    continue;

                if (selectedIds.Count >= PokemonPartyManager.MaxPartySize)
                {
                    adjusting = true;
                    PokemonGrid.SelectedItems.Remove(item);
                    adjusting = false;
                    continue;
                }

                selectedIds.Add(id);
            }

            UpdateSelectedText();
        }

        // Al escribir en el buscador se vuelve a construir la cuadrícula (solo antes de empezar el desafío).
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (GameSession.Data.GymActive)
                return;

            BuildPokemonGrid();
        }

        // Actualiza el texto con el equipo elegido y habilita "Comenzar desafío" si hay al menos un Pokémon.
        private void UpdateSelectedText()
        {
            if (selectedIds.Count == 0)
            {
                SelectedText.Text = "Equipo: 0/6 — elige al menos 1 Pokémon.";
            }
            else
            {
                string names = string.Join(
                    ", ",
                    selectedIds.Select(id => PokemonDatabase.Get(id).Name));

                SelectedText.Text = $"Equipo: {selectedIds.Count}/6 — {names}";
            }

            StartButton.IsEnabled = !busy && selectedIds.Count > 0;
        }



        // Muestra u oculta el panel del asistente de equipos; la primera vez escribe el mensaje de bienvenida.
        private void TeamAdvisorToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (TeamAdvisorPanel.Visibility == Visibility.Visible)
            {
                TeamAdvisorPanel.Visibility = Visibility.Collapsed;
                TeamAdvisorToggleButton.Content = "💬 Asistente de equipos";
                return;
            }

            TeamAdvisorPanel.Visibility = Visibility.Visible;
            TeamAdvisorToggleButton.Content = "Ocultar asistente";

            if (string.IsNullOrWhiteSpace(TeamAdvisorHistory.Text))
            {
                TeamAdvisorHistory.Text =
                    "Asistente: ¡Hola! Te ayudo a elegir tu equipo para los 8 gimnasios, el Alto Mando y el Campeón. " +
                    "Puedo recomendarte 6 Pokémon que se complementen, analizar los que ya elegiste, " +
                    "valorar uno concreto o darte datos de un líder. Usa los botones o escríbeme.";
            }
        }

        // Botones de preguntas rápidas: envían al asistente la pregunta guardada en su Tag.
        private void TeamAdvisorQuick_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string question })
                _ = AskTeamAdvisorAsync(question);
        }

        // Botón "Enviar": envía al asistente lo escrito.
        private void TeamAdvisorSendButton_Click(object sender, RoutedEventArgs e)
        {
            _ = AskTeamAdvisorAsync(TeamAdvisorInput.Text);
        }

        // Enter en el cuadro de texto: envía la pregunta al asistente.
        private void TeamAdvisorInput_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                _ = AskTeamAdvisorAsync(TeamAdvisorInput.Text);
            }
        }

        // Envía una pregunta a TeamAdvisor (en segundo plano para no congelar la interfaz), muestra la respuesta y, si incluye un equipo recomendado, muestra el botón para aplicarlo.
        private async Task AskTeamAdvisorAsync(string question)
        {
            question = (question ?? "").Trim();

            if (question.Length == 0 || advisorBusy)
                return;

            advisorBusy = true;
            TeamAdvisorInput.Text = "";
            TeamAdvisorInput.IsEnabled = false;
            TeamAdvisorSendButton.IsEnabled = false;

            List<int> current = selectedIds.ToList();

            try
            {
                TeamAdvice advice = await Task.Run(() => TeamAdvisor.Answer(question, current));

                AppendTeamAdvisor($"Tú: {question}\n\nAsistente: {advice.Text}");

                suggestedTeam = advice.TeamIds;
                ApplyTeamButton.Visibility = suggestedTeam is { Count: > 0 } ? Visibility.Visible : Visibility.Collapsed;

                if (suggestedTeam is { Count: > 0 })
                    ApplyTeamButton.Content = $"Usar este equipo ({suggestedTeam.Count})";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en el asistente de equipos: {ex}");
                AppendTeamAdvisor($"Tú: {question}\n\nAsistente: Algo falló al analizar tu pregunta. Inténtalo de nuevo.");
            }
            finally
            {
                advisorBusy = false;
                TeamAdvisorInput.IsEnabled = true;
                TeamAdvisorSendButton.IsEnabled = true;
                TeamAdvisorInput.Focus(FocusState.Programmatic);
            }
        }

        // Añade un mensaje al historial del chat y baja el scroll hasta el final.
        private void AppendTeamAdvisor(string text)
        {
            TeamAdvisorHistory.Text += (string.IsNullOrEmpty(TeamAdvisorHistory.Text) ? "" : "\n\n") + text;
            DispatcherQueue.TryEnqueue(() => TeamAdvisorScroll.ChangeView(null, TeamAdvisorScroll.ScrollableHeight, null));
        }

        // Aplica el equipo recomendado por el asistente como selección actual (máximo 6).
        private void ApplyTeamButton_Click(object sender, RoutedEventArgs e)
        {
            if (suggestedTeam == null || suggestedTeam.Count == 0)
                return;

            selectedIds.Clear();
            selectedIds.AddRange(suggestedTeam.Take(PokemonPartyManager.MaxPartySize));

            if (!string.IsNullOrEmpty(SearchBox.Text))
                SearchBox.Text = "";
            else
                BuildPokemonGrid();

            UpdateSelectedText();

            if (PokemonGrid.SelectedItems.Count > 0)
                PokemonGrid.ScrollIntoView(PokemonGrid.SelectedItems[0]);

            AppendTeamAdvisor("Asistente: Equipo seleccionado. Al comenzar el desafío podrás elegir movimientos, objeto, habilidad y naturaleza de cada uno; " +
                "recuerda equipar la megapiedra si te la recomendé.");
        }


        // Actualiza toda la pantalla: modo de preparación o modo partida en curso, botones, rival, medallas, equipo y objetos.
        private void RefreshUi()
        {
            GameData data = GameSession.Data;

            RecordText.Text =
                $"Récord: {data.GymBestStage}/{GymManager.StageCount} combates ganados";

            BuilderPanel.Visibility = data.GymActive ? Visibility.Collapsed : Visibility.Visible;
            RunPanel.Visibility = data.GymActive ? Visibility.Visible : Visibility.Collapsed;

            StartButton.Visibility = data.GymActive ? Visibility.Collapsed : Visibility.Visible;
            FightButton.Visibility = data.GymActive ? Visibility.Visible : Visibility.Collapsed;
            ItemButton.Visibility = data.GymActive ? Visibility.Visible : Visibility.Collapsed;
            LeadButton.Visibility = data.GymActive ? Visibility.Visible : Visibility.Collapsed;
            AbandonButton.Visibility = data.GymActive ? Visibility.Visible : Visibility.Collapsed;

            FightButton.IsEnabled = !busy;
            ItemButton.IsEnabled = !busy;
            LeadButton.IsEnabled = !busy;
            AbandonButton.IsEnabled = !busy;
            BackButton.IsEnabled = !busy;

            // Sin desafío activo: se muestra el constructor de equipo.
            if (!data.GymActive)
            {
                StatusText.Text = "Prepara tu equipo para el desafío (todos a nivel 100).";

                if (PokemonGrid.Items.Count == 0)
                    BuildPokemonGrid();

                UpdateSelectedText();
                return;
            }

            StartButton.IsEnabled = false;

            TeamPanel.Children.Clear();

            // Desafío activo: muestra el rival de la etapa actual (gimnasio, tipo y equipo).
            TrainerData trainer = GymManager.PrepareNextTrainer();
            int number = data.GymStage + 1;

            StatusText.Text =
                $"Combate {number}/{GymManager.StageCount} · {trainer.Name}";

            BadgeText.Text = data.Badges.Count == 0
                ? "Medallas: ninguna todavía"
                : "Medallas: " + string.Join(", ", data.Badges);

            if (trainer.Gym != null)
            {
                GymImage.Source = new BitmapImage(new Uri(trainer.Gym.Background));

                string typeText = trainer.Gym.Type == null
                    ? "Todos los tipos"
                    : $"Tipo {TypeChart.DisplayName(trainer.Gym.Type)}";

                GymText.Text =
                    $"{trainer.Gym.GymName}\n{trainer.Name} · {typeText}\nTodos los Pokémon a nivel 100";
            }

            string rivals = string.Join(", ", trainer.Team.Select(p => p.Name));

            OpponentText.Text =
                $"Equipo rival ({trainer.Team.Count}): {rivals}";

            foreach (PokemonData pokemon in data.GymTeam)
                TeamPanel.Children.Add(CreatePokemonCard(pokemon));

            if (data.GymInventory.Count > 0)
            {
                string items = string.Join(
                    ", ",
                    data.GymInventory
                        .GroupBy(x => x)
                        .Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key));

                TeamPanel.Children.Add(new TextBlock
                {
                    Text = $"Objetos: {items}",
                    Foreground = new SolidColorBrush(Colors.LightGray),
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }

        // Crea la tarjeta visual de un Pokémon del equipo.
        private Border CreatePokemonCard(PokemonData pokemon)
        {
            return PokemonCardFactory.Create(pokemon, false);
        }


        // Botón "Comenzar desafío": para cada Pokémon elegido permite escoger movimientos, objeto, habilidad y naturaleza, y luego inicia la partida.
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (busy || selectedIds.Count == 0)
                return;

            busy = true;
            RefreshUi();

            try
            {
                var team = new List<PokemonData>();
                int number = 0;

                foreach (int id in selectedIds.ToList())
                {
                    number++;

                    var pokemon = new PokemonData(id, TrainerGenerator.GymLevel, true);

                    // Movimientos que puede conocer a nivel 100, de más a menos potentes.
                    List<PokemonMove> options = PokemonDatabase.GetLearnset(id)
                        .Where(l => l.Level <= TrainerGenerator.GymLevel)
                        .Select(l => MoveCatalog.Create(l.Move))
                        .Where(m => m != null)
                        .Select(m => m!)
                        .OrderByDescending(m => m.Power)
                        .ToList();

                    // Si tiene más de 4 opciones, el jugador elige 4 (los recomendados vienen marcados).
                    if (options.Count > 4)
                    {
                        List<ChoiceCard> cards = options
                            .Select(m => Dialogs.MoveCard(m, pokemon.Species.Types.Contains(m.Type)))
                            .ToList();

                        List<int> recommended = pokemon.Moves
                            .Select(m => options.FindIndex(o => o.Name == m.Name))
                            .Where(i => i >= 0)
                            .ToList();

                        List<int> picked = await Dialogs.ChooseManyAsync(
                            XamlRoot,
                            $"Movimientos de {pokemon.Name} ({number}/{selectedIds.Count})",
                            "Elige 4 movimientos. Ya están marcados los recomendados; puedes cambiarlos. Los movimientos con ★ reciben bonificación por su tipo.",
                            cards,
                            4,
                            3,
                            recommended);

                        pokemon.Moves = picked.Select(i => options[i]).ToList();
                    }

                    // Objetos equipables posibles: los normales y la megapiedra de este Pokémon.
                    List<ItemData> items = ItemCatalog.All
                        .Where(i => i.IsEquippable &&
                            (i.Kind == ItemKind.Held || i.MegaBaseId == pokemon.Id))
                        .ToList();

                    var itemCards = new List<ChoiceCard> { new ChoiceCard("Sin objeto", "No lleva nada equipado.", null) };
                    itemCards.AddRange(items.Select(i => Dialogs.ItemCard(i)));

                    int itemIndex = await Dialogs.ChooseAsync(
                        XamlRoot,
                        $"Objeto para {pokemon.Name} ({number}/{selectedIds.Count})",
                        "Los objetos equipados cambian sus estadísticas en combate. Durante el desafío no aparecerán más objetos equipables.",
                        itemCards);

                    pokemon.HeldItem = itemIndex > 0 ? items[itemIndex - 1].Name : null;

                    // Si la especie tiene varias habilidades, el jugador elige una (las que tienen efecto en combate salen en verde).
                    List<AbilityInfo> abilities = Abilities.For(pokemon.Id);

                    if (abilities.Count > 1)
                    {
                        var abilityCards = abilities
                            .Select(a => new ChoiceCard(
                                a.Name,
                                a.Description + (Abilities.NoBattleEffect.Contains(a.Identifier) ? "\n(sin efecto práctico en este juego)" : ""),
                                null,
                                Abilities.NoBattleEffect.Contains(a.Identifier) ? "#64748B" : "#22C55E"))
                            .ToList();

                        int abilityIndex = await Dialogs.ChooseAsync(
                            XamlRoot,
                            $"Habilidad de {pokemon.Name} ({number}/{selectedIds.Count})",
                            "Las habilidades en verde tienen efecto en combate.",
                            abilityCards);

                        pokemon.Ability = abilities[abilityIndex].Identifier;
                    }

                    // Elección de naturaleza: sube un 10% una estadística y baja otra un 10%.
                    var natureCards = Abilities.Natures
                        .Select(n => new ChoiceCard(n.Name, n.Describe(), null, n.IsNeutral ? null : "#3B82F6"))
                        .ToList();

                    int natureIndex = await Dialogs.ChooseAsync(
                        XamlRoot,
                        $"Naturaleza de {pokemon.Name} ({number}/{selectedIds.Count})",
                        "Sube un 10% una estadística y baja otra un 10%. Las neutras no cambian nada.",
                        natureCards);

                    pokemon.Nature = Abilities.Natures[natureIndex].Identifier;

                    team.Add(pokemon);
                }

                // Con el equipo ya configurado se inicia la partida de gimnasios.
                GymManager.StartRun(team);
            }
            finally
            {
                busy = false;
                RefreshUi();
            }
        }

        // Botón "Combatir": abre el combate contra el rival de la etapa (solo si queda algún Pokémon en pie).
        private void FightButton_Click(object sender, RoutedEventArgs e)
        {
            if (busy)
                return;

            if (!GameSession.Data.GymTeam.Any(p => !p.IsFainted))
                return;

            TrainerData trainer = GymManager.PrepareNextTrainer();

            Frame.Navigate(typeof(BattlePage), new BattleRequest(trainer, true));
        }

        // Botón "Usar objeto": usa objetos de la mochila del desafío sobre el equipo del desafío.
        private async void ItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (busy)
                return;

            busy = true;
            RefreshUi();

            try
            {
                string? message = await ItemUI.UseItemAsync(
                    XamlRoot,
                    false,
                    null,
                    null,
                    GameSession.Data.GymTeam,
                    GameSession.Data.GymInventory);

                if (message != null)
                    await Dialogs.MessageAsync(XamlRoot, "Objeto usado", message);
            }
            finally
            {
                busy = false;
                RefreshUi();
            }
        }

        // Botón "Primer Pokémon": elige quién combate primero.
        private async void LeadButton_Click(object sender, RoutedEventArgs e)
        {
            if (busy)
                return;

            busy = true;
            RefreshUi();

            try
            {
                await EquipUI.ChooseLeadAsync(XamlRoot, GameSession.Data.GymTeam);
            }
            finally
            {
                busy = false;
                RefreshUi();
            }
        }

        // Botón "Abandonar": tras confirmar, termina el desafío y limpia la selección.
        private async void AbandonButton_Click(object sender, RoutedEventArgs e)
        {
            if (busy)
                return;

            busy = true;
            RefreshUi();

            try
            {
                bool confirmed = await Dialogs.ConfirmAsync(
                    XamlRoot,
                    "Abandonar el desafío",
                    "Perderás tu equipo y tus objetos del desafío. ¿Seguro?",
                    "Abandonar",
                    "Cancelar");

                if (confirmed)
                {
                    GymManager.EndRun();
                    selectedIds.Clear();
                    PokemonGrid.Items.Clear();
                }
            }
            finally
            {
                busy = false;
                RefreshUi();
            }
        }

        // Botón "Menú": vuelve a la pantalla anterior o al menú principal.
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (busy)
                return;

            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(MainMenuPage));
        }
    }
}
