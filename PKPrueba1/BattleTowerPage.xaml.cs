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
    // Pantalla de la Torre Batalla: muestra el equipo, el próximo rival y los botones para combatir, usar objetos, equipar y gestionar la partida.
    public sealed partial class BattleTowerPage : Page
    {
        // true mientras hay un diálogo o proceso en curso; bloquea los botones para evitar acciones simultáneas.
        private bool busy;

        // Configura iconos y colores de los botones y espera a que la página cargue.
        public BattleTowerPage()
        {
            InitializeComponent();

            Background = UiTheme.Backdrop();

            UiTheme.SetIcon(StartButton, "poke-ball", "INICIAR TORRE");
            UiTheme.SetIcon(FightButton, "x-attack", "COMBATIR");
            UiTheme.SetIcon(ItemButton, "potion", "USAR OBJETO");
            UiTheme.SetIcon(EquipButton, "leftovers", "EQUIPAR");
            UiTheme.SetIcon(LeadButton, "exp-share", "PRIMER POKÉMON");
            UiTheme.SetIcon(InventoryButton, "ultra-ball", "INVENTARIO");
            UiTheme.SetIcon(TutorButton, "metronome", "ENSEÑAR MOVIMIENTO");
            UiTheme.SetIcon(AbandonButton, "escape-rope", "ABANDONAR");
            UiTheme.SetIcon(BackButton, "repel", "MENÚ");

            UiTheme.StyleButton(StartButton, "#16A34A");
            UiTheme.StyleButton(FightButton, "#DC2626");
            UiTheme.StyleButton(ItemButton, "#2563EB");
            UiTheme.StyleButton(EquipButton, "#0D9488");
            UiTheme.StyleButton(LeadButton, "#CA8A04");
            UiTheme.StyleButton(TutorButton, "#BE185D");
            UiTheme.StyleButton(InventoryButton, "#7C3AED");
            UiTheme.StyleButton(AbandonButton, "#475569");
            UiTheme.StyleButton(BackButton, "#334155");

            Loaded += OnLoaded;
        }

        // Al cargar la página: bloquea los botones, procesa recompensas y evoluciones pendientes y actualiza la pantalla.
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
                System.Diagnostics.Debug.WriteLine($"Error en la Torre Batalla: {ex}");
            }

            busy = false;
            RefreshUi();
        }

        // Muestra el mensaje de fin de partida si lo hay; si hay una torre activa, completa las recompensas pendientes y luego los movimientos/evoluciones.
        private async Task InitializeAsync()
        {
            GameData data = GameSession.Data;

            if (TowerManager.PendingMessage != null)
            {
                string message = TowerManager.PendingMessage;
                TowerManager.PendingMessage = null;

                RefreshUi();

                await Dialogs.MessageAsync(XamlRoot, "Fin de la partida", message);
                return;
            }

            // Sin torre activa: muestra las reglas del modo y oculta el equipo.
            if (!data.TowerActive)
                return;

            if (data.RewardStep != 0)
                await RunRewardAsync();

            await TowerGrowth.ProcessAsync(XamlRoot);
        }


        // Gestiona las recompensas tras una victoria en dos pasos: 1) elegir un Pokémon, 2) elegir un objeto. Es reanudable: el paso se guarda en la partida.
        private async Task RunRewardAsync()
        {
            GameData data = GameSession.Data;

            // Paso 1: elección del Pokémon de recompensa.
            while (data.TowerActive && data.RewardStep == 1 && data.RewardPokemon.Count > 0)
            {
                // Si el equipo está lleno, el jugador debe reemplazar a un Pokémon o renunciar a la recompensa.
                bool full = data.PokemonParty.Count >= PokemonPartyManager.MaxPartySize;

                List<ChoiceCard> cards = data.RewardPokemon
                    .Select(p => Dialogs.PokemonCard(p, false))
                    .ToList();

                string title = data.RewardIsBoss
                    ? "¡Minijefe derrotado! Elige un Pokémon"
                    : "¡Victoria! Elige un Pokémon";

                string message = full
                    ? "Tu equipo está lleno: tendrás que reemplazar a un Pokémon."
                    : "El Pokémon elegido se añadirá a tu equipo.";

                int index = await Dialogs.ChooseAsync(
                    XamlRoot,
                    title,
                    message,
                    cards,
                    full ? "No quedarme ninguno" : null);

                // El jugador renunció al Pokémon: se pasa directamente al paso de objetos.
                if (index < 0)
                {
                    data.RewardPokemon.Clear();
                    data.RewardStep = 2;
                    GameSession.Save();
                    continue;
                }

                PokemonData chosen = data.RewardPokemon[index];

                if (full)
                {
                    // Equipo lleno: pregunta a qué Pokémon reemplaza el nuevo.
                    int replace = await Dialogs.ChooseAsync(
                        XamlRoot,
                        $"¿A quién reemplaza {chosen.Name}?",
                        "El Pokémon reemplazado dejará tu equipo.",
                        Dialogs.PartyCards(data.PokemonParty),
                        "Volver");

                    if (replace < 0)
                        continue;

                    // El nuevo Pokémon ocupa el lugar del reemplazado.
                    data.PokemonParty[replace] = chosen;
                }
                else
                {
                    data.PokemonParty.Add(chosen);
                }

                data.RewardPokemon.Clear();
                data.RewardStep = 2;
                GameSession.Save();
            }

            if (data.RewardStep == 1)
            {
                data.RewardStep = 2;
                GameSession.Save();
            }

            // Paso 2: elección del objeto de recompensa.
            while (data.TowerActive && data.RewardStep == 2)
            {
                if (data.RewardItems.Count == 0)
                {
                    data.RewardStep = 0;
                    GameSession.Save();
                    break;
                }

                List<ChoiceCard> cards = data.RewardItems
                    .Select(name =>
                    {
                        ItemData? item = ItemCatalog.Get(name);

                        return item != null
                            ? Dialogs.ItemCard(item)
                            : new ChoiceCard(name, "", null);
                    })
                    .ToList();

                int index = await Dialogs.ChooseAsync(
                    XamlRoot,
                    data.RewardIsBoss ? "Elige un objeto del minijefe" : "Elige un objeto",
                    null,
                    cards);

                // El objeto elegido pasa a la mochila y se limpia el estado de recompensa.
                InventoryManager.AddItem(data.RewardItems[index]);

                data.RewardItems.Clear();
                data.RewardStep = 0;
                data.RewardIsBoss = false;
                GameSession.Save();
            }

            RefreshUi();
        }


        // Actualiza toda la pantalla: récord, visibilidad y estado de los botones, texto de estado, próximo rival y tarjetas del equipo.
        private void RefreshUi()
        {
            GameData data = GameSession.Data;

            RecordText.Text = $"Récord: {data.TowerBestWins} combates ganados";

            StartButton.Visibility = data.TowerActive ? Visibility.Collapsed : Visibility.Visible;
            FightButton.Visibility = data.TowerActive ? Visibility.Visible : Visibility.Collapsed;
            ItemButton.Visibility = data.TowerActive ? Visibility.Visible : Visibility.Collapsed;
            EquipButton.Visibility = data.TowerActive ? Visibility.Visible : Visibility.Collapsed;
            LeadButton.Visibility = data.TowerActive ? Visibility.Visible : Visibility.Collapsed;
            TutorButton.Visibility = data.TowerActive ? Visibility.Visible : Visibility.Collapsed;
            InventoryButton.Visibility = data.TowerActive ? Visibility.Visible : Visibility.Collapsed;
            AbandonButton.Visibility = data.TowerActive ? Visibility.Visible : Visibility.Collapsed;

            StartButton.IsEnabled = !busy;
            FightButton.IsEnabled = !busy;
            ItemButton.IsEnabled = !busy;
            EquipButton.IsEnabled = !busy;
            LeadButton.IsEnabled = !busy;
            TutorButton.IsEnabled = !busy;
            InventoryButton.IsEnabled = !busy;
            AbandonButton.IsEnabled = !busy;
            BackButton.IsEnabled = !busy;

            TeamPanel.Children.Clear();

            if (!data.TowerActive)
            {
                StatusText.Text = "Encadena combates Pokémon. Empiezas con un Pokémon al azar de nivel 5.";
                OpponentText.Text =
                    "• Tras cada victoria eliges 1 de 3 Pokémon y 1 de 3 objetos.\n" +
                    $"• Tras {TrainerGenerator.TrainersPerBoss} entrenadores, el combate {TrainerGenerator.TrainersPerBoss + 1} es contra un minijefe: al vencerlo eliges entre 6 Pokémon y 6 objetos, y se cura todo tu equipo.\n" +
                    "• Con el avance de la torre pueden aparecer Pokémon legendarios.\n" +
                    "• Los Pokémon debilitados no se recuperan hasta que uses un Revivir o venzas a un minijefe.\n" +
                    "• Si todo tu equipo cae, la partida termina.";
                TeamTitleText.Visibility = Visibility.Collapsed;
                return;
            }

            TeamTitleText.Visibility = Visibility.Visible;

            bool boss = TowerManager.IsBossBattle;
            int nextNumber = TowerManager.BattleNumber;

            StatusText.Text = boss
                ? $"Combate {nextNumber} — ¡MINIJEFE! · Combates ganados: {data.TowerWins}"
                : $"Combate {nextNumber} · Combates ganados: {data.TowerWins}";

            // Con torre activa: muestra el próximo rival y su equipo.
            TrainerData trainer = TowerManager.PrepareNextTrainer();

            string rivals = string.Join(
                ", ",
                trainer.Team.Select(p => $"{p.Name} Nv. {p.Level}"));

            OpponentText.Text =
                $"Próximo rival: {trainer.Name}\nEquipo ({trainer.Team.Count}): {rivals}";

            foreach (PokemonData pokemon in data.PokemonParty)
                TeamPanel.Children.Add(CreatePokemonCard(pokemon));
        }

        // Crea la tarjeta visual de un Pokémon del equipo.
        private Border CreatePokemonCard(PokemonData pokemon)
        {
            return PokemonCardFactory.Create(pokemon, true);
        }


        // Botón "Iniciar torre": empieza una partida nueva y muestra el Pokémon inicial.
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (busy)
                return;

            busy = true;

            try
            {
                TowerManager.StartRun();
                RefreshUi();

                PokemonData starter = GameSession.Data.PokemonParty[0];

                await Dialogs.MessageAsync(
                    XamlRoot,
                    "¡Comienza la Torre Batalla!",
                    $"Tu Pokémon inicial es {starter.Name} (Nv. {starter.Level}). ¡Suerte!");
            }
            finally
            {
                busy = false;
                RefreshUi();
            }
        }

        // Botón "Combatir": abre la pantalla de combate contra el siguiente rival (solo si queda algún Pokémon en pie).
        private void FightButton_Click(object sender, RoutedEventArgs e)
        {
            if (busy)
                return;

            if (!GameSession.Data.PokemonParty.Any(p => !p.IsFainted))
                return;

            TrainerData trainer = TowerManager.PrepareNextTrainer();

            Frame.Navigate(typeof(BattlePage), new BattleRequest(trainer, false));
        }

        // Botón "Usar objeto": abre el selector de objetos de curación fuera de combate.
        private async void ItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (busy)
                return;

            busy = true;
            RefreshUi();

            try
            {
                string? message = await ItemUI.UseItemAsync(XamlRoot, false, null, null);

                if (message != null)
                    await Dialogs.MessageAsync(XamlRoot, "Objeto usado", message);
            }
            finally
            {
                busy = false;
                RefreshUi();
            }
        }

        // Botón "Inventario": abre la página del inventario.
        private void InventoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (busy)
                return;

            Frame.Navigate(typeof(InventoryPage));
        }

        // Botón "Equipar": gestiona los objetos equipados del equipo.
        private async void EquipButton_Click(object sender, RoutedEventArgs e)
        {
            if (busy)
                return;

            busy = true;
            RefreshUi();

            try
            {
                await EquipUI.ManageAsync(XamlRoot, GameSession.Data.PokemonParty, GameSession.Data.Inventory);
            }
            finally
            {
                busy = false;
                RefreshUi();
            }
        }

        // Botón "Enseñar movimiento": enseña movimientos de MT / tutor.
        private async void TutorButton_Click(object sender, RoutedEventArgs e)
        {
            if (busy)
                return;

            busy = true;
            RefreshUi();

            try
            {
                await EquipUI.TeachAsync(XamlRoot, GameSession.Data.PokemonParty);
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
                await EquipUI.ChooseLeadAsync(XamlRoot, GameSession.Data.PokemonParty);
            }
            finally
            {
                busy = false;
                RefreshUi();
            }
        }

        // Botón "Abandonar": tras confirmar, termina la partida perdiendo equipo y objetos.
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
                    "Abandonar la torre",
                    "Perderás tu equipo y tus objetos. ¿Seguro que quieres abandonar?",
                    "Abandonar",
                    "Cancelar");

                if (confirmed)
                    TowerManager.EndRun();
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
