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
    private bool _parametresVoulus;
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
        AppliquerPresentation();

        _minuteurTaille = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _minuteurTaille.Interval = _delaiMemoireTaille;
        _minuteurTaille.IsRepeating = false;
        ExecutionProtegee.Brancher(_minuteurTaille, Vue.Journal, MemoriserTaille, "la mémorisation de la taille");
        AppWindow.Changed += AppWindow_Changed;

        // Cycle de vie : la croix masque la fenêtre au lieu de la fermer, la perte du focus la masque aussi.
        AppWindow.Closing += AppWindow_Closing;
        Activated += Fenetre_Activated;

        PanneauApercu.Lier(Vue);
        ZoneParametres.Lier(Vue);
        AppliquerTheme(Vue.Reglages.Theme);
        Vue.ParametresModifies += () => AppliquerTheme(Vue.Reglages.Theme);
        Vue.HandleOutil = Handle;
        Vue.MasquerFenetre = Masquer;
        Vue.CollageDemande += (mode, copieSeule) =>
            ExecutionProtegee.Lancer(Vue.Journal, () => Vue.CollerAsync(mode, copieSeule), "le collage");

        // Après un enregistrement, le curseur revient à la recherche : l'utilisateur peut enchaîner au clavier.
        PanneauApercu.ElementEnregistre += () => ChampRecherche.Focus(FocusState.Programmatic);
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

    /// <summary>Vrai pendant l'ouverture elle-même (jusqu'à la fin de la file de messages qui suit l'affichage).</summary>
    public bool OuvertureEnCours => _ouvertureEnCours;

    /// <summary>Vrai si la fenêtre est la fenêtre active du système.</summary>
    public bool EstAuPremierPlan => PremierPlan.FenetreActive() == Handle;

    /// <summary>
    /// Remet devant une fenêtre déjà affichée mais passée derrière (boîte de dialogue ouverte puis changement d'application,
    /// premier plan refusé à l'ouverture). Si Windows refuse, <see cref="PremierPlanRefuse"/> est levé.
    /// </summary>
    public void RamenerAuPremierPlan()
    {
        if (!PremierPlan.Amener(Handle))
        {
            PremierPlanRefuse?.Invoke();
        }
    }

    /// <summary>Levé quand Windows refuse de donner le premier plan à la fenêtre : l'application doit le dire par l'icône.</summary>
    public event Action? PremierPlanRefuse;

    /// <summary>
    /// Affiche la fenêtre au centre de l'écran de la fenêtre d'origine, sur Temporaire, premier élément choisi, curseur dans
    /// la recherche (AppWindow.Show puis PremierPlan.Amener). Ouverture idempotente : un signal reçu pendant que la fenêtre
    /// est affichée ou en cours d'ouverture est ignoré. Depuis l'icône ou une relance, la fenêtre d'origine n'est pas fiable :
    /// Entrée copie seulement.
    /// </summary>
    public void AfficherPour(nint fenetreOrigine, OrigineOuverture origine) => Ouvrir(fenetreOrigine, origine, messageOrigine: true);

    /// <summary>
    /// Ouvre la fenêtre sur la page Paramètres (menu de l'icône), sans fenêtre d'origine ; si elle est déjà affichée,
    /// elle est remise devant et passe sur la page Paramètres.
    /// </summary>
    public void AfficherParametres()
    {
        if (EstVisible)
        {
            PremierPlan.Amener(Handle);
        }
        else
        {
            Ouvrir(0, OrigineOuverture.Icone, messageOrigine: false);
        }

        _parametresVoulus = true;
        Navigation.SelectedItem = Navigation.SettingsItem;
        AfficherPage(parametres: true);
    }

    private void Ouvrir(nint fenetreOrigine, OrigineOuverture origine, bool messageOrigine)
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
        Vue.Journal.Info($"Ouverture de la fenêtre ({origine}).");
        var rattrapageProgramme = false;
        try
        {
            var origineFiable = origine is OrigineOuverture.WinV or OrigineOuverture.RaccourciSecours
                                && PremierPlan.EstOrigineValide(fenetreOrigine, Handle);
            Vue.FenetreOrigine = origineFiable ? fenetreOrigine : 0;
            Vue.Affichee = true;
            Vue.PreparerOuverture(origine, messageOrigine);
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
            rattrapageProgramme = ExecutionProtegee.Poster(DispatcherQueue, Vue.Journal, () =>
            {
                _ouvertureEnCours = false;
                if (premierPlanObtenu && EstVisible && PremierPlan.FenetreActive() != Handle)
                {
                    Masquer();
                }
            }, "la fin de l'ouverture", DispatcherQueuePriority.Low);
        }
        catch (Exception)
        {
            // L'ouverture a échoué avant l'affichage : le modèle de vue redevient celui d'une fenêtre masquée.
            if (!EstVisible)
            {
                Vue.Affichee = false;
            }

            throw;
        }
        finally
        {
            // Sans rattrapage programmé, rien ne rabaisserait le drapeau : la fenêtre ne pourrait plus jamais s'ouvrir.
            if (!rattrapageProgramme)
            {
                _ouvertureEnCours = false;
            }
        }
    }

    /// <summary>Masque la fenêtre sans terminer l'application.</summary>
    public void Masquer() => AppWindow.Hide();

    /// <summary>Permet à la fenêtre de se fermer vraiment : à appeler avant de quitter l'application (menu de l'icône).</summary>
    public void AutoriserFermeture() => _fermetureAutorisee = true;

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
