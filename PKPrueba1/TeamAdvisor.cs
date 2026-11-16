using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PKPrueba1;

// Respuesta del asistente de equipos: el texto a mostrar y, opcionalmente, un equipo recomendado (ids de especies).
public sealed class TeamAdvice
{
    public string Text { get; init; } = "";
    // Equipo recomendado (null si la respuesta no incluye ninguno).
    public List<int>? TeamIds { get; init; }
}

// Asistente de equipos para el modo Gimnasios. Es un sistema basado en reglas (no usa IA externa): entiende preguntas en español por palabras clave y calcula qué Pokémon rinden mejor contra cada líder.
public static class TeamAdvisor
{
    // Cerrojo para que la construcción de datos solo ocurra una vez aunque haya varias consultas a la vez.
    private static readonly object Gate = new();
    // Conjunto de candidatos evaluados (null hasta que se construye la primera vez).
    private static List<Candidate>? pool;
    // Valor de referencia por tipo (percentil 95) para normalizar las puntuaciones entre 0 y ~1.
    private static double[] typeScale = Array.Empty<double>();
    // Nombres de especies normalizados (sin tildes ni mayúsculas) para detectarlos en las preguntas, de más largo a más corto.
    private static List<(string Name, PokemonSpecies Species)> speciesNames = new();

    // Constantes de apoyo: número de tipos, tipos como listas de uno, número de etapas, lista de todas las etapas y tamaño del equipo.
    private static readonly int TypeCount = TypeChart.AllTypes.Length;
    private static readonly string[][] SingleType = TypeChart.AllTypes.Select(t => new[] { t }).ToArray();
    private static readonly int StageCount = GymCatalog.Stages.Count;
    private static readonly List<int> AllStages = Enumerable.Range(0, StageCount).ToList();

    private const int TeamSize = 6;


    // Un movimiento con su valor ofensivo calculado para un Pokémon concreto.
    private sealed record MoveValue(PokemonMove Move, double Value);

    // Un candidato a formar parte del equipo: una especie (o su forma mega con piedra) con sus puntuaciones precalculadas.
    private sealed class Candidate
    {
        // Especie base.
        public PokemonSpecies Base = null!;
        // Forma que se usa en combate (la misma que la base o su megaevolución).
        public PokemonSpecies Form = null!;
        // Megapiedra necesaria (null si no es una variante mega).
        public ItemData? Stone;

        // Movimientos ofensivos que puede aprender, con su valor.
        public List<MoveValue> Moves = new();
        // Resistencia global: combina PS y defensas.
        public double Bulk;
        // Pequeño factor de bonificación por velocidad (entre 0.85 y 1.15).
        public double SpeedFactor;

        // Multiplicador de daño que recibe de cada tipo (2 = débil, 0.5 = resistente, 0 = inmune).
        public double[] DefMult = new double[TypeChart.AllTypes.Length];
        // Rendimiento bruto contra un rival de cada tipo.
        public double[] Match = new double[TypeChart.AllTypes.Length];
        // Rendimiento contra cada tipo normalizado (máx. 1.3).
        public double[] Norm = new double[TypeChart.AllTypes.Length];
        // Rendimiento contra cada etapa del desafío (líder, Alto Mando, campeón).
        public double[] Stage = new double[GymCatalog.Stages.Count];

        // true si es una variante con megapiedra.
        public bool IsMega => Stone != null;
    }

    // Ámbito de la pregunta: qué etapas incluye, cómo nombrarlo y si es una sola etapa.
    private sealed record Scope(List<int> Stages, string Label, bool Single);

    // Construye los datos del asistente la primera vez que se necesitan (seguro entre hilos).
    private static void EnsureBuilt()
    {
        lock (Gate)
        {
            if (pool == null)
                Build();
        }
    }

    // Evalúa todas las especies finales (sin evolución, total base de 380 o más, sin Ditto) y sus megas, calcula las escalas por tipo, puntúa cada etapa y prepara la lista de nombres.
    private static void Build()
    {
        var result = new List<Candidate>();
        var moveCache = new Dictionary<string, PokemonMove?>();
        List<ItemData> stones = ItemCatalog.All.Where(i => i.Kind == ItemKind.MegaStone).ToList();

        foreach (PokemonSpecies sp in PokemonDatabase.AllSpecies)
        {
            if (sp.Evolutions.Count > 0 || sp.BaseTotal < 380 || sp.Id == 132)
                continue;

            List<PokemonMove> learn = LearnableDamaging(sp.Id, moveCache);

            result.Add(Make(sp, sp, null, learn));

            foreach (ItemData stone in stones.Where(s => s.MegaBaseId == sp.Id))
                result.Add(Make(sp, PokemonDatabase.Get(stone.MegaFormId), stone, learn));
        }

        // La escala de cada tipo es el percentil 95 del rendimiento de todos los candidatos.
        typeScale = new double[TypeCount];

        for (int t = 0; t < TypeCount; t++)
        {
            double[] values = result.Select(c => c.Match[t]).OrderBy(v => v).ToArray();
            double scale = values.Length == 0 ? 1 : values[(int)(0.95 * (values.Length - 1))];
            typeScale[t] = scale <= 0 ? 1 : scale;
        }

        foreach (Candidate c in result)
            Finish(c);

        speciesNames = PokemonDatabase.AllSpecies
            .Select(s => (Name: Normalize(s.Name), Species: s))
            .Where(x => x.Name.Length >= 3)
            .OrderByDescending(x => x.Name.Length)
            .ToList();

        pool = result;
    }

