"""
Genera los datos del juego (especies, movimientos y movimientos aprendibles) a partir de los CSV de PokeAPI.

Uso:  python3 ImportPokeApi.py <carpeta data/v2/csv> <carpeta del proyecto> [tabla_evoluciones.txt]

Escribe:
  MoveCatalog.cs                  catálogo de movimientos (nombres en español, tipo, categoría, poder, precisión, efectos)
  PokemonDatabase.Generated.cs    especies 1-493, evoluciones y movimientos aprendibles
  PokemonDatabaseMega.Stats.txt   estadísticas oficiales de las megaevoluciones (para comprobar la tabla de megas)
"""
# Script que convierte los CSV de PokeAPI en los archivos C# de datos del juego (movimientos, especies, evoluciones, habilidades y naturalezas).
import csv, os, sys, re
from collections import defaultdict

# Argumentos: carpeta con los CSV de PokeAPI y carpeta del proyecto donde se escriben los archivos generados.
CSV_DIR, OUT_DIR = sys.argv[1], sys.argv[2]
# Tabla opcional con niveles de evolución antiguos (origen, destino -> nivel), usada cuando PokeAPI no indica nivel.
OLD_EVO = {}
if len(sys.argv) > 3 and os.path.exists(sys.argv[3]):
    for line in open(sys.argv[3], encoding='utf-8'):
        a, b, c = line.split()
        OLD_EVO[(int(a), int(b))] = int(c)

# Lee un CSV de la carpeta de PokeAPI y devuelve sus filas como diccionarios.
def rd(name):
    with open(os.path.join(CSV_DIR, name), encoding='utf-8') as f:
        return list(csv.DictReader(f))

# Solo se importan las especies hasta la 493 (generaciones 1 a 4).
MAX_SPECIES = 493
# Id del idioma español en PokeAPI.
ES = '7'

# Equivalencia entre el id de tipo de PokeAPI y la abreviatura de 3 letras del juego.
TYPE = {1: 'NOR', 2: 'LUC', 3: 'VOL', 4: 'VEN', 5: 'TIE', 6: 'ROC', 7: 'BIC', 8: 'FAN', 9: 'ACE',
        10: 'FUE', 11: 'AGU', 12: 'PLA', 13: 'ELE', 14: 'PSI', 15: 'HIE', 16: 'DRA', 17: 'SIN', 18: 'HAD'}
FAIRY = 18

# Carga de las tablas de movimientos: datos base, metadatos, banderas (contacto, puño, sonido...), cambios de estadísticas y nombres en español.
moves = {int(r['id']): r for r in rd('moves.csv')}
meta = {int(r['move_id']): r for r in rd('move_meta.csv')}
# Movimientos de contacto (bandera 1).
CONTACT = {int(r['move_id']) for r in rd('move_flag_map.csv') if r['move_flag_id'] == '1'}
# Banderas de PokeAPI que se traducen a etiquetas de efecto del juego.
FLAG_TOKENS = {'8': 'punch', '9': 'sound', '15': 'powder', '16': 'bite', '17': 'pulse', '18': 'ball'}
MOVE_FLAGS = defaultdict(list)
for r in rd('move_flag_map.csv'):
    if r['move_flag_id'] in FLAG_TOKENS:
        MOVE_FLAGS[int(r['move_id'])].append(FLAG_TOKENS[r['move_flag_id']])
# Movimientos de corte (para la habilidad Cortante).
SLICE = {'cut', 'slash', 'night-slash', 'leaf-blade', 'x-scissor', 'psycho-cut', 'cross-poison', 'air-slash',
         'air-cutter', 'razor-leaf', 'sacred-sword', 'fury-cutter', 'aerial-ace', 'razor-shell', 'solar-blade',
         'secret-sword', 'cross-chop'}
# Movimientos de viento (para la habilidad Surcavientos).
WIND = {'gust', 'twister', 'hurricane', 'air-cutter', 'heat-wave', 'blizzard', 'sandstorm', 'icy-wind', 'whirlwind',
        'tailwind', 'petal-blizzard', 'fairy-wind', 'bleakwind-storm', 'wildbolt-storm', 'sandsear-storm',
        'springtide-storm'}
