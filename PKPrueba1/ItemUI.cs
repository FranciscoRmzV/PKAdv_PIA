using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKPrueba1;

// Interfaz para usar objetos (curación, revivir, mejoras de combate y curas de estado).
public static class ItemUI
{
    // Flujo completo de usar un objeto: elegir objeto, elegir Pokémon objetivo y aplicar el efecto. Devuelve el mensaje del resultado o null si se cancela.
    public static async Task<string?> UseItemAsync(
        XamlRoot xamlRoot,
        bool inBattle,
        PokemonData? active,
        Dictionary<PokemonData, BattleBoosts>? boosts,
        List<PokemonData>? party = null,
        List<string>? inventory = null)
    {
        // Si no se indican equipo e inventario, se usan los de la torre.
        party ??= GameSession.Data.PokemonParty;
        inventory ??= GameSession.Data.Inventory;

        // Cuenta cuántas unidades hay de cada objeto.
        Dictionary<string, int> counts = inventory
            .GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());

        // Objetos que se pueden usar ahora: no equipables y, fuera de combate, solo los de curación.
        List<ItemData> usable = counts.Keys
            .Select(ItemCatalog.Get)
            .Where(i => i != null && !i.IsEquippable &&
                (inBattle || (i.Kind != ItemKind.Boost && i.Kind != ItemKind.Cure)))
            .Select(i => i!)
            .ToList();

        // Si no hay objetos utilizables, se avisa y se termina.
        if (usable.Count == 0)
        {
            await Dialogs.MessageAsync(
                xamlRoot,
                "Objetos",
                inBattle
                    ? "No tienes objetos para usar."
                    : "No tienes objetos de curación para usar.");

            return null;
        }

        List<ChoiceCard> itemCards = usable
            .Select(i => Dialogs.ItemCard(i, counts[i.Name]))
            .ToList();

        // Muestra los objetos como tarjetas para elegir uno.
        int itemIndex = await Dialogs.ChooseAsync(
            xamlRoot,
            "Elige un objeto",
            null,
            itemCards,
            "Cancelar");

        if (itemIndex < 0)
            return null;

        ItemData item = usable[itemIndex];

        // Las mejoras de combate y las curas de estado tienen su propio método.
        if (item.Kind == ItemKind.Boost)
            return UseBoost(item, active, boosts, inventory);

        if (item.Kind == ItemKind.Cure)
            return await UseCureAsync(xamlRoot, item, party, boosts, inventory);

        // Pokémon que pueden recibir el objeto: debilitados si es un Revivir; heridos si es una poción.
        List<PokemonData> targets = party
            .Where(p => item.Kind == ItemKind.Revive
                ? p.IsFainted
                : !p.IsFainted && p.CurrentHP < p.MaxHP)
            .ToList();

        if (targets.Count == 0)
        {
            await Dialogs.MessageAsync(
                xamlRoot,
                item.Name,
                "Ningún Pokémon necesita este objeto.");

            return null;
        }

        // Pregunta a qué Pokémon se le aplica el objeto.
        int targetIndex = await Dialogs.ChooseAsync(
            xamlRoot,
            $"¿A quién usar {item.Name}?",
            null,
            Dialogs.PartyCards(targets),
            "Cancelar");

        if (targetIndex < 0)
            return null;

        PokemonData target = targets[targetIndex];

        // Gasta una unidad del objeto.
        if (!RemoveItem(inventory, item.Name))
            return null;

        // Revivir: devuelve al Pokémon un porcentaje de sus PS máximos (mínimo 1).
        if (item.Kind == ItemKind.Revive)
        {
            target.CurrentHP = Math.Max(1, target.MaxHP * item.Amount / 100);
            GameSession.Save();

            return $"¡{target.Name} ha revivido!";
        }

        // Poción: cura la cantidad indicada (0 = todos los PS) sin superar el máximo.
        int heal = item.Amount <= 0 ? target.MaxHP : item.Amount;
        int before = target.CurrentHP;

