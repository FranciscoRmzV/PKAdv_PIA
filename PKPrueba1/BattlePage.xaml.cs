using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PKPrueba1;

// Pantalla de combate por turnos: contiene toda la lógica de la batalla (turnos, movimientos, estados, clima, habilidades, cambios, megaevolución), la IA del rival y el asistente de combate.
public sealed partial class BattlePage : Page
{
    // Entrenador rival del combate actual.
    private TrainerData? trainer;
    // Pokémon del jugador que está combatiendo ahora.
    private PokemonData? active;

    // Equipo del jugador (el de la torre o el del desafío de gimnasios).
    private List<PokemonData> team = new();
    // Posición en el equipo rival del Pokémon que combate ahora.
    private int enemyIndex;
    // true cuando el combate ha terminado (victoria, derrota o rendición).
    private bool finished;
    // true si es un combate de gimnasio; false si es de la torre.
    private bool isGym;
    // true si el jugador ya usó su megaevolución en este combate (solo se permite una).
    private bool megaUsed;
    // true si el rival ya usó su megaevolución.
    private bool enemyMegaUsed;

    // Pokémon que ya gastaron su Banda Aguante (solo funciona una vez por combate).
    private readonly HashSet<PokemonData> sashUsed = new();
    // true mientras se resuelve un turno o hay un diálogo abierto; bloquea los botones.
    private bool busy;

    // Clima actual sin filtrar: "sun", "rain", "sand", "hail" o vacío.
    private string rawWeather = "";

    // Clima efectivo: se anula si algún Pokémon en combate tiene Nubosidad o Esclusa de Aire.
    private string weather => AbilityEffects.CloudNine(active, Enemy) ? "" : rawWeather;

    // Pokémon que se han transformado (por ejemplo, Ditto).
    private readonly HashSet<PokemonData> transformed = new();
    // Pokémon que ya han actuado en el turno actual.
    private readonly HashSet<PokemonData> actedThisTurn = new();
    // Turnos restantes del clima actual.
    private int weatherTurns;
    // Turnos restantes de Espacio Raro (invierte el orden de velocidad).
    private int trickRoomTurns;
    // Pantallas de campo por bando (0 = jugador, 1 = rival): turnos restantes de la que protege contra físicos y de la que protege contra especiales.
    private readonly int[] physScreen = new int[2];
    private readonly int[] specScreen = new int[2];
    // Trampas de campo por bando: capas de púas, capas de púas tóxicas y presencia de rocas.
    private readonly int[] spikes = new int[2];
    private readonly int[] toxicSpikes = new int[2];
    private readonly bool[] rocks = new bool[2];

    // Generador aleatorio del combate.
    private readonly Random random = new();
    // Estado de combate (mejoras, estados alterados...) de cada Pokémon.
    private readonly Dictionary<PokemonData, BattleBoosts> boosts = new();
    // Mensajes del turno que se muestran en pantalla.
    private readonly List<string> log = new();

    // Últimos sprites mostrados, para no recargar la imagen si no ha cambiado.
    private string lastEnemySprite = "";
    private string lastPlayerSprite = "";

    // Pokémon rival que combate ahora (null si no hay o el índice no es válido).
    private PokemonData? Enemy
    {
        get
        {
            if (trainer == null || enemyIndex < 0 || enemyIndex >= trainer.Team.Count)
                return null;

            return trainer.Team[enemyIndex];
        }
    }

    // Configura el aspecto de los botones, y define el atajo de teclado Z para megaevolucionar.
    public BattlePage()
    {
        InitializeComponent();

        Background = UiTheme.Backdrop();

        UiTheme.StyleButton(AttackButton, "#DC2626");
        UiTheme.StyleButton(SwitchButton, "#2563EB");
        UiTheme.StyleButton(ItemButton, "#7C3AED");
        UiTheme.StyleButton(SurrenderButton, "#475569");
        UiTheme.StyleButton(StatsButton, "#0D9488");

        UiTheme.SetIcon(AttackButton, "x-attack", "ATACAR");
        UiTheme.SetIcon(SwitchButton, "great-ball", "CAMBIAR POKÉMON");
        UiTheme.SetIcon(ItemButton, "potion", "OBJETOS");
        UiTheme.SetIcon(SurrenderButton, "escape-rope", "RENDIRSE");
        UiTheme.SetIcon(StatsButton, "scope-lens", "VER ESTADÍSTICAS");
        UiTheme.SetIcon(ContinueButton, "rare-candy", "CONTINUAR");

        var megaKey = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Z };

        megaKey.Invoked += (_, args) =>
        {
            args.Handled = true;
            TryMegaEvolve();
        };

