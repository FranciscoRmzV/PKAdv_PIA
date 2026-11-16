using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PKPrueba1;

// Efecto de estado alterado de un movimiento (por ejemplo "quemar con 10% de probabilidad").
public class StatusEffect
{
    // Código del estado: brn, par, psn, tox, slp, frz, cnf (confusión) o flc (retroceso).
    public string Status { get; set; } = "";
    // Probabilidad en porcentaje de que se aplique.
    public int Chance { get; set; } = 100;
}

// Cambio de estadística provocado por un movimiento.
public class StatChange
{
    // true si afecta al usuario del movimiento; false si afecta al rival.
    public bool OnUser { get; set; }

    // Estadística afectada: atk, def, spa, spd, spe, acc o eva.
    public string Stat { get; set; } = "";
    // Niveles que sube (positivo) o baja (negativo).
    public int Delta { get; set; }
    public int Chance { get; set; } = 100;
}

// Efectos secundarios de un movimiento, obtenidos al interpretar el texto del campo Effect (por ejemplo "brn:10;punch").
public class MoveEffects
{
    // Caché para no volver a interpretar el mismo texto varias veces.
    private static readonly Dictionary<string, MoveEffects> Cache = new();

    // Estados alterados que puede causar.
    public List<StatusEffect> Statuses { get; } = new();
    // Cambios de estadísticas que provoca.
    public List<StatChange> Stats { get; } = new();

    // Porcentaje del daño que el usuario recupera como PS.
    public int Drain { get; set; }
    // Porcentaje del daño que el usuario recibe como retroceso.
    public int Recoil { get; set; }
    // Porcentaje de PS máximos que cura al usuario.
    public int Heal { get; set; }
    // La curación depende del clima.
    public bool HealWeather { get; set; }
    // Tiene probabilidad de crítico elevada.
    public bool HighCrit { get; set; }
    // El usuario debe recargar el turno siguiente.
    public bool Recharge { get; set; }
    // El usuario se debilita al usarlo (Explosión, Autodestrucción).
    public bool SelfKo { get; set; }
    // El usuario se duerme y recupera todos sus PS (Descanso).
    public bool Rest { get; set; }
    // Prioridad del movimiento: mayor valor ataca antes, independientemente de la velocidad.
    public int Priority { get; set; }
    // Número mínimo y máximo de golpes (movimientos de varios golpes).
    public int MultiMin { get; set; } = 1;
    public int MultiMax { get; set; } = 1;
    // Daño fijo, independiente de las estadísticas (0 = no aplica).
    public int Fixed { get; set; }
    // El daño es igual al nivel del usuario.
    public bool FixedLevel { get; set; }

    // Clima que provoca: sun, rain, sand o hail.
    public string Weather { get; set; } = "";
    // Pantalla que levanta: "phys" (contra físicos) o "spec" (contra especiales).
    public string Screen { get; set; } = "";
    // Trampa que deja en el campo rival: spikes, tspikes (tóxicas) o rocks.
    public string Hazard { get; set; } = "";
    // Planta Drenadoras en el rival.
    public bool Seed { get; set; }
    // Atrapa al rival durante varios turnos.
    public bool Trap { get; set; }
    // Protege al usuario este turno.
    public bool Protect { get; set; }
    // Anula todos los cambios de estadísticas.
    public bool Haze { get; set; }
    // Aumenta la probabilidad de crítico (Foco Energía).
    public bool Focus { get; set; }
    // Confunde al rival y le sube el Ataque.
    public bool Swagger { get; set; }
    // Confunde al rival y le sube el Ataque Especial.
    public bool Flatter { get; set; }
    // Gasta la mitad de los PS para subir el Ataque al máximo.
    public bool Belly { get; set; }
    // Invierte el orden de velocidad.
    public bool TrickRoom { get; set; }
    // Obliga al rival a cambiar de Pokémon.
    public bool Force { get; set; }
    // El usuario cambia de Pokémon después de atacar.
    public bool Switch { get; set; }
    // Quita la mitad de los PS actuales del rival.
    public bool Halve { get; set; }
    // Elimina trampas y púas del campo propio.
    public bool Spin { get; set; }
    // Es un movimiento de contacto directo.
    public bool Contact { get; set; }
    // Su tipo y potencia cambian según el clima.
    public bool WeatherBall { get; set; }
    // Movimientos con comportamiento especial según el clima (tipo Rayo Solar, Trueno y Ventisca).
    public bool Solar { get; set; }
    public bool Thunder { get; set; }
    public bool Blizzard { get; set; }

