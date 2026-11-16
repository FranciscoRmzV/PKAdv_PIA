using System;
using System.Collections.Generic;
using System.Linq;

namespace PKPrueba1;

// Tabla de tipos: nombres, efectividades y qué tipos son especiales.
public static class TypeChart
{
    // Los 18 tipos con su abreviatura de 3 letras.
    public static readonly string[] AllTypes =
    {
        "NOR", "FUE", "AGU", "PLA", "ELE", "HIE", "LUC", "VEN", "TIE",
        "VOL", "PSI", "BIC", "ROC", "FAN", "DRA", "SIN", "ACE", "HAD"
    };

    // Nombre completo en español de cada tipo.
    private static readonly Dictionary<string, string> Names = new()
    {
        { "NOR", "Normal" }, { "FUE", "Fuego" }, { "AGU", "Agua" }, { "PLA", "Planta" },
        { "ELE", "Eléctrico" }, { "HIE", "Hielo" }, { "LUC", "Lucha" }, { "VEN", "Veneno" },
        { "TIE", "Tierra" }, { "VOL", "Volador" }, { "PSI", "Psíquico" }, { "BIC", "Bicho" },
        { "ROC", "Roca" }, { "FAN", "Fantasma" }, { "DRA", "Dragón" }, { "SIN", "Siniestro" },
        { "ACE", "Acero" }, { "HAD", "Hada" }
    };

    // Tipos cuyos movimientos son especiales (sistema clásico de las primeras generaciones).
    private static readonly HashSet<string> Special = new()
    {
        "FUE", "AGU", "PLA", "ELE", "PSI", "HIE", "DRA", "SIN"
    };

    // Tipos a los que cada tipo de ataque hace el doble de daño (supereficaz).
    private static readonly Dictionary<string, string[]> Strong = new()
    {
        { "NOR", new string[0] },
        { "FUE", new[] { "PLA", "HIE", "BIC", "ACE" } },
        { "AGU", new[] { "FUE", "TIE", "ROC" } },
        { "ELE", new[] { "AGU", "VOL" } },
        { "PLA", new[] { "AGU", "TIE", "ROC" } },
        { "HIE", new[] { "PLA", "TIE", "VOL", "DRA" } },
        { "LUC", new[] { "NOR", "HIE", "ROC", "SIN", "ACE" } },
        { "VEN", new[] { "PLA", "HAD" } },
        { "TIE", new[] { "FUE", "ELE", "VEN", "ROC", "ACE" } },
        { "VOL", new[] { "PLA", "LUC", "BIC" } },
        { "PSI", new[] { "LUC", "VEN" } },
        { "BIC", new[] { "PLA", "PSI", "SIN" } },
        { "ROC", new[] { "FUE", "HIE", "VOL", "BIC" } },
        { "FAN", new[] { "PSI", "FAN" } },
        { "DRA", new[] { "DRA" } },
        { "SIN", new[] { "PSI", "FAN" } },
        { "ACE", new[] { "HIE", "ROC", "HAD" } },
        { "HAD", new[] { "LUC", "DRA", "SIN" } }
    };

    // Tipos a los que cada tipo de ataque hace la mitad de daño (poco eficaz).
    private static readonly Dictionary<string, string[]> Weak = new()
    {
        { "NOR", new[] { "ROC", "ACE" } },
        { "FUE", new[] { "FUE", "AGU", "ROC", "DRA" } },
        { "AGU", new[] { "AGU", "PLA", "DRA" } },
        { "ELE", new[] { "ELE", "PLA", "DRA" } },
        { "PLA", new[] { "FUE", "PLA", "VEN", "VOL", "BIC", "DRA", "ACE" } },
        { "HIE", new[] { "FUE", "AGU", "HIE", "ACE" } },
        { "LUC", new[] { "VEN", "VOL", "PSI", "BIC", "HAD" } },
        { "VEN", new[] { "VEN", "TIE", "ROC", "FAN" } },
        { "TIE", new[] { "PLA", "BIC" } },
        { "VOL", new[] { "ELE", "ROC", "ACE" } },
        { "PSI", new[] { "PSI", "ACE" } },
        { "BIC", new[] { "FUE", "LUC", "VEN", "VOL", "FAN", "ACE", "HAD" } },
        { "ROC", new[] { "LUC", "TIE", "ACE" } },
        { "FAN", new[] { "SIN" } },
        { "DRA", new[] { "ACE" } },
        { "SIN", new[] { "LUC", "SIN", "HAD" } },
        { "ACE", new[] { "FUE", "AGU", "ELE", "ACE" } },
        { "HAD", new[] { "FUE", "VEN", "ACE" } }
    };

    // Tipos a los que cada tipo de ataque no afecta (daño 0).
    private static readonly Dictionary<string, string[]> Immune = new()
    {
        { "NOR", new[] { "FAN" } },
        { "ELE", new[] { "TIE" } },
        { "LUC", new[] { "FAN" } },
        { "VEN", new[] { "ACE" } },
        { "TIE", new[] { "VOL" } },
        { "PSI", new[] { "SIN" } },
        { "FAN", new[] { "NOR" } },
        { "DRA", new[] { "HAD" } }
    };

    // Devuelve el nombre completo del tipo (o la abreviatura si no se conoce).
    public static string DisplayName(string type)
    {
        return Names.TryGetValue(type, out string? name) ? name : type;
    }

    // Une varios tipos en un texto como "Fuego / Volador".
    public static string DisplayList(IEnumerable<string> types)
    {
        return string.Join(" / ", types.Select(DisplayName));
    }

    // true si los movimientos de ese tipo son especiales.
    public static bool IsSpecial(string type) => Special.Contains(type);

    // Calcula el multiplicador total contra uno o dos tipos defensores: 0 si es inmune, x2 por cada tipo débil y x0.5 por cada tipo resistente.
    public static double Multiplier(string attackType, IEnumerable<string> defenderTypes)
    {
        double result = 1.0;

        foreach (string def in defenderTypes)
        {
            if (Immune.TryGetValue(attackType, out string[]? imm) && imm.Contains(def))
                return 0.0;

            if (Strong.TryGetValue(attackType, out string[]? str) && str.Contains(def))
                result *= 2.0;

            if (Weak.TryGetValue(attackType, out string[]? wk) && wk.Contains(def))
                result *= 0.5;
        }

        return result;
    }
}
