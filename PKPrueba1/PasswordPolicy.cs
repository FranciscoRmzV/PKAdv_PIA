using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PKPrueba1;

// Reglas de usuario y contraseña, además del cifrado (hash) seguro de las contraseñas.
public static class PasswordPolicy
{
    // Longitud mínima de la contraseña.
    public const int MinLength = 8;
    // Longitud máxima de la contraseña.
    public const int MaxLength = 64;

    // Iteraciones de PBKDF2: cuantas más, más difícil es adivinar contraseñas por fuerza bruta.
    private const int Iterations = 120_000;
    // Prefijo que identifica el formato actual del hash guardado.
    private const string Prefix = "v2";

    // Usuario válido: 3 a 20 letras, números o guion bajo.
    private static readonly Regex UsernameRegex = new("^[A-Za-z0-9_]{3,20}$");

    // Lista de contraseñas demasiado comunes que no se permiten.
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password1", "password123", "12345678", "123456789", "1234567890",
        "qwerty123", "qwertyuiop", "contraseña", "contrasena", "abc12345", "iloveyou",
        "admin123", "letmein123", "pokemon123", "pikachu123", "welcome123"
    };

    // Comprueba que el nombre de usuario cumple el formato permitido.
    public static bool IsValidUsername(string username) => UsernameRegex.IsMatch(username);

    // Texto que explica la regla del nombre de usuario.
    public static string UsernameRule =>
        "El usuario debe tener entre 3 y 20 caracteres (letras, números o guion bajo).";

    // Evalúa cada requisito de la contraseña y devuelve la lista con su descripción y si se cumple (para mostrarla en pantalla).
    public static List<(string Text, bool Ok)> Check(string username, string password)
    {
        bool hasSymbol = password.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));

        bool containsUser =
            username.Length >= 3 &&
            password.Contains(username, StringComparison.OrdinalIgnoreCase);

        return new List<(string, bool)>
        {
            ($"Entre {MinLength} y {MaxLength} caracteres", password.Length >= MinLength && password.Length <= MaxLength),
            ("Una letra mayúscula", password.Any(char.IsUpper)),
            ("Una letra minúscula", password.Any(char.IsLower)),
            ("Un número", password.Any(char.IsDigit)),
            ("Un símbolo (por ejemplo ! ? # $ %)", hasSymbol),
            ("Sin espacios", !password.Any(char.IsWhiteSpace)),
            ("No contiene tu usuario", !containsUser),
            ("No es una contraseña demasiado común", !CommonPasswords.Contains(password))
        };
    }

    // true si la contraseña cumple todos los requisitos.
    public static bool IsValid(string username, string password)
    {
        return Check(username, password).All(r => r.Ok);
    }


    // Genera el hash de una contraseña con PBKDF2-SHA256 y una sal aleatoria. Formato: v2$iteraciones$sal$hash.
    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);

        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            32);

        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    // Comprueba una contraseña contra el hash guardado. "needsUpgrade" indica si conviene volver a guardarla con el formato actual.
    public static bool Verify(string password, string stored, out bool needsUpgrade)
    {
        needsUpgrade = false;

        if (string.IsNullOrEmpty(stored))
            return false;

        // Formato nuevo (PBKDF2): se recalcula el hash con la misma sal y se compara en tiempo constante.
        if (stored.StartsWith(Prefix + "$", StringComparison.Ordinal))
        {
            string[] parts = stored.Split('$');

            if (parts.Length != 4 || !int.TryParse(parts[1], out int iterations))
                return false;

            try
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expected = Convert.FromBase64String(parts[3]);

                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expected.Length);

                bool ok = CryptographicOperations.FixedTimeEquals(actual, expected);

                needsUpgrade = ok && iterations < Iterations;

                return ok;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // Formato antiguo (SHA-256 sin sal): se acepta para no bloquear cuentas antiguas y se marca para actualizarlo.
        byte[] legacy = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password));
        string legacyHex = Convert.ToHexString(legacy);

        bool legacyOk = CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(legacyHex),
            System.Text.Encoding.ASCII.GetBytes(stored.ToUpperInvariant()));

        needsUpgrade = legacyOk;

        return legacyOk;
    }
}