stat_changes = defaultdict(list)
for r in rd('move_meta_stat_changes.csv'):
    stat_changes[int(r['move_id'])].append((int(r['stat_id']), int(r['change'])))
move_names = {}
for r in rd('move_names.csv'):
    if r['local_language_id'] == ES:
        move_names[int(r['move_id'])] = r['name']

# Equivalencia entre id de estadística de PokeAPI y su código en el juego.
STAT = {2: 'atk', 3: 'def', 4: 'spa', 5: 'spd', 6: 'spe', 7: 'acc', 8: 'eva'}
# Estados alterados de PokeAPI y su código en el juego.
AILMENT = {1: 'par', 2: 'slp', 3: 'frz', 4: 'brn', 5: 'psn', 6: 'cnf'}
# Movimientos que obligan a recargar el turno siguiente.
RECHARGE = {'hyper-beam', 'giga-impact', 'blast-burn', 'hydro-cannon', 'frenzy-plant', 'roar-of-time', 'rock-wrecker'}
# Movimientos que debilitan al usuario.
SELF_KO = {'explosion', 'self-destruct'}
# Movimientos de daño fijo (igual al nivel, o una cantidad concreta).
FIXED = {'seismic-toss': 'lvl', 'night-shade': 'lvl', 'dragon-rage': '40', 'sonic-boom': '20'}
# Movimientos de estado con efectos especiales (clima, pantallas, trampas, protección, curación por clima...).
SPECIAL_STATUS = {
    'sunny-day': 'weather:sun', 'rain-dance': 'weather:rain', 'sandstorm': 'weather:sand', 'hail': 'weather:hail',
    'leech-seed': 'seed', 'protect': 'protect', 'detect': 'protect', 'reflect': 'screen:phys',
    'light-screen': 'screen:spec', 'haze': 'haze', 'focus-energy': 'focus', 'swagger': 'swagger',
    'flatter': 'flatter', 'spikes': 'hazard:spikes', 'toxic-spikes': 'hazard:tspikes', 'stealth-rock': 'hazard:rocks',
    'trick-room': 'trickroom', 'roar': 'force', 'whirlwind': 'force', 'belly-drum': 'belly',
    'synthesis': 'healw', 'moonlight': 'healw', 'morning-sun': 'healw'}
# Movimientos de daño con un efecto adicional especial (cambio, mitad de PS, depende del clima...).
SPECIAL_DAMAGE = {
    'u-turn': 'switch', 'volt-switch': 'switch', 'dragon-tail': 'force', 'circle-throw': 'force',
    'super-fang': 'halve', 'rapid-spin': 'spin', 'weather-ball': 'wball', 'solar-beam': 'solar',
    'thunder': 'thunder', 'hurricane': 'thunder', 'blizzard': 'blizzard'}
# Movimientos que atrapan al rival varios turnos.
TRAP = {'wrap', 'bind', 'fire-spin', 'clamp', 'whirlpool', 'sand-tomb', 'magma-storm'}
# Potencia fija asignada a movimientos cuya potencia real depende de condiciones que el juego no simula.
POWER_OVERRIDE = {'super-fang': 1, 'return': 90, 'low-kick': 60, 'grass-knot': 60, 'gyro-ball': 60, 'reversal': 60, 'flail': 60,
                  'wring-out': 60, 'hidden-power': 60, 'magnitude': 70, 'punishment': 60, 'electro-ball': 60,
                  'crush-grip': 60, 'trump-card': 60, 'fling': 60, 'present': 40, 'spit-up': 60,
                  'heavy-slam': 60, 'heat-crash': 60, 'frustration': 60}

