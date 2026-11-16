using System.Collections.Generic;
using System.Linq;

namespace PKPrueba1;

// Gestiona la mochila principal del jugador (lista de nombres de objetos).
public static class InventoryManager
{
    // Añade un objeto a la mochila y guarda la partida.
    public static void AddItem(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return;

        GameSession.Data.Inventory.Add(itemName);
        GameSession.Save();
    }

    // Indica si el jugador tiene al menos una unidad del objeto.
    public static bool HasItem(string itemName)
    {
        return GameSession.Data.Inventory
            .Any(x => x == itemName);
    }

    // Consume una unidad del objeto. Devuelve false si no lo tenía.
    public static bool UseItem(string itemName)
    {
        int index = GameSession.Data.Inventory
            .FindIndex(x => x == itemName);

        if (index < 0)
            return false;

        GameSession.Data.Inventory.RemoveAt(index);
        GameSession.Save();

        return true;
    }

    // Devuelve cuántas unidades del objeto hay en la mochila.
    public static int GetItemCount(string itemName)
    {
        return GameSession.Data.Inventory
            .Count(x => x == itemName);
    }

    // Devuelve cada objeto con su cantidad (agrupa los nombres repetidos).
    public static Dictionary<string, int> GetAllItems()
    {
        return GameSession.Data.Inventory
            .GroupBy(x => x)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.Count());
    }
}
