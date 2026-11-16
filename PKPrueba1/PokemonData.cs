using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace PKPrueba1;

// Un Pokémon concreto del jugador o de un rival: especie, nivel, PS, movimientos, objeto, habilidad y naturaleza. Se guarda en la partida.
public class PokemonData
{
    // Id de la especie.
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    // Experiencia acumulada en el nivel actual.
    public int Experience { get; set; }
    // URL del sprite.
    public string SpriteUrl { get; set; } = "";
    // Movimientos conocidos (hasta 4).
    public List<PokemonMove> Moves { get; set; } = new();

    // Movimientos que puede aprender y que aún están pendientes de que el jugador decida.
    public List<string> PendingMoves { get; set; } = new();

    // Nombre del objeto equipado (null si no lleva).
    public string? HeldItem { get; set; }

    // Identificador de la habilidad.
    public string Ability { get; set; } = "";

    // Identificador de la naturaleza.
    public string Nature { get; set; } = "";

    // Constructor vacío para la deserialización JSON.
    public PokemonData() { }

    public PokemonData(int id, int level) : this(id, level, false)
    {
    }

    // Crea un Pokémon de la especie y nivel indicados: calcula estadísticas, elige habilidad y naturaleza al azar y le asigna movimientos (los mejores si bestMoves es true).
    public PokemonData(int id, int level, bool bestMoves)
    {
        Id = id;
        Level = level;

        PokemonSpecies species = PokemonDatabase.Get(id);
        Id = species.Id;
        Name = species.Name;
        SpriteUrl = PokemonSprites.GetFrontSprite(Id);

        Ability = Abilities.PickFor(Id);
        Nature = Abilities.RandomNature().Identifier;

        RecalculateStats(false);

        Moves = (bestMoves
                ? PokemonDatabase.GetBestMoves(Id, level)
                : PokemonDatabase.GetMovesAtLevel(Id, level))
            .Select(MoveCatalog.Create)
            .Where(m => m != null)
            .Select(m => m!)
            .ToList();
    }

    [JsonIgnore]
    // Estado temporal de combate (no se guarda): id de la forma mega activa, habilidad, movimientos y objeto de combate, y tipos modificados.
    public int? MegaSpeciesId { get; set; }

    [JsonIgnore]
    public string? BattleAbility { get; set; }

    [JsonIgnore]
    public List<PokemonMove>? BattleMoves { get; set; }

    [JsonIgnore]
    public string? BattleHeldItem { get; set; }

    [JsonIgnore]
    public List<string>? TypeOverride { get; set; }

    [JsonIgnore]
    // Movimientos que se usan realmente ahora mismo (los de combate si existen).
    public List<PokemonMove> EffectiveMoves => BattleMoves ?? Moves;

    [JsonIgnore]
    // Tipos actuales (pueden cambiar durante el combate).
    public List<string> CurrentTypes => TypeOverride ?? Species.Types;

    [JsonIgnore]
    // Objeto que lleva realmente ahora mismo (null si está vacío).
    public string? EffectiveHeldItem
    {
        get
        {
            string? item = BattleHeldItem ?? HeldItem;
            return string.IsNullOrEmpty(item) ? null : item;
        }
    }

    // Limpia todo el estado temporal de combate (megaevolución, habilidad, movimientos, objeto y tipos).
    public void ClearBattleState()
    {
        MegaSpeciesId = null;
        BattleAbility = null;
        BattleMoves = null;
        BattleHeldItem = null;
        TypeOverride = null;
    }

    [JsonIgnore]
    // true si está megaevolucionado.
    public bool IsMega => MegaSpeciesId.HasValue;

    [JsonIgnore]
    // Datos de la especie actual (la forma mega si está megaevolucionado).
    public PokemonSpecies Species => PokemonDatabase.Get(MegaSpeciesId ?? Id);

    [JsonIgnore]
    // Información completa de su habilidad.
    public AbilityInfo? AbilityData => Abilities.Get(Ability);

    [JsonIgnore]
    // Información completa de su naturaleza.
    public NatureInfo? NatureData => Abilities.GetNature(Nature);

    [JsonIgnore]
    // Nombre a mostrar (el de la forma mega si corresponde).
    public string DisplayName => IsMega ? Species.Name : Name;

