namespace PKPrueba1;

// Petición de combate: indica contra qué entrenador se pelea y si es un combate de gimnasio (true) o de la torre (false).
public record BattleRequest(TrainerData Trainer, bool IsGym);