        target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + heal);
        GameSession.Save();

        return $"{target.Name} recuperó {target.CurrentHP - before} PS.";
    }

    // Usa un objeto que cura estados alterados (y, con Restaurar Todo, también los PS). Solo se puede usar en combate.
    private static async Task<string?> UseCureAsync(
        XamlRoot xamlRoot,
        ItemData item,
        List<PokemonData> party,
        Dictionary<PokemonData, BattleBoosts>? boosts,
        List<string> inventory)
    {
        if (boosts == null)
            return null;

        // Indica si el Pokémon tiene el estado que cura el objeto (o necesita curación de PS).
        bool NeedsItem(PokemonData p)
        {
            if (p.IsFainted)
                return false;

            boosts.TryGetValue(p, out BattleBoosts? b);

            bool statusMatch = b != null && (item.Stat switch
            {
                "all" => b.Status != "" || b.Confusion > 0,
                "psn" => b.Status == "psn" || b.Status == "tox",
                _ => b.Status == item.Stat
            });

            bool needsHealing = item.Effect == "restore" && p.CurrentHP < p.MaxHP;

            return statusMatch || needsHealing;
        }

        List<PokemonData> targets = party.Where(NeedsItem).ToList();

        if (targets.Count == 0)
        {
            await Dialogs.MessageAsync(xamlRoot, item.Name, "Ningún Pokémon necesita este objeto.");
            return null;
        }

        int index = await Dialogs.ChooseAsync(
            xamlRoot,
            $"¿A quién usar {item.Name}?",
            null,
            Dialogs.PartyCards(targets),
            "Cancelar");

        if (index < 0 || !RemoveItem(inventory, item.Name))
            return null;

        PokemonData target = targets[index];

        // Quita el estado alterado (o todos) del Pokémon elegido.
        if (boosts.TryGetValue(target, out BattleBoosts? state))
        {
            if (item.Stat == "all")
            {
                state.Status = "";
                state.Confusion = 0;
            }
            else if (item.Stat == "psn")
            {
                if (state.Status == "psn" || state.Status == "tox") state.Status = "";
            }
            else if (state.Status == item.Stat)
            {
                state.Status = "";
            }
        }

        if (item.Effect == "restore")
            target.CurrentHP = target.MaxHP;

        GameSession.Save();

        return $"{target.Name} se ha curado.";
    }

    // Quita una unidad del objeto del inventario y guarda. Devuelve false si no estaba.
    private static bool RemoveItem(List<string> inventory, string name)
    {
        int index = inventory.IndexOf(name);

        if (index < 0)
            return false;

        inventory.RemoveAt(index);
        GameSession.Save();

        return true;
    }

    // Aplica una mejora de combate (Ataque X, etc.) al Pokémon activo: sube un nivel la estadística (máximo +6) o activa el crítico.
    private static string? UseBoost(
        ItemData item,
        PokemonData? active,
        Dictionary<PokemonData, BattleBoosts>? boosts,
        List<string> inventory)
    {
        if (active == null || boosts == null)
            return null;

        if (!boosts.TryGetValue(active, out BattleBoosts? b))
        {
            b = new BattleBoosts();
            boosts[active] = b;
        }

        string text;

        switch (item.Stat)
        {
            case "atk":
                b.Atk = Math.Min(6, b.Atk + 1);
                text = "El Ataque";
                break;
            case "def":
                b.Def = Math.Min(6, b.Def + 1);
                text = "La Defensa";
                break;
            case "spa":
                b.SpA = Math.Min(6, b.SpA + 1);
                text = "El Ataque Especial";
                break;
            case "spd":
                b.SpD = Math.Min(6, b.SpD + 1);
                text = "La Defensa Especial";
                break;
            case "spe":
                b.Spe = Math.Min(6, b.Spe + 1);
                text = "La Velocidad";
                break;
            default:
                b.Crit = true;
                text = "La probabilidad de crítico";
                break;
        }

        if (!RemoveItem(inventory, item.Name))
            return null;

        return $"¡{text} de {active.Name} ha subido!";
    }
}