    [JsonIgnore]
    // Sprite a mostrar (el de la forma mega si corresponde).
    public string CurrentSpriteUrl =>
        IsMega ? PokemonSprites.GetFrontSprite(MegaSpeciesId!.Value) : SpriteUrl;

    [JsonIgnore]
    // Tipos como texto, por ejemplo "Fuego / Volador".
    public string TypeText => TypeChart.DisplayList(Species.Types);

    [JsonIgnore]
    // true si está debilitado.
    public bool IsFainted => CurrentHP <= 0;

    [JsonIgnore]
    // Experiencia necesaria para subir de nivel.
    public int ExpToNextLevel => Level * 10 + 10;

    [JsonIgnore]
    // Estadísticas de combate calculadas a partir de las base de la especie, el nivel y la naturaleza.
    public int Attack => Calc(Species.Attack, 2);

    [JsonIgnore]
    public int Defense => Calc(Species.Defense, 3);

    [JsonIgnore]
    public int SpAttack => Calc(Species.SpAttack, 4);

    [JsonIgnore]
    public int SpDefense => Calc(Species.SpDefense, 5);

    [JsonIgnore]
    public int Speed => Calc(Species.Speed, 6);

    // Calcula una estadística: base x 2 x nivel / 100 + 5, con +10% o -10% según la naturaleza.
    private int Calc(int baseStat, int statId)
    {
        int value = baseStat * 2 * Level / 100 + 5;

        NatureInfo? nature = NatureData;

        if (nature != null && !nature.IsNeutral)
        {
            if (nature.Up == statId) value = value * 110 / 100;
            else if (nature.Down == statId) value = value * 90 / 100;
        }

        return value;
    }

    // Recalcula los PS máximos (por nivel o evolución). Si keepDamage es true, conserva el daño recibido y suma los PS máximos ganados.
    public void RecalculateStats(bool keepDamage)
    {
        PokemonSpecies sp = PokemonDatabase.Get(Id);
        int oldMax = MaxHP;

        MaxHP = sp.HP * 2 * Level / 100 + Level + 10;

        if (!keepDamage)
            CurrentHP = MaxHP;
        else if (CurrentHP > 0)
            CurrentHP = Math.Min(MaxHP, CurrentHP + Math.Max(0, MaxHP - oldMax));
    }

    // Restaura todos los PS.
    public void Heal()
    {
        CurrentHP = MaxHP;
    }

    // Suma experiencia y sube de nivel todas las veces que corresponda. Devuelve cuántos niveles ganó.
    public int GainExperience(int amount)
    {
        Experience += amount;
        int gained = 0;

        while (Level < 100 && Experience >= ExpToNextLevel)
        {
            Experience -= ExpToNextLevel;
            Level++;
            gained++;

            RecalculateStats(true);
            QueueMovesForLevel(Level);
        }

        return gained;
    }

    // Añade a los movimientos pendientes los que aprende en ese nivel (si no los sabe ya).
    private void QueueMovesForLevel(int level)
    {
        foreach (string move in PokemonDatabase.GetMovesLearnedAt(Id, level))
        {
            if (Moves.Any(m => m.Name == move) || PendingMoves.Contains(move))
                continue;

            PendingMoves.Add(move);
        }
    }

    // Evoluciona al Pokémon: cambia especie, nombre y sprite, recalcula estadísticas, ajusta la habilidad si ya no es válida y encola los movimientos que gana al evolucionar.
    public void EvolveTo(int newId)
    {
        int oldId = Id;

        PokemonSpecies species = PokemonDatabase.Get(newId);

        Id = species.Id;
        Name = species.Name;
        SpriteUrl = PokemonSprites.GetFrontSprite(Id);

        RecalculateStats(true);

        if (!Abilities.For(Id).Any(a => a.Identifier == Ability))
            Ability = Abilities.PickFor(Id);

        foreach (string move in PokemonDatabase.GetMovesGainedByEvolution(oldId, Id, Level))
        {
            if (Moves.Any(m => m.Name == move) || PendingMoves.Contains(move))
                continue;

            PendingMoves.Add(move);
        }
    }
}
