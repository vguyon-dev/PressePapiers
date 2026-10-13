using System.Globalization;
using System.Text;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Recherche;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class DepotElementsTests : IDisposable
{
    private readonly DepotDeTest _banc = new();

    private DepotHistorique Depot => _banc.Depot;

    private long Temporaire => Depot.OngletTemporaire.Id;

    public void Dispose() => _banc.Dispose();

    // ---- Ajout ----

    [Fact]
    public void Ajouter_un_texte_cree_un_element_complet_dans_Temporaire()
    {
        var contenu = ContenusDeTest.Texte("Bonjour é", "Code");

        var element = Depot.AjouterCopie(contenu);

        Assert.True(element.Id > 0);
        Assert.Equal(Temporaire, element.OngletId);
        Assert.Null(element.Nom);
        Assert.Equal(TypeElement.Texte, element.Type);
        Assert.Equal("Bonjour é", element.Texte);
        Assert.Equal("Code", element.Application);
        Assert.Equal(_banc.Horloge.GetUtcNow(), element.DateCopie);
        Assert.Equal(_banc.Horloge.GetUtcNow(), element.DateUsage);
        Assert.Equal(Encoding.UTF8.GetByteCount("Bonjour é"), element.Taille);
        Assert.Equal(Empreinte.Calculer(contenu), element.Empreinte);
        Assert.Null(element.ImageFichier);
        Assert.Equal("bonjour e code", TexteRecherche(element.Id));
    }

    [Fact]
    public void L_element_renvoye_est_identique_a_l_element_relu()
    {
        var ajoute = Depot.AjouterCopie(ContenusDeTest.MisEnForme("Titre", "Word"));

        var relu = Depot.Lire(ajoute.Id);

        Assert.NotNull(relu);
        Assert.Equal(ajoute.Id, relu.Id);
        Assert.Equal(ajoute.OngletId, relu.OngletId);
        Assert.Equal(ajoute.Type, relu.Type);
        Assert.Equal(ajoute.Texte, relu.Texte);
        Assert.Equal(ajoute.Rtf, relu.Rtf);
        Assert.Equal(ajoute.Html, relu.Html);
        Assert.Equal(ajoute.Empreinte, relu.Empreinte);
        Assert.Equal(ajoute.Application, relu.Application);
        Assert.Equal(ajoute.DateCopie, relu.DateCopie);
        Assert.Equal(ajoute.DateUsage, relu.DateUsage);
        Assert.Equal(ajoute.Taille, relu.Taille);
    }

    [Fact]
    public void Ajouter_un_texte_mis_en_forme_garde_le_rtf_et_le_html()
    {
        var contenu = ContenusDeTest.MisEnForme("Titre");

        var element = Depot.AjouterCopie(contenu);

        Assert.Equal(TypeElement.MisEnForme, element.Type);
        Assert.Equal(contenu.Rtf, element.Rtf);
        Assert.Equal(contenu.Html, element.Html);
        Assert.Equal(contenu.Rtf, Depot.Lire(element.Id)!.Rtf);
        Assert.Equal(contenu.Html, Depot.Lire(element.Id)!.Html);
    }

    [Fact]
    public void Ajouter_une_image_ecrit_le_png_sous_un_nom_guid_et_garde_la_miniature()
    {
        var png = ContenusDeTest.Png(1, 300);

        var element = Depot.AjouterCopie(ContenusDeTest.Image(png, "Paint"));

        Assert.Equal(TypeElement.Image, element.Type);
        Assert.Equal(".png", Path.GetExtension(element.ImageFichier));
        Assert.True(Guid.TryParseExact(Path.GetFileNameWithoutExtension(element.ImageFichier), "N", out _));
        Assert.Equal(png, File.ReadAllBytes(Path.Combine(_banc.Dossiers.Images, element.ImageFichier!)));
        Assert.Equal([element.ImageFichier], _banc.NomsImages());
        Assert.Equal<byte[]>([9, 8, 7], element.Miniature);
        Assert.Equal<byte[]>([9, 8, 7], Depot.Lire(element.Id)!.Miniature);
        Assert.Equal(300, element.Taille);
        Assert.Null(element.Texte);
        // Une image se retrouve par son application d'origine.
        Assert.Equal("paint", TexteRecherche(element.Id));
    }

    [Fact]
    public void Ajouter_une_image_cree_le_dossier_des_images_s_il_manque()
    {
        using var banc = new DepotDeTest(creerDossiers: false);
        Assert.False(Directory.Exists(banc.Dossiers.Images));

        var element = banc.Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(2)));

        Assert.True(File.Exists(Path.Combine(banc.Dossiers.Images, element.ImageFichier!)));
    }

    [Fact]
    public void Ajouter_des_fichiers_les_joint_par_des_retours_a_la_ligne()
    {
        string[] chemins = ["C:\\Dossier\\Rapport.docx", "C:\\Dossier\\Chiffres.xlsx"];

        var element = Depot.AjouterCopie(ContenusDeTest.Fichiers(chemins));

        Assert.Equal(TypeElement.Fichiers, element.Type);
        Assert.Equal("C:\\Dossier\\Rapport.docx\nC:\\Dossier\\Chiffres.xlsx", element.Texte);
        Assert.Equal(chemins, element.Fichiers);
        Assert.Equal(Encoding.UTF8.GetByteCount(element.Texte!), element.Taille);
        // Chaque chemin figure une seule fois dans le texte de recherche.
        Assert.Equal(Normalisation.TexteRecherche(null, null, chemins, null), TexteRecherche(element.Id));
    }

    [Fact]
    public void Ajouter_un_contenu_sans_son_contenu_principal_leve_ArgumentException_sans_rien_ecrire()
    {
        var texteAbsent = new ContenuCopie { Type = TypeElement.Texte };
        var imageAbsente = new ContenuCopie { Type = TypeElement.Image };

        Assert.Throws<ArgumentException>(() => Depot.AjouterCopie(texteAbsent));
        Assert.Throws<ArgumentException>(() => Depot.AjouterCopie(imageAbsente));

        Assert.Equal(0, Depot.CompterElements(Temporaire));
        Assert.Empty(_banc.NomsImages());
    }

    [Fact]
    public void Un_texte_de_9_Mo_est_enregistre_et_relu_intact()
    {
        var gros = new string('é', 4_500_000) + new string('x', 4_500_000);

        var element = Depot.AjouterCopie(ContenusDeTest.Texte(gros));

        Assert.Equal(gros, Depot.Lire(element.Id)!.Texte);
        Assert.Equal(Encoding.UTF8.GetByteCount(gros), element.Taille);
    }

    // ---- Dédoublonnage ----

    [Fact]
    public void Recopier_le_meme_texte_remonte_l_element_au_lieu_d_en_creer_un_autre()
    {
        var premier = Depot.AjouterCopie(ContenusDeTest.Texte("abc", "Code"));
        _banc.Horloge.Advance(TimeSpan.FromMinutes(5));

        var second = Depot.AjouterCopie(ContenusDeTest.Texte("abc", "Notepad"));

        Assert.Equal(premier.Id, second.Id);
        Assert.Equal(premier.DateCopie, second.DateCopie);
        Assert.Equal(_banc.Horloge.GetUtcNow(), second.DateUsage);
        Assert.Equal("Notepad", second.Application);
        Assert.Equal(second.DateUsage, Depot.Lire(second.Id)!.DateUsage);
        Assert.Equal(1, Depot.CompterElements(Temporaire));
    }

    [Fact]
    public void Une_application_inconnue_ne_remplace_pas_l_application_connue()
    {
        var premier = Depot.AjouterCopie(ContenusDeTest.Texte("abc", "Code"));
        _banc.Horloge.Advance(TimeSpan.FromSeconds(1));

        var second = Depot.AjouterCopie(ContenusDeTest.Texte("abc"));

        Assert.Equal(premier.Id, second.Id);
        Assert.Equal("Code", second.Application);
        Assert.Equal("Code", Depot.Lire(premier.Id)!.Application);
    }

    [Fact]
    public void Le_texte_de_recherche_et_l_index_suivent_la_nouvelle_application()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("abc", "Notepad"));
        _banc.Horloge.Advance(TimeSpan.FromSeconds(1));

        Depot.AjouterCopie(ContenusDeTest.Texte("abc", "Chrome"));

        Assert.Equal("abc chrome", TexteRecherche(element.Id));
        Assert.Equal([element.Id], InsertionsSql.Trouver(_banc.Connexion, "\"chrome\""));
        Assert.Empty(InsertionsSql.Trouver(_banc.Connexion, "\"notepad\""));
    }

    [Fact]
    public void Un_element_recopie_passe_en_tete_de_la_liste()
    {
        var a = Depot.AjouterCopie(ContenusDeTest.Texte("aaa"));
        _banc.Horloge.Advance(TimeSpan.FromSeconds(1));
        var b = Depot.AjouterCopie(ContenusDeTest.Texte("bbb"));
        Assert.Equal([b.Id, a.Id], Ids(Depot.ListerElements(Temporaire)));
        _banc.Horloge.Advance(TimeSpan.FromSeconds(1));

        Depot.AjouterCopie(ContenusDeTest.Texte("aaa"));

        Assert.Equal([a.Id, b.Id], Ids(Depot.ListerElements(Temporaire)));
    }

    [Fact]
    public void Le_dedoublonnage_ne_s_applique_qu_a_Temporaire()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var original = Depot.AjouterCopie(ContenusDeTest.Texte("signature"));
        Depot.Nommer(original.Id, "Ma signature", onglet.Id);

        var nouveau = Depot.AjouterCopie(ContenusDeTest.Texte("signature"));

        // L'élément nommé a quitté Temporaire : la même copie y crée un nouvel élément, sans toucher à l'autre.
        Assert.NotEqual(original.Id, nouveau.Id);
        Assert.Equal(Temporaire, nouveau.OngletId);
        Assert.Equal(1, Depot.CompterElements(onglet.Id));
        Assert.Equal(1, Depot.CompterElements(Temporaire));
    }

    [Fact]
    public void Un_texte_et_un_fichier_de_meme_empreinte_restent_deux_elements()
    {
        var commeTexte = Depot.AjouterCopie(ContenusDeTest.Texte("C:\\a.txt"));
        var commeFichier = Depot.AjouterCopie(ContenusDeTest.Fichiers("C:\\a.txt"));

        Assert.Equal(commeTexte.Empreinte, commeFichier.Empreinte);
        Assert.NotEqual(commeTexte.Id, commeFichier.Id);
        Assert.Equal(2, Depot.CompterElements(Temporaire));

        // Chacun se dédoublonne avec son semblable, pas avec l'autre.
        Assert.Equal(commeTexte.Id, Depot.AjouterCopie(ContenusDeTest.Texte("C:\\a.txt")).Id);
        Assert.Equal(commeFichier.Id, Depot.AjouterCopie(ContenusDeTest.Fichiers("C:\\a.txt")).Id);
        Assert.Equal(2, Depot.CompterElements(Temporaire));
    }

    [Fact]
    public void Un_meme_texte_copie_avec_ou_sans_mise_en_forme_reste_un_seul_element()
    {
        var riche = Depot.AjouterCopie(ContenusDeTest.MisEnForme("Titre"));

        var brut = Depot.AjouterCopie(ContenusDeTest.Texte("Titre"));

        // La mise en forme n'entre pas dans l'empreinte : le premier enregistrement, qui en a une, est conservé tel quel.
        Assert.Equal(riche.Id, brut.Id);
        Assert.Equal(TypeElement.MisEnForme, Depot.Lire(riche.Id)!.Type);
        Assert.Equal(riche.Rtf, Depot.Lire(riche.Id)!.Rtf);
        Assert.Equal(1, Depot.CompterElements(Temporaire));
    }

    [Fact]
    public void Recopier_la_meme_image_n_ecrit_pas_un_second_png()
    {
        var png = ContenusDeTest.Png(3);
        var premiere = Depot.AjouterCopie(ContenusDeTest.Image(png));
        _banc.Horloge.Advance(TimeSpan.FromSeconds(1));

        var seconde = Depot.AjouterCopie(ContenusDeTest.Image(png));

        Assert.Equal(premiere.Id, seconde.Id);
        Assert.Equal(premiere.ImageFichier, seconde.ImageFichier);
        Assert.Equal([premiere.ImageFichier], _banc.NomsImages());
    }

    [Fact]
    public void Deux_images_differentes_ont_deux_png_aux_noms_differents()
    {
        var a = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(4)));
        var b = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(5)));

        Assert.NotEqual(a.ImageFichier, b.ImageFichier);
        Assert.Equal(2, _banc.NomsImages().Length);
    }

    // ---- Rafales de copies (point de vigilance 1) ----

    [Fact]
    public void Une_rafale_du_meme_contenu_a_la_meme_milliseconde_donne_un_seul_element()
    {
        long premierId = 0;
        for (var i = 0; i < 50; i++)
        {
            var element = Depot.AjouterCopie(ContenusDeTest.Texte("rafale", "Code"));
            premierId = i == 0 ? element.Id : premierId;
            Assert.Equal(premierId, element.Id);
        }

        Assert.Equal(1, Depot.CompterElements(Temporaire));
    }

    [Fact]
    public void Des_contenus_differents_a_la_meme_milliseconde_gardent_un_ordre_stable_le_dernier_en_tete()
    {
        // L'horloge ne bouge pas : les trois copies ont exactement la même date.
        var a = Depot.AjouterCopie(ContenusDeTest.Texte("aaa"));
        var b = Depot.AjouterCopie(ContenusDeTest.Texte("bbb"));
        var c = Depot.AjouterCopie(ContenusDeTest.Texte("ccc"));

        Assert.Equal([c.Id, b.Id, a.Id], Ids(Depot.ListerElements(Temporaire)));

        // Recopier A dans le même instant la remet en tête, sans changer l'ordre relatif des autres.
        Depot.AjouterCopie(ContenusDeTest.Texte("aaa"));

        var liste = Depot.ListerElements(Temporaire);
        Assert.Equal([a.Id, c.Id, b.Id], Ids(liste));
        // Le dépôt garantit des dates d'usage toutes différentes : l'ordre ne dépend pas du hasard des égalités.
        Assert.Equal(3, liste.Select(e => e.DateUsage).Distinct().Count());
    }

    [Fact]
    public void La_liste_est_triee_par_date_d_usage_puis_par_id_decroissants()
    {
        var onglet = Temporaire;
        var dixHeures = new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
        var neufHeures = dixHeures.AddHours(-1);
        var a = _banc.InsererElement(onglet, "aaa", dixHeures);
        var b = _banc.InsererElement(onglet, "bbb", neufHeures);
        var c = _banc.InsererElement(onglet, "ccc", dixHeures);
        var d = _banc.InsererElement(onglet, "ddd", neufHeures);

        // Date décroissante d'abord (a et c avant b et d), puis id décroissant à date égale.
        Assert.Equal([c, a, d, b], Ids(Depot.ListerElements(onglet)));
    }

    [Fact]
    public void Des_ajouts_simultanes_du_meme_contenu_ne_creent_qu_un_element_par_contenu()
    {
        Parallel.For(0, 200, i => Depot.AjouterCopie(ContenusDeTest.Texte("contenu " + (i % 10).ToString(CultureInfo.InvariantCulture))));

        Assert.Equal(10, Depot.CompterElements(Temporaire));
        Assert.Equal(10, Depot.ListerElements(Temporaire).Select(e => e.DateUsage).Distinct().Count());
    }

    // ---- Lecture ----

    [Fact]
    public void Lire_un_element_inconnu_renvoie_null()
    {
        Assert.Null(Depot.Lire(9999));
    }

    [Fact]
    public void Lister_et_compter_ne_portent_que_sur_l_onglet_demande()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var a = Depot.AjouterCopie(ContenusDeTest.Texte("aaa"));
        Depot.AjouterCopie(ContenusDeTest.Texte("bbb"));
        Depot.Nommer(a.Id, "A", onglet.Id);

        Assert.Equal(1, Depot.CompterElements(onglet.Id));
        Assert.Equal(1, Depot.CompterElements(Temporaire));
        Assert.Equal([a.Id], Ids(Depot.ListerElements(onglet.Id)));
        Assert.Empty(Depot.ListerElements(9999));
        Assert.Equal(0, Depot.CompterElements(9999));
    }

    // ---- Nommer ----

    [Fact]
    public void Nommer_donne_un_nom_et_range_l_element_dans_l_onglet_choisi()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("Adresse du client", "Outlook"));

        var nomme = Depot.Nommer(element.Id, "  Facturation  ", onglet.Id);

        Assert.Equal(element.Id, nomme.Id);
        Assert.Equal("Facturation", nomme.Nom);
        Assert.Equal(onglet.Id, nomme.OngletId);
        Assert.Equal(element.DateUsage, nomme.DateUsage);
        Assert.Equal(nomme.Nom, Depot.Lire(element.Id)!.Nom);
        Assert.Equal(0, Depot.CompterElements(Temporaire));
        Assert.Equal(1, Depot.CompterElements(onglet.Id));
    }

    [Fact]
    public void Nommer_recalcule_le_texte_de_recherche_et_l_index()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("Adresse du client", "Outlook"));
        Assert.Empty(InsertionsSql.Trouver(_banc.Connexion, "\"facturation\""));

        Depot.Nommer(element.Id, "Facturation", onglet.Id);

        Assert.Equal("facturation adresse du client outlook", TexteRecherche(element.Id));
        Assert.Equal([element.Id], InsertionsSql.Trouver(_banc.Connexion, "\"facturation\""));
        // Le texte d'origine reste cherchable.
        Assert.Equal([element.Id], InsertionsSql.Trouver(_banc.Connexion, "\"adresse\""));
    }

    [Fact]
    public void Un_element_nomme_peut_etre_renomme_et_deplace()
    {
        var clients = Depot.CreerOnglet("Clients");
        var modeles = Depot.CreerOnglet("Modeles");
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("Bonjour"));
        Depot.Nommer(element.Id, "Salutation", clients.Id);

        var deplace = Depot.Nommer(element.Id, "Accueil", modeles.Id);

        Assert.Equal("Accueil", deplace.Nom);
        Assert.Equal(modeles.Id, deplace.OngletId);
        Assert.Equal(0, Depot.CompterElements(clients.Id));
        Assert.Equal(1, Depot.CompterElements(modeles.Id));
        Assert.Empty(InsertionsSql.Trouver(_banc.Connexion, "\"salutation\""));
        Assert.Equal([element.Id], InsertionsSql.Trouver(_banc.Connexion, "\"accueil\""));
    }

    [Fact]
    public void Nommer_un_fichier_ne_double_pas_les_chemins_dans_le_texte_de_recherche()
    {
        var onglet = Depot.CreerOnglet("Dossiers");
        var element = Depot.AjouterCopie(ContenusDeTest.Fichiers("C:\\Dossier\\Rapport.docx"));

        Depot.Nommer(element.Id, "Rapport", onglet.Id);

        Assert.Equal("rapport c:\\dossier\\rapport.docx", TexteRecherche(element.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Nommer_avec_un_nom_vide_leve_ArgumentException_et_ne_change_rien(string nom)
    {
        var onglet = Depot.CreerOnglet("Clients");
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("abc"));

        Assert.Throws<ArgumentException>(() => Depot.Nommer(element.Id, nom, onglet.Id));

        Assert.Equal(Temporaire, Depot.Lire(element.Id)!.OngletId);
        Assert.Null(Depot.Lire(element.Id)!.Nom);
    }

    [Fact]
    public void Nommer_dans_Temporaire_leve_InvalidOperationException_et_ne_change_rien()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("abc"));

        Assert.Throws<InvalidOperationException>(() => Depot.Nommer(element.Id, "Nom", Temporaire));

        Assert.Null(Depot.Lire(element.Id)!.Nom);
    }

    [Fact]
    public void Nommer_avec_un_onglet_ou_un_element_inconnu_leve_ArgumentException()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("abc"));

        Assert.Throws<ArgumentException>(() => Depot.Nommer(element.Id, "Nom", 9999));
        Assert.Throws<ArgumentException>(() => Depot.Nommer(9999, "Nom", onglet.Id));

        Assert.Equal(Temporaire, Depot.Lire(element.Id)!.OngletId);
    }

    // ---- Usage ----

    [Fact]
    public void Marquer_utilise_remonte_l_element_meme_a_la_meme_milliseconde()
    {
        var a = Depot.AjouterCopie(ContenusDeTest.Texte("aaa"));
        var b = Depot.AjouterCopie(ContenusDeTest.Texte("bbb"));
        Assert.Equal([b.Id, a.Id], Ids(Depot.ListerElements(Temporaire)));

        Depot.MarquerUtilise(a.Id);

        Assert.Equal([a.Id, b.Id], Ids(Depot.ListerElements(Temporaire)));
    }

    [Fact]
    public void Marquer_utilise_avance_la_date_d_usage_sans_toucher_a_la_date_de_copie()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("abc"));
        Depot.Nommer(element.Id, "Nom", onglet.Id);
        _banc.Horloge.Advance(TimeSpan.FromHours(2));

        Depot.MarquerUtilise(element.Id);

        var relu = Depot.Lire(element.Id)!;
        Assert.Equal(_banc.Horloge.GetUtcNow(), relu.DateUsage);
        Assert.Equal(element.DateCopie, relu.DateCopie);
    }

    [Fact]
    public void Marquer_utilise_un_element_inconnu_est_sans_effet()
    {
        var appels = 0;
        Depot.Modifie += () => appels++;

        Depot.MarquerUtilise(9999);

        Assert.Equal(0, appels);
    }

    // ---- Événement ----

    [Fact]
    public void Modifie_est_levee_apres_chaque_ecriture_reussie_et_jamais_apres_une_lecture_ou_un_echec()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var appels = 0;
        Depot.Modifie += () => Interlocked.Increment(ref appels);

        var element = Depot.AjouterCopie(ContenusDeTest.Texte("abc"));
        Assert.Equal(1, appels);
        Depot.AjouterCopie(ContenusDeTest.Texte("abc"));
        Assert.Equal(2, appels);
        Depot.MarquerUtilise(element.Id);
        Assert.Equal(3, appels);
        Depot.Nommer(element.Id, "Nom", onglet.Id);
        Assert.Equal(4, appels);

        _ = Depot.Lire(element.Id);
        _ = Depot.ListerElements(onglet.Id);
        _ = Depot.CompterElements(onglet.Id);
        Assert.Throws<ArgumentException>(() => Depot.AjouterCopie(new ContenuCopie { Type = TypeElement.Texte }));
        Assert.Throws<InvalidOperationException>(() => Depot.Nommer(element.Id, "Nom", Temporaire));
        Assert.Equal(4, appels);
    }

    // ---- Assistants ----

    private static long[] Ids(IEnumerable<Element> elements) => [.. elements.Select(e => e.Id)];

    private string TexteRecherche(long id) =>
        InsertionsSql.Scalaire(_banc.Connexion, $"SELECT texte_recherche FROM element WHERE id = {id}")!;
}
