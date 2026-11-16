using System;

namespace PKPrueba1;

// Efectos de los objetos equipados (llevados) por los Pokémon durante el combate.
public static class HeldItems
{
    // Devuelve el objeto que lleva el Pokémon (null si no lleva ninguno o tiene la habilidad Zoquete, que los anula).
    public static ItemData? Of(PokemonData pokemon)
    {
        if (pokemon.Ability == "klutz" || pokemon.BattleAbility == "klutz")
            return null;

        string? name = pokemon.EffectiveHeldItem;

        return name == null ? null : ItemCatalog.Get(name);
    }

    // Devuelve el código del efecto del objeto equipado (solo para objetos de tipo Held).
    public static string Effect(PokemonData pokemon)
    {
        ItemData? item = Of(pokemon);

        return item != null && item.Kind == ItemKind.Held ? item.Effect : "";
    }

    // Multiplicador de ataque: x1.3 con Cinta Elección (físico) o Gafas Elección (especial).
    public static double AttackMultiplier(PokemonData pokemon, bool special)
    {
        string effect = Effect(pokemon);

        if (!special && effect == "atk") return 1.3;
        if (special && effect == "spa") return 1.3;

        return 1.0;
    }

    // Multiplicador de defensa: x1.5 con Chaleco Asalto (especial) o Mineral Evol (si aún puede evolucionar).
    public static double DefenseMultiplier(PokemonData pokemon, bool special)
    {
        string effect = Effect(pokemon);

        if (special && effect == "spd") return 1.5;
        if (effect == "evio" && pokemon.Species.Evolutions.Count > 0) return 1.5;

        return 1.0;
    }

    // Multiplicador de velocidad: x1.3 con Pañuelo Elección.
    public static double SpeedMultiplier(PokemonData pokemon)
    {
        return Effect(pokemon) == "spe" ? 1.3 : 1.0;
    }

    // Multiplicador de daño: x1.2 con el objeto del tipo del movimiento, x1.2 de Cinta Experto si es supereficaz y x1.3 de Vidasfera.
    public static double DamageMultiplier(PokemonData attacker, PokemonMove move, double effectiveness)
    {
        string effect = Effect(attacker);
        double result = 1.0;

        if (effect == "type:" + move.Type) result *= 1.2;
        if (effect == "expert" && effectiveness > 1.0) result *= 1.2;
        if (effect == "orb") result *= 1.3;

        return result;
    }

    // PS que recupera al final del turno con Restos (1/16 de sus PS máximos, mínimo 1).
    public static int LeftoversHeal(PokemonData pokemon)
    {
        return Effect(pokemon) == "leftovers" ? Math.Max(1, pokemon.MaxHP / 16) : 0;
    }

    // PS que pierde al atacar con Vidasfera (1/10 de sus PS máximos, mínimo 1).
    public static int LifeOrbRecoil(PokemonData pokemon)
    {
        return Effect(pokemon) == "orb" ? Math.Max(1, pokemon.MaxHP / 10) : 0;
    }

    // true si lleva Banda Aguante.
    public static bool HasSash(PokemonData pokemon) => Effect(pokemon) == "sash";

    // Si lleva la megapiedra correcta, devuelve el id de su forma mega; si no, null.
    public static int? MegaFormFor(PokemonData pokemon)
    {
        ItemData? item = Of(pokemon);

        if (item != null && item.Kind == ItemKind.MegaStone && item.MegaBaseId == pokemon.Id)
            return item.MegaFormId;

        return null;
    }

    // Equipa un objeto aleatorio a un Pokémon rival (el de su tipo tiene el doble de probabilidad).
    public static void GiveRandomToEnemy(PokemonData pokemon, Random random)
    {
        var pool = new System.Collections.Generic.List<string>();

        string mainType = pokemon.Species.Type1;
        ItemData? typeItem = ItemCatalog.All.Find(i => i.Effect == "type:" + mainType);

        if (typeItem != null)
        {
            pool.Add(typeItem.Name);
            pool.Add(typeItem.Name);
        }

        pool.Add(pokemon.Species.Attack >= pokemon.Species.SpAttack ? "Cinta Elección" : "Gafas Elección");
        pool.Add("Vidasfera");
        pool.Add("Restos");
        pool.Add("Cinta Experto");
        pool.Add("Banda Aguante");

        pokemon.HeldItem = pool[random.Next(pool.Count)];
    }
}
