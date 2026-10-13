### Tâche 24 : ServiceRecollage

**Fichiers :**
- Créer : `src/PressePapiers/Natif/NatifSaisie.cs`
- Modifier : `src/PressePapiers/Capture/ServiceRecollage.cs` (le plan donne le contenu complet du fichier après modification)
- Tests : aucun test xUnit (code natif, dépend de la fenêtre au premier plan) ; vérification sur le poste par un programme
  jetable hors du dépôt, `$ESSAIS/Recollage/`

**Interfaces :**
- Utilise :
  - tâche 22 : `EcrivainPressePapiers.EcrireAsync(Element element, ModeCollage mode, string? cheminImage) : Task<bool>` (pose
    toujours la marque interne) ;
  - tâche 5 : `DepotHistorique.CheminImage(Element element) : string` et `DepotHistorique.MarquerUtilise(long elementId)` ;
  - tâche 12 : `DecisionCollage.Decider(...)` et `IssueCollage` ;
  - tâche 19 : `PremierPlan.Amener`, `PremierPlan.FenetreActive`, `PremierPlan.EstOrigineValide`, `NiveauIntegrite.DeFenetre`,
    `NiveauIntegrite.DuProcessusCourant` ;
  - tâche 1 : `Element`, `TypeElement`, `ModeCollage`, `IJournal`.
