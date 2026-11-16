namespace PKPrueba1;

// Tipos de objeto del juego.
public enum ItemKind
{
    // Cura PS.
    Heal,
    // Revive a un Pokémon debilitado.
    Revive,
    // Sube una estadística durante el combate.
    Boost,

    // Cura estados alterados.
    Cure,

    // Objeto que el Pokémon lleva equipado.
    Held,

    // Megapiedra que permite la megaevolución.
    MegaStone
}

// Datos de un objeto del catálogo.
public class ItemData
{
    // Nombre del objeto.
    public string Name { get; set; } = "";
    // Descripción que se muestra al jugador.
    public string Description { get; set; } = "";
    // URL de la imagen del objeto.
    public string SpriteUrl { get; set; } = "";
    // Tipo de objeto.
    public ItemKind Kind { get; set; }

    // Cantidad numérica del efecto (PS que cura, etc.).
    public int Amount { get; set; }

    // Estadística que sube (Boost) o estado que cura (Cure; "all" = todos).
    public string Stat { get; set; } = "";

    // Efecto del objeto como texto, por ejemplo "restore" (cura total) o "type:FUE" (potencia un tipo).
    public string Effect { get; set; } = "";

    // Id del Pokémon base que puede usar esta megapiedra.
    public int MegaBaseId { get; set; }

    // Id de la forma mega en la que se transforma.
    public int MegaFormId { get; set; }

    // true si el objeto se equipa a un Pokémon en lugar de usarse directamente.
    public bool IsEquippable => Kind == ItemKind.Held || Kind == ItemKind.MegaStone;

    // Constructor vacío necesario para la deserialización JSON.
    public ItemData()
    {
    }

    // Constructor con los datos principales del objeto.
    public ItemData(
        string name,
        string description,
        string spriteUrl,
        ItemKind kind,
        int amount = 0,
        string stat = "")
    {
        Name = name;
        Description = description;
        SpriteUrl = spriteUrl;
        Kind = kind;
        Amount = amount;
        Stat = stat;
    }
}
