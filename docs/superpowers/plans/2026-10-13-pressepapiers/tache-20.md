### Tâche 20 : ICrochetWinV et CrochetWinV

**Fichiers :**
- Créer : `src/PressePapiers/Natif/NatifClavier.cs`
- Modifier : `src/PressePapiers/Systeme/CrochetWinV.cs` (le plan donne le contenu complet du fichier après modification)
- Inchangé : `src/PressePapiers/Systeme/ICrochetWinV.cs` (écrit à la tâche 1, repris ci-dessous pour mémoire)
- Tests : aucun test xUnit (hook système) ; vérification sur le poste par un programme jetable hors du dépôt, `$ESSAIS/Crochet/`

**Interfaces :**
- Utilise :
  - tâche 11 : `AutomateWinV` (`Traiter`, `Reinitialiser`, `VkV`) et `ActionCrochet` ;
  - tâche 1 : `IJournal` (`Info`, `Avertissement`, `Erreur`), `ICrochetWinV` ;
  - tâche 16 : la ligne `AllowUnsafeBlocks` du projet application (obligatoire pour `LibraryImport`).
- Fournit :
  - `public sealed class CrochetWinV : ICrochetWinV` avec `public CrochetWinV(IJournal journal)` ;
  - `Demarrer()` : lance le fil dédié (un seul crochet vivant par processus, sinon `InvalidOperationException`), attend au plus
    5 s que l'installation soit jouée ; appelé deux fois, sans effet. `EstActif` et `RaisonInactif` (`SetWindowsHookEx a échoué
    (erreur n)` en cas d'échec, `null` quand actif) ;
  - `event Action<nint> WinVPresse` : levé sur le fil du hook (nommé « CrochetWinV »), hors du rappel : le rappel relève
    `GetForegroundWindow()` et poste un message au fil, la boucle de messages lève l'événement. Un abonné qui lève une exception
    est journalisé, la boucle continue ;
  - `Reinstaller()` : poste un message au fil du hook (sans effet avant `Demarrer` ou après `Dispose`). Sur le fil : si Win, Ctrl,
    Alt ou Maj est enfoncée, report de 300 ms (100 reports au plus, puis abandon avec avertissement), sinon pose du nouveau hook
    PUIS retrait de l'ancien, remise à zéro de l'automate. Si la pose échoue, l'ancien reste en place ;
  - une réinstallation périodique toutes les 15 minutes, sur le même fil et par la même voie (spec 11) : la tâche 29 n'a donc à
    brancher que `RelaisWindows.SessionDeverrouillee` et `SortieDeVeille` sur `Reinstaller()` ;
  - `Dispose()` : `WM_QUIT` au fil, retrait du hook, attente de 2 s au plus ; idempotent ;
  - internes : `static long CrochetWinV.FrappesVues` (nombre de frappes vues, sans aucune touche conservée) et
    `Func<bool> ModificateursEnfonces` (remplaçable par l'essai, car `GetAsyncKeyState` ne suit pas les frappes injectées dans
    la session de rédaction) ;
  - `internal static partial class NatifClavier` (hook, boucle de messages, `SendInput`, minuteries).
  - Comportement du rappel : injectée, tout est laissé ; Win+V enfoncée avec Win réellement tenue (`GetAsyncKeyState` de
    `VK_LWIN` ou `VK_RWIN`) : avalée, fenêtre relevée, message posté ; relâchement de Win après un V avalé : touche `0xE8`
    envoyée (appui et relâchement) AVANT de laisser passer le relâchement. Toute exception du rappel est avalée et la frappe
    laissée (une exception ne doit jamais remonter dans Windows).
  - Consommateurs : tâche 29 (`WinVPresse` vers le `DispatcherQueue`, `Reinstaller` sur déverrouillage et sortie de veille,
    `EstActif`/`RaisonInactif` vers `FenetrePrincipaleVue.EtatCrochet` et l'icône).

Rappel de `ICrochetWinV` (tâche 1, inchangé) :

`src/PressePapiers/Systeme/ICrochetWinV.cs`

```csharp
namespace PressePapiers.Systeme;

/// <summary>Hook clavier qui reprend Win+V, isolé derrière une interface pour pouvoir être remplacé.</summary>
public interface ICrochetWinV : IDisposable
{
    /// <summary>Vrai tant que le hook est posé.</summary>
    bool EstActif { get; }