    // Etiquetas de clase de movimiento (puño, sonido, polvo, mordisco, pulso, bola, corte, viento) que usan ciertas habilidades.
    public bool Punch { get; set; }
    public bool Sound { get; set; }
    public bool Powder { get; set; }
    public bool Bite { get; set; }
    public bool Pulse { get; set; }
    public bool Ball { get; set; }
    public bool Slice { get; set; }
    public bool Wind { get; set; }

    // Códigos que se reconocen como estados alterados.
    private static readonly HashSet<string> StatusCodes = new() { "brn", "par", "psn", "tox", "slp", "frz", "cnf", "flc" };

    // Interpreta el texto de efectos (valores separados por ";") y devuelve el objeto de efectos. Usa caché.
    public static MoveEffects Parse(string? text)
    {
        text ??= "";

        if (Cache.TryGetValue(text, out MoveEffects? cached))
            return cached;

        var fx = new MoveEffects();

        foreach (string raw in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string token = raw.Trim();

            // Cambio de estadística: "t:" afecta al objetivo y "u:" al usuario. Formato estadística+/-nivel, con "@prob" opcional (p. ej. "u:atk+2" o "t:spd-1@10").
            if (token.StartsWith("t:") || token.StartsWith("u:"))
            {
                bool user = token[0] == 'u';
                string body = token.Substring(2);
                int chance = 100;

                int at = body.IndexOf('@');

                if (at >= 0)
                {
                    chance = int.Parse(body.Substring(at + 1), CultureInfo.InvariantCulture);
                    body = body.Substring(0, at);
                }

                int sign = body.IndexOfAny(new[] { '+', '-' });

                if (sign > 0)
                {
                    fx.Stats.Add(new StatChange
                    {
                        OnUser = user,
                        Stat = body.Substring(0, sign),
                        Delta = int.Parse(body.Substring(sign), CultureInfo.InvariantCulture),
                        Chance = chance
                    });
                }

                continue;
            }

            // Resto de efectos con formato "clave" o "clave:valor".
            string key = token;
            string value = "";
            int colon = token.IndexOf(':');

            if (colon >= 0)
            {
                key = token.Substring(0, colon);
                value = token.Substring(colon + 1);
            }

            // Estado alterado con probabilidad opcional (p. ej. "brn:10").
            if (StatusCodes.Contains(key))
            {
                fx.Statuses.Add(new StatusEffect
                {
                    Status = key,
                    Chance = value.Length > 0 ? int.Parse(value, CultureInfo.InvariantCulture) : 100
                });

                continue;
            }

            // Efectos simples: activa la propiedad correspondiente según la clave.
            switch (key)
            {
                case "drain": fx.Drain = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "recoil": fx.Recoil = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "heal": fx.Heal = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "healw": fx.HealWeather = true; break;
                case "crit": fx.HighCrit = true; break;
                case "recharge": fx.Recharge = true; break;
                case "selfko": fx.SelfKo = true; break;
                case "rest": fx.Rest = true; break;
                case "prio": fx.Priority = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "multi":
                    string[] range = value.Split('-');
                    fx.MultiMin = int.Parse(range[0], CultureInfo.InvariantCulture);
                    fx.MultiMax = range.Length > 1 ? int.Parse(range[1], CultureInfo.InvariantCulture) : fx.MultiMin;
                    break;
                case "fixed":
                    if (value == "lvl") fx.FixedLevel = true;
                    else fx.Fixed = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "weather": fx.Weather = value; break;
                case "screen": fx.Screen = value; break;
                case "hazard": fx.Hazard = value; break;
                case "seed": fx.Seed = true; break;
                case "trap": fx.Trap = true; break;
                case "protect": fx.Protect = true; break;
                case "haze": fx.Haze = true; break;
                case "focus": fx.Focus = true; break;
                case "swagger": fx.Swagger = true; break;
                case "flatter": fx.Flatter = true; break;
                case "belly": fx.Belly = true; break;
                case "trickroom": fx.TrickRoom = true; break;
                case "force": fx.Force = true; break;
                case "switch": fx.Switch = true; break;
                case "halve": fx.Halve = true; break;
                case "spin": fx.Spin = true; break;
                case "ct": fx.Contact = true; break;
                case "wball": fx.WeatherBall = true; break;
                case "solar": fx.Solar = true; break;
                case "thunder": fx.Thunder = true; break;
                case "blizzard": fx.Blizzard = true; break;
                case "punch": fx.Punch = true; break;
                case "sound": fx.Sound = true; break;
                case "powder": fx.Powder = true; break;
                case "bite": fx.Bite = true; break;
                case "pulse": fx.Pulse = true; break;
                case "ball": fx.Ball = true; break;
                case "slice": fx.Slice = true; break;
                case "wind": fx.Wind = true; break;
            }
        }

        // Guarda el resultado en la caché.
        Cache[text] = fx;
        return fx;
    }

