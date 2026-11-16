using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PKPrueba1
{
    // Pantalla de inicio de sesión y registro de cuentas.
    public sealed partial class LoginPage : Page
    {
        // Intentos fallidos permitidos antes de bloquear al usuario temporalmente.
        private const int MaxFailedAttempts = 5;
        // Duración del bloqueo tras demasiados intentos fallidos.
        private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(30);

        // Intentos fallidos por usuario y hora hasta la que está bloqueado (solo en memoria).
        private static readonly Dictionary<string, (int Fails, DateTime LockedUntil)> Attempts = new();

        // Carpeta de datos de la app (%LocalAppData%\PokemonAdventure).
        private readonly string UsersFolder =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "PokemonAdventure");

        // Archivo users.json con las cuentas registradas.
        private readonly string UsersFile;

        // Prepara la pantalla: fondo, iconos y colores de los botones, requisitos de contraseña y foco inicial en el campo de usuario.
        public LoginPage()
        {
            InitializeComponent();

            Background = UiTheme.Backdrop();

            UiTheme.SetIcon(LoginButton, "poke-ball", "INICIAR SESIÓN");
            UiTheme.SetIcon(RegisterButton, "great-ball", "CREAR CUENTA");

            UiTheme.StyleButton(LoginButton, "#2563EB");
            UiTheme.StyleButton(RegisterButton, "#16A34A");

            UsersFile =
                Path.Combine(
                    UsersFolder,
                    "users.json");

            UpdateRequirements();

            Loaded += (_, _) => UsernameBox.Focus(FocusState.Programmatic);
        }


        // true si la tecla pulsada es Enter.
        private static bool IsEnter(KeyRoutedEventArgs e)
        {
            return e.Key == Windows.System.VirtualKey.Enter;
        }

        // Enter en el usuario: pasa al campo de contraseña.
        private void UsernameBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!IsEnter(e))
                return;

            e.Handled = true;
            PasswordBox.Focus(FocusState.Keyboard);
        }

        // Enter en la contraseña: si hay confirmación escrita registra la cuenta; si no, inicia sesión.
        private void PasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!IsEnter(e))
                return;

            e.Handled = true;

            if (ConfirmBox.Password.Length > 0)
                DoRegister();
            else
                DoLogin();
        }

        // Enter en la confirmación: registra la cuenta.
        private void ConfirmBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!IsEnter(e))
                return;

            e.Handled = true;
            DoRegister();
        }


        // Al cambiar el usuario se actualiza la lista de requisitos (la contraseña no puede contener el usuario).
        private void UsernameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateRequirements();
        }

        // Al cambiar la contraseña se actualiza la lista de requisitos.
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdateRequirements();
        }

        // Redibuja la lista de requisitos de contraseña marcando con ✔ (verde) los cumplidos y con ✘ (gris) los pendientes.
        private void UpdateRequirements()
        {
            if (RequirementsPanel == null)
                return;

            RequirementsPanel.Children.Clear();

            string username = UsernameBox?.Text.Trim() ?? "";
            string password = PasswordBox?.Password ?? "";

            foreach ((string text, bool ok) in PasswordPolicy.Check(username, password))
            {
                RequirementsPanel.Children.Add(new TextBlock
                {
                    Text = (ok ? "✔ " : "✘ ") + text,
                    FontSize = 12,
                    Foreground = UiTheme.Brush(ok ? "#4ADE80" : "#94A3B8")
                });
            }
        }


        // Botón "Iniciar sesión".
        private void Login_Click(object sender, RoutedEventArgs e)
        {
            DoLogin();
        }

        // Botón "Crear cuenta".
        private void Register_Click(object sender, RoutedEventArgs e)
        {
            DoRegister();
        }

        // Valida los datos, comprueba el bloqueo por intentos, verifica la contraseña, actualiza el hash si es antiguo y abre el menú principal.
        private void DoLogin()
        {
            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                StatusText.Text = "Introduce usuario y contraseña.";
                return;
            }

            // Clave para contar intentos (sin distinguir mayúsculas).
            string key = username.ToLowerInvariant();

            // Si el usuario está bloqueado, se muestra cuántos segundos faltan.
            if (Attempts.TryGetValue(key, out var state) && state.LockedUntil > DateTime.Now)
            {
                int seconds = (int)Math.Ceiling((state.LockedUntil - DateTime.Now).TotalSeconds);
                StatusText.Text = $"Demasiados intentos. Espera {seconds} s e inténtalo de nuevo.";
                return;
            }

            List<UserAccount> users = LoadUsers();

            UserAccount? user = users.FirstOrDefault(
                x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            bool ok = false;
            bool upgrade = false;

            if (user != null)
                ok = PasswordPolicy.Verify(password, user.PasswordHash, out upgrade);

            // Usuario inexistente o contraseña incorrecta: se cuenta el fallo con el mismo mensaje en ambos casos para no revelar qué usuarios existen.
            if (user == null || !ok)
            {
                RegisterFailure(key);
                return;
            }

            // Si el hash usa un formato antiguo, se vuelve a guardar con el actual.
            if (upgrade)
            {
                user.PasswordHash = PasswordPolicy.Hash(password);
                SaveUsers(users);
            }

            Attempts.Remove(key);

            // Inicia la sesión de juego (carga la partida) y abre el menú principal.
            GameSession.Login(user.Username);

            Frame.Navigate(typeof(MainMenuPage));
        }

        // Anota un intento fallido; al llegar al máximo bloquea al usuario durante 30 segundos.
        private void RegisterFailure(string key)
        {
            int fails = Attempts.TryGetValue(key, out var state) ? state.Fails + 1 : 1;

            if (fails >= MaxFailedAttempts)
            {
                Attempts[key] = (0, DateTime.Now + LockDuration);
                StatusText.Text = $"Demasiados intentos fallidos. Espera {(int)LockDuration.TotalSeconds} s.";
            }
            else
            {
                Attempts[key] = (fails, DateTime.MinValue);
                StatusText.Text = "Usuario o contraseña incorrectos.";
            }
        }

        // Valida el usuario y la contraseña (requisitos, confirmación y que no exista ya), crea la cuenta y entra al juego.
        private void DoRegister()
        {
            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password;
            string confirm = ConfirmBox.Password;

            if (!PasswordPolicy.IsValidUsername(username))
            {
                StatusText.Text = PasswordPolicy.UsernameRule;
                return;
            }

            List<(string Text, bool Ok)> unmet = PasswordPolicy
                .Check(username, password)
                .Where(r => !r.Ok)
                .ToList();

            if (unmet.Count > 0)
            {
                StatusText.Text = "La contraseña no es segura: " + unmet[0].Text.ToLowerInvariant() + ".";
                return;
            }

            if (password != confirm)
            {
                StatusText.Text = "Las contraseñas no coinciden.";
                ConfirmBox.Focus(FocusState.Programmatic);
                return;
            }

            List<UserAccount> users = LoadUsers();

            if (users.Any(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                StatusText.Text = "Ese usuario ya existe.";
                return;
            }

            users.Add(new UserAccount
            {
                Username = username,
                PasswordHash = PasswordPolicy.Hash(password),
                Game = new GameData()
            });

            SaveUsers(users);

            GameSession.Login(username);

            Frame.Navigate(typeof(MainMenuPage));
        }


        // Lee la lista de cuentas de users.json (devuelve lista vacía si no existe o está dañado).
        private List<UserAccount> LoadUsers()
        {
            Directory.CreateDirectory(UsersFolder);

            if (!File.Exists(UsersFile))
                return new List<UserAccount>();

            try
            {
                string json = File.ReadAllText(UsersFile);

                return JsonSerializer.Deserialize<List<UserAccount>>(json)
                    ?? new List<UserAccount>();
            }
            catch (JsonException)
            {
                return new List<UserAccount>();
            }
        }

        // Guarda la lista de cuentas en users.json.
        private void SaveUsers(List<UserAccount> users)
        {
            Directory.CreateDirectory(UsersFolder);

            string json = JsonSerializer.Serialize(
                users,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(UsersFile, json);
        }
    }

    // Cuenta de usuario guardada en users.json.
    public class UserAccount
    {
        public string Username { get; set; } = "";

        // Hash de la contraseña (nunca se guarda en texto plano).
        public string PasswordHash { get; set; } = "";

        // Datos de partida asociados a la cuenta.
        public GameData Game { get; set; } = new GameData();
    }
}
