### Tâche 17 : RelaisWindows, la fenêtre relais Win32

**Fichiers :**
- Créer : `src/PressePapiers/Natif/NatifRelais.cs`
- Modifier : `src/PressePapiers/Systeme/RelaisWindows.cs` (le plan donne le contenu complet après modification)
- Tests : aucun test xUnit possible (fenêtre, messages et raccourci globaux de Windows). Vérification sur le poste par un
  programme jetable hors du dépôt, dans `%LOCALAPPDATA%\Temp\essais-relais\` : `EssaisRelais.csproj`, `synchro.sh`,
  `Interop.cs`, `Programme.cs`, `Essais.cs`, `EssaisIcone.cs`. Il recopie les sources du dépôt et les exerce avec sa
  propre boucle de messages.

**Interfaces :**
- Utilise :
  - `PressePapiers.Coeur.Journalisation.IJournal` (tâche 1) : `void Info(string message)`,
    `void Avertissement(string message, Exception? exception = null)`, `void Erreur(string message, Exception? exception = null)` ;
  - `PressePapiers.Coeur.Reglages.Raccourci` (tâche 3) : `uint Modificateurs` (MOD_CONTROL, MOD_ALT, MOD_SHIFT et
    MOD_NOREPEAT 0x4000 déjà combinés, directement passables à `RegisterHotKey`), `uint ToucheVirtuelle`, `ToString()` ;
  - la coquille `RelaisWindows` de la tâche 1 : toutes ses signatures publiques restent identiques.
- Fournit (signatures du contrat, inchangées) :
  - `public sealed class RelaisWindows : IDisposable` dans `PressePapiers.Systeme` ;
  - `public const uint MessageIcone = 0x8001;` ; `public RelaisWindows(IJournal journal)` ; `public nint Handle { get; }` ;
  - événements `Action? CopieDetectee`, `Action? RaccourciSecoursPresse`, `Action<uint>? MessageIconeRecu` (LOWORD de lParam),
    `Action? BarreDesTachesRecreee`, `Action? SessionDeverrouillee`, `Action? SortieDeVeille` ;
  - `public bool EcouterPressePapiers()` ; `public bool EnregistrerRaccourci(Raccourci raccourci, out int codeErreur)` ;
    `public void RetirerRaccourci()` ; `public void Dispose()` ;
  - `internal static partial class NatifRelais` dans `PressePapiers.Natif` (déclarations Win32 du relais ; aucune autre
    tâche ne s'en sert, chaque brique native reste autonome).

**Points d'attention pour la suite :**
- Le relais se crée et se libère sur le fil d'interface : `DestroyWindow` n'agit que sur le fil créateur (sinon un
  avertissement est écrit au journal et la fenêtre reste). Dans `App`, créer le relais dans `OnLaunched`, où la boucle
  de messages de WinUI le sert.
- `EnregistrerRaccourci` remplace le raccourci précédent, et rétablit l'ancien si le nouveau est refusé : le code
  d'erreur 1409 (ERROR_HOTKEY_ALREADY_REGISTERED) signifie « combinaison prise par une autre application » (tâche 28 :
  message dans les Paramètres, tâche 29 : message au démarrage).
- Le message `TaskbarCreated` est autorisé par `ChangeWindowMessageFilterEx` ; la fenêtre n'est pas « message-only ».
- Une exception levée par un abonné d'un événement est journalisée (`Erreur`) et ne traverse jamais la procédure de
  fenêtre : dans du code natif elle tuerait le processus.
- `AllowUnsafeBlocks`, exigé dès la première déclaration `LibraryImport` (erreur SYSLIB1062 sinon), est déjà dans le
  projet de l'application depuis la tâche 16 : le projet ne change pas ici.

- [ ] **Étape 1 : Écrire la vérification sur le poste (programme jetable, hors du dépôt)**

Ce code natif n'a pas de test unitaire possible : la vérification est un programme d'essai qui exerce le vrai relais sur
le vrai poste (copie réelle par PowerShell, raccourci réellement frappé par `SendInput`, messages réellement postés à la
fenêtre). Préparer le dossier :

```bash
# À lancer depuis le dépôt. Le programme d'essai vit hors du dépôt, dans un dossier à chemin court.
DEPOT="$(git rev-parse --show-toplevel)"
ESSAIS="$LOCALAPPDATA/Temp/essais-relais"
mkdir -p "$ESSAIS"
```

Créer `$ESSAIS/EssaisRelais.csproj` :

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Programme jetable hors dépôt : recopie les sources du relais et de l'icône (synchro.sh) et les exerce
       avec sa propre boucle de messages. -->
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <PlatformTarget>x64</PlatformTarget>
    <RootNamespace>EssaisRelais</RootNamespace>
    <NoWarn>CS1591</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <None Include="src\Assets\*.ico" Link="Assets\%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

Créer `$ESSAIS/synchro.sh` (recopie les sources du relais et de l'icône du dépôt dans `$ESSAIS/src`, que le projet compile) :

```bash
#!/bin/bash
# Recopie les sources du relais et de l'icône du dépôt dans le programme jetable (dossier src/ à côté de ce script).
# Usage : bash synchro.sh <racine du dépôt>
set -e
depot="${1:?Usage : bash synchro.sh <racine du dépôt>}"
ici="$(cd "$(dirname "$0")" && pwd)"
cible="$ici/src"
rm -rf "$cible"
mkdir -p "$cible/Natif" "$cible/Systeme" "$cible/Coeur" "$cible/Assets"
for f in Natif/NatifRelais.cs Natif/NatifIcone.cs Systeme/RelaisWindows.cs Systeme/IconeNotification.cs; do
  [ -f "$depot/src/PressePapiers/$f" ] && cp "$depot/src/PressePapiers/$f" "$cible/$f"
