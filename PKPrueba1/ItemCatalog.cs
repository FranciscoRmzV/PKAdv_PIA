using System.Collections.Generic;
using System.Linq;

namespace PKPrueba1;

// Catálogo con todos los objetos del juego.
public static class ItemCatalog
{
    // URL base de las imágenes de objetos.
    private const string Base =
        "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/";

    // Lista de todos los objetos: pociones, revivir, mejoras X, curas, objetos de tipo, objetos equipables y (en el constructor estático) megapiedras.
    public static readonly List<ItemData> All = new()
    {
        // Objetos de curación (Amount = PS que restauran; 0 = todos).
        new ItemData("Poción", "Restaura 20 PS.", Base + "potion.png", ItemKind.Heal, 20),
        new ItemData("Superpoción", "Restaura 60 PS.", Base + "super-potion.png", ItemKind.Heal, 60),
        new ItemData("Hiperpoción", "Restaura 120 PS.", Base + "hyper-potion.png", ItemKind.Heal, 120),
        new ItemData("Poción Máxima", "Restaura todos los PS.", Base + "max-potion.png", ItemKind.Heal, 0),
        // Objetos que reviven (Amount = % de PS con el que vuelve).
        new ItemData("Revivir", "Revive a un Pokémon con la mitad de sus PS.", Base + "revive.png", ItemKind.Revive, 50),
        new ItemData("Revivir Máximo", "Revive a un Pokémon con todos sus PS.", Base + "max-revive.png", ItemKind.Revive, 100),
        // Mejoras temporales de combate (Stat = estadística que suben).
        new ItemData("Ataque X", "Sube el Ataque en el combate.", Base + "x-attack.png", ItemKind.Boost, 0, "atk"),
        new ItemData("Defensa X", "Sube la Defensa en el combate.", Base + "x-defense.png", ItemKind.Boost, 0, "def"),
        new ItemData("At. Especial X", "Sube el Ataque Especial en el combate.", Base + "x-sp-atk.png", ItemKind.Boost, 0, "spa"),
        new ItemData("Def. Especial X", "Sube la Defensa Especial en el combate.", Base + "x-sp-def.png", ItemKind.Boost, 0, "spd"),
        new ItemData("Velocidad X", "Sube la Velocidad en el combate.", Base + "x-speed.png", ItemKind.Boost, 0, "spe"),
        new ItemData("Crítico X", "Aumenta la probabilidad de golpe crítico en el combate.", Base + "dire-hit.png", ItemKind.Boost, 0, "crit"),

        // Objetos que curan estados alterados (Stat = estado que curan, "all" = todos).
        new ItemData("Antídoto", "Cura el envenenamiento.", Base + "antidote.png", ItemKind.Cure, 0, "psn"),
        new ItemData("Antiparalizador", "Cura la parálisis.", Base + "paralyze-heal.png", ItemKind.Cure, 0, "par"),
        new ItemData("Despertar", "Despierta a un Pokémon dormido.", Base + "awakening.png", ItemKind.Cure, 0, "slp"),
        new ItemData("Antiquemar", "Cura las quemaduras.", Base + "burn-heal.png", ItemKind.Cure, 0, "brn"),
        new ItemData("Antihielo", "Descongela a un Pokémon.", Base + "ice-heal.png", ItemKind.Cure, 0, "frz"),
        new ItemData("Cura Total", "Cura todos los estados alterados y la confusión.", Base + "full-heal.png", ItemKind.Cure, 0, "all"),
        new ItemData("Restaurar Todo", "Restaura todos los PS y cura todos los estados.", Base + "full-restore.png", ItemKind.Cure, 0, "all") { Effect = "restore" },

        // Objetos equipables que potencian un 20% los movimientos de un tipo.
        Type("Pañuelo de Seda", "NOR", "silk-scarf"),
        Type("Carbón", "FUE", "charcoal"),
        Type("Agua Mística", "AGU", "mystic-water"),
        Type("Semilla Milagro", "PLA", "miracle-seed"),
        Type("Imán", "ELE", "magnet"),
        Type("Hielo Perpetuo", "HIE", "never-melt-ice"),
        Type("Cinturón Negro", "LUC", "black-belt"),
        Type("Flecha Venenosa", "VEN", "poison-barb"),
        Type("Arena Fina", "TIE", "soft-sand"),
        Type("Pico Afilado", "VOL", "sharp-beak"),
        Type("Cuchara Torcida", "PSI", "twisted-spoon"),
        Type("Polvo Plata", "BIC", "silver-powder"),
        Type("Piedra Dura", "ROC", "hard-stone"),
        Type("Hechizo", "FAN", "spell-tag"),
        Type("Colmillo de Dragón", "DRA", "dragon-fang"),
        Type("Gafas de Sol", "SIN", "black-glasses"),
        Type("Revest. Metálico", "ACE", "metal-coat"),
        Type("Pluma Feérica", "HAD", "fairy-feather"),

        // Otros objetos equipables con efectos especiales (ver HeldItems).
        Held("Cinta Elección", "Sube un 30% el Ataque.", "choice-band", "atk"),
        Held("Gafas Elección", "Sube un 30% el Ataque Especial.", "choice-specs", "spa"),
        Held("Pañuelo Elección", "Sube un 30% la Velocidad.", "choice-scarf", "spe"),
        Held("Chaleco Asalto", "Sube un 50% la Defensa Especial.", "assault-vest", "spd"),
        Held("Mineral Evol", "Sube un 50% la Defensa y la Def. Esp. si el Pokémon aún puede evolucionar.", "eviolite", "evio"),
        Held("Cinta Experto", "Potencia un 20% los ataques supereficaces.", "expert-belt", "expert"),
        Held("Vidasfera", "Sube un 30% el daño, pero el portador pierde PS al atacar.", "life-orb", "orb"),
        Held("Restos", "Recupera 1/16 de los PS al final de cada turno.", "leftovers", "leftovers"),
        Held("Banda Aguante", "Resiste un golpe mortal con 1 PS (una vez por combate).", "focus-sash", "sash")
    };

    // Constructor estático: añade al catálogo una megapiedra por cada megaevolución existente.
    static ItemCatalog()
    {
        foreach (PokemonSpecies mega in PokemonDatabase.AllMegas)
        {
            PokemonSpecies baseSpecies = PokemonDatabase.Get(mega.BaseSpeciesId!.Value);

            All.Add(new ItemData(
                mega.StoneName ?? "Megapiedra",
                $"Permite a {baseSpecies.Name} megaevolucionar en combate ({mega.Name}).",
                Base + (mega.StoneSprite ?? "") + ".png",
                ItemKind.MegaStone)
            {
                MegaBaseId = baseSpecies.Id,
                MegaFormId = mega.Id
            });
        }
    }

    // Crea un objeto equipable que potencia los movimientos de un tipo.
    private static ItemData Type(string name, string type, string sprite)
    {
        return new ItemData(
            name,
            $"Potencia un 20% los movimientos de tipo {TypeChart.DisplayName(type)}.",
            Base + sprite + ".png",
            ItemKind.Held)
        {
            Effect = "type:" + type
        };
    }

    // Crea un objeto equipable con el efecto indicado.
    private static ItemData Held(string name, string description, string sprite, string effect)
    {
        return new ItemData(name, description, Base + sprite + ".png", ItemKind.Held)
        {
            Effect = effect
        };
    }

    // Busca un objeto por nombre (null si no existe).
    public static ItemData? Get(string name)
    {
        return All.FirstOrDefault(i => i.Name == name);
    }
}
