using System;
using System.Linq;

namespace PKPrueba1;

// Efectos de las habilidades en combate. Cada método consulta la habilidad del Pokémon y devuelve el modificador que corresponde.
public static class AbilityEffects
{
    // Pokémon cuya habilidad está anulada temporalmente.
    public static PokemonData? Suppressed { get; set; }

    // true si Gas Reactivo está activo: anula todas las habilidades salvo la propia.
    public static bool GasActive { get; set; }

    // Devuelve la habilidad activa del Pokémon (vacía si no tiene, está anulada o hay Gas Reactivo).
    public static string Of(PokemonData? p)
    {
        if (p == null)
            return "";

        string a = p.BattleAbility ?? p.Ability ?? "";

        if (ReferenceEquals(p, Suppressed))
            return "";

        if (GasActive && a != "neutralizing-gas")
            return "";

        return a;
    }

    // true si la habilidad ignora las habilidades del rival (Rompemoldes, Terravoltaje, Turbollama).
    public static bool IgnoresAbilities(PokemonData p)
    {
        string a = Of(p);

        return a == "mold-breaker" || a == "teravolt" || a == "turboblaze";
    }

    // true si algún Pokémon anula los efectos del clima (Nubosidad / Esclusa de Aire).
    public static bool CloudNine(PokemonData? a, PokemonData? b)
    {
        string x = Of(a);
        string y = Of(b);

        return x == "cloud-nine" || x == "air-lock" || y == "cloud-nine" || y == "air-lock";
    }


    // Multiplicador del ataque por habilidades: Potencia/Potencia Pura (x2), Agallas, Ímpetu, Don Floral y Poder Solar (x1.5); Inicio Lento lo reduce a la mitad.
    public static double AttackMultiplier(PokemonData p, bool special, BattleBoosts? state, string weather)
    {
        string a = Of(p);
        double result = 1.0;
        string status = state?.Status ?? "";

        if (!special)
        {
            if (a == "huge-power" || a == "pure-power") result *= 2.0;
            if (a == "guts" && status != "") result *= 1.5;
            if (a == "hustle") result *= 1.5;
            if (a == "flower-gift" && weather == "sun") result *= 1.5;
            if (a == "slow-start" && (state?.SlowStartTurns ?? 0) > 0) result *= 0.5;
        }
        else
        {
            if (a == "solar-power" && weather == "sun") result *= 1.5;
        }

        return result;
    }

    // Multiplicador de la defensa por habilidades: Escama Especial (con estado alterado) y Don Floral (con sol).
    public static double DefenseMultiplier(PokemonData p, bool special, BattleBoosts? state, string weather)
    {
        string a = Of(p);
        string status = state?.Status ?? "";

        if (!special && a == "marvel-scale" && status != "") return 1.5;
        if (special && a == "flower-gift" && weather == "sun") return 1.5;

        return 1.0;
    }

    // Agallas: la quemadura no reduce el ataque.
    public static bool IgnoresBurn(PokemonData p) => Of(p) == "guts";

    // true si el movimiento tiene efectos secundarios probabilísticos (los potencia Fuerza Bruta).
    public static bool HasSecondary(MoveEffects fx)
    {
        return fx.Statuses.Any(s => s.Chance < 100) || fx.Stats.Any(c => c.Chance < 100);
    }

    // Multiplicador de la potencia del movimiento por habilidades: Experto, Puño de Hierro, Audaz, Fuerza Bruta, Mandíbula Fuerte, etc.
    public static double PowerMultiplier(
        PokemonData p,
        PokemonMove move,
        MoveEffects fx,
        BattleBoosts? state,
        string weather)
    {
        string a = Of(p);
        string status = state?.Status ?? "";
        double result = 1.0;

        if (a == "technician" && move.Power > 0 && move.Power <= 60) result *= 1.5;
        if (a == "iron-fist" && fx.Punch) result *= 1.2;
        if (a == "reckless" && fx.Recoil > 0) result *= 1.2;
        if (a == "sheer-force" && HasSecondary(fx)) result *= 1.3;
        if (a == "normalize") result *= 1.2;
        if (a == "toxic-boost" && !move.IsSpecial && (status == "psn" || status == "tox")) result *= 1.5;
        if (a == "flare-boost" && move.IsSpecial && status == "brn") result *= 1.5;
        if (a == "strong-jaw" && fx.Bite) result *= 1.5;
        if (a == "mega-launcher" && fx.Pulse) result *= 1.5;
        if (a == "sharpness" && fx.Slice) result *= 1.5;

        if (a == "sand-force" && weather == "sand" &&
            (move.Type == "ROC" || move.Type == "TIE" || move.Type == "ACE"))
        {
            result *= 1.3;
        }

        // Habilidades de "apuro" (Espesura, Mar Llamas, Torrente, Enjambre): potencian x1.5 su tipo cuando quedan 1/3 o menos de los PS.
        bool pinch = p.MaxHP > 0 && p.CurrentHP * 3 <= p.MaxHP;

        if (pinch &&
            ((a == "overgrow" && move.Type == "PLA") ||
             (a == "blaze" && move.Type == "FUE") ||
             (a == "torrent" && move.Type == "AGU") ||
             (a == "swarm" && move.Type == "BIC")))
        {
            result *= 1.5;
        }

        return result;
    }