done
cp "$depot/src/PressePapiers.Coeur/Journalisation/IJournal.cs" "$depot/src/PressePapiers.Coeur/Reglages/Raccourci.cs" "$cible/Coeur/"
cp "$depot/src/PressePapiers/Assets/"*.ico "$cible/Assets/" 2>/dev/null || true
echo "Sources recopiées dans $cible"
```

Créer `$ESSAIS/Interop.cs` :

```csharp
using System.Runtime.InteropServices;

namespace EssaisRelais;

/// <summary>Appels Win32 propres au programme d'essai (boucle de messages, envoi de frappes, contrôles).</summary>
internal static class Interop
{
    public const uint WmUser = 0x0400;
    public const uint WmQuit = 0x0012;
    public const uint WmWtsSessionChange = 0x02B1;
    public const uint WmPowerBroadcast = 0x0218;
    public const uint VkCtrl = 0xA2, VkAlt = 0xA4, VkMaj = 0xA0;

    [StructLayout(LayoutKind.Sequential)]
    public struct Msg
    {
        public nint Hwnd;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    // INPUT de clavier : l'union commence au décalage 8 en x64 et la structure fait 40 octets.
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct Saisie
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public ushort Touche;
        [FieldOffset(10)] public ushort Scan;
        [FieldOffset(12)] public uint Drapeaux;
        [FieldOffset(16)] public uint Temps;
        [FieldOffset(24)] public nint Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IdentifiantIcone
    {
        public uint Taille;
        public nint Hwnd;
        public uint Id;
        public Guid Guid;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Gauche, Haut, Droite, Bas;
    }

    public delegate bool EnumFenetres(nint hwnd, nint param);

    [DllImport("user32.dll")] public static extern int GetMessageW(out Msg msg, nint hwnd, uint min, uint max);
    [DllImport("user32.dll")] public static extern bool TranslateMessage(in Msg msg);
    [DllImport("user32.dll")] public static extern nint DispatchMessageW(in Msg msg);
    [DllImport("user32.dll")] public static extern bool PostThreadMessageW(uint fil, uint message, nint w, nint l);
    [DllImport("user32.dll")] public static extern bool PostMessageW(nint hwnd, uint message, nint w, nint l);
    [DllImport("user32.dll")] public static extern nint SendMessageW(nint hwnd, uint message, nint w, nint l);
    [DllImport("user32.dll")] public static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] public static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int touche);
    [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint nombre, Saisie[] saisies, int taille);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessageW(string nom);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(nint hwnd, char[] nom, int max);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumFenetres rappel, nint param);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("shell32.dll")] public static extern int Shell_NotifyIconGetRect(in IdentifiantIcone icone, out Rect rect);

    /// <summary>Enfonce une touche sans la relâcher.</summary>
    public static void Appuyer(uint touche) =>
        SendInput(1, [new Saisie { Type = 1, Touche = (ushort)touche }], Marshal.SizeOf<Saisie>());

    /// <summary>Relâche une touche enfoncée par <see cref="Appuyer"/>.</summary>
    public static void Relacher(uint touche) =>
        SendInput(1, [new Saisie { Type = 1, Touche = (ushort)touche, Drapeaux = 2 }], Marshal.SizeOf<Saisie>());

    /// <summary>Appuie sur les touches données dans l'ordre puis les relâche en sens inverse, en un seul SendInput.</summary>
    public static void Combinaison(params uint[] touches)
    {
        var saisies = new List<Saisie>();
        foreach (var t in touches)
        {
            saisies.Add(new Saisie { Type = 1, Touche = (ushort)t });
        }

        foreach (var t in touches.Reverse())
        {
            saisies.Add(new Saisie { Type = 1, Touche = (ushort)t, Drapeaux = 2 });
        }

        var envoyees = SendInput((uint)saisies.Count, saisies.ToArray(), Marshal.SizeOf<Saisie>());
        if (envoyees != saisies.Count)
        {
            Console.WriteLine($"   SendInput : {envoyees}/{saisies.Count} frappes injectées, erreur {Marshal.GetLastWin32Error()}");
        }
    }
}
```

Créer `$ESSAIS/Programme.cs` (boucle de messages sur le fil principal ; le scénario tourne sur un autre fil et passe par
`Sur(...)` pour tout ce qui doit s'exécuter sur le fil d'interface) :

```csharp
using System.Collections.Concurrent;
using PressePapiers.Coeur.Journalisation;

namespace EssaisRelais;

/// <summary>Journal d'essai : garde les lignes pour contrôler l'absence d'avertissement.</summary>
internal sealed class JournalEssai : IJournal
{
    public ConcurrentQueue<string> Lignes { get; } = new();

    private void Noter(string niveau, string message, Exception? e)
    {
        var ligne = $"[{niveau}] {message}{(e is null ? "" : " : " + e.Message)}";
        Lignes.Enqueue(ligne);
        Console.WriteLine("   journal " + ligne);
    }

    public void Info(string message) => Noter("info", message, null);

    public void Avertissement(string message, Exception? exception = null) => Noter("avert", message, exception);

    public void Erreur(string message, Exception? exception = null) => Noter("erreur", message, exception);

    public int Problemes => Lignes.Count(l => !l.StartsWith("[info]"));
}

/// <summary>Un signal compté, pour attendre qu'un événement du relais soit levé.</summary>
internal sealed class Signal
{
    private readonly SemaphoreSlim _s = new(0);
    public int Compte;
    public uint Dernier;