    /// <summary>Raison de l'inactivité, affichée dans les Paramètres ; null si le hook est actif.</summary>
    string? RaisonInactif { get; }

    /// <summary>Levé sur le fil du hook ; l'argument est la fenêtre au premier plan.</summary>
    event Action<nint>? WinVPresse;

    /// <summary>Démarre le fil du hook.</summary>
    void Demarrer();

    /// <summary>Pose un nouveau hook avant de retirer l'ancien (spec, section 11).</summary>
    void Reinstaller();
}
```

- [ ] **Étape 1 : Écrire la vérification sur le poste (programme jetable, hors du dépôt)**

Le programme compile le hook du dépôt et ne simule jamais un vrai Win+V (les frappes injectées sont ignorées exprès) : il envoie
la touche `0xE8` (sans effet), compte les frappes vues par le rappel, poste le message d'ouverture au fil du hook, simule des
modificateurs enfoncés par le point d'injection, force des passages du ramasse-miettes et vérifie l'arrêt propre.

```bash
# Depuis la racine du dépôt : où est le dépôt, où vivent les programmes jetables (hors du dépôt).
DEPOT="$PWD"
ESSAIS="${TEMP}/essais-clavier"
mkdir -p "$ESSAIS"
```

`$ESSAIS/Crochet/EssaiCrochet.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Essai jetable hors dépôt : compile le hook du clone et le fait tourner sans jamais simuler un vrai Win+V. -->
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
    <Compile Include="$(Clone)/PressePapiers/Systeme/CrochetWinV.cs" />
    <Compile Include="$(Clone)/PressePapiers/Systeme/ICrochetWinV.cs" />
    <Compile Include="$(Clone)/PressePapiers/Natif/NatifClavier.cs" />
    <ProjectReference Include="$(Clone)/PressePapiers.Coeur/PressePapiers.Coeur.csproj" />
  </ItemGroup>

</Project>
```

`$ESSAIS/Crochet/Program.cs`

```csharp
// Essai jetable du hook Win+V du clone : démarrage, réception de frappes injectées (touche 0xE8, sans effet),
// réinstallation, arrêt propre. Un vrai Win+V n'est jamais simulé : les frappes injectées sont ignorées exprès.
using System.Diagnostics;
using System.Runtime.InteropServices;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Systeme;

sealed class JournalConsole : IJournal
{
    public void Info(string message) => Console.WriteLine($"  [journal] info : {message}");
    public void Avertissement(string message, Exception? exception = null) => Console.WriteLine($"  [journal] AVERT : {message} {exception?.Message}");
    public void Erreur(string message, Exception? exception = null) => Console.WriteLine($"  [journal] ERREUR : {message} {exception?.Message}");
}

sealed class JournalCompteur : IJournal
{
    public int Reinstalles;
    public void Info(string message) { if (message.Contains("réinstallé")) Interlocked.Increment(ref Reinstalles); }
    public void Avertissement(string message, Exception? exception = null) => Console.WriteLine($"  [journal] AVERT : {message}");
    public void Erreur(string message, Exception? exception = null) => Console.WriteLine($"  [journal] ERREUR : {message}");
}

