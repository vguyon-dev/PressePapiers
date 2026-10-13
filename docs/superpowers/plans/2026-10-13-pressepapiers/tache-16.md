### Tâche 16 : Démarrage de l'application (instance unique, journal, base, session, fenêtre masquée)

**Fichiers :**
- Créer : `src/PressePapiers.Coeur/Stockage/IDialogueDemarrage.cs`
- Créer : `src/PressePapiers.Coeur/Stockage/OuvertureBase.cs`
- Créer : `src/PressePapiers/Natif/NatifSession.cs`
- Créer : `src/PressePapiers/Natif/NatifDemarrage.cs`
- Créer : `src/PressePapiers/Systeme/DialoguesWindows.cs`
- Modifier : `src/PressePapiers/Program.cs` (le plan donne alors le contenu complet du fichier après modification)
- Modifier : `src/PressePapiers/App.xaml.cs` (idem)
- Modifier : `src/PressePapiers/Systeme/JournalSerilog.cs` (idem)
- Modifier : `src/PressePapiers/Systeme/SourceSessionWts.cs` (idem)
- Modifier : `src/PressePapiers/Systeme/DemarrageWindows.cs` (idem)
- Modifier : `src/PressePapiers/PressePapiers.csproj` (propriété `AllowUnsafeBlocks` ajoutée, que le générateur `LibraryImport` exige dès la première déclaration native de l'application ; le plan donne le contenu complet après modification)
- Inchangés : `src/PressePapiers/App.xaml`, les coquilles `FenetrePrincipale` et `FenetrePrincipaleVue`
- Tests : `tests/PressePapiers.Tests/Stockage/OuvertureBaseTests.cs`
- Hors dépôt (étape 5, programmes et scripts jetables) : `$ESSAIS/verifier-demarrage.ps1`, `$ESSAIS/Session/` (2 fichiers), `$ESSAIS_FENETRE/` (7 fichiers)

**Écart au contrat (section 5) :** quatre fichiers s'ajoutent à ceux que le contrat attribue à cette tâche, tous nouveaux, aucun
fichier d'une autre tâche n'est touché. `IDialogueDemarrage.cs` et `OuvertureBase.cs` (dans Coeur, pour que la logique de
restauration de la base soit testée par xUnit sans WinUI), `NatifDemarrage.cs` (MessageBoxW et l'attente non bloquante de la
redirection d'instance, pour ne pas mélanger ces appels à ceux de `NatifSession.cs`, réservé à WTS) et `DialoguesWindows.cs`
(l'implémentation native de `IDialogueDemarrage`).

**Interfaces :**
- Utilise :
  - tâche 1 : `IJournal` (`Info`, `Avertissement`, `Erreur`), `JournalNul.Instance`, `ISourceSession` (`long? LireHeureOuverture()`),
    `OrigineOuverture.Relance`, les coquilles `FenetrePrincipale(FenetrePrincipaleVue vue)` (qui lève `NotImplementedException`
    jusqu'aux tâches 25 à 28) et `FenetrePrincipale.AfficherPour(nint fenetreOrigine, OrigineOuverture origine)` ;
  - tâches 2 et 4 à 7 : `DossierDonnees` (`Base`, `Images`, `Sauvegardes`, `Journal`, `Parametres`, `ParDefaut()`, `CreerDossiers()`),
    `BaseHistorique.Ouvrir(string)`, `BaseIncompatibleException`, `Sauvegarde.Creer(string, string, TimeProvider, int)`,
    `Sauvegarde.PlusRecente(string)`, `Sauvegarde.Restaurer(string, string)`, `DepotHistorique(BaseHistorique, DossierDonnees,
    TimeProvider, IJournal)` ;
  - tâche 8 : `DemarrageSession.Appliquer(ISourceSession, DepotHistorique, IJournal)` et `EtatSession` ;
  - tâche 9 : `MoteurRecherche(BaseHistorique)` ; tâche 10 : `ServiceParametres(string, IJournal)` ;
  - tâches 17, 21, 22, 24 : `RelaisWindows(IJournal)` (propriété `Handle`, `Dispose()`), `EcrivainPressePapiers(nint, IJournal)`,
    `ServiceRecollage(EcrivainPressePapiers, DepotHistorique, IJournal)` ;
  - tâche 19 : `PremierPlan.AmenerParEtapes(nint)` (interne), uniquement dans le programme d'essai de l'étape 5 ;
  - tâches de test : `DossierTemporaire`, `JournalCollecte`, `ContenusDeTest`, `InsertionsSql` (dossier `tests/PressePapiers.Tests/Outils`).
- Fournit :
  - `public interface IDialogueDemarrage { void Informer(string message); bool Confirmer(string question); }` (Coeur, namespace
    `PressePapiers.Coeur.Stockage`) ;
  - `public static class OuvertureBase` avec `public static BaseHistorique? Ouvrir(DossierDonnees dossiers, TimeProvider horloge,
    IJournal journal, IDialogueDemarrage dialogue)` (null = l'application ne peut pas démarrer, l'utilisateur en a été informé) et
    la variante testable `internal static ... Ouvrir(..., Func<string, BaseHistorique> ouvrir)` ;
  - `public sealed class DialoguesWindows : IDialogueDemarrage` (MessageBoxW natif, bouton par défaut Non pour la question) ;
  - `public sealed class SourceSessionWts : ISourceSession` (LogonTime de WTSINFOW, `null` si la lecture échoue) ;
  - `public static class DemarrageWindows` : `EstActive()`, `Appliquer(bool, string)` du contrat, plus les surcharges internes
    `EstActive(string cheminCle)` et `Appliquer(bool, string, string cheminCle)` (clé sous HKCU, pour les essais), et les constantes
    internes `CleRun`, `NomValeur` ;
  - `public sealed class JournalSerilog : IJournal, IDisposable` : fichier `journal-AAAAMMJJ.log`, 14 fichiers gardés, ouverture
    partagée (une seconde instance peut y écrire), appels ignorés après `Dispose` ;
  - `Program.Main` : instance unique ; `App` : `internal static bool ArretEnCours` (drapeau que la croix de la fenêtre,
    tâche 27, doit lire avant de masquer au lieu de fermer), `internal static void SignalerActivation()`, `internal void
    AfficherFenetre(OrigineOuverture origine)`, `internal void Quitter()` (chemin Quitter : drapeau, libération, `Exit()`),
    `internal void PosterSurInterface(Action action, string contexte)` (à utiliser pour tout code posté au `DispatcherQueue`,
    voir l'étape 5d) ;
  - consommateurs : tâche 27 (lit `App.ArretEnCours`, reçoit `AfficherPour(0, OrigineOuverture.Relance)`) et tâche 29 (complète
    `App.CreerFenetrePrincipale` et `App.Quitter`, retire l'attrape-`NotImplementedException`, branche l'écoute du relais, l'icône,
    le hook, la capture, `DemarrageWindows.Appliquer` et `PosterSurInterface` pour `WinVPresse`).

Décisions de comportement à connaître :
- **Rien n'écrit dans la vraie clé Run à cette tâche.** `DemarrageWindows` existe et est vérifié sous une clé d'essai, mais
  l'application ne l'appelle pas encore : l'application du réglage `DemarrageAvecWindows` appartient à la tâche 29.
- **Le relais est créé avant la fenêtre** (la spec le range après) parce que `FenetrePrincipaleVue` exige un `ServiceRecollage`,
  donc un `EcrivainPressePapiers`, donc le handle du relais. Il n'écoute rien et n'enregistre aucun raccourci avant la tâche 29.
- **Tant que `FenetrePrincipale` est une coquille**, son constructeur lève `NotImplementedException` : `App` l'attrape, note un
  avertissement et démarre sans fenêtre. Ce `catch` est provisoire (commentaire « À retirer à la tâche 29 »).
- **Base illisible** (codes SQLite 11 ou 26 seulement) : copie de sécurité impossible, ou ouverture qui échoue. Message clair et
  question « Restaurer la copie la plus récente ? » (Non par défaut) ; le fichier abîmé est copié dans `sauvegardes\illisible-AAAAMMJJ-HHmmss.db`
  avant d'être remplacé. **Base incompatible** (SQLite trop ancienne) ou autre erreur SQLite (accès refusé, verrou) : message
  seulement, jamais de restauration (elle n'y changerait rien et écraserait la base).
- **Arrêt de recette, versions Debug seulement** : la variable d'environnement `PRESSEPAPIERS_ARRET_APRES_MS` programme un appel de
  `Quitter()` après ce délai. Sans icône de notification (tâche 29), c'est le seul moyen de vérifier l'arrêt propre sans tuer le
  processus ; le code est sous `#if DEBUG` et absent des versions Release.

- [ ] **Étape 1 : Écrire le test qui échoue**

Neuf tests sur `OuvertureBase`, le seul code de cette tâche qui se teste sans Windows ni WinUI : le reste (instance unique,
journal Serilog, WTS, clé Run, boîtes de message) se vérifie sur le poste à l'étape 5. Chaque test d'ordre ou de priorité casse si
la logique est inversée : la restauration prend la copie **la plus récente** (deux copies de contenus différents : la plus ancienne
rendrait 1 élément au lieu de 3), et une erreur SQLite autre qu'une base abîmée ne propose **jamais** la restauration (si
`EstBaseAbimee` répondait toujours vrai, `Un_probleme_d_acces_a_la_base_ne_propose_jamais_de_restauration` échouerait : mutation jouée).

`tests/PressePapiers.Tests/Stockage/OuvertureBaseTests.cs`

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class OuvertureBaseTests : IDisposable
{
    private const string Abime = "du texte dans le fichier d'historique, pas une base SQLite";

    private static readonly DateTimeOffset Maintenant = new(2026, 10, 13, 8, 15, 30, TimeSpan.Zero);

    private readonly DossierTemporaire _racine = new();
    private readonly FakeTimeProvider _horloge = new(Maintenant);
    private readonly JournalCollecte _journal = new();
    private readonly DialogueEspion _dialogue = new();
    private readonly DossierDonnees _dossiers;

    public OuvertureBaseTests()
    {
        _dossiers = new DossierDonnees(_racine.Fichier("donnees"));
        _dossiers.CreerDossiers();
    }

    public void Dispose() => _racine.Dispose();

    // ---- Démarrage normal ----

    [Fact]
    public void Sans_fichier_de_base_une_base_neuve_est_creee_sans_rien_demander()
    {
        using var ouverte = Ouvrir();

        Assert.NotNull(ouverte);
        Assert.Equal("0", InsertionsSql.Scalaire(ouverte.Connexion, "SELECT COUNT(*) FROM element"));
        Assert.Empty(_dialogue.Informations);
        Assert.Empty(_dialogue.Questions);
        Assert.Empty(Directory.GetFiles(_dossiers.Sauvegardes));
    }

    [Fact]
    public void Une_base_saine_est_sauvegardee_puis_ouverte_avec_ses_donnees()
    {
        PreparerBase(2);

        using var ouverte = Ouvrir();

        Assert.NotNull(ouverte);
        Assert.Equal("2", InsertionsSql.Scalaire(ouverte.Connexion, "SELECT COUNT(*) FROM element"));
        Assert.Equal(["historique-20261013-081530.db"], NomsDeFichiers(_dossiers.Sauvegardes));
        Assert.Empty(_dialogue.Informations);
        Assert.Empty(_dialogue.Questions);
    }

    [Fact]
    public void Une_copie_de_securite_impossible_n_empeche_pas_de_demarrer()
    {
        PreparerBase(2);
        // Un fichier à la place du dossier des sauvegardes : la copie ne peut pas être créée.
        Directory.Delete(_dossiers.Sauvegardes);
        File.WriteAllText(_dossiers.Sauvegardes, "pas un dossier");

        using var ouverte = Ouvrir();

        Assert.NotNull(ouverte);
        Assert.Equal("2", InsertionsSql.Scalaire(ouverte.Connexion, "SELECT COUNT(*) FROM element"));
        Assert.Single(_journal.Avertissements);
        Assert.Empty(_dialogue.Questions);
    }

    // ---- Base illisible ----

    [Fact]
    public void Une_base_illisible_sans_copie_arrete_le_demarrage_en_le_disant()
    {
        File.WriteAllText(_dossiers.Base, Abime);

        var ouverte = Ouvrir();

        Assert.Null(ouverte);
        Assert.Equal(Abime, File.ReadAllText(_dossiers.Base));
        Assert.Empty(_dialogue.Questions);
        var message = Assert.Single(_dialogue.Informations);
        Assert.Contains("copie de sécurité", message);
        Assert.Contains(_dossiers.Base, message);
        Assert.NotEmpty(_journal.Erreurs);
    }

    [Fact]
    public void Une_base_illisible_est_restauree_depuis_la_copie_la_plus_recente_quand_l_utilisateur_accepte()
    {
        // Deux copies de contenus différents : choisir la plus ancienne rendrait 1 élément au lieu de 3.
        CreerCopie(elements: 1, quand: Maintenant.AddHours(-2));
        CreerCopie(elements: 3, quand: Maintenant.AddHours(-1));
        File.WriteAllText(_dossiers.Base, Abime);
        _dialogue.Reponse = true;

        using var ouverte = Ouvrir();

        Assert.NotNull(ouverte);
        Assert.Equal("3", InsertionsSql.Scalaire(ouverte.Connexion, "SELECT COUNT(*) FROM element"));
        Assert.Single(_dialogue.Questions);
        Assert.Empty(_dialogue.Informations);
        // Les deux bonnes copies survivent à la base abîmée, et celle-ci est gardée à côté pour examen.
        Assert.Equal(
            ["historique-20261013-061530.db", "historique-20261013-071530.db", "illisible-20261013-081530.db"],
            NomsDeFichiers(_dossiers.Sauvegardes));
        Assert.Equal(Abime, File.ReadAllText(Path.Combine(_dossiers.Sauvegardes, "illisible-20261013-081530.db")));
    }

    [Fact]
    public void Le_refus_de_l_utilisateur_laisse_la_base_et_les_copies_en_place()
    {
        CreerCopie(elements: 3, quand: Maintenant.AddHours(-1));
        File.WriteAllText(_dossiers.Base, Abime);
        _dialogue.Reponse = false;

        var ouverte = Ouvrir();

        Assert.Null(ouverte);
        Assert.Single(_dialogue.Questions);
        Assert.Equal(Abime, File.ReadAllText(_dossiers.Base));
        Assert.Equal(["historique-20261013-071530.db"], NomsDeFichiers(_dossiers.Sauvegardes));
    }

    [Fact]
    public void Une_copie_elle_meme_abimee_arrete_le_demarrage_en_le_disant()
    {
        File.WriteAllText(Path.Combine(_dossiers.Sauvegardes, "historique-20261013-071530.db"), Abime);
        File.WriteAllText(_dossiers.Base, Abime);
        _dialogue.Reponse = true;

        var ouverte = Ouvrir();

        Assert.Null(ouverte);
        var message = Assert.Single(_dialogue.Informations);
        Assert.Contains("restauration", message);
        Assert.NotEmpty(_journal.Erreurs);
    }

    // ---- Erreurs qui ne justifient pas d'écraser la base ----

    [Fact]
    public void Un_probleme_d_acces_a_la_base_ne_propose_jamais_de_restauration()
    {
        CreerCopie(elements: 3, quand: Maintenant.AddHours(-1));
        // Un dossier à la place du fichier : SQLite répond « impossible d'ouvrir », pas « base corrompue ».
        Directory.CreateDirectory(_dossiers.Base);

        var ouverte = Ouvrir();

        Assert.Null(ouverte);
        Assert.Empty(_dialogue.Questions);
        Assert.Single(_dialogue.Informations);
        Assert.True(Directory.Exists(_dossiers.Base));
    }

    [Fact]
    public void Une_version_de_SQLite_trop_ancienne_est_expliquee_sans_proposer_de_restauration()
    {
        CreerCopie(elements: 3, quand: Maintenant.AddHours(-1));

        var ouverte = OuvertureBase.Ouvrir(_dossiers, _horloge, _journal, _dialogue,
            _ => throw new BaseIncompatibleException("SQLite 3.30 est trop ancienne."));

        Assert.Null(ouverte);
        Assert.Empty(_dialogue.Questions);
        var message = Assert.Single(_dialogue.Informations);
        Assert.Contains("SQLite 3.30 est trop ancienne.", message);
        Assert.NotEmpty(_journal.Erreurs);
    }

    // ---- Outils ----

    private BaseHistorique? Ouvrir() => OuvertureBase.Ouvrir(_dossiers, _horloge, _journal, _dialogue);

    // Crée une base de test avec ce nombre d'éléments, puis la ferme.
    private void PreparerBase(int elements)
    {
        using var baseHistorique = BaseHistorique.Ouvrir(_dossiers.Base);
        var depot = new DepotHistorique(baseHistorique, _dossiers, _horloge, JournalNul.Instance);
        for (var i = 0; i < elements; i++)
        {
            depot.AjouterCopie(ContenusDeTest.Texte("element " + i));
        }
    }

    // Pose une copie de sécurité datée de « quand » (nom en UTC), puis retire la base d'origine.
    private void CreerCopie(int elements, DateTimeOffset quand)
    {
        PreparerBase(elements);
        Sauvegarde.Creer(_dossiers.Base, _dossiers.Sauvegardes, new FakeTimeProvider(quand));
        File.Delete(_dossiers.Base);
    }

    private static string[] NomsDeFichiers(string dossier) =>
        [.. Directory.GetFiles(dossier).Select(chemin => Path.GetFileName(chemin)).Order()];

    // Dialogue de test : note ce qui est dit à l'utilisateur et répond à la question par la valeur réglée.
    private sealed class DialogueEspion : IDialogueDemarrage
    {
        public bool Reponse { get; set; }

        public List<string> Informations { get; } = [];

        public List<string> Questions { get; } = [];

        public void Informer(string message) => Informations.Add(message);

        public bool Confirmer(string question)
        {
            Questions.Add(question);
            return Reponse;
        }
    }
}
```

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu avant l'étape 3 : échec de compilation du projet de tests, `error CS0246: Le nom de type ou d'espace de noms
'IDialogueDemarrage' est introuvable` (et `OuvertureBase` introuvable), aucun test exécuté.

- [ ] **Étape 3 : Implémenter**

Coeur : l'interface des dialogues puis la logique d'ouverture de la base.

`src/PressePapiers.Coeur/Stockage/IDialogueDemarrage.cs`

```csharp
namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// Ce que le démarrage peut dire ou demander à l'utilisateur avant que la moindre fenêtre existe.
/// L'application l'implémente par une boîte de message native ; les tests, par un espion.
/// </summary>
public interface IDialogueDemarrage
{
    /// <summary>Affiche un message bloquant (problème qui arrête le démarrage).</summary>
    /// <param name="message">Texte clair, en français, qui dit quoi faire.</param>
    void Informer(string message);

    /// <summary>Pose une question oui ou non et renvoie vrai si l'utilisateur répond oui.</summary>
    /// <param name="question">Texte de la question.</param>
    bool Confirmer(string question);
}
```

`src/PressePapiers.Coeur/Stockage/OuvertureBase.cs`

```csharp
using System.Globalization;
using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// Ouverture de la base au démarrage (spec, sections 5 et 12) : copie de sécurité, ouverture, et, si la base est
/// illisible, proposition de restaurer la copie la plus récente. Rien n'est jamais écrasé sans l'accord de l'utilisateur.
/// </summary>
public static class OuvertureBase
{
    // Codes primaires de SQLite pour « base corrompue » (SQLITE_CORRUPT) et « fichier qui n'est pas une base » (SQLITE_NOTADB).
    private const int CodeCorrompue = 11;
    private const int CodePasUneBase = 26;

    /// <summary>
    /// Copie la base, l'ouvre, et gère les échecs. Renvoie null quand l'application ne peut pas démarrer : l'utilisateur
    /// en a alors été informé par <paramref name="dialogue"/> et le détail est au journal. Une copie de sécurité impossible
    /// n'empêche pas de démarrer (avertissement seulement). La restauration n'est proposée que pour une base abîmée,
    /// jamais pour un autre échec (accès refusé, base verrouillée, SQLite trop ancienne).
    /// </summary>
    /// <param name="dossiers">Emplacements des données (base et copies).</param>
    /// <param name="horloge">Source de l'heure du nom des copies.</param>
    /// <param name="journal">Journal des événements notables.</param>
    /// <param name="dialogue">Moyen de parler à l'utilisateur.</param>
    public static BaseHistorique? Ouvrir(DossierDonnees dossiers, TimeProvider horloge, IJournal journal, IDialogueDemarrage dialogue) =>
        Ouvrir(dossiers, horloge, journal, dialogue, BaseHistorique.Ouvrir);

    /// <summary>Variante testable : l'ouverture de la base est injectée.</summary>
    internal static BaseHistorique? Ouvrir(DossierDonnees dossiers, TimeProvider horloge, IJournal journal,
                                           IDialogueDemarrage dialogue, Func<string, BaseHistorique> ouvrir)
    {
        ArgumentNullException.ThrowIfNull(dossiers);
        ArgumentNullException.ThrowIfNull(horloge);
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(ouvrir);

        // La copie passe avant l'ouverture : pas de connexion d'écriture tant que l'état d'origine n'est pas à l'abri.
        var abimee = false;
        try
        {
            Sauvegarde.Creer(dossiers.Base, dossiers.Sauvegardes, horloge);
        }
        catch (SqliteException exception) when (EstBaseAbimee(exception))
        {
            journal.Erreur("La base de l'historique est illisible : aucune copie de sécurité n'a été faite.", exception);
            abimee = true;
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            journal.Avertissement("Copie de sécurité impossible : le démarrage continue sans elle.", exception);
        }

        if (!abimee)
        {
            try
            {
                return ouvrir(dossiers.Base);
            }
            catch (BaseIncompatibleException exception)
            {
                journal.Erreur("Version de SQLite incompatible.", exception);
                dialogue.Informer($"{exception.Message}{Environment.NewLine}{Environment.NewLine}PressePapiers ne peut pas démarrer.");
                return null;
            }
            catch (SqliteException exception) when (EstBaseAbimee(exception))
            {
                journal.Erreur("La base de l'historique est illisible.", exception);
            }
            catch (SqliteException exception)
            {
                journal.Erreur("La base de l'historique ne peut pas être ouverte.", exception);
                dialogue.Informer(
                    $"La base de l'historique ne peut pas être ouverte ({exception.Message}).{Environment.NewLine}{Environment.NewLine}"
                    + $"Fichier : {dossiers.Base}{Environment.NewLine}Détail dans le journal : {dossiers.Journal}");
                return null;
            }
        }

        return Restaurer(dossiers, horloge, journal, dialogue, ouvrir);
    }

    // Vrai pour une base abîmée, seul cas où une restauration a un sens.
    private static bool EstBaseAbimee(SqliteException exception) =>
        exception.SqliteErrorCode is CodeCorrompue or CodePasUneBase;

    private static BaseHistorique? Restaurer(DossierDonnees dossiers, TimeProvider horloge, IJournal journal,
                                             IDialogueDemarrage dialogue, Func<string, BaseHistorique> ouvrir)
    {
        var copie = Sauvegarde.PlusRecente(dossiers.Sauvegardes);
        if (copie is null)
        {
            dialogue.Informer(
                $"La base de l'historique est illisible et il n'existe aucune copie de sécurité.{Environment.NewLine}{Environment.NewLine}"
                + $"Fichier : {dossiers.Base}{Environment.NewLine}"
                + "Déplacez ce fichier pour repartir d'un historique vide, puis relancez PressePapiers.");
            return null;
        }

        var dateCopie = File.GetLastWriteTime(copie).ToString("g", CultureInfo.CurrentCulture);
        var accepte = dialogue.Confirmer(
            $"La base de l'historique est illisible.{Environment.NewLine}{Environment.NewLine}"
            + $"Restaurer la copie de sécurité la plus récente ({dateCopie}) ? Les éléments copiés depuis cette date seront perdus."
            + $"{Environment.NewLine}Le fichier abîmé est gardé dans le dossier des sauvegardes.");
        if (!accepte)
        {
            journal.Info("Restauration refusée par l'utilisateur : l'application ne démarre pas.");
            return null;
        }

        try
        {
            // Le fichier abîmé est copié à côté avant d'être remplacé : rien n'est perdu sans retour possible.
            var ecartee = Path.Combine(dossiers.Sauvegardes,
                $"illisible-{horloge.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.db");
            File.Copy(dossiers.Base, ecartee, overwrite: true);

            Sauvegarde.Restaurer(copie, dossiers.Base);
            var restauree = ouvrir(dossiers.Base);
            journal.Info($"Base restaurée depuis la copie de sécurité {Path.GetFileName(copie)}.");
            return restauree;
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            journal.Erreur("La restauration de la copie de sécurité a échoué.", exception);
            dialogue.Informer(
                $"La restauration de la copie de sécurité a échoué ({exception.Message}).{Environment.NewLine}{Environment.NewLine}"
                + $"Détail dans le journal : {dossiers.Journal}");
            return null;
        }
    }
}
```

Appels natifs du démarrage (un fichier par brique, `LibraryImport`). `WTSINFOW` fait 216 octets en x64 et `LogonTime` est à
l'offset 200 : l'essai de l'étape 5b le contrôle.

`src/PressePapiers/PressePapiers.csproj` (contenu complet après modification : seule la propriété `AllowUnsafeBlocks` et son
commentaire sont nouveaux ; sans elle, les déclarations `LibraryImport` de `NatifSession` et `NatifDemarrage` ne compilent pas,
erreur CS0227) :

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <RootNamespace>PressePapiers</RootNamespace>
    <AssemblyName>PressePapiers</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Platforms>x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <UseWinUI>true</UseWinUI>
    <EnableMsixTooling>true</EnableMsixTooling>
    <!-- Application non empaquetée et autonome : un dossier à copier, sans installation ni droits administrateur. -->
    <WindowsPackageType>None</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <SelfContained>true</SelfContained>
    <!-- Main personnalisé (Program.cs) : instance unique avant toute création de fenêtre. -->
    <DefineConstants>$(DefineConstants);DISABLE_XAML_GENERATED_MAIN</DefineConstants>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <!-- LibraryImport génère des appels natifs qui exigent le code non sûr. -->
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" />
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" />
    <PackageReference Include="CommunityToolkit.Mvvm" />
    <PackageReference Include="Serilog" />
    <PackageReference Include="Serilog.Sinks.File" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\PressePapiers.Coeur\PressePapiers.Coeur.csproj" />
  </ItemGroup>

</Project>
```

`src/PressePapiers/Natif/NatifSession.cs`

```csharp
using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>Appels Windows de la lecture de la session (spec, section 6). Chaque brique native est autonome.</summary>
internal static partial class NatifSession
{
    /// <summary>WTS_CURRENT_SERVER_HANDLE : le serveur local.</summary>
    internal const nint ServeurCourant = 0;

    /// <summary>WTS_CURRENT_SESSION : la session du processus appelant.</summary>
    internal const uint SessionCourante = 0xFFFFFFFF;

    /// <summary>Valeur WTS_INFO_CLASS pour WTSSessionInfo (structure WTSINFOW).</summary>
    internal const int ClasseSessionInfo = 24;

    /// <summary>Taille de WTSINFOW en x64, contrôlée avant de lire la structure.</summary>
    internal const int TailleInfoSession = 216;

    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool InterrogerSession(nint serveur, uint idSession, int classe, out nint tampon, out uint octets);

    /// <summary>Libère un tampon rendu par WTSQuerySessionInformationW.</summary>
    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSFreeMemory")]
    internal static partial void LibererMemoire(nint memoire);

    /// <summary>
    /// WTSINFOW : les huit compteurs, trois noms en UTF-16 de longueur fixe (nom de la station, domaine, utilisateur),
    /// puis cinq dates FILETIME. Seule LogonTime sert ; les autres champs fixent la disposition.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct InfoSession
    {
        internal int Etat;
        internal uint IdSession;
        internal uint OctetsEntrants;
        internal uint OctetsSortants;
        internal uint ImagesEntrantes;
        internal uint ImagesSortantes;
        internal uint OctetsEntrantsCompresses;
        internal uint OctetsSortantsCompresses;
        internal fixed char NomStation[32];
        internal fixed char Domaine[17];
        internal fixed char Utilisateur[21];
        internal long Connexion;
        internal long Deconnexion;
        internal long DerniereEntree;
        internal long OuvertureSession;
        internal long Maintenant;
    }
}
```

`src/PressePapiers/Natif/NatifDemarrage.cs`

```csharp
using System.Runtime.InteropServices;

namespace PressePapiers.Natif;

/// <summary>Appels Windows du démarrage : boîte de message avant toute fenêtre, attente non bloquante de la redirection.</summary>
internal static partial class NatifDemarrage
{
    internal const uint MB_OK = 0x0, MB_YESNO = 0x4, MB_ICONERROR = 0x10, MB_ICONWARNING = 0x30, MB_DEFBUTTON2 = 0x100, MB_SETFOREGROUND = 0x10000;
    internal const int IDYES = 6;

    /// <summary>Attente de CoWaitForMultipleObjects : sans drapeau particulier, les messages COM continuent d'être pompés.</summary>
    internal const uint CWMO_DEFAULT = 0;

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int AfficherMessage(nint proprietaire, string texte, string titre, uint style);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateEventW", SetLastError = true)]
    internal static partial nint CreerEvenement(nint attributs, [MarshalAs(UnmanagedType.Bool)] bool manuel,
                                                [MarshalAs(UnmanagedType.Bool)] bool signale, nint nom);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetEvent(nint evenement);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint handle);

    /// <summary>
    /// Attend un handle en laissant le fil STA pomper ses messages (CoWaitForMultipleObjects) ; renvoie le HRESULT,
    /// 0 quand le handle est signalé.
    /// </summary>
    internal static unsafe int AttendreSansBloquer(nint handle, uint delaiMs) =>
        CoWaitForMultipleObjects(CWMO_DEFAULT, delaiMs, 1, &handle, out _);

    [LibraryImport("ole32.dll")]
    private static unsafe partial int CoWaitForMultipleObjects(uint drapeaux, uint delai, uint nombre, nint* handles, out uint index);
}
```

Briques Windows : repère de session, clé Run, journal, dialogues.

`src/PressePapiers/Systeme/SourceSessionWts.cs`

```csharp
using System.Runtime.InteropServices;
using PressePapiers.Coeur.Session;
using PressePapiers.Natif;

namespace PressePapiers.Systeme;

/// <summary>
/// Repère de session lu par WTSQuerySessionInformationW (spec, section 6) : l'heure d'ouverture de la session de terminal
/// en cours, champ LogonTime de WTSINFOW. Ce repère appartient à la session, pas au jeton du processus : il est le même
/// pour un lancement normal et pour un lancement en administrateur.
/// </summary>
public sealed class SourceSessionWts : ISourceSession
{
    /// <summary>FILETIME brut de LogonTime (gardé tel quel, comparé en égalité exacte), ou null si la lecture échoue.</summary>
    public long? LireHeureOuverture()
    {
        if (!NatifSession.InterrogerSession(NatifSession.ServeurCourant, NatifSession.SessionCourante,
                                            NatifSession.ClasseSessionInfo, out var tampon, out var octets))
        {
            return null;
        }

        try
        {
            // Une structure plus courte que prévu (version de Windows inattendue) ne se lit pas : mieux vaut « inconnu ».
            return octets >= Marshal.SizeOf<NatifSession.InfoSession>()
                ? Marshal.PtrToStructure<NatifSession.InfoSession>(tampon).OuvertureSession
                : null;
        }
        finally
        {
            NatifSession.LibererMemoire(tampon);
        }
    }
}
```

`src/PressePapiers/Systeme/DemarrageWindows.cs`

```csharp
using Microsoft.Win32;

namespace PressePapiers.Systeme;

/// <summary>
/// Lancement avec Windows par la clé Run de l'utilisateur (spec, section 12) : un lancement non élevé, sans droits
/// administrateur. La valeur s'appelle « PressePapiers » et contient le chemin de l'exécutable entre guillemets.
/// </summary>
public static class DemarrageWindows
{
    /// <summary>Clé Run de l'utilisateur, sous HKCU.</summary>
    internal const string CleRun = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Nom de la valeur de la clé Run.</summary>
    internal const string NomValeur = "PressePapiers";

    /// <summary>Vrai si la valeur « PressePapiers » existe dans HKCU\Software\Microsoft\Windows\CurrentVersion\Run.</summary>
    public static bool EstActive() => EstActive(CleRun);

    /// <summary>Crée ou supprime la valeur « PressePapiers » de la clé Run.</summary>
    /// <param name="activer">Vrai pour lancer l'application avec Windows, faux pour retirer la valeur.</param>
    /// <param name="cheminExecutable">Chemin complet de l'exécutable ; ignoré quand <paramref name="activer"/> est faux.</param>
    public static void Appliquer(bool activer, string cheminExecutable) => Appliquer(activer, cheminExecutable, CleRun);

    /// <summary>
    /// Variante qui prend le chemin de la clé sous HKCU : les vérifications travaillent sous une clé d'essai
    /// et ne touchent jamais la vraie clé Run.
    /// </summary>
    internal static bool EstActive(string cheminCle)
    {
        using var cle = Registry.CurrentUser.OpenSubKey(cheminCle);
        return cle?.GetValue(NomValeur) is not null;
    }

    /// <summary>Variante de <see cref="Appliquer(bool, string)"/> sur la clé donnée (sous HKCU).</summary>
    internal static void Appliquer(bool activer, string cheminExecutable, string cheminCle)
    {
        if (activer)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(cheminExecutable);

            // Les guillemets protègent un chemin qui contient des espaces (le dossier de l'utilisateur, par exemple).
            using var cle = Registry.CurrentUser.CreateSubKey(cheminCle);
            cle.SetValue(NomValeur, $"\"{cheminExecutable}\"", RegistryValueKind.String);
            return;
        }

        // Retirer ce qui n'existe pas n'est pas une erreur : la clé peut ne jamais avoir été créée.
        using var existante = Registry.CurrentUser.OpenSubKey(cheminCle, writable: true);
        existante?.DeleteValue(NomValeur, throwOnMissingValue: false);
    }
}
```

`src/PressePapiers/Systeme/JournalSerilog.cs`

```csharp
using System.Globalization;
using System.Text;
using PressePapiers.Coeur.Journalisation;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace PressePapiers.Systeme;

/// <summary>
/// Journal fichier par Serilog (spec, section 13) : un fichier par jour, <c>journal-AAAAMMJJ.log</c>, les 14 plus récents
/// gardés. Les messages sont écrits tels quels (jamais lus comme un modèle Serilog). Après <see cref="Dispose"/>, les
/// appels sont ignorés : un message tardif pendant l'arrêt ne doit rien casser.
/// </summary>
public sealed class JournalSerilog : IJournal, IDisposable
{
    // Une ligne par événement : heure locale avec décalage, niveau sur trois lettres, message, puis l'exception s'il y en a une.
    private const string Modele = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    // Nombre de fichiers de jour gardés.
    private const int FichiersGardes = 14;

    private readonly Logger _logger;
    private int _libere;

    /// <summary>Crée le journal dans un dossier, créé au besoin.</summary>
    /// <param name="dossierJournal">Dossier des fichiers du journal.</param>
    public JournalSerilog(string dossierJournal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dossierJournal);
        Directory.CreateDirectory(dossierJournal);

        // « shared » : une seconde instance, qui note sa redirection avant de s'arrêter, écrit dans le même fichier.
        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(dossierJournal, "journal-.log"),
                outputTemplate: Modele,
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: FichiersGardes,
                shared: true,
                encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            .CreateLogger();
    }

    /// <inheritdoc />
    public void Info(string message) => Ecrire(LogEventLevel.Information, message, null);

    /// <inheritdoc />
    public void Avertissement(string message, Exception? exception = null) => Ecrire(LogEventLevel.Warning, message, exception);

    /// <inheritdoc />
    public void Erreur(string message, Exception? exception = null) => Ecrire(LogEventLevel.Error, message, exception);

    /// <summary>Vide les tampons et ferme le fichier ; sans effet la seconde fois.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _libere, 1) == 0)
        {
            _logger.Dispose();
        }
    }

    private void Ecrire(LogEventLevel niveau, string message, Exception? exception)
    {
        if (Volatile.Read(ref _libere) != 0)
        {
            return;
        }

        // Le texte passe en argument et non dans le modèle : des accolades dans un message restent des accolades.
        _logger.Write(niveau, exception, "{Texte:l}", message);
    }
}
```

`src/PressePapiers/Systeme/DialoguesWindows.cs`

```csharp
using PressePapiers.Coeur.Stockage;
using PressePapiers.Natif;

namespace PressePapiers.Systeme;

/// <summary>
/// Dialogues du démarrage par la boîte de message native (MessageBoxW) : elle n'exige ni fenêtre ni XAML, donc elle sert
/// même quand l'interface n'a pas encore pu démarrer.
/// </summary>
public sealed class DialoguesWindows : IDialogueDemarrage
{
    private const string Titre = "PressePapiers";

    /// <inheritdoc />
    public void Informer(string message) =>
        NatifDemarrage.AfficherMessage(0, message, Titre,
            NatifDemarrage.MB_OK | NatifDemarrage.MB_ICONERROR | NatifDemarrage.MB_SETFOREGROUND);

    /// <inheritdoc />
    public bool Confirmer(string question) =>
        // Le bouton par défaut est Non : un Entrée distrait ne remplace jamais la base.
        NatifDemarrage.AfficherMessage(0, question, Titre,
            NatifDemarrage.MB_YESNO | NatifDemarrage.MB_ICONWARNING | NatifDemarrage.MB_DEFBUTTON2 | NatifDemarrage.MB_SETFOREGROUND)
        == NatifDemarrage.IDYES;
}
```

Point d'entrée et application.

`src/PressePapiers/Program.cs`

```csharp
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Natif;
using PressePapiers.Systeme;

namespace PressePapiers;

/// <summary>
/// Point d'entrée personnalisé (symbole DISABLE_XAML_GENERATED_MAIN) : instance unique avant toute création de fenêtre,
/// selon la documentation du Windows App SDK sur l'instanciation d'une application (spec, section 12).
/// </summary>
public static class Program
{
    // Clé d'instance : deux lancements qui la partagent sont la même application.
    private const string CleInstance = "PressePapiers";

    // Au-delà, la première instance est jugée muette : la seconde s'arrête quand même, en le notant.
    private const uint DelaiRedirectionMs = 15_000;

    /// <summary>Démarre l'application WinUI 3 sur un fil à appartement unique, ou redirige vers l'instance déjà lancée.</summary>
    [STAThread]
    public static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        // Avant toute fenêtre : la décision se prend tant que le processus n'a rien créé.
        var principale = AppInstance.FindOrRegisterForKey(CleInstance);
        if (!principale.IsCurrent)
        {
            RedirigerVers(principale);
            return;
        }

        // L'événement arrive sur un fil quelconque : App le ramène sur le fil d'interface.
        principale.Activated += (_, _) => App.SignalerActivation();

        Application.Start(parametres =>
        {
            // Sans contexte de synchronisation, les await de l'interface reprendraient sur un fil du pool.
            var contexte = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(contexte);
            _ = new App();
        });
    }

    // Transmet l'activation à l'instance principale puis rend la main (le processus se termine ensuite). L'appel est lancé
    // dans une tâche, car RedirectActivationToAsync a besoin du fil STA : on attend donc par CoWaitForMultipleObjects, qui
    // laisse ce fil pomper ses messages, au lieu d'un Wait() qui le bloquerait.
    private static void RedirigerVers(AppInstance principale)
    {
        var arguments = AppInstance.GetCurrent().GetActivatedEventArgs();
        var termine = NatifDemarrage.CreerEvenement(0, manuel: true, signale: false, 0);
        if (termine == 0)
        {
            return;
        }

        Exception? echec = null;
        _ = Task.Run(async () =>
        {
            try
            {
                await principale.RedirectActivationToAsync(arguments);
            }
            catch (Exception exception)
            {
                echec = exception;
            }
            finally
            {
                NatifDemarrage.SetEvent(termine);
            }
        });

        var resultat = NatifDemarrage.AttendreSansBloquer(termine, DelaiRedirectionMs);

        // Le handle reste ouvert en cas de délai dépassé : la tâche peut encore le signaler, et le processus s'arrête de toute façon.
        if (resultat == 0)
        {
            NatifDemarrage.CloseHandle(termine);
        }

        NoterRedirection(resultat == 0 ? echec : new TimeoutException("L'instance principale n'a pas répondu."));
    }

    // Une trace au journal de l'instance principale (fichier partagé). Jamais d'exception : cette instance va s'arrêter.
    private static void NoterRedirection(Exception? echec)
    {
        try
        {
            using var journal = new JournalSerilog(DossierDonnees.ParDefaut().Journal);
            if (echec is null)
            {
                journal.Info($"Seconde instance (processus {Environment.ProcessId}) : activation redirigée vers l'instance principale, arrêt.");
            }
            else
            {
                journal.Avertissement($"Seconde instance (processus {Environment.ProcessId}) : redirection impossible, arrêt.", echec);
            }
        }
        catch (Exception)
        {
            // Pas de journal possible (dossier inaccessible) : la seconde instance s'arrête sans rien dire.
        }
    }
}
```

`src/PressePapiers/App.xaml.cs`

```csharp
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PressePapiers.Capture;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Recherche;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Coeur.Session;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Interface;
using PressePapiers.Systeme;

namespace PressePapiers;

/// <summary>
/// Application WinUI 3 : cycle de vie et ordre de démarrage de la spec (section 12). Le journal, la base, la détection de
/// session et la fenêtre principale masquée sont en place ; le relais Windows existe mais n'écoute rien pour
/// l'instant, l'icône et le hook viennent avec l'assemblage (tâche 29).
/// </summary>
public sealed partial class App : Application
{
    private static readonly Lock _verrou = new();
    private static App? _courante;
    private static bool _relancePendante;

    private readonly DispatcherQueue _fil;
    private readonly IDialogueDemarrage _dialogues = new DialoguesWindows();

    private IJournal _journal = JournalNul.Instance;
    private JournalSerilog? _journalFichier;
    private BaseHistorique? _base;
    private RelaisWindows? _relais;
    private FenetrePrincipale? _fenetre;
    private bool _prete;
#if DEBUG
    private DispatcherQueueTimer? _minuterieArret;
#endif

    /// <summary>Charge les ressources XAML, règle l'arrêt explicite et branche les gestionnaires d'exceptions.</summary>
    public App()
    {
        InitializeComponent();

        // Masquer la fenêtre, voire la fermer, ne doit pas terminer l'application : seul Quitter le fait.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
        _fil = DispatcherQueue.GetForCurrentThread();

        // Branchés tout de suite : une exception du démarrage lui-même doit être notée. Avant la création du journal,
        // ils écrivent dans le journal nul.
        UnhandledException += SurExceptionInterface;
        AppDomain.CurrentDomain.UnhandledException += SurExceptionDomaine;
        TaskScheduler.UnobservedTaskException += SurExceptionTache;

        lock (_verrou)
        {
            _courante = this;
        }
    }

    /// <summary>Vrai dès que Quitter a été demandé : la croix de la fenêtre ne doit plus se contenter de la masquer.</summary>
    internal static bool ArretEnCours { get; private set; }

    /// <summary>
    /// Appelé par l'événement <c>Activated</c> de l'instance principale (sur un fil quelconque) quand une seconde instance
    /// a redirigé son activation. L'ouverture de la fenêtre est ramenée sur le fil d'interface ; si l'application
    /// n'a pas fini de démarrer, elle est jouée à la fin du démarrage.
    /// </summary>
    internal static void SignalerActivation()
    {
        App courante;
        lock (_verrou)
        {
            if (_courante is not { _prete: true } prete)
            {
                _relancePendante = true;
                return;
            }

            courante = prete;
        }

        courante.PosterSurInterface(courante.SurRelance, "la relance");
    }

    /// <summary>
    /// Poste une action sur le fil d'interface en notant ses exceptions. Indispensable : une exception qui sort d'un rappel
    /// de <c>DispatcherQueue</c> ou d'une minuterie n'atteint pas <c>UnhandledException</c> mais arrête le processus (code
    /// 0xC000027B) sans rien écrire au journal, ce qui a été constaté à l'essai.
    /// </summary>
    /// <param name="action">Travail à faire sur le fil d'interface.</param>
    /// <param name="contexte">Ce que l'action fait, pour le message du journal (« la relance »).</param>
    internal void PosterSurInterface(Action action, string contexte) =>
        _fil.TryEnqueue(() => ExecuterProtege(action, contexte));

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Demarrer();
        }
        catch (Exception exception)
        {
            _journal.Erreur("Démarrage impossible.", exception);
            _dialogues.Informer(
                $"PressePapiers n'a pas pu démarrer : {exception.Message}{Environment.NewLine}{Environment.NewLine}"
                + "Le détail est dans le dossier journal des données de l'application.");
            Quitter();
        }
    }

    /// <summary>Affiche la fenêtre principale pour une origine donnée (fil d'interface seulement).</summary>
    internal void AfficherFenetre(OrigineOuverture origine)
    {
        if (ArretEnCours)
        {
            return;
        }

        if (_fenetre is null)
        {
            _journal.Avertissement($"Ouverture demandée ({origine}) mais la fenêtre principale n'existe pas.");
            return;
        }

        // Pas de fenêtre d'origine fiable pour une relance : Entrée copiera seulement.
        _fenetre.AfficherPour(0, origine);
    }

    /// <summary>
    /// Chemin Quitter : lève le drapeau, libère ce que le démarrage a créé dans l'ordre inverse de la création, puis
    /// termine l'application. Sans effet la seconde fois.
    /// </summary>
    internal void Quitter()
    {
        if (ArretEnCours)
        {
            return;
        }

        ArretEnCours = true;
        _journal.Info("Arrêt de PressePapiers.");
        try
        {
#if DEBUG
            _minuterieArret?.Stop();
#endif
            _relais?.Dispose();
            _base?.Dispose();
            _journalFichier?.Dispose();
        }
        catch (Exception exception)
        {
            // Une libération qui échoue ne doit pas empêcher de quitter : le processus s'arrête de toute façon.
            _journal.Erreur("Libération incomplète à l'arrêt.", exception);
        }
        finally
        {
            _relais = null;
            _base = null;
            Exit();
        }
    }

    // Ordre de la spec : journal, dossiers, copie de sécurité et ouverture de la base, détection de session et purge,
    // fenêtre principale masquée. Le relais, l'icône et le hook suivent à la tâche 29.
    private void Demarrer()
    {
        var dossiers = DossierDonnees.ParDefaut();
        dossiers.CreerDossiers();
        _journalFichier = new JournalSerilog(dossiers.Journal);
        _journal = _journalFichier;
        _journal.Info($"Démarrage de PressePapiers {typeof(App).Assembly.GetName().Version} "
                      + $"(processus {Environment.ProcessId}, données : {dossiers.Racine}).");

        _base = OuvertureBase.Ouvrir(dossiers, TimeProvider.System, _journal, _dialogues);
        if (_base is null)
        {
            Quitter();
            return;
        }

        var depot = new DepotHistorique(_base, dossiers, TimeProvider.System, _journal);

        // La purge précède tout ce qui peut capturer une copie : jamais d'effacement d'une copie faite après l'ouverture de session.
        var session = DemarrageSession.Appliquer(new SourceSessionWts(), depot, _journal);
        _journal.Info($"Session Windows : {session.Etat}, {session.ElementsPurges} élément(s) de Temporaire purgé(s).");

        CreerFenetrePrincipale(_base, depot, dossiers);
        ProgrammerArretDeRecette();

        bool relance;
        lock (_verrou)
        {
            _prete = true;
            relance = _relancePendante;
            _relancePendante = false;
        }

        _journal.Info("Démarrage terminé, aucune fenêtre affichée.");
        if (relance)
        {
            SurRelance();
        }
    }

    // La fenêtre est créée sans être affichée. Son modèle de vue exige le service de collage, donc le relais (simple fenêtre
    // Win32 invisible, qui n'écoute encore rien) est créé ici ; la tâche 29 y branche l'écoute, l'icône et le hook.
    private void CreerFenetrePrincipale(BaseHistorique baseHistorique, DepotHistorique depot, DossierDonnees dossiers)
    {
        var parametres = new ServiceParametres(dossiers.Parametres, _journal);
        var recherche = new MoteurRecherche(baseHistorique);
        _relais = new RelaisWindows(_journal);
        var recollage = new ServiceRecollage(new EcrivainPressePapiers(_relais.Handle, _journal), depot, _journal);

        try
        {
            _fenetre = new FenetrePrincipale(new FenetrePrincipaleVue(depot, recherche, recollage, parametres, _journal));
        }
        catch (NotImplementedException exception)
        {
            // Provisoire : tant que les tâches 25 à 28 n'ont pas remplacé la coquille, l'application tourne sans fenêtre.
            // À retirer à la tâche 29.
            _journal.Avertissement("La fenêtre principale n'est pas encore écrite (tâches 25 à 28) : démarrage sans fenêtre.", exception);
        }
    }

    // Jouée sur le fil d'interface : une seconde instance a redirigé son activation vers celle-ci.
    private void SurRelance()
    {
        _journal.Info("Activation redirigée reçue d'une seconde instance : ouverture de la fenêtre demandée.");
        AfficherFenetre(OrigineOuverture.Relance);
    }

    // Note l'exception au journal au lieu de la laisser sortir du rappel (qui arrêterait le processus sans trace).
    private void ExecuterProtege(Action action, string contexte)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            _journal.Erreur($"Exception pendant {contexte}.", exception);
        }
    }

    // Recette automatique (versions Debug seulement) : PRESSEPAPIERS_ARRET_APRES_MS demande un arrêt propre, par le chemin
    // Quitter, au bout de ce délai. Sans icône de notification, c'est le seul moyen de vérifier l'arrêt sans tuer le processus.
    private void ProgrammerArretDeRecette()
    {
#if DEBUG
        if (int.TryParse(Environment.GetEnvironmentVariable("PRESSEPAPIERS_ARRET_APRES_MS"), out var delai) && delai > 0)
        {
            _minuterieArret = _fil.CreateTimer();
            _minuterieArret.Interval = TimeSpan.FromMilliseconds(delai);
            _minuterieArret.IsRepeating = false;
            _minuterieArret.Tick += (_, _) => ExecuterProtege(Quitter, "l'arrêt de recette");
            _minuterieArret.Start();
            _journal.Info($"Arrêt de recette programmé dans {delai} ms.");
        }
#endif
    }

    // L'outil continue chaque fois que c'est possible (spec, section 13) : l'exception est notée puis déclarée traitée.
    private void SurExceptionInterface(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        _journal.Erreur("Exception non gérée de l'interface.", e.Exception);
    }

    // Celle-ci arrive en dernier recours : le processus s'arrête, il ne reste qu'à la noter.
    private void SurExceptionDomaine(object? sender, System.UnhandledExceptionEventArgs e) =>
        _journal.Erreur($"Exception non gérée du processus (arrêt en cours : {e.IsTerminating}).", e.ExceptionObject as Exception);

    private void SurExceptionTache(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        _journal.Erreur("Exception de tâche non observée.", e.Exception);
    }
}
```

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet build PressePapiers.slnx -c Release
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)` et `0 Erreur(s)` aux deux compilations (la compilation Release vérifie que le code sous
`#if DEBUG` ne laisse ni champ inutilisé ni avertissement), puis tous les tests verts : 513 réussis, 0 échec,
0 ignoré (les 504 des tâches 1 à 15, plus les 9 de cette tâche).

- [ ] **Étape 5 : Vérifier sur le poste**

Quatre vérifications : 5a le démarrage réel de l'application, 5b la lecture de `LogonTime` (point 1 de la section 16 de la spec),
5c l'affichage d'une fenêtre WinUI 3 jamais activée (point 2), 5d les exceptions non gérées. Aucune ne touche la vraie clé Run, les
vraies données ni un réglage Windows ; elles ne laissent aucun processus lancé. Les trois programmes sont hors du dépôt.

```bash
# Depuis la racine du dépôt, après l'étape 4 (compilation Debug faite).
DEPOT="$PWD"
ESSAIS="${TEMP}/essais-demarrage"
ESSAIS_FENETRE="${TEMP}/essais-fenetre"
mkdir -p "$ESSAIS/Session" "$ESSAIS_FENETRE"
```

**5a. Démarrage réel de l'application (script PowerShell 7)**

Le script lance `PressePapiers.exe` (Debug) avec `PRESSEPAPIERS_DONNEES` pointant vers un dossier jetable et un `parametres.json`
dont `DemarrageAvecWindows` vaut `false`, vérifie l'absence de fenêtre visible, le journal, la base, la redirection du second
lancement, l'arrêt propre (programmé par `PRESSEPAPIERS_ARRET_APRES_MS`), la relance avec copie de sécurité et reconnaissance de la
même session, puis simule une base illisible : il clique lui-même sur les boîtes de message (Non, Oui, OK) pour contrôler le refus,
l'acceptation de la restauration et le cas sans copie.

`$ESSAIS/verifier-demarrage.ps1`

```powershell
#requires -Version 7
<#
  Vérification du démarrage de PressePapiers sur le poste (tâche 16). Lance l'application compilée en Debug avec un dossier
  de données jetable (PRESSEPAPIERS_DONNEES) et un parametres.json dont DemarrageAvecWindows vaut false ; ne touche ni aux
  vraies données ni à la clé Run. Contrôle : démarrage sans fenêtre visible, journal et base créés, second lancement
  redirigé puis arrêté, arrêt propre, relance avec copie de sécurité et même session.
  Usage : pwsh -File verifier-demarrage.ps1 -Depot <racine du dépôt>
#>
param([Parameter(Mandatory)][string]$Depot)

$ErrorActionPreference = 'Stop'
$exe = Join-Path $Depot 'src/PressePapiers/bin/Debug/net10.0-windows10.0.19041.0/win-x64/PressePapiers.exe'
if (-not (Test-Path $exe)) { throw "Exécutable introuvable : $exe (compiler d'abord en Debug)" }

# Fenêtres de premier niveau visibles d'un processus.
Add-Type -Namespace Essai -Name Fenetres -MemberDefinition @'
public delegate bool Rappel(System.IntPtr hwnd, System.IntPtr param);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool EnumWindows(Rappel r, System.IntPtr p);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool IsWindowVisible(System.IntPtr h);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(System.IntPtr h, out uint pid);
[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] public static extern System.IntPtr FindWindow(string classe, string titre);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern System.IntPtr GetDlgItem(System.IntPtr h, int id);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint message, System.IntPtr w, System.IntPtr l);
public static bool Cliquer(int id) {
  System.IntPtr boite = FindWindow("#32770", "PressePapiers");
  if (boite == System.IntPtr.Zero) return false;
  System.IntPtr bouton = GetDlgItem(boite, id);
  if (bouton == System.IntPtr.Zero && id == 1) bouton = GetDlgItem(boite, 2);   // un seul bouton OK porte l'identifiant IDCANCEL
  return bouton != System.IntPtr.Zero && PostMessage(bouton, 0x00F5, System.IntPtr.Zero, System.IntPtr.Zero);   // BM_CLICK
}
public static int Visibles(uint pid) {
  int n = 0;
  EnumWindows((h, p) => { uint q; GetWindowThreadProcessId(h, out q); if (q == pid && IsWindowVisible(h)) n++; return true; }, System.IntPtr.Zero);
  return n;
}
'@

$echecs = 0
function Controle([string]$nom, [bool]$ok, [string]$detail = '') {
    if ($ok) { Write-Host "OK     $nom" } else { Write-Host "ECHEC  $nom $detail"; $script:echecs++ }
}

$donnees = Join-Path ([IO.Path]::GetTempPath()) ('pp-demarrage-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $donnees | Out-Null
Set-Content (Join-Path $donnees 'parametres.json') '{ "DemarrageAvecWindows": false }' -Encoding utf8NoBOM

$cleRun = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runAvant = (Get-ItemProperty $cleRun -ErrorAction SilentlyContinue).PressePapiers

$env:PRESSEPAPIERS_DONNEES = $donnees
$env:PRESSEPAPIERS_ARRET_APRES_MS = '15000'   # arrêt propre programmé (versions Debug)

function LireJournal {
    $fichier = Get-ChildItem (Join-Path $donnees 'journal') -Filter 'journal-*.log' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $fichier) { return '' }
    $flux = [IO.File]::Open($fichier.FullName, 'Open', 'Read', 'ReadWrite')
    try { return (New-Object IO.StreamReader($flux, [Text.Encoding]::UTF8)).ReadToEnd() } finally { $flux.Dispose() }
}

function Attendre([scriptblock]$condition, [int]$secondes) {
    $fin = (Get-Date).AddSeconds($secondes)
    while ((Get-Date) -lt $fin) { if (& $condition) { return $true }; Start-Sleep -Milliseconds 200 }
    return $false
}

$premiere = $null
try {
    # 1. Premier lancement : démarre sans fenêtre visible, crée journal et base.
    $premiere = Start-Process $exe -PassThru
    $demarre = Attendre { (LireJournal) -match 'Démarrage terminé' } 30
    Controle 'premier lancement : démarrage terminé au journal' $demarre
    Start-Sleep -Seconds 1
    $premiere.Refresh()
    Controle 'premier lancement : processus vivant' (-not $premiere.HasExited)
    Controle 'premier lancement : aucune fenêtre visible' ([Essai.Fenetres]::Visibles([uint32]$premiere.Id) -eq 0) ("fenêtres visibles : " + [Essai.Fenetres]::Visibles([uint32]$premiere.Id))
    Controle 'base historique.db créée' (Test-Path (Join-Path $donnees 'historique.db'))
    Controle 'dossiers images, sauvegardes, journal créés' ((Test-Path (Join-Path $donnees 'images')) -and (Test-Path (Join-Path $donnees 'sauvegardes')) -and (Test-Path (Join-Path $donnees 'journal')))
    Controle 'aucune copie de sécurité au tout premier lancement (pas encore de base)' ((Get-ChildItem (Join-Path $donnees 'sauvegardes')).Count -eq 0)
    $j = LireJournal
    Controle 'journal : ligne de démarrage avec le processus' ($j -match "Démarrage de PressePapiers .* \(processus $($premiere.Id),")
    Controle 'journal : premier lancement de session' ($j -match 'Session Windows : PremierLancement')

    # 2. Second lancement : redirigé vers la première instance, puis sortie.
    $seconde = Start-Process $exe -PassThru
    $sortie = $seconde.WaitForExit(30000)
    Controle 'second lancement : sortie en moins de 30 s' $sortie
    if ($sortie) { Controle 'second lancement : code de sortie 0' ($seconde.ExitCode -eq 0) "code $($seconde.ExitCode)" }
    $recu = Attendre { (LireJournal) -match 'Activation redirigée reçue' } 10
    $j = LireJournal
    Controle 'journal : activation redirigée reçue par la première instance' $recu
    Controle 'journal : trace de la seconde instance' ($j -match "Seconde instance \(processus $($seconde.Id)\) : activation redirigée")
    $premiere.Refresh()
    Controle 'première instance toujours vivante' (-not $premiere.HasExited)
    Controle 'toujours aucune fenêtre visible après la relance' ([Essai.Fenetres]::Visibles([uint32]$premiere.Id) -eq 0)
    Controle 'un seul processus PressePapiers' ((Get-Process PressePapiers -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }).Count -eq 1)

    # 3. Arrêt propre par le chemin Quitter (programmé par la variable d'environnement).
    $arret = $premiere.WaitForExit(60000)
    Controle 'arrêt propre : le processus se termine seul' $arret
    if ($arret) { Controle 'arrêt propre : code de sortie 0' ($premiere.ExitCode -eq 0) "code $($premiere.ExitCode)" }
    $j = LireJournal
    Controle 'journal : arrêt noté' ($j -match 'Arrêt de PressePapiers')

    # 4. Relance sur les mêmes données : copie de sécurité faite, même session, repère inchangé.
    $env:PRESSEPAPIERS_ARRET_APRES_MS = '3000'
    $troisieme = Start-Process $exe -PassThru
    $fini = $troisieme.WaitForExit(40000)
    Controle 'relance : arrêt propre' ($fini -and $troisieme.ExitCode -eq 0)
    $copies = @(Get-ChildItem (Join-Path $donnees 'sauvegardes') -Filter 'historique-*.db')
    Controle 'relance : une copie de sécurité créée' ($copies.Count -eq 1) "copies : $($copies.Count)"
    Controle 'relance : même session reconnue' ((LireJournal) -match 'Session Windows : MemeSession')

    # 5. Base illisible avec une copie de sécurité : refus puis acceptation de la restauration (boîtes de message natives).
    $base = Join-Path $donnees 'historique.db'
    $abime = 'du texte dans le fichier d''historique, pas une base SQLite'
    [IO.File]::WriteAllText($base, $abime)
    $refus = Start-Process $exe -PassThru
    $boite = Attendre { [Essai.Fenetres]::Cliquer(7) } 30   # IDNO
    Controle 'base illisible : la question de restauration s''affiche (clic sur Non)' $boite
    Controle 'refus : le processus se termine, code 0' ($refus.WaitForExit(30000) -and $refus.ExitCode -eq 0)
    Controle 'refus : base laissée telle quelle' ([IO.File]::ReadAllText($base) -eq $abime)
    Controle 'refus noté au journal' ((LireJournal) -match 'Restauration refusée')

    $acceptation = Start-Process $exe -PassThru
    $boite = Attendre { [Essai.Fenetres]::Cliquer(6) } 30   # IDYES
    Controle 'base illisible : la question s''affiche de nouveau (clic sur Oui)' $boite
    Controle 'acceptation : démarrage terminé après restauration' (Attendre { (LireJournal) -match 'Base restaurée depuis la copie de sécurité historique-' } 30)
    Controle 'acceptation : processus vivant, arrêt propre ensuite' ($acceptation.WaitForExit(40000) -and $acceptation.ExitCode -eq 0)
    Controle 'acceptation : base restaurée lisible (en-tête SQLite)' (([Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($base)[0..14])) -eq 'SQLite format 3')
    Controle 'acceptation : fichier abîmé gardé dans sauvegardes' (@(Get-ChildItem (Join-Path $donnees 'sauvegardes') -Filter 'illisible-*.db').Count -eq 1)

    # 6. Base illisible sans aucune copie : message, pas de question, arrêt, base non touchée.
    Get-ChildItem (Join-Path $donnees 'sauvegardes') | Remove-Item -Force
    [IO.File]::WriteAllText($base, $abime)
    $sansCopie = Start-Process $exe -PassThru
    $boite = Attendre { [Essai.Fenetres]::Cliquer(1) } 30   # OK
    Controle 'sans copie : le message s''affiche (clic sur OK)' $boite
    Controle 'sans copie : le processus se termine, code 0' ($sansCopie.WaitForExit(30000) -and $sansCopie.ExitCode -eq 0)
    Controle 'sans copie : base laissée telle quelle' ([IO.File]::ReadAllText($base) -eq $abime)
}
finally {
    foreach ($p in @($premiere, $seconde, $troisieme, $refus, $acceptation, $sansCopie)) { if ($p -and -not $p.HasExited) { $p.Kill() } }
    $runApres = (Get-ItemProperty $cleRun -ErrorAction SilentlyContinue).PressePapiers
    Controle 'clé Run de l''utilisateur inchangée' ($runAvant -eq $runApres)
    Write-Host "Journal complet :"
    Write-Host (LireJournal)
    Remove-Item $donnees -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item Env:PRESSEPAPIERS_DONNEES, Env:PRESSEPAPIERS_ARRET_APRES_MS -ErrorAction SilentlyContinue
}

if ($echecs -gt 0) { Write-Host "$echecs contrôle(s) en échec"; exit 1 }
Write-Host 'Tous les contrôles sont réussis.'
```

```bash
pwsh -NoProfile -File "$ESSAIS/verifier-demarrage.ps1" -Depot "$DEPOT"
```

Résultat attendu, en une à deux minutes : 34 lignes `OK`, aucune `ECHEC`, le journal complet affiché, puis `Tous les contrôles sont
réussis.` Ce que cela prouve :
- l'application démarre sans aucune fenêtre visible (le relais est une fenêtre de premier niveau jamais affichée), crée
  `historique.db`, `images\`, `sauvegardes\`, `journal\`, et note au journal le démarrage, le repère de session et « Démarrage terminé,
  aucune fenêtre affichée » ; au tout premier lancement il n'y a pas encore de base à copier (aucune sauvegarde) ;
- un second lancement est redirigé vers la première instance (`Activation redirigée reçue d'une seconde instance`, côté première
  instance) et se termine seul avec le code 0 en moins de deux secondes, en laissant sa propre trace (`Seconde instance (processus n) :
  activation redirigée vers l'instance principale, arrêt`) ; il ne reste qu'un processus ; tant que la fenêtre est une coquille, la
  relance note l'avertissement « la fenêtre principale n'existe pas » ;
- l'arrêt par `Quitter()` est propre : le processus se termine seul avec le code 0 et le journal note « Arrêt de PressePapiers » ;
- la relance sur les mêmes données crée une copie de sécurité avant l'ouverture et reconnaît la même session (`MemeSession`) ;
- une base illisible avec une copie : la question s'affiche ; Non laisse tout en place et l'application s'arrête ; Oui restaure
  (en-tête `SQLite format 3`), démarre, et garde le fichier abîmé dans `sauvegardes\illisible-*.db` ; sans aucune copie, un message
  s'affiche, l'application s'arrête et la base n'est pas touchée ;
- la clé `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` de l'utilisateur est identique avant et après.

**5b. Point 1 : `LogonTime` (WTSQuerySessionInformationW)**

Le programme contrôle la disposition de `WTSINFOW` (216 octets, `LogonTime` à l'offset 200), lit `LogonTime` deux fois dans le
processus puis dans un processus neuf, vérifie `DemarrageWindows` sous la clé d'essai `HKCU\Software\PressePapiers.Essais` (activer,
valeur entre guillemets de type `REG_SZ`, remplacer, retirer, retirer deux fois, chemin vide refusé, vraie clé Run inchangée, clé
d'essai supprimée), `JournalSerilog` (fichier du jour, accolades et niveaux, exception avec sa pile, 14 fichiers gardés sur 20,
deux journaux ouverts sur le même fichier, appels ignorés après `Dispose`) et `DialoguesWindows` (les trois boîtes de message
sont affichées puis cliquées par le programme, le bouton par défaut de la question est Non).

`$ESSAIS/Session/EssaiSession.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Essai jetable hors dépôt : compile les briques du démarrage du clone (session WTS, clé Run, journal, dialogues). -->
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
    <Compile Include="$(Clone)/PressePapiers/Systeme/SourceSessionWts.cs" />
    <Compile Include="$(Clone)/PressePapiers/Systeme/DemarrageWindows.cs" />
    <Compile Include="$(Clone)/PressePapiers/Systeme/JournalSerilog.cs" />
    <Compile Include="$(Clone)/PressePapiers/Systeme/DialoguesWindows.cs" />
    <Compile Include="$(Clone)/PressePapiers/Natif/NatifSession.cs" />
    <Compile Include="$(Clone)/PressePapiers/Natif/NatifDemarrage.cs" />
    <ProjectReference Include="$(Clone)/PressePapiers.Coeur/PressePapiers.Coeur.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- Hors dépôt, donc hors gestion centralisée : mêmes versions que Directory.Packages.props. -->
    <PackageReference Include="Serilog" Version="4.4.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="7.0.0" />
  </ItemGroup>

</Project>
```

`$ESSAIS/Session/Program.cs`

```csharp
// Essai jetable de la tâche 16 : lecture de la session WTS (point 1 de la spec, section 16), clé Run sous une clé d'essai,
// journal Serilog (fichier du jour, accolades, rotation, arrêt) et boîtes de message natives du démarrage.
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using PressePapiers.Natif;
using PressePapiers.Systeme;

internal static class Essai
{
    private static int _echecs;

    private static void Controle(string nom, bool ok, string detail = "")
    {
        Console.WriteLine($"{(ok ? "OK    " : "ECHEC ")} {nom} {(ok ? "" : detail)}".TrimEnd());
        if (!ok) _echecs++;
    }

    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (args.Length > 0 && args[0] == "logon")
        {
            // Mode appelé par l'essai lui-même, dans un processus neuf : n'affiche que la valeur brute.
            Console.WriteLine(new SourceSessionWts().LireHeureOuverture()?.ToString(CultureInfo.InvariantCulture) ?? "null");
            return 0;
        }

        Session();
        CleRun();
        Journal();
        Dialogues();

        Console.WriteLine(_echecs == 0 ? "Tous les contrôles sont réussis." : $"{_echecs} contrôle(s) en échec.");
        return _echecs == 0 ? 0 : 1;
    }

    // ---- Point 1 : LogonTime ----
    private static void Session()
    {
        Controle("WTSINFOW fait 216 octets", Marshal.SizeOf<NatifSession.InfoSession>() == NatifSession.TailleInfoSession,
                 $"taille {Marshal.SizeOf<NatifSession.InfoSession>()}");
        Controle("LogonTime est à l'offset 200", Marshal.OffsetOf<NatifSession.InfoSession>(nameof(NatifSession.InfoSession.OuvertureSession)) == 200);

        var source = new SourceSessionWts();
        var premiere = source.LireHeureOuverture();
        var seconde = source.LireHeureOuverture();
        Controle("lecture non nulle", premiere is > 0, $"valeur {premiere}");
        Controle("deux lectures identiques (même processus)", premiere == seconde, $"{premiere} contre {seconde}");

        // Un processus neuf, lancé par celui-ci : même valeur, donc le repère ne dépend pas du processus.
        var neuf = Lancer("logon");
        Controle("même valeur dans un processus neuf", neuf == premiere?.ToString(CultureInfo.InvariantCulture), $"{neuf} contre {premiere}");

        if (premiere is > 0)
        {
            var heure = DateTime.FromFileTimeUtc(premiere.Value).ToLocalTime();
            Console.WriteLine($"INFO   LogonTime brut {premiere} = {heure:yyyy-MM-dd HH:mm:ss.fffffff} (heure locale)");
        }
    }

    private static string Lancer(string argument)
    {
        var chemin = Environment.ProcessPath!;
        var info = new System.Diagnostics.ProcessStartInfo(chemin, argument) { RedirectStandardOutput = true, UseShellExecute = false };
        using var processus = System.Diagnostics.Process.Start(info)!;
        var sortie = processus.StandardOutput.ReadToEnd().Trim();
        processus.WaitForExit();
        return sortie;
    }

    // ---- Clé Run, sous une clé d'essai ----
    private static void CleRun()
    {
        const string cle = @"Software\PressePapiers.Essais";
        var vraie = (Registry.CurrentUser.OpenSubKey(DemarrageWindows.CleRun)?.GetValue("PressePapiers")) as string;
        Registry.CurrentUser.DeleteSubKeyTree(cle, throwOnMissingSubKey: false);
        try
        {
            Controle("clé d'essai absente : non activé", !DemarrageWindows.EstActive(cle));
            DemarrageWindows.Appliquer(false, "", cle);   // retirer ce qui n'existe pas : sans erreur
            Controle("retrait sans clé : aucune exception", true);

            const string exe = @"C:\Program Files\Essai Espaces\PressePapiers.exe";
            DemarrageWindows.Appliquer(true, exe, cle);
            Controle("activé après Appliquer(true)", DemarrageWindows.EstActive(cle));
            using (var ouverte = Registry.CurrentUser.OpenSubKey(cle))
            {
                Controle("valeur entre guillemets", ouverte?.GetValue("PressePapiers") as string == $"\"{exe}\"");
                Controle("type REG_SZ", ouverte?.GetValueKind("PressePapiers") == RegistryValueKind.String);
            }

            DemarrageWindows.Appliquer(true, exe + ".autre", cle);
            using (var ouverte = Registry.CurrentUser.OpenSubKey(cle))
            {
                Controle("seconde activation : valeur remplacée", ouverte?.GetValue("PressePapiers") as string == $"\"{exe}.autre\"");
            }

            DemarrageWindows.Appliquer(false, "", cle);
            Controle("désactivé après Appliquer(false)", !DemarrageWindows.EstActive(cle));
            DemarrageWindows.Appliquer(false, "", cle);
            Controle("second retrait : aucune exception", true);

            var refus = Assert(() => DemarrageWindows.Appliquer(true, " ", cle));
            Controle("activer sans chemin : ArgumentException", refus is ArgumentException, refus?.GetType().Name ?? "aucune");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(cle, throwOnMissingSubKey: false);
        }

        var apres = (Registry.CurrentUser.OpenSubKey(DemarrageWindows.CleRun)?.GetValue("PressePapiers")) as string;
        Controle("vraie clé Run inchangée", vraie == apres);
        Controle("clé d'essai supprimée", Registry.CurrentUser.OpenSubKey(cle) is null);
    }

    private static Exception? Assert(Action action)
    {
        try { action(); return null; } catch (Exception e) { return e; }
    }

    // ---- Journal Serilog ----
    private static void Journal()
    {
        var dossier = Path.Combine(Path.GetTempPath(), "pp-essai-journal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        try
        {
            // Vingt fichiers de jours anciens : la rétention en garde 14, celui du jour compris.
            for (var i = 1; i <= 20; i++) File.WriteAllText(Path.Combine(dossier, $"journal-202001{i:00}.log"), "ancien");

            var journal = new JournalSerilog(dossier);
            journal.Info("Message avec {accolades} et {0} et {Texte}");
            journal.Avertissement("Avertissement avec exception", new InvalidOperationException("cause de l'avertissement"));
            journal.Erreur("Erreur sans exception");
            journal.Dispose();
            journal.Dispose();
            Controle("double Dispose : aucune exception", true);
            journal.Info("tardif");
            journal.Erreur("tardif aussi", new Exception("x"));
            Controle("écriture après Dispose : ignorée sans exception", true);

            var aujourdhui = Path.Combine(dossier, $"journal-{DateTime.Now:yyyyMMdd}.log");
            Controle("fichier du jour journal-AAAAMMJJ.log", File.Exists(aujourdhui));
            var texte = File.Exists(aujourdhui) ? File.ReadAllText(aujourdhui) : "";
            Controle("accolades recopiées telles quelles", texte.Contains("Message avec {accolades} et {0} et {Texte}"));
            Controle("niveaux INF, WRN, ERR", texte.Contains("[INF]") && texte.Contains("[WRN]") && texte.Contains("[ERR]"));
            Controle("exception avec sa pile", texte.Contains("InvalidOperationException: cause de l'avertissement"));
            Controle("message tardif absent", !texte.Contains("tardif"));
            Controle("pas de marque d'ordre des octets", File.Exists(aujourdhui) && File.ReadAllBytes(aujourdhui)[0] != 0xEF);
            var fichiers = Directory.GetFiles(dossier, "journal-*.log").Select(Path.GetFileName).Order().ToArray();
            Controle("14 fichiers gardés", fichiers.Length == 14, $"{fichiers.Length} : {string.Join(", ", fichiers)}");
            Controle("les plus anciens sont supprimés", !fichiers.Contains("journal-20200101.log") && fichiers.Contains("journal-20200120.log"));

            // Deux instances qui écrivent dans le même fichier (le second lancement et le premier).
            var a = new JournalSerilog(dossier);
            var b = new JournalSerilog(dossier);
            a.Info("ecrit par A");
            b.Info("ecrit par B");
            a.Dispose();
            b.Dispose();
            var partage = File.ReadAllText(aujourdhui);
            Controle("deux journaux ouverts sur le même fichier", partage.Contains("ecrit par A") && partage.Contains("ecrit par B"));
        }
        finally
        {
            Directory.Delete(dossier, recursive: true);
        }
    }

    // ---- Boîtes de message natives ----
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string classe, string titre);
    [DllImport("user32.dll")] private static extern nint GetDlgItem(nint dialogue, int id);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd, uint message, nint w, nint l);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint hwnd, uint message, nint w, nint l);

    private static void Dialogues()
    {
        var dialogues = new DialoguesWindows();
        foreach (var (bouton, attendu, nom) in new[] { (7, false, "Non"), (6, true, "Oui") })
        {
            var tache = Task.Run(() => dialogues.Confirmer("Question d'essai de la tâche 16 : cliquer sur " + nom));
            var boite = Attendre();
            Controle($"boîte Confirmer affichée ({nom})", boite != 0);
            if (boite == 0) continue;
            var defaut = (int)SendMessage(boite, 0x400, 0, 0);   // DM_GETDEFID : 0x534B dans le mot haut si un défaut existe
            if (nom == "Non") Controle("bouton par défaut : Non", (defaut >> 16) == 0x534B && (defaut & 0xFFFF) == 7, $"0x{defaut:X}");
            PostMessage(GetDlgItem(boite, bouton), 0x00F5, 0, 0);   // BM_CLICK
            Controle($"Confirmer renvoie {attendu.ToString().ToLowerInvariant()} après « {nom} »", tache.Wait(5000) && tache.Result == attendu);
        }

        var information = Task.Run(() => dialogues.Informer("Message d'essai de la tâche 16"));
        var boiteInfo = Attendre();
        Controle("boîte Informer affichée", boiteInfo != 0);
        if (boiteInfo != 0)
        {
            // Avec le seul bouton OK, Windows lui donne l'identifiant IDCANCEL (2) pour que Échap fonctionne ; sinon IDOK (1).
            var ok = GetDlgItem(boiteInfo, 2) != 0 ? GetDlgItem(boiteInfo, 2) : GetDlgItem(boiteInfo, 1);
            PostMessage(ok, 0x00F5, 0, 0);   // BM_CLICK
        }
        Controle("Informer rend la main après OK", information.Wait(5000));
    }

    private static nint Attendre()
    {
        for (var i = 0; i < 50; i++)
        {
            var boite = FindWindow("#32770", "PressePapiers");
            if (boite != 0) return boite;
            Thread.Sleep(100);
        }

        return 0;
    }
}
```

```bash
cd "$ESSAIS/Session" && dotnet run -c Debug -p:Depot="$DEPOT"
```

Résultat attendu : 34 lignes `OK`, une ligne `INFO   LogonTime brut ... (heure locale)`, aucune `ECHEC`, puis `Tous les contrôles
sont réussis.`

Comparer ensuite avec l'heure d'ouverture de la session donnée par Windows (la valeur brute de la ligne `INFO` se convertit en
date locale) :

```powershell
# Dans PowerShell 7.
Get-CimInstance Win32_LogonSession | Where-Object { $_.LogonType -in 2,10,11 } | Sort-Object StartTime |
    Select-Object LogonId, LogonType, @{n='Debut';e={$_.StartTime.ToString('yyyy-MM-dd HH:mm:ss.fffffff')}}
quser
```

Résultat observé sur le poste de rédaction : `LogonTime` = 2026-10-13 08:36:06,9754884 (heure locale) dans les deux processus
(même valeur brute `134363469669754884`) ; `Win32_LogonSession` donne deux sessions de connexion interactives (type 2) de même
début, 08:36:06,8335680 (la session normale et sa jumelle élevée par le contrôle de compte d'utilisateur), et `quser` donne
« 13/10/2026 08:36 ». `LogonTime` suit donc de 142 ms le début de la session de connexion : même seconde, une seule valeur pour
les deux jumelles. **Le cas d'un processus élevé n'est pas testé** (il demande une validation UAC) : à faire à la recette,
en lançant `dotnet run -c Debug --no-build -p:Depot="$DEPOT" -- logon` depuis un terminal ouvert « en tant qu'administrateur » ; la
valeur brute imprimée doit être identique à celle d'un terminal normal. Les autres cas du point 1 (inchangé après Win+L, veille et
reprise d'une connexion Bureau à distance ; changé après « Arrêter » en démarrage rapide puis rallumage) se jouent à la recette
avec le même programme : noter la valeur avant, refaire l'action, comparer.

**5c. Point 2 : fenêtre WinUI 3 jamais activée, affichée par `AppWindow.Show` puis `PremierPlan.Amener`**

Un projet WinUI 3 jetable crée une fenêtre (Mica, barre de titre fondue, 920 x 560, texte en magenta pur) qui n'est **jamais**
activée, puis la montre deux fois de suite pendant qu'un Bloc-notes est au premier plan. Le Bloc-notes est lancé par le script, pas
par l'essai, et le script simule juste avant une frappe sans effet (touche virtuelle `0xE8`) : l'essai est dans la situation de
PressePapiers, un processus d'arrière-plan qui n'a ni le premier plan ni la dernière entrée. Un fil à part relève toutes les
millisecondes si la fenêtre est visible, masquée par DWM, au premier plan, et, par capture de l'écran, si son contenu (le texte
magenta) est réellement affiché. Modes : `directe` (`Show()` puis `Amener`), `showfalse` (`Show(false)` puis `Amener`), `showseul`
(`Show()` sans `Amener`), `prechauffe` (au démarrage `Show(false)` puis `Hide()` aussitôt), `prechauffe300` (idem avec 300 ms
entre les deux), `activee` (au démarrage `Activate()` puis `Hide()`).

`$ESSAIS_FENETRE/EssaiFenetre.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Essai jetable hors dépôt : une fenêtre WinUI 3 jamais activée, affichée par AppWindow.Show puis PremierPlan.Amener. -->
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <RootNamespace>EssaiFenetre</RootNamespace>
    <AssemblyName>EssaiFenetre</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Platforms>x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <UseWinUI>true</UseWinUI>
    <EnableMsixTooling>true</EnableMsixTooling>
    <WindowsPackageType>None</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <SelfContained>true</SelfContained>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <PropertyGroup>
    <!-- Racine du dépôt, passée par -p:Depot=... -->
    <Clone>$(Depot)/src</Clone>
  </PropertyGroup>

  <ItemGroup>
    <!-- La vraie brique Premier plan du clone, avec ses appels natifs. -->
    <Compile Include="$(Clone)/PressePapiers/Systeme/PremierPlan.cs" />
    <Compile Include="$(Clone)/PressePapiers/Natif/NatifPremierPlan.cs" />
  </ItemGroup>

  <ItemGroup>
    <!-- Hors dépôt, donc hors gestion centralisée : mêmes versions que Directory.Packages.props. -->
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="2.5.1" />
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.28000.2705" />
  </ItemGroup>

</Project>
```

`$ESSAIS_FENETRE/app.manifest`

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="EssaiFenetre.app"/>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <!-- Windows 10 et 11. -->
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

`$ESSAIS_FENETRE/App.xaml`

```xml
<Application
    x:Class="EssaiFenetre.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

`$ESSAIS_FENETRE/App.xaml.cs`

```csharp
using Microsoft.UI.Xaml;

namespace EssaiFenetre;

/// <summary>Application d'essai : exécute un scénario (argument de la ligne de commande) puis se ferme.</summary>
public partial class App : Application
{
    /// <summary>Charge les ressources XAML ; l'arrêt est explicite, comme dans PressePapiers.</summary>
    public App()
    {
        InitializeComponent();
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
    }

    /// <inheritdoc />
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Arguments : mode puis fichier de sortie. Le mode est nu : « directe », « showfalse », « prechauffe », « activee », « showseul ».
        var ligne = Environment.GetCommandLineArgs();
        var mode = ligne.Length > 1 ? ligne[1] : "directe";
        var sortie = ligne.Length > 2 ? ligne[2] : Path.Combine(Path.GetTempPath(), "essai-fenetre-" + mode + ".txt");
        try
        {
            await Scenario.ExecuterAsync(mode, sortie);
        }
        catch (Exception exception)
        {
            File.AppendAllText(sortie, "EXCEPTION " + exception + Environment.NewLine);
        }
        finally
        {
            Exit();
        }
    }
}
```

`$ESSAIS_FENETRE/Scenario.cs`

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PressePapiers.Systeme;
using Windows.Graphics;
using WinRT.Interop;

namespace EssaiFenetre;

/// <summary>
/// Scénarios du point 2 de la spec (section 16) : une fenêtre WinUI 3 jamais activée est affichée par AppWindow.Show puis
/// PremierPlan.Amener, une autre application (le Bloc-notes, lancé par le script de l'essai et non par ce processus) étant
/// au premier plan. Chaque scénario joue deux affichages de suite (le premier, puis un second après masquage) et note la
/// chronologie de la fenêtre. Le script donne le départ de chaque affichage en créant un fichier <c>sortie.goN</c>.
/// </summary>
internal static class Scenario
{
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hwnd, StringBuilder texte, int max);

    private static readonly StringBuilder Journal = new();
    private static string _sortie = "";

    private static void Noter(string ligne)
    {
        Journal.AppendLine(ligne);
        File.WriteAllText(_sortie, Journal.ToString(), Encoding.UTF8);
    }

    private static string Decrire(nint hwnd)
    {
        if (hwnd == 0) return "(aucune)";
        var titre = new StringBuilder(128);
        GetWindowText(hwnd, titre, titre.Capacity);
        GetWindowThreadProcessId(hwnd, out var pid);
        string nom;
        try { nom = Process.GetProcessById((int)pid).ProcessName; } catch { nom = "?"; }
        return $"0x{hwnd:X} \"{titre}\" ({nom})";
    }

    public static async Task ExecuterAsync(string mode, string sortie)
    {
        _sortie = sortie;
        Noter($"=== mode « {mode} » ===");

        // Fenêtre proche de la vraie : Mica, barre de titre fondue, taille réglée. Elle n'est jamais activée ici.
        // Le texte est en magenta pur : l'échantillonneur le cherche à l'écran pour savoir si le contenu est affiché.
        var fenetre = new Window
        {
            Title = "EssaiFenetre",
            ExtendsContentIntoTitleBar = true,
            SystemBackdrop = new MicaBackdrop(),
            Content = new Grid
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "PressePapiers",
                        FontSize = 64,
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 255)),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    },
                },
            },
        };
        fenetre.AppWindow.Resize(new SizeInt32(920, 560));
        var hwnd = WindowNative.GetWindowHandle(fenetre);
        Noter($"fenêtre créée : hwnd 0x{hwnd:X}, visible = {IsWindowVisible(hwnd)}, jamais activée");

        try
        {
            await DemarrageAsync(mode, fenetre, hwnd);
            File.WriteAllText(_sortie + ".pret", "pret");
            await AffichageAsync(1, mode, fenetre, hwnd);
            await AffichageAsync(2, mode, fenetre, hwnd);
        }
        finally
        {
            fenetre.AppWindow.Hide();
            fenetre.Close();
        }
    }

    // Ce que fait l'application au démarrage pour « préparer » la fenêtre, selon le mode.
    private static async Task DemarrageAsync(string mode, Window fenetre, nint hwnd)
    {
        if (mode is not ("prechauffe" or "prechauffe300" or "activee")) return;

        var echantillonneur = new Echantillonneur(hwnd);
        echantillonneur.Demarrer();
        switch (mode)
        {
            case "prechauffe":
                fenetre.AppWindow.Show(false);
                fenetre.AppWindow.Hide();
                break;
            case "prechauffe300":
                fenetre.AppWindow.Show(false);
                await Task.Delay(300);
                fenetre.AppWindow.Hide();
                break;
            case "activee":
                fenetre.Activate();
                fenetre.AppWindow.Hide();
                break;
        }

        await Task.Delay(500);
        Noter($"démarrage ({mode}) : chronologie de la fenêtre");
        Noter(echantillonneur.Arreter());
        await Task.Delay(500);
        Noter($"après le démarrage : visible = {IsWindowVisible(hwnd)}");
    }

    private static async Task AffichageAsync(int numero, string mode, Window fenetre, nint hwnd)
    {
        var nom = $"affichage {numero}";

        // Le script lance le Bloc-notes, attend qu'il soit au premier plan, simule une frappe sans effet puis donne le départ.
        while (!File.Exists(_sortie + ".go" + numero)) await Task.Delay(50);
        Noter($"[{nom}] avant : premier plan = {Decrire(GetForegroundWindow())}");

        var echantillonneur = new Echantillonneur(hwnd);
        echantillonneur.Demarrer();
        var chrono = Stopwatch.StartNew();

        // « showfalse » affiche sans activer ; tous les autres modes affichent avec activation. « showseul » n'appelle pas
        // PremierPlan.Amener, pour voir si Windows donne le premier plan tout seul.
        if (mode == "showfalse") fenetre.AppWindow.Show(false); else fenetre.AppWindow.Show();
        var apresShow = chrono.Elapsed.TotalMilliseconds;
        var etape = "non appelé";
        if (mode != "showseul") etape = PremierPlan.AmenerParEtapes(hwnd).ToString();
        var apresAmener = chrono.Elapsed.TotalMilliseconds;

        await Task.Delay(800);
        var chronologie = echantillonneur.Arreter();
        var auPremierPlan = GetForegroundWindow() == hwnd;
        Noter($"[{nom}] Show terminé à {apresShow:0.0} ms, Amener ({etape}) terminé à {apresAmener:0.0} ms");
        Noter(chronologie);
        Noter($"[{nom}] fin : premier plan = {Decrire(GetForegroundWindow())} ; fenêtre au premier plan = {auPremierPlan}");

        fenetre.AppWindow.Hide();
        await Task.Delay(300);
        Noter($"[{nom}] après Hide : visible = {IsWindowVisible(hwnd)}");
        File.WriteAllText(_sortie + ".fini" + numero, "fini");
    }
}
```

`$ESSAIS_FENETRE/Echantillonneur.cs`

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace EssaiFenetre;

/// <summary>
/// Relève, toutes les millisecondes environ et depuis un fil à part, l'état d'une fenêtre : visible, masquée par DWM
/// (« cloaked », donc pas encore peinte à l'écran), au premier plan, son rectangle, et si son contenu est réellement
/// affiché (le texte magenta de l'essai se voit sur une capture de l'écran). Sert à repérer un clignotement : fenêtre
/// visible mais vide, visible mais pas au premier plan, ou affichée puis cachée, se voit dans la chronologie.
/// </summary>
public sealed class Echantillonneur
{
    // Bande centrale de la fenêtre capturée à l'écran : le texte de l'essai est en magenta pur (255, 0, 255), signature
    // qu'aucune autre fenêtre ne porte. Voir ce magenta = le contenu est affiché, pas seulement la fenêtre.
    private const int Largeur = 920, Hauteur = 120, Decalage = 200, SRCCOPY = 0x00CC0020, CAPTUREBLT = 0x40000000;

    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rectangle rect);
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, int attribut, out int valeur, int taille);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint dc, ref EnteteBitmap info, uint usage, out nint bits, nint section, uint decalage);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint objet);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint cible, int x, int y, int l, int h, nint source, int sx, int sy, int operation);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint objet);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint ms);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle { public int Gauche, Haut, Droite, Bas; }

    [StructLayout(LayoutKind.Sequential)]
    private struct EnteteBitmap
    {
        public int Taille; public int Largeur; public int Hauteur; public short Plans; public short Bits;
        public int Compression; public int TailleImage; public int XPpm; public int YPpm; public int Couleurs; public int Importantes;
    }

    private readonly nint _hwnd;
    private readonly Stopwatch _chrono = Stopwatch.StartNew();
    private readonly List<(double Ms, bool Visible, bool Masquee, bool PremierPlan, bool Peint, string Rect)> _releves = [];
    private readonly byte[] _tampon = new byte[Largeur * Hauteur * 4];
    private volatile bool _arret;
    private Thread? _fil;
    private nint _ecran, _dcMemoire, _bitmap, _bits;

    /// <summary>Prépare l'échantillonnage de la fenêtre.</summary>
    public Echantillonneur(nint hwnd) => _hwnd = hwnd;

    /// <summary>Démarre le relevé ; le temps zéro est cet instant.</summary>
    public void Demarrer()
    {
        _chrono.Restart();
        _fil = new Thread(Boucle) { IsBackground = true, Priority = ThreadPriority.AboveNormal };
        _fil.Start();
    }

    /// <summary>Arrête le relevé et rend la chronologie des changements d'état et une synthèse.</summary>
    public string Arreter()
    {
        _arret = true;
        _fil?.Join();

        var texte = new StringBuilder();
        (bool, bool, bool, bool, string)? precedent = null;
        double? premierVisible = null, premierAffichee = null, premierPlan = null, premierPeint = null;
        (double Ms, bool Visible, bool Masquee, bool PremierPlan, bool Peint, string Rect)? precedentReleve = null;
        var visibleHorsPremierPlan = 0.0;
        var visibleSansContenu = 0.0;
        foreach (var r in _releves)
        {
            var etat = (r.Visible, r.Masquee, r.PremierPlan, r.Peint, r.Rect);
            if (precedent is null || !etat.Equals(precedent.Value))
            {
                texte.AppendLine($"    t={r.Ms,7:0.0} ms  visible={Oui(r.Visible)}  masquee-DWM={Oui(r.Masquee)}  premier-plan={Oui(r.PremierPlan)}  contenu-peint={Oui(r.Peint)}  rect={r.Rect}");
            }

            precedent = etat;
            var affichee = r.Visible && !r.Masquee;
            if (r.Visible) premierVisible ??= r.Ms;
            if (affichee) premierAffichee ??= r.Ms;
            if (r.PremierPlan) premierPlan ??= r.Ms;
            if (r.Peint) premierPeint ??= r.Ms;

            // Durées pendant lesquelles la fenêtre est à l'écran sans être au premier plan (elle apparaît derrière les autres),
            // ou sans contenu (un cadre vide qui apparaît avant le contenu : le clignotement). Chaque intervalle entre deux
            // relevés prend l'état du relevé qui le précède.
            if (precedentReleve is { } avant && avant.Visible && !avant.Masquee)
            {
                if (!avant.PremierPlan) visibleHorsPremierPlan += r.Ms - avant.Ms;
                if (!avant.Peint) visibleSansContenu += r.Ms - avant.Ms;
            }

            precedentReleve = r;
        }

        string Ms(double? v) => v is null ? "jamais" : $"{v:0.0} ms";
        texte.AppendLine($"    synthèse : visible dès {Ms(premierVisible)} ; non masquée par DWM dès {Ms(premierAffichee)} ; premier plan dès {Ms(premierPlan)} ; contenu peint dès {Ms(premierPeint)} ;");
        texte.AppendLine($"               à l'écran sans premier plan pendant {visibleHorsPremierPlan:0.0} ms ; à l'écran sans contenu pendant {visibleSansContenu:0.0} ms ; {_releves.Count} relevés");
        return texte.ToString();
    }

    private static string Oui(bool valeur) => valeur ? "oui" : "non";

    private void Boucle()
    {
        timeBeginPeriod(1);   // sans cela, Sleep(1) dure 15 ms et la chronologie est trop grossière
        _ecran = GetDC(0);
        _dcMemoire = CreateCompatibleDC(_ecran);
        var info = new EnteteBitmap { Taille = Marshal.SizeOf<EnteteBitmap>(), Largeur = Largeur, Hauteur = -Hauteur, Plans = 1, Bits = 32 };
        _bitmap = CreateDIBSection(_ecran, ref info, 0, out _bits, 0, 0);
        SelectObject(_dcMemoire, _bitmap);
        try
        {
            while (!_arret)
            {
                var visible = IsWindowVisible(_hwnd);
                var masquee = DwmGetWindowAttribute(_hwnd, 14, out var valeur, 4) == 0 && valeur != 0;   // DWMWA_CLOAKED
                var premierPlan = GetForegroundWindow() == _hwnd;
                GetWindowRect(_hwnd, out var rect);
                var peint = visible && ContenuPeint(rect);
                _releves.Add((_chrono.Elapsed.TotalMilliseconds, visible, masquee, premierPlan, peint,
                              $"{rect.Gauche},{rect.Haut} {rect.Droite - rect.Gauche}x{rect.Bas - rect.Haut}"));
                Thread.Sleep(1);
            }
        }
        finally
        {
            DeleteObject(_bitmap);
            DeleteDC(_dcMemoire);
            ReleaseDC(0, _ecran);
            timeEndPeriod(1);
        }
    }

    private bool ContenuPeint(Rectangle rect)
    {
        if (rect.Droite - rect.Gauche < Largeur || rect.Bas - rect.Haut < Decalage + Hauteur) return false;
        BitBlt(_dcMemoire, 0, 0, Largeur, Hauteur, _ecran, rect.Gauche, rect.Haut + Decalage, SRCCOPY | CAPTUREBLT);
        Marshal.Copy(_bits, _tampon, 0, _tampon.Length);
        var magenta = 0;
        for (var i = 0; i < _tampon.Length; i += 8)
        {
            // Octets B, G, R, A : du magenta pur a B et R hauts, G bas.
            if (_tampon[i] > 200 && _tampon[i + 1] < 60 && _tampon[i + 2] > 200) magenta++;
        }

        return magenta >= 20;
    }
}
```

`$ESSAIS_FENETRE/jouer-fenetre.ps1`

```powershell
#requires -Version 7
<#
  Point 2 de la section 16 de la spec : joue les scénarios de la fenêtre WinUI 3 jamais activée, avec le Bloc-notes au
  premier plan. Le Bloc-notes est lancé par ce script (pas par l'essai), qui simule ensuite une frappe sans effet
  (touche virtuelle 0xE8) : l'essai se trouve donc dans la situation de PressePapiers, un processus d'arrière-plan qui
  n'a ni le premier plan ni la dernière entrée. Ne modifie aucun réglage.
  Usage : pwsh -File jouer-fenetre.ps1 -Executable <EssaiFenetre.exe> -Modes directe,showfalse,...
#>
param(
    [Parameter(Mandatory)][string]$Executable,
    [string[]]$Modes = @('directe', 'showfalse', 'showseul', 'prechauffe', 'prechauffe300', 'activee'),
    [string]$Sortie = (Join-Path ([IO.Path]::GetTempPath()) 'essai-fenetre')
)

$ErrorActionPreference = 'Stop'
# Avec « pwsh -File », une liste passée en « a,b,c » arrive en une seule chaîne.
$Modes = @($Modes | ForEach-Object { $_ -split ',' })
New-Item -ItemType Directory -Force $Sortie | Out-Null

Add-Type -Namespace Essai -Name Systeme -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern System.IntPtr GetForegroundWindow();
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(System.IntPtr h, out uint pid);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, System.UIntPtr extra);
'@

