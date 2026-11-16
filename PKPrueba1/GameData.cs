using System.Collections.Generic;

namespace PKPrueba1;

// Todos los datos guardados de la partida de un jugador (se serializan a JSON).
public class GameData
{
    // Equipo principal del jugador.
    public List<PokemonData> PokemonParty { get; set; } = new();

    // Mochila principal: nombres de los objetos que posee.
    public List<string> Inventory { get; set; } = new();

    // Indica si hay una partida de Torre Batalla en curso.
    public bool TowerActive { get; set; }

    // Victorias consecutivas en la torre de la racha actual.
    public int TowerWins { get; set; }

    // Mejor racha de victorias en la torre (récord personal).
    public int TowerBestWins { get; set; }

    // Indica si hay una partida de modo Gimnasio en curso.
    public bool GymActive { get; set; }

    // Etapa actual del modo Gimnasio.
    public int GymStage { get; set; }

    // Etapa más lejana alcanzada en el modo Gimnasio.
    public int GymBestStage { get; set; }

    // Equipo usado durante la partida de Gimnasio.
    public List<PokemonData> GymTeam { get; set; } = new();

    // Mochila usada durante la partida de Gimnasio.
    public List<string> GymInventory { get; set; } = new();

    // Medallas conseguidas.
    public List<string> Badges { get; set; } = new();

    // Objetos de recompensa ofrecidos tras un combate de gimnasio.
    public List<string> GymRewardItems { get; set; } = new();

    // Paso de la recompensa pendiente de la torre: 0 = ninguna, 1 = elegir Pokémon, 2 = elegir objeto (permite retomarla al reabrir el juego).
    public int RewardStep { get; set; }

    // Indica si la recompensa pendiente de la torre viene de derrotar a un minijefe.
    public bool RewardIsBoss { get; set; }

    // Pokémon ofrecidos como recompensa pendiente en la torre.
    public List<PokemonData> RewardPokemon { get; set; } = new();

    // Objetos ofrecidos como recompensa pendiente en la torre.
    public List<string> RewardItems { get; set; } = new();
}
