using System;
using System.Collections.Generic;
using System.Linq;

namespace PKPrueba1;

// Controla el progreso de la Torre Batalla: inicio de partida, victorias, derrotas y recompensas.
public static class TowerManager
{
    // Siguiente rival ya generado.
    public static TrainerData? NextTrainer { get; set; }

    // Mensaje de fin de partida pendiente de mostrar.
    public static string? PendingMessage { get; set; }

    // Número del combate actual (victorias + 1).
    public static int BattleNumber => GameSession.Data.TowerWins + 1;

    // true si el próximo combate es contra un minijefe.
    public static bool IsBossBattle => TrainerGenerator.IsBossBattle(GameSession.Data.TowerWins);

    // Devuelve el siguiente rival, generándolo si todavía no existe.
    public static TrainerData PrepareNextTrainer()
    {
        NextTrainer ??= TrainerGenerator.CreateTrainer(GameSession.Data.TowerWins);

        return NextTrainer;
    }

    // Empieza una partida nueva: reinicia todo y da un Pokémon inicial al jugador.
    public static void StartRun()
    {
        GameData data = GameSession.Data;

        data.PokemonParty.Clear();
        data.Inventory.Clear();
        data.RewardPokemon.Clear();
        data.RewardItems.Clear();

        data.RewardStep = 0;
        data.RewardIsBoss = false;
        data.TowerActive = true;
        data.TowerWins = 0;

        data.PokemonParty.Add(PokemonGenerator.CreateStarter());

        NextTrainer = null;
        PendingMessage = null;

        GameSession.Save();
    }

    // Termina la partida: anota el récord, borra equipo, mochila y recompensas, y guarda.
    public static void EndRun()
    {
        GameData data = GameSession.Data;

        data.TowerBestWins = Math.Max(data.TowerBestWins, data.TowerWins);
        data.TowerActive = false;
        data.TowerWins = 0;
        data.RewardStep = 0;
        data.RewardIsBoss = false;

        data.PokemonParty.Clear();
        data.Inventory.Clear();
        data.RewardPokemon.Clear();
        data.RewardItems.Clear();

        NextTrainer = null;

        GameSession.Save();
    }

    // Registra una victoria: reparte experiencia, cura al equipo si era un jefe y prepara las recompensas. Devuelve los mensajes a mostrar.
    public static List<string> RegisterVictory(TrainerData trainer)
    {
        GameData data = GameSession.Data;
        var messages = new List<string>();

        // La experiencia depende del nivel del combate, que sube con cada victoria.
        int battleLevel = 5 + data.TowerWins;
        int xp = battleLevel * 10 + 10;

        messages.Add($"Los Pokémon en pie ganaron {xp} puntos de experiencia.");

        foreach (PokemonData pokemon in data.PokemonParty)
        {
            // Los Pokémon debilitados no ganan experiencia.
            if (pokemon.IsFainted)
                continue;

            int before = pokemon.Level;

            pokemon.GainExperience(xp);

            if (pokemon.Level > before)
                messages.Add($"¡{pokemon.Name} subió a nivel {pokemon.Level}!");
        }

        data.TowerWins++;
        data.TowerBestWins = Math.Max(data.TowerBestWins, data.TowerWins);

        // Tras vencer a un minijefe todo el equipo se cura por completo.
        if (trainer.IsBoss)
        {
            foreach (PokemonData pokemon in data.PokemonParty)
                pokemon.Heal();

            messages.Add("¡Tu equipo se ha curado por completo!");
        }

        int rewardLevel = 5 + data.TowerWins;
        // Número de opciones de recompensa: 6 si era jefe, 3 si no.
        int amount = trainer.IsBoss ? 6 : 3;

        data.RewardIsBoss = trainer.IsBoss;
        // Genera los Pokémon y objetos entre los que el jugador elegirá. RewardStep = 1 indica que falta elegir el Pokémon.
        data.RewardPokemon = PokemonGenerator.CreateRewardChoices(amount, rewardLevel, data.TowerWins);
        data.RewardItems = ItemGenerator.GetRandomItems(amount, data.PokemonParty).Select(i => i.Name).ToList();
        data.RewardStep = 1;

        NextTrainer = null;

        GameSession.Save();

        return messages;
    }

    // Registra una derrota: prepara el mensaje con el resultado y termina la partida.
    public static void RegisterDefeat(TrainerData? trainer)
    {
        int wins = GameSession.Data.TowerWins;
        int best = Math.Max(GameSession.Data.TowerBestWins, wins);

        PendingMessage =
            (trainer != null ? $"Has caído ante {trainer.Name}.\n" : "")
            + $"Combates ganados: {wins}.\nRécord: {best}.";

        EndRun();
    }
}