    // Bonificación de tipo propio (STAB): x1.5, o x2 con Adaptabilidad.
    public static double StabMultiplier(PokemonData p)
    {
        return Of(p) == "adaptability" ? 2.0 : 1.5;
    }

    // Reducción de daño del defensor: Sebo, Ignífugo, Piel Seca, Roca Sólida/Filtro, Multiescamas...
    public static double DefenderMultiplier(PokemonData defender, PokemonMove move, double effectiveness)
    {
        string a = Of(defender);
        double result = 1.0;

        if (a == "thick-fat" && (move.Type == "FUE" || move.Type == "HIE")) result *= 0.5;
        if (a == "heatproof" && move.Type == "FUE") result *= 0.5;
        if (a == "dry-skin" && move.Type == "FUE") result *= 1.25;
        if ((a == "solid-rock" || a == "filter" || a == "prism-armor") && effectiveness > 1.0) result *= 0.75;
        if ((a == "multiscale" || a == "shadow-shield") && defender.CurrentHP >= defender.MaxHP) result *= 0.5;

        return result;
    }

    // Cromolente: duplica el daño de ataques poco eficaces.
    public static double TintedLens(PokemonData attacker, double effectiveness)
    {
        return Of(attacker) == "tinted-lens" && effectiveness < 1.0 ? 2.0 : 1.0;
    }

    // Superguardia: solo permite ataques supereficaces.
    public static bool WonderGuardBlocks(PokemonData defender, double effectiveness)
    {
        return Of(defender) == "wonder-guard" && effectiveness <= 1.0;
    }

    // true si el defensor es inmune a críticos (Armadura Batalla, Armadura Caparazón).
    public static bool CritImmune(PokemonData defender)
    {
        string a = Of(defender);

        return a == "battle-armor" || a == "shell-armor";
    }

    // Afortunado: sube un nivel la probabilidad de crítico.
    public static int CritStageBonus(PokemonData attacker) => Of(attacker) == "super-luck" ? 1 : 0;

    // Daño del crítico: x1.5, o x2.25 con Francotirador.
    public static double CritMultiplier(PokemonData attacker) => Of(attacker) == "sniper" ? 2.25 : 1.5;

    // Ajeno: ignora los cambios de estadísticas del rival.
    public static bool IgnoresTargetStages(PokemonData attacker) => Of(attacker) == "unaware";

    // Habilidades que anulan o absorben un ataque. Devuelve "immune" (sin efecto), "heal" (cura 25%), "stat:xxx+1" (sube una estadística) o null.
    public static string? Absorb(PokemonData defender, PokemonMove move)
    {
        string a = Of(defender);
        MoveEffects fx = move.Effects;

        if (a == "soundproof" && fx.Sound) return "immune";
        if (a == "overcoat" && fx.Powder) return "immune";
        if (a == "bulletproof" && fx.Ball) return "immune";
        if (a == "wind-rider" && fx.Wind) return "stat:atk+1";

        return (a, move.Type) switch
        {
            ("levitate", "TIE") => "immune",
            ("flash-fire", "FUE") => "immune",
            ("water-absorb", "AGU") => "heal",
            ("dry-skin", "AGU") => "heal",
            ("volt-absorb", "ELE") => "heal",
            ("lightning-rod", "ELE") => "stat:spa+1",
            ("storm-drain", "AGU") => "stat:spa+1",
            ("sap-sipper", "PLA") => "stat:atk+1",
            ("motor-drive", "ELE") => "stat:spe+1",
            _ => null
        };
    }


