using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Reglages;

public sealed class ServiceParametresTests : IDisposable
{
    private readonly DossierTemporaire _dossier = new();
    private readonly JournalEspion _journal = new();

    public void Dispose() => _dossier.Dispose();

    [Fact]
    public void Un_fichier_absent_donne_les_valeurs_par_defaut_sans_avertissement()
    {
        var service = Creer();

        var parametres = service.Charger();

        AssertDefauts(parametres);
        Assert.Empty(_journal.Avertissements);
    }

    [Fact]
    public void Les_valeurs_par_defaut_sont_celles_de_la_spec()
    {
        var parametres = new Parametres();

        Assert.Equal("Ctrl+Alt+Maj+V", parametres.RaccourciSecours);
        Assert.True(parametres.DemarrageAvecWindows);
        Assert.Equal(ModeCollage.TexteSeul, parametres.MiseEnFormeParDefaut);
        Assert.Equal(5000, parametres.PlafondTemporaire);
        Assert.Equal(ThemeApplication.Systeme, parametres.Theme);
        Assert.Equal(["WindowsTerminal", "Code", "devenv", "powershell", "pwsh", "cmd", "Ssms", "rider64", "notepad++"], parametres.ApplicationsChasseFixe);
        Assert.Equal(920, parametres.LargeurFenetre);
        Assert.Equal(560, parametres.HauteurFenetre);
    }

    [Fact]
    public void Un_aller_retour_conserve_tous_les_reglages()
    {
        var service = Creer();
        var ecrits = new Parametres
        {
            RaccourciSecours = "Ctrl+Alt+F9",
            DemarrageAvecWindows = false,
            MiseEnFormeParDefaut = ModeCollage.AvecMiseEnForme,
            PlafondTemporaire = 0,
            Theme = ThemeApplication.Sombre,
            ApplicationsChasseFixe = ["Code", "Mon éditeur", "x\"y"],
            LargeurFenetre = 1111,
            HauteurFenetre = 777,
        };

        service.Enregistrer(ecrits);
        var lus = Creer().Charger();

        Assert.Equal(ecrits.RaccourciSecours, lus.RaccourciSecours);
        Assert.Equal(ecrits.DemarrageAvecWindows, lus.DemarrageAvecWindows);
        Assert.Equal(ecrits.MiseEnFormeParDefaut, lus.MiseEnFormeParDefaut);
        Assert.Equal(ecrits.PlafondTemporaire, lus.PlafondTemporaire);
        Assert.Equal(ecrits.Theme, lus.Theme);
        Assert.Equal(ecrits.ApplicationsChasseFixe, lus.ApplicationsChasseFixe);
        Assert.Equal(ecrits.LargeurFenetre, lus.LargeurFenetre);
        Assert.Equal(ecrits.HauteurFenetre, lus.HauteurFenetre);
        Assert.Empty(_journal.Avertissements);
    }