- Fournit :
  - `public ServiceRecollage(EcrivainPressePapiers ecrivain, DepotHistorique depot, IJournal journal)` (lève
    `ArgumentNullException` sur un argument nul) ;
  - `public Task<IssueCollage> CollerAsync(Element element, ModeCollage mode, nint fenetreOrigine, bool copieSeule, nint hwndOutil, Action masquerFenetre)`.
    À appeler depuis le fil d'interface : les continuations y reviennent, là où `masquerFenetre` doit s'exécuter. Séquence :
    1. écriture par l'écrivain (chemin du PNG pour une image) ; échec : `Echec`, rien d'autre ;
    2. `MarquerUtilise` dès que l'écriture réussit, collage ou non (l'élément remonte en tête de son onglet) ;
    3. `DecisionCollage.Decider` avec : origine connue = `fenetreOrigine != 0` et `PremierPlan.EstOrigineValide`, fenêtre
       existante = `IsWindow`, niveau de la cible lu seulement si la décision en dépend ;
    4. `Colle` : `PremierPlan.Amener(fenetreOrigine)` (refus : `Echec`, fenêtre non masquée), puis `masquerFenetre()`, puis
       attente du relâchement de Win, Maj, Ctrl et Alt (`GetAsyncKeyState`, pas de 10 ms, une seconde au plus, puis Ctrl+V
       quand même avec un avertissement au journal), contrôle que la fenêtre d'origine est toujours au premier plan (sinon
       `Echec` et aucune frappe : coller dans une autre fenêtre serait pire), puis `SendInput` de Ctrl enfoncée, V enfoncée,
       V relâchée, Ctrl relâchée (4 entrées insérées exigées) ;
    5. `CopieFenetreElevee` : pas de Ctrl+V ; `Amener` au mieux puis `masquerFenetre()` (spec 10.3 : la fenêtre est masquée,
       l'appelant notifie « Copié, colle avec Ctrl+V ») ;
    6. `CopieDemandee`, `CopieSansFenetreOrigine`, `CopieFenetreFermee` : la fenêtre n'est PAS masquée, l'appelant affiche son
       message dans l'`InfoBar` ;
    7. toute exception est journalisée et donne `Echec`.
  - internes pour l'essai : `TimeSpan AttenteMaximaleModificateurs` (1 s) et `Func<bool> ModificateursEnfonces` ;
  - `internal static partial class NatifSaisie` (`SendInput`, `GetAsyncKeyState`, `IsWindow`).
  - Consommateurs : tâches 25 à 29 (la vue appelle `CollerAsync`, masque la fenêtre par `masquerFenetre`, suspend la fermeture
    sur perte du focus pendant l'appel).

- [ ] **Étape 1 : Écrire la vérification sur le poste (programme jetable, hors du dépôt)**

À cette tâche, `EcrivainPressePapiers` et `DepotHistorique` appartiennent à d'autres tâches et toucheraient au presse-papiers
réel : l'essai compile donc `ServiceRecollage` et les fichiers de Coeur dont il dépend, avec un écrivain et un dépôt factices
(qui ne touchent à rien), et une fenêtre de test qui reçoit les frappes injectées. Il vérifie la séquence jusqu'à Ctrl+V et
les cas de refus. Il lance et arrête le Bloc-notes pour disposer d'une autre fenêtre au premier plan.

```bash
# Depuis la racine du dépôt : où est le dépôt, où vivent les programmes jetables (hors du dépôt).
DEPOT="$PWD"
ESSAIS="${TEMP}/essais-clavier"
mkdir -p "$ESSAIS"
```

`$ESSAIS/Recollage/EssaiRecollage.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Essai jetable hors dépôt : compile ServiceRecollage du clone avec un écrivain et un dépôt factices
       (les vrais sont des coquilles écrites par d'autres tâches), et vérifie la séquence jusqu'à Ctrl+V. -->
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Platforms>x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>

  <PropertyGroup>
    <!-- Racine du dépôt, passée par -p:Depot=... -->
    <Clone>$(Depot)/src</Clone>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="$(Clone)/PressePapiers/Capture/ServiceRecollage.cs" />
    <Compile Include="$(Clone)/PressePapiers/Systeme/PremierPlan.cs" />
    <Compile Include="$(Clone)/PressePapiers/Systeme/NiveauIntegrite.cs" />
    <Compile Include="$(Clone)/PressePapiers/Natif/NatifSaisie.cs" />
    <Compile Include="$(Clone)/PressePapiers/Natif/NatifPremierPlan.cs" />
    <Compile Include="$(Clone)/PressePapiers/Natif/NatifIntegrite.cs" />
    <Compile Include="$(Clone)/PressePapiers.Coeur/Collage/*.cs" />
    <Compile Include="$(Clone)/PressePapiers.Coeur/Journalisation/*.cs" />
    <Compile Include="$(Clone)/PressePapiers.Coeur/Modele/*.cs" />
  </ItemGroup>

</Project>
```

`$ESSAIS/Recollage/Program.cs`

```csharp
// Essai jetable : ServiceRecollage du clone, avec un écrivain et un dépôt factices. La fenêtre de test reçoit les
// frappes injectées : on vérifie qu'un collage envoie bien Ctrl+V à la fenêtre d'origine, et seulement alors.
// Le presse-papiers de l'utilisateur n'est jamais touché (l'écrivain factice n'écrit rien).
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using PressePapiers.Capture;
using PressePapiers.Coeur.Collage;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Systeme;

namespace PressePapiers.Capture
{
    public sealed class EcrivainPressePapiers
    {
        public bool Reussit = true;
        public int Appels;
        public long InstantAppel;
        public EcrivainPressePapiers(nint hwnd, IJournal journal) { }
        public Task<bool> EcrireAsync(Element element, ModeCollage mode, string? chemin)
        {
            Appels++;
            InstantAppel = Stopwatch.GetTimestamp();
            return Task.FromResult(Reussit);
        }
    }
}

namespace PressePapiers.Coeur.Stockage
{
    public sealed class DepotHistorique
    {
        public readonly List<long> Utilises = new();
        public void MarquerUtilise(long id) => Utilises.Add(id);
        public string CheminImage(Element element) =>
            element.ImageFichier is null ? throw new InvalidOperationException("pas d'image") : "C:/x/" + element.ImageFichier;
    }
}

sealed class JournalConsole : IJournal
{
    public readonly List<string> Lignes = new();
    public void Info(string message) => Lignes.Add("info : " + message);
    public void Avertissement(string message, Exception? exception = null) => Lignes.Add("AVERT : " + message);
    public void Erreur(string message, Exception? exception = null) => Lignes.Add("ERREUR : " + message + " " + exception?.Message);
}

static class Essai
{
    delegate nint ProcFenetre(nint hwnd, uint msg, nint w, nint l);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WNDCLASSEX
    {
        public uint cbSize, style; public nint lpfnWndProc; public int cbClsExtra, cbWndExtra;
        public nint hInstance, hIcon, hCursor, hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName; public nint hIconSm;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct MSG { public nint hwnd; public uint message; public nint wParam, lParam; public uint time; public int x, y; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern ushort RegisterClassEx(ref WNDCLASSEX c);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern nint CreateWindowEx(uint ex, string classe, string titre, uint style, int x, int y, int l, int h, nint parent, nint menu, nint inst, nint param);
    [DllImport("user32.dll")] static extern nint DefWindowProc(nint hwnd, uint msg, nint w, nint l);
    [DllImport("user32.dll")] static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")] static extern bool PeekMessage(out MSG m, nint hwnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG m);
    [DllImport("user32.dll")] static extern nint DispatchMessage(ref MSG m);
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern nint FindWindow(string classe, string? titre);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] static extern short GetKeyState(int vk);

    static readonly List<(uint vk, bool ctrl)> Touches = new();
    static ProcFenetre? _proc;

    static nint Proc(nint hwnd, uint msg, nint w, nint l)
    {
        if (msg is 0x100 or 0x104) Touches.Add(((uint)w, GetKeyState(0x11) < 0));
        return DefWindowProc(hwnd, msg, w, l);
    }

    static void Pomper(int ms)
    {
        var fin = Stopwatch.StartNew();
        while (fin.ElapsedMilliseconds < ms)
        {
            while (PeekMessage(out var m, 0, 0, 0, 1)) { TranslateMessage(ref m); DispatchMessage(ref m); }
            Thread.Sleep(5);
        }
    }

    static int echecs;
    static void Verifier(string libelle, bool ok, string? detail = null)
    {
        Console.WriteLine($"{(ok ? "OK    " : "ECHEC ")} {libelle}{(detail is null ? "" : " : " + detail)}");
        if (!ok) echecs++;
    }

    static string Decrire(nint hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        try { return $"0x{hwnd:X} ({Process.GetProcessById((int)pid).ProcessName})"; } catch { return $"0x{hwnd:X}"; }
    }

    static Element Elt(TypeElement type = TypeElement.Texte, string? image = null) => new()
    {
        Id = 7, OngletId = 1, Type = type, Texte = "essai", ImageFichier = image, Empreinte = new byte[32],
        DateCopie = DateTimeOffset.UtcNow, DateUsage = DateTimeOffset.UtcNow, Taille = 5,
    };

    static async Task<int> Main()
    {
        _proc = Proc;
        var classe = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            lpszClassName = "EssaiRecollage",
        };
        RegisterClassEx(ref classe);
        var origine = CreateWindowEx(0, "EssaiRecollage", "Origine de test", 0x00CF0000 | 0x10000000, 300, 300, 400, 250, 0, 0, 0, 0);
        var outil = CreateWindowEx(0, "EssaiRecollage", "Outil de test", 0x00CF0000, 100, 100, 300, 200, 0, 0, 0, 0);
        Console.WriteLine($"Origine = {Decrire(origine)}, outil = {Decrire(outil)}");

        var t0 = DateTime.Now;
        Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true });
        nint bloc = 0;
        var delai = Stopwatch.StartNew();
        while (delai.ElapsedMilliseconds < 8000 && bloc == 0)
        {
            Pomper(250);
            var fg = GetForegroundWindow();
            GetWindowThreadProcessId(fg, out var pid);
            try { if (Process.GetProcessById((int)pid).ProcessName.Equals("Notepad", StringComparison.OrdinalIgnoreCase)) bloc = fg; } catch { }
        }
        Verifier("Bloc-notes au premier plan au départ", bloc != 0, Decrire(GetForegroundWindow()));

        var journal = new JournalConsole();
        var ecrivain = new EcrivainPressePapiers(0, journal);
        var depot = new DepotHistorique();
        var service = new ServiceRecollage(ecrivain, depot, journal);
        var sequence = new List<string>();

        async Task<(IssueCollage issue, double ms, double msTouche)> Jouer(string nom, nint cible, bool copie, Element? e = null, bool premierPlanBloc = true, nint? outilHwnd = null)
        {
            Touches.Clear(); depot.Utilises.Clear(); journal.Lignes.Clear(); sequence.Clear(); ecrivain.Appels = 0;
            if (premierPlanBloc && bloc != 0) { PremierPlan.Amener(bloc); Pomper(200); }
            var debut = Stopwatch.GetTimestamp();
            var tache = service.CollerAsync(e ?? Elt(), ModeCollage.TexteSeul, cible, copie, outilHwnd ?? outil, () =>
            {
                sequence.Add($"masquer(premier plan = {Decrire(GetForegroundWindow())})");
            });
            while (!tache.IsCompleted) Pomper(10);
            var issue = await tache;
            Pomper(300);
            var ms = Stopwatch.GetElapsedTime(debut).TotalMilliseconds;
            return (issue, ms, 0);
        }

        // 1. Collage normal.
        var r = await Jouer("colle", origine, copie: false);
        Verifier("collage : issue Colle", r.issue == IssueCollage.Colle, r.issue.ToString());
        Console.WriteLine("  frappes recues par la fenetre d origine : " + string.Join(" ", Touches.Select(t => $"{t.vk:X}/{(t.ctrl ? "Ctrl" : "-")}")));
        Verifier("collage : V reçue avec Ctrl enfoncée", Touches.Contains((0x56, true)));
        Verifier("collage : écrit une fois, marqué utilisé", ecrivain.Appels == 1 && depot.Utilises.SequenceEqual(new long[] { 7 }));
        Verifier("collage : masquer appelée une fois, fenêtre d'origine déjà au premier plan", sequence.Count == 1 && sequence[0].Contains(Decrire(origine)), string.Join("; ", sequence));

        // 2. Modificateurs tenus 400 ms : on attend leur relâchement avant Ctrl+V.
        var debutModif = Stopwatch.StartNew();
        service.ModificateursEnfonces = () => debutModif.ElapsedMilliseconds < 400;
        debutModif.Restart();
        r = await Jouer("modificateurs 400 ms", origine, copie: false);
        Verifier("modificateurs tenus 400 ms : Colle", r.issue == IssueCollage.Colle);
        Verifier("modificateurs tenus 400 ms : durée totale entre 400 ms et 1,2 s (Ctrl+V après l'attente)", r.ms is >= 400 and < 1500, $"{r.ms:F0} ms (comprend 200 ms de mise au premier plan du Bloc-notes et 300 ms de pompage)");

        // 3. Modificateurs coincés : une seconde au plus, puis collage quand même.
        service.ModificateursEnfonces = () => true;
        r = await Jouer("modificateurs coincés", origine, copie: false);
        Verifier("modificateurs coincés : Colle après l'attente maximale", r.issue == IssueCollage.Colle && Touches.Contains((0x56, true)));
        Verifier("modificateurs coincés : durée entre 1,3 s et 2,5 s (200 + 1000 + 300 ms)", r.ms is >= 1300 and < 2500, $"{r.ms:F0} ms");
        Verifier("modificateurs coincés : avertissement au journal", journal.Lignes.Any(l => l.StartsWith("AVERT")), string.Join(" | ", journal.Lignes));
        service.ModificateursEnfonces = () => false;

        // 4. Copie seule demandée.
        r = await Jouer("copie seule", origine, copie: true);
        Verifier("copie seule : CopieDemandee, aucune frappe, fenêtre non masquée, écrit et marqué", r.issue == IssueCollage.CopieDemandee && Touches.Count == 0 && sequence.Count == 0 && ecrivain.Appels == 1 && depot.Utilises.Count == 1);

        // 5. Pas de fenêtre d'origine, la barre des tâches, l'outil lui-même.
        r = await Jouer("sans origine", 0, copie: false);
        Verifier("origine 0 : CopieSansFenetreOrigine, aucune frappe", r.issue == IssueCollage.CopieSansFenetreOrigine && Touches.Count == 0 && ecrivain.Appels == 1);
        r = await Jouer("barre", FindWindow("Shell_TrayWnd", null), copie: false);
        Verifier("origine = barre des tâches : CopieSansFenetreOrigine", r.issue == IssueCollage.CopieSansFenetreOrigine && Touches.Count == 0);
        r = await Jouer("outil", outil, copie: false);
        Verifier("origine = l'outil : CopieSansFenetreOrigine", r.issue == IssueCollage.CopieSansFenetreOrigine && Touches.Count == 0);

        // 6. Fenêtre fermée entre-temps.
        var ephemere = CreateWindowEx(0, "EssaiRecollage", "Éphémère", 0x00CF0000, 50, 50, 100, 100, 0, 0, 0, 0);
        DestroyWindow(ephemere);
        r = await Jouer("fermée", ephemere, copie: false);
        Verifier("fenêtre détruite : CopieFenetreFermee, aucune frappe, écrit quand même", r.issue == IssueCollage.CopieFenetreFermee && Touches.Count == 0 && ecrivain.Appels == 1 && sequence.Count == 0);

        // 7. Écriture impossible : Echec, rien d'autre.
        ecrivain.Reussit = false;
        r = await Jouer("écriture", origine, copie: false);
        Verifier("écriture refusée : Echec, aucune frappe, non masquée, non marqué utilisé", r.issue == IssueCollage.Echec && Touches.Count == 0 && sequence.Count == 0 && depot.Utilises.Count == 0);
        ecrivain.Reussit = true;

        // 8. Image sans fichier : exception interne rattrapée.
        r = await Jouer("image", origine, copie: false, e: Elt(TypeElement.Image, image: null));
        Verifier("image sans fichier : Echec sans exception", r.issue == IssueCollage.Echec && Touches.Count == 0, string.Join(" | ", journal.Lignes));

        // 9. Le premier plan change pendant l'attente : Ctrl+V n'est pas envoyé à la mauvaise fenêtre.
        var premier = true;
        service.ModificateursEnfonces = () =>
        {
            if (premier && bloc != 0) { premier = false; PremierPlan.Amener(bloc); }
            return false;
        };
        r = await Jouer("changement de premier plan", origine, copie: false);
        Verifier("premier plan changé pendant l'attente : Echec, aucune frappe vers l'origine", r.issue == IssueCollage.Echec && Touches.Count == 0, $"{r.issue}, frappes = {Touches.Count}");
        service.ModificateursEnfonces = () => false;

        // 10. Fenêtre sans fenêtre de départ au premier plan : retour au Bloc-notes pour une lecture du niveau.
        Console.WriteLine($"Niveau de l'outil = 0x{NiveauIntegrite.DuProcessusCourant():X} ; niveau de l'origine = 0x{NiveauIntegrite.DeFenetre(origine) ?? -1:X}");

        // Nettoyage.
        DestroyWindow(origine); DestroyWindow(outil);
        foreach (var p in Process.GetProcesses())
        {
            try { if (p.ProcessName.Equals("Notepad", StringComparison.OrdinalIgnoreCase) && p.StartTime >= t0.AddSeconds(-1)) { Console.WriteLine($"Arrêt du Bloc-notes pid {p.Id}"); p.Kill(); } } catch { }
        }
        Console.WriteLine(echecs == 0 ? "TOUT EST OK" : $"{echecs} ECHEC(S)");
        return echecs;
    }
}
```

- [ ] **Étape 2 : Lancer la vérification et constater l'échec**

```bash
cd "$ESSAIS/Recollage" && dotnet build -c Debug -p:Depot="$DEPOT"
```

Résultat attendu avant l'étape 3 : échec de compilation, `error CS2001: Fichier source '.../Natif/NatifSaisie.cs' introuvable`.

- [ ] **Étape 3 : Implémenter**

`src/PressePapiers/Natif/NatifSaisie.cs`

```csharp
using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>Appels Windows du recollage : frappe de Ctrl+V et état réel des modificateurs (spec, section 10.2).</summary>
internal static partial class NatifSaisie
{
    internal const uint INPUT_KEYBOARD = 1;
    internal const uint KEYEVENTF_KEYUP = 0x0002;

    internal const ushort VK_CONTROL = 0x11;
    internal const ushort VK_V = 0x56;

    /// <summary>Win gauche, Win droite, Maj, Ctrl, Alt : ce que l'utilisateur peut encore tenir après Entrée ou Maj+Entrée.</summary>
    internal static readonly int[] ModificateursASurveiller = [0x5B, 0x5C, 0x10, 0x11, 0x12];

    /// <summary>KEYBDINPUT.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct EntreeClavier
    {
        public ushort Touche;
        public ushort CodeBalayage;
        public uint Drapeaux;
        public uint Heure;
        public nint InfoSupplementaire;
    }

    /// <summary>INPUT dimensionné sur sa plus grande variante (souris), sinon SendInput refuse la taille.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    internal struct Entree
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public EntreeClavier Clavier;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint SendInput(uint nombre, Entree[] entrees, int tailleEntree);

    [LibraryImport("user32.dll")]
    internal static partial short GetAsyncKeyState(int touche);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindow(nint hwnd);
}
```

`src/PressePapiers/Capture/ServiceRecollage.cs`

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using PressePapiers.Coeur.Collage;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Natif;
using PressePapiers.Systeme;

namespace PressePapiers.Capture;

/// <summary>
/// Remet un élément dans le presse-papiers et le colle dans la fenêtre d'origine (spec, section 10.2).
/// À appeler depuis le fil d'interface : les continuations reviennent sur lui, là où la fenêtre se masque.
/// </summary>
public sealed class ServiceRecollage
{
    private static readonly TimeSpan _pasAttente = TimeSpan.FromMilliseconds(10);

    private readonly EcrivainPressePapiers _ecrivain;
    private readonly DepotHistorique _depot;
    private readonly IJournal _journal;

    /// <summary>Crée le service de recollage.</summary>
    public ServiceRecollage(EcrivainPressePapiers ecrivain, DepotHistorique depot, IJournal journal)
    {
        ArgumentNullException.ThrowIfNull(ecrivain);
        ArgumentNullException.ThrowIfNull(depot);
        ArgumentNullException.ThrowIfNull(journal);
        _ecrivain = ecrivain;
        _depot = depot;
        _journal = journal;
    }

    /// <summary>Durée maximale d'attente du relâchement des modificateurs (une seconde dans la spec).</summary>
    internal TimeSpan AttenteMaximaleModificateurs { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Dit si un modificateur est encore enfoncé. Remplaçable pour l'essai avec de fausses frappes.</summary>
    internal Func<bool> ModificateursEnfonces { get; set; } = ModificateursReelsEnfonces;

    /// <summary>
    /// Spec, section 10.2 : écriture, contrôle de la fenêtre et de son niveau, premier plan, attente des modificateurs, Ctrl+V.
    /// </summary>
    /// <remarks>
    /// L'élément est toujours écrit en premier et remonte en tête de son onglet dès que l'écriture réussit, collé ou non.
    /// <paramref name="masquerFenetre"/> n'est appelée que lorsque la fenêtre doit disparaître : après un collage, et
    /// quand la cible est plus élevée (notification « Copié, colle avec Ctrl+V »). Pour les autres copies seules, la
    /// fenêtre reste ouverte et l'appelant affiche son message. <see cref="IssueCollage.Echec"/> : écriture
    /// impossible, premier plan refusé, ou frappe non envoyée ; la fenêtre n'est alors pas masquée.
    /// </remarks>
    public async Task<IssueCollage> CollerAsync(Element element, ModeCollage mode, nint fenetreOrigine, bool copieSeule,
                                                nint hwndOutil, Action masquerFenetre)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(masquerFenetre);

        try
        {
            var chemin = element.Type == TypeElement.Image ? _depot.CheminImage(element) : null;
            if (!await _ecrivain.EcrireAsync(element, mode, chemin))
            {
                _journal.Erreur($"Écriture de l'élément {element.Id} dans le presse-papiers impossible.");
                return IssueCollage.Echec;
            }

            _depot.MarquerUtilise(element.Id);

            var origineConnue = fenetreOrigine != 0 && PremierPlan.EstOrigineValide(fenetreOrigine, hwndOutil);
            var existe = origineConnue && NatifSaisie.IsWindow(fenetreOrigine);

            // Le niveau n'est lu que s'il peut changer la décision.
            int? niveauCible = existe && !copieSeule ? NiveauIntegrite.DeFenetre(fenetreOrigine) : null;
            var issue = DecisionCollage.Decider(copieSeule, origineConnue, existe, niveauCible, NiveauIntegrite.DuProcessusCourant());

            switch (issue)
            {
                case IssueCollage.Colle:
                    return await ColleAsync(fenetreOrigine, masquerFenetre);

                case IssueCollage.CopieFenetreElevee:
                    // Pas de Ctrl+V (Windows le bloquerait sans le dire) : on rend la main à la fenêtre d'origine,
                    // au mieux, pour que l'utilisateur colle lui-même.
                    PremierPlan.Amener(fenetreOrigine);
                    masquerFenetre();
                    return issue;

                default:
                    return issue;
            }
        }
        catch (Exception exception)
        {
            _journal.Erreur($"Recollage de l'élément {element.Id} en échec.", exception);
            return IssueCollage.Echec;
        }
    }

    private async Task<IssueCollage> ColleAsync(nint fenetreOrigine, Action masquerFenetre)
    {
        // Le premier plan se rend tant que la fenêtre de l'outil est encore active, puis elle se masque.
        if (!PremierPlan.Amener(fenetreOrigine))
        {
            _journal.Avertissement("Le retour au premier plan de la fenêtre d'origine a été refusé par Windows.");
            return IssueCollage.Echec;
        }

        masquerFenetre();
        await AttendreModificateursRelachesAsync();

        // Coller dans une autre fenêtre que celle prévue serait pire que ne pas coller.
        if (PremierPlan.FenetreActive() != fenetreOrigine)
        {
            _journal.Avertissement("Le premier plan a changé avant l'envoi de Ctrl+V : collage abandonné, l'élément reste copié.");
            return IssueCollage.Echec;
        }

        if (!EnvoyerCtrlV())
        {
            _journal.Erreur($"SendInput n'a pas inséré Ctrl+V (erreur {Marshal.GetLastWin32Error()}).");
            return IssueCollage.Echec;
        }

        return IssueCollage.Colle;
    }

    // Win, Maj, Ctrl ou Alt encore tenus (Entrée avec Maj, ou Win+V pas encore relâchée) fausseraient le raccourci :
    // on attend leur relâchement, une seconde au plus, puis on colle quand même.
    private async Task AttendreModificateursRelachesAsync()
    {
        var chrono = Stopwatch.StartNew();
        while (ModificateursEnfonces())
        {
            if (chrono.Elapsed >= AttenteMaximaleModificateurs)
            {
                _journal.Avertissement("Des modificateurs sont restés enfoncés plus d'une seconde ; Ctrl+V est envoyé quand même.");
                return;
            }

            await Task.Delay(_pasAttente);
        }
    }

    private static bool ModificateursReelsEnfonces()
    {
        foreach (var touche in NatifSaisie.ModificateursASurveiller)
            if ((NatifSaisie.GetAsyncKeyState(touche) & 0x8000) != 0) return true;

        return false;
    }

    // Ctrl enfoncée, V enfoncée et relâchée, Ctrl relâchée : une seule frappe atomique pour Windows.
    private static bool EnvoyerCtrlV()
    {
        var entrees = new NatifSaisie.Entree[4];
        Remplir(ref entrees[0], NatifSaisie.VK_CONTROL, relache: false);
        Remplir(ref entrees[1], NatifSaisie.VK_V, relache: false);
        Remplir(ref entrees[2], NatifSaisie.VK_V, relache: true);
        Remplir(ref entrees[3], NatifSaisie.VK_CONTROL, relache: true);

        return NatifSaisie.SendInput((uint)entrees.Length, entrees, Marshal.SizeOf<NatifSaisie.Entree>()) == entrees.Length;
    }

    private static void Remplir(ref NatifSaisie.Entree entree, ushort touche, bool relache)
    {
        entree.Type = NatifSaisie.INPUT_KEYBOARD;
        entree.Clavier.Touche = touche;
        entree.Clavier.Drapeaux = relache ? NatifSaisie.KEYEVENTF_KEYUP : 0;
    }
}
```

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)`, `0 Erreur(s)`, tous les tests existants verts, aucun test ajouté (521 réussis,
0 échec).

- [ ] **Étape 5 : Vérifier sur le poste**

```bash
cd "$ESSAIS/Recollage" && dotnet build -c Debug -p:Depot="$DEPOT" && dotnet run -c Debug --no-build -p:Depot="$DEPOT"
```

Résultat attendu : 18 lignes `OK`, aucune `ECHEC`, puis `TOUT EST OK` (environ 10 s ; ne touchez pas au clavier ni à la souris pendant
l'essai, le Bloc-notes s'ouvre et se ferme). Ce que cela prouve :
- collage : issue `Colle`, la fenêtre d'origine (qui n'était pas au premier plan) reçoit Ctrl puis V avec Ctrl enfoncée
  (`frappes recues : 11/Ctrl 56/Ctrl`), l'élément est écrit une fois et marqué utilisé, `masquerFenetre` est appelée une fois,
  quand la fenêtre d'origine est déjà au premier plan (ordre de la spec) ;
- modificateurs simulés tenus 400 ms : `Colle` après l'attente ; coincés : `Colle` après environ 1 s avec un avertissement au journal ;
- copie seule : `CopieDemandee`, aucune frappe, fenêtre non masquée ; origine 0, barre des tâches, l'outil lui-même :
  `CopieSansFenetreOrigine` ; fenêtre détruite : `CopieFenetreFermee` ; dans tous ces cas l'élément a bien été écrit ;
- écriture refusée : `Echec`, aucune frappe, fenêtre non masquée, élément non marqué utilisé ; image sans fichier :
  `Echec` sans exception ; premier plan changé pendant l'attente des modificateurs : `Echec` et aucune frappe vers la fenêtre
  d'origine ;
- fin d'essai : `Arrêt du Bloc-notes pid ...`.

Non couvert ici, à voir à la recette (spec, section 14) : le collage dans les vraies applications (texte, mis en forme, image,
fichiers), `CopieFenetreElevee` (il faut une fenêtre de niveau d'intégrité supérieur, par exemple le Gestionnaire des tâches :
`Decider` est couvert par la tâche 12, mais la lecture du niveau d'une vraie fenêtre élevée ne l'est que par la tâche 19), et
l'attente de vraies touches tenues (`GetAsyncKeyState` renvoyait 0 pour les frappes injectées dans la session de rédaction).

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers/Natif/NatifSaisie.cs src/PressePapiers/Capture/ServiceRecollage.cs
git commit -m "Ajouter le service de recollage" -m "Ecriture par l'ecrivain, decision de collage, premier plan rendu a la fenetre d'origine avant de masquer l'outil, attente des modificateurs relaches (une seconde au plus) puis Ctrl+V par SendInput. Un premier plan qui change pendant l'attente abandonne le collage plutot que de coller dans une autre fenetre. L'element remonte en tete de son onglet des que l'ecriture reussit."
```