    // Multiplicador de velocidad: habilidades de clima (Nado Rápido, Clorofila, Poder Arena, Quitanieves), Pies Rápidos, Inicio Lento y Liviano.
    public static double SpeedMultiplier(PokemonData p, string weather, BattleBoosts? state, bool itemConsumed)
    {
        string a = Of(p);
        double result = 1.0;

        if (a == "swift-swim" && weather == "rain") result *= 2.0;
        if (a == "chlorophyll" && weather == "sun") result *= 2.0;
        if (a == "sand-rush" && weather == "sand") result *= 2.0;
        if (a == "slush-rush" && weather == "hail") result *= 2.0;
        if (a == "quick-feet" && (state?.Status ?? "") != "") result *= 1.5;
        if (a == "slow-start" && (state?.SlowStartTurns ?? 0) > 0) result *= 0.5;
        if (a == "unburden" && itemConsumed) result *= 2.0;

        return result;
    }

    // Pies Rápidos: la parálisis no reduce la velocidad.
    public static bool IgnoresParalysisSpeed(PokemonData p) => Of(p) == "quick-feet";

    // Bromista: +1 de prioridad a los movimientos de estado.
    public static int PriorityBonus(PokemonData p, PokemonMove move)
    {
        return Of(p) == "prankster" && move.IsStatus ? 1 : 0;
    }

    // Rezagado: siempre ataca el último.
    public static bool MovesLast(PokemonData p) => Of(p) == "stall";


    // Sin Guardia: los ataques de ambos Pokémon siempre aciertan.
    public static bool NoGuard(PokemonData a, PokemonData b) => Of(a) == "no-guard" || Of(b) == "no-guard";

    // Multiplicador de precisión del atacante: Ojo Compuesto (x1.3) y Ímpetu (x0.8 en físicos).
    public static double AttackerAccuracy(PokemonData p, PokemonMove move)
    {
        string a = Of(p);
        double result = 1.0;

        if (a == "compound-eyes") result *= 1.3;
        if (a == "hustle" && !move.IsSpecial && !move.IsStatus) result *= 0.8;

        return result;
    }

    // Multiplicador de evasión del defensor: Velo Arena, Manto Níveo y Tumbos.
    public static double DefenderEvasion(PokemonData p, string weather, BattleBoosts? state)
    {
        string a = Of(p);

        if (a == "sand-veil" && weather == "sand") return 0.8;
        if (a == "snow-cloak" && weather == "hail") return 0.8;
        if (a == "tangled-feet" && (state?.Confusion ?? 0) > 0) return 0.5;

        return 1.0;
    }


    // true si la habilidad impide recibir ese estado (Flexibilidad, Insomnio, Inmunidad, Velo Agua, Foco Interno...).
    public static bool PreventsStatus(PokemonData p, string status, string weather)
    {
        string a = Of(p);

        if (a == "leaf-guard" && weather == "sun" && status != "flc")
            return true;

        return (a, status) switch
        {
            ("limber", "par") => true,
            ("insomnia", "slp") => true,
            ("vital-spirit", "slp") => true,
            ("immunity", "psn") => true,
            ("immunity", "tox") => true,
            ("water-veil", "brn") => true,
            ("magma-armor", "frz") => true,
            ("own-tempo", "cnf") => true,
            ("oblivious", "inf") => true,
            ("inner-focus", "flc") => true,
            _ => false
        };
    }

    // true si la habilidad impide que le bajen esa estadística (Cuerpo Puro, Corte Fuerte, Ojo Agudo...).
    public static bool PreventsStatDrop(PokemonData p, string stat)
    {
        string a = Of(p);

        return a == "clear-body" || a == "white-smoke" || a == "full-metal-body" ||
               (a == "hyper-cutter" && stat == "atk") ||
               (a == "big-pecks" && stat == "def") ||
               (a == "keen-eye" && stat == "acc");
    }

    // Muro Mágico: no recibe daño indirecto.
    public static bool IsMagicGuard(PokemonData p) => Of(p) == "magic-guard";

    // Encadenado: los movimientos de varios golpes dan siempre el máximo de golpes.
    public static bool SkillLink(PokemonData p) => Of(p) == "skill-link";


    // true si la habilidad del rival (Trampa Arena, Imán, Sombra Trampa) impide al Pokémon cambiar. Los tipos Fantasma pueden huir siempre.
    public static bool CannotSwitch(PokemonData target, PokemonData? foe)
    {
        if (foe == null || target.CurrentTypes.Contains("FAN"))
            return false;

        return Of(foe) switch
        {
            "arena-trap" => !target.CurrentTypes.Contains("VOL") && Of(target) != "levitate",
            "magnet-pull" => target.CurrentTypes.Contains("ACE"),
            "shadow-tag" => Of(target) != "shadow-tag",
            _ => false
        };
    }
}
