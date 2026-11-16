using System;
using System.Collections.Generic;
using System.Linq;

namespace PKPrueba1;

// Controla el progreso del modo Gimnasio: inicio, victorias, derrotas y recompensas.
public static class GymManager
{
    // Objetos entre los que se puede elegir tras cada victoria.
    public const int RewardChoices = 9;
    // Objetos que se pueden quedar de las opciones.
    public const int RewardPicks = 3;
    // Tamaño del equipo en el modo Gimnasio.
    public const int TeamSize = 6;

    // Siguiente rival ya generado (se reutiliza hasta que se derrote).
    public static TrainerData? NextTrainer { get; set; }

    // Mensaje final pendiente de mostrar (victoria total o derrota).
    public static string? PendingMessage { get; set; }

    // Número total de etapas.
    public static int StageCount => GymCatalog.Stages.Count;

    // Devuelve el siguiente rival, generándolo si todavía no existe.
    public static TrainerData PrepareNextTrainer()
    {
        NextTrainer ??= TrainerGenerator.CreateGymTrainer(GameSession.Data.GymStage);

        return NextTrainer;
    }

    // Empieza una partida nueva de Gimnasio con el equipo elegido y todo reiniciado.
    public static void StartRun(List<PokemonData> team)
    {
        GameData data = GameSession.Data;

        data.GymTeam = team;
        data.GymInventory.Clear();
        data.GymRewardItems.Clear();
        data.Badges.Clear();
        data.GymStage = 0;
        data.GymActive = true;

        NextTrainer = null;
        PendingMessage = null;

        GameSession.Save();
    }

    // Termina la partida: anota el récord, vacía equipo, mochila y medallas, y guarda.
    public static void EndRun()
    {
        GameData data = GameSession.Data;

        data.GymBestStage = Math.Max(data.GymBestStage, data.GymStage);
        data.GymActive = false;
        data.GymStage = 0;

        data.GymTeam = new List<PokemonData>();
        data.GymInventory.Clear();
        data.GymRewardItems.Clear();
        data.Badges.Clear();

        NextTrainer = null;

        GameSession.Save();
    }

    // Registra una victoria: da la medalla, avanza de etapa y prepara las recompensas. Devuelve los mensajes a mostrar.
    public static List<string> RegisterVictory(TrainerData trainer)
    {
        GameData data = GameSession.Data;
        var messages = new List<string>();

        if (trainer.Gym?.Badge != null && !data.Badges.Contains(trainer.Gym.Badge))
        {
            data.Badges.Add(trainer.Gym.Badge);
            messages.Add($"¡Has obtenido la {trainer.Gym.Badge}!");
        }

        data.GymStage++;
        data.GymBestStage = Math.Max(data.GymBestStage, data.GymStage);
        NextTrainer = null;

        // Si ya se superaron todas las etapas, el jugador gana el desafío y se termina la partida.
        if (data.GymStage >= StageCount)
        {
            PendingMessage =
                "¡Has derrotado a los 8 líderes de gimnasio, al Alto Mando y al Campeón!\n" +
                "¡Eres el nuevo Campeón de la Liga Pokémon!";

            messages.Add("¡Has completado el desafío de gimnasios!");

            EndRun();

            return messages;
        }

        // Genera los objetos entre los que el jugador podrá elegir (sin objetos equipables).
        data.GymRewardItems = ItemGenerator
            .GetRandomItems(RewardChoices, null, false)
            .Select(i => i.Name)
            .ToList();

        messages.Add($"Podrás elegir {RewardPicks} objetos de {RewardChoices}.");

        GameSession.Save();

        return messages;
    }

    // Registra una derrota: prepara el mensaje con el resultado y termina la partida.
    public static void RegisterDefeat(TrainerData? trainer)
    {
        int stage = GameSession.Data.GymStage;
        int best = Math.Max(GameSession.Data.GymBestStage, stage);

        PendingMessage =
            (trainer != null ? $"Has caído ante {trainer.Name}.\n" : "")
            + $"Combates ganados: {stage}/{StageCount}.\nRécord: {best}/{StageCount}.";

        EndRun();
    }
}
