### Tâche 6 : DepotHistorique : suppression, purge, plafond, index, méta

**Fichiers :**
- Créer : `tests/PressePapiers.Tests/Outils/JournalCollecte.cs`
- Modifier : `src/PressePapiers.Coeur/Stockage/DepotHistorique.cs` (le plan donne le contenu complet du fichier après modification)
- Tests : `tests/PressePapiers.Tests/Stockage/DepotSuppressionTests.cs`
- Tests : `tests/PressePapiers.Tests/Stockage/DepotPurgeTests.cs`
- Tests : `tests/PressePapiers.Tests/Stockage/DepotIndexFtsTests.cs`
- Tests : `tests/PressePapiers.Tests/Stockage/DepotMetaTests.cs`

**Interfaces :**
- Utilise : (tâches 4 et 5) `DepotHistorique` et ses assistants privés (`SupprimerElementsDe`, `SupprimerFichiersImages`, `Preparer`, `Executer`, `Scalaire`, `Compter`), `DepotDeTest` (dont `InsererElement`), `ContenusDeTest` ; (tâche 1) `IJournal`.
- Fournit : `void Supprimer(long elementId)` ; `string CheminImage(Element element)` ; `int PurgerTemporaire()` ; `int AppliquerPlafond(int plafond)` ; `void ReconstruireIndex()` ; `string? LireMeta(string cle)` ; `void EcrireMeta(string cle, string valeur)`. Assistant de test `PressePapiers.Tests.Outils.JournalCollecte : IJournal` avec `Infos`, `Avertissements` et `Erreurs` (`IReadOnlyList<string>`).

