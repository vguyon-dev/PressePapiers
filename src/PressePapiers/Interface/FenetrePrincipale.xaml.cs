using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PressePapiers.Coeur.Modele;
using PressePapiers.Systeme;
using WinRT.Interop;

namespace PressePapiers.Interface;

/// <summary>
/// Fenêtre principale (proposition B de la maquette, rendu WinUI 3) : volet des onglets, liste des éléments et
/// recherche à gauche, aperçu à droite. Créée au démarrage et masquée ; la masquer ne termine pas l'application.
/// </summary>
public sealed partial class FenetrePrincipale : Window
{
    private static readonly TimeSpan _delaiMemoireTaille = TimeSpan.FromMilliseconds(500);

    private readonly DispatcherQueueTimer _minuteurTaille;
    private bool _voletEnSynchronisation;
    private bool _ouvertureEnCours;
    private bool _fermetureAutorisee;

    /// <summary>Crée la fenêtre, masquée, sur le modèle de vue.</summary>
    public FenetrePrincipale(FenetrePrincipaleVue vue)
    {
        ArgumentNullException.ThrowIfNull(vue);
        Vue = vue;
        InitializeComponent();
        Handle = WindowNative.GetWindowHandle(this);

        AppliquerFond(MicaController.IsSupported());

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(ZoneTitre);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "icone-normale.ico"));
        AppliquerTailleMemorisee();

        _minuteurTaille = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _minuteurTaille.Interval = _delaiMemoireTaille;
        _minuteurTaille.IsRepeating = false;
        _minuteurTaille.Tick += (_, _) => MemoriserTaille();
        AppWindow.Changed += AppWindow_Changed;

        // Cycle de vie : la croix masque la fenêtre au lieu de la fermer, la perte du focus la masque aussi.
        AppWindow.Closing += AppWindow_Closing;
        Activated += Fenetre_Activated;

        PanneauApercu.Lier(Vue);
        Vue.HandleOutil = Handle;
        Vue.MasquerFenetre = Masquer;
        Vue.CollageDemande += (mode, copieSeule) => _ = Vue.CollerAsync(mode, copieSeule);
        Vue.Dialogues = new GestionnaireDialogues(() => Content as FrameworkElement, () => EstVisible);
        Vue.Rafraichi += SynchroniserVolet;
        SynchroniserVolet();

        // Le NavigationView ignore une sélection posée avant son chargement : on la rejoue au premier affichage.
        Navigation.Loaded += (_, _) => SynchroniserVolet();
    }

    /// <summary>Modèle de vue lié aux contrôles.</summary>
    public FenetrePrincipaleVue Vue { get; }

    /// <summary>Handle Win32 de la fenêtre.</summary>
    public nint Handle { get; }

    /// <summary>Vrai tant que la fenêtre est affichée.</summary>
    public bool EstVisible => AppWindow.IsVisible;

    /// <summary>Levé quand Windows refuse de donner le premier plan à la fenêtre : l'application doit le dire par l'icône.</summary>
    public event Action? PremierPlanRefuse;

    /// <summary>
    /// Affiche la fenêtre au centre de l'écran de la fenêtre d'origine, sur Temporaire, premier élément choisi, curseur dans
    /// la recherche (AppWindow.Show puis PremierPlan.Amener). Ouverture idempotente : un signal reçu pendant que la fenêtre
    /// est affichée ou en cours d'ouverture est ignoré. Depuis l'icône ou une relance, la fenêtre d'origine n'est pas fiable :
    /// Entrée copie seulement.
    /// </summary>
    public void AfficherPour(nint fenetreOrigine, OrigineOuverture origine)
    {
        if (EstVisible || _ouvertureEnCours)
        {
            // Une relance de l'application, ou l'icône, demandent la fenêtre : on la remet devant sans rien changer.
            if (EstVisible && origine is OrigineOuverture.Icone or OrigineOuverture.Relance)
            {
                PremierPlan.Amener(Handle);
            }

            return;
        }

        _ouvertureEnCours = true;
        var origineFiable = origine is OrigineOuverture.WinV or OrigineOuverture.RaccourciSecours
                            && PremierPlan.EstOrigineValide(fenetreOrigine, Handle);
        Vue.FenetreOrigine = origineFiable ? fenetreOrigine : 0;
        Vue.Affichee = true;
        Vue.PreparerOuverture(origine);
        MontrerHistorique();
        PlacerAuCentre(origineFiable ? fenetreOrigine : 0);
        AppWindow.Show();
        var premierPlanObtenu = PremierPlan.Amener(Handle);
        if (!premierPlanObtenu)
        {
            PremierPlanRefuse?.Invoke();
        }

        ChampRecherche.Focus(FocusState.Programmatic);

        // Les pertes de focus survenues pendant l'ouverture elle-même sont ignorées, donc on les rattrape ici : une fenêtre
        // qui a eu le premier plan puis l'a perdu avant la fin de l'ouverture ne doit pas rester ouverte sans focus.
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _ouvertureEnCours = false;
            if (premierPlanObtenu && EstVisible && PremierPlan.FenetreActive() != Handle)
            {
                Masquer();
            }
        });
    }

    /// <summary>Masque la fenêtre sans terminer l'application.</summary>
    public void Masquer() => AppWindow.Hide();

    /// <summary>Permet à la fenêtre de se fermer vraiment : à appeler avant de quitter l'application (menu de l'icône).</summary>
    public void AutoriserFermeture() => _fermetureAutorisee = true;

    /// <summary>Ouvre la fenêtre sur la page Paramètres.</summary>
    public void AfficherParametres() => throw new NotImplementedException("Tâche 28");

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidVisibilityChange)
        {
            Vue.Affichee = sender.IsVisible;
        }

        // Pas de mémoire tant que la fenêtre est masquée : sa taille y est sans intérêt.
        if (args.DidSizeChange && sender.IsVisible)
        {
            _minuteurTaille.Stop();
            _minuteurTaille.Start();
        }
    }

    // La croix, Alt+F4 et la fermeture par le système masquent la fenêtre : l'application vit dans la zone de notification.
    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_fermetureAutorisee)
        {
            return;
        }

        args.Cancel = true;
        Masquer();
    }

    // Perdre le focus ferme la fenêtre, sauf pendant une ouverture, un recollage (la fenêtre d'origine reprend le premier
    // plan) ou une boîte de dialogue.
    private void Fenetre_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated && EstVisible && !_ouvertureEnCours
            && !Vue.CollageEnCours && Vue.Dialogues?.EnCours != true)
        {
            Masquer();
        }
    }
}
