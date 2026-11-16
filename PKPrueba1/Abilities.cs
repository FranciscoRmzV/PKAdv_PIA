using System;
using System.Collections.Generic;
using System.Linq;

namespace PKPrueba1;

// Datos de una habilidad.
public class AbilityInfo
{
    public int Id { get; set; }
    // Identificador interno (por ejemplo "intimidate").
    public string Identifier { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    // true si la habilidad tiene efecto programado en el combate.
    public bool Implemented => Abilities.Implemented.Contains(Identifier);
}

// Datos de una naturaleza: sube una estadística y baja otra.
public class NatureInfo
{
    public string Identifier { get; set; } = "";
    public string Name { get; set; } = "";

    // Id de la estadística que sube (2 = Ataque, 3 = Defensa, 4 = At. Esp., 5 = Def. Esp., 6 = Velocidad).
    public int Up { get; set; }
    public int Down { get; set; }

    // Una naturaleza es neutra si sube y baja la misma estadística.
    public bool IsNeutral => Up == Down;

    // Describe el efecto de la naturaleza, por ejemplo "+Ataque / -Velocidad".
    public string Describe()
    {
        if (IsNeutral)
            return "Neutra";

        return $"+{StatName(Up)} / -{StatName(Down)}";
    }

    // Convierte el id de estadística en su nombre en español.
    private static string StatName(int id)
    {
        return id switch
        {
            2 => "Ataque",
            3 => "Defensa",
            4 => "At. Esp.",
            5 => "Def. Esp.",
            _ => "Velocidad"
        };
    }
}

// Catálogo de habilidades y naturalezas (los datos están en Abilities.Generated.cs).
public static partial class Abilities
{
    // Habilidades que existen pero no tienen efecto en este tipo de combates.
    public static readonly HashSet<string> NoBattleEffect = new()
    {
        "plus", "minus", "friend-guard", "telepathy", "healer", "honey-gather", "pickup", "run-away", "illuminate",
        "gluttony", "harvest", "sticky-hold", "unnerve", "multitype", "heavy-metal", "light-metal", "pressure", "rivalry"
    };

    // Identificadores de habilidades con efecto programado.
    public static readonly HashSet<string> Implemented = new();

    // Índices para buscar habilidades por id y por identificador, habilidades por especie y lista de naturalezas.
    private static readonly Dictionary<int, AbilityInfo> ById = new();
    private static readonly Dictionary<string, AbilityInfo> ByIdentifier = new();
    private static readonly Dictionary<int, List<int>> SpeciesAbilityIds = new();
    private static readonly List<NatureInfo> NatureList = new();
    private static readonly Random Rng = new();

    // Constructor estático: carga habilidades, habilidades de cada especie y naturalezas desde los datos en bruto.
    static Abilities()
    {
        foreach (string line in RawAbilities)
        {
            string[] p = line.Split('|');

            var info = new AbilityInfo
            {
                Id = int.Parse(p[0]),
                Identifier = p[1],
                Name = p[2],
                Description = p.Length > 3 ? p[3] : ""
            };

            ById[info.Id] = info;
            ByIdentifier[info.Identifier] = info;
            Implemented.Add(info.Identifier);
        }

        foreach (string line in RawSpeciesAbilities)
        {
            string[] p = line.Split('|');

            SpeciesAbilityIds[int.Parse(p[0])] = p[1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.Parse(x.Split(':')[0]))
                .Distinct()
                .ToList();
        }

        foreach (string line in RawNatures)
        {
            string[] p = line.Split('|');

            NatureList.Add(new NatureInfo
            {
                Identifier = p[1],
                Name = p[2],
                Up = int.Parse(p[3]),
                Down = int.Parse(p[4])
            });
        }
    }

    // Lista de todas las naturalezas.
    public static IReadOnlyList<NatureInfo> Natures => NatureList;

    // Busca una habilidad por identificador (null si no existe).
    public static AbilityInfo? Get(string? identifier)
    {
        return identifier != null && ByIdentifier.TryGetValue(identifier, out AbilityInfo? info) ? info : null;
    }

    // Busca una naturaleza por identificador.
    public static NatureInfo? GetNature(string? identifier)
    {
        return NatureList.FirstOrDefault(n => n.Identifier == identifier);
    }

    // Devuelve una naturaleza al azar.
    public static NatureInfo RandomNature()
    {
        return NatureList[Rng.Next(NatureList.Count)];
    }

    // Devuelve las habilidades posibles de una especie.
    public static List<AbilityInfo> For(int speciesId)
    {
        if (!SpeciesAbilityIds.TryGetValue(speciesId, out List<int>? ids))
            return new List<AbilityInfo>();

        return ids.Where(ById.ContainsKey).Select(id => ById[id]).ToList();
    }

    // Elige una habilidad al azar para la especie, prefiriendo las que tienen efecto implementado.
    public static string PickFor(int speciesId)
    {
        List<AbilityInfo> all = For(speciesId);

        if (all.Count == 0)
            return "";

        List<AbilityInfo> useful = all.Where(a => a.Implemented).ToList();

        return (useful.Count > 0 ? useful : all)[Rng.Next((useful.Count > 0 ? useful : all).Count)].Identifier;
    }
}
