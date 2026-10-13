### Tâche 21 : MarqueInterne, ApplicationOrigine, LecteurPressePapiers et NatifPressePapiers

**Fichiers :**
- Créer : `src/PressePapiers/Natif/NatifPressePapiers.cs` (appels user32 et kernel32 de la lecture ; la tâche 22 le complète)
- Créer : `src/PressePapiers/Capture/FormatsPressePapiers.cs` (RTF, HTML et PNG enregistrés une seule fois ; la tâche 22 s'en sert)
- Créer : `src/PressePapiers/Capture/ContenuBrut.cs` (résultat intermédiaire de la lecture native)
- Créer : `src/PressePapiers/Capture/LectureBrute.cs` (lecture synchrone du presse-papiers, destinée au fil dédié)
- Modifier : `src/PressePapiers/Capture/MarqueInterne.cs`, `src/PressePapiers/Capture/ApplicationOrigine.cs`, `src/PressePapiers/Capture/LecteurPressePapiers.cs` (remplacent les coquilles ; contenus complets à l'étape 3)
- Hors dépôt (jetables, ne se committent pas) : `essais-pp/verif/verif.csproj`, `Brut.cs`, `Program.cs`
- Tests : aucun test xUnit, c'est du code natif. Les décodeurs (HTML, fichiers, images) sont déjà testés aux tâches 13 à 15 ;
  la tâche est vérifiée sur le poste par un aller-retour réel (étapes 1, 2 et 5).

**Interfaces :**
- Utilise :
  - tâche 1 : `ContenuCopie`, `TypeElement`, `IJournal` (`Info`, `Avertissement`, `Erreur`) ;
  - tâche 13 : `FormatHtml` (uniquement dans le programme de vérification) ;
  - tâche 14 : `ListeFichiers.DecoderDropFiles(ReadOnlySpan<byte>)` ;
  - tâche 15 : `ConvertisseurImage.DibVersPngAsync(byte[])` et `ConvertisseurImage.MiniatureAsync(byte[], uint)`.
- Fournit :
  - `public static class MarqueInterne` : `const string NomFormat = "PressePapiers.Interne"` ; `static uint Format` (enregistre le
    format au premier appel, `InvalidOperationException` si Windows renvoie 0) ;
  - `public static class ApplicationOrigine` : `static string? Determiner(nint fenetre)` (nom du processus sans extension ;
    `ApplicationFrameHost` résolu par la première fenêtre fille appartenant à un autre processus ; `null` si la fenêtre est
    nulle, invalide, le processus inaccessible ou si rien n'est trouvé) ; `internal static string? DeterminerPourCopie()`
    (fenêtre au premier plan, puis seulement à défaut `GetClipboardOwner`), utilisé par la tâche 23 ;
  - `public sealed class LecteurPressePapiers` : `LecteurPressePapiers(nint hwndRelais, IJournal journal)` et
    `Task<ContenuCopie?> LireAsync(string? application)` ; jamais d'exception ; `null` si la marque interne est présente, si le
    presse-papiers est occupé après 10 essais, vide, illisible, trop gros, ou si la lecture dépasse 2 secondes ;
  - `internal static class FormatsPressePapiers` : `Rtf`, `Html`, `Png` (identifiants des formats « Rich Text Format »,
    « HTML Format », « PNG ») et `Enregistrer(string nom)` ;
  - `internal static class LectureBrute` : `EssaisMax` (10), `PauseEntreEssais` (25 ms), `TailleMaxTexte` (10 Mio),
    `TailleMaxImage` (256 Mio), `Lire(IJournal)` ;
  - `internal static partial class NatifPressePapiers` (namespace `PressePapiers.Natif`) : constantes `CfUnicodeText` (13),
    `CfDib` (8), `CfDibV5` (17), `CfHDrop` (15) ; `OpenClipboard`, `CloseClipboard`, `IsClipboardFormatAvailable`,
    `GetClipboardData`, `RegisterClipboardFormat`, `GetForegroundWindow` (déclaré ici : chaque brique garde ses propres déclarations natives),
    `GetClipboardOwner`, `GetWindowThreadProcessId`, `FindWindowEx`, `GlobalLock`, `GlobalUnlock`, `GlobalSize`.
  - Consommateurs : tâche 22 (`FormatsPressePapiers`, `LectureBrute.EssaisMax`, constantes de formats), tâche 23
    (`LecteurPressePapiers`, `ApplicationOrigine.DeterminerPourCopie`), tâche 29 (composition).

Règles appliquées par la lecture (spec, sections 4 et 12) :
- **Fil dédié** : chaque lecture tourne sur un nouveau fil d'arrière-plan qui appelle `OpenClipboard(NULL)` (jamais sur le fil
  d'interface ni sur le pool). `LireAsync` abandonne l'attente après **2 secondes** (un propriétaire en rendu différé qui ne répond
  pas bloque `GetClipboardData`) et note l'abandon ; le fil bloqué est laissé derrière et se termine quand le propriétaire répond ou
  disparaît. La conversion des images se fait **après** la fermeture du presse-papiers et hors de ce délai.
- **Ouverture** : 10 essais espacés de 25 ms (soit environ 225 ms de pauses), puis « copie ignorée » au journal avec le code d'erreur Windows.
- **Marque interne** d'abord : si son format est présent, `null` sans rien lire. Si son format ne peut pas être enregistré, un
  avertissement est noté et la lecture continue (un doublon vaut mieux qu'une capture muette).
- **Ordre de décision du type** : `CF_HDROP` présent donne `Fichiers` ; sinon `CF_UNICODETEXT` présent donne `MisEnForme` si un RTF
  ou un HTML non vide a pu être lu, `Texte` sinon ; sinon `CF_DIBV5` puis `CF_DIB` donnent `Image` ; sinon `null`. Un HTML seul,
  sans texte, est donc ignoré (les navigateurs mettent toujours le texte avec).
- **Limites** (point de vigilance 2) : texte de plus de 10 Mio en UTF-8 ignoré avec journal (un texte de 11 millions de caractères
  l'est avant même d'être copié ; 6 millions de caractères accentués font 12 Mio en UTF-8 et le sont aussi) ; RTF et HTML de plus de
  10 Mio ignorés seuls (le texte reste capturé) ; DIB de plus de 256 Mio ignoré. Les poignées nulles, tailles nulles et verrouillages
  impossibles donnent `null` avec journal.
- **Copie défensive** : les octets sont copiés tout de suite (`GlobalLock`, `Marshal.Copy`, `GlobalUnlock`), le presse-papiers est
  refermé dans un `finally`. RTF et HTML sont coupés au premier octet nul (le bloc peut être arrondi par `GlobalSize`).

- [ ] **Étape 1 : Écrire la vérification sur le poste (programme jetable)**

Le code natif ne se teste pas en xUnit : un petit programme, hors du dépôt, simule des applications tierces avec ses propres
appels au presse-papiers (`Brut.cs`, indépendants du code testé), puis relit avec `LecteurPressePapiers`. Il recompile les
sources du dépôt (`-p:Depot=` donne le chemin absolu du dépôt) et sert aussi à la tâche 22 (mode `ecriture`). Créer le dossier
`essais-pp/verif` n'importe où hors du dépôt (par exemple à côté de lui) avec ces trois fichiers.

`essais-pp/verif/verif.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Programme jetable, hors du dépôt. Lancer avec -p:Depot=<chemin absolu du dépôt PressePapiers>. -->
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <PlatformTarget>x64</PlatformTarget>
    <NoWarn>CS1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <!-- Tout le dossier Capture sauf ce qui dépend du relais et du recollage ; les fichiers que les tâches 21 et 22 créent
         n'existent pas encore avant elles, d'où les jokers et la condition. -->
    <Compile Include="$(Depot)/src/PressePapiers/Capture/*.cs"
             Exclude="$(Depot)/src/PressePapiers/Capture/ServiceCapture.cs;$(Depot)/src/PressePapiers/Capture/ServiceRecollage.cs" />
    <Compile Include="$(Depot)/src/PressePapiers/Natif/NatifPressePapiers.cs"
             Condition="Exists('$(Depot)/src/PressePapiers/Natif/NatifPressePapiers.cs')" />
    <Compile Include="$(Depot)/tests/PressePapiers.Tests/Outils/ImagesSynthetiques.cs" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="$(Depot)/src/PressePapiers.Coeur/PressePapiers.Coeur.csproj" />
  </ItemGroup>
</Project>
```

`essais-pp/verif/Brut.cs`

```csharp
using System.Runtime.InteropServices;
using System.Text;

/// <summary>Accès brut au presse-papiers, indépendant du code testé : simule des applications tierces et relit les formats.</summary>
internal static class Brut
{
    [DllImport("user32.dll", SetLastError = true)] static extern bool OpenClipboard(nint h);
    [DllImport("user32.dll", SetLastError = true)] static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)] static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] static extern nint SetClipboardData(uint f, nint m);
    [DllImport("user32.dll", SetLastError = true)] static extern nint GetClipboardData(uint f);
    [DllImport("user32.dll")] static extern bool IsClipboardFormatAvailable(uint f);
    [DllImport("user32.dll")] static extern uint EnumClipboardFormats(uint f);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern uint RegisterClipboardFormatW(string n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClipboardFormatNameW(uint f, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern nint FindWindowExW(nint parent, nint apres, string? classe, string? titre);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(nint h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool PostMessageW(nint h, uint m, nuint w, nint l);
    [DllImport("kernel32.dll", SetLastError = true)] static extern nint GlobalAlloc(uint f, nuint s);
    [DllImport("kernel32.dll")] static extern nint GlobalLock(nint m);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(nint m);
    [DllImport("kernel32.dll")] static extern nuint GlobalSize(nint m);

    public const uint CfUnicodeText = 13, CfDib = 8, CfDibV5 = 17, CfHDrop = 15;
    public static uint Rtf => RegisterClipboardFormatW("Rich Text Format");
    public static uint Html => RegisterClipboardFormatW("HTML Format");
    public static uint Png => RegisterClipboardFormatW("PNG");
    public static uint Marque => RegisterClipboardFormatW("PressePapiers.Interne");

    static bool Ouvrir(nint hwnd)
    {
        for (var i = 0; i < 40; i++)
        {
            if (OpenClipboard(hwnd)) return true;
            Thread.Sleep(25);
        }
        return false;
    }

    /// <summary>Remplace le contenu du presse-papiers comme le ferait une application : formats rendus immédiatement.</summary>
    public static void Ecrire(nint hwnd, params (uint Format, byte[] Octets)[] formats)
    {
        if (!Ouvrir(hwnd)) throw new InvalidOperationException("OpenClipboard impossible");
        try
        {
            if (!EmptyClipboard()) throw new InvalidOperationException("EmptyClipboard impossible");
            foreach (var (format, octets) in formats)
            {
                var m = GlobalAlloc(0x2, (nuint)octets.Length);
                var p = GlobalLock(m);
                Marshal.Copy(octets, 0, p, octets.Length);
                GlobalUnlock(m);
                if (SetClipboardData(format, m) == 0) throw new InvalidOperationException($"SetClipboardData {format} impossible : {Marshal.GetLastWin32Error()}");
            }
        }
        finally { CloseClipboard(); }
    }

    /// <summary>Un propriétaire en rendu différé : annonce CF_UNICODETEXT sans le fournir (poignée nulle).</summary>
    public static void EcrireDiffere(nint hwnd)
    {
        if (!Ouvrir(hwnd)) throw new InvalidOperationException("OpenClipboard impossible");
        try
        {
            EmptyClipboard();
            SetClipboardData(CfUnicodeText, 0);
        }
        finally { CloseClipboard(); }
    }

    /// <summary>Ouvre le presse-papiers et le garde (pour simuler une application qui l'occupe) ; faux s'il est déjà pris.</summary>
    public static bool OuvrirPourOccuper(nint hwnd) => OpenClipboard(hwnd);

    /// <summary>Referme le presse-papiers ouvert par <see cref="OuvrirPourOccuper"/>.</summary>
    public static void Fermer() => CloseClipboard();

    /// <summary>Poste un message sans paramètre à une fenêtre (par exemple WM_CLIPBOARDUPDATE, 0x031D).</summary>
    public static void Poster(nint hwnd, uint message) => PostMessageW(hwnd, message, 0, 0);

    public static byte[] Utf16Nul(string texte) => Encoding.Unicode.GetBytes(texte + "\0");

    public static bool Present(uint format) => IsClipboardFormatAvailable(format);

    /// <summary>Octets d'un format (taille réelle du bloc), ou null.</summary>
    public static byte[]? Lire(nint hwnd, uint format)
    {
        if (!Ouvrir(hwnd)) return null;
        try
        {
            var m = GetClipboardData(format);
            if (m == 0) return null;
            var taille = (int)GlobalSize(m);
            var p = GlobalLock(m);
            var octets = new byte[taille];
            Marshal.Copy(p, octets, 0, taille);
            GlobalUnlock(m);
            return octets;
        }
        finally { CloseClipboard(); }
    }

    public static string Formats()
    {
        if (!Ouvrir(0)) return "(illisible)";
        try
        {
            var liste = new List<string>();
            for (var f = EnumClipboardFormats(0); f != 0; f = EnumClipboardFormats(f))
            {
                var nom = new StringBuilder(128);
                liste.Add(GetClipboardFormatNameW(f, nom, 128) > 0 ? nom.ToString() : f.ToString());
            }
            return string.Join(", ", liste);
        }
        finally { CloseClipboard(); }
    }

    public static List<(nint Hwnd, string Titre)> FenetresDeClasse(string classe)
    {
        var liste = new List<(nint, string)>();
        nint f = 0;
        while ((f = FindWindowExW(0, f, classe, null)) != 0)
        {
            var titre = new StringBuilder(256);
            GetWindowTextW(f, titre, 256);
            liste.Add((f, titre.ToString()));
        }
        return liste;
    }
}

internal static class Fen
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern nint CreateWindowExW(uint ex, string cls, string titre, uint style, int x, int y, int w, int h, nint parent, nint menu, nint inst, nint param);
    [DllImport("user32.dll")] public static extern bool DestroyWindow(nint h);

    public static nint Creer() => CreateWindowExW(0, "STATIC", "essai", 0, 0, 0, 10, 10, 0, 0, 0, 0);
}
```

`essais-pp/verif/Program.cs`

```csharp
// Programme jetable de vérification sur le poste des tâches 21 (lecture) et 22 (écriture) : aller-retour réel par le
// presse-papiers. Usage : dotnet run -p:Depot=<dépôt> -- lecture | ecriture | tout
using System.Diagnostics;
using System.Text;
using PressePapiers.Capture;
using PressePapiers.Coeur.Formats;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Tests.Outils;

var mode = args.Length > 0 ? args[0] : "tout";
var journal = new JournalListe();
int echecs = 0, total = 0;

void Verifier(string nom, bool ok, string detail = "")
{
    total++;
    if (!ok) echecs++;
    Console.WriteLine($"{(ok ? "OK    " : "ECHEC ")}{nom} {detail}");
}

void Info(string texte) => Console.WriteLine($"      {texte}");

var hwnd = Fen.Creer();
Verifier("fenêtre relais créée", hwnd != 0);
var lecteur = new LecteurPressePapiers(hwnd, journal);
var dossier = Path.Combine(Path.GetTempPath(), "essai-pp-verif");
Directory.CreateDirectory(dossier);

Element Elt(TypeElement type, string? texte = null, byte[]? rtf = null, byte[]? html = null, string? image = null) => new()
{
    Id = 1, OngletId = 1, Type = type, Texte = texte, Rtf = rtf, Html = html, ImageFichier = image,
    Empreinte = [0], DateCopie = DateTimeOffset.UtcNow, DateUsage = DateTimeOffset.UtcNow, Taille = 0,
};

// Relit le presse-papiers par un autre programme que le nôtre (PowerShell 7 ou Windows PowerShell 5.1).
async Task<string> Externe(string commande, bool sta = false, string exe = "pwsh.exe")
{
    var info = new ProcessStartInfo(exe) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
    info.ArgumentList.Add("-NoProfile");
    if (sta) info.ArgumentList.Add("-STA");
    info.ArgumentList.Add("-Command");
    info.ArgumentList.Add("[Console]::OutputEncoding=[Text.Encoding]::UTF8; " + commande);
    using var p = Process.Start(info)!;
    var sortie = await p.StandardOutput.ReadToEndAsync();
    var erreur = await p.StandardError.ReadToEndAsync();
    await p.WaitForExitAsync();
    if (erreur.Length > 0) Info(exe + " stderr : " + erreur.Trim());
    return sortie.TrimEnd('\r', '\n');
}

var rtfOctets = Encoding.ASCII.GetBytes(@"{\rtf1\ansi{\b gras} et normal}");
const string fragment = "<b>gras é</b> et 😀";
var htmlOctets = FormatHtml.Encoder(fragment);

async Task Lecture()
{
    Console.WriteLine("=== Tâche 21 : lecture ===");
    Verifier("marque interne : format enregistré non nul", MarqueInterne.Format != 0, $"({MarqueInterne.Format})");

    // Texte posé par une application tierce (sans la marque interne).
    const string texteAccents = "Bonjour é à ç œ 😀 日本語";
    Brut.Ecrire(hwnd, (Brut.CfUnicodeText, Brut.Utf16Nul(texteAccents)));
    var lu = await lecteur.LireAsync("AppTest");
    Verifier("texte : lu comme Texte", lu is { Type: TypeElement.Texte } && lu.Texte == texteAccents && lu.Application == "AppTest", $"-> {lu?.Type} '{lu?.Texte}'");
    Verifier("texte : ni RTF ni HTML", lu is { Rtf: null, Html: null });

    // Texte mis en forme.
    Brut.Ecrire(hwnd, (Brut.CfUnicodeText, Brut.Utf16Nul("gras é et 😀")), (Brut.Rtf, [.. rtfOctets, 0]), (Brut.Html, [.. htmlOctets, 0]));
    lu = await lecteur.LireAsync("Word");
    Verifier("mis en forme : type MisEnForme avec RTF et HTML", lu is { Type: TypeElement.MisEnForme, Rtf: not null, Html: not null });
    Verifier("mis en forme : RTF identique sans nul final", lu?.Rtf is not null && lu.Rtf.AsSpan().SequenceEqual(rtfOctets));
    Verifier("mis en forme : HTML identique sans nul final, fragment extrait", lu?.Html is not null && lu.Html.AsSpan().SequenceEqual(htmlOctets) && FormatHtml.ExtraireFragment(lu.Html) == fragment);

    Brut.Ecrire(hwnd, (Brut.CfUnicodeText, Brut.Utf16Nul("juste du html")), (Brut.Html, [.. htmlOctets, 0]));
    lu = await lecteur.LireAsync(null);
    Verifier("texte + HTML seul : MisEnForme sans RTF", lu is { Type: TypeElement.MisEnForme, Rtf: null, Html: not null });

    Brut.Ecrire(hwnd, (Brut.CfUnicodeText, Brut.Utf16Nul("juste du rtf")), (Brut.Rtf, [.. rtfOctets, 0]));
    lu = await lecteur.LireAsync(null);
    Verifier("texte + RTF seul : MisEnForme sans HTML", lu is { Type: TypeElement.MisEnForme, Rtf: not null, Html: null });

    Brut.Ecrire(hwnd, (Brut.Html, [.. htmlOctets, 0]));
    lu = await lecteur.LireAsync(null);
    Verifier("HTML seul sans texte : ignoré (null)", lu is null);

    // Marque interne : une copie qui la porte est ignorée, quel que soit son contenu.
    Brut.Ecrire(hwnd, (Brut.CfUnicodeText, Brut.Utf16Nul("copie de l'outil")), (Brut.Marque, [1]));
    lu = await lecteur.LireAsync(null);
    Verifier("marque interne : copie marquée ignorée", lu is null);

    // Image avec transparence, par un vrai CF_DIBV5.
    const int l = 64, h = 48;
    var pixels = ImagesSynthetiques.Motif(l, h, (x, y) => ((byte)(x * 4), (byte)(y * 5), 200, (byte)(y < 24 ? 255 : 100)));
    Brut.Ecrire(hwnd, (Brut.CfDibV5, ImagesSynthetiques.DibV5(l, h, pixels, 0xFF000000)));
    lu = await lecteur.LireAsync("Paint");
    Verifier("image V5 : lue comme Image avec PNG et miniature", lu is { Type: TypeElement.Image, ImagePng: not null, Miniature: not null });
    if (lu?.ImagePng is not null)
    {
        var (lp, hp, px) = await ImagesSynthetiques.LirePng(lu.ImagePng);
        Verifier("image V5 : pixels et alpha conservés (point 7 de la spec)", (lp, hp) == (l, h) && px.AsSpan().SequenceEqual(pixels), $"{lp}x{hp}, alpha en bas = {px[(40 * l) * 4 + 3]}");
    }

    Brut.Ecrire(hwnd, (Brut.CfDib, ImagesSynthetiques.Dib24(l, h, ImagesSynthetiques.Motif(l, h, (x, y) => ((byte)x, (byte)y, 10, 255)))));
    lu = await lecteur.LireAsync(null);
    Verifier("image CF_DIB 24 bits : lue", lu is { Type: TypeElement.Image, ImagePng: not null });

    var pixels4k = ImagesSynthetiques.Motif(3840, 2160, (x, y) => ((byte)x, (byte)y, (byte)(x ^ y), 255));
    Brut.Ecrire(hwnd, (Brut.CfDibV5, ImagesSynthetiques.DibV5(3840, 2160, pixels4k, 0xFF000000)));
    var chrono = Stopwatch.StartNew();
    lu = await lecteur.LireAsync(null);
    chrono.Stop();
    Verifier("image 4K : lue, convertie, miniature", lu is { Type: TypeElement.Image, ImagePng: not null, Miniature: not null }, $"{chrono.ElapsedMilliseconds} ms");
    if (lu?.Miniature is not null)
    {
        var (lm, hm, _) = await ImagesSynthetiques.LirePng(lu.Miniature);
        Verifier("image 4K : miniature 160 x 90", (lm, hm) == (160, 90), $"{lm}x{hm}");
    }

    // Fichiers.
    var f1 = Path.Combine(dossier, "un é.txt");
    var f2 = Path.Combine(dossier, "deux 😀.txt");
    File.WriteAllText(f1, "1");
    File.WriteAllText(f2, "2");
    Brut.Ecrire(hwnd, (Brut.CfHDrop, ListeFichiers.EncoderDropFiles([f1, f2])));
    lu = await lecteur.LireAsync("Explorer");
    Verifier("fichiers : lus comme Fichiers dans l'ordre", lu is { Type: TypeElement.Fichiers } && lu.Fichiers!.SequenceEqual([f1, f2]));

    // Applications tierces réelles (PowerShell).
    await Externe("Set-Clipboard -Value 'Texte posé par PowerShell é 😀'");
    lu = await lecteur.LireAsync("pwsh");
    Verifier("tiers réel : Set-Clipboard texte", lu is { Type: TypeElement.Texte } && lu.Texte == "Texte posé par PowerShell é 😀", $"-> {lu?.Type} '{lu?.Texte}'");
    await Externe($"Set-Clipboard -Path '{f1}','{f2}'", exe: "powershell.exe");
    lu = await lecteur.LireAsync("powershell");
    Verifier("tiers réel : Set-Clipboard -Path (CF_HDROP)", lu is { Type: TypeElement.Fichiers } && lu.Fichiers!.Count == 2, $"-> {lu?.Type} ; formats = {Brut.Formats()}");
    await Externe("Set-Clipboard -Value '<b>gras é</b>' -AsHtml", exe: "powershell.exe");
    lu = await lecteur.LireAsync("powershell");
    Info($"tiers réel : Set-Clipboard -AsHtml (HTML sans texte) donne {lu?.Type.ToString() ?? "null"} ; formats = {Brut.Formats()}");

    // Gros textes (point de vigilance 2).
    Brut.Ecrire(hwnd, (Brut.CfUnicodeText, Brut.Utf16Nul(new string('a', 9_000_000))));
    chrono.Restart();
    lu = await lecteur.LireAsync(null);
    chrono.Stop();
    Verifier("texte 9 M caractères : lu", lu is { Type: TypeElement.Texte } && lu.Texte!.Length == 9_000_000, $"{chrono.ElapsedMilliseconds} ms");
    Brut.Ecrire(hwnd, (Brut.CfUnicodeText, Brut.Utf16Nul(new string('a', 11_000_000))));
    journal.Vider();
    lu = await lecteur.LireAsync(null);
    Verifier("texte 11 M caractères : ignoré avec journal", lu is null && journal.Contient("au-delà de 10 Mo"), journal.Dernier());
    Brut.Ecrire(hwnd, (Brut.CfUnicodeText, Brut.Utf16Nul(new string('é', 6_000_000))));   // 12 Mo en UTF-8
    journal.Vider();
    lu = await lecteur.LireAsync(null);
    Verifier("texte 6 M caractères accentués (12 Mo en UTF-8) : ignoré", lu is null && journal.Contient("au-delà de 10 Mo"), journal.Dernier());

    // Presse-papiers occupé par une autre application.
    Brut.Ecrire(hwnd, (Brut.CfUnicodeText, Brut.Utf16Nul("contenu avant occupation")));
    async Task<TimeSpan> LireAvecOccupation(int dureeMs, Action<ContenuCopie?> verifie)
    {
        var pret = new ManualResetEventSlim();
        var occupant = new Thread(() =>
        {
            var h2 = Fen.Creer();
            while (!Brut.OuvrirPourOccuper(h2)) Thread.Sleep(10);
            pret.Set();
            Thread.Sleep(dureeMs);
            Brut.Fermer();
            Fen.DestroyWindow(h2);
        });
        occupant.Start();
        pret.Wait();
        var t = Stopwatch.StartNew();
        var resultat = await lecteur.LireAsync(null);
        t.Stop();
        verifie(resultat);
        occupant.Join();
        return t.Elapsed;
    }

    journal.Vider();
    var duree = await LireAvecOccupation(120, r => Verifier("occupé 120 ms : la lecture réussit après réessais", r is { Type: TypeElement.Texte }));
    Verifier("occupé 120 ms : au moins un réessai espacé de 25 ms", duree >= TimeSpan.FromMilliseconds(80), $"{duree.TotalMilliseconds:0} ms");
    journal.Vider();
    duree = await LireAvecOccupation(900, r => Verifier("occupé 900 ms : copie ignorée après 10 essais", r is null && journal.Contient("resté occupé après 10 essais"), journal.Dernier()));
    Verifier("occupé 900 ms : abandon en environ 225 ms (9 pauses de 25 ms)", duree >= TimeSpan.FromMilliseconds(200) && duree < TimeSpan.FromMilliseconds(600), $"{duree.TotalMilliseconds:0} ms");

    // Propriétaire en rendu différé qui ne répond jamais : la lecture est abandonnée après 2 s.
    journal.Vider();
    var proprietaireFini = new ManualResetEventSlim();
    var proprietairePret = new ManualResetEventSlim();
    var filProprietaire = new Thread(() =>
    {
        var h3 = Fen.Creer();
        Brut.EcrireDiffere(h3);
        proprietairePret.Set();
        Thread.Sleep(6000);          // aucune boucle de messages : WM_RENDERFORMAT reste sans réponse
        Fen.DestroyWindow(h3);
        proprietaireFini.Set();
    });
    filProprietaire.Start();
    proprietairePret.Wait();
    chrono.Restart();
    lu = await lecteur.LireAsync(null);
    chrono.Stop();
    Verifier("rendu différé muet : lecture abandonnée en environ 2 s", lu is null && chrono.Elapsed > TimeSpan.FromMilliseconds(1800) && chrono.Elapsed < TimeSpan.FromMilliseconds(3200) && journal.Contient("n'a pas répondu"), $"{chrono.ElapsedMilliseconds} ms ; {journal.Dernier()}");
    proprietaireFini.Wait();
    filProprietaire.Join();
    Thread.Sleep(300);

    // Application d'origine.
    Verifier("application d'origine : fenêtre nulle = null", ApplicationOrigine.Determiner(0) is null);
    var moi = Process.GetCurrentProcess().ProcessName;
    Verifier("application d'origine : fenêtre du processus courant", ApplicationOrigine.Determiner(hwnd) == moi, $"-> {ApplicationOrigine.Determiner(hwnd)} (attendu {moi})");
    Verifier("application d'origine : fenêtre invalide = null sans exception", ApplicationOrigine.Determiner(unchecked((nint)0x7FFFFFF1)) is null);

    // Vraie application UWP : la Calculatrice est lancée puis fermée (seul son processus est arrêté).
    Process.Start(new ProcessStartInfo("calc.exe") { UseShellExecute = true });
    string? origineCalc = null;
    for (var i = 0; i < 60 && origineCalc is null; i++)
    {
        await Task.Delay(250);
        foreach (var cadre in Brut.FenetresDeClasse("ApplicationFrameWindow").Where(c => c.Titre.Contains("Calcul", StringComparison.OrdinalIgnoreCase)))
            origineCalc = ApplicationOrigine.Determiner(cadre.Hwnd) ?? origineCalc;
    }

    Verifier("application d'origine : cadre UWP (Calculatrice) résolu vers l'application réelle", origineCalc == "CalculatorApp", $"-> {origineCalc ?? "null"}");
    foreach (var p in Process.GetProcessesByName("CalculatorApp")) { try { p.Kill(); p.WaitForExit(3000); } catch { /* déjà fermé */ } }
}

async Task Ecriture()
{
    Console.WriteLine("=== Tâche 22 : écriture ===");
    var ecrivain = new EcrivainPressePapiers(hwnd, journal);

    // Texte mis en forme, selon le mode.
    var misEnForme = Elt(TypeElement.MisEnForme, "gras é et 😀", rtfOctets, htmlOctets);
    var ok = await ecrivain.EcrireAsync(misEnForme, ModeCollage.AvecMiseEnForme, null);
    Verifier("mode AvecMiseEnForme : réussi", ok, "formats = " + Brut.Formats());
    Verifier("mode AvecMiseEnForme : texte, RTF, HTML et marque présents", Brut.Present(Brut.CfUnicodeText) && Brut.Present(Brut.Rtf) && Brut.Present(Brut.Html) && Brut.Present(Brut.Marque));
    var rtfRelu = Brut.Lire(hwnd, Brut.Rtf);
    var htmlRelu = Brut.Lire(hwnd, Brut.Html);
    Verifier("RTF relu = octets + nul final", rtfRelu is not null && rtfRelu.AsSpan(0, rtfOctets.Length).SequenceEqual(rtfOctets) && rtfRelu[rtfOctets.Length] == 0);
    Verifier("HTML relu = octets + nul final", htmlRelu is not null && htmlRelu.AsSpan(0, htmlOctets.Length).SequenceEqual(htmlOctets) && htmlRelu[htmlOctets.Length] == 0);
    var lu = await lecteur.LireAsync("x");
    Verifier("marque interne : la copie de l'outil est ignorée par le lecteur", lu is null);
    var texteVu = await Externe("Get-Clipboard -Raw");
    Verifier("texte relu par PowerShell (Get-Clipboard)", texteVu == "gras é et 😀", $"-> '{texteVu}'");

    ok = await ecrivain.EcrireAsync(misEnForme, ModeCollage.TexteSeul, null);
    Verifier("mode TexteSeul : texte et marque, sans RTF ni HTML", ok && Brut.Present(Brut.CfUnicodeText) && !Brut.Present(Brut.Rtf) && !Brut.Present(Brut.Html) && Brut.Present(Brut.Marque), "formats = " + Brut.Formats());
    ok = await ecrivain.EcrireAsync(Elt(TypeElement.Texte, "texte simple"), ModeCollage.AvecMiseEnForme, null);
    Verifier("texte simple en mode mise en forme : texte seul", ok && !Brut.Present(Brut.Rtf));
    ok = await ecrivain.EcrireAsync(Elt(TypeElement.Texte, null), ModeCollage.TexteSeul, null);
    Verifier("texte absent : faux sans exception", !ok);

    // Image avec transparence : CF_DIB, PNG et marque.
    const int l = 64, h = 48;
    var pixels = ImagesSynthetiques.Motif(l, h, (x, y) => ((byte)(x * 4), (byte)(y * 5), 200, (byte)(y < 24 ? 255 : 100)));
    var cheminPng = Path.Combine(dossier, "alpha.png");
    await File.WriteAllBytesAsync(cheminPng, await ImagesSynthetiques.PngDepuis(l, h, pixels));
    ok = await ecrivain.EcrireAsync(Elt(TypeElement.Image, image: "alpha.png"), ModeCollage.TexteSeul, cheminPng);
    Verifier("image : CF_DIB, PNG et marque présents", ok && Brut.Present(Brut.CfDib) && Brut.Present(Brut.Png) && Brut.Present(Brut.Marque), "formats = " + Brut.Formats());
    var pngRelu = Brut.Lire(hwnd, Brut.Png);
    Verifier("image : format PNG = fichier d'origine", pngRelu is not null && pngRelu.AsSpan().SequenceEqual(File.ReadAllBytes(cheminPng)));
    var vu = await Externe("Add-Type -AssemblyName System.Windows.Forms; $i=[System.Windows.Forms.Clipboard]::GetImage(); '{0}x{1}' -f $i.Width,$i.Height", sta: true);
    Verifier("image : relue par Windows Forms", vu == $"{l}x{h}", $"-> {vu}");
    ok = await ecrivain.EcrireAsync(Elt(TypeElement.Image, image: "absent.png"), ModeCollage.TexteSeul, Path.Combine(dossier, "absent.png"));
    Verifier("image : PNG manquant sur disque = faux sans exception", !ok);

    // Fichiers.
    var f1 = Path.Combine(dossier, "un é.txt");
    var f2 = Path.Combine(dossier, "deux 😀.txt");
    File.WriteAllText(f1, "1");
    File.WriteAllText(f2, "2");
    ok = await ecrivain.EcrireAsync(Elt(TypeElement.Fichiers, f1 + "\n" + f2), ModeCollage.TexteSeul, null);
    Verifier("fichiers : CF_HDROP et marque présents", ok && Brut.Present(Brut.CfHDrop) && Brut.Present(Brut.Marque), "formats = " + Brut.Formats());
    var liste = await Externe("Get-Clipboard -Format FileDropList | ForEach-Object { $_.FullName }", exe: "powershell.exe");
    Verifier("fichiers : relus par Windows PowerShell (FileDropList)", liste.Split('\n').Select(s => s.Trim()).SequenceEqual([f1, f2]), $"-> {liste.Replace("\n", " | ")}");
    lu = await lecteur.LireAsync(null);
    Verifier("fichiers : marque interne ignorée à la relecture", lu is null);
}

if (mode is "lecture" or "tout") await Lecture();
if (mode is "ecriture" or "tout") await Ecriture();

// Nettoyage : un texte neutre dans le presse-papiers, plus de fichiers d'essai.
Brut.Ecrire(hwnd, (Brut.CfUnicodeText, Brut.Utf16Nul("(texte neutre : essai terminé)")));
Fen.DestroyWindow(hwnd);
try { Directory.Delete(dossier, true); } catch { /* sans importance */ }

Console.WriteLine();
Console.WriteLine($"Journal : {journal.Lignes.Count} ligne(s), dernières entrées");
foreach (var ligne in journal.Lignes.TakeLast(4)) Console.WriteLine("  " + ligne);
Console.WriteLine($"{total - echecs}/{total} vérifications réussies");
return echecs == 0 ? 0 : 1;

internal sealed class JournalListe : IJournal
{
    private readonly object _verrou = new();
    public List<string> Lignes { get; } = [];
    private void Ajouter(string p, string m, Exception? e) { lock (_verrou) Lignes.Add($"{p} {m}{(e is null ? "" : " | " + e.GetType().Name + ": " + e.Message)}"); }
    public void Info(string message) => Ajouter("INFO", message, null);
    public void Avertissement(string message, Exception? exception = null) => Ajouter("AVERT", message, exception);
    public void Erreur(string message, Exception? exception = null) => Ajouter("ERREUR", message, exception);
    public void Vider() { lock (_verrou) Lignes.Clear(); }
    public bool Contient(string morceau) { lock (_verrou) return Lignes.Any(l => l.Contains(morceau, StringComparison.Ordinal)); }
    public string Dernier() { lock (_verrou) return Lignes.LastOrDefault() ?? "(journal vide)"; }
}
```

Ce que le mode `lecture` vérifie (31 contrôles) : texte avec accents, emoji et CJK ; texte + RTF + HTML (octets identiques, sans nul
final, fragment HTML extrait) ; texte + HTML seul et texte + RTF seul ; HTML sans texte ignoré ; marque interne ignorée ; image
CF_DIBV5 avec transparence (pixels et alpha identiques après le vrai presse-papiers) ; CF_DIB 24 bits ; image 3840 x 2160 avec miniature
160 x 90 ; liste de fichiers ; `Set-Clipboard` de PowerShell (texte et fichiers, applications tierces réelles) ; texte de 9 millions
de caractères lu, de 11 millions et de 6 millions accentués ignorés avec journal ; presse-papiers occupé 120 ms (réessais, succès) et
900 ms (abandon en 225 ms environ) ; propriétaire en rendu différé qui ne répond jamais (abandon à 2 s) ; application d'origine
(fenêtre nulle, invalide, du processus courant, et un vrai cadre UWP : la Calculatrice est lancée puis son processus arrêté).

- [ ] **Étape 2 : Lancer la vérification et constater l'échec**

Avec la coquille de la tâche 1, le constructeur de `LecteurPressePapiers` lève aussitôt :

```bash
cd essais-pp/verif
dotnet run -c Debug -p:Depot=<chemin absolu du dépôt> -- lecture
```

Résultat attendu : `OK    fenêtre relais créée`, puis `Unhandled exception. System.NotImplementedException: Tâche 21`.

- [ ] **Étape 3 : Implémenter**

Appels Windows (la tâche 22 y ajoutera l'écriture) :

`src/PressePapiers/Natif/NatifPressePapiers.cs`

```csharp
using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>
/// Appels Windows de la lecture du presse-papiers et de la recherche de l'application d'origine
/// (user32 et kernel32). Chaque brique du projet déclare ses propres appels : une même fonction peut figurer ailleurs.
/// </summary>
internal static partial class NatifPressePapiers
{
    /// <summary>Texte Unicode (CF_UNICODETEXT).</summary>
    public const uint CfUnicodeText = 13;

    /// <summary>Image DIB (CF_DIB).</summary>
    public const uint CfDib = 8;

    /// <summary>Image DIB version 5 (CF_DIBV5), seule à porter un canal alpha.</summary>
    public const uint CfDibV5 = 17;

    /// <summary>Liste de fichiers (CF_HDROP).</summary>
    public const uint CfHDrop = 15;

    /// <summary>Ouvre le presse-papiers ; <paramref name="hwndProprietaire"/> à 0 l'associe à la tâche courante.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenClipboard(nint hwndProprietaire);

    /// <summary>Referme le presse-papiers.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseClipboard();

    /// <summary>Vrai si un format est présent (le presse-papiers n'a pas besoin d'être ouvert).</summary>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsClipboardFormatAvailable(uint format);

    /// <summary>Poignée mémoire d'un format ; le presse-papiers doit être ouvert, la mémoire reste à lui.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nint GetClipboardData(uint format);

    /// <summary>Enregistre (ou retrouve) un format nommé ; 0 en cas d'échec.</summary>
    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial uint RegisterClipboardFormat(string nom);

    /// <summary>Fenêtre actuellement au premier plan.</summary>
    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    /// <summary>Fenêtre propriétaire du contenu du presse-papiers (repli pour l'application d'origine).</summary>
    [LibraryImport("user32.dll")]
    public static partial nint GetClipboardOwner();

    /// <summary>Fil et processus d'une fenêtre ; renvoie l'identifiant du fil.</summary>
    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(nint hwnd, out uint idProcessus);

    /// <summary>Énumère les fenêtres filles directes : passer 0 pour <paramref name="apres"/> la première fois.</summary>
    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint FindWindowEx(nint parent, nint apres, string? classe, string? titre);

    /// <summary>Verrouille un bloc mémoire globale et renvoie son adresse.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint GlobalLock(nint memoire);

    /// <summary>Déverrouille un bloc mémoire globale.</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GlobalUnlock(nint memoire);

    /// <summary>Taille en octets d'un bloc mémoire globale (peut dépasser la taille demandée).</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nuint GlobalSize(nint memoire);
}
```

Formats enregistrés :

`src/PressePapiers/Capture/FormatsPressePapiers.cs`

```csharp
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using PressePapiers.Natif;

namespace PressePapiers.Capture;

/// <summary>Formats enregistrés du presse-papiers (RTF, HTML, PNG), enregistrés une seule fois par processus.</summary>
internal static class FormatsPressePapiers
{
    // Seuls les succès sont mémorisés : un échec d'enregistrement est retenté à l'appel suivant.
    private static readonly ConcurrentDictionary<string, uint> Enregistres = new(StringComparer.Ordinal);

    /// <summary>« Rich Text Format » (nom exact du format enregistré par Word et WordPad).</summary>
    public static uint Rtf => Enregistrer("Rich Text Format");

    /// <summary>« HTML Format » (en-tête texte puis document, voir FormatHtml).</summary>
    public static uint Html => Enregistrer("HTML Format");

    /// <summary>« PNG » : usage constaté, non documenté par Microsoft ; sert à garder la transparence d'une image.</summary>
    public static uint Png => Enregistrer("PNG");

    /// <summary>Enregistre un format par son nom.</summary>
    /// <exception cref="InvalidOperationException">Windows refuse l'enregistrement (RegisterClipboardFormat renvoie 0).</exception>
    public static uint Enregistrer(string nom)
    {
        if (Enregistres.TryGetValue(nom, out var connu))
        {
            return connu;
        }

        var format = NatifPressePapiers.RegisterClipboardFormat(nom);
        if (format == 0)
        {
            throw new InvalidOperationException(
                $"Windows a refusé d'enregistrer le format « {nom} » (erreur {Marshal.GetLastPInvokeError()}).");
        }

        Enregistres[nom] = format;
        return format;
    }
}
```

Marque interne :

`src/PressePapiers/Capture/MarqueInterne.cs`

```csharp
namespace PressePapiers.Capture;

/// <summary>Format privé que l'outil pose sur chacune de ses écritures pour ignorer ses propres copies.</summary>
public static class MarqueInterne
{
    /// <summary>Nom du format enregistré.</summary>
    public const string NomFormat = "PressePapiers.Interne";

    /// <summary>Identifiant du format (RegisterClipboardFormat), vérifié non nul.</summary>
    /// <exception cref="InvalidOperationException">Windows refuse d'enregistrer le format.</exception>
    public static uint Format => FormatsPressePapiers.Enregistrer(NomFormat);
}
```

Application d'origine :

`src/PressePapiers/Capture/ApplicationOrigine.cs`

```csharp
using System.ComponentModel;
using System.Diagnostics;
using PressePapiers.Natif;

namespace PressePapiers.Capture;

/// <summary>Nom du processus d'où vient une copie : une indication au mieux, jamais une certitude.</summary>
public static class ApplicationOrigine
{
    private const string HoteApplicationsUwp = "ApplicationFrameHost";

    /// <summary>
    /// Nom du processus de la fenêtre, sans extension. Pour une application UWP, la fenêtre appartient à
    /// ApplicationFrameHost : on cherche alors, parmi ses fenêtres filles, celle d'un autre processus, qui est l'application.
    /// </summary>
    /// <param name="fenetre">Fenêtre au premier plan relevée à la copie, ou 0.</param>
    /// <returns>Le nom du processus (par exemple « Code », « WindowsTerminal »), ou null si rien n'est déterminé.</returns>
    public static string? Determiner(nint fenetre)
    {
        if (fenetre == 0)
        {
            return null;
        }

        var idHote = IdProcessus(fenetre);
        var nom = NomProcessus(idHote);
        if (!string.Equals(nom, HoteApplicationsUwp, StringComparison.OrdinalIgnoreCase))
        {
            return nom;
        }

        // Le nom ApplicationFrameHost n'apprend rien : sans fenêtre fille d'un autre processus, l'origine reste inconnue.
        nint fille = 0;
        while ((fille = NatifPressePapiers.FindWindowEx(fenetre, fille, null, null)) != 0)
        {
            var idFille = IdProcessus(fille);
            if (idFille != 0 && idFille != idHote)
            {
                return NomProcessus(idFille);
            }
        }

        return null;
    }

    /// <summary>
    /// Origine de la copie qui vient d'être signalée : processus de la fenêtre au premier plan ; seulement à défaut,
    /// processus propriétaire du presse-papiers (il peut être un service sans rapport avec la fenêtre que l'utilisateur voit).
    /// À appeler dès la réception de la notification, avant que l'utilisateur ne change de fenêtre.
    /// </summary>
    internal static string? DeterminerPourCopie() =>
        Determiner(NatifPressePapiers.GetForegroundWindow()) ?? Determiner(NatifPressePapiers.GetClipboardOwner());

    private static uint IdProcessus(nint fenetre)
    {
        NatifPressePapiers.GetWindowThreadProcessId(fenetre, out var id);
        return id;
    }

    // Le processus peut avoir disparu entre la copie et la lecture, ou refuser l'accès : l'origine est alors inconnue.
    private static string? NomProcessus(uint idProcessus)
    {
        if (idProcessus == 0)
        {
            return null;
        }

        try
        {
            using var processus = Process.GetProcessById((int)idProcessus);
            return processus.ProcessName;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }
}
```

Résultat intermédiaire de la lecture :

`src/PressePapiers/Capture/ContenuBrut.cs`

```csharp
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Capture;

/// <summary>
/// Ce que le fil de lecture a copié du presse-papiers, avant toute conversion : des octets et du texte seulement.
/// L'image reste un DIB, convertie en PNG après la fermeture du presse-papiers.
/// </summary>
/// <param name="Type">Type d'élément décidé d'après les formats présents.</param>
/// <param name="Texte">Texte brut (Texte, MisEnForme).</param>
/// <param name="Rtf">Octets RTF sans nul final.</param>
/// <param name="Html">Octets « HTML Format » sans nul final.</param>
/// <param name="Dib">Octets du CF_DIBV5 ou du CF_DIB (Image).</param>
/// <param name="Fichiers">Chemins des fichiers (Fichiers).</param>
internal sealed record ContenuBrut(
    TypeElement Type,
    string? Texte = null,
    byte[]? Rtf = null,
    byte[]? Html = null,
    byte[]? Dib = null,
    IReadOnlyList<string>? Fichiers = null);
```

Lecture synchrone du presse-papiers :

`src/PressePapiers/Capture/LectureBrute.cs`

```csharp
using System.Runtime.InteropServices;
using System.Text;
using PressePapiers.Coeur.Formats;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Natif;

namespace PressePapiers.Capture;

/// <summary>
/// Lecture synchrone du presse-papiers : ouvre avec réessais, copie les octets des formats retenus puis referme aussitôt.
/// Elle peut bloquer longtemps si le propriétaire d'un format en rendu différé ne répond pas : l'appelant la lance sur un
/// fil dédié et abandonne l'attente au bout de deux secondes.
/// </summary>
internal static class LectureBrute
{
    /// <summary>Nombre d'essais d'ouverture quand une autre application tient le presse-papiers.</summary>
    public const int EssaisMax = 10;

    /// <summary>Pause entre deux essais d'ouverture.</summary>
    public static readonly TimeSpan PauseEntreEssais = TimeSpan.FromMilliseconds(25);

    /// <summary>Taille maximale d'un texte (en octets UTF-8), d'un RTF ou d'un HTML : au-delà, la copie est ignorée.</summary>
    public const long TailleMaxTexte = 10L * 1024 * 1024;

    /// <summary>Taille maximale d'un DIB (une image 8K en 32 bits fait environ 133 Mo).</summary>
    public const long TailleMaxImage = 256L * 1024 * 1024;

    /// <summary>
    /// Lit le contenu selon l'ordre de la spec (section 4) : CF_HDROP donne Fichiers ; sinon un texte donne MisEnForme
    /// si un RTF ou un HTML l'accompagne, Texte sinon ; sinon CF_DIBV5 puis CF_DIB donnent Image.
    /// </summary>
    /// <returns>Le contenu, ou null (marque interne présente, presse-papiers occupé, vide, illisible ou trop gros).</returns>
    public static ContenuBrut? Lire(IJournal journal)
    {
        if (!Ouvrir(journal))
        {
            return null;
        }

        try
        {
            return LireOuvert(journal);
        }
        finally
        {
            NatifPressePapiers.CloseClipboard();
        }
    }

    // OpenClipboard(NULL) : l'outil ne lie pas la lecture à sa fenêtre relais, qui vit sur un autre fil.
    private static bool Ouvrir(IJournal journal)
    {
        var erreur = 0;
        for (var essai = 1; essai <= EssaisMax; essai++)
        {
            if (NatifPressePapiers.OpenClipboard(0))
            {
                return true;
            }

            erreur = Marshal.GetLastPInvokeError();
            if (essai < EssaisMax)
            {
                Thread.Sleep(PauseEntreEssais);
            }
        }

        journal.Avertissement($"Copie ignorée : le presse-papiers est resté occupé après {EssaisMax} essais (erreur Windows {erreur}).");
        return false;
    }

    private static ContenuBrut? LireOuvert(IJournal journal)
    {
        if (MarquePresente(journal))
        {
            return null;
        }

        if (NatifPressePapiers.IsClipboardFormatAvailable(NatifPressePapiers.CfHDrop))
        {
            return LireFichiers(journal);
        }

        if (NatifPressePapiers.IsClipboardFormatAvailable(NatifPressePapiers.CfUnicodeText))
        {
            return LireTexte(journal);
        }

        return LireImage(journal);
    }

    // Si le format de la marque ne peut pas être enregistré, on lit quand même : mieux vaut un doublon qu'une capture muette.
    private static bool MarquePresente(IJournal journal)
    {
        try
        {
            return NatifPressePapiers.IsClipboardFormatAvailable(MarqueInterne.Format);
        }
        catch (InvalidOperationException exception)
        {
            journal.Avertissement("Marque interne indisponible : les écritures de l'outil ne seront pas reconnues.", exception);
            return false;
        }
    }

    private static ContenuBrut? LireFichiers(IJournal journal)
    {
        var octets = Copier(NatifPressePapiers.GetClipboardData(NatifPressePapiers.CfHDrop), TailleMaxTexte,
            "liste de fichiers", journal);
        if (octets is null)
        {
            return null;
        }

        var chemins = ListeFichiers.DecoderDropFiles(octets);
        if (chemins.Count == 0)
        {
            journal.Avertissement("Copie ignorée : la liste de fichiers est vide ou illisible.");
            return null;
        }

        return new ContenuBrut(TypeElement.Fichiers, Fichiers: chemins);
    }

    private static ContenuBrut? LireTexte(IJournal journal)
    {
        // Le bloc est en UTF-16 : un texte de plus de TailleMaxTexte caractères dépasse forcément la limite en UTF-8,
        // inutile de le copier (le petit surplus tient compte de l'arrondi de GlobalSize).
        var octets = Copier(NatifPressePapiers.GetClipboardData(NatifPressePapiers.CfUnicodeText),
            2 * TailleMaxTexte + 128, "texte", journal, TailleMaxTexte);
        if (octets is null)
        {
            return null;
        }

        var texte = DecoderUtf16(octets);
        var tailleUtf8 = Encoding.UTF8.GetByteCount(texte);
        if (tailleUtf8 > TailleMaxTexte)
        {
            journal.Avertissement($"Copie ignorée : texte de {tailleUtf8 / (1024 * 1024)} Mo, au-delà de {TailleMaxTexte / (1024 * 1024)} Mo.");
            return null;
        }

        if (texte.Length == 0)
        {
            return null;
        }

        var rtf = LireFormatTexte(() => FormatsPressePapiers.Rtf, "RTF", journal);
        var html = LireFormatTexte(() => FormatsPressePapiers.Html, "HTML", journal);
        var type = rtf is not null || html is not null ? TypeElement.MisEnForme : TypeElement.Texte;
        return new ContenuBrut(type, Texte: texte, Rtf: rtf, Html: html);
    }

    // RTF et HTML sont des textes terminés par un nul dans un bloc que GlobalSize peut avoir arrondi : on coupe au premier nul.
    private static byte[]? LireFormatTexte(Func<uint> format, string nom, IJournal journal)
    {
        uint identifiant;
        try
        {
            identifiant = format();
        }
        catch (InvalidOperationException exception)
        {
            journal.Avertissement($"Format {nom} indisponible : mise en forme ignorée.", exception);
            return null;
        }

        if (!NatifPressePapiers.IsClipboardFormatAvailable(identifiant))
        {
            return null;
        }

        var octets = Copier(NatifPressePapiers.GetClipboardData(identifiant), TailleMaxTexte, nom, journal);
        if (octets is null)
        {
            return null;
        }

        var fin = Array.IndexOf(octets, (byte)0);
        var utile = fin < 0 ? octets : octets[..fin];
        return utile.Length == 0 ? null : utile;
    }

    private static ContenuBrut? LireImage(IJournal journal)
    {
        // CF_DIBV5 d'abord : Windows fabrique CF_DIB par conversion, et l'alpha s'y perd.
        foreach (var format in new[] { NatifPressePapiers.CfDibV5, NatifPressePapiers.CfDib })
        {
            if (!NatifPressePapiers.IsClipboardFormatAvailable(format))
            {
                continue;
            }

            var dib = Copier(NatifPressePapiers.GetClipboardData(format), TailleMaxImage, "image", journal);
            if (dib is not null)
            {
                return new ContenuBrut(TypeElement.Image, Dib: dib);
            }
        }

        return null;
    }

    // Texte UTF-16 terminé par un nul : le bloc peut être plus grand que le texte, ou ne pas porter de nul du tout.
    private static string DecoderUtf16(byte[] octets)
    {
        var caracteres = MemoryMarshal.Cast<byte, char>(octets.AsSpan(0, octets.Length & ~1));
        var fin = caracteres.IndexOf('\0');
        return new string(fin < 0 ? caracteres : caracteres[..fin]);
    }

    /// <summary>Copie un bloc mémoire du presse-papiers ; null si la poignée est nulle, vide, verrouillage impossible ou trop gros.</summary>
    private static byte[]? Copier(nint poignee, long tailleMax, string description, IJournal journal, long? limiteAffichee = null)
    {
        if (poignee == 0)
        {
            journal.Avertissement($"Copie ignorée : les données « {description} » n'ont pas pu être obtenues (erreur Windows {Marshal.GetLastPInvokeError()}).");
            return null;
        }

        var taille = (long)NatifPressePapiers.GlobalSize(poignee);
        if (taille <= 0)
        {
            return null;
        }

        if (taille > tailleMax)
        {
            journal.Avertissement($"Copie ignorée : {description} de {taille / (1024 * 1024)} Mo, au-delà de {(limiteAffichee ?? tailleMax) / (1024 * 1024)} Mo.");
            return null;
        }

        var adresse = NatifPressePapiers.GlobalLock(poignee);
        if (adresse == 0)
        {
            journal.Avertissement($"Copie ignorée : verrouillage impossible des données « {description} » (erreur Windows {Marshal.GetLastPInvokeError()}).");
            return null;
        }

        try
        {
            var octets = new byte[taille];
            Marshal.Copy(adresse, octets, 0, (int)taille);
            return octets;
        }
        finally
        {
            NatifPressePapiers.GlobalUnlock(poignee);
        }
    }
}
```

Lecteur asynchrone (fil dédié, délai de 2 secondes, conversion des images) :

`src/PressePapiers/Capture/LecteurPressePapiers.cs`

```csharp
using PressePapiers.Coeur.Formats;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Capture;

/// <summary>Lecture du presse-papiers à chaque copie.</summary>
public sealed class LecteurPressePapiers
{
    /// <summary>
    /// Durée maximale de la lecture native : au-delà, la copie est abandonnée. Elle protège de l'application propriétaire
    /// d'un format en rendu différé qui ne répond pas (la lecture reste alors bloquée sur son fil, que l'on laisse derrière).
    /// </summary>
    internal static readonly TimeSpan DelaiGlobal = TimeSpan.FromSeconds(2);

    private readonly IJournal _journal;

    /// <summary>Crée le lecteur.</summary>
    /// <param name="hwndRelais">
    /// Fenêtre relais de l'outil. La lecture ne s'y accroche pas (elle ouvre le presse-papiers avec une fenêtre nulle, sur son
    /// propre fil) ; le paramètre est conservé pour que l'écrivain et le lecteur se construisent de la même façon.
    /// </param>
    /// <param name="journal">Journal des copies ignorées.</param>
    public LecteurPressePapiers(nint hwndRelais, IJournal journal)
    {
        _ = hwndRelais;
        ArgumentNullException.ThrowIfNull(journal);
        _journal = journal;
    }

    /// <summary>
    /// Lit le contenu : 10 essais espacés de 25 ms ; null si la marque interne est présente, si le contenu est illisible ou vide.
    /// Les formats sont lus dans l'ordre de la spec, section 4. Les images sont converties en PNG avec leur miniature.
    /// </summary>
    /// <param name="application">Application d'origine relevée à la réception de la notification, ou null.</param>
    /// <returns>Le contenu à enregistrer, ou null s'il n'y a rien à garder (jamais d'exception).</returns>
    public async Task<ContenuCopie?> LireAsync(string? application)
    {
        try
        {
            var brut = await LireSurFilDedieAsync();
            return brut is null ? null : await ConvertirAsync(brut, application);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Lecture du presse-papiers impossible : la copie est ignorée.", exception);
            return null;
        }
    }

    // Un fil par lecture : OpenClipboard et GetClipboardData peuvent bloquer sans limite, donc jamais sur le fil d'interface
    // ni sur le pool. Si le délai passe, le fil reste derrière (fil d'arrière-plan) et finit quand le propriétaire répond.
    private async Task<ContenuBrut?> LireSurFilDedieAsync()
    {
        var resultat = new TaskCompletionSource<ContenuBrut?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fil = new Thread(() =>
        {
            try
            {
                resultat.TrySetResult(LectureBrute.Lire(_journal));
            }
            catch (Exception exception)
            {
                _journal.Erreur("Lecture native du presse-papiers en échec.", exception);
                resultat.TrySetResult(null);
            }
        })
        {
            IsBackground = true,
            Name = "Lecture du presse-papiers",
        };
        fil.Start();

        var fini = await Task.WhenAny(resultat.Task, Task.Delay(DelaiGlobal));
        if (fini != resultat.Task)
        {
            _journal.Avertissement(
                $"Copie ignorée : le presse-papiers n'a pas répondu en {DelaiGlobal.TotalSeconds:0} s (propriétaire en rendu différé ?).");
            return null;
        }

        return await resultat.Task;
    }

    // L'image arrive en DIB : conversion en PNG et miniature après la fermeture du presse-papiers, hors du délai de lecture.
    private async Task<ContenuCopie?> ConvertirAsync(ContenuBrut brut, string? application)
    {
        switch (brut.Type)
        {
            case TypeElement.Image:
                try
                {
                    var png = await ConvertisseurImage.DibVersPngAsync(brut.Dib!);
                    var miniature = await ConvertisseurImage.MiniatureAsync(png);
                    return new ContenuCopie
                    {
                        Type = TypeElement.Image,
                        ImagePng = png,
                        Miniature = miniature,
                        Application = application,
                    };
                }
                catch (Exception exception)
                {
                    _journal.Avertissement("Copie ignorée : l'image n'a pas pu être convertie en PNG.", exception);
                    return null;
                }

            case TypeElement.Fichiers:
                return new ContenuCopie { Type = TypeElement.Fichiers, Fichiers = brut.Fichiers, Application = application };

            default:
                return new ContenuCopie
                {
                    Type = brut.Type,
                    Texte = brut.Texte,
                    Rtf = brut.Rtf,
                    Html = brut.Html,
                    Application = application,
                };
        }
    }
}
```

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)`, `0 Erreur(s)`, puis

```
Résumé de série de tests : Réussite!
  total : 513
  échec : 0
  réussie : 513
  ignoré : 0
```

(aucun test ajouté par cette tâche : ce sont les 513 des tâches 1 à 16.)

- [ ] **Étape 5 : Vérifier sur le poste**

La vérification de l'étape 1 modifie le presse-papiers réel : ne rien y avoir de précieux, ne pas copier pendant l'essai (une copie
faite par un autre programme au mauvais moment peut faire échouer un contrôle ; il suffit alors de relancer). Elle lance puis arrête
la Calculatrice de Windows, et laisse un texte neutre dans le presse-papiers.

```bash
cd essais-pp/verif
dotnet run -c Debug -p:Depot=<chemin absolu du dépôt> -- lecture
```

À observer : `31/31 vérifications réussies`, aucun `ECHEC`. Ordres de grandeur relevés sur le poste de référence :
`image 4K : lue, convertie, miniature 141 ms`, `texte 9 M caractères : lu 12 ms`, `occupé 120 ms ... 165 ms`,
`occupé 900 ms : abandon ... 281 ms`, `rendu différé muet : lecture abandonnée ... 2004 ms`, cadre UWP de la Calculatrice résolu en
`CalculatorApp`. Le message `AVERT ... n'a pas répondu en 2 s` du journal est normal (c'est le contrôle du rendu différé).
Deux constats à retenir : `Set-Clipboard -AsHtml` (HTML sans texte) donne `null`, comme prévu par l'ordre de décision ; le cadre
« Paramètres » d'une application UWP suspendue n'a pas de fenêtre fille d'une autre application, son origine reste inconnue (`null`).

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers/Natif/NatifPressePapiers.cs src/PressePapiers/Capture/FormatsPressePapiers.cs src/PressePapiers/Capture/ContenuBrut.cs src/PressePapiers/Capture/LectureBrute.cs src/PressePapiers/Capture/MarqueInterne.cs src/PressePapiers/Capture/ApplicationOrigine.cs src/PressePapiers/Capture/LecteurPressePapiers.cs
git commit -m "Ajouter la lecture du presse-papiers" -m "Marque interne, application d'origine (ApplicationFrameHost résolu par la fenêtre fille) et lecteur sur un fil dédié : 10 essais espacés de 25 ms, abandon après 2 secondes si un propriétaire en rendu différé ne répond pas, texte de plus de 10 Mo ignoré, images converties en PNG avec miniature. AllowUnsafeBlocks ajouté au projet pour le générateur de LibraryImport."
```