    // Traduce el código de un estado alterado a texto en español.
    public static string StatusName(string code)
    {
        return code switch
        {
            "brn" => "Quemar",
            "par" => "Paralizar",
            "psn" => "Envenenar",
            "tox" => "Envenenar gravemente",
            "slp" => "Dormir",
            "frz" => "Congelar",
            "cnf" => "Confundir",
            "flc" => "Retroceso",
            _ => code
        };
    }

    // Traduce el código de una estadística a su nombre en español.
    public static string StatName(string code)
    {
        return code switch
        {
            "atk" => "Ataque",
            "def" => "Defensa",
            "spa" => "Ataque Esp.",
            "spd" => "Defensa Esp.",
            "spe" => "Velocidad",
            "acc" => "Precisión",
            "eva" => "Evasión",
            _ => code
        };
    }

    // Traduce el código de un clima a su nombre en español.
    public static string WeatherName(string code)
    {
        return code switch
        {
            "sun" => "Sol",
            "rain" => "Lluvia",
            "sand" => "Tormenta de arena",
            "hail" => "Granizo",
            _ => ""
        };
    }

    // Genera una descripción legible de todos los efectos del movimiento, separados por " · ".
    public string Describe()
    {
        var parts = new List<string>();

        if (Weather.Length > 0) parts.Add($"Clima: {WeatherName(Weather)}");
        if (Screen == "phys") parts.Add("Pantalla contra físicos");
        if (Screen == "spec") parts.Add("Pantalla contra especiales");
        if (Hazard == "spikes") parts.Add("Púas en el campo rival");
        if (Hazard == "tspikes") parts.Add("Púas tóxicas en el campo rival");
        if (Hazard == "rocks") parts.Add("Rocas en el campo rival");
        if (Seed) parts.Add("Drena PS cada turno");
        if (Trap) parts.Add("Atrapa al rival");
        if (Protect) parts.Add("Protege este turno");
        if (Haze) parts.Add("Anula los cambios de estadísticas");
        if (Focus) parts.Add("Sube el crítico");
        if (Swagger) parts.Add("Confunde y sube Ataque rival");
        if (Flatter) parts.Add("Confunde y sube At. Esp. rival");
        if (Belly) parts.Add("Pierde mitad de PS: Ataque al máximo");
        if (TrickRoom) parts.Add("Invierte el orden de velocidad");
        if (Force) parts.Add("Cambia al rival");
        if (Switch) parts.Add("Cambia de Pokémon tras atacar");
        if (Halve) parts.Add("Quita la mitad de PS");
        if (Spin) parts.Add("Elimina trampas y púas");

        foreach (StatusEffect s in Statuses)
            parts.Add(s.Chance >= 100 ? StatusName(s.Status) : $"{StatusName(s.Status)} {s.Chance}%");

        foreach (StatChange c in Stats)
        {
            string who = c.OnUser ? "propio" : "rival";
            string change = c.Delta > 0 ? $"+{c.Delta}" : c.Delta.ToString();
            string chance = c.Chance >= 100 ? "" : $" {c.Chance}%";

            parts.Add($"{StatName(c.Stat)} {who} {change}{chance}");
        }

        if (Drain > 0) parts.Add($"Absorbe {Drain}%");
        if (Recoil > 0) parts.Add($"Retroceso {Recoil}%");
        if (Heal > 0) parts.Add($"Cura {Heal}% PS");
        if (HealWeather) parts.Add("Cura según el clima");
        if (Rest) parts.Add("Duerme y se cura");
        if (HighCrit) parts.Add("Crítico alto");
        if (Recharge) parts.Add("Recarga 1 turno");
        if (SelfKo) parts.Add("Se debilita");
        if (Priority > 0) parts.Add($"Prioridad +{Priority}");
        if (Priority < 0) parts.Add($"Prioridad {Priority}");
        if (MultiMax > 1) parts.Add(MultiMin == MultiMax ? $"{MultiMax} golpes" : $"{MultiMin}-{MultiMax} golpes");
        if (FixedLevel) parts.Add("Daño = nivel");
        else if (Fixed > 0) parts.Add($"Daño fijo {Fixed}");
        if (WeatherBall) parts.Add("Tipo y poder según el clima");
        if (Contact) parts.Add("Contacto");

        return string.Join(" · ", parts);
    }
}