    // Movimientos de daño que la especie puede aprender hasta nivel 100 (sin los de estado ni los de daño fijo).
    private static List<PokemonMove> LearnableDamaging(int speciesId, Dictionary<string, PokemonMove?> cache)
    {
        var list = new List<PokemonMove>();

        IEnumerable<string> names = PokemonDatabase.GetLearnset(speciesId)
            .Where(l => l.Level <= TrainerGenerator.GymLevel)
            .Select(l => l.Move)
            .Distinct();

        foreach (string name in names)
        {
            if (!cache.TryGetValue(name, out PokemonMove? move))
            {
                move = MoveCatalog.Create(name);
                cache[name] = move;
            }

            if (move == null || move.IsStatus || move.Power <= 0)
                continue;

            MoveEffects fx = move.Effects;

            if (fx.Fixed > 0 || fx.FixedLevel)
                continue;

            list.Add(move);
        }

        return list;
    }

    // Potencia efectiva de un movimiento: considera golpes múltiples y penaliza recarga y autodestrucción.
    private static double EffPower(PokemonMove move)
    {
        MoveEffects fx = move.Effects;
        double power = move.Power;

        if (fx.MultiMax > 1)
            power *= (fx.MultiMin + fx.MultiMax) / 2.0;

        if (fx.Recharge)
            power *= 0.6;

        if (fx.SelfKo)
            power *= 0.3;

        return power;
    }

    // Crea un candidato calculando resistencia, velocidad, valor de cada movimiento y su rendimiento contra cada tipo.
    private static Candidate Make(PokemonSpecies basic, PokemonSpecies form, ItemData? stone, List<PokemonMove> learn)
    {
        var c = new Candidate { Base = basic, Form = form, Stone = stone };
        List<string> types = form.Types;

        // Media armónica de las dos defensas: penaliza que una sea muy baja. Con los PS da la resistencia global.
        double harmonic = form.Defense * form.SpDefense / (double)Math.Max(1, form.Defense + form.SpDefense);
        c.Bulk = form.HP * harmonic / 100.0;
        c.SpeedFactor = 0.85 + 0.3 * Math.Min(form.Speed, 150) / 150.0;

        foreach (PokemonMove m in learn)
        {
            double stat = m.IsSpecial ? form.SpAttack : form.Attack;
            double accuracy = Math.Min(m.Accuracy, 100) / 100.0;
            double stab = types.Contains(m.Type) ? 1.5 : 1.0;

            c.Moves.Add(new MoveValue(m, EffPower(m) * accuracy * stab * stat / 100.0));
        }

        for (int t = 0; t < TypeCount; t++)
        {
            c.DefMult[t] = TypeChart.Multiplier(TypeChart.AllTypes[t], types);

            double best = 0;

            foreach (MoveValue mv in c.Moves)
                best = Math.Max(best, mv.Value * TypeChart.Multiplier(mv.Move.Type, SingleType[t]));

            // Rendimiento contra un tipo = mejor daño que puede hacerle x resistencia / lo débil que es a ese tipo x bonificación de velocidad.
            c.Match[t] = best * c.Bulk / Math.Max(0.25, c.DefMult[t]) * c.SpeedFactor;
        }

        return c;
    }

    // Normaliza las puntuaciones por tipo y calcula la puntuación contra cada etapa (el campeón, que usa todos los tipos, usa la media).
    private static void Finish(Candidate c)
    {
        for (int t = 0; t < TypeCount; t++)
            c.Norm[t] = Math.Min(1.3, c.Match[t] / typeScale[t]);

        for (int s = 0; s < StageCount; s++)
        {
            string? type = GymCatalog.Stages[s].Type;

            c.Stage[s] = type == null ? c.Norm.Average() : c.Norm[Array.IndexOf(TypeChart.AllTypes, type)];
        }
    }

    // Crea un candidato "a medida" para especies que no están en la lista (sin evolucionar o muy flojas).
    private static Candidate Adhoc(PokemonSpecies sp)
    {
        var cache = new Dictionary<string, PokemonMove?>();
        Candidate c = Make(sp, sp, null, LearnableDamaging(sp.Id, cache));
        Finish(c);
        return c;
    }

    // Devuelve las variantes de una especie (normal y megas); si no está en la lista, crea una a medida.
    private static List<Candidate> VariantsOf(PokemonSpecies sp)
    {
        List<Candidate> variants = pool!.Where(c => c.Base.Id == sp.Id).ToList();

        if (variants.Count == 0)
            variants.Add(Adhoc(sp));

        return variants;
    }


    // Rendimiento medio del candidato en las etapas indicadas.
    private static double Mean(Candidate c, IReadOnlyList<int> stages)
        => stages.Count == 0 ? 0 : stages.Average(s => c.Stage[s]);

