### Tâche 5 : DepotHistorique : éléments

**Fichiers :**
- Créer : `tests/PressePapiers.Tests/Outils/ContenusDeTest.cs`
- Modifier : `src/PressePapiers.Coeur/Stockage/DepotHistorique.cs` (le plan donne le contenu complet du fichier après modification)
- Modifier : `src/PressePapiers.Coeur/Stockage/SchemaBase.cs` (le plan donne le contenu complet du fichier après modification)
- Tests : `tests/PressePapiers.Tests/Stockage/DepotElementsTests.cs`
- Tests : `tests/PressePapiers.Tests/Stockage/DepotIndexTests.cs`

**Interfaces :**
- Utilise : (tâche 4) tout `DepotHistorique` d'onglets, `DepotDeTest` ; (tâche 2) `Empreinte.Calculer(ContenuCopie)` (SHA-256, lève `ArgumentException` si le contenu principal manque), `Normalisation.TexteRecherche(string? nom, string? texte, IReadOnlyList<string>? fichiers, string? application)` ; (tâche 1) `Element`, `ContenuCopie`, `TypeElement`, `LecteurElement.Colonnes` et `LecteurElement.Lire(SqliteDataReader)`.
- Fournit : `int CompterElements(long ongletId)` ; `IReadOnlyList<Element> ListerElements(long ongletId)` (DateUsage décroissante, puis Id décroissant) ; `Element? Lire(long elementId)` ; `Element AjouterCopie(ContenuCopie contenu)` ; `Element Nommer(long elementId, string nom, long ongletId)` ; `void MarquerUtilise(long elementId)` ; `internal const string RequeteDoublon`. Schéma : index `element_onglet_empreinte` et `element_onglet_usage`, déclencheur `element_au` limité à `texte_recherche`. Assistant de test `PressePapiers.Tests.Outils.ContenusDeTest` : `Texte(string, string? application = null)`, `MisEnForme(string, string? application = null)`, `Image(byte[] png, string? application = null)`, `Fichiers(params string[] chemins)`, `Png(int graine, int taille = 64)`.

