using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using PressePapiers.Capture;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Recherche;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Interface;

/// <summary>
/// Modèle de vue de la fenêtre principale : onglets avec compteurs, liste des éléments de l'onglet ou résultats de
/// recherche, légende de Temporaire. Vit sur le fil d'interface ; les écritures du dépôt faites depuis un autre fil
/// (la capture) y reviennent par le <see cref="DispatcherQueue"/> et sont regroupées en un seul rafraîchissement.
/// </summary>
public sealed partial class FenetrePrincipaleVue : ObservableObject
{
    /// <summary>Délai de frappe avant de lancer la recherche (spec, section 8).</summary>
    internal static readonly TimeSpan DelaiRecherche = TimeSpan.FromMilliseconds(150);

    /// <summary>Durée d'affichage d'un message de réussite avant sa fermeture automatique.</summary>
    internal static readonly TimeSpan DelaiAnnonceReussie = TimeSpan.FromSeconds(5);

    private readonly DepotHistorique _depot;
    private readonly MoteurRecherche _recherche;
    private readonly ServiceRecollage _recollage;
    private readonly ServiceParametres _parametres;
    private readonly IJournal _journal;
    private readonly DispatcherQueue _file;
    private readonly DispatcherQueueTimer _minuteurRecherche;
    private readonly DispatcherQueueTimer _minuteurAnnonce;

    private HashSet<string> _chasseFixe = [];
    private bool _rafraichissementPlanifie;
    private bool _rafraichissementEnAttente;
    private bool _affichee;
    private bool _rechercheInterne;
    private bool _relecture;
    private ElementVue? _elementSelectionne;

    /// <summary>Crée le modèle de vue sur le fil d'interface et lit une première fois les onglets et la liste.</summary>
    public FenetrePrincipaleVue(DepotHistorique depot, MoteurRecherche recherche, ServiceRecollage recollage,
                                ServiceParametres parametres, IJournal journal)
    {
        ArgumentNullException.ThrowIfNull(depot);
        ArgumentNullException.ThrowIfNull(recherche);
        ArgumentNullException.ThrowIfNull(recollage);
        ArgumentNullException.ThrowIfNull(parametres);
        ArgumentNullException.ThrowIfNull(journal);

        _depot = depot;
        _recherche = recherche;
        _recollage = recollage;
        _parametres = parametres;
        _journal = journal;
        _file = DispatcherQueue.GetForCurrentThread()
                ?? throw new InvalidOperationException("Le modèle de vue doit être créé sur le fil d'interface.");

        _minuteurRecherche = _file.CreateTimer();
        _minuteurRecherche.Interval = DelaiRecherche;
        _minuteurRecherche.IsRepeating = false;
        ExecutionProtegee.Brancher(_minuteurRecherche, _journal, () => Rafraichir(premierElement: true), "la recherche");

        _minuteurAnnonce = _file.CreateTimer();
        _minuteurAnnonce.Interval = DelaiAnnonceReussie;
        _minuteurAnnonce.IsRepeating = false;
        ExecutionProtegee.Brancher(_minuteurAnnonce, _journal, () => AnnonceOuverte = false, "la fermeture du message");

        Reglages = parametres.Charger();
        _chasseFixe = ResumeElement.EnsembleChasseFixe(Reglages.ApplicationsChasseFixe);
        AppliquerDemarrage = activer => GestionDemarrage.Modifier(activer, _journal);

        // Seuls les onglets sont lus ici : la liste des éléments n'est lue qu'à l'ouverture de la fenêtre.
        _depot.Modifie += OnDepotModifie;
        try
        {
            LireOnglets();
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible de lire les onglets au démarrage.", exception);
        }
    }

    /// <summary>Horloge de l'affichage des heures ; remplaçable pour l'essai.</summary>
    public TimeProvider Horloge { get; set; } = TimeProvider.System;

    /// <summary>Réglages en vigueur (taille de la fenêtre, applications à chasse fixe, mode de collage).</summary>
    public Parametres Reglages { get; private set; }

