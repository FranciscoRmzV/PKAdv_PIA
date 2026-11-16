using System;
using System.Collections.Generic;
using System.Linq;

namespace PKPrueba1;

// Datos de una evolución: a qué especie evoluciona y desde qué nivel.
public class EvolutionInfo
{
    public int ToId { get; set; }
    public int Level { get; set; }
}

// Una entrada de la lista de movimientos que puede aprender una especie.
public class LearnEntry
{
    public int Level { get; set; }
    public string Move { get; set; } = "";

    // true si se aprende por MT / tutor en lugar de por nivel.
    public bool Machine { get; set; }
}

// Datos de una especie de Pokémon: tipos, estadísticas base, evoluciones y movimientos.
public class PokemonSpecies
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type1 { get; set; } = "NOR";
    public string? Type2 { get; set; }

    public int HP { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int SpAttack { get; set; }
    public int SpDefense { get; set; }
    public int Speed { get; set; }

    // Suma de las estadísticas base (indica lo fuerte que es la especie).
    public int BaseTotal => HP + Attack + Defense + SpAttack + SpDefense + Speed;

    // Solo para formas mega: id de la especie original.
    public int? BaseSpeciesId { get; set; }

    // Solo para formas mega: nombre de la megapiedra necesaria.
    public string? StoneName { get; set; }

    // Solo para formas mega: nombre del sprite de la megapiedra.
    public string? StoneSprite { get; set; }

    // true si es una forma mega.
    public bool IsMega => BaseSpeciesId.HasValue;

    // Id de la especie de la que evoluciona (null si es básica).
    public int? PreEvolutionId { get; set; }
    // Evoluciones posibles de la especie.
    public List<EvolutionInfo> Evolutions { get; } = new();
    // Movimientos que aprende por nivel o por MT.
    public List<LearnEntry> OwnMoves { get; } = new();

    // Lista con los tipos de la especie (uno o dos).
    public List<string> Types
    {
        get
        {
            var list = new List<string> { Type1 };
            if (Type2 != null)
                list.Add(Type2);
            return list;
        }
    }
}

// Base de datos de especies: carga los datos en bruto y ofrece consultas (especies, legendarios, movimientos).
public static partial class PokemonDatabase
{
    // Todas las especies por id.
    private static readonly Dictionary<int, PokemonSpecies> Species = new();
    // Caché de las listas de movimientos ya calculadas.
    private static readonly Dictionary<int, List<LearnEntry>> LearnsetCache = new();
    // Especies básicas (sin preevolución, no legendarias ni mega).
    private static readonly List<PokemonSpecies> Basics = new();
    // Ids de los Pokémon legendarios.
    private static readonly HashSet<int> LegendaryIds = new();

    // Constructor estático: carga especies, movimientos, legendarios y megas, y calcula preevoluciones y especies básicas.
    static PokemonDatabase()
    {
        foreach (string line in RawSpecies)
            ParseSpecies(line);

        foreach (string line in RawLearn)
            ParseLearnLine(line);

        foreach (string id in LegendaryList.Split(',', StringSplitOptions.RemoveEmptyEntries))
            LegendaryIds.Add(int.Parse(id));

        foreach (string line in RawMegas)
            ParseMega(line);

        // Rellena la preevolución de cada especie a partir de las evoluciones de las demás.
        foreach (PokemonSpecies sp in Species.Values)
        {
            foreach (EvolutionInfo evo in sp.Evolutions)
            {
                if (Species.TryGetValue(evo.ToId, out PokemonSpecies? target))
                    target.PreEvolutionId = sp.Id;
            }
        }

        foreach (PokemonSpecies sp in Species.Values.OrderBy(s => s.Id))
        {
            // Una especie es "básica" si no evoluciona de otra y no es mega ni legendaria.
            if (sp.PreEvolutionId == null && !sp.IsMega && !LegendaryIds.Contains(sp.Id))
                Basics.Add(sp);
        }
    }

    // Interpreta una línea de RawSpecies: id|nombre|tipo1|tipo2|estadísticas|evoluciones (id:nivel).
    private static void ParseSpecies(string line)
    {
        string[] f = line.Split('|');

        var sp = new PokemonSpecies
        {
            Id = int.Parse(f[0]),
            Name = f[1],
            Type1 = f[2],
            Type2 = f[3] == "-" ? null : f[3]
        };

        string[] stats = f[4].Split(',');
        sp.HP = int.Parse(stats[0]);
        sp.Attack = int.Parse(stats[1]);
        sp.Defense = int.Parse(stats[2]);
        sp.SpAttack = int.Parse(stats[3]);
        sp.SpDefense = int.Parse(stats[4]);
        sp.Speed = int.Parse(stats[5]);

        if (f.Length > 5 && f[5].Length > 0)
        {
            foreach (string e in f[5].Split(','))
            {
                string[] parts = e.Split(':');

                sp.Evolutions.Add(new EvolutionInfo
                {
                    ToId = int.Parse(parts[0]),
                    Level = int.Parse(parts[1])
                });
            }
        }

        Species[sp.Id] = sp;
    }