Remarques pour les tâches suivantes :
- **Dédoublonnage** (piège c du socle) : dans Temporaire seulement, par empreinte et par famille de contenu. Texte et MisEnForme forment une seule famille (la mise en forme n'entre pas dans l'empreinte, voir `Empreinte`) ; Image et Fichiers sont chacun la leur : le texte `C:\a.txt` et le fichier `C:\a.txt` restent deux éléments. Un doublon n'est pas recopié : il reçoit la nouvelle date d'usage et la nouvelle application d'origine si elle est connue (une application inconnue ne remplace pas une application connue) ; son premier enregistrement est gardé tel quel (une copie avec RTF d'un texte déjà enregistré en brut ne lui ajoute pas le RTF). Aucun PNG n'est réécrit pour une image déjà connue.
- **Rafales** (point de vigilance 1) : `DateCopie` est l'heure de l'horloge, mais `DateUsage` est toujours strictement supérieure à la plus récente de l'onglet (`DateUsageSuivante` : l'heure courante, sinon la plus récente plus 1 tick de 100 ns). Une rafale dans la même milliseconde, ou une horloge qui recule, garde donc l'ordre des événements : le dernier copié ou réutilisé est en tête, un contenu recopié remonte même à date égale. `ListerElements` trie par `date_usage DESC, id DESC` pour les lignes qui auraient malgré tout la même date (insérées hors du dépôt).
- Les dates sont écrites en UTC au format `"O"` (décalage `+00:00` partout), de sorte que le tri par texte de SQLite est chronologique (piège b du socle).
- `AjouterCopie` : l'empreinte se calcule avant le verrou ; `texte_recherche` vient de `Normalisation.TexteRecherche` (pour une liste de fichiers, les chemins sont passés en tant que liste, une seule fois) ; `Taille` = octets UTF-8 du texte ou du PNG ; le PNG est écrit sous un nom GUID (`N`) `.png`, le dossier des images est créé au besoin, et le fichier est retiré si l'insertion échoue. Un contenu principal absent lève `ArgumentException` sans rien écrire (piège d du socle : la capture ne doit pas en transmettre).
- `Nommer` : le nom est débarrassé de ses espaces de bord ; nom vide, élément inconnu ou onglet inconnu = `ArgumentException` ; onglet Temporaire = `InvalidOperationException` ; la date d'usage ne change pas ; `texte_recherche` est recalculé (nom, texte, application). Il sert aussi à renommer ou déplacer un élément déjà nommé.
- `MarquerUtilise` remonte l'élément en tête de son onglet (même règle de dates) ; un élément inconnu est sans effet.
- **Répartition** : `Supprimer` et `CheminImage` appartiennent à la tâche 6 (l'énoncé les y place ; la note du socle les avait mises en tâche 5), leurs coquilles portent donc `Tâche 6`. Les tests de cette tâche relisent les PNG par `Path.Combine(Dossiers.Images, element.ImageFichier)`.
- **Écart au socle (SchemaBase.cs)**, voir `ecarts_contrat` : deux index et un déclencheur `element_au` réduit à `AFTER UPDATE OF texte_recherche`. Mesures faites pendant l'écriture : avec le déclencheur d'origine (`AFTER UPDATE`), recopier ou réutiliser un élément de texte de 9 Mo coûtait environ 4 s (réindexation complète des trigrammes pour un simple changement de date) ; avec le déclencheur réduit, moins de 100 ms. Avec 1 000 éléments de 20 Ko, ajouter un élément passe d'environ 90 ms à environ 46 ms grâce aux index (sans eux, la recherche de doublon et le `MAX(date_usage)` lisent toutes les pages de débordement de l'onglet). `DepotIndexTests` verrouille ces choix (index présents, requête de doublon qui utilise l'index des empreintes, lecture de la liste dans l'ordre de l'index sans tri temporaire, déclencheur limité).
- **Coût d'un gros texte** : `AjouterCopie` d'un texte aléatoire de 9 Mo prend environ 2 s (indexation FTS5 trigram) et la base grossit d'environ 37 Mo. `ServiceCapture` (tâche 23) doit donc appeler le dépôt hors du fil d'interface. `ListerElements` relit le contenu complet de chaque élément (texte, RTF, HTML, miniature) : environ 70 ms pour 1 000 éléments de 20 Ko ; l'interface devra le savoir pour ses listes longues.

- [ ] **Étape 1 : Écrire le test qui échoue**

`ContenusDeTest` fabrique les contenus ; `DepotElementsTests` couvre l'ajout de chaque type, le dédoublonnage, les rafales (point de vigilance 1), la lecture, `Nommer`, `MarquerUtilise` et l'événement `Modifie`. Le texte de 9 Mo de `Un_texte_de_9_Mo_est_enregistre_et_relu_intact` ajoute environ 2 s à la série de tests.

`tests/PressePapiers.Tests/Outils/ContenusDeTest.cs`

```csharp
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Tests.Outils;

/// <summary>Fabrique de contenus copiés pour les tests du dépôt.</summary>
public static class ContenusDeTest
{
    /// <summary>Texte brut.</summary>
    public static ContenuCopie Texte(string texte, string? application = null) =>
        new() { Type = TypeElement.Texte, Texte = texte, Application = application };

    /// <summary>Texte accompagné de RTF et de HTML.</summary>
    public static ContenuCopie MisEnForme(string texte, string? application = null) =>
        new()
        {
            Type = TypeElement.MisEnForme,
            Texte = texte,
            Rtf = System.Text.Encoding.ASCII.GetBytes("{\\rtf1\\ansi " + texte + "}"),
            Html = System.Text.Encoding.UTF8.GetBytes("<b>" + texte + "</b>"),
            Application = application,
        };

    /// <summary>Image déjà convertie en PNG, avec une miniature.</summary>
    public static ContenuCopie Image(byte[] png, string? application = null) =>
        new() { Type = TypeElement.Image, ImagePng = png, Miniature = [9, 8, 7], Application = application };

    /// <summary>Liste de fichiers.</summary>
    public static ContenuCopie Fichiers(params string[] chemins) =>
        new() { Type = TypeElement.Fichiers, Fichiers = chemins };

    /// <summary>
    /// Octets qui tiennent lieu de PNG : le dépôt ne les décode pas. Deux graines différentes donnent deux contenus différents.
    /// </summary>
    public static byte[] Png(int graine, int taille = 64)
    {
        var octets = new byte[taille];
        new Random(graine).NextBytes(octets);
        return octets;
    }
}
```

`tests/PressePapiers.Tests/Stockage/DepotElementsTests.cs`

```csharp
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
        var brut = Depot.AjouterCopie(ContenusDeTest.Texte("Titre"));

        var misEnForme = Depot.AjouterCopie(ContenusDeTest.MisEnForme("Titre"));

        // La mise en forme n'entre pas dans l'empreinte : le premier enregistrement est conservé tel quel.
        Assert.Equal(brut.Id, misEnForme.Id);
        Assert.Equal(TypeElement.Texte, misEnForme.Type);
        Assert.Null(misEnForme.Rtf);
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
```

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : les 35 nouveaux tests échouent avec `System.NotImplementedException : Tâche 5` ; les 162 tests précédents restent verts :

```
Résumé de série de tests : Échec!
  total : 197
  échec : 35
  réussie : 162
  ignoré : 0
```

- [ ] **Étape 3 : Implémenter**

Implémenter les six membres de la tâche 5 dans `DepotHistorique.cs`, ajouter les deux index et réduire le déclencheur dans `SchemaBase.cs`, puis ajouter `DepotIndexTests.cs` (les garde-fous de performance, écrits avec l'implémentation car ils vérifient le schéma et `RequeteDoublon`).

`src/PressePapiers.Coeur/Stockage/DepotHistorique.cs`

```csharp
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Recherche;

namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// Dépôt des onglets et des éléments, sûr entre fils : la connexion SQLite, qui ne l'est pas, sert elle-même de verrou,
/// partagé avec <see cref="Recherche.MoteurRecherche"/>.
/// Onglets par la tâche 4, éléments par la tâche 5, suppression, purge, plafond, index et méta par la tâche 6.
/// </summary>
public sealed class DepotHistorique
{
    private const string NomTemporaire = "Temporaire";

    /// <summary>Recherche d'un doublon dans Temporaire ; interne pour qu'un test vérifie qu'elle passe par l'index des empreintes.</summary>
    internal const string RequeteDoublon =
        """
        SELECT MAX(id) FROM element
        WHERE onglet_id = $onglet AND empreinte = $empreinte AND type BETWEEN $premier AND $dernier
        """;

    // La connexion elle-même : MoteurRecherche la verrouille aussi, donc une recherche n'utilise jamais la connexion
    // pendant une écriture du dépôt faite sur un autre fil (la capture).
    private readonly object _verrou;
    private readonly SqliteConnection _connexion;
    private readonly DossierDonnees _dossiers;
    private readonly TimeProvider _horloge;
    private readonly IJournal _journal;

    /// <summary>Crée le dépôt sur une base ouverte et y crée l'onglet Temporaire s'il n'existe pas encore.</summary>
    public DepotHistorique(BaseHistorique baseHistorique, DossierDonnees dossiers, TimeProvider horloge, IJournal journal)
    {
        ArgumentNullException.ThrowIfNull(baseHistorique);
        ArgumentNullException.ThrowIfNull(dossiers);
        ArgumentNullException.ThrowIfNull(horloge);
        ArgumentNullException.ThrowIfNull(journal);

        _connexion = baseHistorique.Connexion;
        _verrou = _connexion;
        _dossiers = dossiers;
        _horloge = horloge;
        _journal = journal;
        OngletTemporaire = LireOuCreerTemporaire();
    }

    /// <summary>Onglet Temporaire, créé à la première ouverture (nom « Temporaire », ordre 0).</summary>
    public Onglet OngletTemporaire { get; }

    /// <summary>
    /// Après toute écriture réussie ; peut être levé sur n'importe quel fil, une fois le verrou du dépôt libéré
    /// (un abonné peut donc rappeler le dépôt). Un abonné qui lève une exception est noté au journal et n'arrête
    /// ni l'écriture ni les autres abonnés.
    /// </summary>
    public event Action? Modifie;

    /// <summary>Onglets par ordre, Temporaire en premier.</summary>
    public IReadOnlyList<Onglet> ListerOnglets()
    {
        lock (_verrou)
        {
            return LireOnglets(null);
        }
    }

    /// <summary>Crée un onglet à la fin ; le nom ne peut pas être vide.</summary>
    /// <exception cref="ArgumentException">Le nom est vide ou ne contient que des espaces.</exception>
    public Onglet CreerOnglet(string nom)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nom);

        Onglet cree;
        lock (_verrou)
        {
            // Une seule instruction calcule l'ordre et insère : deux créations ne peuvent pas obtenir le même ordre.
            Executer(
                "INSERT INTO onglet (nom, ordre, est_temporaire) SELECT $nom, COALESCE(MAX(ordre), 0) + 1, 0 FROM onglet",
                null, ("$nom", nom.Trim()));
            var id = Convert.ToInt64(Scalaire("SELECT last_insert_rowid()", null), CultureInfo.InvariantCulture);
            cree = LireOnglet(id)!;
        }

        Notifier();
        return cree;
    }

    /// <summary>Renomme un onglet.</summary>
    /// <exception cref="ArgumentException">Le nom est vide, ou l'onglet n'existe pas.</exception>
    /// <exception cref="InvalidOperationException">L'onglet est Temporaire.</exception>
    public void RenommerOnglet(long ongletId, string nom)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nom);

        lock (_verrou)
        {
            ExigerOngletModifiable(ongletId, "renommé");
            Executer("UPDATE onglet SET nom = $nom WHERE id = $id", null, ("$nom", nom.Trim()), ("$id", ongletId));
        }

        Notifier();
    }

    /// <summary>
    /// Place un onglet à une position de la liste, Temporaire valant 0 : une position inférieure à 1 donne la première
    /// place après Temporaire, une position trop grande la dernière. Les ordres sont renumérotés de 1 à n sans trou.
    /// </summary>
    /// <exception cref="ArgumentException">L'onglet n'existe pas.</exception>
    /// <exception cref="InvalidOperationException">L'onglet est Temporaire, qui reste toujours en tête.</exception>
    public void DeplacerOnglet(long ongletId, int nouvelOrdre)
    {
        lock (_verrou)
        {
            ExigerOngletModifiable(ongletId, "déplacé");

            // On sort l'onglet de la file des onglets utilisateur, puis on le réinsère à sa place avant de tout renuméroter.
            var file = LireOnglets(null).Where(o => !o.EstTemporaire && o.Id != ongletId).Select(o => o.Id).ToList();
            file.Insert(Math.Clamp(nouvelOrdre - 1, 0, file.Count), ongletId);

            using var transaction = _connexion.BeginTransaction();
            for (var i = 0; i < file.Count; i++)
            {
                Executer("UPDATE onglet SET ordre = $ordre WHERE id = $id AND ordre <> $ordre", transaction,
                         ("$ordre", i + 1), ("$id", file[i]));
            }

            transaction.Commit();
        }

        Notifier();
    }

    /// <summary>
    /// Supprime un onglet, ses éléments et leurs PNG ; sans effet si l'onglet n'existe pas.
    /// Les lignes partent d'abord (transaction), les fichiers ensuite : un PNG orphelin est sans conséquence,
    /// une ligne sans PNG ne le serait pas.
    /// </summary>
    /// <exception cref="InvalidOperationException">L'onglet est Temporaire.</exception>
    public void SupprimerOnglet(long ongletId)
    {
        lock (_verrou)
        {
            var onglet = LireOnglet(ongletId);
            if (onglet is null)
            {
                return;
            }

            if (onglet.EstTemporaire)
            {
                throw new InvalidOperationException("L'onglet Temporaire ne peut pas être supprimé.");
            }

            var images = new List<string>();
            using (var transaction = _connexion.BeginTransaction())
            {
                SupprimerElementsDe(ongletId, transaction, images);
                Executer("DELETE FROM onglet WHERE id = $id", transaction, ("$id", ongletId));
                transaction.Commit();
            }

            SupprimerFichiersImages(images);
        }

        Notifier();
    }

    /// <summary>Nombre d'éléments d'un onglet.</summary>
    public int CompterElements(long ongletId)
    {
        lock (_verrou)
        {
            var nombre = Scalaire("SELECT COUNT(*) FROM element WHERE onglet_id = $onglet", null, ("$onglet", ongletId));
            return Convert.ToInt32(nombre, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Éléments d'un onglet, par DateUsage décroissante puis Id décroissant (ordre stable à date égale).</summary>
    public IReadOnlyList<Element> ListerElements(long ongletId)
    {
        lock (_verrou)
        {
            return LireElements(null, "WHERE onglet_id = $onglet", ("$onglet", ongletId));
        }
    }

    /// <summary>Lit un élément, ou null s'il n'existe pas.</summary>
    public Element? Lire(long elementId)
    {
        lock (_verrou)
        {
            return LireElement(elementId);
        }
    }

    /// <summary>
    /// Ajoute une copie à Temporaire, écrit le PNG d'une image et renvoie l'élément créé ou remonté.
    /// Un contenu identique déjà présent dans Temporaire (même empreinte, même sorte de contenu) n'est pas copié :
    /// l'élément existant passe en tête et reçoit la nouvelle application d'origine, si elle est connue.
    /// Le texte brut, avec ou sans mise en forme, est une seule sorte de contenu ; le premier enregistrement est gardé tel quel.
    /// </summary>
    /// <exception cref="ArgumentException">Le contenu principal du type demandé est absent.</exception>
    public Element AjouterCopie(ContenuCopie contenu)
    {
        ArgumentNullException.ThrowIfNull(contenu);

        // Hors du verrou : hacher un gros contenu ne doit pas bloquer les autres opérations.
        var empreinte = Empreinte.Calculer(contenu);
        var texte = contenu.Type == TypeElement.Fichiers ? string.Join('\n', contenu.Fichiers!) : contenu.Texte;

        Element resultat;
        lock (_verrou)
        {
            var maintenant = Maintenant();
            var doublon = TrouverDoublon(empreinte, contenu.Type);
            resultat = doublon is null
                ? Inserer(contenu, empreinte, texte, maintenant)
                : Remonter(doublon.Value, contenu.Application, maintenant);
        }

        Notifier();
        return resultat;
    }

    /// <summary>
    /// Donne un nom à un élément et le range dans un onglet autre que Temporaire ; vaut aussi pour renommer
    /// ou déplacer un élément déjà nommé. Le nom est débarrassé des espaces de bord ; sa date d'usage ne change pas.
    /// </summary>
    /// <exception cref="ArgumentException">Le nom est vide, ou l'élément ou l'onglet n'existe pas.</exception>
    /// <exception cref="InvalidOperationException">L'onglet demandé est Temporaire.</exception>
    public Element Nommer(long elementId, string nom, long ongletId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nom);

        Element nomme;
        lock (_verrou)
        {
            var onglet = LireOnglet(ongletId)
                ?? throw new ArgumentException($"L'onglet {ongletId} n'existe pas.", nameof(ongletId));
            if (onglet.EstTemporaire)
            {
                throw new InvalidOperationException("Un élément nommé ne peut pas être rangé dans Temporaire.");
            }

            var courant = LireElement(elementId)
                ?? throw new ArgumentException($"L'élément {elementId} n'existe pas.", nameof(elementId));
            var nomPropre = nom.Trim();

            Executer(
                "UPDATE element SET nom = $nom, onglet_id = $onglet, texte_recherche = $recherche WHERE id = $id",
                null,
                ("$nom", nomPropre), ("$onglet", ongletId), ("$id", elementId),
                ("$recherche", TexteRecherche(nomPropre, courant.Type, courant.Texte, courant.Application)));
            nomme = courant with { Nom = nomPropre, OngletId = ongletId };
        }

        Notifier();
        return nomme;
    }

    /// <summary>
    /// Met à jour la date de dernier usage d'un élément, qui passe en tête de son onglet ; sans effet s'il n'existe pas.
    /// </summary>
    public void MarquerUtilise(long elementId)
    {
        lock (_verrou)
        {
            var ongletId = Scalaire("SELECT onglet_id FROM element WHERE id = $id", null, ("$id", elementId));
            if (ongletId is null)
            {
                return;
            }

            var date = DateUsageSuivante(Convert.ToInt64(ongletId, CultureInfo.InvariantCulture), Maintenant(), null);
            Executer("UPDATE element SET date_usage = $date WHERE id = $id", null, ("$date", EnTexte(date)), ("$id", elementId));
        }

        Notifier();
    }

    /// <summary>Supprime un élément et son PNG.</summary>
    public void Supprimer(long elementId) => throw new NotImplementedException("Tâche 6");

    /// <summary>Chemin complet du PNG d'un élément ; <see cref="InvalidOperationException"/> s'il n'a pas d'image.</summary>
    public string CheminImage(Element element) => throw new NotImplementedException("Tâche 6");

    /// <summary>Supprime les éléments de Temporaire et leurs PNG ; renvoie leur nombre.</summary>
    public int PurgerTemporaire() => throw new NotImplementedException("Tâche 6");

    /// <summary>Supprime les plus anciens éléments de Temporaire au-delà du plafond (0 = désactivé) ; renvoie leur nombre.</summary>
    public int AppliquerPlafond(int plafond) => throw new NotImplementedException("Tâche 6");

    /// <summary>Reconstruit l'index plein texte.</summary>
    public void ReconstruireIndex() => throw new NotImplementedException("Tâche 6");

    /// <summary>Lit une valeur de la table <c>meta</c>.</summary>
    public string? LireMeta(string cle) => throw new NotImplementedException("Tâche 6");

    /// <summary>Écrit une valeur dans la table <c>meta</c>.</summary>
    public void EcrireMeta(string cle, string valeur) => throw new NotImplementedException("Tâche 6");

    // ---- Onglets : lecture ----

    private Onglet LireOuCreerTemporaire()
    {
        lock (_verrou)
        {
            using var transaction = _connexion.BeginTransaction();
            var existant = LireOnglets(transaction, "WHERE est_temporaire = 1").FirstOrDefault();
            if (existant is not null)
            {
                return existant;
            }

            Executer("INSERT INTO onglet (nom, ordre, est_temporaire) VALUES ($nom, 0, 1)", transaction, ("$nom", NomTemporaire));
            var id = Convert.ToInt64(Scalaire("SELECT last_insert_rowid()", transaction), CultureInfo.InvariantCulture);
            transaction.Commit();
            return new Onglet(id, NomTemporaire, 0, true);
        }
    }

    private Onglet? LireOnglet(long id) => LireOnglets(null, "WHERE id = $id", ("$id", id)).FirstOrDefault();

    // La clause est toujours un texte écrit ici, jamais une saisie : les valeurs passent par des paramètres.
    private List<Onglet> LireOnglets(SqliteTransaction? transaction, string clause = "", params (string Nom, object? Valeur)[] parametres)
    {
        using var commande = Preparer($"SELECT id, nom, ordre, est_temporaire FROM onglet {clause} ORDER BY ordre, id", transaction, parametres);
        using var lecteur = commande.ExecuteReader();
        var onglets = new List<Onglet>();
        while (lecteur.Read())
        {
            onglets.Add(new Onglet(lecteur.GetInt64(0), lecteur.GetString(1), lecteur.GetInt32(2), lecteur.GetInt64(3) != 0));
        }

        return onglets;
    }

    // Vérifie qu'un onglet existe et n'est pas Temporaire, avant de le renommer ou de le déplacer.
    private void ExigerOngletModifiable(long ongletId, string action)
    {
        var onglet = LireOnglet(ongletId)
            ?? throw new ArgumentException($"L'onglet {ongletId} n'existe pas.", nameof(ongletId));
        if (onglet.EstTemporaire)
        {
            throw new InvalidOperationException($"L'onglet Temporaire ne peut pas être {action}.");
        }
    }

    // ---- Éléments : lecture ----

    private Element? LireElement(long id) => LireElements(null, "WHERE id = $id", ("$id", id)).FirstOrDefault();

    // Même clause que pour les onglets : un texte écrit ici, les valeurs passent par des paramètres.
    private List<Element> LireElements(SqliteTransaction? transaction, string clause, params (string Nom, object? Valeur)[] parametres)
    {
        using var commande = Preparer(
            $"SELECT {LecteurElement.Colonnes} FROM element {clause} ORDER BY date_usage DESC, id DESC", transaction, parametres);
        using var lecteur = commande.ExecuteReader();
        var elements = new List<Element>();
        while (lecteur.Read())
        {
            elements.Add(LecteurElement.Lire(lecteur));
        }

        return elements;
    }

    // ---- Éléments : ajout et dédoublonnage ----

    // Dates toujours en UTC et en texte « O » : le tri par texte de SQLite suit alors l'ordre chronologique.
    private DateTimeOffset Maintenant() => _horloge.GetUtcNow().ToUniversalTime();

    private static string EnTexte(DateTimeOffset date) => date.ToString("O", CultureInfo.InvariantCulture);

    // Texte brut avec ou sans mise en forme : une seule famille de contenu pour le dédoublonnage. Une liste de fichiers
    // et un texte qui reproduit ses chemins ont la même empreinte mais restent deux éléments.
    private long? TrouverDoublon(byte[] empreinte, TypeElement type)
    {
        var (premier, dernier) = type switch
        {
            TypeElement.Texte or TypeElement.MisEnForme => ((int)TypeElement.Texte, (int)TypeElement.MisEnForme),
            _ => ((int)type, (int)type),
        };

        // Pas de tri : avec un ORDER BY, SQLite parcourrait l'onglet dans l'ordre des dates au lieu d'utiliser l'index
        // des empreintes. Il n'y a normalement qu'un doublon ; s'il y en avait plusieurs, on prend le plus récent par Id.
        var id = Scalaire(
            RequeteDoublon, null,
            ("$onglet", OngletTemporaire.Id), ("$empreinte", empreinte), ("$premier", premier), ("$dernier", dernier));
        return id is long trouve ? trouve : null;
    }

    // Date d'usage d'un élément qui passe en tête : l'heure courante, mais jamais avant ni à égalité avec le plus récent
    // de l'onglet (rafale dans la même milliseconde, horloge qui recule). L'ordre d'usage ne dépend ainsi jamais d'une égalité.
    private DateTimeOffset DateUsageSuivante(long ongletId, DateTimeOffset maintenant, SqliteTransaction? transaction)
    {
        var plusRecente = Scalaire("SELECT MAX(date_usage) FROM element WHERE onglet_id = $onglet", transaction, ("$onglet", ongletId));
        if (plusRecente is not string texte)
        {
            return maintenant;
        }

        var derniere = DateTimeOffset.ParseExact(texte, "O", CultureInfo.InvariantCulture);
        return maintenant > derniere ? maintenant : derniere.AddTicks(1);
    }

    private Element Inserer(ContenuCopie contenu, byte[] empreinte, string? texte, DateTimeOffset maintenant)
    {
        // Le PNG est écrit avant la ligne : un échec de l'insertion retire le fichier, jamais l'inverse.
        string? imageFichier = null;
        if (contenu.Type == TypeElement.Image)
        {
            Directory.CreateDirectory(_dossiers.Images);
            imageFichier = Guid.NewGuid().ToString("N") + ".png";
            File.WriteAllBytes(Path.Combine(_dossiers.Images, imageFichier), contenu.ImagePng!);
        }

        try
        {
            var dateUsage = DateUsageSuivante(OngletTemporaire.Id, maintenant, null);
            var taille = contenu.Type == TypeElement.Image ? contenu.ImagePng!.Length : Encoding.UTF8.GetByteCount(texte!);

            Executer(
                """
                INSERT INTO element (onglet_id, nom, type, texte, texte_recherche, rtf, html, image_fichier, miniature,
                                     empreinte, application, date_copie, date_usage, taille)
                VALUES ($onglet, NULL, $type, $texte, $recherche, $rtf, $html, $image, $miniature,
                        $empreinte, $application, $copie, $usage, $taille)
                """,
                null,
                ("$onglet", OngletTemporaire.Id), ("$type", (int)contenu.Type), ("$texte", texte),
                ("$recherche", TexteRecherche(null, contenu.Type, texte, contenu.Application)),
                ("$rtf", contenu.Rtf), ("$html", contenu.Html), ("$image", imageFichier), ("$miniature", contenu.Miniature),
                ("$empreinte", empreinte), ("$application", contenu.Application),
                ("$copie", EnTexte(maintenant)), ("$usage", EnTexte(dateUsage)), ("$taille", taille));
            var id = Convert.ToInt64(Scalaire("SELECT last_insert_rowid()", null), CultureInfo.InvariantCulture);

            return new Element
            {
                Id = id,
                OngletId = OngletTemporaire.Id,
                Type = contenu.Type,
                Texte = texte,
                Rtf = contenu.Rtf,
                Html = contenu.Html,
                ImageFichier = imageFichier,
                Miniature = contenu.Miniature,
                Empreinte = empreinte,
                Application = contenu.Application,
                DateCopie = maintenant,
                DateUsage = dateUsage,
                Taille = taille,
            };
        }
        catch
        {
            if (imageFichier is not null)
            {
                SupprimerFichiersImages([imageFichier]);
            }

            throw;
        }
    }

    // Un doublon n'est pas recopié : sa date d'usage avance, et son application d'origine est remplacée si la nouvelle est connue.
    // Le texte de recherche porte l'application, il est donc recalculé avec elle.
    private Element Remonter(long id, string? application, DateTimeOffset maintenant)
    {
        var courant = LireElement(id)!;
        var dateUsage = DateUsageSuivante(courant.OngletId, maintenant, null);

        if (application is null || application == courant.Application)
        {
            // Le texte de recherche n'est pas touché : le déclencheur de l'index ne réindexe pas un gros texte pour une date.
            Executer("UPDATE element SET date_usage = $usage WHERE id = $id", null, ("$usage", EnTexte(dateUsage)), ("$id", id));
            return courant with { DateUsage = dateUsage };
        }

        Executer(
            "UPDATE element SET date_usage = $usage, application = $application, texte_recherche = $recherche WHERE id = $id",
            null,
            ("$usage", EnTexte(dateUsage)), ("$application", application), ("$id", id),
            ("$recherche", TexteRecherche(courant.Nom, courant.Type, courant.Texte, application)));
        return courant with { DateUsage = dateUsage, Application = application };
    }

    // Pour une liste de fichiers, le texte est la liste des chemins : on la passe comme telle, pas aussi comme texte.
    private static string TexteRecherche(string? nom, TypeElement type, string? texte, string? application) =>
        type == TypeElement.Fichiers
            ? Normalisation.TexteRecherche(nom, null, texte?.Split('\n', StringSplitOptions.RemoveEmptyEntries), application)
            : Normalisation.TexteRecherche(nom, texte, null, application);

    // ---- Suppression partagée entre les onglets et (tâche 6) la purge ----

    // Supprime les éléments d'un onglet dans la transaction donnée et ajoute les noms de leurs PNG à la liste.
    // Les déclencheurs de la base tiennent l'index plein texte à jour ligne par ligne.
    private int SupprimerElementsDe(long ongletId, SqliteTransaction transaction, List<string> images)
    {
        using (var commande = Preparer(
                   "SELECT image_fichier FROM element WHERE onglet_id = $onglet AND image_fichier IS NOT NULL",
                   transaction, ("$onglet", ongletId)))
        using (var lecteur = commande.ExecuteReader())
        {
            while (lecteur.Read())
            {
                images.Add(lecteur.GetString(0));
            }
        }

        return Executer("DELETE FROM element WHERE onglet_id = $onglet", transaction, ("$onglet", ongletId));
    }

    // Un PNG déjà absent n'est pas une erreur ; un PNG verrouillé est noté au journal et laissé en place.
    private void SupprimerFichiersImages(IEnumerable<string> noms)
    {
        foreach (var nom in noms)
        {
            if (string.IsNullOrEmpty(nom))
            {
                continue;
            }

            // GetFileName écarte tout chemin qu'une base abîmée aurait pu y mettre : on ne supprime que dans le dossier des images.
            var chemin = Path.Combine(_dossiers.Images, Path.GetFileName(nom));
            try
            {
                File.Delete(chemin);
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _journal.Avertissement($"Image non supprimée : {chemin}", exception);
            }
        }
    }

    // ---- Événement et accès SQL ----

    // Appelé hors du verrou. Chaque abonné est isolé : une exception de l'un n'empêche ni l'écriture ni les autres.
    private void Notifier()
    {
        var abonnes = Modifie;
        if (abonnes is null)
        {
            return;
        }

        foreach (var abonne in abonnes.GetInvocationList().Cast<Action>())
        {
            try
            {
                abonne();
            }
            catch (Exception exception)
            {
                _journal.Erreur("Un abonné de l'événement Modifie a levé une exception.", exception);
            }
        }
    }

    private SqliteCommand Preparer(string sql, SqliteTransaction? transaction, params (string Nom, object? Valeur)[] parametres)
    {
        var commande = _connexion.CreateCommand();
        commande.Transaction = transaction;
        commande.CommandText = sql;
        foreach (var (nom, valeur) in parametres)
        {
            commande.Parameters.AddWithValue(nom, valeur ?? DBNull.Value);
        }

        return commande;
    }

    private int Executer(string sql, SqliteTransaction? transaction, params (string Nom, object? Valeur)[] parametres)
    {
        using var commande = Preparer(sql, transaction, parametres);
        return commande.ExecuteNonQuery();
    }

    private object? Scalaire(string sql, SqliteTransaction? transaction, params (string Nom, object? Valeur)[] parametres)
    {
        using var commande = Preparer(sql, transaction, parametres);
        return commande.ExecuteScalar();
    }
}
```

`src/PressePapiers.Coeur/Stockage/SchemaBase.cs`

```csharp
namespace PressePapiers.Coeur.Stockage;

/// <summary>Schéma SQL de la base (spec, section 5), écrit pour pouvoir être rejoué sans effet sur une base existante.</summary>
internal static class SchemaBase
{
    /// <summary>Instructions de création : tables, index plein texte et déclencheurs de synchronisation.</summary>
    public const string Creation = """
        CREATE TABLE IF NOT EXISTS onglet (
          id              INTEGER PRIMARY KEY,
          nom             TEXT NOT NULL,
          ordre           INTEGER NOT NULL,
          est_temporaire  INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS element (
          id              INTEGER PRIMARY KEY,
          onglet_id       INTEGER NOT NULL REFERENCES onglet(id),
          nom             TEXT,
          type            INTEGER NOT NULL,
          texte           TEXT,
          texte_recherche TEXT NOT NULL,
          rtf             BLOB,
          html            BLOB,
          image_fichier   TEXT,
          miniature       BLOB,
          empreinte       BLOB NOT NULL,
          application     TEXT,
          date_copie      TEXT NOT NULL,
          date_usage      TEXT NOT NULL,
          taille          INTEGER NOT NULL
        );

        CREATE VIRTUAL TABLE IF NOT EXISTS element_fts USING fts5(
          texte_recherche, content='element', content_rowid='id', tokenize='trigram'
        );

        CREATE TRIGGER IF NOT EXISTS element_ai AFTER INSERT ON element BEGIN
          INSERT INTO element_fts(rowid, texte_recherche) VALUES (new.id, new.texte_recherche);
        END;

        CREATE TRIGGER IF NOT EXISTS element_ad AFTER DELETE ON element BEGIN
          INSERT INTO element_fts(element_fts, rowid, texte_recherche) VALUES ('delete', old.id, old.texte_recherche);
        END;

        CREATE TRIGGER IF NOT EXISTS element_au AFTER UPDATE OF texte_recherche ON element BEGIN
          INSERT INTO element_fts(element_fts, rowid, texte_recherche) VALUES ('delete', old.id, old.texte_recherche);
          INSERT INTO element_fts(rowid, texte_recherche) VALUES (new.id, new.texte_recherche);
        END;

        CREATE INDEX IF NOT EXISTS element_onglet_empreinte ON element(onglet_id, empreinte);
        CREATE INDEX IF NOT EXISTS element_onglet_usage ON element(onglet_id, date_usage DESC, id DESC);

        CREATE TABLE IF NOT EXISTS meta (cle TEXT PRIMARY KEY, valeur TEXT);
        """;
}
```

`tests/PressePapiers.Tests/Stockage/DepotIndexTests.cs`

```csharp
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

/// <summary>
/// Garde-fous de performance du dépôt : index présents, utilisés par les requêtes de Temporaire, et déclencheur de mise à jour
/// limité au texte de recherche (sinon une simple nouvelle date d'usage réindexerait un texte de plusieurs mégaoctets).
/// </summary>
public sealed class DepotIndexTests : IDisposable
{
    private readonly DepotDeTest _banc = new();

    public void Dispose() => _banc.Dispose();

    [Theory]
    [InlineData("element_onglet_empreinte")]
    [InlineData("element_onglet_usage")]
    public void Le_schema_porte_les_index_du_depot(string nom)
    {
        Assert.Equal(nom, InsertionsSql.Scalaire(_banc.Connexion, $"SELECT name FROM sqlite_master WHERE type = 'index' AND name = '{nom}'"));
    }

    [Fact]
    public void Le_declencheur_de_mise_a_jour_ne_reagit_qu_au_texte_de_recherche()
    {
        var sql = InsertionsSql.Scalaire(_banc.Connexion, "SELECT sql FROM sqlite_master WHERE name = 'element_au'");

        Assert.Contains("AFTER UPDATE OF texte_recherche ON element", sql);
    }

    [Fact]
    public void La_recherche_de_doublon_passe_par_l_index_des_empreintes()
    {
        var plan = Plan(DepotHistorique.RequeteDoublon, ("$onglet", 1L), ("$empreinte", new byte[] { 1 }), ("$premier", 1), ("$dernier", 2));

        Assert.Contains("element_onglet_empreinte", plan);
    }

    [Fact]
    public void La_liste_d_un_onglet_se_lit_dans_l_ordre_de_l_index_sans_tri()
    {
        var plan = Plan(
            $"SELECT {LecteurElement.Colonnes} FROM element WHERE onglet_id = $onglet ORDER BY date_usage DESC, id DESC",
            ("$onglet", 1L));

        Assert.Contains("element_onglet_usage", plan);
        Assert.DoesNotContain("TEMP B-TREE", plan);
    }

    private string Plan(string sql, params (string Nom, object Valeur)[] parametres)
    {
        using var commande = _banc.Connexion.CreateCommand();
        commande.CommandText = "EXPLAIN QUERY PLAN " + sql;
        foreach (var (nom, valeur) in parametres)
        {
            commande.Parameters.AddWithValue(nom, valeur);
        }

        using var lecteur = commande.ExecuteReader();
        var details = new List<string>();
        while (lecteur.Read())
        {
            details.Add(lecteur.GetString(3));
        }

        return string.Join(" | ", details);
    }
}
```

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)`, `0 Erreur(s)`, puis

```
Résumé de série de tests : Réussite!
  total : 202
  échec : 0
  réussie : 202
  ignoré : 0
```

Les 40 nouveaux tests : `DepotElementsTests` 35, `DepotIndexTests` 5 (2 index, 1 déclencheur, 1 requête de doublon, 1 lecture de liste).

Contrôle de l'ordre et des priorités (vérifié par mutation) : sans `DateUsageSuivante`, `Des_contenus_differents_a_la_meme_milliseconde_gardent_un_ordre_stable_le_dernier_en_tete`, `Marquer_utilise_remonte_l_element_meme_a_la_meme_milliseconde` et `Des_ajouts_simultanes_du_meme_contenu_ne_creent_qu_un_element_par_contenu` échouent ; si la requête de doublon ignore la famille de contenu, `Un_texte_et_un_fichier_de_meme_empreinte_restent_deux_elements` échoue ; si `ListerElements` trie par Id seul, 4 tests échouent, par date seule, `La_liste_est_triee_par_date_d_usage_puis_par_id_decroissants` échoue ; si la requête de doublon reprend un `ORDER BY ... LIMIT 1`, SQLite abandonne l'index des empreintes et `La_recherche_de_doublon_passe_par_l_index_des_empreintes` échoue.

- [ ] **Étape 5 : Vérifier sur le poste**

Sans objet : logique portable, entièrement couverte par les tests.

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Stockage/DepotHistorique.cs src/PressePapiers.Coeur/Stockage/SchemaBase.cs tests/PressePapiers.Tests/Outils/ContenusDeTest.cs tests/PressePapiers.Tests/Stockage/DepotElementsTests.cs tests/PressePapiers.Tests/Stockage/DepotIndexTests.cs
git commit -m "Enregistrer, dédoublonner et nommer les éléments du dépôt" -m "AjouterCopie calcule l'empreinte et le texte de recherche, écrit le PNG d'une
image sous un nom GUID et dédoublonne dans Temporaire seulement : un contenu
identique remonte avec la nouvelle application d'origine. Les dates d'usage
d'un onglet restent strictement croissantes, donc une rafale dans la même
milliseconde garde un ordre stable." -m "Le schéma gagne deux index (onglet et empreinte, onglet et date d'usage) et le
déclencheur de mise à jour ne réagit plus qu'au texte de recherche : mesuré sur
un texte de 9 Mo, recopier ou réutiliser l'élément passe d'environ 4 s à moins
de 100 ms."
```
