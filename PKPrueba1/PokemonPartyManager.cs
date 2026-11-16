using System.Collections.Generic;
using System.Linq;

namespace PKPrueba1;

// Gestiona el equipo Pokémon del jugador (máximo 6).
public static class PokemonPartyManager
{
    // Número máximo de Pokémon en el equipo.
    public const int MaxPartySize = 6;

    // Acceso directo al equipo guardado en la sesión actual.
    public static List<PokemonData> Party => GameSession.Data.PokemonParty;

    // Añade un Pokémon al equipo y guarda. Devuelve false si el equipo ya está completo.
    public static bool AddPokemon(PokemonData pokemon)
    {
        if (Party.Count >= MaxPartySize)
            return false;

        Party.Add(pokemon);
        GameSession.Save();

        return true;
    }

    // Devuelve el primer Pokémon del equipo que no esté debilitado (null si no queda ninguno).
    public static PokemonData? GetFirstHealthyPokemon()
    {
        return Party.FirstOrDefault(p => !p.IsFainted);
    }

    // Cura por completo a todo el equipo y guarda la partida.
    public static void HealParty()
    {
        foreach (PokemonData pokemon in Party)
            pokemon.Heal();

        GameSession.Save();
    }
}
