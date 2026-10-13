### Tâche 4 : DepotHistorique : onglets

**Fichiers :**
- Créer : `tests/PressePapiers.Tests/Outils/DepotDeTest.cs`
- Modifier : `src/PressePapiers.Coeur/Stockage/DepotHistorique.cs` (le plan donne le contenu complet du fichier après modification)
- Tests : `tests/PressePapiers.Tests/Stockage/DepotOngletsTests.cs`

**Interfaces :**
- Utilise : (tâche 1) `BaseHistorique.Ouvrir(string)` et `BaseHistorique.Connexion` (`SqliteConnection`), le schéma de `SchemaBase`, `Onglet(long Id, string Nom, int Ordre, bool EstTemporaire)`, `IJournal`, `JournalNul.Instance`, les assistants de test `DossierTemporaire` et `InsertionsSql` ; (tâche 2) `DossierDonnees(string Racine)` avec `Images`, `Base`, `Racine` et `CreerDossiers()`.
- Fournit : `public DepotHistorique(BaseHistorique baseHistorique, DossierDonnees dossiers, TimeProvider horloge, IJournal journal)` ; `Onglet OngletTemporaire { get; }` ; `event Action? Modifie` ; `IReadOnlyList<Onglet> ListerOnglets()` ; `Onglet CreerOnglet(string nom)` ; `void RenommerOnglet(long ongletId, string nom)` ; `void DeplacerOnglet(long ongletId, int nouvelOrdre)` ; `void SupprimerOnglet(long ongletId)`. Assistant de test `PressePapiers.Tests.Outils.DepotDeTest : IDisposable`, construit par `new DepotDeTest(IJournal? journal = null, bool creerDossiers = true)`, qui expose `Dossiers` (`DossierDonnees`), `Base` (`BaseHistorique`), `Connexion`, `Horloge` (`FakeTimeProvider`, démarrée le 13/10/2026 à 08:00 UTC), `Depot`, `long InsererElement(long ongletId, string texte, DateTimeOffset? date = null, string? imageFichier = null)` (insertion SQL directe, avec un nom de PNG au choix) et `string[] NomsImages()`.

