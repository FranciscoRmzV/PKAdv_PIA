using System;
using System.Collections.Generic;
using System.Linq;

namespace PKPrueba1;

// Genera Pokémon aleatorios (para rivales, iniciales y recompensas).
public static class PokemonGenerator
{
    // Generador de números aleatorios compartido.
    private static readonly Random Random = new();

    // Límite de estadísticas base totales permitido según las victorias: empieza en 335 y sube 10 por victoria, para que la dificultad crezca poco a poco.
    public static int BaseTotalCap(int wins)
    {
        return 335 + wins * 10;
    }

    // Crea un Pokémon aleatorio del nivel indicado. Permite fijar un tipo temático, un tope de estadísticas, mejores movimientos y probabilidad de legendario.
    public static PokemonData CreateForLevel(
        int level,
        string? themeType = null,
        bool playerOwned = false,
        int maxBaseTotal = int.MaxValue,
        bool bestMoves = false,
        double legendaryChance = 0)
    {
        // Con cierta probabilidad se genera un legendario (respetando el tope de estadísticas).
        if (legendaryChance > 0 && Random.NextDouble() < legendaryChance)
        {
            List<PokemonSpecies> legends = PokemonDatabase.Legendaries
                .Where(s => s.BaseTotal <= maxBaseTotal)
                .ToList();

            // Si hay tipo temático, el 60% de las veces se prefieren legendarios de ese tipo.
            if (themeType != null)
            {
                List<PokemonSpecies> themed = legends.Where(s => s.Types.Contains(themeType)).ToList();

                if (themed.Count > 0 && Random.NextDouble() < 0.6)
                    legends = themed;
            }

            if (legends.Count > 0)
                return new PokemonData(legends[Random.Next(legends.Count)].Id, level, bestMoves);
        }

        // Si no sale legendario, se elige una especie básica al azar y se evoluciona según el nivel.
        IReadOnlyList<PokemonSpecies> pool = PokemonDatabase.BasicSpecies;
        PokemonData? fallback = null;

        // Hasta 80 intentos para encontrar un Pokémon que cumpla el tope de estadísticas y, si se pide, el tipo temático.
        for (int attempt = 0; attempt < 80; attempt++)
        {
            PokemonSpecies basic = pool[Random.Next(pool.Count)];
            PokemonData candidate = CreateEvolvedForLevel(basic.Id, level, playerOwned, bestMoves);

            if (candidate.Species.BaseTotal > maxBaseTotal)
                continue;

            // Guarda el primer candidato válido como respaldo por si no aparece uno del tipo pedido.
            fallback ??= candidate;

            if (themeType != null && attempt < 60 && !candidate.Species.Types.Contains(themeType))
                continue;

            return candidate;
        }

        if (fallback != null)
            return fallback;

        // Último recurso: si ninguno cumple el tope, se usa la especie más débil.
        PokemonSpecies weakest = pool.OrderBy(s => s.BaseTotal).First();
        return CreateEvolvedForLevel(weakest.Id, level, playerOwned, bestMoves);
    }

    // Crea un Pokémon evolucionándolo tanto como permita su nivel. Si es del jugador y tiene varias evoluciones posibles, se detiene para que el jugador elija.
    public static PokemonData CreateEvolvedForLevel(int basicId, int level, bool playerOwned, bool bestMoves = false)
    {
        int id = basicId;

        while (true)
        {
            List<EvolutionInfo> options = PokemonDatabase.Get(id).Evolutions
                .Where(e => level >= e.Level)
                .ToList();

            if (options.Count == 0)
                break;

            if (options.Count > 1 && playerOwned)
                break;

            id = options[Random.Next(options.Count)].ToId;
        }

        return new PokemonData(id, level, bestMoves);
    }

    // Crea el Pokémon inicial de nivel 5, evitando especies que serían demasiado débiles (Magikarp, Feebas, etc.).
    public static PokemonData CreateStarter()
    {
        PokemonData starter;

        do
        {
            starter = CreateForLevel(5, null, true, BaseTotalCap(0));
        }
        while (starter.Id == 129 || starter.Id == 132 || starter.Id == 191 || starter.Id == 349);

        return starter;
    }

    // Probabilidad de legendario: 0 hasta la victoria 10; después sube un 0.4% por victoria (máximo 50%).
    public static double LegendaryChance(int wins, double extra = 0)
    {
        if (wins < 10)
            return 0;

        return Math.Min(0.04 + (wins - 10) * 0.004 + extra, 0.5);
    }

    // Genera los Pokémon entre los que el jugador elige como recompensa, sin repetir especies.
    public static List<PokemonData> CreateRewardChoices(int count, int level, int wins)
    {
        List<PokemonData> result = new();
        int cap = BaseTotalCap(wins) + 20;

        int guard = 0;

        while (result.Count < count && guard++ < 200)
        {
            PokemonData candidate = CreateForLevel(level, null, true, cap, false, LegendaryChance(wins, count > 3 ? 0.1 : 0));

            if (result.Any(r => r.Id == candidate.Id))
                continue;

            result.Add(candidate);
        }

        return result;
    }
}