    public void Lever() { Interlocked.Increment(ref Compte); _s.Release(); }

    public void Lever(uint valeur) { Dernier = valeur; Lever(); }

    public bool Attendre(int ms) => _s.Wait(ms);

    /// <summary>Vide les signaux en attente avant une nouvelle action.</summary>
    public void Purger() { while (_s.Wait(0)) { } Compte = 0; }
}

internal static class Programme
{
    private static readonly ConcurrentQueue<Action> File = new();
    private static uint _filUi;

    /// <summary>Exécute l'action sur le fil de la boucle de messages et attend son résultat.</summary>
    public static T Sur<T>(Func<T> action)
    {
        T? resultat = default;
        Exception? erreur = null;
        using var fini = new ManualResetEventSlim();
        File.Enqueue(() =>
        {
            try { resultat = action(); } catch (Exception e) { erreur = e; }
            fini.Set();
        });
        Interop.PostThreadMessageW(_filUi, Interop.WmUser + 1, 0, 0);
        fini.Wait();
        if (erreur is not null) throw new InvalidOperationException("Erreur sur le fil UI", erreur);
        return resultat!;
    }

    public static void Sur(Action action) => Sur<int>(() => { action(); return 0; });

    [STAThread]
    private static int Main()
    {
        _filUi = Interop.GetCurrentThreadId();

        // Garde-fou : le programme ne reste jamais lancé.
        var garde = new Thread(() => { Thread.Sleep(240_000); Console.WriteLine("DELAI DEPASSE"); Environment.Exit(3); }) { IsBackground = true };
        garde.Start();

        var code = 0;
        var pilote = Task.Run(() =>
        {
            try { code = Essais.Executer(); }
            catch (Exception e) { Console.WriteLine("EXCEPTION DU PILOTE : " + e); code = 1; }
            Interop.PostThreadMessageW(_filUi, Interop.WmQuit, 0, 0);
        });

        while (Interop.GetMessageW(out var msg, 0, 0, 0) > 0)
        {
            if (msg.Hwnd == 0 && msg.Message == Interop.WmUser + 1)
            {
                while (File.TryDequeue(out var action)) action();
                continue;
            }

            Interop.TranslateMessage(msg);
            Interop.DispatchMessageW(msg);
        }

        pilote.Wait(5000);
        return code;
    }
}
```

Créer `$ESSAIS/Essais.cs` (scénario du relais ; il appelle aussi `EssaisDeLIcone()` et `Voir()` de la tâche 18) :

```csharp
using System.Diagnostics;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Systeme;
using static EssaisRelais.Programme;

namespace EssaisRelais;

/// <summary>Scénario d'essai sur le poste : le relais, puis l'icône.</summary>
internal static partial class Essais
{
    private static int _echecs;
    private static readonly JournalEssai Journal = new();

    public static void Verifier(string nom, bool ok, string detail = "")
    {
        Console.WriteLine($"{(ok ? "OK    " : "ECHEC ")} {nom}{(detail.Length > 0 ? " : " + detail : "")}");
        if (!ok) _echecs++;
    }

    private static bool _injection = true;
    private static int _nonTestes;

    /// <summary>
    /// Vérification qui dépend de frappes injectées par SendInput. Il arrive que le système avale les frappes injectées
    /// (fenêtre au premier plan de niveau d'intégrité supérieur, comme un logiciel de prise en main à distance lancé en administrateur, ou une fenêtre administrateur : UIPI) : l'essai le détecte et marque alors
    /// ces points « NON TESTE » au lieu de les compter en échec, à refaire quand l'injection fonctionne.
    /// </summary>
    public static void VerifierFrappe(string nom, bool ok, string detail = "")
    {
        if (_injection)
        {
            Verifier(nom, ok, detail);
            return;
        }

        Console.WriteLine($"NON TESTE {nom} : SendInput sans effet : fenêtre au premier plan de niveau d'intégrité supérieur ?");
        _nonTestes++;
    }

    /// <summary>Vérifie que SendInput agit : une touche sans effet (F24) enfoncée doit apparaître dans GetAsyncKeyState.</summary>
    private static bool InjectionPossible()
    {
        for (var essai = 0; essai < 15; essai++)
        {
            Interop.Appuyer(0x87);
            Thread.Sleep(60);
            var enfoncee = (Interop.GetAsyncKeyState(0x87) & 0x8000) != 0;
            Interop.Relacher(0x87);
            if (enfoncee)
            {
                return true;
            }

            Thread.Sleep(1000);
        }

        return false;
    }

    /// <summary>Lance PowerShell pour copier un texte, comme le ferait n'importe quelle application.</summary>
    public static void CopierTexte(string texte)
    {
        var p = Process.Start(new ProcessStartInfo("powershell.exe",
            ["-NoProfile", "-NonInteractive", "-Command", $"Set-Clipboard -Value '{texte}'"])
        { CreateNoWindow = true, UseShellExecute = false })!;
        p.WaitForExit(15_000);
    }