Remarques pour les tâches suivantes :
- `Supprimer` retire la ligne (les déclencheurs tiennent l'index à jour) puis le PNG. Point de vigilance 4 : un PNG déjà absent, ou un dossier des images disparu, ne lève rien ; un PNG verrouillé par un autre processus est noté au journal (`Avertissement`) et laissé en place, la ligne part dans tous les cas ; seul le nom de fichier compte (`Path.GetFileName`), donc une base abîmée qui désignerait `..\fichier` ne fait jamais supprimer un fichier hors du dossier des images. Un élément inconnu est sans effet.
- `CheminImage` applique la même règle de nom ; `InvalidOperationException` si `ImageFichier` est absent ou vide.
- `PurgerTemporaire` ne touche que Temporaire (transaction, puis fichiers) ; les éléments nommés et leurs PNG restent. `Modifie` n'est levé que si au moins un élément est parti.
- `AppliquerPlafond(plafond)` : 0 ou moins = désactivé (renvoie 0). Au-delà, les plus anciens de Temporaire partent, au sens inverse de l'affichage : `date_usage` croissante puis `id` croissant. Les éléments nommés ne sont ni comptés ni touchés.
- `ReconstruireIndex` exécute `INSERT INTO element_fts(element_fts) VALUES ('rebuild')`. `ReconstruireIndex` et `EcrireMeta` lèvent `Modifie` (le contrat dit « après toute écriture réussie ») ; `LireMeta` renvoie `null` pour une clé absente ; `EcrireMeta` remplace la valeur d'une clé existante (upsert) ; clé vide ou nulle = `ArgumentException`, valeur nulle = `ArgumentNullException`.
- Les tests de synchronisation de l'index utilisent la commande FTS5 `integrity-check` avec `rank = 1`, qui compare l'index à la table `element` et lève `SqliteException` à la moindre divergence ; un test prouve que ce contrôle détecte bien un index vidé par `delete-all`.

- [ ] **Étape 1 : Écrire le test qui échoue**

`JournalCollecte` garde les messages ; `DepotSuppressionTests` couvre `Supprimer` et `CheminImage` (dont PNG absent, verrouillé, nom piégé) ; `DepotPurgeTests` couvre la purge et le plafond ; `DepotIndexFtsTests` vérifie l'index après insertion, renommage, suppression, purge, plafond, suppression d'onglet et reconstruction ; `DepotMetaTests` couvre `LireMeta` et `EcrireMeta`.

`tests/PressePapiers.Tests/Outils/JournalCollecte.cs`

```csharp
using System.Collections.Concurrent;
using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Tests.Outils;

/// <summary>Journal de test qui garde les messages, pour vérifier ce que le code note (avertissements, erreurs).</summary>
public sealed class JournalCollecte : IJournal
{
    private readonly ConcurrentQueue<string> _infos = new();
    private readonly ConcurrentQueue<string> _avertissements = new();
    private readonly ConcurrentQueue<string> _erreurs = new();

    /// <summary>Messages d'information, dans l'ordre.</summary>
    public IReadOnlyList<string> Infos => [.. _infos];

    /// <summary>Messages d'avertissement, dans l'ordre.</summary>
    public IReadOnlyList<string> Avertissements => [.. _avertissements];

    /// <summary>Messages d'erreur, dans l'ordre.</summary>
    public IReadOnlyList<string> Erreurs => [.. _erreurs];

    /// <inheritdoc />
    public void Info(string message) => _infos.Enqueue(message);

    /// <inheritdoc />
    public void Avertissement(string message, Exception? exception = null) => _avertissements.Enqueue(message);

    /// <inheritdoc />
    public void Erreur(string message, Exception? exception = null) => _erreurs.Enqueue(message);
}
```

`tests/PressePapiers.Tests/Stockage/DepotSuppressionTests.cs`

```csharp
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class DepotSuppressionTests : IDisposable
{
    private readonly JournalCollecte _journal = new();
    private readonly DepotDeTest _banc;

    public DepotSuppressionTests() => _banc = new DepotDeTest(_journal);

    private DepotHistorique Depot => _banc.Depot;

    public void Dispose() => _banc.Dispose();

    // ---- Supprimer ----

    [Fact]
    public void Supprimer_retire_l_element_et_lui_seul()
    {
        var garde = Depot.AjouterCopie(ContenusDeTest.Texte("a garder"));
        var condamne = Depot.AjouterCopie(ContenusDeTest.Texte("a supprimer"));

        Depot.Supprimer(condamne.Id);

        Assert.Null(Depot.Lire(condamne.Id));
        Assert.NotNull(Depot.Lire(garde.Id));
        Assert.Equal(1, Depot.CompterElements(Depot.OngletTemporaire.Id));
    }

    [Fact]
    public void Supprimer_retire_aussi_le_png_et_laisse_les_autres()
    {
        var condamnee = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(1)));
        var gardee = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(2)));

        Depot.Supprimer(condamnee.Id);

        Assert.Equal([gardee.ImageFichier], _banc.NomsImages());
    }

    [Fact]
    public void Supprimer_un_element_nomme_d_un_autre_onglet_fonctionne()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var element = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(3)));
        Depot.Nommer(element.Id, "Logo", onglet.Id);

        Depot.Supprimer(element.Id);

        Assert.Equal(0, Depot.CompterElements(onglet.Id));
        Assert.Empty(_banc.NomsImages());
    }

    [Fact]
    public void Supprimer_quand_le_png_manque_deja_retire_quand_meme_la_ligne_sans_exception()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(4)));
        File.Delete(Depot.CheminImage(element));

        Depot.Supprimer(element.Id);

        Assert.Null(Depot.Lire(element.Id));
        Assert.Empty(_journal.Avertissements);
    }

    [Fact]
    public void Supprimer_quand_le_dossier_des_images_a_disparu_ne_leve_pas_d_exception()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(5)));
        Directory.Delete(_banc.Dossiers.Images, recursive: true);

        Depot.Supprimer(element.Id);

        Assert.Null(Depot.Lire(element.Id));
    }

    [Fact]
    public void Supprimer_un_png_verrouille_retire_la_ligne_et_note_un_avertissement()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(6)));
        var chemin = Depot.CheminImage(element);

        // Un lecteur d'images ouvert sur le fichier suffit à l'empêcher d'être supprimé.
        using (new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Depot.Supprimer(element.Id);
        }

        Assert.Null(Depot.Lire(element.Id));
        Assert.True(File.Exists(chemin));
        Assert.Single(_journal.Avertissements);
    }

    [Fact]
    public void Supprimer_ne_touche_jamais_un_fichier_hors_du_dossier_des_images()
    {
        // Une base abîmée ou trafiquée pourrait désigner un fichier d'ailleurs par un chemin relatif.
        var horsImages = Path.Combine(_banc.Dossiers.Racine, "hors.txt");
        File.WriteAllText(horsImages, "a ne pas supprimer");
        var id = _banc.InsererElement(Depot.OngletTemporaire.Id, "piege", imageFichier: "..\\hors.txt");

        Depot.Supprimer(id);

        Assert.Null(Depot.Lire(id));
        Assert.True(File.Exists(horsImages));
    }

    [Fact]
    public void Supprimer_un_element_inconnu_est_sans_effet_et_ne_leve_pas_modifie()
    {
        var appels = 0;
        Depot.AjouterCopie(ContenusDeTest.Texte("reste"));
        Depot.Modifie += () => appels++;

        Depot.Supprimer(9999);

        Assert.Equal(0, appels);
        Assert.Equal(1, Depot.CompterElements(Depot.OngletTemporaire.Id));
    }

    [Fact]
    public void Supprimer_leve_Modifie_une_fois()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("abc"));
        var appels = 0;
        Depot.Modifie += () => appels++;

        Depot.Supprimer(element.Id);

        Assert.Equal(1, appels);
    }

    // ---- CheminImage ----

    [Fact]
    public void CheminImage_donne_le_chemin_complet_du_png()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(7)));

        var chemin = Depot.CheminImage(element);

        Assert.Equal(Path.Combine(_banc.Dossiers.Images, element.ImageFichier!), chemin);
        Assert.True(File.Exists(chemin));
    }

    [Fact]
    public void CheminImage_d_un_element_sans_image_leve_InvalidOperationException()
    {
        var texte = Depot.AjouterCopie(ContenusDeTest.Texte("abc"));

        Assert.Throws<InvalidOperationException>(() => Depot.CheminImage(texte));
    }

    [Fact]
    public void CheminImage_reste_dans_le_dossier_des_images()
    {
        var element = new Element
        {
            Id = 1,
            OngletId = 1,
            Type = TypeElement.Image,
            ImageFichier = "..\\..\\ailleurs.png",
            Empreinte = [1],
            DateCopie = DateTimeOffset.UnixEpoch,
            DateUsage = DateTimeOffset.UnixEpoch,
            Taille = 1,
        };

        Assert.Equal(Path.Combine(_banc.Dossiers.Images, "ailleurs.png"), Depot.CheminImage(element));
    }

}
```

`tests/PressePapiers.Tests/Stockage/DepotPurgeTests.cs`

```csharp
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class DepotPurgeTests : IDisposable
{
    private readonly DepotDeTest _banc = new();

    private DepotHistorique Depot => _banc.Depot;

    private long Temporaire => Depot.OngletTemporaire.Id;

    public void Dispose() => _banc.Dispose();

    // ---- PurgerTemporaire ----

    [Fact]
    public void Purger_vide_Temporaire_avec_ses_images_et_renvoie_le_nombre()
    {
        Depot.AjouterCopie(ContenusDeTest.Texte("texte un"));
        Depot.AjouterCopie(ContenusDeTest.Texte("texte deux"));
        Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(1)));

        var purges = Depot.PurgerTemporaire();

        Assert.Equal(3, purges);
        Assert.Equal(0, Depot.CompterElements(Temporaire));
        Assert.Empty(_banc.NomsImages());
    }

    [Fact]
    public void Purger_garde_les_elements_nommes_et_leurs_images()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var nomme = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(2)));
        Depot.Nommer(nomme.Id, "Logo", onglet.Id);
        Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(3)));
        Depot.AjouterCopie(ContenusDeTest.Texte("temporaire"));

        var purges = Depot.PurgerTemporaire();

        Assert.Equal(2, purges);
        Assert.Equal(1, Depot.CompterElements(onglet.Id));
        Assert.Equal([nomme.ImageFichier], _banc.NomsImages());
    }

    [Fact]
    public void Purger_un_temporaire_vide_renvoie_zero_sans_lever_Modifie()
    {
        var appels = 0;
        Depot.Modifie += () => appels++;

        Assert.Equal(0, Depot.PurgerTemporaire());

        Assert.Equal(0, appels);
    }

    [Fact]
    public void Purger_leve_Modifie_une_fois()
    {
        Depot.AjouterCopie(ContenusDeTest.Texte("a"));
        Depot.AjouterCopie(ContenusDeTest.Texte("b"));
        var appels = 0;
        Depot.Modifie += () => appels++;

        Depot.PurgerTemporaire();

        Assert.Equal(1, appels);
    }

    [Fact]
    public void Purger_quand_un_png_manque_deja_ne_leve_pas_d_exception()
    {
        var image = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(4)));
        Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(5)));
        File.Delete(Depot.CheminImage(image));

        var purges = Depot.PurgerTemporaire();

        Assert.Equal(2, purges);
        Assert.Empty(_banc.NomsImages());
    }

    // ---- AppliquerPlafond ----

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Un_plafond_nul_ou_negatif_est_desactive(int plafond)
    {
        for (var i = 0; i < 5; i++)
        {
            Depot.AjouterCopie(ContenusDeTest.Texte("element " + i));
        }

        Assert.Equal(0, Depot.AppliquerPlafond(plafond));

        Assert.Equal(5, Depot.CompterElements(Temporaire));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(5000)]
    public void Sous_le_plafond_ou_a_sa_hauteur_rien_n_est_supprime(int plafond)
    {
        for (var i = 0; i < 5; i++)
        {
            Depot.AjouterCopie(ContenusDeTest.Texte("element " + i));
        }

        var appels = 0;
        Depot.Modifie += () => appels++;

        Assert.Equal(0, Depot.AppliquerPlafond(plafond));

        Assert.Equal(5, Depot.CompterElements(Temporaire));
        Assert.Equal(0, appels);
    }

    [Fact]
    public void Le_plafond_supprime_les_plus_anciens_par_date_d_usage_et_non_par_id()
    {
        // Cinq éléments créés à une seconde d'intervalle ; le premier (le plus ancien par Id) est ensuite réutilisé.
        var elements = new List<long>();
        for (var i = 0; i < 5; i++)
        {
            _banc.Horloge.Advance(TimeSpan.FromSeconds(1));
            elements.Add(Depot.AjouterCopie(ContenusDeTest.Texte("element " + i)).Id);
        }

        _banc.Horloge.Advance(TimeSpan.FromSeconds(1));
        Depot.MarquerUtilise(elements[0]);

        var supprimes = Depot.AppliquerPlafond(3);

        // Par date d'usage, les deux plus anciens sont les éléments 1 et 2 ; l'élément 0, réutilisé, est le plus récent.
        Assert.Equal(2, supprimes);
        Assert.Equal([elements[0], elements[4], elements[3]], Depot.ListerElements(Temporaire).Select(e => e.Id));
    }

    [Fact]
    public void A_date_egale_le_plafond_supprime_les_plus_petits_id()
    {
        var date = new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
        var ids = Enumerable.Range(0, 4).Select(i => _banc.InsererElement(Temporaire, "element " + i, date)).ToArray();

        Assert.Equal(2, Depot.AppliquerPlafond(2));

        // C'est l'ordre inverse de celui de ListerElements : ce qui est affiché en bas de la liste part en premier.
        Assert.Equal([ids[3], ids[2]], Depot.ListerElements(Temporaire).Select(e => e.Id));
    }

    [Fact]
    public void Le_plafond_ne_compte_et_ne_touche_que_Temporaire()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var date = new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 10; i++)
        {
            _banc.InsererElement(onglet.Id, "nomme " + i, date.AddMinutes(-i - 1));
        }

        Depot.AjouterCopie(ContenusDeTest.Texte("temporaire un"));
        Depot.AjouterCopie(ContenusDeTest.Texte("temporaire deux"));

        Assert.Equal(0, Depot.AppliquerPlafond(2));
        Assert.Equal(10, Depot.CompterElements(onglet.Id));

        Assert.Equal(1, Depot.AppliquerPlafond(1));
        Assert.Equal(10, Depot.CompterElements(onglet.Id));
        Assert.Equal(1, Depot.CompterElements(Temporaire));
    }

    [Fact]
    public void Le_plafond_supprime_aussi_les_png_des_elements_retires_et_leve_Modifie_une_fois()
    {
        var ancienne = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(6)));
        _banc.Horloge.Advance(TimeSpan.FromSeconds(1));
        var recente = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(7)));
        var appels = 0;
        Depot.Modifie += () => appels++;

        Assert.Equal(1, Depot.AppliquerPlafond(1));

        Assert.Null(Depot.Lire(ancienne.Id));
        Assert.Equal([recente.ImageFichier], _banc.NomsImages());
        Assert.Equal(1, appels);
    }
}
```

`tests/PressePapiers.Tests/Stockage/DepotIndexFtsTests.cs`

```csharp
using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

/// <summary>L'index plein texte doit suivre chaque écriture du dépôt : insertion, renommage, suppression, purge, plafond.</summary>
public sealed class DepotIndexFtsTests : IDisposable
{
    private readonly DepotDeTest _banc = new();

    private DepotHistorique Depot => _banc.Depot;

    public void Dispose() => _banc.Dispose();

    [Fact]
    public void Le_controle_d_integrite_detecte_un_index_desynchronise()
    {
        // Preuve que l'assistant des autres tests voit bien une désynchronisation.
        Depot.AjouterCopie(ContenusDeTest.Texte("zebulon"));
        ControlerIndex();

        Executer("INSERT INTO element_fts(element_fts) VALUES ('delete-all')");

        Assert.Throws<SqliteException>(ControlerIndex);
    }

    [Fact]
    public void Un_element_ajoute_est_aussitot_dans_l_index()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("Le zébulon bondit", "Code"));

        // Texte normalisé (sans accent), mot entier ou fragment de trois lettres et plus, et application d'origine.
        Assert.Equal([element.Id], Trouver("zebulon"));
        Assert.Equal([element.Id], Trouver("bul"));
        Assert.Equal([element.Id], Trouver("code"));
        Assert.Empty(Trouver("girafe"));
        ControlerIndex();
    }

    [Fact]
    public void Un_element_recopie_avec_une_autre_application_met_l_index_a_jour()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon", "Code"));
        _banc.Horloge.Advance(TimeSpan.FromSeconds(1));

        Depot.AjouterCopie(ContenusDeTest.Texte("zebulon", "Chrome"));

        Assert.Equal([element.Id], Trouver("chrome"));
        Assert.Empty(Trouver("code"));
        ControlerIndex();
    }

    [Fact]
    public void Nommer_met_l_index_a_jour_le_nom_remplace_l_ancien_nom()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("adresse postale"));
        Assert.Empty(Trouver("facturation"));

        Depot.Nommer(element.Id, "Facturation", onglet.Id);
        Assert.Equal([element.Id], Trouver("facturation"));
        Assert.Equal([element.Id], Trouver("postale"));
        ControlerIndex();

        Depot.Nommer(element.Id, "Livraison", onglet.Id);
        Assert.Empty(Trouver("facturation"));
        Assert.Equal([element.Id], Trouver("livraison"));
        Assert.Equal([element.Id], Trouver("postale"));
        ControlerIndex();
    }

    [Fact]
    public void Marquer_utilise_laisse_l_index_intact()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon"));
        _banc.Horloge.Advance(TimeSpan.FromSeconds(1));

        Depot.MarquerUtilise(element.Id);

        Assert.Equal([element.Id], Trouver("zebulon"));
        ControlerIndex();
    }

    [Fact]
    public void Supprimer_retire_l_element_de_l_index()
    {
        var garde = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon garde"));
        var condamne = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon condamne"));
        Assert.Equal([garde.Id, condamne.Id], Trouver("zebulon"));

        Depot.Supprimer(condamne.Id);

        Assert.Equal([garde.Id], Trouver("zebulon"));
        Assert.Empty(Trouver("condamne"));
        ControlerIndex();
    }

    [Fact]
    public void Purger_Temporaire_retire_ses_elements_de_l_index_et_garde_les_nommes()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var nomme = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon nomme"));
        Depot.Nommer(nomme.Id, "Garde", onglet.Id);
        Depot.AjouterCopie(ContenusDeTest.Texte("zebulon temporaire"));

        Depot.PurgerTemporaire();

        Assert.Equal([nomme.Id], Trouver("zebulon"));
        ControlerIndex();
    }

    [Fact]
    public void Le_plafond_retire_les_elements_supprimes_de_l_index()
    {
        var ancien = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon ancien"));
        _banc.Horloge.Advance(TimeSpan.FromSeconds(1));
        var recent = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon recent"));

        Depot.AppliquerPlafond(1);

        Assert.Equal([recent.Id], Trouver("zebulon"));
        Assert.DoesNotContain(ancien.Id, Trouver("zebulon"));
        ControlerIndex();
    }

    [Fact]
    public void Supprimer_un_onglet_retire_ses_elements_de_l_index()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon rangé"));
        Depot.Nommer(element.Id, "Rangé", onglet.Id);
        var reste = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon reste"));

        Depot.SupprimerOnglet(onglet.Id);

        Assert.Equal([reste.Id], Trouver("zebulon"));
        ControlerIndex();
    }

    [Fact]
    public void Reconstruire_l_index_repare_un_index_vide_ou_desynchronise()
    {
        var premier = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon un"));
        var second = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon deux"));
        Executer("INSERT INTO element_fts(element_fts) VALUES ('delete-all')");
        Assert.Empty(Trouver("zebulon"));
        Assert.Throws<SqliteException>(ControlerIndex);

        Depot.ReconstruireIndex();

        Assert.Equal([premier.Id, second.Id], Trouver("zebulon"));
        ControlerIndex();
    }

    [Fact]
    public void Reconstruire_l_index_d_une_base_saine_ne_change_rien_et_leve_Modifie()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("zebulon"));
        var appels = 0;
        Depot.Modifie += () => appels++;

        Depot.ReconstruireIndex();

        Assert.Equal([element.Id], Trouver("zebulon"));
        Assert.Equal(1, appels);
        ControlerIndex();
    }

    // Mot ou fragment entre guillemets doubles : une requête MATCH sans syntaxe FTS5 (le moteur de recherche fait de même).
    private List<long> Trouver(string mot) => InsertionsSql.Trouver(_banc.Connexion, "\"" + mot + "\"");

    // Compare l'index à la table element : lève SqliteException à la moindre divergence.
    private void ControlerIndex() => Executer("INSERT INTO element_fts(element_fts, rank) VALUES ('integrity-check', 1)");

    private void Executer(string sql)
    {
        using var commande = _banc.Connexion.CreateCommand();
        commande.CommandText = sql;
        commande.ExecuteNonQuery();
    }
}
```

`tests/PressePapiers.Tests/Stockage/DepotMetaTests.cs`

```csharp
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class DepotMetaTests : IDisposable
{
    private readonly DepotDeTest _banc = new();

    private DepotHistorique Depot => _banc.Depot;

    public void Dispose() => _banc.Dispose();

    [Fact]
    public void Une_cle_absente_donne_null()
    {
        Assert.Null(Depot.LireMeta("inconnue"));
    }

    [Fact]
    public void Une_valeur_ecrite_se_relit()
    {
        Depot.EcrireMeta("session.heure_ouverture", "133750000000000000");

        Assert.Equal("133750000000000000", Depot.LireMeta("session.heure_ouverture"));
    }

    [Fact]
    public void Ecrire_une_cle_existante_remplace_la_valeur_sans_creer_de_doublon()
    {
        Depot.EcrireMeta("cle", "avant");
        Depot.EcrireMeta("cle", "apres");

        Assert.Equal("apres", Depot.LireMeta("cle"));
        Assert.Equal("1", InsertionsSql.Scalaire(_banc.Connexion, "SELECT COUNT(*) FROM meta WHERE cle = 'cle'"));
    }

    [Fact]
    public void Les_cles_sont_independantes_et_sensibles_a_la_casse()
    {
        Depot.EcrireMeta("a", "1");
        Depot.EcrireMeta("A", "2");

        Assert.Equal("1", Depot.LireMeta("a"));
        Assert.Equal("2", Depot.LireMeta("A"));
    }

    [Fact]
    public void Une_valeur_vide_ou_accentuee_se_relit_telle_quelle()
    {
        Depot.EcrireMeta("vide", "");
        Depot.EcrireMeta("accents", "été à Noël");

        Assert.Equal("", Depot.LireMeta("vide"));
        Assert.Equal("été à Noël", Depot.LireMeta("accents"));
    }

    [Fact]
    public void La_version_du_schema_ecrite_a_la_creation_se_lit()
    {
        Assert.Equal("1", Depot.LireMeta("schema.version"));
    }

    [Fact]
    public void Les_valeurs_survivent_a_la_fermeture_de_la_base()
    {
        using var racine = new DossierTemporaire();
        var dossiers = new DossierDonnees(racine.Fichier("donnees"));
        dossiers.CreerDossiers();

        using (var premiere = BaseHistorique.Ouvrir(dossiers.Base))
        {
            new DepotHistorique(premiere, dossiers, TimeProvider.System, JournalNul.Instance).EcrireMeta("cle", "valeur");
        }

        using var seconde = BaseHistorique.Ouvrir(dossiers.Base);
        Assert.Equal("valeur", new DepotHistorique(seconde, dossiers, TimeProvider.System, JournalNul.Instance).LireMeta("cle"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Une_cle_vide_ou_nulle_est_refusee(string? cle)
    {
        Assert.ThrowsAny<ArgumentException>(() => Depot.LireMeta(cle!));
        Assert.ThrowsAny<ArgumentException>(() => Depot.EcrireMeta(cle!, "valeur"));
    }

    [Fact]
    public void Une_valeur_nulle_est_refusee()
    {
        Assert.Throws<ArgumentNullException>(() => Depot.EcrireMeta("cle", null!));
    }

    [Fact]
    public void Ecrire_une_valeur_leve_Modifie_et_la_lire_non()
    {
        var appels = 0;
        Depot.Modifie += () => appels++;

        Depot.EcrireMeta("cle", "valeur");
        Assert.Equal(1, appels);

        _ = Depot.LireMeta("cle");
        Assert.Equal(1, appels);
    }
}
```

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : 42 des 48 nouveaux tests échouent avec `System.NotImplementedException : Tâche 6`. Les 6 autres passent déjà : ils vérifient la synchronisation de l'index après des écritures des tâches 4 et 5 (`Le_controle_d_integrite_detecte_un_index_desynchronise`, `Un_element_ajoute_est_aussitot_dans_l_index`, `Un_element_recopie_avec_une_autre_application_met_l_index_a_jour`, `Nommer_met_l_index_a_jour_le_nom_remplace_l_ancien_nom`, `Marquer_utilise_laisse_l_index_intact`, `Supprimer_un_onglet_retire_ses_elements_de_l_index`). Les 202 tests précédents restent verts :

```
Résumé de série de tests : Échec!
  total : 250
  échec : 42
  réussie : 208
  ignoré : 0
```

- [ ] **Étape 3 : Implémenter**

Implémenter les sept membres de la tâche 6 dans `DepotHistorique.cs` (la coquille `Tâche 6` disparaît).

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
/// Dépôt des onglets et des éléments de l'historique : ajout avec dédoublonnage dans Temporaire, nommage, suppression,
/// purge, plafond de Temporaire, index plein texte et table <c>meta</c>. Sûr entre fils : la connexion SQLite, qui ne l'est
/// pas, sert elle-même de verrou, partagé avec <see cref="Recherche.MoteurRecherche"/>. Les dates sont écrites en UTC au format « O » pour que leur tri par texte soit chronologique.
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
            return Compter(ongletId);
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

    /// <summary>
    /// Supprime un élément et son PNG ; sans effet si l'élément n'existe pas. Un PNG déjà absent n'est pas une erreur,
    /// un PNG verrouillé est noté au journal et laissé en place : la ligne part dans les deux cas.
    /// </summary>
    public void Supprimer(long elementId)
    {
        bool supprime;
        lock (_verrou)
        {
            var image = Scalaire("SELECT image_fichier FROM element WHERE id = $id", null, ("$id", elementId));
            supprime = Executer("DELETE FROM element WHERE id = $id", null, ("$id", elementId)) > 0;
            if (supprime && image is string nom)
            {
                SupprimerFichiersImages([nom]);
            }
        }

        if (supprime)
        {
            Notifier();
        }
    }

    /// <summary>Chemin complet du PNG d'un élément, toujours dans le dossier des images.</summary>
    /// <exception cref="InvalidOperationException">L'élément n'a pas d'image.</exception>
    public string CheminImage(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (string.IsNullOrEmpty(element.ImageFichier))
        {
            throw new InvalidOperationException("Cet élément n'a pas d'image.");
        }

        // Même précaution qu'à la suppression : seul le nom du fichier compte, jamais un chemin venu de la base.
        return Path.Combine(_dossiers.Images, Path.GetFileName(element.ImageFichier));
    }

    /// <summary>
    /// Supprime les éléments de Temporaire et leurs PNG ; renvoie leur nombre.
    /// Les éléments nommés, rangés dans d'autres onglets, ne sont jamais touchés.
    /// </summary>
    public int PurgerTemporaire()
    {
        int supprimes;
        lock (_verrou)
        {
            var images = new List<string>();
            using (var transaction = _connexion.BeginTransaction())
            {
                supprimes = SupprimerElementsDe(OngletTemporaire.Id, transaction, images);
                transaction.Commit();
            }

            SupprimerFichiersImages(images);
        }

        if (supprimes > 0)
        {
            Notifier();
        }

        return supprimes;
    }

    /// <summary>
    /// Supprime les plus anciens éléments de Temporaire (par date d'usage, puis par Id) au-delà du plafond ;
    /// un plafond de 0 ou moins est désactivé. Renvoie le nombre d'éléments supprimés.
    /// </summary>
    public int AppliquerPlafond(int plafond)
    {
        if (plafond <= 0)
        {
            return 0;
        }

        int supprimes;
        lock (_verrou)
        {
            var exces = Compter(OngletTemporaire.Id) - plafond;
            if (exces <= 0)
            {
                return 0;
            }

            var images = new List<string>();
            using (var transaction = _connexion.BeginTransaction())
            {
                // Les plus anciens sont ceux qui viennent en dernier dans ListerElements : même tri, à l'envers.
                const string plusAnciens =
                    "SELECT id FROM element WHERE onglet_id = $onglet ORDER BY date_usage ASC, id ASC LIMIT $exces";
                using (var commande = Preparer(
                           $"SELECT image_fichier FROM element WHERE image_fichier IS NOT NULL AND id IN ({plusAnciens})",
                           transaction, ("$onglet", OngletTemporaire.Id), ("$exces", exces)))
                using (var lecteur = commande.ExecuteReader())
                {
                    while (lecteur.Read())
                    {
                        images.Add(lecteur.GetString(0));
                    }
                }

                supprimes = Executer($"DELETE FROM element WHERE id IN ({plusAnciens})", transaction,
                                     ("$onglet", OngletTemporaire.Id), ("$exces", exces));
                transaction.Commit();
            }

            SupprimerFichiersImages(images);
        }

        Notifier();
        return supprimes;
    }

    /// <summary>
    /// Reconstruit l'index plein texte à partir de la table <c>element</c> (réparation d'un index désynchronisé).
    /// </summary>
    public void ReconstruireIndex()
    {
        lock (_verrou)
        {
            Executer("INSERT INTO element_fts(element_fts) VALUES ('rebuild')", null);
        }

        Notifier();
    }

    /// <summary>Lit une valeur de la table <c>meta</c> ; null si la clé n'existe pas.</summary>
    public string? LireMeta(string cle)
    {
        ArgumentException.ThrowIfNullOrEmpty(cle);

        lock (_verrou)
        {
            return Scalaire("SELECT valeur FROM meta WHERE cle = $cle", null, ("$cle", cle)) as string;
        }
    }

    /// <summary>Écrit une valeur dans la table <c>meta</c>, en remplaçant celle de la clé si elle existe.</summary>
    public void EcrireMeta(string cle, string valeur)
    {
        ArgumentException.ThrowIfNullOrEmpty(cle);
        ArgumentNullException.ThrowIfNull(valeur);

        lock (_verrou)
        {
            Executer(
                "INSERT INTO meta (cle, valeur) VALUES ($cle, $valeur) ON CONFLICT(cle) DO UPDATE SET valeur = excluded.valeur",
                null, ("$cle", cle), ("$valeur", valeur));
        }

        Notifier();
    }

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

    private int Compter(long ongletId) =>
        Convert.ToInt32(
            Scalaire("SELECT COUNT(*) FROM element WHERE onglet_id = $onglet", null, ("$onglet", ongletId)),
            CultureInfo.InvariantCulture);

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

    // ---- Suppression partagée entre les onglets et la purge de Temporaire ----

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

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)`, `0 Erreur(s)`, puis

```
Résumé de série de tests : Réussite!
  total : 250
  échec : 0
  réussie : 250
  ignoré : 0
