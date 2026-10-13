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
