using System;
using System.Collections.Generic;
using System.Linq;

namespace PKPrueba1;

// Genera objetos aleatorios que se ofrecen como recompensa.
public static class ItemGenerator
{
    // Generador de números aleatorios compartido.
    private static readonly Random Random = new();

    // Devuelve "amount" objetos distintos al azar del catálogo. Las megapiedras solo salen si el equipo tiene el Pokémon correspondiente.
    public static List<ItemData> GetRandomItems(
        int amount,
        IEnumerable<PokemonData>? party = null,
        bool includeEquippable = true)
    {
        // Conjunto con los ids de las especies del equipo (vacío si no se pasa equipo).
        HashSet<int> speciesInParty = party == null
            ? new HashSet<int>()
            : party.Select(p => p.Id).ToHashSet();

        // Filtra los objetos disponibles: excluye los equipables si no se piden y las megapiedras sin Pokémon compatible.
        List<ItemData> available = ItemCatalog.All
            .Where(i => !i.IsEquippable || (includeEquippable &&
                (i.Kind != ItemKind.MegaStone || speciesInParty.Contains(i.MegaBaseId))))
            .ToList();

        // Lista con los objetos elegidos.
        List<ItemData> result = new();

        // Elige objetos al azar sin repetir hasta completar la cantidad pedida o quedarse sin opciones.
        while (result.Count < amount && available.Count > 0)
        {
            int index = Random.Next(available.Count);

            result.Add(available[index]);
            available.RemoveAt(index);
        }

        return result;
    }
}
