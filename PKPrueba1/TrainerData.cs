using System.Collections.Generic;

namespace PKPrueba1;

// Datos de un entrenador rival: nombre, equipo y modificadores de dificultad.
public class TrainerData
{
    // Nombre que se muestra del entrenador.
    public string Name { get; set; } = "Entrenador";
    // Indica si es un jefe (combates especiales con mejores recompensas).
    public bool IsBoss { get; set; }
    // Multiplicador del daño que hacen sus Pokémon (sube la dificultad).
    public double DamageMultiplier { get; set; } = 1.0;

    // Información del gimnasio al que pertenece (null si es un combate de la torre).
    public GymInfo? Gym { get; set; }
    // Equipo de Pokémon del entrenador.
    public List<PokemonData> Team { get; set; } = new();
}
