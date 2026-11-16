using System;
using System.Collections.Generic;
using System.Linq;

namespace PKPrueba1;

// Genera los entrenadores rivales de la torre y de los gimnasios.
public static class TrainerGenerator
{
    // Entrenadores normales entre un minijefe y el siguiente.
    public const int TrainersPerBoss = 9;

    // Nivel de todos los Pokémon del modo Gimnasio.
    public const int GymLevel = 100;

    private static readonly Random Random = new();

    // Clases de entrenador para componer nombres aleatorios.
    private static readonly string[] Classes =
    {
        "Joven", "Cazabichos", "Campista", "Excursionista", "Pescador",
        "Karateka", "Aprendiz", "Ranger", "Montañero", "Nadador", "Chico", "Chica"
    };

    // Nombres de pila para componer nombres aleatorios.
    private static readonly string[] Names =
    {
        "Marcos", "Lucía", "Pablo", "Sara", "Diego", "Elena", "Iván", "Marta",
        "Hugo", "Nuria", "Álex", "Julia", "Rubén", "Carla", "Óscar", "Irene"
    };

    // true si el próximo combate es contra un minijefe (uno de cada 10 combates).
    public static bool IsBossBattle(int wins)
    {
        return (wins + 1) % (TrainersPerBoss + 1) == 0;
    }

    // Crea el rival de una etapa de gimnasio: equipo de nivel 100 del tipo del líder, con objetos y mejores movimientos.
    public static TrainerData CreateGymTrainer(int stage)
    {
        GymInfo info = GymCatalog.Stages[Math.Clamp(stage, 0, GymCatalog.Stages.Count - 1)];

        // Tamaño del equipo según la etapa (4, 5 o 6) y multiplicador de daño que crece un 2% por etapa.
        int teamSize = stage < 3 ? 4 : stage < 6 ? 5 : 6;
        double multiplier = 1.0 + stage * 0.02;

        var team = new List<PokemonData>();
        int guard = 0;

        while (team.Count < teamSize)
        {
            PokemonData pokemon = PokemonGenerator.CreateForLevel(
                GymLevel,
                info.Type,
                false,
                int.MaxValue,
                true);

            HeldItems.GiveRandomToEnemy(pokemon, Random);

            // Evita repetir especies y excluye a Ditto; tras 60 reintentos acepta lo que salga para no quedarse en bucle.
            bool bad = pokemon.Id == 132 || team.Any(p => p.Id == pokemon.Id);

            if (bad && guard++ < 60)
                continue;

            team.Add(pokemon);
        }

        return new TrainerData
        {
            Name = info.Title,
            IsBoss = true,
            DamageMultiplier = multiplier,
            Gym = info,
            Team = team
        };
    }

    // Crea el rival de la torre; su nivel, equipo y dificultad dependen de las victorias acumuladas.
    public static TrainerData CreateTrainer(int wins)
    {
        bool isBoss = IsBossBattle(wins);

        // Nivel base y tamaño del equipo (sube uno cada 3 victorias, máximo 6; los jefes llevan uno más).
        int baseLevel = 5 + wins;
        int teamSize = Math.Clamp(1 + wins / 3, 1, 6);

        if (isBoss)
            teamSize = Math.Clamp(teamSize + 1, 2, 6);

        // Multiplicador de daño: +1% por victoria hasta +80%, y +10% extra para los jefes.
        double multiplier = 1.0 + Math.Min(wins * 0.01, 0.8);

        if (isBoss)
            multiplier *= 1.1;

        string name;
        string? theme = null;

        if (isBoss)
        {
            // Los minijefes tienen un tipo temático aleatorio para su equipo.
            name = $"Minijefe {Names[Random.Next(Names.Length)]}";

            theme = TypeChart.AllTypes[Random.Next(TypeChart.AllTypes.Length)];
        }
        else
        {
            name = $"{Classes[Random.Next(Classes.Length)]} {Names[Random.Next(Names.Length)]}";
        }

        var team = new List<PokemonData>();

        // Genera cada Pokémon del equipo: ajusta su nivel, probabilidad de legendario (mayor en el último de un jefe) y le da más PS según las victorias.
        for (int i = 0; i < teamSize; i++)
        {
            int level = baseLevel + (isBoss ? 2 : 0);

            if (!isBoss && wins > 0 && Random.Next(3) == 0)
                level--;

            if (isBoss && i == teamSize - 1)
                level++;

            bool lastOfBoss = isBoss && i == teamSize - 1;

            double legendary = PokemonGenerator.LegendaryChance(
                wins,
                lastOfBoss ? 0.3 : isBoss ? 0.05 : 0);

            PokemonData pokemon = PokemonGenerator.CreateForLevel(
                Math.Max(2, level),
                theme,
                false,
                PokemonGenerator.BaseTotalCap(wins) + (isBoss ? 40 : 0),
                false,
                legendary);

            double hpBoost = 1.0 + Math.Min(wins * 0.01, 0.5);
            pokemon.MaxHP = (int)(pokemon.MaxHP * hpBoost);
            pokemon.CurrentHP = pokemon.MaxHP;

            team.Add(pokemon);
        }

        return new TrainerData
        {
            Name = name,
            IsBoss = isBoss,
            DamageMultiplier = multiplier,
            Team = team
        };
    }
}
