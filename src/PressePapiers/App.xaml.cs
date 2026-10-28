using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PressePapiers.Capture;
using PressePapiers.Coeur.Clavier;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Session;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Interface;
using PressePapiers.Systeme;

namespace PressePapiers;

/// <summary>
/// Application WinUI 3 : cycle de vie et ordre de démarrage de la spec (section 12). Ce fichier porte le démarrage,
/// l'arrêt et les exceptions ; la composition des briques (relais, icône, hook, capture, fenêtre) est dans
/// <c>App.Composition.cs</c>.
/// </summary>
public sealed partial class App : Application
{
    private static readonly Lock _verrou = new();
    private static App? _courante;
    private static bool _relancePendante;

    private readonly DispatcherQueue _fil;
    private readonly IDialogueDemarrage _dialogues = new DialoguesWindows();

    // Un seul anti-rebond pour Win+V et le raccourci de secours : deux signaux à moins de 300 ms n'ouvrent qu'une fois.
    private readonly AntiRebond _antiRebond = new(TimeProvider.System, TimeSpan.FromMilliseconds(300));

    private IJournal _journal = JournalNul.Instance;
    private JournalSerilog? _journalFichier;
    private BaseHistorique? _base;
    private DepotHistorique? _depot;
    private RelaisWindows? _relais;
    private IconeNotification? _icone;
    private ICrochetWinV? _crochet;
    private ServiceCapture? _capture;
    private FenetrePrincipaleVue? _vue;
    private FenetrePrincipale? _fenetre;
    private DispatcherQueueTimer? _minuterieCrochet;
    private SurveillanceInterface? _surveillance;
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
        ExecutionProtegee.Poster(_fil, _journal, action, contexte);

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

    /// <summary>Affiche la fenêtre principale sans fenêtre d'origine (icône, relance) ; fil d'interface seulement.</summary>
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

        // Pas de fenêtre d'origine fiable : Entrée copiera seulement.
        _fenetre.AfficherPour(0, origine);
    }

    /// <summary>
    /// Chemin Quitter : lève le drapeau, arrête ce qui écrit dans la base (capture, hook), retire l'icône, autorise la
    /// fenêtre à se fermer, libère le relais, la base et le journal, puis termine l'application. Sans effet la seconde fois.
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
            _minuterieCrochet?.Stop();
            _surveillance?.Dispose();
            _capture?.Dispose();
            _crochet?.Dispose();
            _icone?.Dispose();
            _fenetre?.AutoriserFermeture();
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
            _capture = null;
            _crochet = null;
            _icone = null;
            _relais = null;
            _base = null;
            Exit();
        }
    }

    // Ordre de la spec : journal, dossiers, copie de sécurité et ouverture de la base, détection de session et purge,
    // fenêtre principale masquée, relais (écoute, raccourci de secours, icône), hook, démarrage avec Windows.
    private void Demarrer()
    {
        var dossiers = DossierDonnees.ParDefaut();
        dossiers.CreerDossiers();
        _journalFichier = new JournalSerilog(dossiers.Journal);
        _journal = _journalFichier;
        _journal.Info($"Démarrage de PressePapiers {typeof(App).Assembly.GetName().Version} "
                      + $"(processus {Environment.ProcessId}, données : {dossiers.Racine}, "
                      + $"{(NiveauIntegrite.ProcessusCourantEstEleve() ? "élevé" : "non élevé")}, exécutable : {Environment.ProcessPath}).");

        PremierPlan.Journal = _journal;

        // Dès le journal ouvert : un gel du démarrage lui-même doit laisser un dump.
        _surveillance = new SurveillanceInterface(_fil, _journal, Path.Combine(dossiers.Racine, "dumps"));

        _base = OuvertureBase.Ouvrir(dossiers, TimeProvider.System, _journal, _dialogues);
        if (_base is null)
        {
            Quitter();
            return;
        }

        _journal.Info($"SQLite {_base.VersionSqlite}.");
        _depot = new DepotHistorique(_base, dossiers, TimeProvider.System, _journal);

        // La purge précède tout ce qui peut capturer une copie : jamais d'effacement d'une copie faite après l'ouverture de session.
        var session = DemarrageSession.Appliquer(new SourceSessionWts(), _depot, _journal);
        _journal.Info($"Session Windows : {session.Etat}, {session.ElementsPurges} élément(s) de Temporaire purgé(s).");

        // Après la purge et avant toute capture : un plantage a pu laisser des PNG sans élément. Pas après une
        // restauration : les PNG des éléments perdus de la base abîmée (gardée à part) doivent rester récupérables.
        if (_base.RestaureeDepuisSauvegarde)
        {
            _journal.Info("Base restaurée depuis une copie de sécurité : les images sans élément sont conservées.");
        }
        else
        {
            _depot.NettoyerImagesOrphelines();
        }

        CreerFenetrePrincipale(_base, _depot, dossiers);
        DemarrerRelais(_depot);
        DemarrerCrochet();
        AppliquerDemarrageWindows();
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

    // Jouée sur le fil d'interface : une seconde instance a redirigé son activation vers celle-ci.
    private void SurRelance()
    {
        _journal.Info("Activation redirigée reçue d'une seconde instance : ouverture de la fenêtre demandée.");
        AfficherFenetre(OrigineOuverture.Relance);
    }

    // Note l'exception au journal au lieu de la laisser sortir du rappel (qui arrêterait le processus sans trace).
    private void ExecuterProtege(Action action, string contexte) => ExecutionProtegee.Executer(_journal, action, contexte);

    // Recette automatique (versions Debug seulement) : PRESSEPAPIERS_ARRET_APRES_MS demande un arrêt propre, par le chemin
    // Quitter, au bout de ce délai : un moyen de vérifier l'arrêt sans tuer le processus ni cliquer sur l'icône.
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

        // PRESSEPAPIERS_GEL_APRES_MS fige le fil d'interface 25 s : recette de SurveillanceInterface (gel, dump, reprise).
        if (int.TryParse(Environment.GetEnvironmentVariable("PRESSEPAPIERS_GEL_APRES_MS"), out var gel) && gel > 0)
        {
            var minuterieGel = _fil.CreateTimer();
            minuterieGel.Interval = TimeSpan.FromMilliseconds(gel);
            minuterieGel.IsRepeating = false;
            minuterieGel.Tick += (_, _) => Thread.Sleep(TimeSpan.FromSeconds(25));
            minuterieGel.Start();
            _journal.Info($"Gel de recette programmé dans {gel} ms.");
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