    // Puntúa un equipo: en cada etapa cuenta al mejor Pokémon (y algo al segundo), más un pequeño extra por el rendimiento general de cada miembro.
    private static double TeamScore(List<Candidate> team, IReadOnlyList<int> stages)
    {
        double total = 0;

        foreach (int s in stages)
        {
            double first = 0, second = 0;

            foreach (Candidate c in team)
            {
                double v = c.Stage[s];

                if (v > first)
                {
                    second = first;
                    first = v;
                }
                else if (v > second)
                {
                    second = v;
                }
            }

            total += first + 0.35 * second;
        }

        foreach (Candidate c in team)
            total += 0.12 * stages.Sum(s => c.Stage[s]);

        return total;
    }

    // Construye un equipo de forma voraz: añade en cada paso el candidato que más sube la puntuación. No repite especies ni permite más de una megaevolución.
    private static List<Candidate> PickTeam(IReadOnlyList<int> stages, IEnumerable<Candidate>? seed = null, int size = TeamSize)
    {
        var team = seed?.ToList() ?? new List<Candidate>();

        while (team.Count < size)
        {
            Candidate? best = null;
            double bestScore = double.MinValue;

            foreach (Candidate c in pool!)
            {
                if (team.Any(t => t.Base.Id == c.Base.Id))
                    continue;

                if (c.IsMega && team.Any(t => t.IsMega))
                    continue;

                team.Add(c);
                double score = TeamScore(team, stages);
                team.RemoveAt(team.Count - 1);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }

            if (best == null)
                break;

            team.Add(best);
        }

        return team;
    }

    // Sugiere 4 movimientos que maximicen la cobertura, dando más peso a los tipos de los líderes de las etapas elegidas.
    private static List<string> SuggestMoves(Candidate c, IReadOnlyList<int> stages)
    {
        var weight = new double[TypeCount];
        Array.Fill(weight, 0.4);

        foreach (int s in stages)
        {
            string? type = GymCatalog.Stages[s].Type;

            if (type != null)
                weight[Array.IndexOf(TypeChart.AllTypes, type)] = 1.0;
        }

        var chosen = new List<MoveValue>();
        var cover = new double[TypeCount];

        for (int k = 0; k < 4; k++)
        {
            MoveValue? best = null;
            double bestGain = 0;

            foreach (MoveValue mv in c.Moves)
            {
                if (chosen.Any(x => x.Move.Name == mv.Move.Name))
                    continue;

                double gain = 0;

                for (int t = 0; t < TypeCount; t++)
                {
                    double v = mv.Value * TypeChart.Multiplier(mv.Move.Type, SingleType[t]);
                    gain += weight[t] * Math.Max(0, v - cover[t]);
                }

                if (gain > bestGain)
                {
                    bestGain = gain;
                    best = mv;
                }
            }

            if (best == null)
                break;

            chosen.Add(best);

            for (int t = 0; t < TypeCount; t++)
                cover[t] = Math.Max(cover[t], best.Value * TypeChart.Multiplier(best.Move.Type, SingleType[t]));
        }

        return chosen.Select(x => x.Move.Name).ToList();
    }


    // Nombre del líder de una etapa.
    private static string StageName(int s) => GymCatalog.Stages[s].Leader;

    // Convierte una puntuación en una valoración: excelente, muy buena, buena, justa o floja.
    private static string Rating(double v)
    {
        if (v >= 0.9) return "excelente";
        if (v >= 0.7) return "muy buena";
        if (v >= 0.5) return "buena";
        if (v >= 0.35) return "justa";
        return "floja";
    }

    // Nombre completo de un tipo.
    private static string TypeName(string code) => TypeChart.DisplayName(code);

    // Texto descriptivo de un miembro del equipo: nombre, tipos, contra quién destaca y movimientos sugeridos.
    private static string DescribeMember(Candidate c, List<Candidate> team, IReadOnlyList<int> stages, int number, bool single)
    {
        var sb = new StringBuilder();

        sb.Append($"{number}. {c.Base.Name}");

        if (c.Stone != null)
            sb.Append($" + {c.Stone.Name}");

        sb.Append($" · {TypeChart.DisplayList(c.Base.Types)}");

        if (c.Stone != null && !c.Form.Types.SequenceEqual(c.Base.Types))
            sb.Append($" (Mega: {TypeChart.DisplayList(c.Form.Types)})");

        sb.Append('\n');

        if (single)
        {
            sb.Append($"   Frente a {StageName(stages[0])}: {Rating(c.Stage[stages[0]])}.\n");
        }
        else
        {
            List<string> shines = stages
                .Where(s => c.Stage[s] >= 0.5 && team.All(o => o == c || o.Stage[s] <= c.Stage[s]))
                .OrderByDescending(s => c.Stage[s])
                .Take(4)
                .Select(StageName)
                .ToList();

            if (shines.Count == 0)
                shines = stages
                    .Where(s => c.Stage[s] >= 0.6)
                    .OrderByDescending(s => c.Stage[s])
                    .Take(3)
                    .Select(StageName)
                    .ToList();

            sb.Append(shines.Count > 0
                ? $"   Destaca contra: {string.Join(", ", shines)}.\n"
                : "   Apoyo general: aguanta bien y rinde de forma pareja.\n");
        }

        sb.Append($"   Movimientos: {string.Join(", ", SuggestMoves(c, stages))}.");

        return sb.ToString();
    }

