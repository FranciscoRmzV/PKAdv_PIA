using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;


namespace PKPrueba1
{
    // Clase principal de la aplicación WinUI 3; controla el ciclo de vida de la app.
    public partial class App : Application
    {
        // Ventana principal de la aplicación.
        private Window? _window;

        // Constructor: inicializa los recursos definidos en App.xaml.
        public App()
        {
            InitializeComponent();
        }

        // Se ejecuta al iniciar la app: crea la ventana principal y la muestra.
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