# Construye la entrada de un movimiento (nombre, tipo, categoría, potencia, precisión y texto de efectos). Devuelve None si el juego no soporta sus efectos.
def build_move(mid):
    r = moves[mid]
    ident = r['identifier']
    dc = int(r['damage_class_id'])
    mt = meta.get(mid)
    if mt is None or mid not in move_names:
        return None
    mcat = int(mt['meta_category_id'])
    ailment = int(mt['meta_ailment_id'])
    power = int(r['power']) if r['power'] else None
    acc = int(r['accuracy']) if r['accuracy'] else 101
    prio = int(r['priority'])
    target = int(r['target_id'])
    ptype = TYPE[int(r['type_id'])]
    fx = []

    # Probabilidad del efecto (100 si PokeAPI no la indica).
    def chance(value):
        return int(value) if value and int(value) > 0 else 100

    # Movimientos de estado (clase 1): se traducen según su meta-categoría (curación, estados alterados, cambios de estadísticas).
    if dc == 1:
        category = 'T'
        power = 0
        if ident in SPECIAL_STATUS:
            fx.append(SPECIAL_STATUS[ident])
        elif ident == 'rest':
            fx.append('rest')
        elif mcat == 3 and int(mt['healing'] or 0) > 0 and target == 7:
            fx.append('heal:%d' % int(mt['healing']))
        elif mcat == 1 and ailment in AILMENT:
            code = AILMENT[ailment]
            if ident == 'toxic':
                code = 'tox'
            fx.append(code)
        elif mcat == 2:
            changes = stat_changes.get(mid, [])
            if not changes or any(s not in STAT for s, _ in changes):
                return None
            who = 'u' if target in (7, 13, 5) else 't'
            for s, c in changes:
                fx.append('%s:%s%+d' % (who, STAT[s], c))
        else:
            return None
    # Movimientos de daño: categoría física o especial y efectos secundarios (estados, retroceso, drenaje, golpes múltiples, crítico, recarga...).
    else:
        category = 'P' if dc == 2 else 'S'
        if power is None:
            if ident in FIXED:
                power = 1
            elif ident in POWER_OVERRIDE:
                power = POWER_OVERRIDE[ident]
            else:
                return None
        if power == 0:
            return None
        if ident in FIXED:
            fx.append('fixed:%s' % FIXED[ident])
        if ident in SPECIAL_DAMAGE:
            fx.append(SPECIAL_DAMAGE[ident])
        if ident in TRAP:
            fx.append('trap')
        if mid in CONTACT:
            fx.append('ct')
        if ailment in AILMENT and mcat in (4, 1):
            code = AILMENT[ailment]
            if ident == 'poison-fang':
                code = 'tox'
            c = chance(mt['ailment_chance'])
            fx.append(code if c >= 100 else '%s:%d' % (code, c))
        if int(mt['flinch_chance'] or 0) > 0:
            c = int(mt['flinch_chance'])
            fx.append('flc' if c >= 100 else 'flc:%d' % c)
        if mcat in (6, 7):
            who = 't' if mcat == 6 else 'u'
            c = chance(mt['stat_chance'])
            for s, ch in stat_changes.get(mid, []):
                if s in STAT:
                    suffix = '' if c >= 100 else '@%d' % c
                    fx.append('%s:%s%+d%s' % (who, STAT[s], ch, suffix))
        drain = int(mt['drain'] or 0)
        if drain > 0:
            fx.append('drain:%d' % drain)
        elif drain < 0:
            fx.append('recoil:%d' % -drain)
        if int(mt['crit_rate'] or 0) > 0:
            fx.append('crit')
        mn, mx = mt['min_hits'], mt['max_hits']
        if mn and mx and int(mx) > 1:
            fx.append('multi:%s' % (mn if mn == mx else '%s-%s' % (mn, mx)))
        if ident in RECHARGE:
            fx.append('recharge')
        if ident in SELF_KO:
            fx.append('selfko')
    for flag in MOVE_FLAGS.get(mid, []):
        fx.append(flag)
    if ident in SLICE:
        fx.append('slice')
    if ident in WIND:
        fx.append('wind')
    if prio != 0:
        fx.append('prio:%d' % prio)
    return dict(id=mid, name=move_names[mid], type=ptype, cat=category, power=power, acc=acc, fx=';'.join(fx))

