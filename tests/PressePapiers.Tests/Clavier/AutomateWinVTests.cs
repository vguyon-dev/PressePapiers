using PressePapiers.Coeur.Clavier;

namespace PressePapiers.Tests.Clavier;

public sealed class AutomateWinVTests
{
    private const uint Win = AutomateWinV.VkWinGauche;
    private const uint WinDroite = AutomateWinV.VkWinDroite;
    private const uint V = AutomateWinV.VkV;
    private const uint Autre = 0x41; // la lettre A

    // Raccourcis de lecture : une frappe réelle (non injectée) avec la touche Windows réellement enfoncée ou non.
    private static ActionCrochet Appuyer(AutomateWinV a, uint touche, bool winReelle = true) =>
        a.Traiter(touche, enfoncee: true, injectee: false, winReellementEnfoncee: winReelle);

    private static ActionCrochet Relacher(AutomateWinV a, uint touche, bool winReelle = true) =>
        a.Traiter(touche, enfoncee: false, injectee: false, winReellementEnfoncee: winReelle);

    [Fact]
    public void Win_puis_V_avale_et_ouvre()
    {
        var automate = new AutomateWinV();

        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, Win));
        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V));
    }

    [Fact]
    public void Win_droite_puis_V_avale_et_ouvre()
    {
        var automate = new AutomateWinV();

        Appuyer(automate, WinDroite);

        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V));
    }

    [Fact]
    public void La_repetition_de_V_est_avalee_sans_rouvrir()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V));

        // Le clavier répète V tant que la touche est tenue : une seule ouverture au total.
        Assert.Equal(ActionCrochet.Avaler, Appuyer(automate, V));
        Assert.Equal(ActionCrochet.Avaler, Appuyer(automate, V));
        Assert.Equal(ActionCrochet.Avaler, Appuyer(automate, V));
    }

    [Fact]
    public void Le_relachement_de_V_est_avale()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);

        Assert.Equal(ActionCrochet.Avaler, Relacher(automate, V));
    }

    [Fact]
    public void Un_second_Win_V_apres_relachement_de_V_rouvre()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);
        Relacher(automate, V);

        // Win reste tenue : un nouvel appui sur V est une nouvelle demande, pas une répétition.
        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V));
    }

    [Fact]
    public void Le_relachement_de_Win_apres_un_V_avale_masque_le_menu_Demarrer()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);
        Relacher(automate, V);

        Assert.Equal(ActionCrochet.MasquerPuisLaisser, Relacher(automate, Win));
    }

    [Fact]
    public void Le_masque_n_est_envoye_qu_une_fois()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);
        Assert.Equal(ActionCrochet.MasquerPuisLaisser, Relacher(automate, Win));

        // Le second relâchement (autre touche Windows, ou répétition) ne doit plus rien envoyer.
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, Win));
    }

    [Fact]
    public void Win_relachee_avant_V_masque_puis_la_fin_de_V_est_avalee()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);

        // L'utilisateur lâche Win avant V : le masque part, et le relâchement de V, dont l'enfoncement a été avalé, l'est aussi.
        Assert.Equal(ActionCrochet.MasquerPuisLaisser, Relacher(automate, Win, winReelle: false));
        Assert.Equal(ActionCrochet.Avaler, Relacher(automate, V, winReelle: false));
        // V est ensuite une touche ordinaire.
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V, winReelle: false));
    }

    [Fact]
    public void Un_nouveau_Win_V_apres_une_sequence_interrompue_ouvre_encore()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);
        Relacher(automate, Win, winReelle: false); // V jamais relâchée vue par le hook (fenêtre élevée par exemple)

        Appuyer(automate, Win);

        // Une nouvelle pression de Win recommence la séquence : V n'est plus traitée comme une répétition.
        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V));
    }

    [Fact]
    public void Win_seule_est_laissee_aux_deux_transitions()
    {
        var automate = new AutomateWinV();

        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, Win));
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, Win));
    }

    [Fact]
    public void Win_seule_ne_demande_aucun_masque()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Relacher(automate, Win);

        // Win, Win : le menu Démarrer doit s'ouvrir normalement, aucun masque ne traîne.
        Appuyer(automate, Win);
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, Win));
    }

    [Fact]
    public void V_sans_Win_est_laissee()
    {
        var automate = new AutomateWinV();

        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V, winReelle: false));
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, V, winReelle: false));
    }

    [Fact]
    public void Win_plus_une_autre_touche_est_laissee_et_le_relachement_de_Win_aussi()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);

        // Win+E, Win+Maj+S, etc. restent aux mains de Windows.
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, Autre));
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, Autre));
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, Win));
    }

    [Fact]
    public void Une_frappe_injectee_est_toujours_laissee_meme_pour_Win_V()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);

        // La priorité de l'injection sur le reste : sans elle, ce V injecté serait avalé et ouvrirait la fenêtre.
        var action = automate.Traiter(V, enfoncee: true, injectee: true, winReellementEnfoncee: true);

        Assert.Equal(ActionCrochet.Laisser, action);
    }

    [Fact]
    public void Une_frappe_injectee_de_Win_ne_change_pas_l_etat()
    {
        var automate = new AutomateWinV();

        automate.Traiter(Win, enfoncee: true, injectee: true, winReellementEnfoncee: true);

        // Win injectée n'a pas armé l'automate : un V réel qui suit n'est pas une demande, même si GetAsyncKeyState
        // dit que Win est enfoncée (c'est alors la frappe injectée elle-même qui l'enfonce).
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V, winReelle: true));
    }

    [Fact]
    public void Le_masque_injecte_du_hook_lui_meme_est_laisse()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);

        // Le hook reçoit aussi la touche 0xE8 qu'il vient d'injecter : elle ne doit rien déclencher.
        var action = automate.Traiter(0xE8, enfoncee: true, injectee: true, winReellementEnfoncee: true);

        Assert.Equal(ActionCrochet.Laisser, action);
        Assert.Equal(ActionCrochet.MasquerPuisLaisser, Relacher(automate, Win));
    }

    [Fact]
    public void V_quand_Win_n_est_plus_reellement_enfoncee_est_laissee_et_remet_a_zero()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);

        // Le relâchement de Win est passé inaperçu (écran de verrouillage, fenêtre élevée) : l'état est périmé.
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V, winReelle: false));

        // Remise à zéro : même si Win est de nouveau signalée enfoncée, l'automate n'a pas vu son appui.
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V, winReelle: true));
    }

    [Fact]
    public void La_remise_a_zero_par_V_perimee_ne_laisse_aucun_masque_en_attente()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);
        Appuyer(automate, V, winReelle: false); // état périmé détecté : tout est oublié

        Appuyer(automate, Win);
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, Win));
    }

    [Fact]
    public void Reinitialiser_oublie_Win_enfoncee()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);

        automate.Reinitialiser();

        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V));
    }

    [Fact]
    public void Reinitialiser_oublie_le_V_avale_et_le_masque()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);

        automate.Reinitialiser();

        // Plus de masque en attente : le relâchement de Win est laissé tel quel.
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, Win));

        // Et V n'est plus considérée comme déjà avalée : une nouvelle séquence ouvre.
        Appuyer(automate, Win);
        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V));
    }

    [Fact]
    public void Deux_touches_Windows_tenues_gardent_la_sequence_active_apres_le_relachement_de_l_une()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, WinDroite);
        Relacher(automate, Win);

        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V));
    }
}
