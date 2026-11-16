using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKPrueba1;

// Interfaz para gestionar el equipo: objetos equipados, enseñar movimientos y elegir quién combate primero.
public static class EquipUI
{
    // Pantalla para equipar o quitar objetos: elige un Pokémon y se repite hasta que el jugador cierre.
    public static async Task ManageAsync(XamlRoot xamlRoot, List<PokemonData> party, List<string> inventory)
    {
        while (true)
        {
            int index = await Dialogs.ChooseAsync(
                xamlRoot,
                "Equipar objetos",
                "Elige un Pokémon para darle o quitarle un objeto. Los objetos equipados cambian sus estadísticas en combate.",
                Dialogs.PartyCards(party),
                "Cerrar");

            if (index < 0)
                return;

            await EquipOneAsync(xamlRoot, party[index], inventory);
        }
    }

    // Permite equipar, cambiar o quitar el objeto de un Pokémon concreto.
    private static async Task EquipOneAsync(XamlRoot xamlRoot, PokemonData pokemon, List<string> inventory)
    {
        Dictionary<string, int> counts = inventory
            .GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());

        // Objetos equipables disponibles: los normales y las megapiedras que corresponden a este Pokémon.
        List<ItemData> items = counts.Keys
            .Select(ItemCatalog.Get)
            .Where(i => i != null && i.IsEquippable &&
                (i.Kind == ItemKind.Held || i.MegaBaseId == pokemon.Id))
            .Select(i => i!)
            .ToList();

        ItemData? current = pokemon.HeldItem == null ? null : ItemCatalog.Get(pokemon.HeldItem);

        // Si no hay nada que equipar ni quitar, se avisa.
        if (items.Count == 0 && current == null)
        {
            await Dialogs.MessageAsync(
                xamlRoot,
                "Sin objetos",
                $"No tienes objetos equipables para {pokemon.Name}. Consíguelos como recompensa tras los combates.");

            return;
        }

        var cards = new List<ChoiceCard>();

        // Si ya lleva un objeto, se añade la opción "Quitar" como primera tarjeta.
        if (current != null)
        {
            cards.Add(new ChoiceCard(
                $"Quitar {current.Name}",
                "Vuelve a tu inventario.",
                current.SpriteUrl,
                "#EF4444"));
        }

        cards.AddRange(items.Select(i => Dialogs.ItemCard(i, counts[i.Name])));

        int choice = await Dialogs.ChooseAsync(
            xamlRoot,
            $"Objeto para {pokemon.Name}",
            current != null
                ? $"Lleva equipado: {current.Name}. Si eliges otro, el actual vuelve al inventario."
                : "Elige el objeto que llevará equipado.",
            cards,
            "Volver");

        if (choice < 0)
            return;

        // Se eligió quitar el objeto: vuelve al inventario.
        if (current != null && choice == 0)
        {
            inventory.Add(current.Name);
            pokemon.HeldItem = null;
            GameSession.Save();
            return;
        }

        // Se eligió un objeto nuevo (se compensa la tarjeta extra de "Quitar"). Pasa del inventario al Pokémon y el anterior vuelve al inventario.
        ItemData chosen = items[choice - (current != null ? 1 : 0)];

        inventory.Remove(chosen.Name);

        if (current != null)
            inventory.Add(current.Name);

        pokemon.HeldItem = chosen.Name;
        GameSession.Save();
    }

    // Pantalla para enseñar movimientos de MT / tutor a los Pokémon del equipo.
    public static async Task TeachAsync(XamlRoot xamlRoot, List<PokemonData> party)
    {
        while (true)
        {
            int index = await Dialogs.ChooseAsync(
                xamlRoot,
                "Enseñar movimientos",
                "Elige un Pokémon para enseñarle movimientos de MT, tutor o huevo. Los más potentes requieren más nivel.",
                Dialogs.PartyCards(party),
                "Cerrar");

            if (index < 0)
                return;

            PokemonData pokemon = party[index];

            // Movimientos disponibles que aún no sabe, ordenados de más a menos potentes.
            List<PokemonMove> options = PokemonDatabase.GetTutorMoves(pokemon.Id, pokemon.Level)
                .Select(MoveCatalog.Create)
                .Where(m => m != null && !pokemon.Moves.Any(k => k.Name == m.Name))
                .Select(m => m!)
                .OrderByDescending(m => m.IsStatus ? 0 : m.Power)
                .ThenBy(m => m.Name)
                .ToList();

            if (options.Count == 0)
            {
                await Dialogs.MessageAsync(
                    xamlRoot,
                    "Sin movimientos",
                    $"{pokemon.Name} no tiene movimientos nuevos que aprender a nivel {pokemon.Level}.");

                continue;
            }

            int move = await Dialogs.ChooseAsync(
                xamlRoot,
                $"Movimientos para {pokemon.Name}",
                $"Disponibles a nivel {pokemon.Level}: {options.Count}.",
                options.Select(m => Dialogs.MoveCard(m, pokemon.Species.Types.Contains(m.Type))).ToList(),
                "Volver",
                4);

            if (move < 0)
                continue;

            // Aprende el movimiento elegido (si ya sabe 4, pregunta cuál olvidar).
            await Dialogs.LearnMoveAsync(xamlRoot, pokemon, options[move].Name);

            GameSession.Save();
        }
    }

    // Permite elegir qué Pokémon sale primero a combatir moviéndolo a la primera posición del equipo.
    public static async Task ChooseLeadAsync(XamlRoot xamlRoot, List<PokemonData> party)
    {
        if (party.Count < 2)
        {
            await Dialogs.MessageAsync(
                xamlRoot,
                "Pokémon inicial",
                "Necesitas al menos 2 Pokémon para cambiar quién combate primero.");

            return;
        }

        int index = await Dialogs.ChooseAsync(
            xamlRoot,
            "¿Quién combatirá primero?",
            $"Ahora sale primero: {party[0].Name}. Si el elegido está debilitado, saldrá el siguiente en pie.",
            Dialogs.PartyCards(party),
            "Cancelar");

        if (index <= 0)
            return;

        PokemonData chosen = party[index];

        party.RemoveAt(index);
        party.Insert(0, chosen);

        GameSession.Save();

        await Dialogs.MessageAsync(
            xamlRoot,
            "Pokémon inicial",
            $"{chosen.Name} combatirá primero.");
    }
}