# Grupos de versiones de PokeAPI usados: para los niveles se toma el primero que tenga datos (del más reciente al más antiguo).
GROUPS_LEVEL = ['25', '20', '18', '17', '16']
GROUPS_OTHER = {'16', '17', '18', '20', '25'}
# Lectura de los movimientos aprendibles: por nivel (método 1) y por MT/tutor/huevo (métodos 2, 3 y 4).
level_rows = defaultdict(lambda: defaultdict(dict))
other_moves = defaultdict(set)
all_move_ids = set()
for r in rd('pokemon_moves.csv'):
    pid = int(r['pokemon_id'])
    if pid > MAX_SPECIES:
        continue
    vg = r['version_group_id']
    mid = int(r['move_id'])
    method = r['pokemon_move_method_id']
    if vg not in GROUPS_OTHER:
        continue
    if method == '1':
        lvl = max(1, int(r['level'] or 1))
        cur = level_rows[pid][vg].get(mid)
        if cur is None or lvl < cur:
            level_rows[pid][vg][mid] = lvl
        all_move_ids.add(mid)
    elif method in ('2', '3', '4'):
        other_moves[pid].add(mid)
        all_move_ids.add(mid)

# Catálogo final de movimientos soportados.
catalog = {}
for mid in sorted(all_move_ids):
    m = build_move(mid)
    if m:
        catalog[mid] = m

# Comprobación de que no hay dos movimientos con el mismo nombre (se usan como clave).
names_seen = {}
for m in catalog.values():
    if m['name'] in names_seen:
        raise SystemExit('Nombre duplicado: %s (%s y %s)' % (m['name'], names_seen[m['name']], m['id']))
    names_seen[m['name']] = m['id']

# Nivel mínimo para enseñar un movimiento de MT/tutor, según su potencia (los más fuertes exigen más nivel).
def machine_level(m):
    if m['cat'] == 'T':
        return 20
    p = m['power']
    if p <= 50: return 12
    if p <= 70: return 20
    if p <= 90: return 30
    if p <= 110: return 38
    return 46

# Carga de especies, nombres, estadísticas base y tipos.
species = {int(r['id']): r for r in rd('pokemon_species.csv') if int(r['id']) <= MAX_SPECIES}
sp_names = {int(r['pokemon_species_id']): r['name'] for r in rd('pokemon_species_names.csv') if r['local_language_id'] == ES}
stats = defaultdict(dict)
for r in rd('pokemon_stats.csv'):
    stats[int(r['pokemon_id'])][int(r['stat_id'])] = int(r['base_stat'])
types = defaultdict(list)
for r in sorted(rd('pokemon_types.csv'), key=lambda r: (int(r['pokemon_id']), int(r['slot']))):
    types[int(r['pokemon_id'])].append(int(r['type_id']))

# Devuelve los tipos de una especie sin repetidos.
def species_types(pid):
    out = []
    for t in types[pid]:
        if TYPE[t] not in out:
            out.append(TYPE[t])
    return out

evo_rows = defaultdict(list)
for r in rd('pokemon_evolution.csv'):
    evo_rows[int(r['evolved_species_id'])].append(r)

# Nivel de evolución por defecto cuando no hay dato (36 o 32 según el método, 30 en el resto).
def default_level(trigger):
    return {'2': 36, '3': 32}.get(trigger, 30)

# Cálculo de las evoluciones de cada especie con su nivel (el de PokeAPI, el de la tabla antigua o el de por defecto).
evolutions = defaultdict(list)
evo_report = []
for sid, sp in species.items():
    src = sp['evolves_from_species_id']
    if not src:
        continue
    src = int(src)
    rows = evo_rows.get(sid, [])
    level = None
    for r in rows:
        if r['minimum_level']:
            level = int(r['minimum_level'])
            break
    if level is None:
        level = OLD_EVO.get((src, sid))
    if level is None:
        level = default_level(rows[0]['evolution_trigger_id'] if rows else '1')
    evolutions[src].append((sid, level))

# Carga de habilidades: nombres y descripciones en español y habilidades de cada especie.
ab_rows = {int(r['id']): r for r in rd('abilities.csv')}
ab_names = {int(r['ability_id']): r['name'] for r in rd('ability_names.csv') if r['local_language_id'] == ES}
ab_text = {}
for r in sorted(rd('ability_flavor_text.csv'), key=lambda r: int(r['version_group_id'])):
    if r['language_id'] == ES:
        ab_text[int(r['ability_id'])] = ' '.join(r['flavor_text'].split())