    // Texto que describe el ámbito (desafío completo, 8 gimnasios, Liga o lista de líderes).
    private static string ScopeLabel(IReadOnlyList<int> stages)
    {
        if (stages.Count == StageCount) return "el desafío completo (8 gimnasios, Alto Mando y Campeón)";
        if (stages.SequenceEqual(Enumerable.Range(0, 8))) return "los 8 gimnasios";
        if (stages.SequenceEqual(Enumerable.Range(8, 5))) return "la Liga (Alto Mando y Campeón)";

        return string.Join(" y ", stages.Select(s => StageInfoName(s)));
    }

    // Nombre de una etapa con su tipo, por ejemplo "Misty (Agua)".
    private static string StageInfoName(int s)
    {
        GymInfo g = GymCatalog.Stages[s];
        return g.Type == null ? g.Leader : $"{g.Leader} ({TypeName(g.Type)})";
    }

    // Ficha de un líder: su tipo, qué tipos le hacen daño o lo resisten, a quién golpea fuerte y cuántos Pokémon lleva.
    private static string StageProfile(int stage)
    {
        GymInfo g = GymCatalog.Stages[stage];
        var sb = new StringBuilder();

        int teamSize = stage < 3 ? 4 : stage < 6 ? 5 : 6;
        double multiplier = 1.0 + stage * 0.02;

        sb.Append($"{g.Role} {g.Leader} · {g.GymName}\n");

        if (g.Type == null)
        {
            sb.Append("• Usa Pokémon de cualquier tipo, así que no hay una debilidad común: conviene un equipo equilibrado y fuerte.\n");
        }
        else
        {
            string type = g.Type;

            List<string> weak = TypeChart.AllTypes.Where(t => TypeChart.Multiplier(t, new[] { type }) > 1).Select(TypeName).ToList();
            List<string> resists = TypeChart.AllTypes.Where(t => { double m = TypeChart.Multiplier(t, new[] { type }); return m < 1; }).Select(TypeName).ToList();
            List<string> hitsHard = TypeChart.AllTypes.Where(t => TypeChart.Multiplier(type, new[] { t }) > 1).Select(TypeName).ToList();
            List<string> noDamage = TypeChart.AllTypes.Where(t => TypeChart.Multiplier(type, new[] { t }) == 0).Select(TypeName).ToList();

            sb.Append($"• Tipo {TypeName(type)}. Sus Pokémon son aleatorios con ese tipo, a nivel 100 y con sus mejores movimientos.\n");

            if (weak.Count > 0)
                sb.Append($"• Atácalo con: {string.Join(", ", weak)} (daño x2).\n");

            if (resists.Count > 0)
                sb.Append($"• Evita: {string.Join(", ", resists)} (poco eficaz).\n");

            if (hitsHard.Count > 0)
                sb.Append($"• Sus ataques {TypeName(type)} son superefectivos contra: {string.Join(", ", hitsHard)}.\n");

            if (noDamage.Count > 0)
                sb.Append($"• No te hace daño con su tipo si eres de tipo: {string.Join(", ", noDamage)}.\n");
        }

        sb.Append($"• Lleva {teamSize} Pokémon y su daño tiene un bonus de x{multiplier.ToString("0.00", CultureInfo.InvariantCulture)}.");

        return sb.ToString();
    }


    // Respuesta de solo texto, sin equipo recomendado.
    private static TeamAdvice Plain(string text) => new() { Text = text };

    // Recomienda un equipo de 6 para el ámbito indicado, explica cada miembro e indica los combates más exigentes.
    private static TeamAdvice Recommend(Scope scope)
    {
        List<int> stages = scope.Stages;
        List<Candidate> team = PickTeam(stages);

        List<Candidate> ordered = team
            .OrderBy(c => stages.OrderByDescending(s => c.Stage[s]).First())
            .ToList();

        var sb = new StringBuilder();

        if (scope.Single)
            sb.Append(StageProfile(stages[0])).Append("\n\n");

        sb.Append($"Equipo recomendado para {scope.Label}:\n\n");

        for (int i = 0; i < ordered.Count; i++)
            sb.Append(DescribeMember(ordered[i], ordered, stages, i + 1, scope.Single)).Append("\n\n");

        if (!scope.Single)
        {
            var best = new double[StageCount];

            foreach (int s in stages)
                best[s] = ordered.Max(c => c.Stage[s]);

            List<string> hard = stages.Where(s => best[s] < 0.55).OrderBy(s => best[s]).Take(3).Select(StageName).ToList();

            sb.Append(hard.Count == 0
                ? "Cobertura: tienes una buena respuesta para todos los combates.\n"
                : $"Combates más exigentes: {string.Join(", ", hard)}. Ahí guarda tus objetos de curación.\n");
        }

        if (ordered.Any(c => c.IsMega))
            sb.Append("Solo se puede megaevolucionar una vez por combate, por eso incluí una sola megapiedra (pulsa Z en combate).\n");

        sb.Append("Los movimientos son una guía de cobertura; en el desafío podrás cambiarlos.\n");
        sb.Append("Pulsa «Usar este equipo» para seleccionarlos.");

        return new TeamAdvice
        {
            Text = sb.ToString().TrimEnd(),
            TeamIds = ordered.Select(c => c.Base.Id).ToList()
        };
    }

