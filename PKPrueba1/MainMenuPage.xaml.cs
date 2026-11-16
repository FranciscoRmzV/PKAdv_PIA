using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PKPrueba1
{
    // Página del menú principal: acceso a Torre Batalla, Gimnasios, Ranking, música y cierre de sesión.
    public sealed partial class MainMenuPage : Page
    {
        // Construye el menú: fondo, contenido e iconos de los botones, colores y texto de bienvenida.
        public MainMenuPage()
        {
            InitializeComponent();

            Background = UiTheme.Backdrop();

            // Crea el contenido (icono, título y descripción) de los tres botones principales.
            TowerButton.Content = MenuContent(
                "master-ball",
                "TORRE BATALLA",
                "Combates seguidos con un equipo que crece. ¿Hasta dónde llegarás?");

            GymsButton.Content = MenuContent(
                "rare-candy",
                "GIMNASIOS",
                "Líderes de Kanto, Alto Mando y Campeón, todo a nivel 100.");

            RankingButton.Content = MenuContent(
                "nugget",
                "RANKING GLOBAL",
                "Los mejores récords de cada jugador en la Torre Batalla y en Gimnasios.");

            // Iconos y textos de los botones secundarios.
            UiTheme.SetIcon(ExitButton, "escape-rope", "CERRAR SESIÓN");
            UiTheme.SetIcon(QuitButton, "poke-ball", "CERRAR JUEGO");

            // Aplica el estilo (color de fondo) a cada botón.
            UiTheme.StyleButton(TowerButton, "#B91C1C");
            UiTheme.StyleButton(GymsButton, "#1D4ED8");
            UiTheme.StyleButton(ExitButton, "#334155");
            UiTheme.StyleButton(RankingButton, "#A16207");
            UiTheme.StyleButton(QuitButton, "#7F1D1D");
            UiTheme.StyleButton(MusicButton, "#0F766E");

            // Muestra si la música está activada o no.
            UpdateMusicButton();

            // Saluda al jugador con su nombre de usuario.
            WelcomeText.Text =
                $"Bienvenido, {GameSession.Username}";
        }

        // Crea el contenido de un botón del menú: icono a la izquierda y, a la derecha, título y descripción.
        private static Microsoft.UI.Xaml.FrameworkElement MenuContent(string icon, string title, string description)
        {
            var text = new StackPanel
            {
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center
            };

            text.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 24,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            });

            text.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 13,
                Opacity = 0.85,
                TextWrapping = TextWrapping.Wrap
            });

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16
            };

            panel.Children.Add(UiTheme.ItemIcon(icon, 56));
            panel.Children.Add(text);

            return panel;
        }

        // Abre la Torre Batalla.
        private void BattleTower_Click(
            object sender,
            RoutedEventArgs e)
        {
            Frame.Navigate(
                typeof(BattleTowerPage));
        }

        // Abre el modo Gimnasios.
        private void Gyms_Click(
            object sender,
            RoutedEventArgs e)
        {
            Frame.Navigate(
                typeof(GymPage));
        }

        // Actualiza el texto del botón de música según su estado.
        private void UpdateMusicButton()
        {
            UiTheme.SetIcon(
                MusicButton,
                "poke-flute",
                MusicPlayer.Enabled ? "MÚSICA: ACTIVADA" : "MÚSICA: DESACTIVADA");
        }

        // Activa o desactiva la música y actualiza el botón.
        private async void Music_Click(
            object sender,
            RoutedEventArgs e)
        {
            await MusicPlayer.SetEnabledAsync(!MusicPlayer.Enabled);

            UpdateMusicButton();
        }

        // Abre el ranking global.
        private void Ranking_Click(
            object sender,
            RoutedEventArgs e)
        {
            Frame.Navigate(typeof(RankingPage));
        }

        // Cierra la sesión (guardando) y sale del juego.
        private void Quit_Click(
            object sender,
            RoutedEventArgs e)
        {
            GameSession.Logout();

            Application.Current.Exit();
        }

        // Cierra la sesión (guardando) y vuelve a la pantalla de inicio de sesión.
        private void Exit_Click(
            object sender,
            RoutedEventArgs e)
        {
            GameSession.Logout();

            Frame.Navigate(
                typeof(LoginPage));
        }
    }
}