species_abilities = defaultdict(list)
for r in sorted(rd('pokemon_abilities.csv'), key=lambda r: (int(r['pokemon_id']), int(r['slot']))):
    if int(r['pokemon_id']) <= MAX_SPECIES:
        species_abilities[int(r['pokemon_id'])].append((int(r['ability_id']), int(r['is_hidden'])))
used_abilities = sorted({a for lst in species_abilities.values() for a, _ in lst})
nature_names = {int(r['nature_id']): r['name'] for r in rd('nature_names.csv') if r['local_language_id'] == ES}
natures = rd('natures.csv')

# Escapa barras y comillas para incrustar el texto en una cadena de C#.
def cs0(t):
    return t.replace('\\', '\\\\').replace('"', '\\"')

# Líneas de datos de habilidades, habilidades por especie y naturalezas.
ab_lines = ['        "%d|%s|%s|%s"' % (a, ab_rows[a]['identifier'], cs0(ab_names.get(a, ab_rows[a]['identifier'])), cs0(ab_text.get(a, '')))
            for a in used_abilities]
sa_lines = ['        "%d|%s"' % (pid, ','.join('%d:%d' % x for x in lst)) for pid, lst in sorted(species_abilities.items())]
nat_lines = ['        "%s|%s|%s|%s|%s"' % (n['id'], n['identifier'], cs0(nature_names[int(n['id'])]), n['increased_stat_id'], n['decreased_stat_id'])
             for n in natures]
# Escribe Abilities.Generated.cs.
open(os.path.join(OUT_DIR, 'Abilities.Generated.cs'), 'w', encoding='utf-8').write('''namespace PKPrueba1;

/// <summary>
/// Habilidades y naturalezas generadas a partir de PokeAPI (Tools/ImportPokeApi.py). No editar a mano.
/// RawAbilities:        id|identificador|Nombre|Descripción
/// RawSpeciesAbilities: idEspecie|idHabilidad:oculta,...
/// RawNatures:          id|identificador|Nombre|estadística que sube|estadística que baja (2 Atq, 3 Def, 4 AtqEsp, 5 DefEsp, 6 Vel)
/// </summary>
public static partial class Abilities
{
    private static readonly string[] RawAbilities =
    {
%s
    };

    private static readonly string[] RawSpeciesAbilities =
    {
%s
    };

    private static readonly string[] RawNatures =
    {
%s
    };
}
''' % (',\n'.join(ab_lines), ',\n'.join(sa_lines), ',\n'.join(nat_lines)))

# Escapa barras y comillas para incrustar el texto en una cadena de C#.
def cs(s):
    return s.replace('\\', '\\\\').replace('"', '\\"')

# Escribe MoveCatalog.cs con todos los movimientos del catálogo.
move_lines = ['        "%d|%s|%s|%s|%d|%d|%s"' % (m['id'], cs(m['name']), m['type'], m['cat'], m['power'], m['acc'], m['fx'])
              for m in catalog.values()]
open(os.path.join(OUT_DIR, 'MoveCatalog.cs'), 'w', encoding='utf-8').write('''using System;
using System.Collections.Generic;

namespace PKPrueba1;

/// <summary>
/// Catálogo de movimientos generado a partir de los datos de PokeAPI (Tools/ImportPokeApi.py).
/// Formato: id|Nombre|Tipo|Categoría (P físico, S especial, T estado)|Poder|Precisión (101 = nunca falla)|Efectos
/// Solo se incluyen los movimientos cuyos efectos soporta el juego.
/// </summary>
public static class MoveCatalog
{
    private static readonly string[] Raw =
    {
%s
    };

    private static readonly Dictionary<string, PokemonMove> Moves = new();
    private static readonly Dictionary<int, string> NamesById = new();

    static MoveCatalog()
    {
        foreach (string line in Raw)
        {
            string[] p = line.Split('|');
            int id = int.Parse(p[0]);

            Moves[p[1]] = new PokemonMove(
                p[1],
                int.Parse(p[4]),
                int.Parse(p[5]),
                p[2],
                p[3],
                p.Length > 6 ? p[6] : "");

            NamesById[id] = p[1];
        }
    }

    public static bool Exists(string name) => Moves.ContainsKey(name);

    public static IEnumerable<PokemonMove> All => Moves.Values;

    /// <summary>Nombre del movimiento a partir de su número en la base de datos (null si no existe).</summary>
    public static string? NameOf(int id) => NamesById.TryGetValue(id, out string? name) ? name : null;

    public static PokemonMove? Create(string name)
    {
        return Moves.TryGetValue(name, out PokemonMove? move) ? move.Clone() : null;
    }
}
''' % ',\n'.join(move_lines))

