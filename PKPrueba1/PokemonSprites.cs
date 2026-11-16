namespace PKPrueba1
{
    // Genera las URL de los sprites (imágenes) de cada Pokémon usando el repositorio PokeAPI/sprites.
    public static class PokemonSprites
    {
        // URL base de los sprites estilo Gen III (Rojo Fuego / Verde Hoja), disponibles hasta el Pokémon 151.
        private const string BaseUrl =
            "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/versions/generation-iii/firered-leafgreen/";

        // URL base alternativa para Pokémon con id mayor a 151, que no tienen sprite de Gen III.
        private const string FallbackUrl =
            "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/";

        // Devuelve la URL del sprite de frente del Pokémon.
        public static string GetFrontSprite(int pokemonId)
        {
            return pokemonId <= 151
                ? $"{BaseUrl}{pokemonId}.png"
                : $"{FallbackUrl}{pokemonId}.png";
        }

        // Devuelve la URL del sprite de espaldas (el que se ve del Pokémon propio en combate).
        public static string GetBackSprite(int pokemonId)
        {
            return pokemonId <= 151
                ? $"{BaseUrl}back/{pokemonId}.png"
                : $"{FallbackUrl}back/{pokemonId}.png";
        }

        // Devuelve la URL del sprite variocolor (shiny) del Pokémon.
        public static string GetShinySprite(int pokemonId)
        {
            return pokemonId <= 151
                ? $"{BaseUrl}shiny/{pokemonId}.png"
                : $"{FallbackUrl}shiny/{pokemonId}.png";
        }
    }
}
