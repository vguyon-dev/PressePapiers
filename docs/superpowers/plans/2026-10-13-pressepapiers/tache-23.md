### Tâche 23 : ServiceCapture

**Fichiers :**
- Créer : `src/PressePapiers.Coeur/Capture/ExecutionUnique.cs` (« une seule exécution à la fois, les demandes en rafale se fondent en une relance » ; dans Coeur pour pouvoir se tester sans Windows)
- Modifier : `src/PressePapiers/Capture/ServiceCapture.cs` (remplace la coquille ; contenu complet à l'étape 3)
- Tests : `tests/PressePapiers.Tests/Capture/ExecutionUniqueTests.cs`
- Hors dépôt (jetable, ne se commite pas) : `essais-pp/verif-capture/verif-capture.csproj` et `Program.cs` (réutilise `essais-pp/verif/Brut.cs`
  de la tâche 21)

**Interfaces :**
- Utilise :
  - tâche 1 : `Parametres` (`PlafondTemporaire`), `Element`, `ContenuCopie`, `IJournal` ;
  - tâche 4 à 6 : `DepotHistorique.AjouterCopie(ContenuCopie)` et `DepotHistorique.AppliquerPlafond(int)` (coquilles tant que ces
    tâches ne sont pas faites : la tâche se code contre le contrat et se vérifie avec un double en mémoire) ;
  - tâche 17 : `RelaisWindows.CopieDetectee` (événement `Action`, levé sur le fil d'interface) ;
  - tâche 21 : `LecteurPressePapiers.LireAsync(string?)`, `ApplicationOrigine.DeterminerPourCopie()`.
- Fournit :
  - `public sealed class ExecutionUnique` (namespace `PressePapiers.Coeur.Capture`) : `ExecutionUnique(Func<Task> travail, Action<Exception> surErreur)`,
    `void Demander()` (démarre le travail sur le fil appelant jusqu'à son premier `await` ; pendant qu'un travail tourne, toute
    demande se fond dans **une seule** relance lancée à sa fin ; sûr entre fils, ne bloque jamais), `Task AttendreRepos()`
    (terminée quand plus rien ne tourne ni n'attend) ; une exception du travail, même levée avant qu'il rende sa tâche, est confiée
    à `surErreur` sans interrompre l'exécuteur, et une exception de `surErreur` est avalée ;
  - `public sealed class ServiceCapture : IDisposable` : `ServiceCapture(RelaisWindows relais, LecteurPressePapiers lecteur,
    DepotHistorique depot, Func<Parametres> parametres, IJournal journal)`, `event Action<Element>? ElementAjoute`, `Dispose()`.
  - Consommateurs : tâche 25 et suivantes (la fenêtre rafraîchit sa liste sur `ElementAjoute` ou sur `DepotHistorique.Modifie`), tâche 29.

Comportement de `ServiceCapture` (spec, sections 3 et 4) :
- À chaque `CopieDetectee`, **aussitôt** (avant que l'utilisateur ne change de fenêtre), l'application d'origine est relevée par
  `ApplicationOrigine.DeterminerPourCopie()` (fenêtre au premier plan, puis `GetClipboardOwner` seulement en repli) et mémorisée ; puis
  une capture est demandée à l'`ExecutionUnique`.
- Une capture : lecture (`LireAsync` avec l'origine la plus récente), puis `DepotHistorique.AjouterCopie`, puis
  `AppliquerPlafond(parametres().PlafondTemporaire)` (réglage relu à chaque copie ; un échec du plafond est un avertissement au
  journal qui ne fait pas perdre la copie), puis `ElementAjoute`. Une lecture qui rend `null` (marque interne, illisible) n'écrit rien.
- **Une capture à la fois** : une rafale de notifications pendant une capture ne relance qu'**une** lecture de plus, qui voit le
  dernier contenu (point de vigilance 1, avec le dédoublonnage de la tâche 5 pour le contenu identique recopié).
- `ElementAjoute` est levé sur le fil qui a reçu la notification (le fil d'interface : le contexte de synchronisation de `Program` y
  ramène la suite du `await`). Une erreur du dépôt est journalisée (`Erreur`) et le service continue.
- `Dispose` se désabonne du relais ; une capture déjà commencée peut finir de lire mais n'écrit plus dans l'historique.
- Écart au contrat : le type public `ExecutionUnique` s'ajoute (nouveau fichier, aucune signature existante changée).

- [ ] **Étape 1 : Écrire le test qui échoue**

`tests/PressePapiers.Tests/Capture/ExecutionUniqueTests.cs`

```csharp
using PressePapiers.Coeur.Capture;

namespace PressePapiers.Tests.Capture;

public sealed class ExecutionUniqueTests
{
    private static readonly TimeSpan Delai = TimeSpan.FromSeconds(10);

    // Un travail dont on maîtrise le début et la fin : il se bloque jusqu'à ce que le test le libère.
    private sealed class TravailPilote
    {
        public int Debuts;
        public int Fins;
        public int EnCours;
        public int MaxSimultanes;
        public readonly List<string> Journal = [];
        private readonly Queue<TaskCompletionSource> _portes = new();
        public TaskCompletionSource Commencee = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ExecuterAsync()
        {
            TaskCompletionSource? porte;
            lock (this)
            {
                Debuts++;
                var numero = Debuts;
                Journal.Add($"debut{numero}");
                EnCours++;
                MaxSimultanes = Math.Max(MaxSimultanes, EnCours);
                _portes.TryDequeue(out porte);
                Commencee.TrySetResult();
            }

            return Terminer(porte);
        }

        private async Task Terminer(TaskCompletionSource? porte)
        {
            if (porte is not null)
            {
                await porte.Task;
            }

            lock (this)
            {
                Fins++;
                Journal.Add($"fin{Fins}");
                EnCours--;
            }
        }

        // La prochaine exécution restera bloquée jusqu'à l'appel de Liberer sur la porte rendue.
        public TaskCompletionSource Bloquer()
        {
            var porte = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (this)
            {
                _portes.Enqueue(porte);
            }

            return porte;
        }
    }

    private static ExecutionUnique Creer(Func<Task> travail, Action<Exception>? surErreur = null) =>
        new(travail, surErreur ?? (_ => { }));

    [Fact]
    public async Task Une_demande_lance_le_travail_une_fois()
    {
        var travail = new TravailPilote();
        var execution = Creer(travail.ExecuterAsync);

        execution.Demander();
        await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);

        Assert.Equal(1, travail.Debuts);
        Assert.Equal(1, travail.Fins);
    }

    [Fact]
    public async Task Une_rafale_pendant_un_travail_ne_relance_qu_une_seule_fois()
    {
        var travail = new TravailPilote();
        var porte = travail.Bloquer();
        var execution = Creer(travail.ExecuterAsync);

        execution.Demander();
        await travail.Commencee.Task.WaitAsync(Delai, TestContext.Current.CancellationToken);
        for (var i = 0; i < 50; i++)
        {
            execution.Demander();
        }

        porte.SetResult();
        await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);

        // Le travail en cours, plus une seule relance pour les 50 demandes : ni 51 (une par demande), ni 1 (relance perdue).
        Assert.Equal(2, travail.Debuts);
        Assert.Equal(2, travail.Fins);
    }

    [Fact]
    public async Task La_relance_commence_apres_la_fin_du_travail_en_cours()
    {
        var travail = new TravailPilote();
        var porte = travail.Bloquer();
        var execution = Creer(travail.ExecuterAsync);

        execution.Demander();
        await travail.Commencee.Task.WaitAsync(Delai, TestContext.Current.CancellationToken);
        execution.Demander();
        porte.SetResult();
        await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);

        // Deux lectures du presse-papiers ne se chevauchent jamais : l'ordre est strict.
        Assert.Equal(["debut1", "fin1", "debut2", "fin2"], travail.Journal);
        Assert.Equal(1, travail.MaxSimultanes);
    }

    [Fact]
    public async Task Une_demande_apres_la_fin_relance_normalement()
    {
        var travail = new TravailPilote();
        var execution = Creer(travail.ExecuterAsync);

        for (var i = 0; i < 3; i++)
        {
            execution.Demander();
            await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);
        }

        Assert.Equal(3, travail.Debuts);
    }

    [Fact]
    public async Task Une_exception_est_signalee_sans_bloquer_les_demandes_suivantes()
    {
        var erreurs = new List<Exception>();
        var appels = 0;
        var execution = Creer(() =>
        {
            appels++;
            // Premier appel : exception levée avant même de rendre une tâche ; deuxième : tâche en échec.
            if (appels == 1)
            {
                throw new InvalidOperationException("synchrone");
            }

            return appels == 2 ? Task.FromException(new InvalidOperationException("asynchrone")) : Task.CompletedTask;
        }, erreurs.Add);

        for (var i = 0; i < 3; i++)
        {
            execution.Demander();
            await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);
        }

        Assert.Equal(3, appels);
        Assert.Equal(["synchrone", "asynchrone"], erreurs.Select(e => e.Message));
    }

    [Fact]
    public async Task Un_signalement_d_erreur_qui_leve_ne_bloque_pas_l_executeur()
    {
        var appels = 0;
        var execution = Creer(() =>
        {
            appels++;
            throw new InvalidOperationException("boum");
        }, _ => throw new InvalidOperationException("le signalement lui-même échoue"));

        execution.Demander();
        await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);
        execution.Demander();
        await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);

        Assert.Equal(2, appels);
    }

    [Fact]
    public async Task Des_demandes_venues_de_plusieurs_fils_ne_se_chevauchent_jamais()
    {
        var travail = new TravailPilote();
        var execution = Creer(async () =>
        {
            await travail.ExecuterAsync();
            await Task.Delay(1);
        });

        var fils = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                execution.Demander();
            }
        })).ToArray();
        await Task.WhenAll(fils).WaitAsync(Delai, TestContext.Current.CancellationToken);
        await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);

        Assert.Equal(1, travail.MaxSimultanes);
        Assert.InRange(travail.Debuts, 1, 1600);
        Assert.Equal(travail.Debuts, travail.Fins);

        // Après le repos, une demande de plus donne exactement une exécution de plus.
        var avant = travail.Debuts;
        execution.Demander();
        await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);
        Assert.Equal(avant + 1, travail.Debuts);
    }

    [Fact]
    public void Les_arguments_nuls_sont_refuses()
    {
        Assert.Throws<ArgumentNullException>(() => new ExecutionUnique(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new ExecutionUnique(() => Task.CompletedTask, null!));
    }
}
```

Ce que ces tests prouvent : `Une_rafale_pendant_un_travail_ne_relance_qu_une_seule_fois` échoue si chaque demande relance (51
exécutions) ou si la relance est perdue (1) ; `La_relance_commence_apres_la_fin_du_travail_en_cours` échoue si deux lectures se
chevauchent ou si l'ordre est inversé ; `Des_demandes_venues_de_plusieurs_fils_ne_se_chevauchent_jamais` échoue au moindre
chevauchement sous 1 600 demandes concurrentes ; les deux tests d'exception échouent si une erreur laisse l'exécuteur « en cours » pour toujours.

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : la compilation des tests échoue, `ExecutionUnique` et son espace de noms n'existant pas encore :

```
error CS0234: Le nom de type ou d'espace de noms 'Capture' n'existe pas dans l'espace de noms 'PressePapiers.Coeur'
error CS0246: Le nom de type ou d'espace de noms 'ExecutionUnique' est introuvable
```

- [ ] **Étape 3 : Implémenter**

`src/PressePapiers.Coeur/Capture/ExecutionUnique.cs`

```csharp
namespace PressePapiers.Coeur.Capture;

/// <summary>
/// Exécute un travail asynchrone une seule fois à la fois. Les demandes reçues pendant qu'il s'exécute ne s'empilent pas :
/// elles se fondent en une seule relance, lancée quand le travail en cours est fini. Sert à la capture du presse-papiers :
/// une rafale de notifications pendant une lecture ne provoque qu'une lecture de plus, qui verra le dernier contenu.
/// </summary>
public sealed class ExecutionUnique
{
    private readonly Func<Task> _travail;
    private readonly Action<Exception> _surErreur;
    private readonly object _verrou = new();
    private bool _enCours;
    private bool _relanceDemandee;
    private TaskCompletionSource? _repos;

    /// <summary>Crée l'exécuteur.</summary>
    /// <param name="travail">Travail à exécuter ; il peut lever une exception, y compris avant de rendre sa tâche.</param>
    /// <param name="surErreur">Appelé avec l'exception d'un travail en échec ; l'exécuteur continue ensuite normalement.</param>
    public ExecutionUnique(Func<Task> travail, Action<Exception> surErreur)
    {
        ArgumentNullException.ThrowIfNull(travail);
        ArgumentNullException.ThrowIfNull(surErreur);
        _travail = travail;
        _surErreur = surErreur;
    }

    /// <summary>
    /// Demande une exécution. Sans travail en cours, il démarre aussitôt sur le fil appelant jusqu'à son premier point
    /// d'attente ; sinon la demande est mémorisée et une seule relance suivra. Sûr entre fils, ne bloque jamais.
    /// </summary>
    public void Demander()
    {
        lock (_verrou)
        {
            if (_enCours)
            {
                _relanceDemandee = true;
                return;
            }

            _enCours = true;
            _repos = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        _ = ExecuterAsync();
    }

    /// <summary>Tâche terminée quand plus aucun travail n'est en cours ni en attente de relance (pour les tests et l'arrêt).</summary>
    public Task AttendreRepos()
    {
        lock (_verrou)
        {
            return _enCours ? _repos!.Task : Task.CompletedTask;
        }
    }

    // Ne lève jamais : toute exception du travail est confiée à _surErreur, sinon l'état « en cours » resterait vrai à jamais.
    private async Task ExecuterAsync()
    {
        while (true)
        {
            try
            {
                await _travail();
            }
            catch (Exception exception)
            {
                Signaler(exception);
            }

            TaskCompletionSource repos;
            lock (_verrou)
            {
                if (_relanceDemandee)
                {
                    _relanceDemandee = false;
                    continue;
                }

                _enCours = false;
                repos = _repos!;
            }

            repos.SetResult();
            return;
        }
    }

    private void Signaler(Exception exception)
    {
        try
        {
            _surErreur(exception);
        }
        catch
        {
            // Le signalement ne doit jamais bloquer l'exécuteur.
        }
    }
}
```

`src/PressePapiers/Capture/ServiceCapture.cs`

```csharp
using PressePapiers.Coeur.Capture;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Systeme;

namespace PressePapiers.Capture;

/// <summary>
/// Enregistre chaque copie détectée dans l'historique : relève l'application d'origine, lit le presse-papiers puis ajoute
/// l'élément à Temporaire. Une seule capture à la fois ; une rafale de notifications pendant une capture ne provoque qu'une
/// lecture de plus.
/// </summary>
public sealed class ServiceCapture : IDisposable
{
    private readonly RelaisWindows _relais;
    private readonly LecteurPressePapiers _lecteur;
    private readonly DepotHistorique _depot;
    private readonly Func<Parametres> _parametres;
    private readonly IJournal _journal;
    private readonly ExecutionUnique _execution;
    private volatile string? _application;
    private volatile bool _libere;

    /// <summary>Branche la capture sur le relais. À créer sur le fil d'interface, où le relais lève ses événements.</summary>
    /// <param name="relais">Relais qui signale chaque copie.</param>
    /// <param name="lecteur">Lecteur du presse-papiers.</param>
    /// <param name="depot">Historique où ranger les copies.</param>
    /// <param name="parametres">Fournit les réglages courants (plafond de Temporaire), relus à chaque copie.</param>
    /// <param name="journal">Journal des captures en échec.</param>
    public ServiceCapture(RelaisWindows relais, LecteurPressePapiers lecteur, DepotHistorique depot,
                          Func<Parametres> parametres, IJournal journal)
    {
        ArgumentNullException.ThrowIfNull(relais);
        ArgumentNullException.ThrowIfNull(lecteur);
        ArgumentNullException.ThrowIfNull(depot);
        ArgumentNullException.ThrowIfNull(parametres);
        ArgumentNullException.ThrowIfNull(journal);
        _relais = relais;
        _lecteur = lecteur;
        _depot = depot;
        _parametres = parametres;
        _journal = journal;
        _execution = new ExecutionUnique(CapturerAsync, exception => _journal.Erreur("Capture d'une copie en échec.", exception));
        _relais.CopieDetectee += SurCopieDetectee;
    }

    /// <summary>
    /// Levé après l'ajout (ou la remontée) d'un élément, sur le fil qui a reçu la notification (le fil d'interface).
    /// </summary>
    public event Action<Element>? ElementAjoute;

    /// <summary>Cesse d'écouter le relais. Une capture déjà commencée peut finir de lire mais n'écrit plus dans l'historique.</summary>
    public void Dispose()
    {
        _libere = true;
        _relais.CopieDetectee -= SurCopieDetectee;
    }

    // L'origine est relevée tout de suite, avant que l'utilisateur ne change de fenêtre ; chaque notification la remplace,
    // la lecture de relance utilisera donc la plus récente.
    private void SurCopieDetectee()
    {
        if (_libere)
        {
            return;
        }

        _application = ApplicationOrigine.DeterminerPourCopie();
        _execution.Demander();
    }

    private async Task CapturerAsync()
    {
        var contenu = await _lecteur.LireAsync(_application);
        if (contenu is null || _libere)
        {
            return;
        }

        var element = _depot.AjouterCopie(contenu);
        AppliquerPlafond();
        ElementAjoute?.Invoke(element);
    }

    // Le plafond est un ménage : son échec ne doit pas faire perdre la copie qui vient d'être enregistrée.
    private void AppliquerPlafond()
    {
        try
        {
            _depot.AppliquerPlafond(_parametres().PlafondTemporaire);
        }
        catch (Exception exception)
        {
            _journal.Avertissement("Plafond de Temporaire non appliqué après une copie.", exception);
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
  total : 521
  échec : 0
  réussie : 521
  ignoré : 0
```

Les 8 nouveaux tests : `ExecutionUniqueTests` 8.

- [ ] **Étape 5 : Vérifier sur le poste**

`ServiceCapture` se vérifie avec le **vrai** relais (tâche 17, vraies notifications `WM_CLIPBOARDUPDATE`) et le vrai presse-papiers ; seul
le dépôt SQLite est remplacé par un double en mémoire qui enregistre les appels (`AjouterCopie`, `AppliquerPlafond`) : le vrai
dépôt est écrit par d'autres tâches. Le programme simule un fil d'interface (boucle de messages, contexte de synchronisation).
Créer le dossier `essais-pp/verif-capture` à côté de `essais-pp/verif` (tâche 21), avec ces deux fichiers :

`essais-pp/verif-capture/verif-capture.csproj`

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
    <RootNamespace>VerifCapture</RootNamespace>
    <!-- Dépôt qui porte le vrai RelaisWindows (tâche 17) ; par défaut, le même. -->
    <Relais Condition="'$(Relais)' == ''">$(Depot)</Relais>
  </PropertyGroup>
  <ItemGroup>
    <!-- Coeur, sans le dépôt SQLite : un double en mémoire le remplace pour la vérification. -->
    <Compile Include="$(Depot)/src/PressePapiers.Coeur/Modele/*.cs" />
    <Compile Include="$(Depot)/src/PressePapiers.Coeur/Journalisation/*.cs" />
    <Compile Include="$(Depot)/src/PressePapiers.Coeur/Formats/*.cs" />
    <Compile Include="$(Depot)/src/PressePapiers.Coeur/Capture/*.cs" />
    <Compile Include="$(Depot)/src/PressePapiers.Coeur/Reglages/Parametres.cs" />
    <Compile Include="$(Depot)/src/PressePapiers.Coeur/Reglages/Raccourci.cs" />
    <Compile Include="$(Depot)/src/PressePapiers.Coeur/Reglages/ThemeApplication.cs" />
    <!-- Application : la capture à vérifier et le vrai relais. -->
    <Compile Include="$(Depot)/src/PressePapiers/Capture/ApplicationOrigine.cs" />
    <Compile Include="$(Depot)/src/PressePapiers/Capture/ContenuBrut.cs" />
    <Compile Include="$(Depot)/src/PressePapiers/Capture/EcrivainPressePapiers.cs" />
    <Compile Include="$(Depot)/src/PressePapiers/Capture/FormatsPressePapiers.cs" />
    <Compile Include="$(Depot)/src/PressePapiers/Capture/LectureBrute.cs" />
    <Compile Include="$(Depot)/src/PressePapiers/Capture/LecteurPressePapiers.cs" />
    <Compile Include="$(Depot)/src/PressePapiers/Capture/MarqueInterne.cs" />
    <Compile Include="$(Depot)/src/PressePapiers/Capture/ServiceCapture.cs" />
    <Compile Include="$(Depot)/src/PressePapiers/Natif/NatifPressePapiers.cs" />
    <Compile Include="$(Relais)/src/PressePapiers/Systeme/RelaisWindows.cs" />
    <Compile Include="$(Relais)/src/PressePapiers/Natif/NatifRelais.cs" />
    <!-- Accès brut au presse-papiers, partagé avec le programme de vérification des tâches 21 et 22. -->
    <Compile Include="../verif/Brut.cs" />
  </ItemGroup>
</Project>
```

`essais-pp/verif-capture/Program.cs`

```csharp
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PressePapiers.Capture;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Systeme;

// Programme jetable, hors du dépôt : vérifie ServiceCapture avec le vrai relais et le vrai presse-papiers (le dépôt SQLite
// est remplacé par un double en mémoire). Lancer avec -p:Depot=<dépôt> [-p:Relais=<dépôt qui porte RelaisWindows>].
int echecs = 0, total = 0;
void Verifier(string nom, bool ok, string detail = "")
{
    total++;
    if (!ok) echecs++;
    Console.WriteLine($"{(ok ? "OK    " : "ECHEC ")}{nom} {detail}");
}
void Info(string texte) => Console.WriteLine($"      {texte}");

static async Task<bool> Attendre(Func<bool> condition, int delaiMs = 3000)
{
    var chrono = Stopwatch.StartNew();
    while (chrono.ElapsedMilliseconds < delaiMs)
    {
        if (condition()) return true;
        await Task.Delay(10);
    }
    return condition();
}

var journal = new JournalListe();
var depot = new DepotHistorique();
var ui = new FilInterface();
var ajoutes = new ConcurrentQueue<(Element Element, int Fil)>();
RelaisWindows relais = null!;
ServiceCapture capture = null!;
ui.Invoquer(() =>
{
    relais = new RelaisWindows(journal);
    Verifier("relais : écoute du presse-papiers", relais.EcouterPressePapiers());
    var lecteur = new LecteurPressePapiers(relais.Handle, journal);
    capture = new ServiceCapture(relais, lecteur, depot, () => new Parametres { PlafondTemporaire = 123 }, journal);
    capture.ElementAjoute += e => ajoutes.Enqueue((e, Environment.CurrentManagedThreadId));
});
var filUi = ui.IdFil;
var hwndEcrivain = Fen.Creer();
var ecrivain = new EcrivainPressePapiers(relais.Handle, journal);

// ---------- Une copie simple ----------
Brut.Ecrire(hwndEcrivain, (Brut.CfUnicodeText, Brut.Utf16Nul("Premier é 😀")));
var vu = await Attendre(() => depot.Elements.Any(e => e.Texte == "Premier é 😀"));
Verifier("copie simple : l'élément arrive dans le dépôt", vu);
var premier = depot.Elements.FirstOrDefault(e => e.Texte == "Premier é 😀");
Verifier("copie simple : application d'origine relevée", !string.IsNullOrEmpty(premier?.Application), $"-> {premier?.Application}");
Verifier("copie simple : ajout puis plafond (réglage lu dans Parametres)", depot.Appels.Take(2).SequenceEqual(["Ajouter:Premier é 😀", "Plafond:123"]), string.Join(" ; ", depot.Appels));
await Task.Delay(300);
Verifier("copie simple : ElementAjoute levé sur le fil d'interface", ajoutes.Count >= 1 && ajoutes.All(a => a.Fil == filUi), $"{ajoutes.Count} événement(s)");

// ---------- Copie de l'outil lui-même ----------
var avant = depot.Appels.Count;
var ok = await ecrivain.EcrireAsync(new Element
{
    Id = 1, OngletId = 1, Type = TypeElement.Texte, Texte = "Venu de l'outil", Empreinte = [0],
    DateCopie = DateTimeOffset.UtcNow, DateUsage = DateTimeOffset.UtcNow, Taille = 0,
}, ModeCollage.TexteSeul, null);
await Task.Delay(700);
Verifier("marque interne : l'écriture de l'outil n'est pas capturée", ok && depot.Appels.Count == avant && !depot.Elements.Any(e => e.Texte == "Venu de l'outil"));

// ---------- Rafale de copies différentes ----------
ajoutes.Clear();
for (var i = 0; i < 30; i++)
{
    Brut.Ecrire(hwndEcrivain, (Brut.CfUnicodeText, Brut.Utf16Nul($"Rafale {i}")));
}
await Task.Delay(1500);
var dernier = depot.Elements.OrderByDescending(e => e.DateUsage).First();
Verifier("rafale de 30 copies : la dernière est bien dans l'historique", dernier.Texte == "Rafale 29", $"-> {dernier.Texte}");
Info($"rafale de 30 copies : {ajoutes.Count} capture(s) effective(s) (au lieu de 30)");

// ---------- Rafale de notifications pendant une capture lente ----------
Brut.Ecrire(hwndEcrivain, (Brut.CfUnicodeText, Brut.Utf16Nul("avant occupation")));
await Task.Delay(800);
journal.Vider();
var occupantPret = new ManualResetEventSlim();
var occupant = new Thread(() =>
{
    var h = Fen.Creer();
    while (!Brut.OuvrirPourOccuper(h)) Thread.Sleep(5);
    occupantPret.Set();
    Thread.Sleep(900);
    Brut.Fermer();
    Fen.DestroyWindow(h);
});
occupant.Start();
occupantPret.Wait();
Brut.Poster(relais.Handle, 0x031D);            // WM_CLIPBOARDUPDATE : la capture commence et reste 225 ms à réessayer
await Task.Delay(40);
for (var i = 0; i < 50; i++) Brut.Poster(relais.Handle, 0x031D);   // 50 notifications pendant la lecture
await Task.Delay(1800);
occupant.Join();
var lectures = journal.Compter("resté occupé après 10 essais");
Verifier("50 notifications pendant une capture : une seule lecture de plus (2 au total)", lectures == 2, $"{lectures} lecture(s) tentée(s)");

// ---------- Le service survit à une erreur du dépôt ----------
depot.Echec = c => c.Texte == "BOOM";
journal.Vider();
Brut.Ecrire(hwndEcrivain, (Brut.CfUnicodeText, Brut.Utf16Nul("BOOM")));
var erreurJournalisee = await Attendre(() => journal.Contient("Capture d'une copie en échec"));
Verifier("erreur du dépôt : journalisée", erreurJournalisee, journal.Dernier());
Brut.Ecrire(hwndEcrivain, (Brut.CfUnicodeText, Brut.Utf16Nul("Après l'erreur")));
Verifier("erreur du dépôt : la copie suivante est capturée", await Attendre(() => depot.Elements.Any(e => e.Texte == "Après l'erreur")));
depot.Echec = null;

// ---------- Dispose ----------
ui.Invoquer(() => capture.Dispose());
var avantDispose = depot.Appels.Count;
Brut.Ecrire(hwndEcrivain, (Brut.CfUnicodeText, Brut.Utf16Nul("Après dispose")));
await Task.Delay(800);
Verifier("après Dispose : plus aucune capture", depot.Appels.Count == avantDispose && !depot.Elements.Any(e => e.Texte == "Après dispose"));

// ---------- Nettoyage ----------
ui.Invoquer(() => relais.Dispose());
ui.Arreter();
Brut.Ecrire(hwndEcrivain, (Brut.CfUnicodeText, Brut.Utf16Nul("(texte neutre : essai terminé)")));
Fen.DestroyWindow(hwndEcrivain);

Console.WriteLine();
foreach (var ligne in journal.Lignes.TakeLast(5)) Console.WriteLine("  journal : " + ligne);
Console.WriteLine($"{total - echecs}/{total} vérifications réussies");
return echecs == 0 ? 0 : 1;

internal sealed class FilInterface
{
    [DllImport("user32.dll")] static extern bool PeekMessageW(out Msg m, nint h, uint a, uint b, uint remove);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref Msg m);
    [DllImport("user32.dll")] static extern nint DispatchMessageW(ref Msg m);

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg { public nint Hwnd; public uint Message; public nuint WParam; public nint LParam; public uint Time; public int X, Y; public uint Prive; }

    private readonly BlockingCollection<Action> _file = new();
    private readonly Thread _fil;
    private volatile bool _arret;
    public int IdFil { get; private set; }

    private sealed class Contexte(FilInterface f) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => f._file.Add(() => d(state));
    }

    public FilInterface()
    {
        var pret = new ManualResetEventSlim();
        _fil = new Thread(() =>
        {
            IdFil = Environment.CurrentManagedThreadId;
            SynchronizationContext.SetSynchronizationContext(new Contexte(this));
            pret.Set();
            while (!_arret)
            {
                while (PeekMessageW(out var m, 0, 0, 0, 1)) { TranslateMessage(ref m); DispatchMessageW(ref m); }
                while (_file.TryTake(out var action)) action();
                Thread.Sleep(1);
            }
        }) { IsBackground = true, Name = "Fil d'interface simulé" };
        _fil.SetApartmentState(ApartmentState.STA);
        _fil.Start();
        pret.Wait();
    }

    public void Invoquer(Action action)
    {
        var fini = new ManualResetEventSlim();
        Exception? erreur = null;
        _file.Add(() => { try { action(); } catch (Exception e) { erreur = e; } fini.Set(); });
        fini.Wait();
        if (erreur is not null) throw new InvalidOperationException("Erreur sur le fil d'interface", erreur);
    }

    public void Arreter() { _arret = true; _fil.Join(); }
}

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
    public int Compter(string morceau) { lock (_verrou) return Lignes.Count(l => l.Contains(morceau, StringComparison.Ordinal)); }
    public string Dernier() { lock (_verrou) return Lignes.LastOrDefault() ?? "(journal vide)"; }
}

namespace PressePapiers.Coeur.Stockage
{
    /// <summary>Double en mémoire du dépôt, pour vérifier ServiceCapture sans la base : mêmes signatures que le contrat.</summary>
    public sealed class DepotHistorique
    {
        private readonly object _verrou = new();
        private long _id;
        public List<string> Appels { get; } = [];
        public List<Element> Elements { get; } = [];
        public Func<ContenuCopie, bool>? Echec { get; set; }

        public Element AjouterCopie(ContenuCopie c)
        {
            lock (_verrou)
            {
                Appels.Add("Ajouter:" + c.Texte);
                if (Echec?.Invoke(c) == true) throw new InvalidOperationException("dépôt en panne simulée");
                var maintenant = DateTimeOffset.UtcNow;
                var existant = Elements.FindIndex(e => e.Type == c.Type && e.Texte == c.Texte);
                var element = new Element
                {
                    Id = existant >= 0 ? Elements[existant].Id : ++_id, OngletId = 1, Type = c.Type, Texte = c.Texte, Rtf = c.Rtf, Html = c.Html,
                    Empreinte = [0], Application = c.Application, DateCopie = maintenant, DateUsage = maintenant, Taille = 0,
                };
                if (existant >= 0) Elements[existant] = element; else Elements.Add(element);
                return element;
            }
        }

        public int AppliquerPlafond(int plafond)
        {
            lock (_verrou) Appels.Add("Plafond:" + plafond);
            return 0;
        }
    }
}
```

```bash
cd essais-pp/verif-capture
dotnet run -c Debug -p:Depot=<chemin absolu du dépôt>
```

À observer : `11/11 vérifications réussies`, aucun `ECHEC`. Les contrôles : une copie simple arrive dans le dépôt avec son application
d'origine, l'appel au plafond suit l'ajout et lit le réglage de `Parametres` (123 dans l'essai), `ElementAjoute` est levé sur le fil
d'interface ; une écriture de l'outil (marque interne) n'est pas capturée ; une rafale de 30 copies différentes aboutit à la dernière
(« Rafale 29 ») avec **2 captures** seulement au lieu de 30 ; 50 notifications `WM_CLIPBOARDUPDATE` postées pendant une lecture
retardée (presse-papiers occupé 900 ms) donnent **exactement 2 lectures** (celle en cours et une relance), pas 51 ; une panne du dépôt
est journalisée et la copie suivante est capturée ; après `Dispose`, plus rien n'est capturé. Ne rien copier à la main pendant
l'essai. Le programme laisse un texte neutre dans le presse-papiers.

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Capture/ExecutionUnique.cs tests/PressePapiers.Tests/Capture/ExecutionUniqueTests.cs src/PressePapiers/Capture/ServiceCapture.cs
git commit -m "Ajouter le service de capture" -m "Application d'origine relevée dès la notification, lecture, AjouterCopie puis AppliquerPlafond, ElementAjoute. Une capture à la fois : ExecutionUnique fond une rafale de notifications en une seule relance. Une erreur du dépôt est journalisée sans arrêter le service."
```