    [Fact]
    public void Le_fichier_est_indente_et_ecrit_les_enumerations_en_texte()
    {
        Creer().Enregistrer(new Parametres { Theme = ThemeApplication.Clair, MiseEnFormeParDefaut = ModeCollage.AvecMiseEnForme });

        var contenu = File.ReadAllText(Chemin);

        Assert.Contains("\"Theme\": \"Clair\"", contenu);
        Assert.Contains("\"MiseEnFormeParDefaut\": \"AvecMiseEnForme\"", contenu);
        Assert.Contains("\n", contenu);
        Assert.Contains("  \"RaccourciSecours\"", contenu);
        Assert.False(File.ReadAllBytes(Chemin).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }), "pas de marque d'ordre des octets");
    }

    [Fact]
    public void Enregistrer_cree_le_dossier_manquant_et_ne_laisse_aucun_fichier_temporaire()
    {
        var chemin = _dossier.Fichier(Path.Combine("sous", "dossier", "parametres.json"));
        var service = new ServiceParametres(chemin, _journal);

        service.Enregistrer(new Parametres { PlafondTemporaire = 42 });

        Assert.True(File.Exists(chemin));
        Assert.Equal([chemin], Directory.GetFiles(Path.GetDirectoryName(chemin)!));
        Assert.Equal(42, service.Charger().PlafondTemporaire);
    }

    [Fact]
    public void Enregistrer_remplace_le_fichier_existant_sans_rien_laisser_a_cote()
    {
        var service = Creer();
        service.Enregistrer(new Parametres { PlafondTemporaire = 1 });

        service.Enregistrer(new Parametres { PlafondTemporaire = 2 });

        Assert.Equal(2, service.Charger().PlafondTemporaire);
        Assert.Equal([Chemin], Directory.GetFiles(_dossier.Chemin));
    }

    [Fact]
    public void Un_ancien_temporaire_verrouille_n_empeche_pas_d_enregistrer()
    {
        var service = Creer();
        Directory.CreateDirectory(_dossier.Chemin);

        // Un reste de coupure sous l'ancien nom fixe, tenu ouvert : un nom de fichier temporaire unique l'ignore.
        using (new FileStream(Chemin + ".tmp", FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            service.Enregistrer(new Parametres { PlafondTemporaire = 9 });
        }

        Assert.Equal(9, service.Charger().PlafondTemporaire);
    }

    [Fact]
    public void Deux_enregistrements_simultanes_ne_se_genent_pas_et_laissent_un_fichier_valide()
    {
        var service = Creer();
        service.Enregistrer(new Parametres());

        // Avec un seul nom de fichier temporaire, deux écritures en même temps se heurteraient (partage refusé ou fichier mélangé).
        Parallel.For(0, 200, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                     i => service.Enregistrer(new Parametres { PlafondTemporaire = i + 1 }));

        Assert.Equal([Chemin], Directory.GetFiles(_dossier.Chemin));
        Assert.Empty(_journal.Avertissements);
        Assert.InRange(service.Charger().PlafondTemporaire, 1, 200);
    }

    [Fact]
    public void Un_echec_d_ecriture_laisse_l_ancien_fichier_intact_et_supprime_le_temporaire()
    {
        var service = Creer();
        service.Enregistrer(new Parametres { PlafondTemporaire = 7 });

        // Un lecteur qui interdit le remplacement du fichier fait échouer le déplacement final.
        using (new FileStream(Chemin, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.ThrowsAny<Exception>(() => service.Enregistrer(new Parametres { PlafondTemporaire = 8 }));
        }

        Assert.Equal([Chemin], Directory.GetFiles(_dossier.Chemin));
        Assert.Equal(7, service.Charger().PlafondTemporaire);
    }

    [Theory]
    [InlineData("{ pas du json")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("\"texte\"")]
    [InlineData("{\"Theme\": \"Rose\"}")]
    [InlineData("{\"Theme\": 1}")]
    [InlineData("{\"MiseEnFormeParDefaut\": 9}")]
    [InlineData("{\"PlafondTemporaire\": \"beaucoup\"}")]
    [InlineData("{\"LargeurFenetre\": 1e99}")]
    [InlineData("{\"ApplicationsChasseFixe\": \"Code\"}")]
    [InlineData("{\"DemarrageAvecWindows\": \"oui\"}")]
    [InlineData("{\"Theme\": \"Clair\"")]
    public void Un_fichier_corrompu_donne_les_valeurs_par_defaut_avec_un_avertissement(string contenu)
    {
        File.WriteAllText(Chemin, contenu);

        var parametres = Creer().Charger();

        AssertDefauts(parametres);
        Assert.Single(_journal.Avertissements);
    }

    [Fact]
    public void Un_fichier_binaire_donne_les_valeurs_par_defaut_avec_un_avertissement()
    {
        File.WriteAllBytes(Chemin, [0xFF, 0xFE, 0x00, 0x01, 0x80, 0x81, 0x00, 0x00, 0xC3, 0x28]);

        var parametres = Creer().Charger();

        AssertDefauts(parametres);
        Assert.Single(_journal.Avertissements);
    }

    [Fact]
    public void Un_fichier_partiel_garde_les_valeurs_manquantes_par_defaut()
    {
        File.WriteAllText(Chemin, "{ \"Theme\": \"Clair\", \"PlafondTemporaire\": 100 }");

        var parametres = Creer().Charger();

        Assert.Equal(ThemeApplication.Clair, parametres.Theme);
        Assert.Equal(100, parametres.PlafondTemporaire);
        Assert.Equal("Ctrl+Alt+Maj+V", parametres.RaccourciSecours);
        Assert.Equal(920, parametres.LargeurFenetre);
        Assert.Equal(new Parametres().ApplicationsChasseFixe, parametres.ApplicationsChasseFixe);
        Assert.Empty(_journal.Avertissements);
    }

    [Fact]
    public void Un_fichier_modifie_a_la_main_est_tolere()
    {
        // Casse libre, commentaires, virgule finale, propriété inconnue, texte d'énumération en minuscules.
        File.WriteAllText(Chemin, """
            {
              // thème choisi à la main
              "theme": "sombre",
              "inconnue": 12,
              "plafondTemporaire": 250,
            }
            """);

        var parametres = Creer().Charger();

        Assert.Equal(ThemeApplication.Sombre, parametres.Theme);
        Assert.Equal(250, parametres.PlafondTemporaire);
        Assert.Empty(_journal.Avertissements);
    }

    [Fact]
    public void Un_fichier_avec_marque_d_ordre_des_octets_est_lu()
    {
        File.WriteAllText(Chemin, "{ \"PlafondTemporaire\": 33 }", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Assert.Equal(33, Creer().Charger().PlafondTemporaire);
    }

    [Fact]
    public void Des_valeurs_lisibles_mais_inutilisables_sont_remplacees_une_a_une_avec_un_avertissement()
    {
        File.WriteAllText(Chemin, """
            {
              "Theme": "Sombre",
              "RaccourciSecours": null,
              "ApplicationsChasseFixe": null,
              "PlafondTemporaire": -5,
              "LargeurFenetre": 10,
              "HauteurFenetre": -1
            }
            """);

        var parametres = Creer().Charger();

        Assert.Equal(ThemeApplication.Sombre, parametres.Theme);
        Assert.Equal("Ctrl+Alt+Maj+V", parametres.RaccourciSecours);
        Assert.Equal(new Parametres().ApplicationsChasseFixe, parametres.ApplicationsChasseFixe);
        Assert.Equal(5000, parametres.PlafondTemporaire);
        Assert.Equal(920, parametres.LargeurFenetre);
        Assert.Equal(560, parametres.HauteurFenetre);
        Assert.Single(_journal.Avertissements);
    }

    [Fact]
    public void Une_valeur_inconnue_ne_fait_perdre_que_sa_propre_propriete()
    {
        File.WriteAllText(Chemin, """
            {
              "Theme": "Rose",
              "PlafondTemporaire": 123,
              "RaccourciSecours": "Ctrl+Alt+K",
              "DemarrageAvecWindows": false,
              "MiseEnFormeParDefaut": "MiseEnForme",
              "LargeurFenetre": 1000
            }
            """);

        var parametres = Creer().Charger();

        Assert.Equal(ThemeApplication.Systeme, parametres.Theme);
        Assert.Equal(123, parametres.PlafondTemporaire);
        Assert.Equal("Ctrl+Alt+K", parametres.RaccourciSecours);
        Assert.False(parametres.DemarrageAvecWindows);
        Assert.Equal(1000, parametres.LargeurFenetre);
        var avertissement = Assert.Single(_journal.Avertissements);
        Assert.Contains("Theme", avertissement);
        Assert.DoesNotContain("PlafondTemporaire", avertissement);
    }

    [Theory]
    [InlineData("{\"Theme\": 1, \"LargeurFenetre\": 700}", 700)]
    [InlineData("{\"MiseEnFormeParDefaut\": 9, \"LargeurFenetre\": 700}", 700)]
    [InlineData("{\"DemarrageAvecWindows\": \"oui\", \"LargeurFenetre\": 700}", 700)]
    [InlineData("{\"PlafondTemporaire\": \"beaucoup\", \"LargeurFenetre\": 700}", 700)]
    [InlineData("{\"ApplicationsChasseFixe\": \"Code\", \"LargeurFenetre\": 700}", 700)]
    [InlineData("{\"HauteurFenetre\": null, \"LargeurFenetre\": 700}", 700)]
    public void Chaque_sorte_de_valeur_invalide_laisse_les_autres_reglages_intacts(string contenu, int largeur)
    {
        File.WriteAllText(Chemin, contenu);

        var parametres = Creer().Charger();

        Assert.Equal(largeur, parametres.LargeurFenetre);
        Assert.Equal(new Parametres().HauteurFenetre, parametres.HauteurFenetre);
        Assert.Equal(new Parametres().Theme, parametres.Theme);
        Assert.Single(_journal.Avertissements);
    }

    [Fact]
    public void Les_elements_null_de_la_liste_des_applications_sont_ecartes_et_le_reste_garde()
    {
        File.WriteAllText(Chemin, "{ \"ApplicationsChasseFixe\": [\"Code\", null, \"cmd\", null] }");

        var parametres = Creer().Charger();

        Assert.Equal(["Code", "cmd"], parametres.ApplicationsChasseFixe);
        Assert.Single(_journal.Avertissements);
    }

    [Fact]
    public void Un_plafond_a_zero_et_une_liste_vide_sont_des_choix_valides()
    {
        File.WriteAllText(Chemin, "{ \"PlafondTemporaire\": 0, \"ApplicationsChasseFixe\": [] }");

        var parametres = Creer().Charger();

        Assert.Equal(0, parametres.PlafondTemporaire);
        Assert.Empty(parametres.ApplicationsChasseFixe);
        Assert.Empty(_journal.Avertissements);
    }

    [Fact]
    public void Un_fichier_corrompu_est_remplace_par_un_fichier_valide_a_l_enregistrement()
    {
        File.WriteAllText(Chemin, "{ abîmé");
        var service = Creer();
        service.Charger();
        Assert.Single(_journal.Avertissements);

        service.Enregistrer(new Parametres { Theme = ThemeApplication.Clair });

        Assert.Equal(ThemeApplication.Clair, service.Charger().Theme);
    }

    [Fact]
    public void Le_constructeur_et_Enregistrer_refusent_les_arguments_nuls()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceParametres(Chemin, null!));
        Assert.ThrowsAny<ArgumentException>(() => new ServiceParametres("  ", _journal));
        Assert.Throws<ArgumentNullException>(() => Creer().Enregistrer(null!));
    }

    private string Chemin => _dossier.Fichier("parametres.json");

    private ServiceParametres Creer() => new(Chemin, _journal);

    private static void AssertDefauts(Parametres parametres)
    {
        var defauts = new Parametres();
        Assert.Equal(defauts.RaccourciSecours, parametres.RaccourciSecours);
        Assert.Equal(defauts.DemarrageAvecWindows, parametres.DemarrageAvecWindows);
        Assert.Equal(defauts.MiseEnFormeParDefaut, parametres.MiseEnFormeParDefaut);
        Assert.Equal(defauts.PlafondTemporaire, parametres.PlafondTemporaire);
        Assert.Equal(defauts.Theme, parametres.Theme);
        Assert.Equal(defauts.ApplicationsChasseFixe, parametres.ApplicationsChasseFixe);
        Assert.Equal(defauts.LargeurFenetre, parametres.LargeurFenetre);
        Assert.Equal(defauts.HauteurFenetre, parametres.HauteurFenetre);
    }

    // Journal qui retient les avertissements, propre à ce fichier de tests.
    private sealed class JournalEspion : IJournal
    {
        public List<string> Avertissements { get; } = [];

        public void Info(string message)
        {
        }

        public void Avertissement(string message, Exception? exception = null) => Avertissements.Add(message);

        public void Erreur(string message, Exception? exception = null)
        {
        }
    }
}
