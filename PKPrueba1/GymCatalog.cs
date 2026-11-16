using System.Collections.Generic;

namespace PKPrueba1;

// Información de una etapa del modo Gimnasio (líder, Alto Mando o campeón).
public class GymInfo
{
    // Rol del rival: Líder, Teniente, Alto Mando o Campeón.
    public string Role { get; init; } = "Líder";
    // Nombre del líder / rival.
    public string Leader { get; init; } = "";
    // Nombre del gimnasio o sala donde se combate.
    public string GymName { get; init; } = "";
    // Tipo de Pokémon que usa (abreviatura de 3 letras, null para el campeón que usa varios).
    public string? Type { get; init; }
    // Medalla que se obtiene al ganar (null en el Alto Mando y el campeón).
    public string? Badge { get; init; }
    // Ruta de la imagen de fondo del combate.
    public string Background { get; init; } = "";

    // Título completo para mostrar en pantalla, por ejemplo "Líder Brock".
    public string Title => $"{Role} {Leader}";
}

// Catálogo con todas las etapas del modo Gimnasio, en orden.
public static class GymCatalog
{
    // Carpeta de imágenes de los gimnasios dentro de la app.
    private const string Assets = "ms-appx:///Assets/Gyms/";

    // Lista ordenada de etapas: 8 líderes, 4 del Alto Mando y el campeón.
    public static readonly List<GymInfo> Stages = new()
    {
        new GymInfo { Role = "Líder", Leader = "Brock", GymName = "Gimnasio de Ciudad Plateada", Type = "ROC", Badge = "Medalla Roca", Background = Assets + "brock.png" },
        new GymInfo { Role = "Líder", Leader = "Misty", GymName = "Gimnasio de Ciudad Celeste", Type = "AGU", Badge = "Medalla Cascada", Background = Assets + "misty.png" },
        new GymInfo { Role = "Teniente", Leader = "Surge", GymName = "Gimnasio de Ciudad Carmín", Type = "ELE", Badge = "Medalla Trueno", Background = Assets + "surge.png" },
        new GymInfo { Role = "Líder", Leader = "Erika", GymName = "Gimnasio de Ciudad Azulona", Type = "PLA", Badge = "Medalla Arcoíris", Background = Assets + "erika.png" },
        new GymInfo { Role = "Líder", Leader = "Koga", GymName = "Gimnasio de Ciudad Fucsia", Type = "VEN", Badge = "Medalla Alma", Background = Assets + "koga.png" },
        new GymInfo { Role = "Líder", Leader = "Sabrina", GymName = "Gimnasio de Ciudad Azafrán", Type = "PSI", Badge = "Medalla Pantano", Background = Assets + "sabrina.png" },
        new GymInfo { Role = "Líder", Leader = "Blaine", GymName = "Gimnasio de Isla Canela", Type = "FUE", Badge = "Medalla Volcán", Background = Assets + "blaine.png" },
        new GymInfo { Role = "Líder", Leader = "Giovanni", GymName = "Gimnasio de Ciudad Verde", Type = "TIE", Badge = "Medalla Tierra", Background = Assets + "giovanni.png" },
        new GymInfo { Role = "Alto Mando", Leader = "Lorelei", GymName = "Liga Pokémon · Sala de Lorelei", Type = "HIE", Background = Assets + "lorelei.png" },
        new GymInfo { Role = "Alto Mando", Leader = "Bruno", GymName = "Liga Pokémon · Sala de Bruno", Type = "LUC", Background = Assets + "bruno.png" },
        new GymInfo { Role = "Alto Mando", Leader = "Agatha", GymName = "Liga Pokémon · Sala de Agatha", Type = "FAN", Background = Assets + "agatha.png" },
        new GymInfo { Role = "Alto Mando", Leader = "Lance", GymName = "Liga Pokémon · Sala de Lance", Type = "DRA", Background = Assets + "lance.png" },
        new GymInfo { Role = "Campeón", Leader = "Azul", GymName = "Liga Pokémon · Sala del Campeón", Type = null, Background = Assets + "champion.png" }
    };
}
