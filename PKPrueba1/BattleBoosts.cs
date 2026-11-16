namespace PKPrueba1;

// Estado temporal de un Pokémon durante el combate: cambios de estadísticas, estado alterado y efectos de movimientos.
public class BattleBoosts
{
    // Niveles de mejora/penalización (de -6 a +6) de cada estadística.
    public int Atk { get; set; }
    public int Def { get; set; }
    public int SpA { get; set; }
    public int SpD { get; set; }
    public int Spe { get; set; }
    public int Acc { get; set; }
    public int Eva { get; set; }
    // true si tiene el golpe crítico potenciado (por ejemplo con Crítico X).
    public bool Crit { get; set; }

    // Estado alterado principal: "psn" veneno, "par" parálisis, "slp" sueño, "brn" quemadura, "frz" congelado (vacío = ninguno).
    public string Status { get; set; } = "";
    // Turnos restantes del estado alterado (por ejemplo, del sueño).
    public int StatusTurns { get; set; }
    // Contador del envenenamiento grave: el daño aumenta cada turno.
    public int ToxicCounter { get; set; }

    // Turnos de confusión restantes (0 = no está confuso).
    public int Confusion { get; set; }
    // true si retrocede y pierde el turno.
    public bool Flinch { get; set; }
    // true si debe descansar un turno tras un ataque de recarga (por ejemplo Hiper Rayo).
    public bool MustRecharge { get; set; }

    // true si está infectado por Drenadoras (pierde PS cada turno).
    public bool Seeded { get; set; }

    // Turnos que le quedan atrapado por un movimiento de atadura.
    public int TrapTurns { get; set; }

    // true si está enamorado (a veces no puede atacar).
    public bool Infatuated { get; set; }

    // Movimiento anulado temporalmente.
    public string DisabledMove { get; set; } = "";
    // Turnos que le quedan al movimiento anulado.
    public int DisabledTurns { get; set; }

    // Turnos restantes de la habilidad Inicio Lento (ataque y velocidad reducidos).
    public int SlowStartTurns { get; set; }

    // Indica que toca descansar por la habilidad Holgazán.
    public bool TruantRest { get; set; }

    // true si se ha protegido este turno.
    public bool Protected { get; set; }
    // Usos seguidos de protección (cada uso seguido tiene menos probabilidad de funcionar).
    public int ProtectStreak { get; set; }

    // Convierte un nivel de mejora de estadística (-6..+6) en multiplicador: 2/2, 3/2, 4/2... o 2/3, 2/4...
    public static double Mult(int stage)
    {
        return stage >= 0 ? (2.0 + stage) / 2.0 : 2.0 / (2.0 - stage);
    }

    // Igual que Mult pero para precisión y evasión (usa la base 3).
    public static double AccMult(int stage)
    {
        return stage >= 0 ? (3.0 + stage) / 3.0 : 3.0 / (3.0 - stage);
    }

    // Quita los efectos temporales (mejoras, confusión, retroceso, etc.), por ejemplo al cambiar de Pokémon. El estado alterado principal se conserva.
    public void ResetVolatile()
    {
        Atk = Def = SpA = SpD = Spe = Acc = Eva = 0;
        Crit = false;
        Confusion = 0;
        Flinch = false;
        MustRecharge = false;
        Seeded = false;
        Infatuated = false;
        DisabledMove = "";
        DisabledTurns = 0;
        TruantRest = false;
        TrapTurns = 0;
        Protected = false;
        ProtectStreak = 0;
    }
}