    // Interpreta una línea de RawLearn: movimientos que aprende una especie ("idMov@nivel"; con prefijo "m" son de MT).
    private static void ParseLearnLine(string line)
    {
        string[] p = line.Split('|');

        if (!Species.TryGetValue(int.Parse(p[0]), out PokemonSpecies? sp) || p.Length < 2)
            return;

        foreach (string token in p[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            bool machine = token[0] == 'm';
            string[] parts = (machine ? token.Substring(1) : token).Split('@');

            string? name = MoveCatalog.NameOf(int.Parse(parts[0]));

            if (name == null)
                continue;

            sp.OwnMoves.Add(new LearnEntry
            {
                Level = int.Parse(parts[1]),
                Move = name,
                Machine = machine
            });
        }
    }

    // Indica si existe una especie con ese id.
    public static bool Exists(int id) => Species.ContainsKey(id);

    // Devuelve la especie con ese id (si no existe, devuelve Rattata, id 19, como valor por defecto).
    public static PokemonSpecies Get(int id)
    {
        return Species.TryGetValue(id, out PokemonSpecies? sp) ? sp : Species[19];
    }

    // Lista de especies básicas.
    public static IReadOnlyList<PokemonSpecies> BasicSpecies => Basics;

    // Todas las especies normales (sin megas ni legendarios), ordenadas por id.
    public static IEnumerable<PokemonSpecies> AllSpecies =>
        Species.Values.Where(s => !s.IsMega && !LegendaryIds.Contains(s.Id)).OrderBy(s => s.Id);

    // true si la especie es legendaria.
    public static bool IsLegendary(int id) => LegendaryIds.Contains(id);

    // Todas las especies legendarias ordenadas por id.
    public static IEnumerable<PokemonSpecies> Legendaries =>
        LegendaryIds.Select(id => Species[id]).OrderBy(s => s.Id);

    // Indica si alguna especie de la línea evolutiva (la básica y todas sus evoluciones) tiene ese tipo.
    public static bool LineHasType(int basicId, string type)
    {
        var pending = new Queue<int>();
        pending.Enqueue(basicId);

        while (pending.Count > 0)
        {
            PokemonSpecies sp = Get(pending.Dequeue());

            if (sp.Types.Contains(type))
                return true;

            foreach (EvolutionInfo evo in sp.Evolutions)
                pending.Enqueue(evo.ToId);
        }

        return false;
    }


    // Devuelve la lista de movimientos de una especie sin duplicados y ordenada por nivel. Si no tiene ataque temprano, añade Placaje.
    public static List<LearnEntry> GetLearnset(int id)
    {
        if (LearnsetCache.TryGetValue(id, out List<LearnEntry>? cached))
            return cached;

        var result = new List<LearnEntry>();

        foreach (LearnEntry entry in Get(id).OwnMoves.OrderBy(e => e.Level))
        {
            if (!result.Any(r => r.Move == entry.Move))
                result.Add(entry);
        }

        bool hasEarlyAttack = result.Any(e =>
            !e.Machine && e.Level <= 10 && MoveCatalog.Create(e.Move) is { IsStatus: false });

        if (!hasEarlyAttack && !result.Any(r => r.Move == "Placaje") && MoveCatalog.Exists("Placaje"))
            result.Insert(0, new LearnEntry { Level = 1, Move = "Placaje" });

        LearnsetCache[id] = result;
        return result;
    }

    // Movimientos de MT / tutor disponibles para la especie a ese nivel.
    public static List<string> GetTutorMoves(int id, int level)
    {
        return GetLearnset(id)
            .Where(e => e.Machine && e.Level <= level)
            .Select(e => e.Move)
            .ToList();
    }

    // Potencia "real" de un movimiento para puntuarlo: multiplica por los golpes medios y penaliza recarga y autodestrucción.
    private static double EffectivePower(PokemonMove move)
    {
        MoveEffects fx = move.Effects;

        double power = move.Power;

        if (fx.MultiMax > 1)
            power *= (fx.MultiMin + fx.MultiMax) / 2.0;

        if (fx.Fixed > 0 || fx.FixedLevel)
            power = 60;

        if (fx.Recharge)
            power *= 0.6;

        if (fx.SelfKo)
            power *= 0.3;

        return power;
    }

    // Puntúa lo útil que es un movimiento de estado (curación, estados alterados y mejoras propias valen más).
    private static double StatusUtility(PokemonMove move)
    {
        MoveEffects fx = move.Effects;
        double best = 20;

        if (fx.Heal > 0) best = Math.Max(best, 70);
        if (fx.Rest) best = Math.Max(best, 55);

        foreach (StatusEffect s in fx.Statuses)
        {
            if (s.Status == "slp" || s.Status == "par" || s.Status == "tox" || s.Status == "brn")
                best = Math.Max(best, 60);
        }

        foreach (StatChange c in fx.Stats)
        {
            if (c.OnUser && c.Delta >= 2 && (c.Stat == "atk" || c.Stat == "spa"))
                best = Math.Max(best, 85);
            else if (c.OnUser && c.Delta > 0)
                best = Math.Max(best, 50);
        }

        return best * (Math.Min(move.Accuracy, 100) / 100.0);
    }

    // Elige los 4 mejores movimientos de una especie para su nivel: prioriza ataques potentes de su tipo y estadística alta, evita repetir tipo más de 2 veces y añade un movimiento de estado útil a partir de nivel 20.
    public static List<string> GetBestMoves(int id, int level)
    {
        PokemonSpecies sp = Get(id);

        List<PokemonMove> candidates = GetLearnset(id)
            .Where(e => e.Level <= level)
            .Select(e => MoveCatalog.Create(e.Move))
            .Where(m => m != null)
            .Select(m => m!)
            .ToList();

        var damaging = candidates
            .Where(m => !m.IsStatus)
            .Select(m => new
            {
                Move = m,
                Score = EffectivePower(m)
                    * (Math.Min(m.Accuracy, 100) / 100.0)
                    * (sp.Types.Contains(m.Type) ? 1.5 : 1.0)
                    * (m.IsSpecial ? sp.SpAttack : sp.Attack)
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        var chosen = new List<PokemonMove>();

        foreach (var c in damaging)
        {
            if (chosen.Count >= 3)
                break;

            if (chosen.Count(m => m.Type == c.Move.Type) >= 2)
                continue;

            chosen.Add(c.Move);
        }

        foreach (var c in damaging)
        {
            if (chosen.Count >= 3)
                break;

            if (!chosen.Any(m => m.Name == c.Move.Name))
                chosen.Add(c.Move);
        }

        PokemonMove? status = candidates
            .Where(m => m.IsStatus)
            .OrderByDescending(StatusUtility)
            .FirstOrDefault();

        if (chosen.Count >= 3 && status != null && StatusUtility(status) >= 50 && level >= 20)
        {
            chosen.Add(status);
        }
        else
        {
            foreach (var c in damaging)
            {
                if (chosen.Count >= 4)
                    break;

                if (!chosen.Any(m => m.Name == c.Move.Name))
                    chosen.Add(c.Move);
            }
        }

        if (chosen.Count == 0)
        {
            PokemonMove? tackle = MoveCatalog.Create("Placaje");

            if (tackle != null)
                chosen.Add(tackle);
        }

        return chosen.Select(m => m.Name).ToList();
    }

    // Movimientos con los que nace un Pokémon de ese nivel (los mejores disponibles).
    public static List<string> GetMovesAtLevel(int id, int level)
    {
        return GetBestMoves(id, level);
    }

    // Movimientos que se aprenden exactamente al llegar a ese nivel.
    public static List<string> GetMovesLearnedAt(int id, int level)
    {
        return GetLearnset(id)
            .Where(e => !e.Machine && e.Level == level && level > 1)
            .Select(e => e.Move)
            .ToList();
    }

    // Movimientos nuevos que gana al evolucionar (máximo los 2 últimos) y que su forma anterior no tenía.
    public static List<string> GetMovesGainedByEvolution(int oldId, int newId, int level)
    {
        HashSet<string> oldMoves = GetLearnset(oldId).Select(e => e.Move).ToHashSet();

        List<string> gained = GetLearnset(newId)
            .Where(e => !e.Machine && e.Level <= level && !oldMoves.Contains(e.Move))
            .Select(e => e.Move)
            .ToList();

        return gained.Skip(Math.Max(0, gained.Count - 2)).ToList();
    }
}
