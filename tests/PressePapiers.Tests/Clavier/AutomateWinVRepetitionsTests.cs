using PressePapiers.Coeur.Clavier;

namespace PressePapiers.Tests.Clavier;

/// <summary>
/// Win+V dont Win est lâchée avant V (les répétitions de V restent avalées, une frappe neuve passe) et Win+Ctrl+V
/// (raccourci de Windows laissé passer sans oublier que Win est tenue).
/// </summary>
public sealed class AutomateWinVRepetitionsTests
{
    private const uint Win = AutomateWinV.VkWinGauche;
    private const uint V = AutomateWinV.VkV;

    private static ActionCrochet Appuyer(AutomateWinV a, uint touche, bool winReelle, uint temps, bool autre = false) =>
        a.Traiter(touche, enfoncee: true, injectee: false, winReellementEnfoncee: winReelle, temps: temps,
                  autreModificateurEnfonce: autre);

    private static ActionCrochet Relacher(AutomateWinV a, uint touche, bool winReelle, uint temps) =>
        a.Traiter(touche, enfoncee: false, injectee: false, winReellementEnfoncee: winReelle, temps: temps);

    // Win+V ouvre à l'instant donné, puis Win est lâchée avant V (le masque part) : V reste tenue.
    private static AutomateWinV WinVPuisWinLachee(uint tempsV, uint delaiRepetitionClavierMs = 500)
    {
        var automate = new AutomateWinV(delaiRepetitionClavierMs);
        Appuyer(automate, Win, winReelle: true, temps: tempsV - 100);
        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V, winReelle: true, temps: tempsV));
        Assert.Equal(ActionCrochet.MasquerPuisLaisser, Relacher(automate, Win, winReelle: false, temps: tempsV + 10));
        return automate;
    }

    [Fact]
    public void Une_repetition_de_V_a_33_ms_apres_le_relachement_de_Win_est_avalee()
    {
        var automate = WinVPuisWinLachee(1000);

        Assert.Equal(ActionCrochet.Avaler, Appuyer(automate, V, winReelle: false, temps: 1033));
    }

    [Fact]
    public void Une_serie_de_repetitions_est_avalee_jusqu_au_relachement_de_V()
    {
        var automate = WinVPuisWinLachee(1000);

        // Première répétition après le délai du clavier (500 ms), puis une toutes les 33 ms.
        Assert.Equal(ActionCrochet.Avaler, Appuyer(automate, V, winReelle: false, temps: 1500));
        for (uint temps = 1533; temps < 3000; temps += 33)
            Assert.Equal(ActionCrochet.Avaler, Appuyer(automate, V, winReelle: false, temps: temps));

        Assert.Equal(ActionCrochet.Avaler, Relacher(automate, V, winReelle: false, temps: 3010));

        // V est ensuite une touche ordinaire.
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V, winReelle: false, temps: 3200));
    }

    [Fact]
    public void Une_frappe_neuve_deux_secondes_apres_est_laissee_et_remet_a_zero()
    {
        var automate = WinVPuisWinLachee(1000);

        // Le relâchement de V n'a pas été vu : un V deux secondes plus tard est une vraie frappe.
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V, winReelle: false, temps: 3000));
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, V, winReelle: false, temps: 3050));

        // Tout est oublié : le V suivant n'est plus une répétition, et Win+V ouvre à nouveau.
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V, winReelle: false, temps: 3060));
        Appuyer(automate, Win, winReelle: true, temps: 4000);
        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V, winReelle: true, temps: 4010));
    }

    [Fact]
    public void Le_compteur_du_systeme_qui_repasse_par_zero_ne_casse_pas_la_detection()
    {
        // Le champ time est un compteur 32 bits : V à 16 ms avant le retour à zéro, répétition 32 ms plus tard.
        var automate = WinVPuisWinLachee(0xFFFFFFF0);

        Assert.Equal(ActionCrochet.Avaler, Appuyer(automate, V, winReelle: false, temps: 0x10));
        Assert.Equal(ActionCrochet.Avaler, Appuyer(automate, V, winReelle: false, temps: 0x31));

        // Une frappe neuve bien après le retour à zéro reste reconnue comme telle.
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V, winReelle: false, temps: 3000));
    }

    [Fact]
    public void Le_delai_de_repetition_du_clavier_fixe_la_limite_a_ce_delai_plus_150_ms()
    {
        // Clavier réglé sur 250 ms : limite à 400 ms.
        var automate = WinVPuisWinLachee(1000, delaiRepetitionClavierMs: 250);
        Assert.Equal(ActionCrochet.Avaler, Appuyer(automate, V, winReelle: false, temps: 1399));

        var autre = WinVPuisWinLachee(1000, delaiRepetitionClavierMs: 250);
        Assert.Equal(ActionCrochet.Laisser, Appuyer(autre, V, winReelle: false, temps: 1400));
    }

    [Fact]
    public void Win_Ctrl_V_est_laissee_puis_V_avec_Win_toujours_tenue_ouvre()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win, winReelle: true, temps: 100);

        // Win+Ctrl+V : raccourci de Windows, laissé passer.
        Assert.Equal(ActionCrochet.Laisser, Appuyer(automate, V, winReelle: true, temps: 200, autre: true));
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, V, winReelle: true, temps: 250));

        // Ctrl est lâchée, Win reste tenue : l'automate n'a pas oublié Win, ce V ouvre.
        Assert.Equal(ActionCrochet.AvalerEtOuvrir, Appuyer(automate, V, winReelle: true, temps: 400));
    }

    [Fact]
    public void Win_Ctrl_V_ne_laisse_aucun_masque_en_attente_au_relachement_de_Win()
    {
        var automate = new AutomateWinV();
        Appuyer(automate, Win, winReelle: true, temps: 100);
        Appuyer(automate, V, winReelle: true, temps: 200, autre: true);
        Relacher(automate, V, winReelle: true, temps: 250);

        // Windows a vu Win+Ctrl+V : le menu Démarrer ne s'ouvrira pas, aucune touche sans effet n'est utile.
        Assert.Equal(ActionCrochet.Laisser, Relacher(automate, Win, winReelle: false, temps: 500));
    }
}