    public static int Executer()
    {
        try
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("voir")) { return Voir(int.TryParse(args[^1], out var duree) ? duree : 15); }
            EssaisDuRelais();
            EssaisDeLIcone();
        }
        finally
        {
            // Texte neutre pour ne pas laisser un contenu d'essai dans le presse-papiers.
            CopierTexte("ok");
        }

        var reserve = _nonTestes == 0 ? "" : $" ({_nonTestes} point(s) NON TESTE(S) : frappes injectées sans effet)";
        Console.WriteLine(_echecs == 0 ? "TOUS LES ESSAIS REUSSIS" + reserve : $"{_echecs} ECHEC(S)" + reserve);
        return _echecs == 0 ? 0 : 1;
    }

    private static void EssaisDuRelais()
    {
        Console.WriteLine("== Relais Windows ==");
        var relais = Sur(() => new RelaisWindows(Journal));
        var h = relais.Handle;
        Verifier("fenêtre créée", h != 0 && Interop.IsWindow(h));
        Verifier("fenêtre de premier niveau (ni enfant ni message-only)", Interop.GetParent(h) == 0);
        Verifier("fenêtre jamais affichée", !Interop.IsWindowVisible(h));

        var copie = new Signal();
        var secours = new Signal();
        var icone = new Signal();
        var barre = new Signal();
        var deverrou = new Signal();
        var veille = new Signal();
        relais.CopieDetectee += copie.Lever;
        relais.RaccourciSecoursPresse += secours.Lever;
        relais.MessageIconeRecu += icone.Lever;
        relais.BarreDesTachesRecreee += barre.Lever;
        relais.SessionDeverrouillee += deverrou.Lever;
        relais.SortieDeVeille += veille.Lever;

        // Copie
        Verifier("EcouterPressePapiers", Sur(relais.EcouterPressePapiers));
        Verifier("EcouterPressePapiers répété", Sur(relais.EcouterPressePapiers));
        CopierTexte("texte du relais");
        Verifier("CopieDetectee levé après Set-Clipboard", copie.Attendre(5000), $"{copie.Compte} signal(aux)");

        // Raccourci de secours
        _injection = InjectionPossible();
        Console.WriteLine(_injection ? "   injection de frappes : active" : "   injection de frappes : sans effet");
        var combinaison = new Raccourci(true, true, true, 0x56);
        var ok = Sur(() => (relais.EnregistrerRaccourci(combinaison, out var code), code));
        Verifier("EnregistrerRaccourci Ctrl+Alt+Maj+V", ok.Item1, $"code {ok.code}");
        secours.Purger();
        Interop.Combinaison(Interop.VkCtrl, Interop.VkAlt, Interop.VkMaj, 0x56);
        VerifierFrappe("RaccourciSecoursPresse levé par SendInput", secours.Attendre(3000), $"{secours.Compte} signal(aux)");

        // Un conflit ne doit pas faire perdre le raccourci en place.
        var autre = Sur(() => new RelaisWindows(Journal));
        var w = new Raccourci(true, true, true, 0x57);
        Verifier("second relais : Ctrl+Alt+Maj+W enregistré", Sur(() => autre.EnregistrerRaccourci(w, out _)));
        var conflit = Sur(() => (relais.EnregistrerRaccourci(w, out var code), code));
        Verifier("conflit : refus avec le code 1409", !conflit.Item1 && conflit.code == 1409, $"code {conflit.code}");
        secours.Purger();
        Interop.Combinaison(Interop.VkCtrl, Interop.VkAlt, Interop.VkMaj, 0x56);
        VerifierFrappe("après le conflit, l'ancien raccourci fonctionne encore", secours.Attendre(3000));
        Sur(autre.Dispose);
        Verifier("second relais libéré", !Interop.IsWindow(autre.Handle));

        // Un nouveau raccourci remplace l'ancien.
        var b = new Raccourci(true, true, true, 0x42);
        Verifier("remplacement par Ctrl+Alt+Maj+B", Sur(() => relais.EnregistrerRaccourci(b, out _)));
        secours.Purger();
        Interop.Combinaison(Interop.VkCtrl, Interop.VkAlt, Interop.VkMaj, 0x56);
        VerifierFrappe("l'ancien (V) ne lève plus rien", !secours.Attendre(700));
        Interop.Combinaison(Interop.VkCtrl, Interop.VkAlt, Interop.VkMaj, 0x42);
        VerifierFrappe("le nouveau (B) lève l'événement", secours.Attendre(3000));
        Sur(relais.RetirerRaccourci);
        secours.Purger();
        Interop.Combinaison(Interop.VkCtrl, Interop.VkAlt, Interop.VkMaj, 0x42);
        VerifierFrappe("après RetirerRaccourci, plus d'événement", !secours.Attendre(700));
        Verifier("ré-enregistrement de V pour la suite", Sur(() => relais.EnregistrerRaccourci(combinaison, out _)));

        // Messages système
        var tb = Interop.RegisterWindowMessageW("TaskbarCreated");
        Interop.PostMessageW(h, tb, 0, 0);
        Verifier("BarreDesTachesRecreee levé par TaskbarCreated", barre.Attendre(3000));

        Interop.SendMessageW(h, Interop.WmWtsSessionChange, 7, 1);
        Verifier("verrouillage (7) : pas d'événement de déverrouillage", deverrou.Compte == 0);
        Interop.SendMessageW(h, Interop.WmWtsSessionChange, 8, 1);
        Verifier("SessionDeverrouillee levé par WTS_SESSION_UNLOCK", deverrou.Compte == 1);
        Interop.SendMessageW(h, Interop.WmPowerBroadcast, 0x0004, 0);
        Verifier("suspension (PBT_APMSUSPEND) : pas d'événement de veille", veille.Compte == 0);
        Interop.SendMessageW(h, Interop.WmPowerBroadcast, 0x0012, 0);
        Verifier("SortieDeVeille levé par PBT_APMRESUMEAUTOMATIC", veille.Compte == 1);
        Interop.SendMessageW(h, 0x8001, 0, (1 << 16) | 0x400);
        Verifier("MessageIconeRecu : LOWORD(lParam)", icone.Compte == 1 && icone.Dernier == 0x400, $"0x{icone.Dernier:X}");

        // Une exception d'abonné est journalisée sans traverser la procédure native.
        var avant = Journal.Problemes;
        Action lance = () => throw new InvalidOperationException("abonné défaillant");
        relais.SortieDeVeille += lance;
        Interop.SendMessageW(h, Interop.WmPowerBroadcast, 0x0012, 0);
        relais.SortieDeVeille -= lance;
        Verifier("exception d'abonné journalisée, processus intact", Journal.Problemes == avant + 1);

        // Libération
        Sur(relais.Dispose);
        Sur(relais.Dispose);
        Verifier("Dispose : fenêtre détruite", !Interop.IsWindow(h));
        Verifier("Dispose : Handle remis à zéro", relais.Handle == 0);
        copie.Purger();
        CopierTexte("texte après libération");
        Verifier("Dispose : plus de copie détectée", !copie.Attendre(1500) && copie.Compte == 0);
        secours.Purger();
        Interop.Combinaison(Interop.VkCtrl, Interop.VkAlt, Interop.VkMaj, 0x56);
        VerifierFrappe("Dispose : raccourci libéré", !secours.Attendre(700));
        var temoin = Sur(() => new RelaisWindows(Journal));
        Verifier("Dispose : combinaison de nouveau disponible", Sur(() => temoin.EnregistrerRaccourci(combinaison, out _)));
        Sur(temoin.Dispose);
        Verifier("aucun avertissement ni erreur inattendu au journal", Journal.Problemes == avant + 1, $"{Journal.Problemes} (attendu {avant + 1} : le conflit 1409 et l'abonné défaillant)");
    }
}
```

Créer `$ESSAIS/EssaisIcone.cs` (simple bouchon jusqu'à la tâche 18, qui le remplace) :

```csharp
namespace EssaisRelais;

