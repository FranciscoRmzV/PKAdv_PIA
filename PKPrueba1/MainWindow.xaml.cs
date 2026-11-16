using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using System.IO;

namespace PKPrueba1
{
    // Ventana principal: contiene el Frame donde se muestran todas las páginas y el reproductor de música.
    public sealed partial class MainWindow : Window
    {
        // Configura la ventana: título, icono, pantalla completa, atajo F11, página de inicio (login) y música.
        public MainWindow()
        {
            InitializeComponent();

            Title = "Pokémon Adventure";

            try
            {
                // Pone el icono de la Poké Ball en la ventana si el archivo existe.
                string icon = Path.Combine(AppContext.BaseDirectory, "Assets", "pokeball.ico");

                if (File.Exists(icon))
                    AppWindow.SetIcon(icon);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"No se pudo poner el icono: {ex.Message}");
            }

            // La app arranca en pantalla completa.
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);

            // Atajo de teclado F11 para alternar la pantalla completa.
            var f11 = new KeyboardAccelerator { Key = Windows.System.VirtualKey.F11 };

            f11.Invoked += (_, args) =>
            {
                args.Handled = true;
                ToggleFullScreen();
            };

            MainFrame.KeyboardAccelerators.Add(f11);

            // Se muestra primero la pantalla de inicio de sesión.
            MainFrame.Navigate(typeof(LoginPage));

            // Inicia el reproductor de música de fondo (sin esperar a que termine).
            _ = MusicPlayer.InitializeAsync(MusicView);
        }

        // Alterna entre pantalla completa y ventana maximizada.
        private void ToggleFullScreen()
        {
            if (AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
            {
                AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

                if (AppWindow.Presenter is OverlappedPresenter presenter)
                    presenter.Maximize();
            }
            else
            {
                AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            }
        }
    }
}