```

Les 48 nouveaux tests : `DepotSuppressionTests` 12, `DepotPurgeTests` 14, `DepotIndexFtsTests` 11, `DepotMetaTests` 11.

Contrôle de l'ordre (vérifié par mutation) : si `AppliquerPlafond` supprime les plus récents, 4 tests échouent ; s'il trie par `id` seul, `Le_plafond_supprime_les_plus_anciens_par_date_d_usage_et_non_par_id` échoue ; s'il départage les dates égales par `id` décroissant, `A_date_egale_le_plafond_supprime_les_plus_petits_id` échoue.

- [ ] **Étape 5 : Vérifier sur le poste**

Sans objet : logique portable, entièrement couverte par les tests (le verrouillage d'un PNG est reproduit par un `FileStream` ouvert sans partage).

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Stockage/DepotHistorique.cs tests/PressePapiers.Tests/Outils/JournalCollecte.cs tests/PressePapiers.Tests/Stockage/DepotIndexFtsTests.cs tests/PressePapiers.Tests/Stockage/DepotMetaTests.cs tests/PressePapiers.Tests/Stockage/DepotPurgeTests.cs tests/PressePapiers.Tests/Stockage/DepotSuppressionTests.cs
git commit -m "Supprimer, purger, plafonner et réindexer l'historique" -m "Supprimer retire la ligne puis le PNG : un PNG absent n'est pas une erreur, un
PNG verrouillé est noté au journal, et seul le nom du fichier compte, jamais un
chemin lu en base. La purge ne touche que Temporaire. Le plafond retire les
plus anciens par date d'usage puis par Id, comme l'inverse de l'affichage.
L'index plein texte est contrôlé par integrity-check après chaque écriture et
se reconstruit à la demande. LireMeta et EcrireMeta servent le repère de session."
```