        KeyboardAccelerators.Add(megaKey);
        UiTheme.StyleButtonHover(ContinueButton, "#16A34A");
        UiTheme.StyleButtonHover(MegaButton, "#DB2777");
    }

    // Se ejecuta al abrir la pantalla: recibe la petición de combate, elige el equipo y el primer Pokémon en pie, prepara el fondo, saca a los primeros Pokémon y aplica sus habilidades de entrada.
    protected override void OnNavigatedTo(
        Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var request = e.Parameter as BattleRequest;

        trainer = request?.Trainer;
        isGym = request?.IsGym ?? false;

        team = isGym ? GameSession.Data.GymTeam : GameSession.Data.PokemonParty;

        active = team.FirstOrDefault(p => !p.IsFainted);

        // Si faltan datos para combatir, se muestra un error y solo se permite continuar.
        if (trainer == null || trainer.Team.Count == 0 || active == null)
        {
            finished = true;
            MessageText.Text = "No se pudo iniciar el combate.";
            UpdateButtons();
            ContinueButton.Visibility = Visibility.Visible;
            return;
        }

        ApplyBackground();

        MessageText.Text = trainer.IsBoss
            ? $"¡{trainer.Name} te desafía en el {trainer.Gym?.GymName ?? "gimnasio"}! Todos los Pokémon están a nivel 100."
            : $"¡{trainer.Name} te desafía!";

        TrainerText.Text = trainer.Name;

        Log($"¡{trainer.Name} envía a {Enemy!.Name}!");
        TryEnemyMega();
        Log($"¡Adelante, {active.Name}!");

        AbilityEffects.GasActive = false;
        AbilityEffects.Suppressed = null;

        OnEnter(active, true);
        OnEnter(Enemy!, false);

        UpdateUi();
        UpdateButtons();
    }


    // true si el panel del asistente quedó desactualizado porque pasó un turno.
    private bool advisorStale;

    // Abre el panel del asistente de combate; muestra el saludo o avisa de que la situación cambió.
    private void OpenAdvisor()
    {
        AdvisorPanel.Visibility = Visibility.Visible;
        AdvisorToggleButton.Content = "Ocultar asistente";

        if (string.IsNullOrWhiteSpace(AdvisorHistory.Text))
        {
            AdvisorHistory.Text = "Asistente: ¡Hola! Analizo el combate en tiempo real. Pregúntame qué movimiento usar, " +
                "si conviene cambiar, quién es más rápido, qué tan débil es el rival o cómo está tu equipo. " +
                "Usa los botones de abajo para consultas rápidas.";
        }
        else if (advisorStale)
        {
            AdvisorHistory.Text += "\n\n──── Turno nuevo ────\nAsistente: La situación cambió desde mi último consejo. Pregúntame otra vez y lo recalculo.";
        }

        advisorStale = false;
        DispatcherQueue.TryEnqueue(() => AdvisorScroll.ChangeView(null, AdvisorScroll.ScrollableHeight, null));
    }

    // Cierra el panel del asistente de combate.
    private void CloseAdvisor()
    {
        if (AdvisorPanel.Visibility != Visibility.Visible)
            return;

        AdvisorPanel.Visibility = Visibility.Collapsed;
        AdvisorToggleButton.Content = "💬 Ayuda para el combate";
    }

    // Botón que abre o cierra el asistente.
    private void AdvisorToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (AdvisorPanel.Visibility == Visibility.Visible)
            CloseAdvisor();
        else
            OpenAdvisor();
    }

    // Botones de consulta rápida: ponen su pregunta (guardada en el Tag) en el cuadro y la responden.
    private void AdvisorQuickButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string question })
        {
            AdvisorInput.Text = question;
            AnswerAdvisor();
        }
    }

    // Enter en el cuadro de texto: responde la pregunta.
    private void AdvisorInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            AnswerAdvisor();
        }
    }

    // Botón "Enviar" del asistente.
    private void AdvisorSendButton_Click(object sender, RoutedEventArgs e) => AnswerAdvisor();

    // Responde la pregunta escrita: primero saludos/agradecimientos, luego validación (longitud, lenguaje, tema) y, si es válida, consejo de combate.
    private void AnswerAdvisor()
    {
        string question = AdvisorInput.Text.Trim();
        if (question.Length == 0)
            return;

        string answer = AdvisorSmallTalk(question) ?? ValidateAdvisorQuestion(question) ?? GetBattleAdvice(question);
        AdvisorHistory.Text += $"\n\nTú: {question}\n\nAsistente: {answer}";
        AdvisorInput.Text = "";
        advisorStale = false;
        DispatcherQueue.TryEnqueue(() => AdvisorScroll.ChangeView(null, AdvisorScroll.ScrollableHeight, null));
    }

    // Responde a saludos y agradecimientos cortos; devuelve null si no lo es.
    private string? AdvisorSmallTalk(string question)
    {
        string[] words = Regex.Split(RemoveAdvisorDiacritics(question).ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(w => w.Length > 0).ToArray();

        if (words.Length == 0 || words.Length > 4)
            return null;

        if (words.Any(w => w is "gracias" or "grax" or "thanks" or "genial" or "perfecto"))
            return "¡De nada! Aquí sigo si necesitas otro consejo. ¡Mucha suerte en el combate!";

        if (words.Any(w => w is "hola" or "buenas" or "hey" or "ey" or "holi"))
            return "¡Hola! Pregúntame, por ejemplo: «¿qué movimiento uso?», «¿conviene cambiar?» o «¿quién es más rápido?».";

        return null;
    }

    // Comprueba que la pregunta no sea demasiado larga, ofensiva ni ajena al combate. Devuelve el mensaje de rechazo o null si es válida.
    private string? ValidateAdvisorQuestion(string question)
    {
        if (question.Length > 240)
            return "La pregunta es demasiado larga. Resúmela en 240 caracteres o menos y vuelve a intentarlo.";

        string normalized = RemoveAdvisorDiacritics(question).ToLowerInvariant();
        string[] profanityRoots = { "puto", "puta", "pendej", "mierd", "ching", "verga", "cabr", "pinche", "jod", "mamon", "mamón", "culer", "maric", "coñ", "hijueputa" };
        foreach (string word in profanityRoots)
        {
            string pattern = @"(?<!\p{L})" + string.Join(@"[\p{P}\p{Z}\p{S}_]*", word.Select(c => Regex.Escape(c.ToString()))) + @"\p{L}*(?!\p{L})";
            if (Regex.IsMatch(normalized, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return "Mantengamos el chat respetuoso. Puedo ayudarte con movimientos, tipos, PS, cambios de Pokémon y estrategia del combate.";
        }

        string[] tokens = Regex.Split(normalized, @"[^\p{L}\p{N}]+").Where(t => t.Length > 0).ToArray();
        // Raíces de palabras que indican que la pregunta trata del combate.
        string[] battleTerms =
        {
            "combate", "batalla", "pokemon", "rival", "enemig", "oponent", "mov", "ataq", "atac", "golp", "dan", "tip", "efectiv", "debil", "débil", "fuerte", "resist", "inmun", "vida", "salud", "ps", "cur", "san", "camb", "relev", "equipo", "estrateg", "consej", "suger", "recomend", "mejor", "convien", "hacer", "hag", "debo", "puedo", "ganar", "perder", "estado", "venen", "quem", "paral", "dorm", "congel", "objeto", "pocion", "poción", "mega", "veloc", "primero", "ayud", "peligro", "tipo", "rapid", "lento", "turno", "adelant", "clima", "campo", "pua", "roca", "pantalla", "reflejo", "baya", "mochila", "item", "reserv", "restan", "retir", "sustitu", "sacar", "stat", "confus", "mejora"
        };
        bool isBattleQuestion = tokens.Any(token => battleTerms.Any(term => token.StartsWith(term, StringComparison.Ordinal)));

        if (!isBattleQuestion)
        {
            IEnumerable<string> currentNames = team.SelectMany(p => new[] { p.Name, p.DisplayName }.Concat(p.EffectiveMoves.Select(m => m.Name)))
                .Concat(Enemy == null ? Array.Empty<string>() : new[] { Enemy.Name, Enemy.DisplayName }.Concat(Enemy.EffectiveMoves.Select(m => m.Name)));
            isBattleQuestion = currentNames.Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(RemoveAdvisorDiacritics).Any(name => normalized.Contains(name.ToLowerInvariant(), StringComparison.Ordinal));
        }

        if (!isBattleQuestion)
            return "Solo puedo responder preguntas relacionadas con este juego y el combate actual. Pregúntame por un movimiento, los tipos, la vida, los cambios o una estrategia.";

        return null;
    }

    // Quita tildes y diacríticos del texto para comparar sin importar acentos.
    private static string RemoveAdvisorDiacritics(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (char character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                result.Append(character);
        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    // Estimación del resultado de un movimiento: efectividad, STAB, precisión, daño mínimo/medio/máximo, probabilidad de K.O. y prioridad.
    private sealed record MoveEstimate(
        PokemonMove Move,
        double Effectiveness,
        bool Stab,
        double Accuracy,
        int Min,
        int Avg,
        int Max,
        double KoChance,
        int Priority);

    // Devuelve el estado de combate del Pokémon sin crearlo si no existe (para consultas).
    private BattleBoosts PeekState(PokemonData pokemon)
        => boosts.TryGetValue(pokemon, out BattleBoosts? b) ? b : new BattleBoosts();

    // Estima el daño de un movimiento simulándolo 30 veces (por la variación aleatoria) y calculando mínimo, medio, máximo y probabilidad de K.O.
    private MoveEstimate EstimateMove(PokemonData attacker, PokemonData defender, PokemonMove move)
    {
        double effectiveness = BattleCalculator.Effectiveness(attacker, defender, move.Type);
        bool stab = attacker.CurrentTypes.Contains(move.Type);
        double accuracy = move.Accuracy >= 101 ? 1.0 : Math.Clamp(move.Accuracy, 0, 100) / 100.0;
        MoveEffects fx = move.Effects;
        int priority = PriorityOf(attacker, move);

        bool fixedDamage = fx.Fixed > 0 || fx.FixedLevel;

        // Movimientos sin daño directo: estimación vacía.
        if (effectiveness <= 0 || move.IsStatus || (move.Power <= 0 && !fixedDamage))
            return new MoveEstimate(move, effectiveness, stab, accuracy, 0, 0, 0, 0, priority);

        int defenderHp = Math.Max(1, defender.CurrentHP);
        double hits = fx.MultiMax > 1 ? (fx.MultiMin + fx.MultiMax) / 2.0 : 1.0;

        // Movimientos de daño fijo: el daño es siempre el mismo.
        if (fixedDamage)
        {
            int fixedValue = (int)(fx.FixedLevel ? attacker.Level : fx.Fixed);
            return new MoveEstimate(move, effectiveness, stab, accuracy, fixedValue, fixedValue, fixedValue,
                fixedValue >= defenderHp ? 1.0 : 0.0, priority);
        }

        bool attackerIsPlayer = SideOf(attacker) == 0;
        double multiplier = attackerIsPlayer || trainer == null ? 1.0 : trainer.DamageMultiplier;
        int defenderSide = SideOf(defender);
        bool screened = move.IsSpecial ? specScreen[defenderSide] > 0 : physScreen[defenderSide] > 0;

        if (screened && Ab(attacker) != "infiltrator")
            multiplier *= 0.5;

        // Simulación del daño con aleatoriedad: 30 muestras.
        const int samples = 30;
        int min = int.MaxValue, max = 0, kos = 0;
        double sum = 0;

        for (int i = 0; i < samples; i++)
        {
            DamageResult r = BattleCalculator.Calculate(
                attacker, defender, move, PeekState(attacker), PeekState(defender), multiplier, weather, 1.0);

            int damage = (int)Math.Round(r.Damage * hits);

            sum += damage;
            min = Math.Min(min, damage);
            max = Math.Max(max, damage);

            if (damage >= defenderHp)
                kos++;
        }

        return new MoveEstimate(move, effectiveness, stab, accuracy, min, (int)Math.Round(sum / samples), max,
            kos / (double)samples, priority);
    }

    // Estima todos los movimientos disponibles del atacante (excluye el anulado).
    private List<MoveEstimate> EstimateAll(PokemonData attacker, PokemonData defender)
    {
        BattleBoosts state = PeekState(attacker);

        return attacker.EffectiveMoves
            .Where(m => !(state.DisabledTurns > 0 && state.DisabledMove == m.Name))
            .Select(m => EstimateMove(attacker, defender, m))
            .ToList();
    }

    // Convierte un daño en porcentaje de los PS.
    private static int Percent(int damage, int hp) => (int)Math.Round(damage * 100.0 / Math.Max(1, hp));

    // Texto de la efectividad: "superefectivo", "poco eficaz", "no le afecta"...
    private static string EffectivenessLabel(double e)
    {
        if (e <= 0) return "no le afecta";
        if (e >= 4) return "superefectivo x4";
        if (e > 1) return "superefectivo";
        if (e < 1) return "poco eficaz";
        return "daño normal";
    }

    // Resumen del daño como porcentaje de PS del objetivo, indicando si puede dejarlo K.O.
    private static string DamageSummary(MoveEstimate est, PokemonData target)
    {
        if (est.Avg <= 0)
            return est.Effectiveness <= 0 ? "no le afecta" : "sin daño directo";

        int hp = Math.Max(1, target.CurrentHP);
        int low = Percent(est.Min, hp);
        int high = Percent(est.Max, hp);
        string range = low >= 100 ? "100%+" : $"{low}–{Math.Min(high, 999)}%";

        string ko = est.KoChance >= 0.9 ? " · K.O. casi seguro"
            : est.KoChance >= 0.35 ? $" · podría dejarlo K.O. (~{(int)Math.Round(est.KoChance * 100)}%)"
            : "";

        return $"{range} de sus PS actuales{ko}";
    }

    // Frase que indica quién actúa primero (considera Espacio Raro y empates).
    private string SpeedLine(PokemonData me, PokemonData foe, out bool iAmFaster, out bool tie)
    {
        double mine = SpeedOf(me);
        double theirs = SpeedOf(foe);
        tie = Math.Abs(mine - theirs) < 0.0001;
        iAmFaster = trickRoomTurns > 0 ? mine < theirs : mine > theirs;

        string room = trickRoomTurns > 0 ? " (con Espacio Raro, el más lento actúa primero)" : "";

        if (tie)
            return $"Misma velocidad que {foe.DisplayName}: el orden se decide al azar{room}.";

        return iAmFaster
            ? $"{me.DisplayName} actúa antes que {foe.DisplayName}{room}."
            : $"{foe.DisplayName} actúa antes que {me.DisplayName}{room}.";
    }

    // Devuelve el movimiento del atacante que más daño esperado hace al defensor.
    private MoveEstimate? WorstThreat(PokemonData attacker, PokemonData defender)
    {
        return EstimateAll(attacker, defender)
            .Where(x => x.Avg > 0)
            .OrderByDescending(x => x.Avg * x.Accuracy)
            .FirstOrDefault();
    }


    // Interpreta la pregunta (por palabras clave) y devuelve el consejo adecuado: cambio, movimiento, salud, estados, equipo, tipos, velocidad...
    private string GetBattleAdvice(string question)
    {
        if (active == null || Enemy == null)
            return "No hay un combate activo para analizar.";

        string[] tokens = Regex.Split(RemoveAdvisorDiacritics(question).ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(t => t.Length > 0).ToArray();

        // Funciones auxiliares: Has busca palabras que empiecen por una raíz; Is busca palabras exactas.
        bool Has(params string[] roots) => tokens.Any(t => roots.Any(r => t.StartsWith(r, StringComparison.Ordinal)));
        bool Is(params string[] words) => tokens.Any(t => words.Contains(t));

        PokemonData me = active;
        PokemonData foe = Enemy;

        // Se decide el tipo de consejo según las palabras de la pregunta, en este orden: cambio, salud, estados, equipo, velocidad, objetos, megaevolución, campo, tipos y, por defecto, movimiento.
        if (Has("cambi", "relev", "retir", "sustitu", "sacar", "peligro", "salir") || Is("cambio", "swap"))
            return SwitchAdvice(me, foe);

        if (Is("ps", "hp") || Has("vida", "salud", "curar", "cura", "queda", "aguant"))
            return HealthAdvice(me, foe);

        if (Has("estado", "venen", "quem", "paral", "dorm", "congel", "confus", "mejora", "baja", "stat") || Is("sube", "suben", "bajas"))
            return StatusAdvice(me, foe);

        if (Has("equipo", "reserva", "banca", "restan"))
            return TeamAdvice();

        if (Has("veloc", "rapid", "primero", "adelant", "turno", "orden", "lento"))
        {
            string line = SpeedLine(me, foe, out bool faster, out bool tie);
            string detail = $"Velocidad: {me.DisplayName} {(int)SpeedOf(me)} vs {foe.DisplayName} {(int)SpeedOf(foe)}.";
            string tip = tie ? "" : faster
                ? " Aprovecha para atacar primero y quitarle PS antes de que reaccione."
                : " Si quieres actuar antes, busca un movimiento con prioridad o cambia a alguien más veloz.";
            return $"{line}\n{detail}{tip}";
        }

        if (Has("objeto", "pocion", "mochila", "item", "bayas", "baya"))
            return "Puedes abrir Objetos durante tu turno para curarte o quitar estados. Usar un objeto consume el turno, " +
                "así que conviene hacerlo cuando el rival no pueda rematarte con su siguiente golpe " +
                $"(ahora {me.DisplayName} tiene {Percent(me.CurrentHP, me.MaxHP)}% de PS).";

        if (Has("mega"))
        {
            if (megaUsed || me.IsMega)
                return "Ya megaevolucionaste en este combate; solo se puede una vez.";

            return CanMega()
                ? $"{me.DisplayName} puede megaevolucionar: pulsa el botón Z (o la tecla Z). No gasta el turno y mejora sus estadísticas, así que hazlo antes de atacar."
                : $"{me.DisplayName} no puede megaevolucionar ahora. Necesita llevar su megapiedra equipada.";
        }

        if (Has("clima", "campo", "trampa", "pua", "roca", "pantalla", "reflejo"))
        {
            string field = FieldSummary();
            return field.Length == 0
                ? "No hay clima ni efectos de campo activos ahora mismo."
                : $"Efectos de campo activos: {field}.";
        }

        // Si la pregunta pide un movimiento, no se responde solo con tipos.
        bool asksMove = Has("mov", "atac", "ataq", "uso", "usar", "usa", "golp", "dan", "pegar", "mejor", "recomend", "suger", "que hago", "hago");

        if (Has("tipo", "debil", "resist", "inmun", "efectiv") && !asksMove)
            return TypeAdvice(me, foe);

        return MoveAdvice(me, foe);
    }

    // Recomienda el mejor movimiento: puntúa cada uno por daño esperado (limitado a los PS del rival) x precisión, valora la prioridad y penaliza recarga y autodestrucción. Indica alternativas, quién es más rápido y el peligro del rival.
    private string MoveAdvice(PokemonData me, PokemonData foe)
    {
        List<MoveEstimate> all = EstimateAll(me, foe);
        SpeedLine(me, foe, out bool faster, out bool tie);
        MoveEstimate? threat = WorstThreat(foe, me);
        // Hay peligro si el rival puede dejar K.O. con probabilidad de 50% o más y actúa antes (o empata).
        bool inDanger = threat != null && threat.KoChance >= 0.5 && (!faster || tie);

        var ranked = all
            .Where(x => x.Avg > 0)
            .Select(x =>
            {
                double value = Math.Min(x.Avg, Math.Max(1, foe.CurrentHP)) * x.Accuracy;

                if (x.Priority > 0 && x.KoChance >= 0.5 && !faster)
                    value *= 1.5;

                if (x.Move.Effects.Recharge) value *= 0.7;
                if (x.Move.Effects.SelfKo) value *= 0.5;

                return new { Est = x, Value = value };
            })
            .OrderByDescending(x => x.Value)
            .ThenByDescending(x => x.Est.KoChance)
            .ToList();

        var sb = new StringBuilder();

        // Si ningún movimiento hace daño, se sugieren movimientos de apoyo o cambiar de Pokémon.
        if (ranked.Count == 0)
        {
            sb.Append($"Ninguno de tus movimientos de daño afecta a {foe.DisplayName} ({TypeChart.DisplayList(foe.CurrentTypes)}). ");

            string support = SupportIdeas(me, foe);
            sb.Append(support.Length > 0 ? support : "Considera cambiar de Pokémon.");
            return sb.ToString().Trim();
        }

        MoveEstimate best = ranked[0].Est;

        sb.Append($"Mi recomendación: {best.Move.Name}.\n");
        sb.Append($"• Daño: {DamageSummary(best, foe)}.\n");

        var why = new List<string> { EffectivenessLabel(best.Effectiveness) };
        if (best.Stab) why.Add("mismo tipo que tu Pokémon");
        if (best.Accuracy < 1.0) why.Add($"precisión {(int)Math.Round(best.Accuracy * 100)}%");
        if (best.Priority > 0) why.Add("tiene prioridad");
        if (best.Move.Effects.Recharge) why.Add("tendrás que recargar el siguiente turno");
        if (best.Move.Effects.Recoil > 0) why.Add($"retroceso {best.Move.Effects.Recoil}%");
        if (best.Move.Effects.SelfKo) why.Add("te debilita al usarlo");
        sb.Append($"• Porque: {string.Join(", ", why)}.");

        List<string> others = ranked.Skip(1).Take(2)
            .Select(x => $"{x.Est.Move.Name} ({DamageSummary(x.Est, foe)}, {EffectivenessLabel(x.Est.Effectiveness)})")
            .ToList();

        if (others.Count > 0)
            sb.Append("\n• Alternativas: " + string.Join("; ", others) + ".");

        sb.Append("\n\n" + SpeedLine(me, foe, out _, out _));

        if (threat != null)
        {
            int myHp = Math.Max(1, me.CurrentHP);
            sb.Append($" Su golpe más peligroso, {threat.Move.Name}, te quitaría ~{Percent(threat.Avg, myHp)}% de tus PS actuales");
            sb.Append(threat.KoChance >= 0.9 ? " y te dejaría K.O.." : ".");
        }

        if (best.KoChance >= 0.9 && (faster && !tie))
            sb.Append("\nVeredicto: atácalo, lo debilitas antes de que actúe.");
        else if (inDanger)
            sb.Append($"\nOjo: probablemente te debilita antes o al mismo tiempo. Si {me.DisplayName} no se salva, considera cambiar.");
        else if (best.KoChance >= 0.5)
            sb.Append("\nVeredicto: buen momento para rematar.");
        else
        {
            string support = SupportIdeas(me, foe);

            if (support.Length > 0 && threat != null && threat.Avg < me.CurrentHP * 0.35)
                sb.Append("\nSi quieres jugar con ventaja: " + support);
        }

        sb.Append("\n(Estimación con el cálculo real del juego; los críticos y la variación del daño pueden cambiar el resultado.)");
        return sb.ToString();
    }

    // Sugiere hasta 2 movimientos de apoyo útiles ahora: causar un estado, mejorar estadísticas (con buena salud) o curarse (con poca salud).
    private string SupportIdeas(PokemonData me, PokemonData foe)
    {
        BattleBoosts myState = PeekState(me);
        BattleBoosts foeState = PeekState(foe);
        var ideas = new List<string>();

        foreach (PokemonMove move in me.EffectiveMoves.Where(m => m.IsStatus || m.Power <= 0))
        {
            if (myState.DisabledTurns > 0 && myState.DisabledMove == move.Name)
                continue;

            MoveEffects fx = move.Effects;

            bool inflicts = fx.Statuses.Any(s => s.Chance >= 100 && s.Status != "cnf" && s.Status != "flc");

            if (inflicts && foeState.Status.Length == 0 && BattleCalculator.Effectiveness(me, foe, move.Type) > 0)
            {
                ideas.Add($"{move.Name} ({fx.Describe()})");
                continue;
            }

            bool boostsSelf = fx.Stats.Any(c => c.OnUser && c.Delta > 0 && c.Chance >= 100);

            if (boostsSelf && me.CurrentHP * 100 >= me.MaxHP * 60)
            {
                ideas.Add($"{move.Name} ({fx.Describe()})");
                continue;
            }

            if ((fx.Heal > 0 || fx.HealWeather) && me.CurrentHP * 100 <= me.MaxHP * 55)
                ideas.Add($"{move.Name} ({fx.Describe()})");
        }

        return ideas.Count == 0 ? "" : string.Join("; ", ideas.Take(2)) + ".";
    }

    // Aconseja si cambiar de Pokémon: compara el daño que haría y recibiría cada relevo y propone el mejor. No recomienda cambiar si puedes debilitar al rival antes de que actúe.
    private string SwitchAdvice(PokemonData me, PokemonData foe)
    {
        if (IsTrapped(me))
            return $"{me.DisplayName} está atrapado: no puede cambiar mientras dure el efecto. Lo mejor es atacar o usar un objeto.";

        List<PokemonData> reserves = team.Where(p => !p.IsFainted && p != me).ToList();

        if (reserves.Count == 0)
            return "No te queda ningún otro Pokémon en pie, así que no puedes cambiar. Ataca con tu mejor movimiento o usa un objeto de curación.";

        SpeedLine(me, foe, out bool faster, out bool tie);
        MoveEstimate? threat = WorstThreat(foe, me);
        MoveEstimate? myBest = EstimateAll(me, foe).Where(x => x.Avg > 0).OrderByDescending(x => x.Avg * x.Accuracy).FirstOrDefault();
        int hpPercent = Percent(me.CurrentHP, me.MaxHP);

        // Puntúa cada Pokémon de reserva: daño que haría al rival menos (con peso 0.8) el daño que recibiría.
        var options = reserves.Select(p =>
        {
            double offense = EstimateAll(p, foe).Where(x => x.Avg > 0).Select(x => Math.Min(x.Avg, foe.CurrentHP) * x.Accuracy).DefaultIfEmpty(0).Max();
            MoveEstimate? incoming = WorstThreat(foe, p);
            double defense = incoming == null ? 0 : incoming.Avg;

            return new
            {
                Pokemon = p,
                Offense = Percent((int)offense, foe.CurrentHP),
                Incoming = Percent((int)defense, p.CurrentHP),
                Score = offense / Math.Max(1, foe.CurrentHP) - defense / Math.Max(1, p.CurrentHP) * 0.8
            };
        }).OrderByDescending(x => x.Score).ToList();

        var best = options[0];

        bool inDanger = threat != null && threat.KoChance >= 0.5 && (!faster || tie);
        bool badMatchup = myBest == null || myBest.Effectiveness < 1.0 || (threat != null && threat.Effectiveness > 1.0 && hpPercent < 70);
        bool canKoFirst = myBest != null && myBest.KoChance >= 0.7 && faster && !tie;

        var sb = new StringBuilder();

        if (canKoFirst)
        {
            sb.Append($"No te conviene cambiar: {me.DisplayName} es más rápido y {myBest!.Move.Name} puede debilitar a {foe.DisplayName} de inmediato ({DamageSummary(myBest, foe)}).");
            return sb.ToString();
        }

        if (inDanger || (badMatchup && best.Score > 0.15))
        {
            sb.Append(inDanger
                ? $"Sí, conviene cambiar: {foe.DisplayName} probablemente te debilita con {threat!.Move.Name} (~{Percent(threat.Avg, me.CurrentHP)}% de tus PS).\n"
                : $"Puede convenir cambiar: el cruce de tipos no te favorece contra {foe.DisplayName}.\n");
        }
        else
        {
            sb.Append($"Por ahora puedes quedarte con {me.DisplayName} ({hpPercent}% de PS)");
            sb.Append(myBest != null ? $" y atacar con {myBest.Move.Name}.\n" : ".\n");
            sb.Append("Si aun así quieres cambiar, el mejor relevo sería:\n");
        }

        sb.Append($"• {best.Pokemon.DisplayName} ({Percent(best.Pokemon.CurrentHP, best.Pokemon.MaxHP)}% PS, {TypeChart.DisplayList(best.Pokemon.CurrentTypes)}): " +
            $"haría ~{best.Offense}% de daño a {foe.DisplayName} y recibiría ~{best.Incoming}% de su mejor ataque.");

        foreach (var alt in options.Skip(1).Take(1))
            sb.Append($"\n• Alternativa: {alt.Pokemon.DisplayName} (haría ~{alt.Offense}%, recibiría ~{alt.Incoming}%).");

        sb.Append("\nRecuerda que cambiar consume el turno: el rival atacará al entrante.");
        return sb.ToString();
    }

    // Informa de los PS de ambos, cuántos golpes del rival aguantas y si estás en zona de riesgo.
    private string HealthAdvice(PokemonData me, PokemonData foe)
    {
        int myPercent = Percent(me.CurrentHP, me.MaxHP);
        int foePercent = Percent(foe.CurrentHP, foe.MaxHP);
        MoveEstimate? threat = WorstThreat(foe, me);

        var sb = new StringBuilder();
        sb.Append($"{me.DisplayName}: {me.CurrentHP}/{me.MaxHP} PS ({myPercent}%). {foe.DisplayName}: {foePercent}% de PS.");

        if (threat != null)
        {
            int hitsToFaint = (int)Math.Ceiling(me.CurrentHP / (double)Math.Max(1, threat.Avg));
            sb.Append($"\nSu mejor golpe ({threat.Move.Name}) te quita ~{Percent(threat.Avg, me.CurrentHP)}% de tus PS actuales; " +
                (hitsToFaint <= 1 ? "podrías caer de un solo golpe." : $"aguantas unos {hitsToFaint} golpes así."));
        }

        sb.Append(myPercent <= 30
            ? "\nEstás en zona de riesgo: cambia a un Pokémon más sano o usa un objeto de curación (consume el turno)."
            : myPercent <= 60
                ? "\nVas por la mitad: aún puedes atacar, pero vigila que no te rematen."
                : "\nTienes buena salud; puedes concentrarte en atacar.");

        return sb.ToString();
    }

    // Explica los estados alterados y cambios de estadísticas de ambos Pokémon y su efecto práctico (quemadura, parálisis, veneno, sueño).
    private string StatusAdvice(PokemonData me, PokemonData foe)
    {
        string own = BoostText(me);
        string rival = BoostText(foe);

        var sb = new StringBuilder();
        sb.Append($"{me.DisplayName}: {(own.Length == 0 ? "sin estados ni cambios" : own)}.\n");
        sb.Append($"{foe.DisplayName}: {(rival.Length == 0 ? "sin estados ni cambios" : rival)}.");

        BattleBoosts mine = PeekState(me);

        if (mine.Status == "slp" || mine.Status == "frz")
            sb.Append("\nNo podrás actuar siempre; si tienes un relevo sano, puede convenir cambiar.");
        else if (mine.Status == "brn" && me.EffectiveMoves.Any(m => !m.IsStatus && !m.IsSpecial && m.Power > 0))
            sb.Append("\nLa quemadura reduce a la mitad tu daño físico: prioriza movimientos especiales o cambia.");
        else if (mine.Status == "par")
            sb.Append("\nLa parálisis baja tu velocidad a la mitad y puede impedirte atacar.");
        else if (mine.Status == "psn" || mine.Status == "tox")
            sb.Append("\nEl veneno te quita PS cada turno; no alargues el combate.");

        return sb.ToString();
    }

    // Resumen del equipo del jugador: cada Pokémon con su nivel, tipos y estado (▶ marca el que combate).
    private string TeamAdvice()
    {
        var lines = team.Select(p =>
        {
            string mark = p == active ? "▶ " : "• ";
            string state = p.IsFainted ? "debilitado" : $"{Percent(p.CurrentHP, p.MaxHP)}% PS";
            return $"{mark}{p.DisplayName} Nv.{p.Level} ({TypeChart.DisplayList(p.CurrentTypes)}) — {state}";
        });

        int alive = team.Count(p => !p.IsFainted);
        return $"Tu equipo ({alive}/{team.Count} en pie):\n{string.Join("\n", lines)}";
    }

    // Explica la efectividad de cada uno de tus movimientos contra el rival y si su mejor ataque tiene ventaja de tipo sobre ti.
    private string TypeAdvice(PokemonData me, PokemonData foe)
    {
        var sb = new StringBuilder();
        sb.Append($"{foe.DisplayName} es de tipo {TypeChart.DisplayList(foe.CurrentTypes)}.\n");

        foreach (PokemonMove move in me.EffectiveMoves)
        {
            double e = BattleCalculator.Effectiveness(me, foe, move.Type);
            string extra = move.IsStatus || move.Power <= 0 ? " (movimiento de estado)" : "";
            sb.Append($"• {move.Name}: {EffectivenessLabel(e)}{extra}\n");
        }

        MoveEstimate? threat = WorstThreat(foe, me);

        if (threat != null && threat.Effectiveness > 1)
            sb.Append($"Cuidado: {threat.Move.Name} del rival es {EffectivenessLabel(threat.Effectiveness)} contra {me.DisplayName}.");
        else
            sb.Append($"Sus ataques no tienen ventaja de tipo clara sobre {me.DisplayName}.");

        return sb.ToString().TrimEnd();
    }


    // Si el combate es de gimnasio, usa la imagen del gimnasio como fondo de la pantalla.
    private void ApplyBackground()
    {
        if (trainer?.Gym == null)
            return;

        try
        {
            Background = new Microsoft.UI.Xaml.Media.ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(trainer.Gym.Background)),
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"No se pudo cargar el fondo del gimnasio: {ex.Message}");
        }
    }

    // Añade un mensaje al registro del combate (se conservan los 8 últimos) y lo muestra en pantalla.
    private void Log(string text)
    {
        log.Add(text);

        while (log.Count > 8)
            log.RemoveAt(0);

        BattleStatusText.Text = string.Join("\n", log);
    }

    // Habilita o deshabilita los botones según si el combate está ocupado o terminado; muestra "Continuar" al finalizar y activa el botón Mega si se puede.
    private void UpdateButtons()
    {
        bool enabled = !busy && !finished;

        AttackButton.IsEnabled = enabled;
        SwitchButton.IsEnabled = enabled;
        ItemButton.IsEnabled = enabled;
        SurrenderButton.IsEnabled = enabled;
        StatsButton.IsEnabled = !busy;

        MegaButton.IsEnabled = CanMega() && !busy;

        ContinueButton.Visibility = finished ? Visibility.Visible : Visibility.Collapsed;
        ContinueButton.IsEnabled = !busy;
    }

    // Cambia el sprite de una imagen solo si la URL es distinta a la anterior (evita recargas innecesarias).
    private static void SetSprite(Image image, string url, ref string last)
    {
        if (url == last)
            return;

        last = url;
        image.Source = string.IsNullOrWhiteSpace(url)
            ? null
            : new BitmapImage(new Uri(url));
    }

    // Actualiza toda la interfaz: panel del clima/campo, datos del Pokémon rival y del jugador (sprite, nombre, nivel, habilidad, objeto, tipos, barra de PS y estados) y los equipos restantes.
    private void UpdateUi()
    {
        FieldText.Text = FieldSummary();

        PokemonData? enemy = Enemy;

        if (enemy != null && trainer != null)
        {
            SetSprite(EnemyImage, enemy.CurrentSpriteUrl, ref lastEnemySprite);

            EnemyNameText.Text = enemy.DisplayName;
            EnemyLevelText.Text = $"Nivel {enemy.Level}" +
                (Abilities.Get(enemy.BattleAbility ?? enemy.Ability) is { } enemyAbility ? $" · {enemyAbility.Name}" : "") +
                (enemy.HeldItem != null ? $" · Obj.: {enemy.HeldItem}" : "");

            EnemyTypes.Children.Clear();

            foreach (string type in enemy.CurrentTypes)
                EnemyTypes.Children.Add(UiTheme.TypeChip(type));

            EnemyHPBar.Foreground = UiTheme.HpBrush(enemy.CurrentHP, enemy.MaxHP);
            EnemyStatusText.Text = BoostText(enemy);

            EnemyHPBar.Maximum = Math.Max(1, enemy.MaxHP);
            EnemyHPBar.Value = Math.Clamp(enemy.CurrentHP, 0, enemy.MaxHP);
            EnemyHPText.Text = $"PS: {enemy.CurrentHP}/{enemy.MaxHP}";

            int remaining = trainer.Team.Count - enemyIndex;
            EnemyTeamText.Text = $"Pokémon rivales restantes: {remaining}/{trainer.Team.Count}";
        }

        if (active != null)
        {
            SetSprite(
                PlayerImage,
                active.IsMega ? active.CurrentSpriteUrl : PokemonSprites.GetBackSprite(active.Id),
                ref lastPlayerSprite);

            PlayerNameText.Text = active.DisplayName;
            PlayerLevelText.Text = $"Nivel {active.Level}" +
                (Abilities.Get(active.BattleAbility ?? active.Ability) is { } activeAbility ? $" · {activeAbility.Name}" : "") +
                (active.HeldItem != null ? $" · Obj.: {active.HeldItem}" : "");

            PlayerTypes.Children.Clear();

            foreach (string type in active.CurrentTypes)
                PlayerTypes.Children.Add(UiTheme.TypeChip(type));

            PlayerHPBar.Foreground = UiTheme.HpBrush(active.CurrentHP, active.MaxHP);

            PlayerHPBar.Maximum = Math.Max(1, active.MaxHP);
            PlayerHPBar.Value = Math.Clamp(active.CurrentHP, 0, active.MaxHP);
            PlayerHPText.Text = $"PS: {active.CurrentHP}/{active.MaxHP}";

            PlayerBoostText.Text = BoostText(active);
        }

        int alive = team.Count(p => !p.IsFainted);
        PlayerTeamText.Text = $"Equipo en pie: {alive}/{team.Count}";
    }

    // Texto con icono de un estado alterado.
    private static string StatusLabel(string code)
    {
        return code switch
        {
            "brn" => "🔥 Quemado",
            "par" => "⚡ Paralizado",
            "psn" => "☠ Envenenado",
            "tox" => "☠ Envenenado grave",
            "slp" => "💤 Dormido",
            "frz" => "❄ Congelado",
            _ => ""
        };
    }

    // Resumen en una línea de los estados y cambios de estadísticas de un Pokémon (quemado, confuso, Atq +2...).
    private string BoostText(PokemonData pokemon)
    {
        if (!boosts.TryGetValue(pokemon, out BattleBoosts? b))
            return "";

        var parts = new List<string>();

        string status = StatusLabel(b.Status);

        if (status.Length > 0) parts.Add(status);
        if (b.Confusion > 0) parts.Add("😵 Confuso");
        if (b.Infatuated) parts.Add("Enamorado");
        if (b.DisabledTurns > 0) parts.Add($"{b.DisabledMove} anulado");
        if (b.Seeded) parts.Add("Drenadoras");
        if (b.TrapTurns > 0) parts.Add("Atrapado");
        if (b.SlowStartTurns > 0) parts.Add("Inicio lento");
        if (b.Atk != 0) parts.Add($"Atq {b.Atk:+#;-#}");
        if (b.Def != 0) parts.Add($"Def {b.Def:+#;-#}");
        if (b.SpA != 0) parts.Add($"AtqEsp {b.SpA:+#;-#}");
        if (b.SpD != 0) parts.Add($"DefEsp {b.SpD:+#;-#}");
        if (b.Spe != 0) parts.Add($"Vel {b.Spe:+#;-#}");
        if (b.Crit) parts.Add("Crítico+");

        return string.Join(" · ", parts);
    }

    // Devuelve el estado de combate del Pokémon, creándolo si todavía no existe.
    private BattleBoosts StateOf(PokemonData pokemon)
    {
        if (!boosts.TryGetValue(pokemon, out BattleBoosts? b))
        {
            b = new BattleBoosts();
            boosts[pokemon] = b;
        }

        return b;
    }

    // Velocidad efectiva en combate: velocidad base x objeto x habilidad x niveles de mejora, reducida a la mitad si está paralizado.
    private double SpeedOf(PokemonData pokemon)
    {
        BattleBoosts b = StateOf(pokemon);

        double speed = pokemon.Speed
            * HeldItems.SpeedMultiplier(pokemon)
            * AbilityEffects.SpeedMultiplier(pokemon, weather, b, sashUsed.Contains(pokemon))
            * BattleBoosts.Mult(b.Spe);

        if (b.Status == "par" && !AbilityEffects.IgnoresParalysisSpeed(pokemon))
            speed *= 0.5;

        return speed;
    }


    // Botón "Atacar": deja elegir un movimiento y resuelve el turno.
    private async void AttackButton_Click(object sender, RoutedEventArgs e)
    {
        if (busy || finished || active == null || Enemy == null)
            return;

        CloseAdvisor();

        busy = true;
        UpdateButtons();

        try
        {
            PokemonMove? move = await ChooseMoveAsync();

            if (move != null)
                await ResolveTurnAsync(move);
        }
        finally
        {
            busy = false;
            UpdateButtons();
        }
    }

    // Muestra los movimientos como tarjetas y devuelve el elegido (null si cancela o si el movimiento está anulado).
    private async Task<PokemonMove?> ChooseMoveAsync()
    {
        if (active == null)
            return null;

        List<PokemonMove> moves = active.EffectiveMoves;

        List<ChoiceCard> cards = moves
            .Select(m => Dialogs.MoveCard(m, active.CurrentTypes.Contains(m.Type)))
            .ToList();

        int index = await Dialogs.ChooseAsync(
            XamlRoot,
            "Elige un movimiento",
            null,
            cards,
            "Cancelar",
            2);

        if (index < 0)
            return null;

        BattleBoosts state = StateOf(active);

        if (state.DisabledTurns > 0 && state.DisabledMove == moves[index].Name)
        {
            Log($"¡{moves[index].Name} está anulado! Elige otro movimiento.");
            UpdateUi();
            return null;
        }

        return moves[index];
    }

    // Botón "Cambiar Pokémon": elige un relevo en pie, comprueba que no esté atrapado, hace el cambio (habilidades de salida/entrada y trampas de campo) y consume el turno.
    private async void SwitchButton_Click(object sender, RoutedEventArgs e)
    {
        if (busy || finished || active == null)
            return;

        CloseAdvisor();

        busy = true;
        UpdateButtons();

        try
        {
            List<PokemonData> options = team
                .Where(p => !p.IsFainted && p != active)
                .ToList();

            if (options.Count == 0)
            {
                Log("No tienes otros Pokémon en condiciones de luchar.");
                return;
            }

            int index = await Dialogs.ChooseAsync(
                XamlRoot,
                "Cambiar Pokémon",
                null,
                Dialogs.PartyCards(options),
                "Cancelar");

            if (index < 0)
                return;

            if (IsTrapped(active))
            {
                Log($"¡{active.Name} está atrapado y no puede cambiar!");
                return;
            }

            ClearLogForTurn();

            OnSwitchOut(active);
            StateOf(active).ResetVolatile();

            active = options[index];

            Log($"¡Adelante, {active.Name}!");

            OnEnter(active, true);
            ApplyHazards(active);

            UpdateUi();

            await ResolveTurnAsync(null);
        }
        finally
        {
            busy = false;
            UpdateButtons();
        }
    }

    // Botón "Objetos": usa un objeto (curación, mejora, cura de estado) y consume el turno.
    private async void ItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (busy || finished || active == null)
            return;

        CloseAdvisor();

        busy = true;
        UpdateButtons();

        try
        {
            string? message = await ItemUI.UseItemAsync(
                XamlRoot,
                true,
                active,
                boosts,
                team,
                isGym ? GameSession.Data.GymInventory : GameSession.Data.Inventory);

            if (message == null)
                return;

            ClearLogForTurn();
            Log(message);
            UpdateUi();

            await ResolveTurnAsync(null);
        }
        finally
        {
            busy = false;
            UpdateButtons();
        }
    }

    // Botón "Ver estadísticas": elige entre tu Pokémon activo y el rival y abre su ficha.
    private async void StatsButton_Click(object sender, RoutedEventArgs e)
    {
        if (busy || active == null || Enemy == null)
            return;

        busy = true;
        UpdateButtons();

        try
        {
            var cards = new List<ChoiceCard>
            {
                Dialogs.PokemonCard(active),
                Dialogs.PokemonCard(Enemy)
            };

            int index = await Dialogs.ChooseAsync(
                XamlRoot,
                "¿De quién quieres ver las estadísticas?",
                null,
                cards,
                "Cancelar",
                2);

            if (index == 0)
                await PokemonInfo.ShowAsync(XamlRoot, active);
            else if (index == 1)
                await PokemonInfo.ShowAsync(XamlRoot, Enemy);
        }
        finally
        {
            busy = false;
            UpdateButtons();
        }
    }

    // Botón "Rendirse": tras confirmar, termina la partida (torre o desafío) como derrota.
    private async void SurrenderButton_Click(object sender, RoutedEventArgs e)
    {
        if (busy || finished)
            return;

        busy = true;
        UpdateButtons();

        try
        {
            bool confirmed = await Dialogs.ConfirmAsync(
                XamlRoot,
                "Rendirse",
                isGym
                    ? "Si te rindes, el desafío de gimnasios terminará. ¿Seguro?"
                    : "Si te rindes, la partida de la Torre Batalla terminará. ¿Seguro?",
                "Rendirme",
                "Seguir luchando");

            if (confirmed)
                LoseBattle("Te has rendido.");
        }
        finally
        {
            busy = false;
            UpdateButtons();
        }
    }

    // Botón "Mega": megaevoluciona al Pokémon activo (también con la tecla Z).
    private void MegaButton_Click(object sender, RoutedEventArgs e)
    {
        TryMegaEvolve();
    }

    // Botón "Continuar" al terminar el combate: vuelve a la pantalla anterior (torre o gimnasios).
    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
            Frame.GoBack();
        else
            Frame.Navigate(isGym ? typeof(GymPage) : typeof(BattleTowerPage));
    }


    // true si el Pokémon activo puede megaevolucionar ahora: combate en curso, no se ha usado ya, no está debilitado ni mega y lleva su megapiedra.
    private bool CanMega()
    {
        return !finished &&
            !megaUsed &&
            active != null &&
            !active.IsFainted &&
            !active.IsMega &&
            HeldItems.MegaFormFor(active).HasValue;
    }

    // Megaevoluciona al Pokémon activo (no consume turno) y lo registra en el combate. Solo una vez por combate.
    private void TryMegaEvolve()
    {
        if (busy || !CanMega() || active == null)
            return;

        int? form = HeldItems.MegaFormFor(active);

        if (!form.HasValue)
            return;

        active.MegaSpeciesId = form.Value;
        megaUsed = true;

        Log($"¡{active.Name} megaevolucionó a {active.DisplayName}!");

        UpdateUi();
        UpdateButtons();
    }

    // En combates de gimnasio, el rival megaevoluciona automáticamente si su Pokémon tiene forma mega (una sola vez).
    private void TryEnemyMega()
    {
        PokemonData? enemy = Enemy;

        if (!isGym || enemyMegaUsed || enemy == null || enemy.IsMega)
            return;

        List<PokemonSpecies> forms = PokemonDatabase.MegasOf(enemy.Id);

        if (forms.Count == 0)
            return;

        enemy.MegaSpeciesId = forms[random.Next(forms.Count)].Id;
        enemyMegaUsed = true;

        Log($"¡{enemy.Name} rival megaevolucionó a {enemy.DisplayName}!");
    }

    // Al salir del combate, limpia todo el estado temporal (megas, transformaciones, habilidades anuladas) de ambos equipos.
    private void RevertMegas()
    {
        foreach (PokemonData pokemon in team)
            pokemon.ClearBattleState();

        if (trainer != null)
        {
            foreach (PokemonData pokemon in trainer.Team)
                pokemon.ClearBattleState();
        }

        transformed.Clear();
        AbilityEffects.GasActive = false;
        AbilityEffects.Suppressed = null;
    }

    // Se ejecuta al salir de la pantalla: restaura los Pokémon a su forma normal.
    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        RevertMegas();

        base.OnNavigatedFrom(e);
    }


    // Vacía el registro de mensajes al empezar un turno nuevo.
    private void ClearLogForTurn()
    {
        log.Clear();
    }

    // Atajo para obtener la habilidad activa de un Pokémon.
    private static string Ab(PokemonData? pokemon) => AbilityEffects.Of(pokemon);

    // IA del rival: puntúa sus movimientos disponibles y elige el mejor el 70% de las veces (el 30% restante, uno al azar entre los útiles). Si no tiene movimientos usa Placaje.
    private PokemonMove ChooseEnemyMove(PokemonData foe, PokemonData target)
    {
        BattleBoosts mine = StateOf(foe);
        BattleBoosts theirs = StateOf(target);

        List<PokemonMove> usable = foe.EffectiveMoves
            .Where(m => !(mine.DisabledTurns > 0 && mine.DisabledMove == m.Name))
            .ToList();

        if (usable.Count == 0)
            return new PokemonMove("Placaje", 40, 100, "NOR", "P");

        var scored = usable
            .Select(m => new { Move = m, Score = ScoreMove(foe, target, m, mine, theirs) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();

        if (scored.Count == 0)
            return usable[random.Next(usable.Count)];

        return random.NextDouble() < 0.7
            ? scored[0].Move
            : scored[random.Next(scored.Count)].Move;
    }

    // Puntúa un movimiento para la IA. Los de daño: potencia x precisión x efectividad x STAB (0 si el objetivo lo absorbe). Los de estado tienen puntuaciones fijas según la situación (curar con poca vida, clima, pantallas, trampas, mejoras...).
    private double ScoreMove(
        PokemonData foe,
        PokemonData target,
        PokemonMove move,
        BattleBoosts mine,
        BattleBoosts theirs)
    {
        MoveEffects fx = move.Effects;
        int mySide = SideOf(foe);
        int theirSide = SideOf(target);

        if (!move.IsStatus)
        {
            if (AbilityEffects.Absorb(target, move) != null)
                return 0;

            double power = move.Power;

            if (fx.MultiMax > 1) power *= (fx.MultiMin + fx.MultiMax) / 2.0;
            if (fx.Fixed > 0 || fx.FixedLevel) power = 60;
            if (fx.Recharge) power *= 0.65;
            if (fx.SelfKo) power *= 0.25;

            return power
                * (Math.Min(move.Accuracy, 100) / 100.0)
                * BattleCalculator.Effectiveness(foe, target, move.Type)
                * (foe.CurrentTypes.Contains(move.Type) ? 1.5 : 1.0);
        }

        double hpRatio = foe.MaxHP <= 0 ? 1 : (double)foe.CurrentHP / foe.MaxHP;

        // Curación: porcentaje fijo o, si depende del clima, 67% con sol, 50% normal y 25% con otro clima.
        if (fx.Heal > 0 || fx.HealWeather)
            return hpRatio < 0.5 ? 90 : 0;

        // Descanso: el usuario se duerme 3 turnos y recupera todos sus PS.
        if (fx.Rest)
            return hpRatio < 0.4 && mine.Status == "" ? 70 : 0;

        if (fx.Weather.Length > 0)
            return weather == fx.Weather ? 0 : 40;

        if (fx.Screen == "phys")
            return physScreen[mySide] > 0 ? 0 : 40;

        if (fx.Screen == "spec")
            return specScreen[mySide] > 0 ? 0 : 40;

        if (fx.Hazard == "spikes")
            return spikes[theirSide] >= 3 ? 0 : 35;

        if (fx.Hazard == "tspikes")
            return toxicSpikes[theirSide] >= 2 ? 0 : 30;

        if (fx.Hazard == "rocks")
            return rocks[theirSide] ? 0 : 45;

        if (fx.Seed)
            return theirs.Seeded || target.CurrentTypes.Contains("PLA") ? 0 : 45;

        if (fx.TrickRoom)
            return trickRoomTurns == 0 && SpeedOf(target) > SpeedOf(foe) ? 45 : 0;

        // Protección: cada uso seguido tiene un tercio de probabilidad respecto al anterior.
        if (fx.Protect)
            return mine.ProtectStreak == 0 ? 15 : 0;

        if (fx.Focus)
            return mine.Crit ? 0 : 20;

        if (fx.Belly)
            return hpRatio > 0.7 && mine.Atk < 2 ? 45 : 0;

        if (fx.Swagger || fx.Flatter)
            return theirs.Confusion == 0 ? 40 : 0;

        if (fx.Haze)
            return theirs.Atk + theirs.SpA + theirs.Spe < -1 || mine.Atk + mine.SpA < 0 ? 30 : 0;

        if (fx.Force)
            return 20;

        double score = 0;

        foreach (StatusEffect s in fx.Statuses)
        {
            if (s.Status == "cnf")
                score = Math.Max(score, theirs.Confusion == 0 ? 45 : 0);
            else if (theirs.Status == "")
                score = Math.Max(score, 55);
        }

        foreach (StatChange c in fx.Stats)
        {
            int stage = c.Stat switch
            {
                "atk" => c.OnUser ? mine.Atk : theirs.Atk,
                "def" => c.OnUser ? mine.Def : theirs.Def,
                "spa" => c.OnUser ? mine.SpA : theirs.SpA,
                "spd" => c.OnUser ? mine.SpD : theirs.SpD,
                "acc" => c.OnUser ? mine.Acc : theirs.Acc,
                "eva" => c.OnUser ? mine.Eva : theirs.Eva,
                _ => c.OnUser ? mine.Spe : theirs.Spe
            };

            if (c.OnUser && c.Delta > 0 && stage < 2)
                score = Math.Max(score, 50);
            else if (!c.OnUser && c.Delta < 0 && stage > -2)
                score = Math.Max(score, 35);
        }

        return score;
    }


    // Devuelve a qué bando pertenece un Pokémon: 0 = jugador, 1 = rival.
    private int SideOf(PokemonData pokemon)
    {
        return team.Contains(pokemon) ? 0 : 1;
    }

    // Nombre para los mensajes (añade "rival" si es del bando contrario).
    private string NameFor(PokemonData pokemon)
    {
        return SideOf(pokemon) == 0 ? pokemon.DisplayName : $"{pokemon.DisplayName} rival";
    }

    // Actualiza si Gas Reactivo está activo (algún Pokémon en pie en combate tiene esa habilidad).
    private void RefreshGas()
    {
        AbilityEffects.GasActive = new[] { active, Enemy }
            .Any(p => p != null && !p.IsFainted && (p.BattleAbility ?? p.Ability) == "neutralizing-gas");
    }

    // Habilidad Predicción: cambia el tipo del Pokémon según el clima (Fuego, Agua, Hielo o Normal).
    private void UpdateForecast()
    {
        foreach (PokemonData? p in new[] { active, Enemy })
        {
            if (p == null || p.IsFainted || Ab(p) != "forecast")
                continue;

            string type = weather switch { "sun" => "FUE", "rain" => "AGU", "hail" => "HIE", _ => "NOR" };

            if (p.CurrentTypes.Count != 1 || p.CurrentTypes[0] != type)
            {
                p.TypeOverride = new List<string> { type };
                Log($"¡{NameFor(p)} cambia de forma con el clima!");
            }
        }
    }

    // Activa un clima durante 5 turnos y lo anuncia.
    private void SetWeather(string newWeather)
    {
        rawWeather = newWeather;
        weatherTurns = 5;

        Log(newWeather switch
        {
            "sun" => "¡El sol brilla con fuerza!",
            "rain" => "¡Ha empezado a llover!",
            "sand" => "¡Se ha desatado una tormenta de arena!",
            _ => "¡Ha empezado a granizar!"
        });

        UpdateForecast();
    }

    // Texto con el clima, Espacio Raro y los efectos de cada campo (pantallas y trampas) para mostrar en pantalla.
    private string FieldSummary()
    {
        var parts = new List<string>();

        if (rawWeather.Length > 0)
            parts.Add($"{MoveEffects.WeatherName(rawWeather)} ({weatherTurns})" + (weather.Length == 0 ? " [anulado]" : ""));

        if (trickRoomTurns > 0)
            parts.Add($"Espacio Raro ({trickRoomTurns})");

        for (int side = 0; side < 2; side++)
        {
            var items = new List<string>();

            if (physScreen[side] > 0) items.Add("Reflejo");
            if (specScreen[side] > 0) items.Add("Pantalla de Luz");
            if (spikes[side] > 0) items.Add($"Púas x{spikes[side]}");
            if (toxicSpikes[side] > 0) items.Add($"Púas Tóxicas x{toxicSpikes[side]}");
            if (rocks[side]) items.Add("Trampa Rocas");

            if (items.Count > 0)
                parts.Add($"{(side == 0 ? "Tu campo" : "Campo rival")}: {string.Join(", ", items)}");
        }

        return string.Join("  ·  ", parts);
    }

    // Transformar (Ditto): copia especie, tipos, movimientos, habilidad y mejoras de estadísticas del rival.
    private void Transform(PokemonData pokemon, PokemonData foe)
    {
        pokemon.MegaSpeciesId = foe.MegaSpeciesId ?? foe.Id;
        pokemon.TypeOverride = foe.TypeOverride == null ? null : new List<string>(foe.TypeOverride);
        pokemon.BattleMoves = foe.EffectiveMoves.Select(m => m.Clone()).ToList();
        pokemon.BattleAbility = foe.BattleAbility ?? foe.Ability;

        BattleBoosts mine = StateOf(pokemon);
        BattleBoosts theirs = StateOf(foe);

        mine.Atk = theirs.Atk;
        mine.Def = theirs.Def;
        mine.SpA = theirs.SpA;
        mine.SpD = theirs.SpD;
        mine.Spe = theirs.Spe;

        transformed.Add(pokemon);
    }

    // Aplica las habilidades que se activan al entrar un Pokémon al combate.
    private void OnEnter(PokemonData pokemon, bool byPlayer)
    {
        RefreshGas();

        string name = NameFor(pokemon);
        string ability = Ab(pokemon);
        // Rival del Pokémon que entra (null si es él mismo).
        PokemonData? foe = byPlayer ? Enemy : active;

        if (foe == pokemon)
            foe = null;

        bool foeUsable = foe != null && !foe.IsFainted;
        string foeName = foe != null ? NameFor(foe) : "";

        // Habilidades de entrada: clima (Llovizna, Sequía, Chorro Arena, Nevada), Intimidación (baja el Ataque rival), Descarga, Rastro (copia la habilidad rival), Impostor (se transforma), Inicio Lento, y las informativas (Registro, Premonición, Anticipación...).
        switch (ability)
        {
            case "drizzle":
                Log($"¡Llovizna de {name}!");
                SetWeather("rain");
                break;
            case "drought":
                Log($"¡Sequía de {name}!");
                SetWeather("sun");
                break;
            case "sand-stream":
                Log($"¡Chorro Arena de {name}!");
                SetWeather("sand");
                break;
            case "snow-warning":
                Log($"¡Nevada de {name}!");
                SetWeather("hail");
                break;
            case "intimidate":
                if (foeUsable)
                {
                    Log($"¡Intimidación de {name}!");
                    ChangeStat(foe!, "atk", -1, foeName, true);
                }

                break;
            case "download":
                if (foeUsable)
                {
                    bool special = foe!.Defense >= foe.SpDefense ? false : true;
                    Log($"¡Descarga de {name}!");
                    ChangeStat(pokemon, special ? "spa" : "atk", 1, name);
                }

                break;
            case "trace":
                if (foeUsable)
                {
                    string copied = foe!.BattleAbility ?? foe.Ability;

                    if (copied != "" && copied != "trace" && copied != "multitype")
                    {
                        pokemon.BattleAbility = copied;
                        Log($"¡{name} copia la habilidad {Abilities.Get(copied)?.Name ?? copied} de {foeName}!");
                        OnEnter(pokemon, byPlayer);
                    }
                }

                break;
            case "imposter":
                if (foeUsable)
                {
                    Transform(pokemon, foe!);
                    Log($"¡{name} se transforma en {foe!.DisplayName}!");
                }

                break;
            case "slow-start":
                StateOf(pokemon).SlowStartTurns = 5;
                Log($"¡{name} empieza con lentitud!");
                break;
            case "pressure":
                Log($"¡{name} ejerce su presión!");
                break;
            case "mold-breaker":
            case "teravolt":
            case "turboblaze":
                Log($"¡{name} rompe los esquemas!");
                break;
            case "unnerve":
                Log($"¡{name} pone nervioso al rival!");
                break;
            case "neutralizing-gas":
                Log($"¡Gas reactivo de {name} se extiende por el campo!");
                break;
            case "frisk":
                if (foeUsable && foe!.EffectiveHeldItem != null)
                    Log($"{name} registra al rival: lleva {foe.EffectiveHeldItem}.");

                break;
            case "forewarn":
                if (foeUsable && foe!.EffectiveMoves.Count > 0)
                {
                    PokemonMove strongest = foe.EffectiveMoves.OrderByDescending(m => m.Power).First();
                    Log($"¡{name} presiente {strongest.Name}!");
                }

                break;
            case "anticipation":
                if (foeUsable && foe!.EffectiveMoves.Any(m =>
                        !m.IsStatus && BattleCalculator.Effectiveness(foe, pokemon, m.Type) > 1.0))
                {
                    Log($"¡{name} se estremece!");
                }

                break;
        }

        UpdateForecast();
    }

    // Efectos al retirarse un Pokémon: Cura Natural quita el estado, Regeneración cura 1/3 de los PS, se deshacen las transformaciones y se limpian habilidad y tipos temporales.
    private void OnSwitchOut(PokemonData pokemon)
    {
        BattleBoosts state = StateOf(pokemon);
        string ability = Ab(pokemon);

        if (!pokemon.IsFainted)
        {
            if (ability == "natural-cure" && state.Status != "")
                state.Status = "";

            if (ability == "regenerator")
                pokemon.CurrentHP = Math.Min(pokemon.MaxHP, pokemon.CurrentHP + pokemon.MaxHP / 3);
        }

        if (transformed.Remove(pokemon))
        {
            pokemon.MegaSpeciesId = null;
            pokemon.BattleMoves = null;
        }

        pokemon.BattleAbility = null;
        pokemon.TypeOverride = null;
    }

    // true si el Pokémon no puede cambiar: está atrapado por un movimiento o por una habilidad del rival.
    private bool IsTrapped(PokemonData pokemon)
    {
        if (StateOf(pokemon).TrapTurns > 0)
            return true;

        PokemonData? foe = SideOf(pokemon) == 0 ? Enemy : active;

        return AbilityEffects.CannotSwitch(pokemon, foe);
    }

    // Aplica las trampas del campo a un Pokémon que entra: rocas (daño según efectividad), púas (1/8, 1/6 o 1/4 de PS según capas) y púas tóxicas (veneno; las absorben los tipo Veneno).
    private void ApplyHazards(PokemonData pokemon)
    {
        int side = SideOf(pokemon);
        string name = NameFor(pokemon);
        bool magicGuard = AbilityEffects.IsMagicGuard(pokemon);

        // Los Pokémon que vuelan o levitan no sufren púas.
        bool grounded = !pokemon.CurrentTypes.Contains("VOL") && Ab(pokemon) != "levitate";

        if (rocks[side] && !magicGuard)
        {
            double eff = TypeChart.Multiplier("ROC", pokemon.CurrentTypes);
            int damage = Math.Max(1, (int)(pokemon.MaxHP * eff / 8.0));

            pokemon.CurrentHP = Math.Max(0, pokemon.CurrentHP - damage);
            Log($"¡Las piedras puntiagudas hieren a {name}! (-{damage} PS)");
        }

        if (spikes[side] > 0 && grounded && !magicGuard && !pokemon.IsFainted)
        {
            int divisor = spikes[side] == 1 ? 8 : spikes[side] == 2 ? 6 : 4;
            int damage = Math.Max(1, pokemon.MaxHP / divisor);

            pokemon.CurrentHP = Math.Max(0, pokemon.CurrentHP - damage);
            Log($"¡Las púas hieren a {name}! (-{damage} PS)");
        }

        if (toxicSpikes[side] > 0 && grounded && !pokemon.IsFainted)
        {
            if (pokemon.CurrentTypes.Contains("VEN"))
            {
                toxicSpikes[side] = 0;
                Log($"¡{name} absorbió las púas tóxicas!");
            }
            else if (!pokemon.CurrentTypes.Contains("ACE"))
            {
                TryInflict(pokemon, toxicSpikes[side] >= 2 ? "tox" : "psn", name, false);
            }
        }
    }

    // Efectos de campo al final del turno: daño por tormenta de arena o granizo (1/16 de PS salvo inmunes), cuenta atrás del clima, de Espacio Raro y de las pantallas.
    private void ApplyFieldEndOfTurn()
    {
        if (rawWeather.Length > 0)
        {
            // Daño del clima a cada Pokémon en combate.
            foreach (PokemonData? pokemon in new[] { active, Enemy })
            {
                if (pokemon == null || pokemon.IsFainted || AbilityEffects.IsMagicGuard(pokemon))
                    continue;

                string ability = Ab(pokemon);

                bool immune = weather switch
                {
                    "sand" => pokemon.CurrentTypes.Any(t => t == "ROC" || t == "TIE" || t == "ACE") ||
                              ability == "sand-veil" || ability == "sand-rush" || ability == "sand-force" ||
                              ability == "overcoat",
                    "hail" => pokemon.CurrentTypes.Contains("HIE") ||
                              ability == "snow-cloak" || ability == "slush-rush" || ability == "ice-body" ||
                              ability == "overcoat",
                    _ => true
                };

                if (immune)
                    continue;

                int damage = Math.Max(1, pokemon.MaxHP / 16);
                pokemon.CurrentHP = Math.Max(0, pokemon.CurrentHP - damage);

                Log($"{NameFor(pokemon)} sufre por {(weather == "sand" ? "la tormenta de arena" : "el granizo")}. (-{damage} PS)");
            }

            weatherTurns--;

            if (weatherTurns <= 0)
            {
                Log(rawWeather switch
                {
                    "sun" => "Se acabó el sol.",
                    "rain" => "Ha dejado de llover.",
                    "sand" => "La tormenta de arena se ha calmado.",
                    _ => "Ha dejado de granizar."
                });

                rawWeather = "";
                UpdateForecast();
            }
        }

        if (trickRoomTurns > 0)
        {
            trickRoomTurns--;

            if (trickRoomTurns == 0)
                Log("El espacio raro vuelve a la normalidad.");
        }

        for (int side = 0; side < 2; side++)
        {
            if (physScreen[side] > 0) physScreen[side]--;
            if (specScreen[side] > 0) specScreen[side]--;
        }
    }


    // Obliga al jugador a cambiar a un Pokémon aleatorio en pie (por Remolino, Rugido, etc.). Devuelve false si no hay relevo.
    private bool ForcePlayerSwitch()
    {
        if (active == null)
            return false;

        List<PokemonData> options = team.Where(p => !p.IsFainted && p != active).ToList();

        if (options.Count == 0)
            return false;

        OnSwitchOut(active);
        StateOf(active).ResetVolatile();

        active = options[random.Next(options.Count)];
        Log($"¡{active.Name} fue arrastrado al combate!");

        OnEnter(active, true);
        ApplyHazards(active);

        return true;
    }

    // Obliga al rival a cambiar su Pokémon por otro aleatorio de los que le quedan en pie.
    private bool SwapEnemy()
    {
        if (trainer == null || Enemy == null)
            return false;

        var candidates = Enumerable.Range(enemyIndex + 1, Math.Max(0, trainer.Team.Count - enemyIndex - 1))
            .Where(i => !trainer.Team[i].IsFainted)
            .ToList();

        if (candidates.Count == 0)
            return false;

        int chosen = candidates[random.Next(candidates.Count)];
        PokemonData old = trainer.Team[enemyIndex];

        OnSwitchOut(old);
        StateOf(old).ResetVolatile();

        trainer.Team[enemyIndex] = trainer.Team[chosen];
        trainer.Team[chosen] = old;

        Log($"¡{trainer.Name} envía a {Enemy.Name}!");

        TryEnemyMega();
        OnEnter(Enemy, false);
        ApplyHazards(Enemy);

        return true;
    }

    // Fuerza el cambio del objetivo, salvo que tenga Ventosas.
    private bool ForceSwitchOf(PokemonData target)
    {
        if (Ab(target) == "suction-cups")
        {
            Log($"¡{NameFor(target)} se agarra con sus ventosas!");
            return true;
        }

        return SideOf(target) == 0 ? ForcePlayerSwitch() : SwapEnemy();
    }


    // Prioridad total del movimiento: la del propio movimiento más la bonificación de habilidad (Bromista).
    private static int PriorityOf(PokemonData pokemon, PokemonMove move)
    {
        return move.Effects.Priority + AbilityEffects.PriorityBonus(pokemon, move);
    }

    // Resuelve un turno completo: el rival elige su movimiento, se decide quién actúa primero, se ejecutan los dos ataques, se aplican los efectos de fin de turno y se comprueban los debilitados. Si playerMove es null (cambio u objeto), solo ataca el rival.
    private async Task ResolveTurnAsync(PokemonMove? playerMove)
    {
        if (active == null || Enemy == null)
            return;

        CloseAdvisor();
        advisorStale = true;

        PokemonData me = active;
        PokemonData foe = Enemy;
        PokemonMove foeMove = ChooseEnemyMove(foe, me);

        if (playerMove != null)
            ClearLogForTurn();

        actedThisTurn.Clear();

        // Orden de actuación: primero la prioridad del movimiento, luego Rezagado, luego la velocidad (invertida con Espacio Raro) y por último el azar si hay empate.
        bool playerFirst = false;

        if (playerMove != null)
        {
            int myPriority = PriorityOf(me, playerMove);
            int foePriority = PriorityOf(foe, foeMove);

            if (myPriority != foePriority)
            {
                playerFirst = myPriority > foePriority;
            }
            else if (AbilityEffects.MovesLast(me) != AbilityEffects.MovesLast(foe))
            {
                playerFirst = AbilityEffects.MovesLast(foe);
            }
            else
            {
                double mySpeed = SpeedOf(me);
                double foeSpeed = SpeedOf(foe);

                if (mySpeed == foeSpeed)
                    playerFirst = random.Next(2) == 0;
                else
                    playerFirst = trickRoomTurns > 0 ? mySpeed < foeSpeed : mySpeed > foeSpeed;
            }
        }

        // Si el jugador va primero, el rival solo contraataca si sigue en pie (y no ha sido cambiado); si no, al revés.
        if (playerMove != null && playerFirst)
        {
            await StrikeAsync(me, foe, playerMove, true);

            if (Enemy == foe && active != null && !foe.IsFainted && !active.IsFainted)
                await StrikeAsync(foe, active, foeMove, false);
        }
        else
        {
            await StrikeAsync(foe, me, foeMove, false);

            if (playerMove != null && active == me && Enemy != null && !me.IsFainted && !Enemy.IsFainted)
                await StrikeAsync(me, Enemy, playerMove, true);
        }

        // Efectos de fin de turno de cada Pokémon (veneno, quemadura, Restos...) y del campo.
        if (active != null && Enemy != null)
        {
            ApplyEndOfTurn(active, true, Enemy);
            ApplyEndOfTurn(Enemy, false, active);
        }

        ApplyFieldEndOfTurn();

        // Reinicia los efectos de un solo turno (retroceso, protección) y reduce la cuenta atrás de movimiento anulado e Inicio Lento.
        foreach (PokemonData? pokemon in new[] { active, Enemy })
        {
            if (pokemon == null)
                continue;

            BattleBoosts state = StateOf(pokemon);

            state.Flinch = false;

            if (!state.Protected)
                state.ProtectStreak = 0;

            state.Protected = false;

            if (state.DisabledTurns > 0)
            {
                state.DisabledTurns--;

                if (state.DisabledTurns == 0)
                    state.DisabledMove = "";
            }

            if (state.SlowStartTurns > 0)
                state.SlowStartTurns--;
        }

        // Comprueba si alguien se debilitó y gestiona los relevos o el final del combate.
        await ResolveFaintsAsync();
    }


    // Tipo inmune a un estado: Fuego no se quema, Eléctrico no se paraliza, Hielo no se congela.
    private static string ImmuneStatusType(string status)
    {
        return status switch
        {
            "brn" => "FUE",
            "par" => "ELE",
            "frz" => "HIE",
            _ => ""
        };
    }

    // Intenta causar un estado alterado al objetivo respetando inmunidades (habilidades, tipos, estado ya existente...). Devuelve true si se aplicó.
    private bool TryInflict(
        PokemonData target,
        string status,
        string name,
        bool announceFailure,
        PokemonData? source = null)
    {
        BattleBoosts state = StateOf(target);

        if (AbilityEffects.PreventsStatus(target, status, weather))
        {
            if (announceFailure)
                Log($"La habilidad de {name} lo protege.");

            return false;
        }

        // El retroceso no es un estado persistente: solo marca que el Pokémon pierde el turno (Fuerza Interna lo evita; Entereza sube su velocidad).
        if (status == "flc")
        {
            state.Flinch = true;

            if (Ab(target) == "steadfast")
                ChangeStat(target, "spe", 1, name);

            return true;
        }

        if (target.IsFainted)
            return false;

        // Enamoramiento: solo se aplica si no estaba ya enamorado.
        if (status == "inf")
        {
            if (state.Infatuated)
                return false;

            state.Infatuated = true;
            Log($"¡{name} se ha enamorado!");
            return true;
        }

        // Confusión: dura de 2 a 4 turnos.
        if (status == "cnf")
        {
            if (state.Confusion > 0)
            {
                if (announceFailure) Log($"{name} ya está confuso.");
                return false;
            }

            state.Confusion = random.Next(2, 5);
            Log($"¡{name} está confuso!");
            return true;
        }

        if (state.Status != "")
        {
            if (announceFailure) Log($"{name} ya tiene un problema de estado.");
            return false;
        }

        // Inmunidades por tipo: Fuego a quemaduras, Eléctrico a parálisis, Hielo a congelación; Veneno y Acero al veneno.
        string immune = ImmuneStatusType(status);
        bool poisonImmune = (status == "psn" || status == "tox") &&
            (target.CurrentTypes.Contains("VEN") || target.CurrentTypes.Contains("ACE"));

        if ((immune != "" && target.CurrentTypes.Contains(immune)) || poisonImmune)
        {
            if (announceFailure) Log($"No afecta a {name}...");
            return false;
        }

        // Se aplica el estado. El sueño dura de 2 a 4 turnos (la mitad con Madrugador).
        state.Status = status;
        state.ToxicCounter = 0;

        if (status == "slp")
        {
            int turns = random.Next(2, 5);

            state.StatusTurns = Ab(target) == "early-bird" ? Math.Max(1, turns / 2) : turns;
        }
        else
        {
            state.StatusTurns = 0;
        }

        Log(status switch
        {
            "brn" => $"¡{name} se ha quemado!",
            "par" => $"¡{name} está paralizado! Le costará moverse.",
            "psn" => $"¡{name} ha sido envenenado!",
            "tox" => $"¡{name} ha sido gravemente envenenado!",
            "slp" => $"¡{name} se ha dormido!",
            _ => $"¡{name} se ha congelado!"
        });

        // Sincronía: devuelve quemadura, parálisis o veneno a quien se los causó.
        if (Ab(target) == "synchronize" && source != null && !source.IsFainted &&
            (status == "brn" || status == "par" || status == "psn" || status == "tox"))
        {
            Log($"¡La sincronía de {name} devuelve el estado!");
            TryInflict(source, status, NameFor(source), false);
        }

        return true;
    }

    // Sube o baja una estadística (límite entre -6 y +6) considerando habilidades: Simple duplica el cambio, Respondón lo invierte, Cuerpo Puro y similares bloquean bajadas, Competitivo y Rebeldía reaccionan.
    private void ChangeStat(PokemonData target, string stat, int delta, string name, bool byOpponent = false)
    {
        string ability = Ab(target);

        if (ability == "simple") delta *= 2;
        if (ability == "contrary") delta = -delta;

        if (delta < 0 && byOpponent && AbilityEffects.PreventsStatDrop(target, stat))
        {
            Log($"La habilidad de {name} impide que baje su {MoveEffects.StatName(stat)}.");
            return;
        }

        BattleBoosts s = StateOf(target);

        int current = stat switch
        {
            "atk" => s.Atk,
            "def" => s.Def,
            "spa" => s.SpA,
            "spd" => s.SpD,
            "acc" => s.Acc,
            "eva" => s.Eva,
            _ => s.Spe
        };

        int updated = Math.Clamp(current + delta, -6, 6);

        if (updated == current)
        {
            Log($"{MoveEffects.StatName(stat)} de {name} no puede {(delta > 0 ? "subir" : "bajar")} más.");
            return;
        }

        switch (stat)
        {
            case "atk": s.Atk = updated; break;
            case "def": s.Def = updated; break;
            case "spa": s.SpA = updated; break;
            case "spd": s.SpD = updated; break;
            case "acc": s.Acc = updated; break;
            case "eva": s.Eva = updated; break;
            default: s.Spe = updated; break;
        }

        string degree = Math.Abs(delta) >= 2 ? " mucho" : "";

        Log($"¡{MoveEffects.StatName(stat)} de {name} {(delta > 0 ? "subió" : "bajó")}{degree}!");

        if (delta < 0 && byOpponent)
        {
            if (ability == "defiant")
                ChangeStat(target, "atk", 2, name);
            else if (ability == "competitive")
                ChangeStat(target, "spa", 2, name);
        }
    }

    // Comprueba si el Pokémon puede actuar este turno: recarga, retroceso, Holgazán, sueño, congelación (20% de descongelarse), parálisis (25% de fallar), enamoramiento (50%) y confusión (33% de herirse). Devuelve false si pierde el turno.
    private bool CanAct(PokemonData attacker, BattleBoosts state, string name)
    {
        if (state.MustRecharge)
        {
            state.MustRecharge = false;
            Log($"¡{name} debe recargar energía!");
            return false;
        }

        if (state.Flinch)
        {
            state.Flinch = false;
            Log($"¡{name} retrocedió y no pudo atacar!");
            return false;
        }

        if (Ab(attacker) == "truant" && state.TruantRest)
        {
            state.TruantRest = false;
            Log($"¡{name} está holgazaneando!");
            return false;
        }

        if (state.Status == "slp")
        {
            state.StatusTurns--;

            if (state.StatusTurns <= 0)
            {
                state.Status = "";
                Log($"¡{name} se despertó!");
            }
            else
            {
                Log($"{name} está profundamente dormido.");
                return false;
            }
        }

        if (state.Status == "frz")
        {
            if (random.NextDouble() < 0.2 || (weather == "sun" && random.NextDouble() < 0.2))
            {
                state.Status = "";
                Log($"¡{name} se ha descongelado!");
            }
            else
            {
                Log($"¡{name} está congelado y no puede moverse!");
                return false;
            }
        }

        if (state.Status == "par" && random.NextDouble() < 0.25)
        {
            Log($"¡{name} está paralizado! ¡No se puede mover!");
            return false;
        }

        if (state.Infatuated && random.NextDouble() < 0.5)
        {
            Log($"¡{name} está enamorado y no puede atacar!");
            return false;
        }

        if (state.Confusion > 0)
        {
            state.Confusion--;

            if (state.Confusion == 0)
            {
                Log($"¡{name} ya no está confuso!");
            }
            else
            {
                Log($"{name} está confuso...");

                if (random.NextDouble() < 0.33)
                {
                    double dmg = ((2.0 * attacker.Level / 5.0 + 2.0) * 40 * attacker.Attack
                        / Math.Max(1, attacker.Defense)) / 50.0 + 2.0;

                    int hurt = Math.Max(1, (int)dmg);
                    attacker.CurrentHP = Math.Max(0, attacker.CurrentHP - hurt);

                    Log($"¡Se hirió a sí mismo en su confusión! ({hurt} de daño)");
                    return false;
                }
            }
        }

        return true;
    }

    // Efectos de fin de turno de un Pokémon: quemadura (1/16), veneno (1/8), veneno grave (creciente), Drenadoras, atadura, pesadillas, habilidades de clima (Cura Lluvia, Piel Seca...), curación de estados, Impulso, Veleta y Restos.
    private void ApplyEndOfTurn(PokemonData pokemon, bool byPlayer, PokemonData other)
    {
        if (pokemon.IsFainted)
            return;

        BattleBoosts state = StateOf(pokemon);
        string name = NameFor(pokemon);
        string ability = Ab(pokemon);
        bool magicGuard = AbilityEffects.IsMagicGuard(pokemon);

        int damage = 0;
        string reason = "";

        switch (state.Status)
        {
            case "brn":
                damage = Math.Max(1, pokemon.MaxHP / 16);
                reason = "sufre quemaduras";
                break;
            case "psn":
                damage = Math.Max(1, pokemon.MaxHP / 8);
                reason = "sufre por el veneno";
                break;
            case "tox":
                state.ToxicCounter = Math.Min(15, state.ToxicCounter + 1);
                damage = Math.Max(1, pokemon.MaxHP * state.ToxicCounter / 16);
                reason = "sufre por el veneno";
                break;
        }

        // Antídoto: el veneno cura en lugar de dañar.
        if (ability == "poison-heal" && (state.Status == "psn" || state.Status == "tox"))
        {
            if (pokemon.CurrentHP < pokemon.MaxHP)
            {
                int healed = Math.Max(1, pokemon.MaxHP / 8);
                pokemon.CurrentHP = Math.Min(pokemon.MaxHP, pokemon.CurrentHP + healed);
                Log($"¡El veneno cura a {name}! (+{healed} PS)");
            }

            damage = 0;
        }

        if (damage > 0 && !magicGuard)
        {
            pokemon.CurrentHP = Math.Max(0, pokemon.CurrentHP - damage);
            Log($"{name} {reason}. (-{damage} PS)");
        }

        if (state.Seeded && !pokemon.IsFainted && !magicGuard)
        {
            int drained = Math.Max(1, pokemon.MaxHP / 8);
            pokemon.CurrentHP = Math.Max(0, pokemon.CurrentHP - drained);
            Log($"¡Las Drenadoras absorben PS de {name}! (-{drained} PS)");

            if (!other.IsFainted)
            {
                if (Ab(pokemon) == "liquid-ooze")
                    pokemon.CurrentHP = Math.Max(0, pokemon.CurrentHP - drained);
                else
                    other.CurrentHP = Math.Min(other.MaxHP, other.CurrentHP + drained);
            }
        }

        if (state.TrapTurns > 0 && !pokemon.IsFainted)
        {
            state.TrapTurns--;

            if (!magicGuard)
            {
                int trap = Math.Max(1, pokemon.MaxHP / 8);
                pokemon.CurrentHP = Math.Max(0, pokemon.CurrentHP - trap);
                Log($"{name} sufre por estar atrapado. (-{trap} PS)");
            }

            if (state.TrapTurns == 0)
                Log($"¡{name} se ha liberado!");
        }

        if (Ab(other) == "bad-dreams" && state.Status == "slp" && !pokemon.IsFainted && !magicGuard)
        {
            int dream = Math.Max(1, pokemon.MaxHP / 8);
            pokemon.CurrentHP = Math.Max(0, pokemon.CurrentHP - dream);
            Log($"¡{name} sufre pesadillas! (-{dream} PS)");
        }

        if (!pokemon.IsFainted)
        {
            int weatherChange = 0;

            if (ability == "rain-dish" && weather == "rain") weatherChange = Math.Max(1, pokemon.MaxHP / 16);
            if (ability == "ice-body" && weather == "hail") weatherChange = Math.Max(1, pokemon.MaxHP / 16);
            if (ability == "dry-skin" && weather == "rain") weatherChange = Math.Max(1, pokemon.MaxHP / 8);
            if (ability == "dry-skin" && weather == "sun" && !magicGuard) weatherChange = -Math.Max(1, pokemon.MaxHP / 8);
            if (ability == "solar-power" && weather == "sun" && !magicGuard) weatherChange = -Math.Max(1, pokemon.MaxHP / 8);

            if (weatherChange > 0 && pokemon.CurrentHP < pokemon.MaxHP)
            {
                pokemon.CurrentHP = Math.Min(pokemon.MaxHP, pokemon.CurrentHP + weatherChange);
                Log($"¡{name} recupera PS gracias a su habilidad! (+{weatherChange} PS)");
            }
            else if (weatherChange < 0)
            {
                pokemon.CurrentHP = Math.Max(0, pokemon.CurrentHP + weatherChange);
                Log($"¡El sol hiere a {name}! ({weatherChange} PS)");
            }
        }

        if (!pokemon.IsFainted && state.Status != "")
        {
            if ((ability == "hydration" && weather == "rain") ||
                (ability == "shed-skin" && random.NextDouble() < 0.33))
            {
                Log($"¡La habilidad de {name} cura su problema de estado!");
                state.Status = "";
            }
        }

        if (!pokemon.IsFainted)
        {
            if (ability == "speed-boost" && state.Spe < 6)
            {
                state.Spe++;
                Log($"¡Impulso de {name} sube su Velocidad!");
            }
            else if (ability == "moody")
            {
                string[] stats = { "atk", "def", "spa", "spd", "spe" };
                string up = stats[random.Next(stats.Length)];
                string down = stats[random.Next(stats.Length)];

                Log($"¡Veleta de {name}!");
                ChangeStat(pokemon, up, 2, name);

                if (down != up)
                    ChangeStat(pokemon, down, -1, name);
            }
        }

        int heal = HeldItems.LeftoversHeal(pokemon);

        if (heal > 0 && !pokemon.IsFainted && pokemon.CurrentHP < pokemon.MaxHP)
        {
            int before = pokemon.CurrentHP;
            pokemon.CurrentHP = Math.Min(pokemon.MaxHP, pokemon.CurrentHP + heal);
            Log($"{name} recuperó {pokemon.CurrentHP - before} PS con sus Restos.");
        }
    }


    // Decide si el movimiento acierta, según la precisión, el clima (Trueno, Ventisca), habilidades y niveles de precisión/evasión.
    private bool MoveHits(PokemonData attacker, PokemonData defender, PokemonMove move, MoveEffects fx)
    {
        int accuracy = move.Accuracy;

        if (fx.Thunder)
        {
            if (weather == "rain") accuracy = 101;
            else if (weather == "sun") accuracy = 50;
        }

        if (fx.Blizzard && weather == "hail")
            accuracy = 101;

        if (AbilityEffects.NoGuard(attacker, defender))
            return true;

        if (accuracy >= 101)
            return true;

        // Probabilidad final de acertar: precisión x niveles de precisión/evasión x habilidades. Con precisión 101 el movimiento nunca falla.
        if (move.IsStatus && Ab(defender) == "wonder-skin")
            accuracy = Math.Min(accuracy, 50);

        double chance = accuracy
            * BattleBoosts.AccMult(StateOf(attacker).Acc)
            / BattleBoosts.AccMult(StateOf(defender).Eva)
            * AbilityEffects.AttackerAccuracy(attacker, move)
            * AbilityEffects.DefenderEvasion(defender, weather, StateOf(defender));

        return random.NextDouble() * 100 < chance;
    }

    // Ejecuta el ataque de un Pokémon. Si su habilidad ignora habilidades (Rompemoldes...), anula temporalmente la del defensor durante el ataque.
    private async Task StrikeAsync(
        PokemonData attacker,
        PokemonData defender,
        PokemonMove move,
        bool byPlayer)
    {
        AbilityEffects.Suppressed = AbilityEffects.IgnoresAbilities(attacker) ? defender : null;

        try
        {
            await StrikeCoreAsync(attacker, defender, move, byPlayer);
        }
        finally
        {
            AbilityEffects.Suppressed = null;
        }
    }

    // Núcleo de un ataque: comprueba si puede actuar, aplica transformaciones del movimiento (clima, Normalizar, Mutatipo), protección, precisión, movimientos de estado, absorciones, cálculo de daño (con golpes múltiples, Banda Aguante y Robustez), absorción, retroceso y efectos de habilidades por contacto.
    private async Task StrikeCoreAsync(
        PokemonData attacker,
        PokemonData defender,
        PokemonMove move,
        bool byPlayer)
    {
        BattleBoosts aState = StateOf(attacker);
        BattleBoosts dState = StateOf(defender);

        string aName = NameFor(attacker);
        string dName = NameFor(defender);

        // Si el Pokémon no puede actuar (dormido, paralizado, etc.), pierde el turno tras una breve pausa.
        if (!CanAct(attacker, aState, aName))
        {
            UpdateUi();
            await Task.Delay(650);
            return;
        }

        actedThisTurn.Add(attacker);

        if (aState.DisabledTurns > 0 && aState.DisabledMove == move.Name)
        {
            Log($"¡{move.Name} está anulado y {aName} no puede usarlo!");
            UpdateUi();
            await Task.Delay(650);
            return;
        }

        MoveEffects fx = move.Effects;

        if (Ab(attacker) == "truant")
            aState.TruantRest = true;

        Log($"{aName} usó {move.Name}.");

        if (fx.SelfKo && (Ab(active) == "damp" || Ab(Enemy) == "damp"))
        {
            Log("¡La humedad impide que se use ese movimiento!");
            UpdateUi();
            await Task.Delay(650);
            return;
        }

        // Movimiento realmente usado: puede ser una copia modificada (Bola Meteoro cambia tipo y potencia con el clima; Rayo Solar baja a la mitad con mal clima).
        PokemonMove used = move;

        if (fx.WeatherBall && weather.Length > 0)
        {
            used = move.Clone();
            used.Power = 100;
            used.Category = "S";
            used.Type = weather switch { "sun" => "FUE", "rain" => "AGU", "sand" => "ROC", _ => "HIE" };
        }
        else if (fx.Solar && (weather == "rain" || weather == "sand" || weather == "hail"))
        {
            used = move.Clone();
            used.Power = Math.Max(1, move.Power / 2);
        }

        if (Ab(attacker) == "normalize" && !used.IsStatus && used.Type != "NOR")
        {
            if (ReferenceEquals(used, move))
                used = move.Clone();

            used.Type = "NOR";
        }

        if (Ab(attacker) == "protean" && !used.IsStatus &&
            (attacker.CurrentTypes.Count != 1 || attacker.CurrentTypes[0] != used.Type))
        {
            attacker.TypeOverride = new List<string> { used.Type };
            Log($"¡{aName} cambia a tipo {TypeChart.DisplayName(used.Type)}!");
        }

        // true si el movimiento afecta al rival (y por tanto puede ser bloqueado por Protección o rebotado).
        bool hitsFoe = !used.IsStatus || fx.Statuses.Count > 0 || fx.Stats.Any(c => !c.OnUser)
            || fx.Seed || fx.Swagger || fx.Flatter || fx.Force;

        if (hitsFoe && dState.Protected)
        {
            Log($"¡{dName} se protegió!");
            UpdateUi();
            await Task.Delay(650);
            return;
        }

        // Si falla la precisión, el movimiento no tiene efecto.
        if (!MoveHits(attacker, defender, used, fx))
        {
            Log("¡Pero falló!");
            UpdateUi();
            await Task.Delay(650);
            return;
        }

        // Movimientos de estado: se aplican con ApplyStatusMove (Espejo Mágico los devuelve al atacante).
        if (used.IsStatus)
        {
            if (hitsFoe && Ab(defender) == "magic-bounce")
            {
                Log($"¡{dName} refleja el movimiento!");
                AbilityEffects.Suppressed = null;
                ApplyStatusMove(defender, attacker, used, fx, dName, aName);
            }
            else
            {
                ApplyStatusMove(attacker, defender, used, fx, aName, dName);
            }

            UpdateUi();
            await Task.Delay(700);
            return;
        }

        double effectiveness = BattleCalculator.Effectiveness(attacker, defender, used.Type);

        // Habilidades que absorben el ataque (Absorbe Agua, Pararrayos, Levitación...): curan, suben una estadística o dan inmunidad.
        string? absorb = AbilityEffects.Absorb(defender, used);

        if (absorb != null)
        {
            if (absorb == "heal")
            {
                int healed = Math.Max(1, defender.MaxHP / 4);
                int before = defender.CurrentHP;
                defender.CurrentHP = Math.Min(defender.MaxHP, defender.CurrentHP + healed);
                Log($"¡{dName} absorbió el ataque y recuperó {defender.CurrentHP - before} PS!");
            }
            else if (absorb.StartsWith("stat:"))
            {
                string body = absorb.Substring(5);
                Log($"¡{dName} absorbió el ataque con su habilidad!");
                ChangeStat(defender, body.Substring(0, 3), int.Parse(body.Substring(3)), dName);
            }
            else
            {
                Log($"¡{dName} es inmune por su habilidad!");
            }

            UpdateUi();
            await Task.Delay(700);
            return;
        }

        if (effectiveness > 0 && AbilityEffects.WonderGuardBlocks(defender, effectiveness))
        {
            Log($"¡{dName} es inmune por su habilidad Superguarda!");
            UpdateUi();
            await Task.Delay(700);
            return;
        }

        // Multiplicador de daño: solo los ataques del rival usan el multiplicador de dificultad del entrenador; las pantallas lo reducen a la mitad.
        double multiplier = byPlayer || trainer == null ? 1.0 : trainer.DamageMultiplier;
        int dSide = SideOf(defender);

        bool screened = used.IsSpecial ? specScreen[dSide] > 0 : physScreen[dSide] > 0;

        if (screened && Ab(attacker) != "infiltrator")
            multiplier *= 0.5;

        double extra = Ab(attacker) == "analytic" && actedThisTurn.Contains(defender) ? 1.3 : 1.0;

        // Número de golpes del movimiento (Encadenado siempre da el máximo).
        int hits = fx.MultiMax > 1 ? random.Next(fx.MultiMin, fx.MultiMax + 1) : 1;

        if (fx.MultiMax > 1 && AbilityEffects.SkillLink(attacker))
            hits = fx.MultiMax;

        int total = 0;
        int landed = 0;
        bool anyCrit = false;
        double lastEffectiveness = 1.0;

        // Bucle de golpes: calcula el daño de cada uno (fijo, mitad de PS o cálculo normal), aplica Banda Aguante / Robustez para sobrevivir con 1 PS y resta los PS.
        for (int i = 0; i < hits && !defender.IsFainted; i++)
        {
            int damage;
            double hitEffectiveness;

            if (fx.Halve)
            {
                hitEffectiveness = effectiveness;
                damage = effectiveness <= 0 ? 0 : Math.Max(1, defender.CurrentHP / 2);
            }
            else if (fx.Fixed > 0 || fx.FixedLevel)
            {
                hitEffectiveness = effectiveness;
                damage = effectiveness <= 0 ? 0 : (fx.FixedLevel ? attacker.Level : fx.Fixed);
            }
            else
            {
                DamageResult result = BattleCalculator.Calculate(
                    attacker, defender, used, aState, dState, multiplier, weather, extra);

                damage = result.Damage;
                hitEffectiveness = result.Effectiveness;
                anyCrit |= result.Critical;
            }

            lastEffectiveness = hitEffectiveness;

            if (damage <= 0)
            {
                if (i == 0)
                    Log($"No afecta a {dName}...");

                break;
            }

            bool sash = HeldItems.HasSash(defender) && !sashUsed.Contains(defender);
            bool sturdy = Ab(defender) == "sturdy";

            if ((sash || sturdy) &&
                defender.CurrentHP == defender.MaxHP &&
                damage >= defender.CurrentHP)
            {
                damage = defender.CurrentHP - 1;

                if (sash && !sturdy)
                    sashUsed.Add(defender);

                Log($"¡{dName} resistió el golpe!");
            }

            defender.CurrentHP = Math.Max(0, defender.CurrentHP - damage);
            total += damage;
            landed++;
        }

        AbilityEffects.Suppressed = null;

        if (landed == 0)
        {
            UpdateUi();
            await Task.Delay(650);
            return;
        }

        string text = "";

        if (anyCrit)
            text += " ¡Golpe crítico!";

        if (lastEffectiveness > 1.0)
            text += " ¡Es muy eficaz!";
        else if (lastEffectiveness < 1.0)
            text += " No es muy eficaz...";

        // Mensaje con el daño total, número de golpes, crítico y efectividad.
        Log($"Causó {total} de daño{(landed > 1 ? $" ({landed} golpes)" : "")}.{text}");

        // Absorción: el atacante recupera parte del daño (Lodo Líquido del defensor lo convierte en daño).
        if (fx.Drain > 0 && !attacker.IsFainted)
        {
            int amount = Math.Max(1, total * fx.Drain / 100);

            if (Ab(defender) == "liquid-ooze")
            {
                attacker.CurrentHP = Math.Max(0, attacker.CurrentHP - amount);
                Log($"¡{aName} sufre por el lodo líquido! (-{amount} PS)");
            }
            else
            {
                int before = attacker.CurrentHP;

                attacker.CurrentHP = Math.Min(attacker.MaxHP, attacker.CurrentHP + amount);

                if (attacker.CurrentHP > before)
                    Log($"{aName} absorbió {attacker.CurrentHP - before} PS.");
            }
        }

        // Daño al atacante: retroceso (Cabeza Roca lo evita) y Vidasfera.
        string attackerAbility = Ab(attacker);
        bool noRecoil = attackerAbility == "rock-head" || attackerAbility == "magic-guard";
        bool noIndirect = attackerAbility == "magic-guard";

        if (fx.Recoil > 0 && !attacker.IsFainted && !noRecoil)
        {
            int recoil = Math.Max(1, total * fx.Recoil / 100);
            attacker.CurrentHP = Math.Max(0, attacker.CurrentHP - recoil);
            Log($"¡{aName} sufrió {recoil} de daño por el retroceso!");
        }

        int orb = HeldItems.LifeOrbRecoil(attacker);

        if (orb > 0 && !attacker.IsFainted && !noIndirect && attackerAbility != "sheer-force")
        {
            attacker.CurrentHP = Math.Max(0, attacker.CurrentHP - orb);
            Log($"{aName} perdió {orb} PS por su Vidasfera.");
        }

        // Habilidades del defensor que reaccionan al recibir el golpe (Cambio Color, Irascible, Justiciero, Armadura Frágil, Cuerpo Maldito...).
        string defenderAbility = Ab(defender);

        if (!defender.IsFainted)
        {
            switch (defenderAbility)
            {
                case "color-change":
                    if (!defender.CurrentTypes.Contains(used.Type) || defender.CurrentTypes.Count > 1)
                    {
                        defender.TypeOverride = new List<string> { used.Type };
                        Log($"¡{dName} cambia a tipo {TypeChart.DisplayName(used.Type)}!");
                    }

                    break;
                case "anger-point":
                    if (anyCrit)
                    {
                        dState.Atk = 6;
                        Log($"¡{dName} se enfurece y maximiza su Ataque!");
                    }

                    break;
                case "justified":
                    if (used.Type == "SIN") ChangeStat(defender, "atk", 1, dName);
                    break;
                case "rattled":
                    if (used.Type == "SIN" || used.Type == "BIC" || used.Type == "FAN") ChangeStat(defender, "spe", 1, dName);
                    break;
                case "weak-armor":
                    if (!used.IsSpecial)
                    {
                        ChangeStat(defender, "def", -1, dName);
                        ChangeStat(defender, "spe", 1, dName);
                    }

                    break;
                case "stamina":
                    ChangeStat(defender, "def", 1, dName);
                    break;
                case "water-compaction":
                    if (used.Type == "AGU") ChangeStat(defender, "def", 2, dName);
                    break;
                case "cursed-body":
                    if (random.Next(100) < 30 && !attacker.IsFainted)
                    {
                        aState.DisabledMove = move.Name;
                        aState.DisabledTurns = 3;
                        Log($"¡{move.Name} de {aName} ha sido anulado por el Cuerpo Maldito!");
                    }

                    break;
            }
        }

        // Habilidades que reaccionan a un golpe de contacto: Elec. Estática, Cuerpo Llama, Punto Tóxico, Efecto Espora, Piel Tosca, Viscosidad, Momia, Ladrón, Resquicio...
        if (fx.Contact && !attacker.IsFainted)
        {
            switch (defenderAbility)
            {
                case "static":
                    if (random.Next(100) < 30) TryInflict(attacker, "par", aName, false);
                    break;
                case "flame-body":
                    if (random.Next(100) < 30) TryInflict(attacker, "brn", aName, false);
                    break;
                case "poison-point":
                    if (random.Next(100) < 30) TryInflict(attacker, "psn", aName, false);
                    break;
                case "effect-spore":
                    if (random.Next(100) < 30)
                    {
                        string[] spores = { "psn", "par", "slp" };
                        TryInflict(attacker, spores[random.Next(spores.Length)], aName, false);
                    }

                    break;
                case "cute-charm":
                    if (random.Next(100) < 30) TryInflict(attacker, "inf", aName, false);
                    break;
                case "rough-skin":
                case "iron-barbs":
                    if (!noIndirect)
                    {
                        int thorn = Math.Max(1, attacker.MaxHP / 8);
                        attacker.CurrentHP = Math.Max(0, attacker.CurrentHP - thorn);
                        Log($"¡{aName} se hirió al golpear! (-{thorn} PS)");
                    }

                    break;
                case "gooey":
                case "tangling-hair":
                    ChangeStat(attacker, "spe", -1, aName, true);
                    break;
                case "mummy":
                case "wandering-spirit":
                    if (attackerAbility != "mummy" && attackerAbility != "")
                    {
                        attacker.BattleAbility = "mummy";
                        Log($"¡La habilidad de {aName} se convierte en Momia!");
                    }

                    break;
                case "pickpocket":
                    if (defender.EffectiveHeldItem == null && attacker.EffectiveHeldItem != null &&
                        attackerAbility != "sticky-hold")
                    {
                        defender.BattleHeldItem = attacker.EffectiveHeldItem;
                        Log($"¡{dName} le roba {attacker.EffectiveHeldItem} a {aName}!");
                        attacker.BattleHeldItem = "";
                    }

                    break;
                case "aftermath":
                    if (defender.IsFainted && Ab(active) != "damp" && Ab(Enemy) != "damp" && !noIndirect)
                    {
                        int blast = Math.Max(1, attacker.MaxHP / 4);
                        attacker.CurrentHP = Math.Max(0, attacker.CurrentHP - blast);
                        Log($"¡{aName} recibe el estallido de {dName}! (-{blast} PS)");
                    }

                    break;
            }

            if (attackerAbility == "poison-touch" && !defender.IsFainted && random.Next(100) < 30)
                TryInflict(defender, "psn", dName, false, attacker);
        }

        // Efectos secundarios: Fuerza Bruta los anula (a cambio de más potencia), Polvo Escudo protege al defensor y Serena Gracia duplica las probabilidades.
        bool sheer = attackerAbility == "sheer-force" && AbilityEffects.HasSecondary(fx);
        bool shielded = defenderAbility == "shield-dust";
        double luck = attackerAbility == "serene-grace" ? 2.0 : 1.0;

        if (!defender.IsFainted && !sheer && !shielded)
        {
            foreach (StatusEffect s in fx.Statuses)
            {
                if (random.Next(100) < Math.Min(100, (int)(s.Chance * luck)))
                    TryInflict(defender, s.Status, dName, false, attacker);
            }

            if (attackerAbility == "stench" && !fx.Statuses.Any(s => s.Status == "flc") && random.Next(100) < 10)
                TryInflict(defender, "flc", dName, false, attacker);

            if (fx.Trap && dState.TrapTurns == 0)
            {
                dState.TrapTurns = random.Next(4, 6);
                Log($"¡{dName} ha quedado atrapado!");
            }
        }

        foreach (StatChange c in fx.Stats)
        {
            if (c.Chance < 100 && (sheer || (shielded && !c.OnUser)))
                continue;

            if (random.Next(100) >= Math.Min(100, (int)(c.Chance * luck)))
                continue;

            if (c.OnUser)
            {
                if (!attacker.IsFainted)
                    ChangeStat(attacker, c.Stat, c.Delta, aName);
            }
            else if (!defender.IsFainted)
            {
                ChangeStat(defender, c.Stat, c.Delta, dName, true);
            }
        }

        // Autoestima: sube el Ataque al debilitar a un rival.
        if (defender.IsFainted && attackerAbility == "moxie" && !attacker.IsFainted)
            ChangeStat(attacker, "atk", 1, aName);

        // Giro Rápido: elimina púas, rocas, Drenadoras y ataduras del campo propio.
        if (fx.Spin && !attacker.IsFainted)
        {
            int aSide = SideOf(attacker);

            if (spikes[aSide] > 0 || toxicSpikes[aSide] > 0 || rocks[aSide] || aState.Seeded || aState.TrapTurns > 0)
            {
                spikes[aSide] = 0;
                toxicSpikes[aSide] = 0;
                rocks[aSide] = false;
                aState.Seeded = false;
                aState.TrapTurns = 0;
                Log($"¡{aName} eliminó las trampas de su campo!");
            }
        }

        // Efectos finales del movimiento: recarga obligatoria, autodestrucción y cambio forzado del rival.
        if (fx.Recharge && !attacker.IsFainted)
            aState.MustRecharge = true;

        if (fx.SelfKo && !attacker.IsFainted)
        {
            attacker.CurrentHP = 0;
            Log($"¡{aName} se debilitó por el esfuerzo!");
        }

        if (fx.Force && !defender.IsFainted && !attacker.IsFainted)
            ForceSwitchOf(defender);

        UpdateUi();

        // Movimientos de cambio (Voltiocambio, Viraje): el atacante se retira tras golpear.
        if (fx.Switch && !attacker.IsFainted)
            await SwitchOutAttackerAsync(attacker, byPlayer);

        await Task.Delay(700);
    }

    // El atacante se retira tras su movimiento: el jugador elige relevo y el rival cambia al azar.
    private async Task SwitchOutAttackerAsync(PokemonData attacker, bool byPlayer)
    {
        if (byPlayer)
        {
            List<PokemonData> options = team.Where(p => !p.IsFainted && p != attacker).ToList();

            if (options.Count == 0 || active != attacker)
                return;

            int index = await Dialogs.ChooseAsync(
                XamlRoot,
                $"{attacker.Name} vuelve",
                "Elige quién entra en su lugar.",
                Dialogs.PartyCards(options));

            OnSwitchOut(attacker);
            StateOf(attacker).ResetVolatile();

            active = options[index];
            Log($"¡Adelante, {active.Name}!");

            OnEnter(active, true);
            ApplyHazards(active);
        }
        else if (Enemy == attacker)
        {
            SwapEnemy();
        }
    }

    // Aplica los efectos de un movimiento de estado en este orden: clima, pantallas, trampas, Espacio Raro, Protección, Neblina, Foco Energía, Tambor, Contoneo, Camelo, Drenadoras, cambio forzado, curación, Descanso, estados alterados y cambios de estadísticas. Si no ocurre nada, lo avisa.
    private void ApplyStatusMove(
        PokemonData attacker,
        PokemonData defender,
        PokemonMove move,
        MoveEffects fx,
        string aName,
        string dName)
    {
        BattleBoosts aState = StateOf(attacker);
        BattleBoosts dState = StateOf(defender);
        int aSide = SideOf(attacker);
        int dSide = SideOf(defender);

        // Se marca a true cuando algún efecto se intenta aplicar; si sigue false, el movimiento "no pasó nada".
        bool didSomething = false;

        if (fx.Weather.Length > 0)
        {
            if (rawWeather == fx.Weather)
                Log("¡Pero falló!");
            else
                SetWeather(fx.Weather);

            didSomething = true;
        }

        if (fx.Screen.Length > 0)
        {
            bool physical = fx.Screen == "phys";

            if ((physical ? physScreen[aSide] : specScreen[aSide]) > 0)
            {
                Log("¡Pero falló!");
            }
            else
            {
                if (physical) physScreen[aSide] = 5; else specScreen[aSide] = 5;
                Log($"¡{(physical ? "Reflejo" : "Pantalla de Luz")} protege al equipo de {aName}!");
            }

            didSomething = true;
        }

        if (fx.Hazard.Length > 0)
        {
            switch (fx.Hazard)
            {
                case "spikes":
                    if (spikes[dSide] >= 3) Log("¡Pero falló!");
                    else { spikes[dSide]++; Log("¡Hay púas esparcidas por el campo rival!"); }
                    break;
                case "tspikes":
                    if (toxicSpikes[dSide] >= 2) Log("¡Pero falló!");
                    else { toxicSpikes[dSide]++; Log("¡Hay púas tóxicas esparcidas por el campo rival!"); }
                    break;
                default:
                    if (rocks[dSide]) Log("¡Pero falló!");
                    else { rocks[dSide] = true; Log("¡Hay piedras flotando alrededor del rival!"); }
                    break;
            }

            didSomething = true;
        }

        if (fx.TrickRoom)
        {
            trickRoomTurns = trickRoomTurns > 0 ? 0 : 5;
            Log(trickRoomTurns > 0 ? "¡Se ha retorcido el espacio!" : "El espacio raro vuelve a la normalidad.");
            didSomething = true;
        }

        if (fx.Protect)
        {
            double chance = Math.Pow(1.0 / 3.0, aState.ProtectStreak);

            if (random.NextDouble() < chance)
            {
                aState.Protected = true;
                aState.ProtectStreak++;
                Log($"¡{aName} se protege!");
            }
            else
            {
                aState.ProtectStreak = 0;
                Log("¡Pero falló!");
            }

            didSomething = true;
        }

        if (fx.Haze)
        {
            foreach (PokemonData p in new[] { attacker, defender })
            {
                BattleBoosts s = StateOf(p);
                s.Atk = s.Def = s.SpA = s.SpD = s.Spe = s.Acc = s.Eva = 0;
                s.Crit = false;
            }

            Log("¡Se han anulado todos los cambios de estadísticas!");
            didSomething = true;
        }

        if (fx.Focus)
        {
            if (aState.Crit)
            {
                Log("¡Pero falló!");
            }
            else
            {
                aState.Crit = true;
                Log($"¡{aName} se está concentrando!");
            }

            didSomething = true;
        }

        if (fx.Belly)
        {
            if (attacker.CurrentHP * 2 <= attacker.MaxHP)
            {
                Log("¡Pero falló!");
            }
            else
            {
                attacker.CurrentHP -= attacker.MaxHP / 2;
                aState.Atk = 6;
                Log($"¡{aName} sacrifica PS y maximiza su Ataque!");
            }

            didSomething = true;
        }

        if (fx.Swagger)
        {
            TryInflict(defender, "cnf", dName, true, attacker);
            ChangeStat(defender, "atk", 2, dName, false);
            didSomething = true;
        }

        if (fx.Flatter)
        {
            TryInflict(defender, "cnf", dName, true, attacker);
            ChangeStat(defender, "spa", 1, dName, false);
            didSomething = true;
        }

        if (fx.Seed)
        {
            if (dState.Seeded || defender.CurrentTypes.Contains("PLA"))
            {
                Log("¡Pero falló!");
            }
            else
            {
                dState.Seeded = true;
                Log($"¡{dName} ha sido infectado por las Drenadoras!");
            }

            didSomething = true;
        }

        if (fx.Force)
        {
            if (!ForceSwitchOf(defender))
                Log("¡Pero falló!");

            didSomething = true;
        }

        if (fx.Heal > 0 || fx.HealWeather)
        {
            int percent = fx.Heal;

            if (fx.HealWeather)
            {
                percent = weather switch
                {
                    "sun" => 67,
                    "" => 50,
                    _ => 25
                };
            }

            if (attacker.CurrentHP >= attacker.MaxHP)
            {
                Log($"Los PS de {aName} ya están al máximo.");
            }
            else
            {
                int before = attacker.CurrentHP;
                attacker.CurrentHP = Math.Min(attacker.MaxHP, attacker.CurrentHP + attacker.MaxHP * percent / 100);
                Log($"{aName} recuperó {attacker.CurrentHP - before} PS.");
            }

            didSomething = true;
        }

        if (fx.Rest)
        {
            if (attacker.CurrentHP >= attacker.MaxHP || aState.Status == "slp")
            {
                Log("¡Pero falló!");
            }
            else
            {
                aState.Status = "slp";
                aState.StatusTurns = 3;
                attacker.CurrentHP = attacker.MaxHP;
                Log($"¡{aName} se durmió y recuperó toda su salud!");
            }

            didSomething = true;
        }

        // Aplica los estados alterados del movimiento según su probabilidad (y Hedor puede causar retroceso).
        foreach (StatusEffect s in fx.Statuses)
        {
            TryInflict(defender, s.Status, dName, true, attacker);
            didSomething = true;
        }

        // Aplica los cambios de estadísticas del movimiento, al usuario o al rival, según su probabilidad.
        foreach (StatChange c in fx.Stats)
        {
            if (random.Next(100) >= c.Chance)
                continue;

            ChangeStat(c.OnUser ? attacker : defender, c.Stat, c.Delta, c.OnUser ? aName : dName, !c.OnUser);
            didSomething = true;
        }

        if (!didSomething)
            Log("¡Pero no pasó nada!");
    }

    // Gestiona los Pokémon debilitados: si cae el rival entra el siguiente (o se gana el combate); si cae el del jugador, elige relevo (o se pierde).
    private async Task ResolveFaintsAsync()
    {
        PokemonData? foe = Enemy;

        if (foe != null && foe.IsFainted && trainer != null)
        {
            Log($"¡{foe.Name} rival se debilitó!");

            enemyIndex++;

            if (Enemy == null)
            {
                UpdateUi();
                await WinBattleAsync();
                return;
            }

            Log($"¡{trainer.Name} envía a {Enemy.Name}!");
            TryEnemyMega();

            OnEnter(Enemy, false);
            ApplyHazards(Enemy);
        }

        if (active != null && active.IsFainted)
        {
            Log($"¡{active.Name} se debilitó!");
            UpdateUi();

            PokemonData? next = await ForceSwitchAsync();

            if (next == null)
            {
                LoseBattle("Todos tus Pokémon están debilitados.");
                return;
            }

            active = next;

            Log($"¡Adelante, {active.Name}!");

            OnEnter(active, true);
            ApplyHazards(active);
        }

        UpdateUi();
    }

    // Pide al jugador el siguiente Pokémon tras un debilitado (automático si solo queda uno; null si no queda ninguno).
    private async Task<PokemonData?> ForceSwitchAsync()
    {
        List<PokemonData> options = team
            .Where(p => !p.IsFainted)
            .ToList();

        if (options.Count == 0)
            return null;

        if (options.Count == 1)
            return options[0];

        int index = await Dialogs.ChooseAsync(
            XamlRoot,
            "Elige tu siguiente Pokémon",
            null,
            Dialogs.PartyCards(options));

        return options[index];
    }


    // Victoria: marca el combate como terminado, registra la victoria (gimnasio o torre), muestra el resumen y, en la torre, procesa evoluciones y movimientos nuevos.
    private async Task WinBattleAsync()
    {
        if (trainer == null)
            return;

        finished = true;
        RevertMegas();

        List<string> messages = isGym
            ? GymManager.RegisterVictory(trainer)
            : TowerManager.RegisterVictory(trainer);

        MessageText.Text = isGym
            ? $"¡Has derrotado a {trainer.Name}!"
            : trainer.IsBoss
                ? "¡Has derrotado al minijefe!"
                : "¡Has vencido al entrenador!";

        log.Clear();
        Log("¡Victoria!");

        UpdateUi();
        UpdateButtons();

        await Dialogs.MessageAsync(XamlRoot, "Resumen del combate", string.Join("\n", messages));

        if (!isGym)
        {
            await TowerGrowth.ProcessAsync(XamlRoot);
        }

        UpdateUi();
        Log("Pulsa Continuar.");
    }

    // Derrota o rendición: termina el combate y registra la derrota (finaliza la partida de torre o desafío).
    private void LoseBattle(string reason)
    {
        finished = true;
        RevertMegas();

        if (isGym)
            GymManager.RegisterDefeat(trainer);
        else
            TowerManager.RegisterDefeat(trainer);

        MessageText.Text = "Has perdido";
        Log(reason);
        Log(isGym ? "Fin del desafío de gimnasios." : "Fin de la partida en la Torre Batalla.");

        UpdateButtons();
    }
}
