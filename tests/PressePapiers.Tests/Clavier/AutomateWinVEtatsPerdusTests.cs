using PressePapiers.Coeur.Clavier;

namespace PressePapiers.Tests.Clavier;

/// <summary>
/// Cas limites de l'automate Win+V : répétitions de Win, relâchements sans enfoncement avalé, relâchements que le hook
/// n'a pas vus (fenêtre élevée, écran de verrouillage).
/// </summary>
public sealed class AutomateWinVEtatsPerdusTests
{
    private const uint Win = AutomateWinV.VkWinGauche;
    private const uint V = AutomateWinV.VkV;

    private static ActionCrochet Appuyer(AutomateWinV a, uint touche, bool winReelle = true) =>
        a.Traiter(touche, enfoncee: true, injectee: false, winReellementEnfoncee: winReelle);

    private static ActionCrochet Relacher(AutomateWinV a, uint touche, bool winReelle = true) =>
        a.Traiter(touche, enfoncee: false, injectee: false, winReellementEnfoncee: winReelle);

    [Fact]
    public void La_repetition_de_Win_ne_fait_pas_rouvrir_pendant_la_repetition_de_V()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V));

        // Win et V tenues : le clavier répète les deux. Sans la garde « Win déjà enfoncée », la répétition de Win
        // remettrait V à « non avalée » et la répétition de V rouvrirait la fenêtre.
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, Win));
        Assert.Equal(ActionCrochet.Avaler, Appuyer(automate, V));
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, Win));
        Assert.Equal(ActionCrochet.Avaler, Appuyer(automate, V));
    }

    [Fact]
    public void La_repetition_de_Win_ne_fait_pas_oublier_le_masque_a_envoyer()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);

        Appuyer(automate, Win);

        Relacher(automate, V);
        Assert.Equal(ActionCrochet.MasquerPuisLaisser, Relacher(automate, Win));
    }

    [Fact]
    public void Le_relachement_de_V_sans_enfoncement_avale_est_laisse_passer()
    {
        var automate = new AutomateWinV();

        // V tenue avant Win : l'application a vu l'enfoncement, elle doit voir le relâchement.
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V, winReelle: false));
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, Win));
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, V));
    }

    [Fact]
    public void Un_relachement_de_V_en_trop_apres_une_sequence_complete_est_laisse_passer()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);
        Assert.Equal(ActionCrochet.Avaler, Relacher(automate, V));

        // Le premier relâchement a soldé l'enfoncement avalé : un second n'a plus rien à solder.
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, V));
    }

    [Fact]
    public void Un_enfoncement_de_V_sans_Win_alors_que_V_est_dite_avalee_est_une_frappe_neuve()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);
        Relacher(automate, Win, winReelle: false);
        // Le relâchement de V n'a jamais été vu. Plus tard, l'utilisateur tape un « v » ordinaire.

        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V, winReelle: false));
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, V, winReelle: false));
    }

    [Fact]
    public void La_frappe_neuve_remet_tout_a_zero_et_Win_V_ouvre_encore_ensuite()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);
        Relacher(automate, Win, winReelle: false);
        Appuyer(automate, V, winReelle: false);
        Relacher(automate, V, winReelle: false);

        Appuyer(automate, Win);

        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V));
    }

    [Fact]
    public void La_frappe_neuve_ne_laisse_aucun_masque_en_attente()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win);
        Appuyer(automate, V);
        Relacher(automate, Win, winReelle: false);
        Appuyer(automate, V, winReelle: false);

        // Win seule ensuite : le menu Démarrer doit s'ouvrir normalement.
        Appuyer(automate, Win);
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, Win));
    }
}
