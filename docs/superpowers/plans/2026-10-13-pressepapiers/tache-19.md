### Tâche 19 : PremierPlan et NiveauIntegrite

**Fichiers :**
- Créer : `src/PressePapiers/Natif/NatifPremierPlan.cs`, `src/PressePapiers/Natif/NatifIntegrite.cs`
- Modifier : `src/PressePapiers/Systeme/PremierPlan.cs`, `src/PressePapiers/Systeme/NiveauIntegrite.cs` (le plan donne le contenu complet de chaque fichier après modification)
- Tests : aucun test xUnit (code natif) ; vérification sur le poste par un programme jetable hors du dépôt, `$ESSAIS/PremierPlan/`

**Interfaces :**
- Utilise (tâche 1) : les coquilles `PremierPlan` et `NiveauIntegrite` (espace de noms `PressePapiers.Systeme`) ; le projet
  application (`src/PressePapiers/PressePapiers.csproj`).
- Fournit :
  - `public static bool PremierPlan.Amener(nint hwnd)` : étape 1 (restaurer si réduite, afficher si cachée), étape 2
    (`SendInput` d'une entrée souris vide puis `SetForegroundWindow`, contrôle par `GetForegroundWindow`), étape 3
    (`AttachThreadInput` du fil courant vers le fil du premier plan et celui de la cible, `BringWindowToTop`,
    `SetForegroundWindow`, détachement toujours exécuté). Renvoie faux si `hwnd` est nul, invalide, ou si Windows refuse
    après l'étape 3 (c'est à l'appelant de prévenir l'utilisateur : spec 10.1, étape 4) ;
  - `internal enum PremierPlan.Etape { DejaAuPremierPlan, SendInputVide, AttachThreadInput, Echec }` et
    `internal static Etape PremierPlan.AmenerParEtapes(nint hwnd)` : comme `Amener`, avec l'étape qui a réussi (diagnostic) ;
  - `public static nint PremierPlan.FenetreActive()` (= `GetForegroundWindow`) ;
  - `public static bool PremierPlan.EstOrigineValide(nint hwnd, nint hwndOutil)` : faux pour 0, l'outil, une fenêtre dont le
    propriétaire racine est l'outil, le bureau (`GetDesktopWindow`, `GetShellWindow`, classes `Progman` et `WorkerW`) et la
    barre des tâches (classes `Shell_TrayWnd` et `Shell_SecondaryTrayWnd`) ;
  - `public static int? NiveauIntegrite.DeFenetre(nint hwnd)` : RID du jeton du processus de la fenêtre
    (`GetWindowThreadProcessId`, `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)`, `OpenProcessToken(TOKEN_QUERY)`,
    `GetTokenInformation(TokenIntegrityLevel)`, dernier sous-identifiant du SID) ; `null` si une étape échoue (0, fenêtre
    inexistante, processus inaccessible), jamais un niveau supposé ;
  - `public static int NiveauIntegrite.DuProcessusCourant()` : lecture unique mise en cache ; renvoie 0 (le plus bas) si la
    lecture échoue, pour que toute cible passe pour plus élevée et que l'outil copie sans coller à tort ;
  - classes natives `internal static partial class NatifPremierPlan` et `NatifIntegrite` (une par brique, `LibraryImport`) ;
  - le projet application déclare `AllowUnsafeBlocks` depuis la tâche 16 (`LibraryImport` génère du code non sûr et refuse
    de compiler sans lui, erreur SYSLIB1062) : le projet ne change pas ici.
  - Consommateurs : tâche 24 (`ServiceRecollage`), tâche 27 (ouverture : `Amener`), tâche 29.

- [ ] **Étape 1 : Écrire la vérification sur le poste (programme jetable, hors du dépôt)**

