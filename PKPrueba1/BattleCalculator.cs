using System;
using System.Linq;

namespace PKPrueba1;

// Resultado de un ataque: daño causado, efectividad del tipo y si fue golpe crítico.
public record DamageResult(int Damage, double Effectiveness, bool Critical);

// Calcula el daño de los movimientos siguiendo una fórmula parecida a la de los juegos de Pokémon.
public static class BattleCalculator
{
    // Generador aleatorio para críticos y variación del daño.
    private static readonly Random Rng = new();

    // Calcula la efectividad del tipo del movimiento contra el defensor (0, 0.5, 1, 2, 4...).
    public static double Effectiveness(PokemonData attacker, PokemonData defender, string moveType)
    {
        double result = TypeChart.Multiplier(moveType, defender.CurrentTypes);

        // Habilidad Intrépido (scrappy): los movimientos Normal y Lucha pueden golpear a tipos Fantasma.
        if (result <= 0.0 &&
            AbilityEffects.Of(attacker) == "scrappy" &&
            (moveType == "NOR" || moveType == "LUC") &&
            defender.CurrentTypes.Contains("FAN"))
        {
            result = TypeChart.Multiplier(
                moveType,
                defender.CurrentTypes.Where(t => t != "FAN").DefaultIfEmpty("NOR"));
        }

        return result;
    }

    // Calcula el daño de un movimiento teniendo en cuenta estadísticas, objetos, habilidades, clima, críticos, STAB y efectividad.
    public static DamageResult Calculate(
        PokemonData attacker,
        PokemonData defender,
        PokemonMove move,
        BattleBoosts? attackerBoosts,
        BattleBoosts? defenderBoosts,
        double damageMultiplier,
        string weather = "",
        double extraMultiplier = 1.0)
    {
        // Decide si se usan las estadísticas especiales o las físicas.
        bool special = move.IsSpecial;
        MoveEffects fx = move.Effects;

        // Ataque efectivo del atacante, con objetos y habilidades.
        double a = (special ? attacker.SpAttack : attacker.Attack)
            * HeldItems.AttackMultiplier(attacker, special)
            * AbilityEffects.AttackMultiplier(attacker, special, attackerBoosts, weather);

        // Defensa efectiva del defensor, con objetos y habilidades.
        double d = (special ? defender.SpDefense : defender.Defense)
            * HeldItems.DefenseMultiplier(defender, special)
            * AbilityEffects.DefenseMultiplier(defender, special, defenderBoosts, weather);

        // Tormenta de arena: los tipos Roca ganan +50% de Defensa Especial.
        if (weather == "sand" && special && defender.CurrentTypes.Contains("ROC"))
            d *= 1.5;

        // Algunas habilidades ignoran las mejoras de estadísticas del rival.
        bool ignoreDefenderStages = AbilityEffects.IgnoresTargetStages(attacker);
        bool ignoreAttackerStages = AbilityEffects.IgnoresTargetStages(defender);

        // Aplica los niveles de mejora del atacante y la reducción del ataque físico por quemadura.
        if (attackerBoosts != null)
        {
            if (!ignoreAttackerStages)
                a *= BattleBoosts.Mult(special ? attackerBoosts.SpA : attackerBoosts.Atk);

            if (!special && attackerBoosts.Status == "brn" && !AbilityEffects.IgnoresBurn(attacker))
                a *= 0.5;
        }

        // Aplica los niveles de mejora de la defensa del defensor.
        if (defenderBoosts != null && !ignoreDefenderStages)
            d *= BattleBoosts.Mult(special ? defenderBoosts.SpD : defenderBoosts.Def);

        // Si el defensor es inmune, el daño es 0.
        double effectiveness = Effectiveness(attacker, defender, move.Type);

        if (effectiveness <= 0.0)
            return new DamageResult(0, 0.0, false);

        // Nivel de probabilidad de crítico: sube con movimientos de crítico alto, Crítico X y habilidades.
        int critStage = (fx.HighCrit ? 1 : 0)
            + (attackerBoosts != null && attackerBoosts.Crit ? 2 : 0)
            + AbilityEffects.CritStageBonus(attacker);

        // Probabilidad de crítico según el nivel: 6.25%, 12.5%, 50% o 100%.
        double critChance = critStage switch
        {
            0 => 0.0625,
            1 => 0.125,
            2 => 0.5,
            _ => 1.0
        };

        // Tira el dado del crítico (algunas habilidades lo impiden).
        bool critical = !AbilityEffects.CritImmune(defender) && Rng.NextDouble() < critChance;

        // STAB: bonificación si el movimiento es del mismo tipo que el atacante.
        double stab = attacker.CurrentTypes.Contains(move.Type)
            ? AbilityEffects.StabMultiplier(attacker)
            : 1.0;

        // Potencia final del movimiento con los modificadores de habilidades.
        double power = move.Power * AbilityEffects.PowerMultiplier(attacker, move, fx, attackerBoosts, weather);

        // Fórmula base de daño según el nivel, la potencia y la relación ataque/defensa.
        double baseDamage =
            ((2.0 * attacker.Level / 5.0 + 2.0) * power * a / Math.Max(1.0, d)) / 50.0 + 2.0;

        // Clima: el sol potencia Fuego y debilita Agua; la lluvia hace lo contrario.
        double weatherMultiplier = 1.0;

        if (weather == "sun")
            weatherMultiplier = move.Type == "FUE" ? 1.5 : move.Type == "AGU" ? 0.5 : 1.0;
        else if (weather == "rain")
            weatherMultiplier = move.Type == "AGU" ? 1.5 : move.Type == "FUE" ? 0.5 : 1.0;

        // Daño final: se multiplican todos los modificadores (STAB, efectividad, clima, variación aleatoria 85-100%, crítico, objetos, habilidades y dificultad).
        double damage =
            baseDamage
            * stab
            * effectiveness
            * weatherMultiplier
            * (0.85 + Rng.NextDouble() * 0.15)
            * (critical ? AbilityEffects.CritMultiplier(attacker) : 1.0)
            * HeldItems.DamageMultiplier(attacker, move, effectiveness)
            * AbilityEffects.DefenderMultiplier(defender, move, effectiveness)
            * AbilityEffects.TintedLens(attacker, effectiveness)
            * extraMultiplier
            * damageMultiplier;

        // El daño mínimo es siempre 1.
        return new DamageResult(Math.Max(1, (int)damage), effectiveness, critical);
    }
}