Remarques pour les tâches suivantes :
- `DepotHistorique` tient une seule connexion SQLite, qui n'est pas sûre entre fils : toutes les opérations passent par un verrou (`lock`, réentrant) pris sur la connexion elle-même, le même objet que verrouille `MoteurRecherche` (tâche 9), pour que la recherche et le dépôt n'utilisent jamais la connexion en même temps. Les tâches 5 et 6 complètent ce même fichier ; les assistants privés posés ici (`Preparer`, `Executer`, `Scalaire`, `LireOnglet`, `LireOnglets`, `SupprimerElementsDe`, `SupprimerFichiersImages`, `Notifier`) leur servent.
- `Modifie` et le verrou sont posés dès cette tâche, parce que les écritures d'onglets les exigent (le texte de la tâche 5 les cite, la tâche 5 les étend aux éléments). L'événement est levé après la libération du verrou, abonné par abonné : une exception d'un abonné est notée au journal (`Erreur`) et n'arrête ni l'écriture ni les autres abonnés. Il n'est pas levé quand rien n'a été écrit (échec, suppression d'un onglet absent).
- Erreurs : nom vide ou espaces seuls = `ArgumentException` (un nom `null` lève une `ArgumentNullException`, qui en dérive) ; onglet inconnu pour renommer ou déplacer = `ArgumentException` ; Temporaire (renommer, déplacer ou supprimer) = `InvalidOperationException` ; supprimer un onglet inconnu est sans effet. Les noms sont débarrassés de leurs espaces de bord.
- `DeplacerOnglet(id, nouvelOrdre)` : `nouvelOrdre` est la position finale dans `ListerOnglets()`, Temporaire valant 0. Une valeur inférieure à 1 range l'onglet juste après Temporaire, une valeur trop grande en dernier ; tous les ordres sont renumérotés de 1 à n sans trou. `CreerOnglet` prend l'ordre maximal plus 1 en une seule instruction SQL (pas de doublon d'ordre sous concurrence).
- `SupprimerOnglet` retire les éléments et l'onglet dans une transaction, puis supprime les PNG : un PNG orphelin est sans conséquence, une ligne sans PNG ne le serait pas. Un PNG absent ne lève rien ; seul le nom de fichier est retenu (`Path.GetFileName`), jamais un chemin lu en base. Les déclencheurs de la base tiennent l'index plein texte à jour.
- Les tâches 5 à 8 et 9 construisent leur dépôt de test avec `DepotDeTest` ; il ne crée que le dossier de données (`creerDossiers: false` laisse le dossier des images absent).
- Point de vigilance 4 (données abîmées) : un PNG manquant à la suppression d'un onglet ne fait pas échouer l'opération (test dédié).

- [ ] **Étape 1 : Écrire le test qui échoue**

L'assistant `DepotDeTest` ne dépend que de types déjà présents ; les tests couvrent la création unique de Temporaire, la liste, la création, le renommage, 9 déplacements, la suppression (éléments, PNG, index plein texte, PNG absent, Temporaire protégé), l'événement `Modifie` et une création simultanée de 40 onglets.

`tests/PressePapiers.Tests/Outils/DepotDeTest.cs`

```csharp
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Tests.Outils;

/// <summary>
/// Dépôt prêt à l'emploi sur une base et un dossier de données jetables, avec une horloge que le test règle.
/// L'horloge démarre le 13/10/2026 à 08:00 UTC.
/// </summary>
public sealed class DepotDeTest : IDisposable
{
    private readonly DossierTemporaire _racine = new();

    /// <summary>Crée le dossier de données, la base et le dépôt.</summary>
    /// <param name="journal">Journal du dépôt ; <see cref="JournalNul"/> par défaut.</param>
    /// <param name="creerDossiers">Faux pour laisser le dossier des images absent (le dépôt doit alors le créer).</param>
    public DepotDeTest(IJournal? journal = null, bool creerDossiers = true)
    {
        Dossiers = new DossierDonnees(_racine.Fichier("donnees"));
        if (creerDossiers)
        {
            Dossiers.CreerDossiers();
        }
        else
        {
            Directory.CreateDirectory(Dossiers.Racine);
        }

        Base = BaseHistorique.Ouvrir(Dossiers.Base);
        Horloge = new FakeTimeProvider(new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero));
        Depot = new DepotHistorique(Base, Dossiers, Horloge, journal ?? JournalNul.Instance);
    }

    /// <summary>Emplacements des données du test.</summary>
    public DossierDonnees Dossiers { get; }

    /// <summary>Base ouverte.</summary>
    public BaseHistorique Base { get; }

    /// <summary>Connexion de la base, pour les contrôles en SQL direct.</summary>
    public SqliteConnection Connexion => Base.Connexion;

    /// <summary>Horloge du dépôt, que le test avance.</summary>
    public FakeTimeProvider Horloge { get; }

    /// <summary>Dépôt testé.</summary>
    public DepotHistorique Depot { get; }

    /// <summary>
    /// Insère un élément texte directement en SQL, sans passer par le dépôt : on choisit la date, et le nom du PNG
    /// (même invalide) qu'une base abîmée pourrait contenir. L'empreinte dérive du texte, donc deux textes différents
    /// ont deux empreintes différentes. Renvoie l'identifiant de la ligne.
    /// </summary>
    public long InsererElement(long ongletId, string texte, DateTimeOffset? date = null, string? imageFichier = null)
    {
        using var commande = Connexion.CreateCommand();
        commande.CommandText = """
            INSERT INTO element (onglet_id, type, texte, texte_recherche, image_fichier, empreinte, date_copie, date_usage, taille)
            VALUES ($onglet, 1, $texte, $texte, $image, $empreinte, $date, $date, 1);
            SELECT last_insert_rowid();
            """;
        commande.Parameters.AddWithValue("$onglet", ongletId);
        commande.Parameters.AddWithValue("$texte", texte);
        commande.Parameters.AddWithValue("$image", (object?)imageFichier ?? DBNull.Value);
        commande.Parameters.AddWithValue("$empreinte", System.Text.Encoding.UTF8.GetBytes(texte));
        commande.Parameters.AddWithValue("$date", (date ?? Horloge.GetUtcNow()).ToString("O", CultureInfo.InvariantCulture));
        return Convert.ToInt64(commande.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>Noms des fichiers présents dans le dossier des images, triés.</summary>
    public string[] NomsImages() =>
        Directory.Exists(Dossiers.Images)
            ? [.. Directory.GetFiles(Dossiers.Images).Select(chemin => Path.GetFileName(chemin)).Order()]
            : [];

    /// <inheritdoc />
    public void Dispose()
    {
        Base.Dispose();
        _racine.Dispose();
    }
}
```

`tests/PressePapiers.Tests/Stockage/DepotOngletsTests.cs`

```csharp
using System.Globalization;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class DepotOngletsTests : IDisposable
{
    private readonly DepotDeTest _banc = new();

    private DepotHistorique Depot => _banc.Depot;

    public void Dispose() => _banc.Dispose();

    [Fact]
    public void Le_depot_cree_l_onglet_temporaire_une_seule_fois()
    {
        var temporaire = Depot.OngletTemporaire;

        Assert.Equal("Temporaire", temporaire.Nom);
        Assert.Equal(0, temporaire.Ordre);
        Assert.True(temporaire.EstTemporaire);

        // Un second dépôt sur la même base retrouve l'onglet au lieu d'en créer un autre.
        var second = new DepotHistorique(_banc.Base, _banc.Dossiers, _banc.Horloge, JournalNul.Instance);
        Assert.Equal(temporaire.Id, second.OngletTemporaire.Id);
        Assert.Equal("1", InsertionsSql.Scalaire(_banc.Connexion, "SELECT COUNT(*) FROM onglet WHERE est_temporaire = 1"));
        Assert.Single(Depot.ListerOnglets());
    }

    [Fact]
    public void Rouvrir_la_base_garde_le_meme_onglet_temporaire_et_les_onglets()
    {
        using var racine = new DossierTemporaire();
        var dossiers = new DossierDonnees(racine.Fichier("donnees"));
        dossiers.CreerDossiers();
        long idTemporaire;

        using (var premiere = BaseHistorique.Ouvrir(dossiers.Base))
        {
            var depot = new DepotHistorique(premiere, dossiers, TimeProvider.System, JournalNul.Instance);
            idTemporaire = depot.OngletTemporaire.Id;
            depot.CreerOnglet("Clients");
        }

        using var seconde = BaseHistorique.Ouvrir(dossiers.Base);
        var rouvert = new DepotHistorique(seconde, dossiers, TimeProvider.System, JournalNul.Instance);

        Assert.Equal(idTemporaire, rouvert.OngletTemporaire.Id);
        Assert.Equal(["Temporaire", "Clients"], rouvert.ListerOnglets().Select(o => o.Nom));
    }

    [Fact]
    public void Les_onglets_se_listent_dans_l_ordre_de_creation_Temporaire_en_premier()
    {
        Depot.CreerOnglet("Bravo");
        Depot.CreerOnglet("Alpha");
        Depot.CreerOnglet("Charlie");

        var onglets = Depot.ListerOnglets();

        // L'ordre n'est pas alphabétique : c'est celui de la création.
        Assert.Equal(["Temporaire", "Bravo", "Alpha", "Charlie"], onglets.Select(o => o.Nom));
        Assert.Equal([0, 1, 2, 3], onglets.Select(o => o.Ordre));
        Assert.Equal([true, false, false, false], onglets.Select(o => o.EstTemporaire));
    }

    [Fact]
    public void Creer_un_onglet_renvoie_l_onglet_ajoute_en_dernier()
    {
        Depot.CreerOnglet("Premier");

        var cree = Depot.CreerOnglet("Dernier");

        Assert.Equal("Dernier", cree.Nom);
        Assert.Equal(2, cree.Ordre);
        Assert.False(cree.EstTemporaire);
        Assert.Equal(cree, Depot.ListerOnglets()[^1]);
    }

    [Fact]
    public void Creer_un_onglet_retire_les_espaces_autour_du_nom()
    {
        var cree = Depot.CreerOnglet("  Clients  ");

        Assert.Equal("Clients", cree.Nom);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Creer_un_onglet_sans_nom_leve_ArgumentException(string nom)
    {
        Assert.Throws<ArgumentException>(() => Depot.CreerOnglet(nom));

        Assert.Single(Depot.ListerOnglets());
    }

    [Fact]
    public void Creer_un_onglet_sans_nom_null_leve_une_exception_d_argument()
    {
        Assert.ThrowsAny<ArgumentException>(() => Depot.CreerOnglet(null!));
    }

    [Fact]
    public void Renommer_un_onglet_change_son_nom_seulement()
    {
        var onglet = Depot.CreerOnglet("Ancien");

        Depot.RenommerOnglet(onglet.Id, "Nouveau");

        var relu = Depot.ListerOnglets().Single(o => o.Id == onglet.Id);
        Assert.Equal("Nouveau", relu.Nom);
        Assert.Equal(onglet.Ordre, relu.Ordre);
    }

    [Fact]
    public void Renommer_Temporaire_leve_InvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => Depot.RenommerOnglet(Depot.OngletTemporaire.Id, "Autre"));

        Assert.Equal("Temporaire", Depot.ListerOnglets()[0].Nom);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Renommer_avec_un_nom_vide_leve_ArgumentException_et_garde_le_nom(string nom)
    {
        var onglet = Depot.CreerOnglet("Garde");

        Assert.Throws<ArgumentException>(() => Depot.RenommerOnglet(onglet.Id, nom));

        Assert.Equal("Garde", Depot.ListerOnglets().Single(o => o.Id == onglet.Id).Nom);
    }

    [Fact]
    public void Renommer_un_onglet_inconnu_leve_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Depot.RenommerOnglet(9999, "Fantome"));
    }

    // Départ : Temporaire, A, B, C, D. L'ordre demandé est la position finale de l'onglet dans la liste (Temporaire = 0).
    [Theory]
    [InlineData("D", 1, "D,A,B,C")]
    [InlineData("D", 3, "A,B,D,C")]
    [InlineData("A", 99, "B,C,D,A")]
    [InlineData("A", 4, "B,C,D,A")]
    [InlineData("A", 1, "A,B,C,D")]
    [InlineData("B", 3, "A,C,B,D")]
    [InlineData("C", 1, "C,A,B,D")]
    [InlineData("C", 0, "C,A,B,D")]
    [InlineData("C", -5, "C,A,B,D")]
    public void Deplacer_un_onglet_le_range_a_la_position_demandee(string nom, int nouvelOrdre, string attendu)
    {
        var parNom = CreerAbcd();

        Depot.DeplacerOnglet(parNom[nom].Id, nouvelOrdre);

        var onglets = Depot.ListerOnglets();
        Assert.Equal(["Temporaire", .. attendu.Split(',')], onglets.Select(o => o.Nom));
        // Les ordres restent consécutifs, sans trou ni doublon.
        Assert.Equal(Enumerable.Range(0, 5), onglets.Select(o => o.Ordre));
        Assert.True(onglets[0].EstTemporaire);
    }

    [Fact]
    public void Deplacer_Temporaire_leve_InvalidOperationException_et_le_laisse_en_premier()
    {
        CreerAbcd();

        Assert.Throws<InvalidOperationException>(() => Depot.DeplacerOnglet(Depot.OngletTemporaire.Id, 3));

        Assert.Equal(["Temporaire", "A", "B", "C", "D"], Depot.ListerOnglets().Select(o => o.Nom));
    }

    [Fact]
    public void Deplacer_un_onglet_inconnu_leve_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Depot.DeplacerOnglet(9999, 1));
    }

    [Fact]
    public void Supprimer_un_onglet_supprime_ses_elements_et_leurs_png_et_laisse_le_reste()
    {
        var condamne = Depot.CreerOnglet("Condamne");
        var epargne = Depot.CreerOnglet("Epargne");
        var png1 = CreerPng("a1.png");
        var png2 = CreerPng("a2.png");
        var pngEpargne = CreerPng("b1.png");
        _banc.InsererElement(condamne.Id, "zebulon un", imageFichier: png1);
        _banc.InsererElement(condamne.Id, "zebulon deux", imageFichier: png2);
        var idEpargne = _banc.InsererElement(epargne.Id, "zebulon trois", imageFichier: pngEpargne);
        var idTemporaire = _banc.InsererElement(Depot.OngletTemporaire.Id, "zebulon quatre");

        Depot.SupprimerOnglet(condamne.Id);

        Assert.Equal(["Temporaire", "Epargne"], Depot.ListerOnglets().Select(o => o.Nom));
        Assert.Equal("0", InsertionsSql.Scalaire(_banc.Connexion, $"SELECT COUNT(*) FROM element WHERE onglet_id = {condamne.Id}"));
        Assert.Equal(["b1.png"], _banc.NomsImages());
        // Les suppressions passent par les déclencheurs : l'index plein texte ne garde que les deux éléments restants.
        Assert.Equal([idEpargne, idTemporaire], InsertionsSql.Trouver(_banc.Connexion, "\"zebulon\""));
    }

    [Fact]
    public void Supprimer_un_onglet_dont_un_png_manque_deja_ne_leve_pas_d_exception()
    {
        var onglet = Depot.CreerOnglet("Abime");
        _banc.InsererElement(onglet.Id, "perdu", imageFichier: "absent-du-disque.png");

        Depot.SupprimerOnglet(onglet.Id);

        Assert.Single(Depot.ListerOnglets());
        Assert.Equal("0", InsertionsSql.Scalaire(_banc.Connexion, "SELECT COUNT(*) FROM element"));
    }

    [Fact]
    public void Supprimer_Temporaire_leve_InvalidOperationException_et_garde_ses_elements()
    {
        _banc.InsererElement(Depot.OngletTemporaire.Id, "precieux");

        Assert.Throws<InvalidOperationException>(() => Depot.SupprimerOnglet(Depot.OngletTemporaire.Id));

        Assert.Single(Depot.ListerOnglets());
        Assert.Equal("1", InsertionsSql.Scalaire(_banc.Connexion, "SELECT COUNT(*) FROM element"));
    }

    [Fact]
    public void Supprimer_un_onglet_inconnu_est_sans_effet()
    {
        Depot.CreerOnglet("Reste");

        Depot.SupprimerOnglet(9999);

        Assert.Equal(["Temporaire", "Reste"], Depot.ListerOnglets().Select(o => o.Nom));
    }

    [Fact]
    public void Un_onglet_cree_apres_une_suppression_passe_quand_meme_en_dernier()
    {
        var parNom = CreerAbcd();
        Depot.SupprimerOnglet(parNom["B"].Id);

        var cree = Depot.CreerOnglet("E");

        // La suppression laisse un trou dans les ordres (1, 3, 4) : le nouvel onglet prend l'ordre après le plus grand.
        Assert.Equal(["Temporaire", "A", "C", "D", "E"], Depot.ListerOnglets().Select(o => o.Nom));
        Assert.True(cree.Ordre > parNom["D"].Ordre);
    }

    [Fact]
    public void Modifie_est_levee_apres_chaque_ecriture_reussie_et_jamais_apres_un_echec()
    {
        var appels = 0;
        Depot.Modifie += () => Interlocked.Increment(ref appels);

        var onglet = Depot.CreerOnglet("A");
        Assert.Equal(1, appels);
        Depot.RenommerOnglet(onglet.Id, "B");
        Assert.Equal(2, appels);
        Depot.DeplacerOnglet(onglet.Id, 1);
        Assert.Equal(3, appels);
        Depot.SupprimerOnglet(onglet.Id);
        Assert.Equal(4, appels);

        // Échecs et suppression d'un onglet absent : rien n'a été écrit, rien n'est signalé.
        Assert.Throws<ArgumentException>(() => Depot.CreerOnglet(" "));
        Assert.Throws<InvalidOperationException>(() => Depot.RenommerOnglet(Depot.OngletTemporaire.Id, "x"));
        Assert.Throws<InvalidOperationException>(() => Depot.SupprimerOnglet(Depot.OngletTemporaire.Id));
        Depot.SupprimerOnglet(9999);
        Assert.Equal(4, appels);
    }

    [Fact]
    public void Un_abonne_qui_leve_une_exception_ne_fait_pas_echouer_l_ecriture_ni_les_autres_abonnes()
    {
        var suivantAppele = false;
        Depot.Modifie += () => throw new InvalidOperationException("abonné défaillant");
        Depot.Modifie += () => suivantAppele = true;

        var cree = Depot.CreerOnglet("Cree quand meme");

        Assert.Contains(cree, Depot.ListerOnglets());
        Assert.True(suivantAppele);
    }

    [Fact]
    public void Un_abonne_peut_relire_le_depot_pendant_l_evenement()
    {
        IReadOnlyList<Onglet>? vus = null;
        Depot.Modifie += () => vus = Depot.ListerOnglets();

        Depot.CreerOnglet("Visible");

        // L'événement est levé après la libération du verrou : un abonné peut rappeler le dépôt sans blocage.
        Assert.Equal(["Temporaire", "Visible"], vus!.Select(o => o.Nom));
    }

    [Fact]
    public void Des_creations_simultanees_donnent_des_ordres_uniques_et_consecutifs()
    {
        Parallel.For(0, 40, i => Depot.CreerOnglet("Onglet " + i.ToString(CultureInfo.InvariantCulture)));

        var onglets = Depot.ListerOnglets();

        Assert.Equal(41, onglets.Count);
        Assert.Equal(Enumerable.Range(0, 41), onglets.Select(o => o.Ordre));
    }

    // Quatre onglets A, B, C, D créés dans cet ordre, indexés par leur nom.
    private Dictionary<string, Onglet> CreerAbcd() =>
        new[] { "A", "B", "C", "D" }.ToDictionary(nom => nom, nom => Depot.CreerOnglet(nom));

    private string CreerPng(string nom)
    {
        Directory.CreateDirectory(_banc.Dossiers.Images);
        File.WriteAllBytes(Path.Combine(_banc.Dossiers.Images, nom), [1, 2, 3]);
        return nom;
    }
}
```

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : les 34 nouveaux tests échouent avec `System.NotImplementedException : Tâche 4` (le constructeur de la coquille lève), les 128 tests des tâches 1 à 3 restent verts :

```
Résumé de série de tests : Échec!
  total : 162
  échec : 34
  réussie : 128
  ignoré : 0
```

- [ ] **Étape 3 : Implémenter**

Remplacer les membres d'onglets de la coquille ; les membres des tâches 5 et 6 restent des coquilles (`NotImplementedException`), le pragma `CS0067` en tête de fichier disparaît puisque `Modifie` est maintenant levé.

`src/PressePapiers.Coeur/Stockage/DepotHistorique.cs`

```csharp
using System.Globalization;
using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// Dépôt des onglets et des éléments, sûr entre fils : la connexion SQLite, qui ne l'est pas, sert elle-même de verrou,
/// partagé avec <see cref="Recherche.MoteurRecherche"/>.
/// Onglets par la tâche 4, éléments par la tâche 5, suppression, purge, plafond, index et méta par la tâche 6.
/// </summary>
public sealed class DepotHistorique
{
    private const string NomTemporaire = "Temporaire";

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
    public int CompterElements(long ongletId) => throw new NotImplementedException("Tâche 5");

    /// <summary>Éléments d'un onglet, par DateUsage décroissante puis Id décroissant (ordre stable à date égale).</summary>
    public IReadOnlyList<Element> ListerElements(long ongletId) => throw new NotImplementedException("Tâche 5");

    /// <summary>Lit un élément, ou null s'il n'existe pas.</summary>
    public Element? Lire(long elementId) => throw new NotImplementedException("Tâche 5");

    /// <summary>
    /// Ajoute une copie à Temporaire avec dédoublonnage, écrit le PNG d'une image ;
    /// renvoie l'élément créé ou remonté.
    /// </summary>
    public Element AjouterCopie(ContenuCopie contenu) => throw new NotImplementedException("Tâche 5");

    /// <summary>Donne un nom à un élément et le range dans un onglet autre que Temporaire.</summary>
    public Element Nommer(long elementId, string nom, long ongletId) => throw new NotImplementedException("Tâche 5");

    /// <summary>Met à jour la date de dernier usage d'un élément.</summary>
    public void MarquerUtilise(long elementId) => throw new NotImplementedException("Tâche 5");

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

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)`, `0 Erreur(s)`, puis

```
Résumé de série de tests : Réussite!
  total : 162
  échec : 0
  réussie : 162
  ignoré : 0
```

Les 34 nouveaux tests : `DepotOngletsTests` 34 (création unique de Temporaire, réouverture de la base, liste, création, noms vides, renommage, 9 déplacements, Temporaire protégé, suppression d'onglet avec PNG et index, PNG absent, événement, abonné défaillant, rappel du dépôt pendant l'événement, 40 créations simultanées).

Contrôle de l'ordre et du verrou (vérifié par mutation) : `Les_onglets_se_listent_dans_l_ordre_de_creation_Temporaire_en_premier` et les déplacements échouent si `ListerOnglets` trie par nom (17 échecs au total) ; `Des_creations_simultanees_donnent_des_ordres_uniques_et_consecutifs` échoue sans le verrou (la connexion SQLite corrompue lève une `NullReferenceException`).

- [ ] **Étape 5 : Vérifier sur le poste**

Sans objet : logique portable, entièrement couverte par les tests (concurrence comprise).

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Stockage/DepotHistorique.cs tests/PressePapiers.Tests/Outils/DepotDeTest.cs tests/PressePapiers.Tests/Stockage/DepotOngletsTests.cs
git commit -m "Gérer les onglets du dépôt d'historique" -m "Le dépôt crée l'onglet Temporaire à la première ouverture, puis crée, renomme,
déplace et supprime les onglets sous le verrou de la connexion. Supprimer un onglet
retire ses éléments dans une transaction, puis leurs PNG. L'événement Modifie
est levé hors du verrou, chaque abonné étant isolé des autres."
```