    // Convierte los ids elegidos por el jugador en candidatos: usa la mejor variante no mega y, si una megapiedra mejora mucho, la usa en un solo Pokémon.
    private static List<Candidate> ResolveTeam(IReadOnlyList<int> ids)
    {
        var team = new List<Candidate>();

        foreach (int id in ids)
        {
            List<Candidate> variants = VariantsOf(PokemonDatabase.Get(id));
            team.Add(variants.Where(v => !v.IsMega).OrderByDescending(v => Mean(v, AllStages)).FirstOrDefault() ?? variants[0]);
        }

        Candidate? bestMega = null;
        int bestIndex = -1;
        double bestGain = 0;

        for (int i = 0; i < team.Count; i++)
        {
            foreach (Candidate mega in VariantsOf(PokemonDatabase.Get(ids[i])).Where(v => v.IsMega))
            {
                double gain = Mean(mega, AllStages) - Mean(team[i], AllStages);

                if (gain > bestGain)
                {
                    bestGain = gain;
                    bestMega = mega;
                    bestIndex = i;
                }
            }
        }

        if (bestMega != null)
            team[bestIndex] = bestMega;

        return team;
    }

    // Analiza el equipo actual: puntos fuertes de cada miembro, cobertura contra el desafío, debilidades compartidas y sugerencia para completarlo o mejorarlo con un cambio.
    private static TeamAdvice Analyze(IReadOnlyList<int> ids)
    {
        if (ids.Count == 0)
        {
            return Plain("Todavía no has elegido ningún Pokémon. Selecciónalos en la lista de abajo y vuelvo a analizarlos, " +
                "o pídeme que te recomiende un equipo.");
        }

        List<Candidate> team = ResolveTeam(ids);
        var sb = new StringBuilder();

        sb.Append($"Análisis de tu equipo ({team.Count}/{TeamSize}):\n");

        foreach (Candidate c in team)
        {
            sb.Append($"• {c.Base.Name} · {TypeChart.DisplayList(c.Base.Types)}");

            if (c.Stone != null)
                sb.Append($" (con {c.Stone.Name} sería {c.Form.Name})");

            List<string> shines = AllStages
                .Where(s => c.Stage[s] >= 0.6)
                .OrderByDescending(s => c.Stage[s])
                .Take(3)
                .Select(StageName)
                .ToList();

            sb.Append(shines.Count > 0 ? $" — destaca contra {string.Join(", ", shines)}" : " — rendimiento discreto");

            if (c.Base.Evolutions.Count > 0)
            {
                PokemonSpecies evo = PokemonDatabase.Get(c.Base.Evolutions.OrderByDescending(e => PokemonDatabase.Get(e.ToId).BaseTotal).First().ToId);
                sb.Append($". Ojo: no está evolucionado; {evo.Name} tiene mejores estadísticas");
            }
            else if (c.Base.BaseTotal < 380)
            {
                sb.Append(". Ojo: sus estadísticas base son bajas para nivel 100");
            }

            sb.Append('\n');
        }

        var best = new double[StageCount];

        foreach (int s in AllStages)
            best[s] = team.Max(c => c.Stage[s]);

        // Función auxiliar: une los nombres de varias etapas.
        string Names(IEnumerable<int> stages) => string.Join(", ", stages.Select(StageName));

        List<int> strong = AllStages.Where(s => best[s] >= 0.6).ToList();
        List<int> medium = AllStages.Where(s => best[s] >= 0.4 && best[s] < 0.6).ToList();
        List<int> weak = AllStages.Where(s => best[s] < 0.4).ToList();

        sb.Append("\nCobertura contra el desafío:\n");

        if (strong.Count > 0) sb.Append($"✔ Bien cubierto: {Names(strong)}.\n");
        if (medium.Count > 0) sb.Append($"~ Justo: {Names(medium)}.\n");
        if (weak.Count > 0) sb.Append($"✖ Flojo: {Names(weak)}.\n");

        var shared = new List<string>();

        for (int t = 0; t < TypeCount; t++)
        {
            int count = team.Count(c => c.DefMult[t] >= 2);

            if (count >= 3 || (team.Count >= 3 && count * 2 > team.Count))
                shared.Add($"{TypeName(TypeChart.AllTypes[t])} ({count} de {team.Count})");
        }

        if (shared.Count > 0)
            sb.Append($"\nDebilidades compartidas: {string.Join(", ", shared)}. Si el rival usa ese tipo, caerán varios seguidos.\n");

        // Equipo propuesto tras el análisis (si hay una mejora).
        List<int>? newTeam = null;

        // Equipo incompleto: sugiere los Pokémon que mejor lo completan.
        if (team.Count < TeamSize)
        {
            List<Candidate> completed = PickTeam(AllStages, team);
            List<Candidate> added = completed.Skip(team.Count).ToList();

            sb.Append("\nPara completar el equipo te sugiero: ");
            sb.Append(string.Join("; ", added.Select(a =>
            {
                List<string> helps = AllStages
                    .Where(s => best[s] < 0.6 && a.Stage[s] >= 0.6)
                    .OrderByDescending(s => a.Stage[s])
                    .Take(3)
                    .Select(StageName)
                    .ToList();

                string name = a.Stone != null ? $"{a.Base.Name} + {a.Stone.Name}" : a.Base.Name;
                return helps.Count > 0 ? $"{name} (ayuda contra {string.Join(", ", helps)})" : name;
            })));
            sb.Append(".\nPulsa «Usar este equipo» para añadirlos.");

            newTeam = completed.Select(c => c.Base.Id).ToList();
        }
        else
        {
            // Equipo completo: prueba cambiar cada miembro por cada candidato y guarda el cambio que más mejora la puntuación.
            double baseScore = TeamScore(team, AllStages);
            double bestScore = baseScore;
            Candidate? out1 = null, in1 = null;

            foreach (Candidate m in team)
            {
                List<Candidate> rest = team.Where(x => x != m).ToList();

                foreach (Candidate c in pool!)
                {
                    if (rest.Any(r => r.Base.Id == c.Base.Id))
                        continue;

                    if (c.IsMega && rest.Any(r => r.IsMega))
                        continue;

                    rest.Add(c);
                    double score = TeamScore(rest, AllStages);
                    rest.RemoveAt(rest.Count - 1);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        out1 = m;
                        in1 = c;
                    }
                }
            }

            // Solo se propone el cambio si la mejora es de más del 3%.
            if (out1 != null && in1 != null && (bestScore - baseScore) / baseScore > 0.03)
            {
                List<Candidate> improved = team.Where(x => x != out1).Append(in1).ToList();

                List<int> helped = AllStages
                    .Where(s => improved.Max(c => c.Stage[s]) >= best[s] + 0.15)
                    .OrderBy(s => best[s])
                    .Take(4)
                    .ToList();

                if (in1.Base.Id == out1.Base.Id)
                {
                    sb.Append($"\nMejora fácil: equipa a {in1.Base.Name} con su {in1.Stone?.Name} y megaevoluciónalo en combate (Z).");
                }
                else
                {
                    string inName = in1.Stone != null ? $"{in1.Base.Name} + {in1.Stone.Name}" : in1.Base.Name;

                    sb.Append($"\nSi quieres mejorarlo, cambiaría a {out1.Base.Name} por {inName}");
                    sb.Append(helped.Count > 0 ? $", que ayuda mucho contra {Names(helped)}." : ".");
                    sb.Append("\nPulsa «Usar este equipo» para aplicar el cambio.");

                    newTeam = improved.Select(c => c.Base.Id).ToList();
                }
            }
            else
            {
                sb.Append("\nTu equipo está bien equilibrado: no veo un cambio que lo mejore de forma clara.");
            }
        }