    /// <summary>Texte d'état du hook Win+V affiché dans les Paramètres, fourni par l'application.</summary>
    [ObservableProperty]
    public partial string EtatCrochet { get; set; } = "Inconnu";

    /// <summary>Onglets du volet de navigation, Temporaire en premier.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<OngletVue> Onglets { get; private set; } = [];

    /// <summary>Onglet dont la liste est affichée.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LegendeVisible))]
    public partial OngletVue? OngletSelectionne { get; private set; }

    /// <summary>Lignes de la liste : éléments de l'onglet, ou résultats de la recherche dans tous les onglets.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListeVide))]
    [NotifyPropertyChangedFor(nameof(MessageVide))]
    public partial IReadOnlyList<ElementVue> Elements { get; private set; } = [];

    /// <summary>
    /// Ligne choisie dans la liste. Quand la liste est remplacée, le ListView se vide un instant et renvoie null par la
    /// liaison : pendant une relecture ce null est ignoré, pour que l'aperçu et ses champs ne soient pas remis à zéro.
    /// </summary>
    public ElementVue? ElementSelectionne
    {
        get => _elementSelectionne;
        set
        {
            if (_relecture && value is null)
            {
                return;
            }

            SetProperty(ref _elementSelectionne, value);
        }
    }

    /// <summary>Texte saisi dans la zone de recherche ; la liste suit 150 ms après la dernière frappe.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnRecherche))]
    [NotifyPropertyChangedFor(nameof(LegendeVisible))]
    [NotifyPropertyChangedFor(nameof(MessageVide))]
    public partial string Recherche { get; set; } = string.Empty;

    /// <summary>Vrai si la saisie contient autre chose que des espaces : la liste montre alors des résultats.</summary>
    public bool EnRecherche => !string.IsNullOrWhiteSpace(Recherche);

    /// <summary>Vrai pour la légende de Temporaire, qui disparaît pendant une recherche.</summary>
    public bool LegendeVisible => !EnRecherche && OngletSelectionne?.EstTemporaire == true;

    /// <summary>Texte de la légende de l'onglet Temporaire.</summary>
    public string LegendeTemporaire =>
        "Vidé à la prochaine ouverture de session Windows. F2 pour garder un élément sous un nom.";

    /// <summary>Vrai si la liste n'a aucune ligne.</summary>
    public bool ListeVide => Elements.Count == 0;

    /// <summary>Message affiché à la place d'une liste vide.</summary>
    public string MessageVide => EnRecherche
        ? $"Aucun élément ne contient « {Recherche.Trim()} »."
        : "Aucun élément dans cet onglet.";

    /// <summary>
    /// Vrai tant que la fenêtre est affichée. Masquée, elle ne relit rien et ne garde aucune ligne : une écriture du dépôt
    /// est seulement notée (une copie par seconde ne doit pas coûter une lecture de la base). La liste est lue par
    /// <see cref="PreparerOuverture"/> à chaque ouverture ; si l'affichage a lieu sans elle, la relecture en attente est planifiée.
    /// </summary>
    public bool Affichee
    {
        get => _affichee;
        set
        {
            if (_affichee == value)
            {
                return;
            }

            _affichee = value;
            if (value)
            {
                if (_rafraichissementEnAttente)
                {
                    PlanifierRafraichissement();
                }

                return;
            }

            // Les réglages en cours de saisie sont enregistrés avant que la liste soit vidée.
            try
            {
                FenetreMasquee?.Invoke();
            }
            finally
            {
                LibererListe();
            }
        }
    }

    /// <summary>Levé quand la fenêtre passe de affichée à masquée : la page Paramètres y enregistre ce qui est en cours de saisie.</summary>
    public event Action? FenetreMasquee;

    /// <summary>Événement levé après chaque rafraîchissement, pour que la fenêtre aligne le volet de navigation.</summary>
    public event Action? Rafraichi;
}