function PremierPlanDe([string]$nom) {
    $h = [Essai.Systeme]::GetForegroundWindow(); $pid_ = [uint32]0
    [void][Essai.Systeme]::GetWindowThreadProcessId($h, [ref]$pid_)
    $p = Get-Process -Id $pid_ -ErrorAction SilentlyContinue
    return ($p -and $p.ProcessName -eq $nom)
}

function Attendre([scriptblock]$condition, [int]$secondes) {
    $fin = (Get-Date).AddSeconds($secondes)
    while ((Get-Date) -lt $fin) { if (& $condition) { return $true }; Start-Sleep -Milliseconds 100 }
    return $false
}

foreach ($mode in $Modes) {
    $fichier = Join-Path $Sortie "$mode.txt"
    Get-ChildItem $Sortie -Filter "$mode.txt*" | Remove-Item -Force
    $essai = Start-Process $Executable -ArgumentList $mode, $fichier -PassThru
    try {
        if (-not (Attendre { Test-Path "$fichier.pret" } 30)) { throw "mode $mode : l'essai n'est pas prêt" }
        foreach ($n in 1, 2) {
            $avant = (Get-Date).AddSeconds(-1); Start-Process notepad.exe
            [void](Attendre { PremierPlanDe 'Notepad' } 10)
            # Une entrée « utilisateur » sans effet (VK 0xE8, non attribuée) : la dernière entrée n'est pas à l'essai.
            [Essai.Systeme]::keybd_event(0xE8, 0, 0, [UIntPtr]::Zero); [Essai.Systeme]::keybd_event(0xE8, 0, 2, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 1500
            Set-Content "$fichier.go$n" 'go'
            if (-not (Attendre { Test-Path "$fichier.fini$n" } 30)) { throw "mode $mode : affichage $n sans fin" }
            # Seuls les Bloc-notes lancés par ce script sont fermés.
            Get-Process Notepad -ErrorAction SilentlyContinue | Where-Object { $_.StartTime -ge $avant } | ForEach-Object { try { $_.Kill() } catch {} }
        }
        [void]$essai.WaitForExit(15000)
    }
    finally {
        if (-not $essai.HasExited) { $essai.Kill() }
    }
    Get-Content $fichier -Encoding utf8
}
```

```bash
cd "$ESSAIS_FENETRE" && dotnet build -c Debug -p:Depot="$DEPOT"
pwsh -NoProfile -File "$ESSAIS_FENETRE/jouer-fenetre.ps1" \
  -Executable "$ESSAIS_FENETRE/bin/Debug/net10.0-windows10.0.19041.0/win-x64/EssaiFenetre.exe" \
  -Modes directe,showfalse,showseul,prechauffe,prechauffe300,activee
```

Le script ouvre puis ferme deux fois le Bloc-notes par mode (environ une minute en tout) et affiche, pour chaque affichage, la
chronologie des changements d'état et une synthèse. Les Bloc-notes déjà ouverts par l'utilisateur ne sont pas touchés.

Résultats observés sur le poste (Windows 11 build 26300, Windows App SDK 2.5.1, quatre passages en mode `directe`, un seul pour
les autres modes ; durées en millisecondes, écart entre passages dû à la charge de la machine) :

| Mode | Premier affichage | Second affichage |
|---|---|---|
| `directe` | `Show()` seul ne suffit pas : étape **SendInputVide** de `Amener` nécessaire (4 passages sur 4). Fenêtre visible dès 11 à 21, premier plan dès 26 à 39, contenu affiché dès 46 à 78 ; 8 à 23 ms à l'écran sans le premier plan, 29 à 62 ms à l'écran sans contenu. | `Amener` rend **DejaAuPremierPlan** (4 sur 4) : `Show()` a suffi. Premier plan dès l'apparition (0 ms hors premier plan, une fois 10 ms), contenu dès 28 à 66 ; 20 à 49 ms sans contenu. |
| `showfalse` | `Show(false)` : la fenêtre est visible mais jamais au premier plan avant `Amener` (étape SendInputVide). Contenu dès 70 ; 52 ms sans contenu. | Encore SendInputVide (`Show(false)` n'active pas) ; contenu dès 30. |
| `showseul` | Sans `Amener` : la fenêtre reste visible derrière le Bloc-notes pendant toute la mesure (800 ms), contenu **jamais** affiché (recouvert). | Idem. |
| `prechauffe` | `Show(false)` puis `Hide()` aussitôt au démarrage : la fenêtre est visible 2 à 3 ms, jamais au premier plan, contenu jamais peint ; rien à voir à l'écran. Premier affichage ensuite : SendInputVide, 33 à 98 ms sans contenu : **pas mieux que sans préchauffage**. | DejaAuPremierPlan, 20 à 49 ms sans contenu. |
| `prechauffe300` | Fenêtre visible 300 ms au démarrage, contenu peint dès 65 : **un vrai clignotement au lancement**. Premier affichage ensuite : SendInputVide, 33 ms sans contenu. | DejaAuPremierPlan, 19 ms sans contenu. |
| `activee` | `Activate()` puis `Hide()` au démarrage : la fenêtre prend le **premier plan** quelques millisecondes (vole le focus à l'application en cours). Premier affichage ensuite : DejaAuPremierPlan, 23 ms sans contenu. | DejaAuPremierPlan, 20 ms sans contenu. |

Ce qui marche, et ce que la tâche 27 doit en retenir :
- **Aucune première activation n'est nécessaire.** Une fenêtre WinUI 3 créée puis jamais activée s'affiche, prend le premier plan
  et peint son contenu par `AppWindow.Show()` suivi de `PremierPlan.Amener(hwnd)`, depuis une application tierce. Le préchauffage
  `Show(false)` puis `Hide()` n'apporte rien (même durée sans contenu) ; `Activate()` au démarrage vole le focus ; garder la
  fenêtre visible quelques centaines de millisecondes au démarrage clignote. **Ne rien faire de tout cela au démarrage** : créer
  la fenêtre et ne pas l'afficher.
- **`Amener` est indispensable au premier affichage** : `Show()` seul laisse la fenêtre derrière l'application au premier plan
  (étape SendInputVide observée à chaque premier affichage). Aux affichages suivants, `Show()` a donné le premier plan seul dans
  l'essai, mais `Amener` reste à appeler à chaque fois (il rend aussitôt `DejaAuPremierPlan`, sans coût).
- **Pas de `Show(false)`** : il n'active pas la fenêtre, `Amener` doit alors tout faire, et le contenu arrive plus tard.
- **Clignotement** : la fenêtre reste visible derrière les autres 8 à 23 ms avant le premier plan (53 ms une fois, machine chargée), et son
  contenu apparaît 20 à 60 ms après la fenêtre (cadre Mica vide pendant ce court intervalle, plus long au tout premier
  affichage). C'est un effet de la composition de WinUI 3, que ni `Show(false)` ni un préchauffage ne réduisent. Une mesure
  d'écran à la milliseconde reste approximative ; l'impression visuelle est à confirmer à la recette (spec, section 14).
- Limites : la fenêtre de l'essai ne contient qu'un texte (la vraie fenêtre, plus chargée, peut mettre plus de temps à peindre) et
  le premier plan était celui du Bloc-notes (une application élevée n'a pas été essayée).

**5d. Exceptions non gérées (constats, sans programme)**

Essai avec du code temporaire ajouté à la fin de `Demarrer` (retiré ensuite), en versions Debug, chaque cas dans un processus neuf :

| Exception levée par | Résultat |
|---|---|
| une tâche non observée (`Task.Run(() => throw ...)` puis `GC.Collect`) | notée au journal par `SurExceptionTache`, le processus continue et s'arrête proprement |
| un fil nu (`new Thread(() => throw ...)`) | notée par `SurExceptionDomaine` (`arrêt en cours : True`), le processus s'arrête (code `0xE0434352`), comme prévu |
| un rappel de `DispatcherQueue.TryEnqueue` ou de `DispatcherQueueTimer.Tick` | **le processus s'arrête (code `0xC000027B`) sans rien écrire au journal** : `Application.UnhandledException` n'est pas appelé |
| un événement Click lancé par `ButtonAutomationPeer.Invoke` | l'exception remonte à l'appelant (chemin managé) ; le chemin `UnhandledException` de WinUI n'a pas pu être exercé sans interface |

Conséquence retenue dans le code : tout travail posté sur le fil d'interface passe par `App.PosterSurInterface` (ou un `try`/`catch`
local), qui note l'exception au lieu de la laisser sortir du rappel ; `SignalerActivation` et la minuterie d'arrêt de recette
l'utilisent déjà. La tâche 29 l'utilisera pour `WinVPresse`. Le gestionnaire `UnhandledException` de l'application reste en place
pour les exceptions des événements XAML (à confirmer à la recette avec un vrai clic).

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Stockage/IDialogueDemarrage.cs src/PressePapiers.Coeur/Stockage/OuvertureBase.cs src/PressePapiers/Natif/NatifSession.cs src/PressePapiers/Natif/NatifDemarrage.cs src/PressePapiers/Systeme/DialoguesWindows.cs src/PressePapiers/Systeme/SourceSessionWts.cs src/PressePapiers/Systeme/DemarrageWindows.cs src/PressePapiers/Systeme/JournalSerilog.cs src/PressePapiers/Program.cs src/PressePapiers/App.xaml.cs src/PressePapiers/PressePapiers.csproj tests/PressePapiers.Tests/Stockage/OuvertureBaseTests.cs
git commit -m "Démarrer l'application : instance unique, journal, base, session et fenêtre masquée" -m "Program prend l'instance unique avant toute fenêtre (AppInstance.FindOrRegisterForKey, redirection attendue par CoWaitForMultipleObjects, trace au journal des deux côtés). App suit l'ordre de démarrage de la spec : journal Serilog (un fichier par jour, 14 gardés), dossiers, copie de sécurité et ouverture de la base, purge de session, fenêtre principale créée sans être affichée, arrêt explicite, chemin Quitter, gestionnaires d'exceptions non gérées." -m "OuvertureBase (Coeur, testée) propose la restauration de la copie la plus récente quand la base est abîmée, garde le fichier abîmé à côté, et ne restaure jamais pour une autre erreur (accès refusé, SQLite trop ancienne). SourceSessionWts lit LogonTime par WTSQuerySessionInformationW ; DemarrageWindows écrit la clé Run, avec une surcharge sur le chemin de clé pour les essais."
```
