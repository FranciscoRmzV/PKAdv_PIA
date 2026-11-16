using System.Collections.Generic;
using System.Linq;

namespace PKPrueba1;

// Parte de PokemonDatabase dedicada a las megaevoluciones.
public static partial class PokemonDatabase
{
    // Datos en bruto de cada mega: id|id del Pokémon base|nombre|tipo1|tipo2|PS,Atq,Def,AtqEsp,DefEsp,Vel|nombre de la megapiedra|sprite de la piedra.
    private static readonly string[] RawMegas =
    {
        "10033|3|Mega Venusaur|PLA|VEN|80,100,123,122,120,80|Venusaurita|venusaurite",
        "10034|6|Mega Charizard X|FUE|DRA|78,130,111,130,85,100|Charizardita X|charizardite-x",
        "10035|6|Mega Charizard Y|FUE|VOL|78,104,78,159,115,100|Charizardita Y|charizardite-y",
        "10036|9|Mega Blastoise|AGU|-|79,103,120,135,115,78|Blastoisita|blastoisinite",
        "10037|65|Mega Alakazam|PSI|-|55,50,65,175,105,150|Alakazamita|alakazite",
        "10038|94|Mega Gengar|FAN|VEN|60,65,80,170,95,130|Gengarita|gengarite",
        "10039|115|Mega Kangaskhan|NOR|-|105,125,100,60,100,100|Kangaskhanita|kangaskhanite",
        "10040|127|Mega Pinsir|BIC|VOL|65,155,120,65,90,105|Pinsirita|pinsirite",
        "10041|130|Mega Gyarados|AGU|SIN|95,155,109,70,130,81|Gyaradosita|gyaradosite",
        "10042|142|Mega Aerodactyl|ROC|VOL|80,135,85,70,95,150|Aerodactylita|aerodactylite",
        "10045|181|Mega Ampharos|ELE|DRA|90,95,105,165,110,45|Ampharosita|ampharosite",
        "10046|212|Mega Scizor|BIC|ACE|70,150,140,65,100,75|Scizorita|scizorite",
        "10047|214|Mega Heracross|BIC|LUC|80,185,115,40,105,75|Heracrossita|heracronite",
        "10048|229|Mega Houndoom|SIN|FUE|75,90,90,140,90,115|Houndoomita|houndoominite",
        "10049|248|Mega Tyranitar|ROC|SIN|100,164,150,95,120,71|Tyranitarita|tyranitarite",
        "10050|257|Mega Blaziken|FUE|LUC|80,160,80,130,80,100|Blazikenita|blazikenite",
        "10051|282|Mega Gardevoir|PSI|HAD|68,85,65,165,135,100|Gardevoirita|gardevoirite",
        "10052|303|Mega Mawile|ACE|HAD|50,105,125,55,95,50|Mawilita|mawilite",
        "10053|306|Mega Aggron|ACE|-|70,140,230,60,80,50|Aggronita|aggronite",
        "10054|308|Mega Medicham|LUC|PSI|60,100,85,80,85,100|Medichamita|medichamite",
        "10055|310|Mega Manectric|ELE|-|70,75,80,135,80,135|Manectricita|manectite",
        "10056|354|Mega Banette|FAN|-|64,165,75,93,83,75|Banettita|banettite",
        "10057|359|Mega Absol|SIN|-|65,150,60,115,60,115|Absolita|absolite",
        "10058|445|Mega Garchomp|DRA|TIE|108,170,115,120,95,92|Garchompita|garchompite",
        "10059|448|Mega Lucario|LUC|ACE|70,145,88,140,70,112|Lucarita|lucarionite",
        "10060|460|Mega Abomasnow|PLA|HIE|90,132,105,132,105,30|Abomasnita|abomasite",
        "10064|260|Mega Swampert|AGU|TIE|100,150,110,95,110,70|Swampertita|swampertite",
        "10065|254|Mega Sceptile|PLA|DRA|70,110,75,145,85,145|Sceptilita|sceptilite",
        "10066|302|Mega Sableye|SIN|FAN|50,85,125,85,115,20|Sablenita|sablenite",
        "10067|334|Mega Altaria|DRA|HAD|75,110,110,110,105,80|Altarianita|altarianite",
        "10068|475|Mega Gallade|PSI|LUC|68,165,95,65,115,110|Galladita|galladite",
        "10070|319|Mega Sharpedo|AGU|SIN|70,140,70,110,65,105|Sharpedonita|sharpedonite",
        "10071|80|Mega Slowbro|AGU|PSI|95,75,180,130,80,30|Slowbronita|slowbronite",
        "10072|208|Mega Steelix|ACE|TIE|75,125,230,55,95,30|Steelixita|steelixite",
        "10073|18|Mega Pidgeot|NOR|VOL|83,80,80,135,80,121|Pidgeotita|pidgeotite",
        "10074|362|Mega Glalie|HIE|-|80,120,80,120,80,100|Glalitita|glalitite",
        "10076|376|Mega Metagross|ACE|PSI|80,145,150,105,110,110|Metagrossita|metagrossite",
        "10087|323|Mega Camerupt|FUE|TIE|70,120,100,145,105,20|Cameruptita|cameruptite",
        "10088|428|Mega Lopunny|NOR|LUC|65,136,94,54,96,135|Lopunnita|lopunnite",
        "10089|373|Mega Salamence|DRA|VOL|95,145,130,120,90,120|Salamencita|salamencite",
        "10090|15|Mega Beedrill|BIC|VEN|65,150,40,15,80,145|Beedrillita|beedrillite",
        "10043|150|Mega Mewtwo X|PSI|LUC|106,190,100,154,100,130|Mewtwonita X|mewtwonite-x",
        "10044|150|Mega Mewtwo Y|PSI|-|106,150,70,194,120,140|Mewtwonita Y|mewtwonite-y",
        "10062|380|Mega Latias|DRA|PSI|80,100,120,140,150,110|Latiasita|latiasite",
        "10063|381|Mega Latios|DRA|PSI|80,130,100,160,120,110|Latiosita|latiosite"
    };

    // Los ids de las formas mega empiezan en 10000 para distinguirlas de las especies normales.
    public const int MegaIdStart = 10000;

    // Convierte una línea de RawMegas en una especie y la registra en el diccionario de especies.
    private static void ParseMega(string line)
    {
        string[] f = line.Split('|');

        var sp = new PokemonSpecies
        {
            Id = int.Parse(f[0]),
            BaseSpeciesId = int.Parse(f[1]),
            Name = f[2],
            Type1 = f[3],
            Type2 = f[4] == "-" ? null : f[4],
            StoneName = f[6],
            StoneSprite = f[7]
        };

        string[] stats = f[5].Split(',');
        sp.HP = int.Parse(stats[0]);
        sp.Attack = int.Parse(stats[1]);
        sp.Defense = int.Parse(stats[2]);
        sp.SpAttack = int.Parse(stats[3]);
        sp.SpDefense = int.Parse(stats[4]);
        sp.Speed = int.Parse(stats[5]);

        Species[sp.Id] = sp;
    }

    // Devuelve las megaevoluciones disponibles de un Pokémon base.
    public static List<PokemonSpecies> MegasOf(int baseId)
    {
        return Species.Values.Where(s => s.BaseSpeciesId == baseId).ToList();
    }

    // Todas las formas mega registradas, ordenadas por id.
    public static IEnumerable<PokemonSpecies> AllMegas => Species.Values.Where(s => s.IsMega).OrderBy(s => s.Id);
}