        return new TeamAdvice { Text = sb.ToString().TrimEnd(), TeamIds = newTeam };
    }

    // Informe de un Pokémon concreto: su posición frente al resto, evolución recomendada, a quién gana y de quién sufre, debilidades, mega y movimientos.
    private static TeamAdvice SpeciesReport(PokemonSpecies sp, Scope? scope)
    {
        List<int> stages = scope?.Stages ?? AllStages;
        string label = scope?.Label ?? "el desafío completo";

        List<Candidate> variants = VariantsOf(sp);
        Candidate best = variants.OrderByDescending(v => Mean(v, stages)).First();
        double value = Mean(best, stages);

        List<double> others = pool!
            .GroupBy(c => c.Base.Id)
            .Select(g => g.Max(c => Mean(c, stages)))
            .ToList();

        int rank = others.Count(v => v > value) + 1;
        double percentile = rank / (double)Math.Max(1, others.Count);

        // Valoración según el percentil: top 10%, 30%, 60% o inferior.
        string tier = percentile <= 0.10 ? "entre los mejores del juego"
            : percentile <= 0.30 ? "una opción muy buena"
            : percentile <= 0.60 ? "una opción correcta"
            : "una opción floja";

        var sb = new StringBuilder();

        sb.Append($"{sp.Name} · {TypeChart.DisplayList(sp.Types)} · total base {sp.BaseTotal}\n");
        sb.Append($"Para {label} es {tier} (puesto {rank} de {others.Count} formas finales).\n");

        if (sp.Evolutions.Count > 0)
        {
            PokemonSpecies evo = PokemonDatabase.Get(sp.Evolutions.OrderByDescending(e => PokemonDatabase.Get(e.ToId).BaseTotal).First().ToId);
            sb.Append($"Ojo: aquí se usa sin evolucionar. {evo.Name} tiene {evo.BaseTotal} de total base frente a {sp.BaseTotal}; mejor elige su evolución.\n");
        }
        else if (sp.BaseTotal < 380)
        {
            sb.Append("Sus estadísticas base son bajas para llegar a las últimas etapas del desafío.\n");
        }

        if (stages.Count > 1)
        {
            List<string> good = stages.OrderByDescending(s => best.Stage[s]).Take(3).Select(StageName).ToList();
            List<string> bad = stages.OrderBy(s => best.Stage[s]).Take(2).Select(StageName).ToList();

            sb.Append($"Brilla contra: {string.Join(", ", good)}.\n");
            sb.Append($"Sufre contra: {string.Join(", ", bad)}.\n");
        }
        else
        {
            sb.Append($"Frente a {StageName(stages[0])}: {Rating(best.Stage[stages[0]])}.\n");
        }

        List<string> weaknesses = Enumerable.Range(0, TypeCount)
            .Where(t => best.DefMult[t] >= 2)
            .Select(t => TypeName(TypeChart.AllTypes[t]))
            .ToList();

        if (weaknesses.Count > 0)
            sb.Append($"Débil a: {string.Join(", ", weaknesses)}.\n");

        Candidate? mega = variants.Where(v => v.IsMega).OrderByDescending(v => Mean(v, stages)).FirstOrDefault();

        if (mega != null)
        {
            sb.Append($"Con la {mega.Stone!.Name} pasa a {mega.Form.Name} ({TypeChart.DisplayList(mega.Form.Types)}, total {mega.Form.BaseTotal}).\n");
        }

        if (best.Moves.Count > 0)
            sb.Append($"Movimientos sugeridos: {string.Join(", ", SuggestMoves(best, stages))}.");

        return Plain(sb.ToString().TrimEnd());
    }

    // Respuesta de ayuda con lo que sabe hacer el asistente.
    private static TeamAdvice Help()
    {
        return Plain(
            "Puedo ayudarte con:\n" +
            "• Armar un equipo de 6 para el desafío completo, solo los 8 gimnasios o solo la Liga (Alto Mando y Campeón).\n" +
            "• Un equipo para un líder concreto, por ejemplo «equipo contra Misty» o «contra Lance».\n" +
            "• Analizar el equipo que ya elegiste y decirte qué le falta.\n" +
            "• Valorar un Pokémon concreto, por ejemplo «¿Garchomp sirve para la Liga?».\n" +
            "• Darte datos de un líder: tipos débiles y resistentes y tamaño de su equipo.\n\n" +
            "Cuando te recomiende un equipo, pulsa «Usar este equipo» para seleccionarlo.");
    }


    // Normaliza el texto: quita tildes y pasa a minúsculas para comparar sin importar acentos.
    private static string Normalize(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);

        foreach (char ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    // true si alguna palabra de la pregunta empieza por alguna de las raíces.
    private static bool Has(string[] tokens, params string[] roots)
        => tokens.Any(t => roots.Any(r => t.StartsWith(r, StringComparison.Ordinal)));

    // true si alguna palabra de la pregunta coincide exactamente con alguna de las indicadas.
    private static bool Is(string[] tokens, params string[] words)
        => tokens.Any(t => words.Contains(t));

    // Detecta lenguaje ofensivo con expresiones regulares que toleran signos o espacios entre letras.
    private static bool IsProfane(string normalized)
    {
        string[] roots = { "puto", "puta", "pendej", "mierd", "ching", "verga", "cabr", "pinche", "jod", "mamon", "culer", "maric", "hijueputa" };

        foreach (string word in roots)
        {
            string pattern = @"(?<!\p{L})" + string.Join(@"[\p{P}\p{Z}\p{S}_]*", word.Select(c => Regex.Escape(c.ToString()))) + @"\p{L}*(?!\p{L})";

            if (Regex.IsMatch(normalized, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return true;
        }

        return false;
    }

    // true si la palabra se refiere a ese tipo (admite plural y variaciones de género, por ejemplo "electrico/electricos").
    private static bool TypeWordMatches(string token, string typeName)
    {
        string name = Normalize(typeName);

        if (token == name || token == name + "s")
            return true;

        return name.Length >= 6 && token.StartsWith(name.Substring(0, name.Length - 1), StringComparison.Ordinal);
    }

    // Crea un ámbito de una sola etapa.
    private static Scope OneStage(int stage)
        => new(new List<int> { stage }, StageInfoName(stage), true);

    // Deduce de qué etapas habla la pregunta: un líder por nombre, uno o varios tipos, los gimnasios, la Liga, el campeón o todo el desafío. Devuelve null si no detecta nada.
    private static Scope? DetectScope(string[] tokens)
    {
        for (int i = 0; i < StageCount; i++)
        {
            GymInfo g = GymCatalog.Stages[i];

            if (g.Type != null && tokens.Contains(Normalize(g.Leader)))
                return OneStage(i);
        }

        // Solo se interpreta un tipo como objetivo si la frase lo indica ("contra", "para", "tipo"...).
        bool typeCue = Has(tokens, "contra", "vs", "frente", "ante", "vencer", "ganar", "derrot", "gana", "para", "tipo");

        if (typeCue)
        {
            var matched = new List<int>();

            for (int i = 0; i < StageCount; i++)
            {
                string? type = GymCatalog.Stages[i].Type;

                if (type != null && tokens.Any(t => TypeWordMatches(t, TypeName(type))))
                    matched.Add(i);
            }

            if (matched.Count == 1)
                return OneStage(matched[0]);

            if (matched.Count > 1)
                return new Scope(matched, ScopeLabel(matched), false);
        }

        bool gyms = Has(tokens, "gimnas", "lider", "medall");
        bool league = Has(tokens, "liga", "alto", "mando");
        bool everything = Has(tokens, "complet", "desafio", "entero") || Is(tokens, "todo", "toda", "todos", "todas");

        if (everything || (gyms && league))
            return new Scope(new List<int>(AllStages), ScopeLabel(AllStages), false);

        if (league)
        {
            List<int> stages = Enumerable.Range(8, 5).ToList();
            return new Scope(stages, ScopeLabel(stages), false);
        }

        if (gyms)
        {
            List<int> stages = Enumerable.Range(0, 8).ToList();
            return new Scope(stages, ScopeLabel(stages), false);
        }

        if (Has(tokens, "campeon"))
            return OneStage(StageCount - 1);

        return null;
    }

    // Busca nombres de Pokémon en la pregunta (máximo 2), sin solapar coincidencias.
    private static List<PokemonSpecies> FindSpecies(string normalized)
    {
        var found = new List<(PokemonSpecies Species, int Start, int End)>();

        foreach ((string name, PokemonSpecies sp) in speciesNames)
        {
            Match m = Regex.Match(normalized, @"(?<![\p{L}\p{N}])" + Regex.Escape(name) + @"(?![\p{L}\p{N}])");

            if (!m.Success)
                continue;

            int start = m.Index, end = m.Index + m.Length;

            if (found.Any(f => start < f.End && end > f.Start))
                continue;

            found.Add((sp, start, end));
        }

        return found.OrderBy(f => f.Start).Select(f => f.Species).Take(2).ToList();
    }

    // Raíces de palabras que indican que la pregunta trata de equipos, combates o Pokémon.
    private static readonly string[] RelevantRoots =
    {
        "equip", "pokemon", "gimnas", "liga", "lider", "campeon", "alto", "mando", "recomend", "sugier", "sugerenc",
        "analiz", "mejor", "tipo", "contra", "debil", "medall", "desafio", "mega", "mov", "objeto", "cobertur",
        "falta", "arma", "elig", "sirv", "util", "bueno", "fuerte", "rival", "combat", "batalla", "entrenador",
        "ayud", "consej", "estrateg", "vencer", "ganar", "derrot", "seleccion", "evolu", "resist", "stat"
    };

    // Punto de entrada: interpreta la pregunta y devuelve la respuesta. Orden: validar longitud y lenguaje, saludos, relevancia, ayuda, análisis del equipo, informe de Pokémon, ficha de líder y, por defecto, recomendación de equipo.
    public static TeamAdvice Answer(string question, IReadOnlyList<int> currentTeam)
    {
        EnsureBuilt();

        question = (question ?? "").Trim();

        if (question.Length == 0)
            return Plain("Escríbeme qué necesitas, por ejemplo: «recomiéndame un equipo para la Liga».");

        // Límite de longitud de la pregunta.
        if (question.Length > 240)
            return Plain("La pregunta es demasiado larga. Resúmela en 240 caracteres o menos y vuelve a intentarlo.");

        string normalized = Normalize(question);

        if (IsProfane(normalized))
            return Plain("Mantengamos el chat respetuoso. Puedo ayudarte a armar y analizar equipos para los gimnasios y la Liga.");

        // Divide la pregunta en palabras.
        string[] tokens = Regex.Split(normalized, @"[^\p{L}\p{N}]+").Where(t => t.Length > 0).ToArray();

        if (tokens.Length <= 4)
        {
            if (Is(tokens, "gracias", "grax", "thanks", "genial", "perfecto"))
                return Plain("¡De nada! Si quieres, puedo revisar el equipo que elijas o preparar otro. ¡Suerte en la Liga!");

            if (Is(tokens, "hola", "buenas", "hey", "ey", "holi"))
                return Plain("¡Hola! Puedo recomendarte un equipo para los gimnasios o la Liga, analizar el que ya elegiste " +
                    "o valorar un Pokémon concreto. Prueba con los botones de abajo.");
        }

        List<PokemonSpecies> mentioned = FindSpecies(normalized);
        Scope? scope = DetectScope(tokens);

        // Si la pregunta no tiene relación con el tema, se explica qué sabe hacer el asistente.
        bool relevant = mentioned.Count > 0 || scope != null || Has(tokens, RelevantRoots);

        if (!relevant)
        {
            return Plain("Solo puedo ayudarte con equipos para los gimnasios y la Liga. Pídeme, por ejemplo, " +
                "«un equipo para el Alto Mando», «analiza mi equipo» o «¿sirve Gengar contra Sabrina?».");
        }

        if (Has(tokens, "puedes", "ayud", "opciones", "comandos") && mentioned.Count == 0 && scope == null)
            return Help();

        // Pide analizar el equipo actual.
        bool analyze = Has(tokens, "analiz", "revis", "evalu", "falta", "cobertur", "opin")
            || (Is(tokens, "mi", "mis") && Has(tokens, "equipo", "seleccion", "pokemon"));

        if (analyze && mentioned.Count == 0)
            return Analyze(currentTeam);

        if (mentioned.Count > 0)
        {
            string text = string.Join("\n\n", mentioned.Select(sp => SpeciesReport(sp, scope).Text));
            return Plain(text);
        }

        // Pide información sobre un líder (en lugar de un equipo).
        bool info = Has(tokens, "info", "dato", "cuant", "quien", "descri", "detall", "debil")
            || (Is(tokens, "como") && Is(tokens, "es", "son"));
        bool wantsTeam = Has(tokens, "recomend", "sugier", "equipo", "pokemon", "mejor", "arma", "elig");

        if (scope != null && scope.Single && info && !wantsTeam)
            return Plain(StageProfile(scope.Stages[0]));

        return Recommend(scope ?? new Scope(new List<int>(AllStages), ScopeLabel(AllStages), false));
    }
}
