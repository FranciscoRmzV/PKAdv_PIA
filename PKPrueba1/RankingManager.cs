using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PKPrueba1;

// Una fila del ranking: usuario, mejor racha en la torre y mejor etapa en gimnasios.
public record RankingEntry(string Username, int TowerBest, int GymBest);

// Construye el ranking leyendo los guardados de todos los usuarios.
public static class RankingManager
{
    // Misma carpeta de guardados que usa GameSaveManager.
    private static readonly string SaveFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PokemonAdventure",
        "Saves");

    // Lee todos los archivos de guardado y devuelve el récord de cada usuario.
    public static List<RankingEntry> Load()
    {
        // Diccionario por nombre de usuario (sin distinguir mayúsculas).
        var entries = new Dictionary<string, RankingEntry>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (Directory.Exists(SaveFolder))
            {
                foreach (string file in Directory.GetFiles(SaveFolder, "*.json"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);

                    try
                    {
                        // Se lee solo lo necesario del JSON sin deserializar toda la partida.
                        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));

                        entries[name] = new RankingEntry(
                            name,
                            ReadInt(doc.RootElement, "TowerBestWins"),
                            ReadInt(doc.RootElement, "GymBestStage"));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"No se pudo leer {file}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"No se pudo leer el ranking: {ex.Message}");
        }

        // Para el usuario conectado se usan los datos en memoria, que pueden ser más recientes que el archivo.
        if (!string.IsNullOrWhiteSpace(GameSession.Username))
        {
            entries[GameSession.Username] = new RankingEntry(
                GameSession.Username,
                GameSession.Data.TowerBestWins,
                GameSession.Data.GymBestStage);
        }

        return entries.Values.ToList();
    }

    // Lee una propiedad numérica del JSON; devuelve 0 si no existe o no es un número.
    private static int ReadInt(JsonElement root, string property)
    {
        return root.TryGetProperty(property, out JsonElement value) && value.TryGetInt32(out int number)
            ? number
            : 0;
    }

    // Ordena a los usuarios de mayor a menor según el valor elegido y asigna posiciones (los empates comparten puesto).
    public static List<(int Position, RankingEntry Entry, int Value)> Rank(
        IEnumerable<RankingEntry> entries,
        Func<RankingEntry, int> selector)
    {
        var sorted = entries
            .Select(e => (Entry: e, Value: selector(e)))
            // Se excluye a quien no tiene puntuación.
            .Where(x => x.Value > 0)
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Entry.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<(int, RankingEntry, int)>();

        for (int i = 0; i < sorted.Count; i++)
        {
            // Si empata con el anterior mantiene su posición; si no, la posición es el índice + 1.
            int position = i > 0 && sorted[i].Value == sorted[i - 1].Value
                ? result[i - 1].Item1
                : i + 1;

            result.Add((position, sorted[i].Entry, sorted[i].Value));
        }

        return result;
    }
}
