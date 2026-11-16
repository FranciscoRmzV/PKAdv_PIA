
namespace PKPrueba1;

// Sesión actual del juego: guarda qué usuario ha iniciado sesión y sus datos de partida.
public static class GameSession
{
    // Nombre del usuario conectado (vacío si no hay sesión).
    public static string Username { get; private set; } = "";

    // Datos de partida cargados del usuario actual.
    public static GameData Data { get; set; } = new();

    // Inicia sesión: guarda el nombre y carga su partida desde el disco.
    public static void Login(string username)
    {
        Username = username.Trim();
        Data = GameSaveManager.Load(Username);
    }

    // Guarda la partida actual en disco (solo si hay un usuario conectado).
    public static void Save()
    {
        if (!string.IsNullOrWhiteSpace(Username))
            GameSaveManager.Save(Username, Data);
    }

    // Cierra sesión: guarda los datos, borra el usuario y reinicia los datos en memoria.
    public static void Logout()
    {
        Save();
        Username = "";
        Data = new GameData();
    }
}
