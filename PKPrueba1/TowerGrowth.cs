using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKPrueba1;

// Gestiona el crecimiento de los Pokémon tras cada combate de la torre: movimientos nuevos y evoluciones.
public static class TowerGrowth
{
    // Recuerda en qué nivel el jugador rechazó evolucionar a cada Pokémon, para no preguntarle otra vez en ese mismo nivel.
    private static readonly Dictionary<PokemonData, int> Declined = new();

    // Devuelve las evoluciones a las que el Pokémon ya puede acceder por su nivel actual.
    public static List<EvolutionInfo> GetAvailableEvolutions(PokemonData pokemon)
    {
        return pokemon.Species.Evolutions
            .Where(e => pokemon.Level >= e.Level)
            .ToList();
    }

    // Recorre el equipo y, para cada Pokémon, ofrece aprender los movimientos pendientes y evolucionar si puede. Guarda al terminar.
    public static async Task ProcessAsync(XamlRoot xamlRoot)
    {
        foreach (PokemonData pokemon in GameSession.Data.PokemonParty.ToList())
        {
            bool changed;

            // Se repite mientras el Pokémon siga evolucionando (una evolución puede desbloquear otra).
            do
            {
                changed = false;

                // Pregunta uno a uno por los movimientos que el Pokémon puede aprender.
                while (pokemon.PendingMoves.Count > 0)
                {
                    string move = pokemon.PendingMoves[0];
                    pokemon.PendingMoves.RemoveAt(0);

                    await Dialogs.LearnMoveAsync(xamlRoot, pokemon, move);
                }

                // Evoluciones disponibles ahora mismo.
                List<EvolutionInfo> options = GetAvailableEvolutions(pokemon);

                // true si el jugador ya rechazó evolucionar en este mismo nivel.
                bool alreadyDeclined =
                    Declined.TryGetValue(pokemon, out int declinedLevel) &&
                    declinedLevel == pokemon.Level;

                if (options.Count > 0 && !alreadyDeclined)
                {
                    // Convierte cada evolución posible en una tarjeta con nombre, estadísticas base, sprite y tipos para el diálogo.
                    List<ChoiceCard> cards = options
                        .Select(e =>
                        {
                            PokemonSpecies s = PokemonDatabase.Get(e.ToId);
                            return new ChoiceCard(
                                s.Name,
                                $"Total base {s.BaseTotal}",
                                PokemonSprites.GetFrontSprite(s.Id),
                                UiTheme.TypeColor(s.Type1),
                                s.Types);
                        })
                        .ToList();

                    // Muestra el diálogo de elección; devuelve el índice elegido o -1 si el jugador decide no evolucionar.
                    int index = await Dialogs.ChooseAsync(
                        xamlRoot,
                        $"¡{pokemon.Name} puede evolucionar!",
                        "Elige su evolución:",
                        cards,
                        "No evolucionar ahora");

                    if (index >= 0)
                    {
                        string oldName = pokemon.Name;
                        // Aplica la evolución elegida.
                        pokemon.EvolveTo(options[index].ToId);

                        await Dialogs.MessageAsync(
                            xamlRoot,
                            "¡Evolución!",
                            $"¡{oldName} evolucionó a {pokemon.Name}!");

                        changed = true;
                    }
                    else
                    {
                        // El jugador rechazó: se anota el nivel para no volver a preguntar hasta que suba.
                        Declined[pokemon] = pokemon.Level;
                    }
                }
            }
            while (changed);
        }

        GameSession.Save();
    }
}