// Réservé à la tâche 18 : l'icône de notification n'existe pas encore, il n'y a rien à exercer ni à regarder.
internal static partial class Essais
{
    private static void EssaisDeLIcone()
    {
    }

    private static int Voir(int secondes)
    {
        Console.WriteLine("Le mode voir sera disponible avec la tâche 18.");
        return 0;
    }
}
```

- [ ] **Étape 2 : Lancer la vérification contre la coquille et constater l'échec**

```bash
DEPOT="$(git rev-parse --show-toplevel)"; ESSAIS="$LOCALAPPDATA/Temp/essais-relais"
bash "$ESSAIS/synchro.sh" "$DEPOT" && cd "$ESSAIS" && dotnet build -c Debug && dotnet run -c Debug --no-build
```

Résultat attendu : le programme se compile, puis échoue dès la création du relais (code de sortie 1) :

```
== Relais Windows ==
EXCEPTION DU PILOTE : System.InvalidOperationException: Erreur sur le fil UI
 ---> System.NotImplementedException: Tâche 17
   at PressePapiers.Systeme.RelaisWindows..ctor(IJournal journal)
```

- [ ] **Étape 3 : Implémenter**

`src/PressePapiers/Natif/NatifRelais.cs` (nouveau fichier) :

```csharp
using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>
/// Déclarations Win32 de la fenêtre relais : classe de fenêtre, écoute du presse-papiers, raccourci global, messages
/// enregistrés et notifications de session. Une même fonction peut être déclarée ailleurs : chaque brique reste autonome.
/// </summary>
internal static partial class NatifRelais
{
    // Messages traités par la procédure du relais.
    internal const uint WmHotkey = 0x0312;
    internal const uint WmClipboardUpdate = 0x031D;
    internal const uint WmPowerBroadcast = 0x0218;
    internal const uint WmWtsSessionChange = 0x02B1;

    // Paramètres de ces messages.
    internal const int PbtApmResumeAutomatic = 0x0012;
    internal const int WtsSessionUnlock = 0x0008;

    // WS_POPUP : fenêtre de premier niveau, sans bordure ; jamais affichée. WS_EX_TOOLWINDOW et WS_EX_NOACTIVATE
    // l'écartent de la barre des tâches et d'Alt+Tab au cas où elle serait un jour montrée.
    internal const uint WsPopup = 0x80000000;
    internal const uint WsExToolWindow = 0x00000080;
    internal const uint WsExNoActivate = 0x08000000;

    // MSGFLT_ALLOW : laisse passer le message même si le processus tourne à un niveau d'intégrité plus élevé.
    internal const uint MsgfltAllow = 1;

    // NOTIFY_FOR_THIS_SESSION : seules les notifications de la session de l'utilisateur.
    internal const uint NotifyForThisSession = 0;

    /// <summary>Procédure de fenêtre (WNDPROC).</summary>
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint ProcedureFenetre(nint hwnd, uint message, nint wParam, nint lParam);