Le programme compile les quatre fichiers du dépôt par `Compile Include` (le dépôt n'est pas modifié), crée une fenêtre de test
cachée, lance le Bloc-notes au premier plan, appelle `AmenerParEtapes` sur la fenêtre de test, puis revient au Bloc-notes, et
enfin lit les niveaux d'intégrité. Il arrête le Bloc-notes qu'il a lancé.

```bash
# Depuis la racine du dépôt : où est le dépôt, où vivent les programmes jetables (hors du dépôt).
DEPOT="$PWD"
ESSAIS="${TEMP}/essais-clavier"
mkdir -p "$ESSAIS"
```

`$ESSAIS/PremierPlan/EssaiPremierPlan.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Essai jetable hors dépôt : compile les sources du dépôt pour exercer PremierPlan et NiveauIntegrite. -->
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
    <Compile Include="$(Clone)/PressePapiers/Systeme/PremierPlan.cs" />
    <Compile Include="$(Clone)/PressePapiers/Systeme/NiveauIntegrite.cs" />
    <Compile Include="$(Clone)/PressePapiers/Natif/NatifPremierPlan.cs" />
    <Compile Include="$(Clone)/PressePapiers/Natif/NatifIntegrite.cs" />
  </ItemGroup>

</Project>
```

`$ESSAIS/PremierPlan/Program.cs`

```csharp
// Essai jetable : PremierPlan.Amener sur une fenêtre de test pendant que le Bloc-notes est au premier plan,
// et NiveauIntegrite sur l'Explorateur et sur ce processus. Ne modifie aucun réglage.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using PressePapiers.Systeme;

static class Essai
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern nint CreateWindowEx(uint exStyle, string classe, string titre, uint style, int x, int y, int l, int h,
                                      nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern nint FindWindow(string classe, string? titre);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(nint hwnd, StringBuilder t, int max);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint hwnd);

    const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;

    static string Decrire(nint hwnd)
    {
        if (hwnd == 0) return "(aucune)";
        var t = new StringBuilder(128);
        GetWindowText(hwnd, t, t.Capacity);
        GetWindowThreadProcessId(hwnd, out var pid);
        string p;
        try { p = Process.GetProcessById((int)pid).ProcessName; } catch { p = "?"; }
        return $"0x{hwnd:X} \"{t}\" ({p}, pid {pid})";
    }

    static int Main()
    {
        var t0 = DateTime.Now;
        Console.WriteLine($"Mon pid : {Environment.ProcessId}");

        // Fenêtre de test cachée (pas de WS_VISIBLE) : classe STATIC prédéfinie.
        var test = CreateWindowEx(0, "STATIC", "EssaiPremierPlan", WS_OVERLAPPEDWINDOW, 200, 200, 400, 250, 0, 0, 0, 0);
        Console.WriteLine($"Fenêtre de test : {Decrire(test)}, visible au départ : {IsWindowVisible(test)}");

        Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true });
        // Attente du Bloc-notes au premier plan (au plus 8 s).
        var fin = DateTime.UtcNow.AddSeconds(8);
        nint bloc = 0;
        while (DateTime.UtcNow < fin)
        {
            Thread.Sleep(250);
            var fg = GetForegroundWindow();
            GetWindowThreadProcessId(fg, out var pid);
            string nom;
            try { nom = Process.GetProcessById((int)pid).ProcessName; } catch { continue; }
            if (nom.Equals("Notepad", StringComparison.OrdinalIgnoreCase)) { bloc = fg; break; }
        }
        Console.WriteLine($"Premier plan avant : {Decrire(GetForegroundWindow())}");
        Console.WriteLine(bloc != 0 ? "Bloc-notes au premier plan : oui" : "Bloc-notes au premier plan : NON (essai moins probant)");

        var etape = PremierPlan.AmenerParEtapes(test);
        Thread.Sleep(150);
        var apres = GetForegroundWindow();
        Console.WriteLine($"AmenerParEtapes -> {etape} ; premier plan après : {Decrire(apres)} ; fenêtre de test au premier plan : {apres == test}");
        Console.WriteLine($"Amener (déjà au premier plan) -> {PremierPlan.Amener(test)}");

        // Retour au Bloc-notes puis nouvel essai, pour voir si le résultat se répète.
        if (bloc != 0)
        {
            var e2 = PremierPlan.AmenerParEtapes(bloc);
            Thread.Sleep(150);
            Console.WriteLine($"Retour au Bloc-notes -> {e2} ; premier plan : {Decrire(GetForegroundWindow())}");
            var e3 = PremierPlan.AmenerParEtapes(test);
            Thread.Sleep(150);
            Console.WriteLine($"Nouvel essai sur la fenêtre de test -> {e3} ; premier plan : {Decrire(GetForegroundWindow())}");
        }

        Console.WriteLine($"Amener(0) -> {PremierPlan.Amener(0)} ; Amener(handle bidon) -> {PremierPlan.Amener(0x1234)}");

        // EstOrigineValide
        var barre = FindWindow("Shell_TrayWnd", null);
        Console.WriteLine($"EstOrigineValide barre des tâches ({Decrire(barre)}) -> {PremierPlan.EstOrigineValide(barre, test)}");
        Console.WriteLine($"EstOrigineValide fenêtre de test (outil=test) -> {PremierPlan.EstOrigineValide(test, test)}");
        Console.WriteLine($"EstOrigineValide fenêtre de test (outil=autre) -> {PremierPlan.EstOrigineValide(test, 0x99)}");
        Console.WriteLine($"EstOrigineValide Bloc-notes -> {(bloc != 0 ? PremierPlan.EstOrigineValide(bloc, test) : "n/a")}");
        Console.WriteLine($"EstOrigineValide 0 -> {PremierPlan.EstOrigineValide(0, test)}");
        Console.WriteLine($"FenetreActive == GetForegroundWindow -> {PremierPlan.FenetreActive() == GetForegroundWindow()}");

        // NiveauIntegrite
        string Hex(int? n) => n is int v ? $"0x{v:X}" : "null";
        Console.WriteLine($"NiveauIntegrite processus courant -> 0x{NiveauIntegrite.DuProcessusCourant():X}");
        Console.WriteLine($"NiveauIntegrite fenêtre de test (mon processus) -> {Hex(NiveauIntegrite.DeFenetre(test))}");
        Console.WriteLine($"NiveauIntegrite explorer (barre des tâches) -> {Hex(NiveauIntegrite.DeFenetre(barre))}");
        if (bloc != 0) Console.WriteLine($"NiveauIntegrite Bloc-notes -> {Hex(NiveauIntegrite.DeFenetre(bloc))}");
        Console.WriteLine($"NiveauIntegrite 0 -> {Hex(NiveauIntegrite.DeFenetre(0))} ; bidon -> {Hex(NiveauIntegrite.DeFenetre(0x1234))}");

        // Nettoyage : fermeture des Bloc-notes lancés par l'essai.
        DestroyWindow(test);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.ProcessName.Equals("Notepad", StringComparison.OrdinalIgnoreCase) && p.StartTime >= t0.AddSeconds(-1))
                { Console.WriteLine($"Arrêt du Bloc-notes pid {p.Id}"); p.Kill(); }
            }
            catch { }
        }
        return 0;
    }
}
```

- [ ] **Étape 2 : Lancer la vérification et constater l'échec**

Avant l'étape 3, les fichiers natifs n'existent pas :

```bash
cd "$ESSAIS/PremierPlan" && dotnet build -c Debug -p:Depot="$DEPOT"
```

Résultat attendu : échec de compilation, `error CS2001: Fichier source '.../src/PressePapiers/Natif/NatifPremierPlan.cs' introuvable`
(et de même pour `NatifIntegrite.cs`).

- [ ] **Étape 3 : Implémenter**

`src/PressePapiers/Natif/NatifPremierPlan.cs`

```csharp
using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>Appels Windows de la brique Premier plan (spec, section 10.1). Chaque brique native est autonome.</summary>
internal static unsafe partial class NatifPremierPlan
{
    internal const int SW_SHOW = 5;
    internal const int SW_RESTORE = 9;
    internal const uint INPUT_MOUSE = 0;

    /// <summary>GA_ROOTOWNER : fenêtre propriétaire de plus haut niveau.</summary>
    internal const uint GA_ROOTOWNER = 3;

    /// <summary>MOUSEINPUT (32 octets en x64, remplissage compris).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct EntreeSouris
    {
        public int Dx;
        public int Dy;
        public uint DonneesSouris;
        public uint Drapeaux;
        public uint Heure;
        public nint InfoSupplementaire;
    }

    /// <summary>INPUT dimensionné sur sa plus grande variante, sinon SendInput refuse la taille.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    internal struct Entree
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public EntreeSouris Souris;
    }

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool BringWindowToTop(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint hwnd, int commande);

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint hwnd, out uint idProcessus);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AttachThreadInput(uint idFilSource, uint idFilCible, [MarshalAs(UnmanagedType.Bool)] bool attacher);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint SendInput(uint nombre, in Entree entrees, int tailleEntree);

    [LibraryImport("user32.dll")]
    internal static partial nint GetDesktopWindow();

    [LibraryImport("user32.dll")]
    internal static partial nint GetShellWindow();

    [LibraryImport("user32.dll")]
    internal static partial nint GetAncestor(nint hwnd, uint drapeau);

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW")]
    internal static partial int GetClassName(nint hwnd, char* nom, int tailleMax);

    [LibraryImport("kernel32.dll")]
    internal static partial uint GetCurrentThreadId();
}
```

`src/PressePapiers/Natif/NatifIntegrite.cs`

```csharp
using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>Appels Windows de la lecture du niveau d'intégrité (spec, section 10.3). Chaque brique native est autonome.</summary>
internal static partial class NatifIntegrite
{
    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    internal const uint TOKEN_QUERY = 0x0008;

    /// <summary>Valeur TOKEN_INFORMATION_CLASS pour TokenIntegrityLevel.</summary>
    internal const int TokenIntegrityLevel = 25;

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint hwnd, out uint idProcessus);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial nint OpenProcess(uint acces, [MarshalAs(UnmanagedType.Bool)] bool heriter, uint idProcessus);

    /// <summary>Pseudo-handle du processus courant : inutile de le fermer.</summary>
    [LibraryImport("kernel32.dll")]
    internal static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint handle);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool OpenProcessToken(nint processus, uint acces, out nint jeton);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetTokenInformation(nint jeton, int classe, nint tampon, uint tailleTampon, out uint tailleRequise);

    /// <summary>Renvoie un pointeur vers l'octet qui compte les sous-autorités du SID.</summary>
    [LibraryImport("advapi32.dll")]
    internal static partial nint GetSidSubAuthorityCount(nint sid);

    /// <summary>Renvoie un pointeur vers la sous-autorité demandée du SID.</summary>
    [LibraryImport("advapi32.dll")]
    internal static partial nint GetSidSubAuthority(nint sid, uint index);
}
```

`src/PressePapiers/Systeme/PremierPlan.cs`

```csharp
using System.Runtime.InteropServices;
using PressePapiers.Natif;

namespace PressePapiers.Systeme;

/// <summary>
/// Amène une fenêtre au premier plan (spec, section 10.1). Windows interdit à un processus d'arrière-plan de
/// voler le premier plan : on applique, dans l'ordre, les deux recettes éprouvées par PowerToys, CopyQ et Ditto,
/// sans jamais toucher au réglage ForegroundLockTimeout de l'utilisateur.
/// </summary>
public static class PremierPlan
{
    /// <summary>Étape qui a fini par donner le premier plan, relevée pour le diagnostic.</summary>
    internal enum Etape
    {
        /// <summary>La fenêtre l'avait déjà (ou l'a obtenue dès la restauration).</summary>
        DejaAuPremierPlan,

        /// <summary>SendInput d'une entrée souris vide, puis SetForegroundWindow.</summary>
        SendInputVide,

        /// <summary>AttachThreadInput, puis SetForegroundWindow.</summary>
        AttachThreadInput,

        /// <summary>Windows a refusé : l'appelant doit le dire à l'utilisateur.</summary>
        Echec,
    }

    // Classes de la barre des tâches (principale et secondaires) et du bureau : jamais une fenêtre d'origine.
    private static readonly string[] _classesExclues = ["Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW"];

    /// <summary>Restaure la fenêtre si besoin puis la met au premier plan ; renvoie faux si Windows refuse.</summary>
    public static bool Amener(nint hwnd) => AmenerParEtapes(hwnd) != Etape.Echec;

    /// <summary>Comme <see cref="Amener"/>, mais indique l'étape qui a réussi.</summary>
    internal static Etape AmenerParEtapes(nint hwnd)
    {
        if (hwnd == 0 || !NatifPremierPlan.IsWindow(hwnd)) return Etape.Echec;

        // Étape 1 : une fenêtre réduite est restaurée, une fenêtre cachée est affichée.
        if (NatifPremierPlan.IsIconic(hwnd)) NatifPremierPlan.ShowWindow(hwnd, NatifPremierPlan.SW_RESTORE);
        else if (!NatifPremierPlan.IsWindowVisible(hwnd)) NatifPremierPlan.ShowWindow(hwnd, NatifPremierPlan.SW_SHOW);

        if (EstAuPremierPlan(hwnd)) return Etape.DejaAuPremierPlan;

        // Étape 2 : une entrée souris vide fait de l'outil le dernier fournisseur d'entrée, ce qui rend
        // SetForegroundWindow recevable. Technique non documentée, utilisée en production par PowerToys.
        EnvoyerEntreeVide();
        NatifPremierPlan.SetForegroundWindow(hwnd);
        if (EstAuPremierPlan(hwnd)) return Etape.SendInputVide;

        // Étape 3 : partage de la file d'entrée avec les fils concernés pendant l'appel.
        AmenerParAttachement(hwnd);
        return EstAuPremierPlan(hwnd) ? Etape.AttachThreadInput : Etape.Echec;
    }

    /// <summary>Fenêtre actuellement au premier plan.</summary>
    public static nint FenetreActive() => NatifPremierPlan.GetForegroundWindow();

    /// <summary>Vrai si la fenêtre peut servir de fenêtre d'origine : ni l'outil, ni le bureau, ni la barre des tâches.</summary>
    public static bool EstOrigineValide(nint hwnd, nint hwndOutil)
    {
        if (hwnd == 0 || hwnd == hwndOutil) return false;
        if (hwnd == NatifPremierPlan.GetDesktopWindow() || hwnd == NatifPremierPlan.GetShellWindow()) return false;

        // Une fenêtre fille de l'outil (boîte de dialogue, fenêtre flottante) vaut l'outil lui-même.
        if (hwndOutil != 0 && NatifPremierPlan.GetAncestor(hwnd, NatifPremierPlan.GA_ROOTOWNER) == hwndOutil) return false;

        return Array.IndexOf(_classesExclues, LireClasse(hwnd)) < 0;
    }

    private static bool EstAuPremierPlan(nint hwnd) => NatifPremierPlan.GetForegroundWindow() == hwnd;

    private static void EnvoyerEntreeVide()
    {
        // Entrée entièrement à zéro : aucun déplacement, aucun bouton, aucun effet visible.
        var entree = new NatifPremierPlan.Entree { Type = NatifPremierPlan.INPUT_MOUSE };
        NatifPremierPlan.SendInput(1, in entree, Marshal.SizeOf<NatifPremierPlan.Entree>());
    }

    private static void AmenerParAttachement(nint hwnd)
    {
        var courant = NatifPremierPlan.GetCurrentThreadId();
        var filPremierPlan = NatifPremierPlan.GetWindowThreadProcessId(NatifPremierPlan.GetForegroundWindow(), out _);
        var filCible = NatifPremierPlan.GetWindowThreadProcessId(hwnd, out _);

        // Le fil courant se lie à chaque autre fil concerné (premier plan, cible), une seule fois chacun.
        Span<uint> candidats = [filPremierPlan, filCible];
        Span<uint> attaches = stackalloc uint[2];
        var nombre = 0;

        foreach (var fil in candidats)
        {
            if (fil == 0 || fil == courant || attaches[..nombre].Contains(fil)) continue;
            if (NatifPremierPlan.AttachThreadInput(courant, fil, true)) attaches[nombre++] = fil;
        }

        try
        {
            NatifPremierPlan.BringWindowToTop(hwnd);
            NatifPremierPlan.SetForegroundWindow(hwnd);
        }
        finally
        {
            // Toujours détacher : un attachement oublié lierait durablement les files d'entrée.
            for (var i = 0; i < nombre; i++) NatifPremierPlan.AttachThreadInput(courant, attaches[i], false);
        }
    }

    private static unsafe string LireClasse(nint hwnd)
    {
        var tampon = stackalloc char[64];
        var longueur = NatifPremierPlan.GetClassName(hwnd, tampon, 64);
        return longueur > 0 ? new string(tampon, 0, longueur) : string.Empty;
    }
}
```

`src/PressePapiers/Systeme/NiveauIntegrite.cs`

```csharp
using System.Runtime.InteropServices;
using PressePapiers.Natif;

namespace PressePapiers.Systeme;

/// <summary>
/// Niveau d'intégrité des processus (spec, section 10.3). Windows bloque sans rien dire un Ctrl+V envoyé vers une
/// fenêtre de niveau supérieur : le collage compare donc le niveau de la cible à celui de l'outil.
/// </summary>
public static class NiveauIntegrite
{
    // Le niveau d'un processus ne change pas pendant sa vie : une seule lecture suffit.
    private static readonly Lazy<int> _niveauCourant = new(LireNiveauCourant);

    /// <summary>RID du niveau d'intégrité du processus d'une fenêtre ; null si une étape de la lecture échoue.</summary>
    public static int? DeFenetre(nint hwnd)
    {
        if (hwnd == 0) return null;
        if (NatifIntegrite.GetWindowThreadProcessId(hwnd, out var idProcessus) == 0 || idProcessus == 0) return null;

        var processus = NatifIntegrite.OpenProcess(NatifIntegrite.PROCESS_QUERY_LIMITED_INFORMATION, false, idProcessus);
        if (processus == 0) return null;
        try
        {
            return LireDuProcessus(processus);
        }
        finally
        {
            NatifIntegrite.CloseHandle(processus);
        }
    }

    /// <summary>
    /// RID du niveau d'intégrité du processus courant. Si la lecture échoue, renvoie 0 (le plus bas) : toute cible
    /// passe alors pour plus élevée et l'outil copie sans coller, au lieu de coller à tort.
    /// </summary>
    public static int DuProcessusCourant() => _niveauCourant.Value;

    private static int LireNiveauCourant() => LireDuProcessus(NatifIntegrite.GetCurrentProcess()) ?? 0;

    // Jeton du processus, TokenIntegrityLevel, puis dernier sous-identifiant du SID : c'est le RID du niveau
    // (0x1000 bas, 0x2000 moyen, 0x3000 élevé, 0x4000 système).
    private static int? LireDuProcessus(nint processus)
    {
        if (!NatifIntegrite.OpenProcessToken(processus, NatifIntegrite.TOKEN_QUERY, out var jeton)) return null;
        try
        {
            NatifIntegrite.GetTokenInformation(jeton, NatifIntegrite.TokenIntegrityLevel, 0, 0, out var taille);
            if (taille == 0) return null;

            var tampon = Marshal.AllocHGlobal((int)taille);
            try
            {
                if (!NatifIntegrite.GetTokenInformation(jeton, NatifIntegrite.TokenIntegrityLevel, tampon, taille, out _))
                    return null;

                // TOKEN_MANDATORY_LABEL commence par le pointeur vers le SID.
                var sid = Marshal.ReadIntPtr(tampon);
                if (sid == 0) return null;

                var pointeurNombre = NatifIntegrite.GetSidSubAuthorityCount(sid);
                if (pointeurNombre == 0) return null;
                var nombre = Marshal.ReadByte(pointeurNombre);
                if (nombre == 0) return null;

                var pointeurRid = NatifIntegrite.GetSidSubAuthority(sid, (uint)(nombre - 1));
                return pointeurRid == 0 ? null : Marshal.ReadInt32(pointeurRid);
            }
            finally
            {
                Marshal.FreeHGlobal(tampon);
            }
        }
        finally
        {
            NatifIntegrite.CloseHandle(jeton);
        }
    }
}
```

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)`, `0 Erreur(s)`, tous les tests existants verts, aucun test ajouté (513 réussis,
0 échec).

- [ ] **Étape 5 : Vérifier sur le poste**

```bash
cd "$ESSAIS/PremierPlan" && dotnet build -c Debug -p:Depot="$DEPOT" && dotnet run -c Debug --no-build -p:Depot="$DEPOT"
```

Le Bloc-notes s'ouvre un instant au premier plan, puis la fenêtre de test le remplace, puis le Bloc-notes revient. À observer :
- `AmenerParEtapes -> DejaAuPremierPlan` pour la fenêtre de test cachée : l'étape 1 (`ShowWindow`) a suffi, car le processus venait
  de lancer le Bloc-notes ; `fenêtre de test au premier plan : True` ;
- `Retour au Bloc-notes` (retour vers une autre application, le cas du recollage) : `SendInputVide` ou `AttachThreadInput`, et le
  premier plan est bien le Bloc-notes. Relevé sur trois passages du poste de rédaction : `SendInputVide` 4 fois sur 6 retours
  vers une autre fenêtre, `AttachThreadInput` 2 fois sur 6, aucun `Echec` : les deux étapes servent ;
- `Amener(0) -> False`, `Amener(handle bidon) -> False` ;
- `EstOrigineValide` : barre des tâches `False`, fenêtre de test avec outil = elle-même `False`, avec un autre outil `True`,
  Bloc-notes `True`, 0 `False` ;
- `NiveauIntegrite` : processus courant, fenêtre de test, Explorateur et Bloc-notes en `0x2000` (niveau moyen) ; `0` et un
  handle bidon donnent `null`. Un niveau `0x3000` n'a pas pu être lu faute de processus élevé sous la main : à relever à la
  recette avec le Gestionnaire des tâches (spec 10.3 annonce `0x3000` contre `0x2000`) ;
- en fin d'essai, `Arrêt du Bloc-notes pid ...` et plus aucun processus lancé par l'essai.

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers/Natif/NatifPremierPlan.cs src/PressePapiers/Natif/NatifIntegrite.cs src/PressePapiers/Systeme/PremierPlan.cs src/PressePapiers/Systeme/NiveauIntegrite.cs
git commit -m "Ajouter le premier plan et le niveau d'integrite" -m "PremierPlan applique les trois etapes de la spec (restauration, SendInput vide puis SetForegroundWindow, AttachThreadInput) et dit laquelle a reussi. NiveauIntegrite lit le RID du jeton d'un processus ; toute etape en echec donne null, jamais un niveau suppose. Le projet application autorise le code non sur, exige par LibraryImport."
```