# Especies legendarias y míticas.
legend = sorted(sid for sid, sp in species.items() if sp['is_legendary'] == '1' or sp['is_mythical'] == '1')
# Generación de las líneas de especies (con evoluciones) y de movimientos aprendibles de cada una.
sp_lines = []
learn_lines = []
learn_total = 0
for sid in sorted(species):
    st = stats[sid]
    ts = species_types(sid)
    evos = ','.join('%d:%d' % e for e in evolutions.get(sid, []))
    sp_lines.append('        "%d|%s|%s|%s|%d,%d,%d,%d,%d,%d|%s"' % (
        sid, cs(sp_names[sid]), ts[0], ts[1] if len(ts) > 1 else '-',
        st[1], st[2], st[3], st[4], st[5], st[6], evos))
    lv = {}
    for vg in GROUPS_LEVEL:
        if level_rows[sid].get(vg):
            lv = level_rows[sid][vg]
            break
    tokens = []
    for mid, lvl in sorted(lv.items(), key=lambda x: (x[1], x[0])):
        if mid in catalog:
            tokens.append('%d@%d' % (mid, lvl))
    for mid in sorted(other_moves[sid]):
        if mid in catalog and mid not in lv:
            tokens.append('m%d@%d' % (mid, machine_level(catalog[mid])))
    learn_total += len(tokens)
    learn_lines.append('        "%d|%s"' % (sid, ','.join(tokens)))

open(os.path.join(OUT_DIR, 'PokemonDatabase.Generated.cs'), 'w', encoding='utf-8').write('''namespace PKPrueba1;

/// <summary>
/// Datos de las generaciones 1 a 4 generados a partir de PokeAPI (Tools/ImportPokeApi.py). No editar a mano.
/// RawSpecies:  id|Nombre|Tipo1|Tipo2|PS,Atq,Def,AtqEsp,DefEsp,Vel|evoluciones (id:nivel,...)
/// RawLearn:    id|movimientos. "idMov@nivel" se aprende al subir de nivel; "midMov@nivel" (con m) se enseña
///              (MT, MO, tutores y movimientos huevo) y está disponible a partir de ese nivel.
/// </summary>
public static partial class PokemonDatabase
{
    private static readonly string[] RawSpecies =
    {
%s
    };

    private static readonly string[] RawLearn =
    {
%s
    };

    private const string LegendaryList = "%s";
}
''' % (',\n'.join(sp_lines), ',\n'.join(learn_lines), ','.join(map(str, legend))))

# Exporta a un archivo de texto las estadísticas oficiales de las megaevoluciones, para comprobar la tabla del juego.
poke = {int(r['id']): r for r in rd('pokemon.csv')}
with open(os.path.join(OUT_DIR, 'PokemonDatabaseMega.Stats.txt'), 'w', encoding='utf-8') as f:
    for pid, r in sorted(poke.items()):
        if 'mega' in r['identifier'] and pid in stats:
            ts = [TYPE[t] for t in types[pid]]
            s = stats[pid]
            f.write('%d|%s|%s|%s\n' % (pid, r['identifier'], '/'.join(ts), ','.join(str(s[i]) for i in range(1, 7))))

# Resumen final por consola.
print('Movimientos en el catálogo:', len(catalog))
print('Especies:', len(sp_lines), '| legendarios:', len(legend))
print('Entradas de aprendizaje:', learn_total)
