using System.Text.Json.Serialization;

namespace PKPrueba1;

// Un movimiento de un Pokémon con sus datos básicos de combate.
public class PokemonMove
{
    // Nombre del movimiento.
    public string Name { get; set; } = "";
    // Potencia base (0 en movimientos de estado).
    public int Power { get; set; }

    // Precisión en porcentaje.
    public int Accuracy { get; set; } = 100;
    // Tipo del movimiento (abreviatura de 3 letras, por defecto Normal).
    public string Type { get; set; } = "NOR";

    // Categoría: "P" físico, "S" especial, "T" estado; vacío = se deduce del tipo.
    public string Category { get; set; } = "";

    // Texto con los efectos secundarios del movimiento (se interpreta en MoveEffects).
    public string Effect { get; set; } = "";

    // Constructor vacío necesario para la deserialización JSON.
    public PokemonMove() { }

    // Constructor con todos los datos del movimiento.
    public PokemonMove(string name, int power, int accuracy = 100, string type = "NOR", string category = "", string effect = "")
    {
        Name = name;
        Power = power;
        Accuracy = accuracy;
        Type = type;
        Category = category;
        Effect = effect;
    }

    [JsonIgnore]
    // true si es un movimiento de estado (no hace daño directo).
    public bool IsStatus => Category == "T";

    [JsonIgnore]
    // true si el movimiento es especial (usa Atq. Esp. / Def. Esp.); si no tiene categoría se decide por su tipo.
    public bool IsSpecial => Category == "S" || (Category == "" && TypeChart.IsSpecial(Type));

    [JsonIgnore]
    // Efectos secundarios ya interpretados a partir del texto.
    public MoveEffects Effects => MoveEffects.Parse(Effect);

    // Crea una copia independiente del movimiento.
    public PokemonMove Clone() => new PokemonMove(Name, Power, Accuracy, Type, Category, Effect);
}