    /// <summary>WNDCLASSEXW ; les pointeurs de texte sont alloués par l'appelant.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ClasseFenetre
    {
        public uint Taille;
        public uint Style;
        public nint Procedure;
        public int OctetsSupplementairesClasse;
        public int OctetsSupplementairesFenetre;
        public nint Instance;
        public nint Icone;
        public nint Curseur;
        public nint Fond;
        public nint NomMenu;
        public nint NomClasse;
        public nint IconePetite;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW")]
    internal static partial nint ObtenirModule(nint nomModule);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    internal static partial ushort EnregistrerClasse(in ClasseFenetre classe);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterClassW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RetirerClasse(string nomClasse, nint instance);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreerFenetre(uint styleEtendu, string nomClasse, string? titre, uint style,
        int x, int y, int largeur, int hauteur, nint parent, nint menu, nint instance, nint parametre);

    [LibraryImport("user32.dll", EntryPoint = "DestroyWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DetruireFenetre(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    internal static partial nint ProcedureParDefaut(nint hwnd, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "AddClipboardFormatListener", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AjouterEcouteurPressePapiers(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "RemoveClipboardFormatListener", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RetirerEcouteurPressePapiers(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "RegisterHotKey", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnregistrerRaccourciGlobal(nint hwnd, int identifiant, uint modificateurs, uint touche);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterHotKey", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RetirerRaccourciGlobal(nint hwnd, int identifiant);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint EnregistrerMessage(string nom);

    [LibraryImport("user32.dll", EntryPoint = "ChangeWindowMessageFilterEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AutoriserMessage(nint hwnd, uint message, uint action, nint structureResultat);

    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSRegisterSessionNotification", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool InscrireNotificationsSession(nint hwnd, uint portee);

    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSUnRegisterSessionNotification", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DesinscrireNotificationsSession(nint hwnd);
}
```

`src/PressePapiers/Systeme/RelaisWindows.cs` (contenu complet, il remplace la coquille ; le `#pragma warning disable CS0067`
de la coquille est retiré, les événements étant maintenant levés) :

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Natif;

namespace PressePapiers.Systeme;

/// <summary>
/// Fenêtre Win32 de premier niveau invisible, sur le fil d'interface : reçoit les copies, le raccourci de secours,
/// les messages de l'icône et les changements de session. Ce n'est pas une fenêtre « message-only » : celles-ci ne
/// reçoivent pas les messages diffusés, donc pas « TaskbarCreated ». Elle doit être créée et libérée sur le fil
/// d'interface, qui pompe ses messages.
/// </summary>
public sealed class RelaisWindows : IDisposable
{
    /// <summary>Message de rappel de l'icône de notification (WM_APP + 1).</summary>
    public const uint MessageIcone = 0x8001;

    // Identifiant du raccourci de secours auprès de RegisterHotKey.
    private const int IdRaccourci = 1;

    private readonly IJournal _journal;

    // La procédure est gardée dans un champ : le pointeur donné à Windows ne retient pas le délégué, et le ramasse-miettes
    // le libérerait pendant que la fenêtre vit encore.
    private readonly NatifRelais.ProcedureFenetre _procedure;
    private readonly string _nomClasse;
    private readonly nint _instance;
    private readonly uint _messageBarreDesTaches;

    private nint _hwnd;
    private bool _libere;
    private bool _ecoutePressePapiers;
    private bool _sessionInscrite;
    private Raccourci? _raccourci;

    /// <summary>Crée la fenêtre sur le fil courant, qui doit être le fil d'interface.</summary>
    /// <param name="journal">Journal des échecs de configuration.</param>
    /// <exception cref="Win32Exception">La classe ou la fenêtre n'a pas pu être créée.</exception>
    public RelaisWindows(IJournal journal)
    {
        _journal = journal;
        _procedure = Traiter;
        _instance = NatifRelais.ObtenirModule(0);

        // Un nom de classe propre à l'instance : deux relais dans un même processus ne se gênent pas.
        _nomClasse = $"PressePapiers.Relais.{Guid.NewGuid():N}";

        var nomNatif = Marshal.StringToHGlobalUni(_nomClasse);
        try
        {
            var classe = new NatifRelais.ClasseFenetre
            {
                Taille = (uint)Unsafe.SizeOf<NatifRelais.ClasseFenetre>(),
                Procedure = Marshal.GetFunctionPointerForDelegate(_procedure),
                Instance = _instance,
                NomClasse = nomNatif,
            };
            if (NatifRelais.EnregistrerClasse(classe) == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Enregistrement de la classe du relais impossible.");
            }
        }
        finally
        {
            // Windows garde sa propre copie du nom.
            Marshal.FreeHGlobal(nomNatif);
        }

        var hwnd = NatifRelais.CreerFenetre(
            NatifRelais.WsExToolWindow | NatifRelais.WsExNoActivate, _nomClasse, "PressePapiers relais",
            NatifRelais.WsPopup, 0, 0, 0, 0, 0, 0, _instance, 0);
        if (hwnd == 0)
        {
            var code = Marshal.GetLastPInvokeError();
            NatifRelais.RetirerClasse(_nomClasse, _instance);
            throw new Win32Exception(code, "Création de la fenêtre du relais impossible.");
        }

        _hwnd = hwnd;

        // Les échecs suivants ne sont pas fatals : l'outil reste utilisable, sans la fonction concernée.
        _messageBarreDesTaches = NatifRelais.EnregistrerMessage("TaskbarCreated");
        if (_messageBarreDesTaches == 0)
        {
            _journal.Avertissement($"Message TaskbarCreated non enregistré (code {Marshal.GetLastPInvokeError()}).");
        }
        else if (!NatifRelais.AutoriserMessage(_hwnd, _messageBarreDesTaches, NatifRelais.MsgfltAllow, 0))
        {
            _journal.Avertissement($"TaskbarCreated non autorisé par le filtre de messages (code {Marshal.GetLastPInvokeError()}).");
        }

        _sessionInscrite = NatifRelais.InscrireNotificationsSession(_hwnd, NatifRelais.NotifyForThisSession);
        if (!_sessionInscrite)
        {
            _journal.Avertissement($"Notifications de session non inscrites (code {Marshal.GetLastPInvokeError()}).");
        }
    }

    /// <summary>Handle de la fenêtre relais ; zéro une fois le relais libéré.</summary>
    public nint Handle => _hwnd;

    /// <summary>WM_CLIPBOARDUPDATE.</summary>
    public event Action? CopieDetectee;

    /// <summary>WM_HOTKEY, identifiant 1.</summary>
    public event Action? RaccourciSecoursPresse;

    /// <summary>Message de l'icône ; l'argument est le LOWORD de lParam.</summary>
    public event Action<uint>? MessageIconeRecu;

    /// <summary>Message enregistré « TaskbarCreated » : l'Explorateur a redémarré.</summary>
    public event Action? BarreDesTachesRecreee;

    /// <summary>WM_WTSSESSION_CHANGE avec WTS_SESSION_UNLOCK.</summary>
    public event Action? SessionDeverrouillee;

    /// <summary>WM_POWERBROADCAST avec PBT_APMRESUMEAUTOMATIC.</summary>
    public event Action? SortieDeVeille;

    /// <summary>Commence à écouter le presse-papiers (AddClipboardFormatListener) ; faux en cas d'échec.</summary>
    /// <returns>Vrai si l'écoute est en place (y compris si elle l'était déjà).</returns>
    public bool EcouterPressePapiers()
    {
        ControlerVivant();
        if (_ecoutePressePapiers)
        {
            return true;
        }

        _ecoutePressePapiers = NatifRelais.AjouterEcouteurPressePapiers(_hwnd);
        if (!_ecoutePressePapiers)
        {
            _journal.Erreur($"Écoute du presse-papiers impossible (code {Marshal.GetLastPInvokeError()}).");
        }

        return _ecoutePressePapiers;
    }

    /// <summary>
    /// Enregistre le raccourci de secours en remplaçant le précédent. Si le nouveau est refusé (déjà pris par une autre
    /// application, par exemple), le précédent est rétabli : une saisie malheureuse ne laisse pas l'outil sans secours.
    /// </summary>
    /// <param name="raccourci">Combinaison à enregistrer.</param>
    /// <param name="codeErreur">Code d'erreur Windows en cas d'échec (1409 : combinaison déjà prise), sinon zéro.</param>
    /// <returns>Vrai si le raccourci est enregistré.</returns>
    public bool EnregistrerRaccourci(Raccourci raccourci, out int codeErreur)
    {
        ControlerVivant();

        var precedent = _raccourci;
        if (precedent is not null)
        {
            NatifRelais.RetirerRaccourciGlobal(_hwnd, IdRaccourci);
            _raccourci = null;
        }

        if (NatifRelais.EnregistrerRaccourciGlobal(_hwnd, IdRaccourci, raccourci.Modificateurs, raccourci.ToucheVirtuelle))
        {
            _raccourci = raccourci;
            codeErreur = 0;
            return true;
        }

        codeErreur = Marshal.GetLastPInvokeError();
        _journal.Avertissement($"Raccourci de secours {raccourci} refusé (code {codeErreur}).");

        if (precedent is { } ancien
            && NatifRelais.EnregistrerRaccourciGlobal(_hwnd, IdRaccourci, ancien.Modificateurs, ancien.ToucheVirtuelle))
        {
            _raccourci = ancien;
        }

        return false;
    }

    /// <summary>Retire le raccourci de secours ; sans effet s'il n'est pas enregistré.</summary>
    public void RetirerRaccourci()
    {
        if (_libere || _raccourci is null)
        {
            return;
        }

        NatifRelais.RetirerRaccourciGlobal(_hwnd, IdRaccourci);
        _raccourci = null;
    }

    /// <summary>Retire le raccourci, l'écoute et les inscriptions, puis détruit la fenêtre. Sans effet au second appel.</summary>
    public void Dispose()
    {
        if (_libere)
        {
            return;
        }

        RetirerRaccourci();
        if (_ecoutePressePapiers)
        {
            NatifRelais.RetirerEcouteurPressePapiers(_hwnd);
            _ecoutePressePapiers = false;
        }

        if (_sessionInscrite)
        {
            NatifRelais.DesinscrireNotificationsSession(_hwnd);
            _sessionInscrite = false;
        }

        // Le drapeau passe avant la destruction : les messages de fin de vie ne lèvent plus d'événement.
        _libere = true;
        var hwnd = _hwnd;
        _hwnd = 0;

        // DestroyWindow n'agit que sur le fil créateur ; ailleurs, la fenêtre reste et l'échec est noté.
        if (!NatifRelais.DetruireFenetre(hwnd))
        {
            _journal.Avertissement($"Destruction du relais impossible (code {Marshal.GetLastPInvokeError()}), appelée depuis le mauvais fil ?");
        }

        NatifRelais.RetirerClasse(_nomClasse, _instance);
    }

    private void ControlerVivant() => ObjectDisposedException.ThrowIf(_libere, this);

    // Procédure de fenêtre : une exception qui la traverserait remonterait dans du code natif et tuerait le processus.
    private nint Traiter(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (_libere || _hwnd == 0)
        {
            return NatifRelais.ProcedureParDefaut(hwnd, message, wParam, lParam);
        }

        try
        {
            if (_messageBarreDesTaches != 0 && message == _messageBarreDesTaches)
            {
                BarreDesTachesRecreee?.Invoke();
                return 0;
            }

            switch (message)
            {
                case NatifRelais.WmClipboardUpdate:
                    CopieDetectee?.Invoke();
                    return 0;

                case NatifRelais.WmHotkey when wParam == IdRaccourci:
                    RaccourciSecoursPresse?.Invoke();
                    return 0;

                case MessageIcone:
                    MessageIconeRecu?.Invoke((uint)(lParam & 0xFFFF));
                    return 0;

                case NatifRelais.WmWtsSessionChange when wParam == NatifRelais.WtsSessionUnlock:
                    SessionDeverrouillee?.Invoke();
                    return 0;

                case NatifRelais.WmPowerBroadcast when wParam == NatifRelais.PbtApmResumeAutomatic:
                    SortieDeVeille?.Invoke();
                    return 1; // TRUE : message pris en compte
            }
        }
        catch (Exception exception)
        {
            _journal.Erreur($"Exception dans un abonné du relais (message 0x{message:X4}).", exception);
        }

        return NatifRelais.ProcedureParDefaut(hwnd, message, wParam, lParam);
    }
}
```

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : la compilation se termine par `0 Avertissement(s)` et `0 Erreur(s)`. Tous les tests déjà présents
restent verts ; cette tâche n'en ajoute ni n'en retire (les 513 des tâches 1 à 16) :

```
Résumé de série de tests : Réussite!
  total : 513
  échec : 0
  réussie : 513
  ignoré : 0
```

- [ ] **Étape 5 : Vérifier sur le poste**

```bash
DEPOT="$(git rev-parse --show-toplevel)"; ESSAIS="$LOCALAPPDATA/Temp/essais-relais"
bash "$ESSAIS/synchro.sh" "$DEPOT" && cd "$ESSAIS" && dotnet build -c Debug && dotnet run -c Debug --no-build
```

Ce que le programme fait réellement : crée le relais ; copie un texte par `powershell Set-Clipboard` ; enregistre
Ctrl+Alt+Maj+V puis le frappe par `SendInput` ; enregistre le même raccourci depuis un second relais (conflit, code 1409) ;
remplace le raccourci par Ctrl+Alt+Maj+B ; poste `TaskbarCreated`, `WM_WTSSESSION_CHANGE` (verrouillage 7 puis déverrouillage 8),
`WM_POWERBROADCAST` (suspension 0x4 puis reprise 0x12) et un message d'icône ; fait lever une exception à un abonné ;
libère le relais et vérifie que fenêtre, écoute et raccourci ont disparu. À la fin, un texte neutre (`ok`) est remis dans
le presse-papiers. Résultat attendu (le nombre de signaux après `Set-Clipboard` varie, 2 ou 3 ; les lignes `journal` sont
voulues : le conflit 1409 et l'abonné défaillant sont des cas d'essai) :

```
== Relais Windows ==
OK     fenêtre créée
OK     fenêtre de premier niveau (ni enfant ni message-only)
OK     fenêtre jamais affichée
OK     EcouterPressePapiers
OK     EcouterPressePapiers répété
OK     CopieDetectee levé après Set-Clipboard : 3 signal(aux)
   injection de frappes : active
OK     EnregistrerRaccourci Ctrl+Alt+Maj+V : code 0
OK     RaccourciSecoursPresse levé par SendInput : 1 signal(aux)
OK     second relais : Ctrl+Alt+Maj+W enregistré
   journal [avert] Raccourci de secours Ctrl+Alt+Maj+W refusé (code 1409).
OK     conflit : refus avec le code 1409 : code 1409
OK     après le conflit, l'ancien raccourci fonctionne encore
OK     second relais libéré
OK     remplacement par Ctrl+Alt+Maj+B
OK     l'ancien (V) ne lève plus rien
OK     le nouveau (B) lève l'événement
OK     après RetirerRaccourci, plus d'événement
OK     ré-enregistrement de V pour la suite
OK     BarreDesTachesRecreee levé par TaskbarCreated
OK     verrouillage (7) : pas d'événement de déverrouillage
OK     SessionDeverrouillee levé par WTS_SESSION_UNLOCK
OK     suspension (PBT_APMSUSPEND) : pas d'événement de veille
OK     SortieDeVeille levé par PBT_APMRESUMEAUTOMATIC
OK     MessageIconeRecu : LOWORD(lParam) : 0x400
   journal [erreur] Exception dans un abonné du relais (message 0x0218). : abonné défaillant
OK     exception d'abonné journalisée, processus intact
OK     Dispose : fenêtre détruite
OK     Dispose : Handle remis à zéro
OK     Dispose : plus de copie détectée
OK     Dispose : raccourci libéré
OK     Dispose : combinaison de nouveau disponible
OK     aucun avertissement ni erreur inattendu au journal : 2 (attendu 2 : le conflit 1409 et l'abonné défaillant)
TOUS LES ESSAIS REUSSIS
```

Si le raccourci ne se déclenche pas, c'est presque toujours que la fenêtre au premier plan a un niveau d'intégrité supérieur
à celui du programme (par exemple un logiciel de prise en main à distance lancé en administrateur, ou une fenêtre administrateur) : Windows avale alors silencieusement les frappes
injectées (UIPI, voir spec section 10.3). Le programme le détecte avant d'envoyer les touches (il enfonce F24 et lit
`GetAsyncKeyState`, jusqu'à quinze secondes) et affiche alors `NON TESTE ... SendInput sans effet` au lieu d'un échec ;
refaire l'essai en mettant au premier plan une fenêtre ordinaire (l'explorateur de fichiers, un navigateur). Les messages
postés directement ne dépendent pas de cela.

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers/Natif/NatifRelais.cs src/PressePapiers/Systeme/RelaisWindows.cs
git commit -m "Implémenter RelaisWindows : fenêtre de premier niveau, copies, raccourci de secours, TaskbarCreated et session"
```
