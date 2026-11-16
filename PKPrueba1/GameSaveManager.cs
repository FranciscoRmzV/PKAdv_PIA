
using System;
using System.IO;
using System.Text.Json;

namespace PKPrueba1;

// Guarda y carga las partidas en archivos JSON, uno por usuario.
public static class GameSaveManager
{
    // Carpeta donde se guardan las partidas: %LocalAppData%\PokemonAdventure\Saves.
    private static readonly string SaveFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PokemonAdventure",
        "Saves"
    );

    // Devuelve la ruta del archivo de guardado del usuario, sustituyendo los caracteres no válidos del nombre por "_".
    private static string GetSavePath(string username)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            username = username.Replace(c, '_');

        return Path.Combine(SaveFolder, username + ".json");
    }

    // Serializa los datos de la partida a JSON y los escribe en disco. Si falla, solo lo registra en la depuración.
    public static void Save(string username, GameData data)
    {
        try
        {
            // Crea la carpeta de guardados si todavía no existe.
            Directory.CreateDirectory(SaveFolder);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            // Convierte la partida a texto JSON y lo guarda en el archivo.
            string json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(GetSavePath(username), json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Error al guardar la partida: {ex.Message}"
            );
        }
    }

    // Carga la partida del usuario. Si no existe el archivo o está dañado, devuelve una partida nueva.
    public static GameData Load(string username)
    {
        try
        {
            string path = GetSavePath(username);

            if (!File.Exists(path))
                return new GameData();

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GameData>(json)
                   ?? new GameData();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Error al cargar la partida: {ex.Message}"
            );

            return new GameData();
        }
    }

    // Indica si el usuario tiene un archivo de guardado.
    public static bool HasSave(string username)
    {
        return File.Exists(GetSavePath(username));
    }

    // Borra el archivo de guardado del usuario si existe.
    public static void DeleteSave(string username)
    {
        string path = GetSavePath(username);

        if (File.Exists(path))
            File.Delete(path);
    }
}