static class Essai
{
    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public nint extra; }
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public KEYBDINPUT ki; }
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);

    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint idFil, uint msg, nint w, nint l);

    static void ToucheMaj(bool bas)
    {
        var e = new INPUT[1];
        e[0].type = 1; e[0].ki.wVk = 0x10; e[0].ki.dwFlags = bas ? 0u : 2u;
        SendInput(1, e, Marshal.SizeOf<INPUT>());
    }

    static void FrapperSansEffet()
    {
        var e = new INPUT[2];
        e[0].type = 1; e[0].ki.wVk = 0xE8;
        e[1].type = 1; e[1].ki.wVk = 0xE8; e[1].ki.dwFlags = 2;
        SendInput(2, e, Marshal.SizeOf<INPUT>());
    }

    static int echecs;
    static void Verifier(string libelle, bool ok, string? detail = null)
    {
        Console.WriteLine($"{(ok ? "OK    " : "ECHEC ")} {libelle}{(detail is null ? "" : " : " + detail)}");
        if (!ok) echecs++;
    }

    static long Frappes()
    {
        var t = typeof(CrochetWinV).GetProperty("FrappesVues", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (long)t!.GetValue(null)!;
    }

    static int Main()
    {
        int filsAvant = Process.GetCurrentProcess().Threads.Count;
        var journal = new JournalConsole();
        var crochet = new CrochetWinV(journal);
        int signaux = 0;
        crochet.WinVPresse += _ => Interlocked.Increment(ref signaux);

        Verifier("avant Demarrer : EstActif faux", !crochet.EstActif);
        Verifier("avant Demarrer : une raison est donnée", !string.IsNullOrEmpty(crochet.RaisonInactif), crochet.RaisonInactif);

        crochet.Demarrer();
        Verifier("après Demarrer : EstActif vrai", crochet.EstActif);
        Verifier("après Demarrer : RaisonInactif nulle", crochet.RaisonInactif is null);
        crochet.Demarrer();
        Verifier("second Demarrer : sans effet ni erreur", crochet.EstActif);

        using (var autre = new CrochetWinV(journal))
        {
            bool leve = false;
            try { autre.Demarrer(); } catch (InvalidOperationException) { leve = true; }
            Verifier("second crochet simultané : InvalidOperationException", leve);
        }

        var avant = Frappes();
        FrapperSansEffet();
        Thread.Sleep(300);
        var delta = Frappes() - avant;
        Verifier("frappes injectées 0xE8 (appui + relâchement) vues par le rappel", delta >= 2, $"delta = {delta}");
        Verifier("aucun signal WinVPresse sur une frappe injectée", signaux == 0, $"signaux = {signaux}");

        // Réinstallation : si l'ancien hook restait, chaque frappe serait vue deux fois (delta 4 au lieu de 2).
        crochet.Reinstaller();
        Thread.Sleep(500);
        Verifier("après Reinstaller : EstActif vrai", crochet.EstActif);
        avant = Frappes();
        FrapperSansEffet();
        Thread.Sleep(300);
        delta = Frappes() - avant;
        Verifier("après Reinstaller : une seule fois par frappe (ancien hook retiré)", delta == 2, $"delta = {delta} (4 si l'ancien restait ; un écart peut venir d'une frappe réelle pendant l'essai)");

        for (int i = 0; i < 5; i++) crochet.Reinstaller();
        Thread.Sleep(800);
        avant = Frappes();
        FrapperSansEffet();
        Thread.Sleep(300);
        delta = Frappes() - avant;
        Verifier("5 Reinstaller de suite : toujours actif et un seul hook", crochet.EstActif && delta == 2, $"delta = {delta}");

        // Signal WinVPresse : on poste au fil du hook le message qu'enverrait le rappel après un Win+V avalé.
        var idFil = (uint)typeof(CrochetWinV).GetField("_idFilCrochet", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
        nint recu = 0; int filRecu = 0; string? nomFil = null;
        crochet.WinVPresse += h => { recu = h; filRecu = Environment.CurrentManagedThreadId; nomFil = Thread.CurrentThread.Name; };
        PostThreadMessage(idFil, 0x8000 + 1, 0x1234, 0);
        Thread.Sleep(300);
        Verifier("message WinV posé au fil : WinVPresse levé avec la fenêtre relevée", recu == 0x1234, $"fenêtre = 0x{recu:X}");
        Verifier("WinVPresse levé sur le fil du hook, pas sur le fil appelant", nomFil == "CrochetWinV" && filRecu != Environment.CurrentManagedThreadId, $"fil = {nomFil}");

        // Réinstallation reportée tant qu'un modificateur est enfoncé (modificateur simulé par le point d injection).
        var journalPose = new JournalCompteur(); var enfonce = false;
        using (var report = new CrochetWinV(journalPose))
        {
            crochet.Dispose();
            report.ModificateursEnfonces = () => enfonce;
            report.Demarrer();
            var avantReinstall = journalPose.Reinstalles;
            enfonce = true;
            try
            {
                report.Reinstaller();
                Thread.Sleep(900);
                Verifier("Maj enfoncée : réinstallation reportée", journalPose.Reinstalles == avantReinstall, $"réinstallations = {journalPose.Reinstalles - avantReinstall}");
            }
            finally { enfonce = false; }
            Thread.Sleep(900);
            Verifier("Maj relâchée : réinstallation faite après le report", journalPose.Reinstalles == avantReinstall + 1, $"réinstallations = {journalPose.Reinstalles - avantReinstall}");
        }

        crochet.Dispose();
        Verifier("après Dispose : EstActif faux", !crochet.EstActif);
        avant = Frappes();
        FrapperSansEffet();
        Thread.Sleep(300);
        delta = Frappes() - avant;
        Verifier("après Dispose : plus aucune frappe vue (hook retiré)", delta == 0, $"delta = {delta}");

        crochet.Dispose();
        crochet.Reinstaller();
        Verifier("Dispose répété et Reinstaller après Dispose : sans erreur", true);

        // Un nouveau crochet peut reprendre la place après l'arrêt du premier.
        using (var suivant = new CrochetWinV(journal))
        {
            suivant.Demarrer();
            Verifier("nouveau crochet après Dispose : actif", suivant.EstActif);
        }

        GC.Collect(); GC.WaitForPendingFinalizers(); Thread.Sleep(300);
        int filsApres = Process.GetCurrentProcess().Threads.Count;
        Verifier("aucun fil du hook ne reste", filsApres <= filsAvant + 1, $"fils avant = {filsAvant}, après = {filsApres}");

        // Tenir le hook un moment sous GC forcé : le délégué gardé en champ ne doit pas être libéré.
        using (var gc = new CrochetWinV(journal))
        {
            gc.Demarrer();
            for (int i = 0; i < 5; i++) { GC.Collect(2, GCCollectionMode.Forced, true); FrapperSansEffet(); Thread.Sleep(60); }
            avant = Frappes();
            FrapperSansEffet();
            Thread.Sleep(200);
            Verifier("après GC forcés : le rappel répond encore", Frappes() - avant >= 2);
        }

        Console.WriteLine(echecs == 0 ? "TOUT EST OK" : $"{echecs} ECHEC(S)");
        return echecs;
    }
}
```

- [ ] **Étape 2 : Lancer la vérification et constater l'échec**

```bash
cd "$ESSAIS/Crochet" && dotnet build -c Debug -p:Depot="$DEPOT"
```

Résultat attendu avant l'étape 3 : échec de compilation, `error CS2001: Fichier source '.../Natif/NatifClavier.cs' introuvable`.

- [ ] **Étape 3 : Implémenter**

`src/PressePapiers/Natif/NatifClavier.cs`

```csharp
using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>Appels Windows du hook clavier bas niveau (spec, section 11). Chaque brique native est autonome.</summary>
internal static partial class NatifClavier
{
    internal const int WH_KEYBOARD_LL = 13;
    internal const int WM_KEYDOWN = 0x0100;
    internal const int WM_KEYUP = 0x0101;
    internal const int WM_SYSKEYDOWN = 0x0104;
    internal const int WM_SYSKEYUP = 0x0105;
    internal const uint WM_QUIT = 0x0012;
    internal const uint WM_TIMER = 0x0113;
    internal const uint WM_APP = 0x8000;
    internal const uint PM_NOREMOVE = 0x0000;
    internal const uint WM_USER = 0x0400;

    /// <summary>Drapeau LLKHF_INJECTED de KBDLLHOOKSTRUCT.flags : frappe produite par SendInput.</summary>
    internal const uint LLKHF_INJECTED = 0x10;

    internal const uint INPUT_KEYBOARD = 1;
    internal const uint KEYEVENTF_KEYUP = 0x0002;

    internal const int VK_SHIFT = 0x10;
    internal const int VK_CONTROL = 0x11;
    internal const int VK_MENU = 0x12;
    internal const int VK_LWIN = 0x5B;
    internal const int VK_RWIN = 0x5C;

    /// <summary>Touche « sans effet » envoyée avant le relâchement de Win pour que le menu Démarrer reste fermé.</summary>
    internal const ushort VK_MASQUE = 0xE8;

    /// <summary>Décalages des champs de KBDLLHOOKSTRUCT (vkCode, scanCode, flags, time).</summary>
    internal const int DecalageCodeTouche = 0;
    internal const int DecalageDrapeaux = 8;

    /// <summary>MSG avec son champ de fin : la structure fait 48 octets en x64.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Message
    {
        public nint Fenetre;
        public uint Identifiant;
        public nint WParam;
        public nint LParam;
        public uint Heure;
        public int X;
        public int Y;
        public uint Prive;
    }

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

    /// <summary>Signature de LowLevelKeyboardProc ; le délégué doit être gardé vivant par l'appelant.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint ProcedureHook(int code, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    internal static partial nint SetWindowsHookEx(int idHook, nint procedure, nint module, uint idFil);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnhookWindowsHookEx(nint crochet);

    [LibraryImport("user32.dll")]
    internal static partial nint CallNextHookEx(nint crochet, int code, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
    internal static partial int GetMessage(out Message message, nint fenetre, uint min, uint max);

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PeekMessage(out Message message, nint fenetre, uint min, uint max, uint retrait);

    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostThreadMessage(uint idFil, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    internal static partial short GetAsyncKeyState(int touche);

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint SendInput(uint nombre, Entree[] entrees, int tailleEntree);

    [LibraryImport("user32.dll")]
    internal static partial nuint SetTimer(nint fenetre, nuint idTimer, uint delaiMs, nint rappel);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool KillTimer(nint fenetre, nuint idTimer);

    [LibraryImport("kernel32.dll")]
    internal static partial uint GetCurrentThreadId();

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint GetModuleHandle(string? nom);
}
```

`src/PressePapiers/Systeme/CrochetWinV.cs`

```csharp
using System.Runtime.InteropServices;
using PressePapiers.Coeur.Clavier;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Natif;

namespace PressePapiers.Systeme;

/// <summary>
/// Hook clavier bas niveau (WH_KEYBOARD_LL) qui reprend Win+V (spec, section 11). Le hook vit sur un fil dédié avec
/// sa boucle de messages. Son rappel ne fait que décider (<see cref="AutomateWinV"/>), envoyer au besoin la touche
/// sans effet et poster un message : Windows retire sans prévenir un hook trop lent. Le signal <see cref="WinVPresse"/>
/// est levé par la boucle du fil du hook, hors du rappel, et ne touche jamais à l'interface.
/// </summary>
public sealed class CrochetWinV : ICrochetWinV
{
    // Messages postés au fil du hook.
    private const uint MessageWinV = NatifClavier.WM_APP + 1;
    private const uint MessageReinstaller = NatifClavier.WM_APP + 2;

    private static readonly TimeSpan _delaiDemarrage = TimeSpan.FromSeconds(5);
    private const uint DelaiReportMs = 300;
    private const int ReportsMaximum = 100;
    private const uint DelaiPeriodiqueMs = 15 * 60 * 1000;

    // Le rappel est une méthode statique unique, son délégué est gardé dans un champ statique : sans cela le
    // ramasse-miettes le libérerait et Windows appellerait une adresse morte. L'état de l'automate est partagé
    // par le hook courant et le nouveau hook posé pendant une réinstallation.
    private static readonly NatifClavier.ProcedureHook _rappel = Rappel;
    private static readonly nint _pointeurRappel = Marshal.GetFunctionPointerForDelegate(_rappel);
    private static readonly AutomateWinV _automate = new();

    // Un seul crochet vivant à la fois : la procédure statique ne sait parler qu'à lui.
    private static CrochetWinV? _actuel;
    private static nint _crochetCourant;
    private static uint _idFilCrochet;

    private static long _frappesVues;

    private readonly IJournal _journal;
    private readonly ManualResetEventSlim _pret = new(false);
    private readonly object _verrou = new();
    private Thread? _fil;
    private volatile bool _actif;
    private volatile string? _raison = "Le hook n'a pas été démarré.";
    private bool _arrete;

    // État touché uniquement par le fil du hook.
    private nuint _minuterieReport;
    private int _reports;

    /// <summary>Crée le hook, sans le démarrer.</summary>
    public CrochetWinV(IJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        _journal = journal;
    }

    /// <summary>
    /// Dit si Win, Ctrl, Alt ou Maj est enfoncée. Remplaçable pour l'essai : GetAsyncKeyState ne suit pas les
    /// frappes injectées dans certaines sessions, donc le report de réinstallation ne s'exerce pas avec de fausses frappes.
    /// </summary>
    internal Func<bool> ModificateursEnfonces { get; set; } = ModificateursReelsEnfonces;

    /// <summary>Nombre de frappes vues par le rappel depuis le début du processus (diagnostic, aucune touche conservée).</summary>
    internal static long FrappesVues => Interlocked.Read(ref _frappesVues);

    /// <inheritdoc />
    public bool EstActif => _actif;

    /// <inheritdoc />
    public string? RaisonInactif => _actif ? null : _raison;

    /// <inheritdoc />
    public event Action<nint>? WinVPresse;

    /// <inheritdoc />
    public void Demarrer()
    {
        lock (_verrou)
        {
            ObjectDisposedException.ThrowIf(_arrete, this);
            if (_fil is not null) return;

            if (Interlocked.CompareExchange(ref _actuel, this, null) is not null)
                throw new InvalidOperationException("Un autre crochet Win+V est déjà en service dans ce processus.");

            _fil = new Thread(BoucleDuFil) { Name = "CrochetWinV", IsBackground = true };
            _fil.Start();
        }

        // Le fil dit quand l'installation est jouée, réussie ou non ; on n'attend que quelques secondes.
        if (!_pret.Wait(_delaiDemarrage))
        {
            _raison = "Le fil du hook n'a pas répondu à temps.";
            _journal.Erreur(_raison);
        }
    }

    /// <inheritdoc />
    public void Reinstaller()
    {
        uint idFil;
        lock (_verrou)
        {
            if (_fil is null || _arrete) return;
            idFil = _idFilCrochet;
        }

        // Le remplacement se joue sur le fil du hook, à l'abri des frappes en cours de traitement.
        if (!NatifClavier.PostThreadMessage(idFil, MessageReinstaller, 0, 0))
            _journal.Avertissement("Réinstallation du hook impossible : le message n'a pas pu être posté.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Thread? fil;
        uint idFil;
        lock (_verrou)
        {
            if (_arrete) return;
            _arrete = true;
            fil = _fil;
            idFil = _idFilCrochet;
        }

        if (fil is not null)
        {
            NatifClavier.PostThreadMessage(idFil, NatifClavier.WM_QUIT, 0, 0);
            if (!fil.Join(TimeSpan.FromSeconds(2))) _journal.Avertissement("Le fil du hook ne s'est pas arrêté à temps.");
        }

        _actif = false;
        Interlocked.CompareExchange(ref _actuel, null, this);
        _pret.Dispose();
    }

    private void BoucleDuFil()
    {
        // Un premier PeekMessage crée la file de messages du fil : sans elle PostThreadMessage échouerait.
        NatifClavier.PeekMessage(out _, 0, NatifClavier.WM_USER, NatifClavier.WM_USER, NatifClavier.PM_NOREMOVE);
        _idFilCrochet = NatifClavier.GetCurrentThreadId();

        var minuteriePeriodique = NatifClavier.SetTimer(0, 0, DelaiPeriodiqueMs, 0);

        InstallerPremierCrochet();
        _pret.Set();

        // Boucle de messages obligatoire : sans elle Windows n'appelle jamais le hook bas niveau.
        while (NatifClavier.GetMessage(out var message, 0, 0, 0) > 0)
        {
            switch (message.Identifiant)
            {
                case MessageWinV:
                    LeverWinVPresse(message.WParam);
                    break;
                case MessageReinstaller:
                    TenterReinstallation(depuisReport: false);
                    break;
                case NatifClavier.WM_TIMER when (nuint)message.WParam == minuteriePeriodique:
                    TenterReinstallation(depuisReport: false);
                    break;
                case NatifClavier.WM_TIMER when _minuterieReport != 0 && (nuint)message.WParam == _minuterieReport:
                    TenterReinstallation(depuisReport: true);
                    break;
            }
        }

        if (minuteriePeriodique != 0) NatifClavier.KillTimer(0, minuteriePeriodique);
        ArreterReport();
        RetirerCrochet();
    }

    private void InstallerPremierCrochet()
    {
        var crochet = PoserCrochet();
        if (crochet == 0)
        {
            var erreur = Marshal.GetLastWin32Error();
            _raison = $"SetWindowsHookEx a échoué (erreur {erreur}).";
            _journal.Erreur(_raison);
            return;
        }

        _automate.Reinitialiser();
        _crochetCourant = crochet;
        _raison = null;
        _actif = true;
        _journal.Info("Hook Win+V posé.");
    }

    // Remplace le hook. Le neuf est posé avant le retrait de l'ancien : posé en tête de chaîne, le neuf avale
    // la frappe avant l'ancien, qui ne la reçoit donc pas ; il n'y a jamais de fenêtre sans hook.
    private void TenterReinstallation(bool depuisReport)
    {
        if (ModificateursEnfonces())
        {
            // Remplacer le hook pendant qu'une touche est tenue ferait perdre un relâchement à l'automate.
            if (!depuisReport) _reports = 0;
            if (++_reports > ReportsMaximum)
            {
                ArreterReport();
                _journal.Avertissement("Réinstallation du hook abandonnée : des modificateurs sont restés enfoncés.");
                return;
            }

            if (_minuterieReport == 0) _minuterieReport = NatifClavier.SetTimer(0, 0, DelaiReportMs, 0);
            return;
        }

        ArreterReport();

        var neuf = PoserCrochet();
        if (neuf == 0)
        {
            _journal.Avertissement($"Réinstallation du hook impossible (erreur {Marshal.GetLastWin32Error()}) ; l'ancien reste en place.");
            return;
        }

        var ancien = _crochetCourant;
        _crochetCourant = neuf;
        if (ancien != 0) NatifClavier.UnhookWindowsHookEx(ancien);

        // Le hook ne voit pas les relâchements survenus devant une fenêtre élevée ou l'écran de verrouillage.
        _automate.Reinitialiser();
        _reports = 0;
        _raison = null;
        _actif = true;
        _journal.Info("Hook Win+V réinstallé.");
    }

    private void ArreterReport()
    {
        if (_minuterieReport == 0) return;
        NatifClavier.KillTimer(0, _minuterieReport);
        _minuterieReport = 0;
    }

    private void RetirerCrochet()
    {
        var crochet = _crochetCourant;
        _crochetCourant = 0;
        _actif = false;
        if (crochet != 0) NatifClavier.UnhookWindowsHookEx(crochet);
    }

    private void LeverWinVPresse(nint fenetre)
    {
        try
        {
            WinVPresse?.Invoke(fenetre);
        }
        catch (Exception exception)
        {
            // Un abonné défaillant ne doit pas arrêter la boucle du hook.
            _journal.Erreur("Un abonné de WinVPresse a levé une exception.", exception);
        }
    }

    private static nint PoserCrochet() =>
        NatifClavier.SetWindowsHookEx(NatifClavier.WH_KEYBOARD_LL, _pointeurRappel, NatifClavier.GetModuleHandle(null), 0);

    private static bool EstEnfoncee(int touche) => (NatifClavier.GetAsyncKeyState(touche) & 0x8000) != 0;

    private static bool ModificateursReelsEnfonces() =>
        EstEnfoncee(NatifClavier.VK_LWIN) || EstEnfoncee(NatifClavier.VK_RWIN) || EstEnfoncee(NatifClavier.VK_CONTROL)
        || EstEnfoncee(NatifClavier.VK_MENU) || EstEnfoncee(NatifClavier.VK_SHIFT);

    // Procédure unique du hook. Elle doit rendre la main très vite et ne jamais laisser filer une exception.
    private static nint Rappel(int code, nint wParam, nint lParam)
    {
        if (code < 0) return NatifClavier.CallNextHookEx(_crochetCourant, code, wParam, lParam);

        try
        {
            Interlocked.Increment(ref _frappesVues);

            var touche = (uint)Marshal.ReadInt32(lParam, NatifClavier.DecalageCodeTouche);
            var drapeaux = (uint)Marshal.ReadInt32(lParam, NatifClavier.DecalageDrapeaux);
            var message = (int)wParam;
            var enfoncee = message is NatifClavier.WM_KEYDOWN or NatifClavier.WM_SYSKEYDOWN;
            var relachee = message is NatifClavier.WM_KEYUP or NatifClavier.WM_SYSKEYUP;
            if (!enfoncee && !relachee) return NatifClavier.CallNextHookEx(_crochetCourant, code, wParam, lParam);

            var injectee = (drapeaux & NatifClavier.LLKHF_INJECTED) != 0;

            // L'état réel de Win n'est utile (et lu) que pour V : les autres touches ne l'interrogent pas.
            var winReelle = touche == AutomateWinV.VkV && !injectee
                            && (EstEnfoncee(NatifClavier.VK_LWIN) || EstEnfoncee(NatifClavier.VK_RWIN));

            switch (_automate.Traiter(touche, enfoncee, injectee, winReelle))
            {
                case ActionCrochet.Avaler:
                    return 1;

                case ActionCrochet.AvalerEtOuvrir:
                    // La fenêtre active est relevée ici, dans le rappel, avant que quoi que ce soit ne la change.
                    NatifClavier.PostThreadMessage(_idFilCrochet, MessageWinV, NatifClavier.GetForegroundWindow(), 0);
                    return 1;

                case ActionCrochet.MasquerPuisLaisser:
                    EnvoyerToucheSansEffet();
                    break;
            }
        }
        catch
        {
            // Rien ne doit remonter dans Windows ; la frappe suit son chemin.
        }

        return NatifClavier.CallNextHookEx(_crochetCourant, code, wParam, lParam);
    }

    // Une touche sans effet (code 0xE8) glissée avant le relâchement de Win : Windows ne voit plus « Win seule »
    // et n'ouvre pas le menu Démarrer.
    private static void EnvoyerToucheSansEffet()
    {
        var entrees = new NatifClavier.Entree[2];
        entrees[0].Type = NatifClavier.INPUT_KEYBOARD;
        entrees[0].Clavier.Touche = NatifClavier.VK_MASQUE;
        entrees[1].Type = NatifClavier.INPUT_KEYBOARD;
        entrees[1].Clavier.Touche = NatifClavier.VK_MASQUE;
        entrees[1].Clavier.Drapeaux = NatifClavier.KEYEVENTF_KEYUP;
        NatifClavier.SendInput(2, entrees, Marshal.SizeOf<NatifClavier.Entree>());
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
cd "$ESSAIS/Crochet" && dotnet build -c Debug -p:Depot="$DEPOT" && dotnet run -c Debug --no-build -p:Depot="$DEPOT"
```

Résultat attendu : 21 lignes `OK`, aucune `ECHEC`, puis `TOUT EST OK`, en une dizaine de secondes. Ce que cela prouve :
- `EstActif` vrai après `Demarrer`, faux avant et après `Dispose` ; `RaisonInactif` nulle quand actif ; second `Demarrer` sans
  effet ; un second crochet simultané lève `InvalidOperationException` ;
- `delta = 2` pour une frappe injectée (appui et relâchement vus par le rappel), et toujours `2` après `Reinstaller` et après
  cinq `Reinstaller` de suite : l'ancien hook est bien retiré (s'il restait, chaque frappe serait vue deux fois : `4`). Un
  écart peut venir d'une vraie touche frappée pendant l'essai : relancer ;
- aucun `WinVPresse` sur une frappe injectée ; le message d'ouverture posté au fil lève `WinVPresse` avec la fenêtre relevée
  (`0x1234` dans l'essai), sur le fil nommé « CrochetWinV » et pas sur le fil appelant ;
- réinstallation reportée tant que le modificateur simulé est « enfoncé » (0 réinstallation), faite après sa libération (1) ;
- après `Dispose` : plus aucune frappe vue (hook retiré), `Dispose` répété et `Reinstaller` après `Dispose` sans erreur,
  nouveau crochet possible, aucun fil laissé, rappel toujours appelé après des `GC.Collect` forcés (délégué gardé en champ).

Limites de cette vérification, à reprendre à la recette de la spec (section 14) : un vrai Win+V ne peut pas être simulé, donc
l'avalement réel, le masque `0xE8` contre le menu Démarrer et l'ouverture devant une fenêtre élevée restent à voir à la
main. Dans la session de rédaction, `GetAsyncKeyState` renvoyait `0` même pour des frappes injectées (Maj, Ctrl, Alt, y compris
par code de balayage) : le contrôle `winReellementEnfoncee` du rappel n'a donc pas pu être exercé avec une vraie
touche Windows. Si Win+V n'est pas avalé à la recette alors que l'état « actif » est affiché, c'est le premier point à
examiner.

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers/Natif/NatifClavier.cs src/PressePapiers/Systeme/CrochetWinV.cs
git commit -m "Ajouter le hook clavier Win+V" -m "Fil dedie avec boucle de messages, procedure statique unique et delegue garde dans un champ. Le rappel decide par AutomateWinV, envoie la touche sans effet 0xE8 avant le relachement de Win et poste le message ; WinVPresse est leve par la boucle du fil, hors du rappel, avec la fenetre relevee dans le rappel. Reinstaller pose le nouveau hook avant de retirer l'ancien, reporte de 300 ms tant qu'un modificateur est enfonce, et une reinstallation periodique de 15 minutes tourne sur le meme fil."
```
