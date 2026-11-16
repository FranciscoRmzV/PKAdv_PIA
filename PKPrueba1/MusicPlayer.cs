using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace PKPrueba1;

// Reproductor de música de fondo. Usa los archivos de la carpeta Assets/Music si existen; si no, reproduce un vídeo de YouTube de forma invisible.
public static class MusicPlayer
{
    // Id del vídeo de YouTube que se usa como música de respaldo.
    private const string VideoId = "gvi0H2E-XcI";
    // Dominio virtual desde el que se sirve la página del reproductor (necesario para que YouTube acepte el reproductor incrustado).
    private const string VirtualHost = "pokeadventure.local";
    // Volumen de la música (35%).
    private const double Volume = 0.35;

    // Formatos de audio admitidos para la música local.
    private static readonly string[] Extensions = { ".mp3", ".wav", ".m4a", ".wma", ".ogg", ".flac" };

    // Reproductor de la música local (null si no se usa).
    private static MediaPlayer? local;
    // Control web que reproduce el vídeo de YouTube (null si no se usa).
    private static WebView2? web;
    // true cuando la página del reproductor web ya terminó de cargar.
    private static bool webReady;

    // Indica si la música está activada.
    public static bool Enabled { get; private set; } = true;

    // Archivo donde se guarda si el jugador desactivó la música.
    private static string SettingsFile =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PokemonAdventure",
            "music.txt");

    // Lee la configuración guardada e inicia la música: primero intenta la local y, si no hay archivos, la de YouTube.
    public static async Task InitializeAsync(WebView2 host)
    {
        try
        {
            if (File.Exists(SettingsFile))
                Enabled = File.ReadAllText(SettingsFile).Trim() != "off";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"No se pudo leer la configuración de música: {ex.Message}");
        }

        try
        {
            if (StartLocal())
                return;

            await StartYouTubeAsync(host);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"No se pudo iniciar la música: {ex.Message}");
        }
    }

    // Activa o desactiva la música, guarda la preferencia y pausa o reanuda el reproductor que esté en uso.
    public static async Task SetEnabledAsync(bool enabled)
    {
        Enabled = enabled;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, enabled ? "on" : "off");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"No se pudo guardar la configuración de música: {ex.Message}");
        }

        if (local != null)
        {
            // Música local: reanuda o pausa.
            if (enabled) local.Play(); else local.Pause();
        }

        if (web?.CoreWebView2 != null && webReady)
        {
            try
            {
                // Música de YouTube: llama a la función JavaScript setPlaying de la página para reanudar o pausar.
                await web.CoreWebView2.ExecuteScriptAsync($"setPlaying({(enabled ? "true" : "false")})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"No se pudo cambiar la música: {ex.Message}");
            }
        }
    }


    // Reproduce en bucle y de forma aleatoria los archivos de audio de Assets/Music. Devuelve false si no hay ninguno.
    private static bool StartLocal()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "Assets", "Music");

        if (!Directory.Exists(folder))
            return false;

        string[] files = Directory.GetFiles(folder)
            .Where(f => Extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f)
            .ToArray();

        if (files.Length == 0)
            return false;

        // Lista de reproducción con repetición automática (y orden aleatorio si hay varios archivos).
        var list = new MediaPlaybackList
        {
            AutoRepeatEnabled = true,
            ShuffleEnabled = files.Length > 1
        };

        foreach (string file in files)
            list.Items.Add(new MediaPlaybackItem(MediaSource.CreateFromUri(new Uri(file))));

        local = new MediaPlayer
        {
            Source = list,
            Volume = Volume,
            AutoPlay = Enabled
        };

        if (Enabled)
            local.Play();

        return true;
    }


    // Crea una página HTML con el reproductor de YouTube, la sirve desde un dominio virtual local y la carga en el WebView2.
    private static async Task StartYouTubeAsync(WebView2 host)
    {
        web = host;

        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PokemonAdventure");

        string pageFolder = Path.Combine(root, "web");
        Directory.CreateDirectory(pageFolder);

        // Empieza el vídeo en un segundo aleatorio para que no suene siempre igual.
        int start = new Random().Next(0, 15000);

        File.WriteAllText(
            Path.Combine(pageFolder, "player.html"),
            BuildPage(start));

        // Permite la reproducción automática sin que el usuario haga clic.
        var options = new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required"
        };

        var environment = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateWithOptionsAsync(
            null,
            Path.Combine(root, "webview"),
            options);

        await host.EnsureCoreWebView2Async(environment);

        // Asocia el dominio virtual a la carpeta local donde está la página.
        host.CoreWebView2.SetVirtualHostNameToFolderMapping(
            VirtualHost,
            pageFolder,
            Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

        host.CoreWebView2.NavigationCompleted += (_, _) => webReady = true;
        host.Source = new Uri($"https://{VirtualHost}/player.html");
    }

    // Genera el HTML del reproductor: carga la API de YouTube, reproduce el vídeo en bucle con el volumen configurado y define setPlaying(on) para pausar o reanudar.
    private static string BuildPage(int start)
    {
        string initial = Enabled ? "true" : "false";

        return @"<!DOCTYPE html>
<html>
<body style=""margin:0;background:#000;overflow:hidden"">
<div id=""p""></div>
<script src=""https://www.youtube.com/iframe_api""></script>
<script>
var player = null;
var wantPlay = " + initial + @";
function onYouTubeIframeAPIReady() {
  player = new YT.Player('p', {
    width: '2',
    height: '2',
    videoId: '" + VideoId + @"',
    playerVars: {
      autoplay: 1,
      controls: 0,
      rel: 0,
      loop: 1,
      playlist: '" + VideoId + @"',
      start: " + start + @",
      playsinline: 1,
      origin: location.origin
    },
    events: {
      onReady: function (e) {
        e.target.setVolume(" + (int)(Volume * 100) + @");
        if (wantPlay) e.target.playVideo(); else e.target.pauseVideo();
      }
    }
  });
}
function setPlaying(on) {
  wantPlay = on;
  if (!player || !player.playVideo) return;
  if (on) player.playVideo(); else player.pauseVideo();
}
</script>
</body>
</html>";
    }
}
