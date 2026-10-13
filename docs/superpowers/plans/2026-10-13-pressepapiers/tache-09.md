### Tâche 9 : MoteurRecherche

**Fichiers :**
- Modifier : `src/PressePapiers.Coeur/Recherche/MoteurRecherche.cs` (le plan donne le contenu complet du fichier après modification)
- Créer : aucun
- Tests : `tests/PressePapiers.Tests/Recherche/MoteurRechercheTests.cs` (créé)
- Tests : `tests/PressePapiers.Tests/Recherche/RechercheConcurrenteTests.cs` (créé)

**Interfaces :**
- Utilise (tâches 1 et 2) :
  - `BaseHistorique.Connexion` (`SqliteConnection`), schéma de `SchemaBase` : table `element` (colonne `texte_recherche`), index
    `element_fts` (FTS5, tokenizer `trigram`, `content='element'`), tables `onglet` et `meta` ;
  - `internal static class LecteurElement` : `const string Colonnes`, `Element Lire(SqliteDataReader)` ;
  - `Normalisation.Normaliser(string?)` et `Normalisation.TexteRecherche(string? nom, string? texte, IReadOnlyList<string>? fichiers, string? application)` ;
  - `Element` (record du modèle) ; tests : `DossierTemporaire`, `InsertionsSql.InsererOnglet(SqliteConnection, string, int, bool)`.
- Fournit :
  - `public MoteurRecherche(BaseHistorique baseHistorique)` (lève `ArgumentNullException` si nul) ;
  - `public IReadOnlyList<Element> Chercher(string requete, int limite = 500)` : tous les mots requis ; MATCH pour les mots de
    3 caractères Unicode ou plus, LIKE pour les mots de 1 ou 2 ; requête vide, espaces seuls ou `limite <= 0` : liste vide ;
    `DateUsage` puis `Id` décroissants ; jamais d'exception sur une saisie hostile ;
  - limites documentées : 16 mots distincts au plus, mots tronqués à 128 unités UTF-16 (sans couper un emoji), caractère nul traité
    en séparateur ;
  - verrou : la recherche prend `lock (baseHistorique.Connexion)` pendant toute la vie de sa commande (création, lecture,
    libération : la connexion tient la liste de ses commandes sans protection), le même objet que le verrou de
    `DepotHistorique` (tâche 4). Consommateurs : tâche 25 (zone de recherche de `FenetrePrincipaleVue`), et la capture de
    la tâche 23, qui écrit dans le dépôt pendant que la fenêtre cherche.

- [ ] **Étape 1 : Écrire le test qui échoue**

`tests/PressePapiers.Tests/Recherche/MoteurRechercheTests.cs`

Les lignes sont insérées en SQL direct (colonnes de `LecteurElement.Colonnes`, `texte_recherche` calculé par
`Normalisation.TexteRecherche`) : le test ne dépend pas de `DepotHistorique`.

```csharp
using System.Globalization;
using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Recherche;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Recherche;

public sealed class MoteurRechercheTests : IDisposable
{
    private static readonly DateTimeOffset Debut = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);

    private readonly DossierTemporaire _dossier = new();
    private readonly BaseHistorique _base;
    private readonly MoteurRecherche _moteur;
    private readonly long _ongletA;
    private readonly long _ongletB;

    public MoteurRechercheTests()
    {
        _base = BaseHistorique.Ouvrir(_dossier.Fichier("historique.db"));
        _moteur = new MoteurRecherche(_base);
        _ongletA = InsertionsSql.InsererOnglet(_base.Connexion, "Temporaire", 0, temporaire: true);
        _ongletB = InsertionsSql.InsererOnglet(_base.Connexion, "Projets", 1);
    }

    public void Dispose()
    {
        _base.Dispose();
        _dossier.Dispose();
    }

    [Fact]
    public void Tous_les_mots_sont_requis_dans_n_importe_quel_ordre()
    {
        var chatChien = Inserer("le chat et le chien dorment");
        Inserer("le chat dort seul");
        Inserer("le chien aboie");

        Assert.Equal([chatChien], Ids(_moteur.Chercher("chien chat")));
        Assert.Equal([chatChien], Ids(_moteur.Chercher("chat chien")));
        Assert.Empty(_moteur.Chercher("chat chien poisson"));
    }

    [Fact]
    public void La_recherche_ignore_la_casse_les_accents_et_les_ligatures()
    {
        var ecole = Inserer("École Élémentaire");
        var oeuvre = Inserer("Une Œuvre majeure");

        Assert.Equal([ecole], Ids(_moteur.Chercher("ecole")));
        Assert.Equal([ecole], Ids(_moteur.Chercher("ÉCOLE élémentaire")));
        Assert.Equal([oeuvre], Ids(_moteur.Chercher("OEUVRE")));
        Assert.Equal([oeuvre], Ids(_moteur.Chercher("œuvre")));
    }

    [Fact]
    public void La_recherche_porte_sur_le_nom_le_texte_les_fichiers_et_l_application()
    {
        var parNom = Inserer("contenu quelconque", nom: "Facture Dupont");
        var parFichier = Inserer(null, fichiers: ["C:\\Clients\\Martin\\contrat.pdf"]);
        var parApplication = Inserer("autre contenu", application: "WindowsTerminal");

        Assert.Equal([parNom], Ids(_moteur.Chercher("dupont")));
        Assert.Equal([parFichier], Ids(_moteur.Chercher("martin contrat")));
        Assert.Equal([parApplication], Ids(_moteur.Chercher("windowsterminal")));
    }

    [Fact]
    public void La_recherche_parcourt_tous_les_onglets()
    {
        var dansA = Inserer("rapport trimestriel", ongletId: _ongletA);
        var dansB = Inserer("rapport annuel", ongletId: _ongletB);

        Assert.Equal(new HashSet<long> { dansA, dansB }, Ids(_moteur.Chercher("rapport")).ToHashSet());
    }

    [Fact]
    public void Un_mot_de_deux_caracteres_passe_par_like_et_trouve_une_sous_chaine()
    {
        var abeille = Inserer("la petite abeille");
        Inserer("le chat dort");

        Assert.Equal([abeille], Ids(_moteur.Chercher("ab")));
        Assert.Equal([abeille], Ids(_moteur.Chercher("bei")));
        Assert.Empty(_moteur.Chercher("zz"));
    }

    [Fact]
    public void Un_mot_court_et_un_mot_long_sont_tous_deux_requis()
    {
        var voulu = Inserer("ok pour le budget");
        Inserer("budget seul");
        Inserer("ok sans le reste");

        Assert.Equal([voulu], Ids(_moteur.Chercher("ok budget")));
    }

    [Fact]
    public void Les_caracteres_joker_de_like_sont_des_caracteres_ordinaires()
    {
        var pourcentage = Inserer("remise de 100% aujourd'hui");
        var sansPourcentage = Inserer("remise de 100 euros");
        var souligne = Inserer("a_b");
        var sansSouligne = Inserer("axb");

        Assert.Equal([pourcentage], Ids(_moteur.Chercher("%")));
        Assert.Equal([pourcentage], Ids(_moteur.Chercher("0%")));
        Assert.Equal([souligne], Ids(_moteur.Chercher("_")));
        Assert.Equal([souligne], Ids(_moteur.Chercher("a_")));
        Assert.DoesNotContain(sansPourcentage, Ids(_moteur.Chercher("%")));
        Assert.DoesNotContain(sansSouligne, Ids(_moteur.Chercher("a_")));
    }

    [Fact]
    public void L_antislash_est_un_caractere_ordinaire()
    {
        var chemin = Inserer("dossier c:\\tmp");
        Inserer("dossier c:/tmp");

        Assert.Equal([chemin], Ids(_moteur.Chercher("\\")));
        Assert.Equal([chemin], Ids(_moteur.Chercher("c:\\tmp")));
    }

    [Fact]
    public void L_ordre_est_DateUsage_decroissante_puis_Id_decroissant()
    {
        // Insérés dans cet ordre : l'identifiant le plus petit porte la date la plus récente.
        var ancien = Inserer("mot commun", usage: Debut.AddHours(1));
        var recentBas = Inserer("mot commun", usage: Debut.AddHours(5));
        var egalA = Inserer("mot commun", usage: Debut.AddHours(3));
        var egalB = Inserer("mot commun", usage: Debut.AddHours(3));

        // Date décroissante d'abord (recentBas avant tout le reste), puis Id décroissant à date égale (egalB avant egalA).
        Assert.Equal([recentBas, egalB, egalA, ancien], Ids(_moteur.Chercher("commun")));
        // Le chemin LIKE suit le même ordre.
        Assert.Equal([recentBas, egalB, egalA, ancien], Ids(_moteur.Chercher("mo")));
    }

    [Fact]
    public void La_limite_borne_le_nombre_de_resultats_en_gardant_les_plus_recents()
    {
        var premier = Inserer("serie", usage: Debut.AddMinutes(1));
        var deuxieme = Inserer("serie", usage: Debut.AddMinutes(2));
        var troisieme = Inserer("serie", usage: Debut.AddMinutes(3));

        Assert.Equal([troisieme, deuxieme], Ids(_moteur.Chercher("serie", limite: 2)));
        Assert.Equal([troisieme], Ids(_moteur.Chercher("serie", limite: 1)));
        Assert.Empty(_moteur.Chercher("serie", limite: 0));
        Assert.Empty(_moteur.Chercher("serie", limite: -1));
        Assert.Equal(3, _moteur.Chercher("serie").Count);
        Assert.DoesNotContain(premier, Ids(_moteur.Chercher("serie", limite: 2)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("      ")]
    [InlineData("\t\r\n")]
    public void Une_requete_vide_ou_faite_d_espaces_donne_une_liste_vide(string requete)
    {
        Inserer("du texte");

        Assert.Empty(_moteur.Chercher(requete));
    }

    [Fact]
    public void Les_mots_AND_OR_NOT_sont_cherches_comme_du_texte()
    {
        var litteral = Inserer("chat and chien");
        Inserer("chat chien");
        Inserer("chat seul");

        // Lus comme de la syntaxe FTS5, « OR » et « NOT » élargiraient ou restreindraient autrement le résultat.
        Assert.Equal([litteral], Ids(_moteur.Chercher("chat AND chien")));
        Assert.Empty(_moteur.Chercher("chat OR chien"));
        Assert.Empty(_moteur.Chercher("chat NOT chien"));
        var avecOr = Inserer("chat or chien");
        Assert.Equal([avecOr], Ids(_moteur.Chercher("chat OR chien")));
    }

    [Fact]
    public void Les_guillemets_l_etoile_et_les_deux_points_sont_cherches_comme_du_texte()
    {
        var guillemet = Inserer("il a dit \"bonjour\" hier");
        var etoile = Inserer("fichier abc*def");
        var sansEtoile = Inserer("fichier abcdef");
        var deuxPoints = Inserer("clef titre:valeur");

        Assert.Equal([guillemet], Ids(_moteur.Chercher("\"bonjour\"")));
        Assert.Equal([guillemet], Ids(_moteur.Chercher("\"bonjour")));
        Assert.Equal([etoile], Ids(_moteur.Chercher("abc*")));
        Assert.DoesNotContain(sansEtoile, Ids(_moteur.Chercher("abc*")));
        Assert.Equal([deuxPoints], Ids(_moteur.Chercher("titre:valeur")));
        Assert.Equal([deuxPoints], Ids(_moteur.Chercher("titre:")));
    }

    [Fact]
    public void Un_emoji_est_un_seul_caractere_pour_le_choix_entre_match_et_like()
    {
        var deux = Inserer("bravo 😀😀 merci");
        var trois = Inserer("fete 😀😀😀 finale");

        // Deux emojis = 2 caractères (4 unités UTF-16) : LIKE, pas MATCH, sinon l'index trigram ne trouve rien.
        Assert.Contains(deux, Ids(_moteur.Chercher("😀😀")));
        Assert.Contains(deux, Ids(_moteur.Chercher("😀")));
        Assert.Equal([trois], Ids(_moteur.Chercher("😀😀😀")));
    }

    [Theory]
    [InlineData("\"")]
    [InlineData("\"\"")]
    [InlineData("\"\"\"")]
    [InlineData("\"abc")]
    [InlineData("abc\"")]
    [InlineData("*")]
    [InlineData("abc*")]
    [InlineData("*abc")]
    [InlineData(":")]
    [InlineData("a:b")]
    [InlineData("texte_recherche:abc")]
    [InlineData("AND")]
    [InlineData("OR")]
    [InlineData("NOT")]
    [InlineData("abc AND")]
    [InlineData("AND abc")]
    [InlineData("abc OR def")]
    [InlineData("abc NOT def")]
    [InlineData("NEAR(abc def)")]
    [InlineData("(abc")]
    [InlineData("abc)")]
    [InlineData("((()))")]
    [InlineData("-abc")]
    [InlineData("^abc")]
    [InlineData("%")]
    [InlineData("%%%")]
    [InlineData("_")]
    [InlineData("___")]
    [InlineData("\\")]
    [InlineData("\\%")]
    [InlineData("'")]
    [InlineData("'; DROP TABLE element; --")]
    [InlineData("😀")]
    [InlineData("😀😀😀😀")]
    [InlineData("\uD83D")]
    [InlineData("a\uD83Db")]
    [InlineData("abc\0def")]
    [InlineData("\0")]
    [InlineData("é")]
    [InlineData("ÀÉÎÕÜ œæ")]
    public void Une_saisie_hostile_ne_leve_jamais_d_exception_et_ne_touche_pas_aux_donnees(string requete)
    {
        Inserer("abc def 100% a_b \"x\" 😀 titre:valeur");

        var resultats = _moteur.Chercher(requete);

        Assert.NotNull(resultats);
        Assert.Equal("1", InsertionsSql.Scalaire(_base.Connexion, "SELECT COUNT(*) FROM element"));
    }

    [Fact]
    public void Une_requete_tres_longue_ou_de_tres_nombreux_mots_ne_leve_pas_d_exception()
    {
        var present = Inserer("mot0 mot1 mot2 mot3 mot4 mot5 mot6 mot7 mot8 mot9 mot10 mot11 mot12 mot13 mot14 mot15");

        var enormeMot = new string('x', 2_000_000);
        var beaucoupDeMotsLongs = string.Join(' ', Enumerable.Range(0, 5000).Select(i => "mot" + i.ToString(CultureInfo.InvariantCulture)));
        var beaucoupDeMotsCourts = string.Join(' ', Enumerable.Range(0, 5000).Select(i => "m" + (i % 10).ToString(CultureInfo.InvariantCulture)));

        var chrono = System.Diagnostics.Stopwatch.StartNew();
        Assert.Empty(_moteur.Chercher(enormeMot));
        Assert.NotNull(_moteur.Chercher(beaucoupDeMotsCourts));
        // Seuls les 16 premiers mots distincts comptent : mot0 à mot15 sont tous présents, le reste est ignoré.
        Assert.Equal([present], Ids(_moteur.Chercher(beaucoupDeMotsLongs)));
        Assert.Equal([present], Ids(_moteur.Chercher(beaucoupDeMotsLongs + " absent")));
        chrono.Stop();

        Assert.True(chrono.Elapsed < TimeSpan.FromSeconds(2), $"Recherche trop lente : {chrono.Elapsed}");
    }

    [Fact]
    public void Un_mot_tres_long_est_raccourci_sans_couper_un_emoji()
    {
        var present = Inserer(new string('a', 127) + "😀" + "suite");

        // Coupé à 128 unités au milieu de la paire de substituts, le mot ferait lever Normaliser ou fausserait le MATCH.
        Assert.Equal([present], Ids(_moteur.Chercher(new string('a', 127) + "😀" + new string('z', 500))));
    }

    [Fact]
    public void Une_suppression_ou_une_mise_a_jour_est_vue_par_la_recherche()
    {
        var id = Inserer("document provisoire");
        Assert.Equal([id], Ids(_moteur.Chercher("provisoire")));

        using (var commande = _base.Connexion.CreateCommand())
        {
            commande.CommandText = "UPDATE element SET texte_recherche = 'document definitif' WHERE id = $id";
            commande.Parameters.AddWithValue("$id", id);
            commande.ExecuteNonQuery();
        }

        Assert.Empty(_moteur.Chercher("provisoire"));
        Assert.Equal([id], Ids(_moteur.Chercher("definitif")));
    }

    [Fact]
    public void Le_constructeur_refuse_une_base_nulle()
    {
        Assert.Throws<ArgumentNullException>(() => new MoteurRecherche(null!));
    }

    [Fact]
    public void Chercher_sur_un_gros_historique_reste_rapide()
    {
        using var transaction = _base.Connexion.BeginTransaction();
        for (var i = 0; i < 3000; i++)
        {
            Inserer("ligne numero " + i.ToString(CultureInfo.InvariantCulture) + " du journal", usage: Debut.AddSeconds(i));
        }

        transaction.Commit();

        var chrono = System.Diagnostics.Stopwatch.StartNew();
        var long_ = _moteur.Chercher("numero 2999");
        var court = _moteur.Chercher("99");
        chrono.Stop();

        Assert.Single(long_);
        Assert.NotEmpty(court);
        Assert.True(chrono.Elapsed < TimeSpan.FromSeconds(2), $"Recherche trop lente : {chrono.Elapsed}");
    }

    private static List<long> Ids(IReadOnlyList<PressePapiers.Coeur.Modele.Element> elements) =>
        elements.Select(e => e.Id).ToList();

    // Insertion SQL directe : DepotHistorique n'est pas utilisé ici. Le texte de recherche est calculé comme le fera le dépôt.
    private long Inserer(
        string? texte,
        string? nom = null,
        string? application = null,
        IReadOnlyList<string>? fichiers = null,
        long? ongletId = null,
        DateTimeOffset? usage = null)
    {
        using var commande = _base.Connexion.CreateCommand();
        commande.CommandText = $"""
            INSERT INTO element (onglet_id, nom, type, texte, texte_recherche, empreinte, application, date_copie, date_usage, taille)
            VALUES ($onglet, $nom, $type, $texte, $recherche, $empreinte, $application, $date, $usage, $taille);
            SELECT last_insert_rowid();
            """;
        commande.Parameters.AddWithValue("$onglet", ongletId ?? _ongletA);
        commande.Parameters.AddWithValue("$nom", (object?)nom ?? DBNull.Value);
        commande.Parameters.AddWithValue("$type", fichiers is null ? 1 : 4);
        var contenu = fichiers is null ? texte : string.Join('\n', fichiers);
        commande.Parameters.AddWithValue("$texte", (object?)contenu ?? DBNull.Value);
        commande.Parameters.AddWithValue("$recherche", Normalisation.TexteRecherche(nom, texte, fichiers, application));
        commande.Parameters.AddWithValue("$empreinte", Guid.NewGuid().ToByteArray());
        commande.Parameters.AddWithValue("$application", (object?)application ?? DBNull.Value);
        commande.Parameters.AddWithValue("$date", Debut.ToString("O", CultureInfo.InvariantCulture));
        commande.Parameters.AddWithValue("$usage", (usage ?? Debut).ToString("O", CultureInfo.InvariantCulture));
        commande.Parameters.AddWithValue("$taille", 10);
        return Convert.ToInt64(commande.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}
```

`tests/PressePapiers.Tests/Recherche/RechercheConcurrenteTests.cs`

Test de charge sur deux fils : un fil ajoute des copies et applique le plafond de Temporaire (qui supprime dans une
transaction), l'autre cherche en boucle. Il échoue si le moteur et le dépôt ne prennent pas le même verrou
(`InvalidOperationException` : commande hors de la transaction en cours), ou si la commande est créée ou libérée hors du
verrou (`NullReferenceException` à la fermeture de la connexion, liste des commandes abîmée).

```csharp
using PressePapiers.Coeur.Recherche;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Recherche;

/// <summary>
/// Le dépôt et le moteur de recherche partagent la même connexion SQLite, qui n'est pas sûre entre fils : ils doivent
/// prendre le même verrou. Dans l'application, la capture écrit pendant que la fenêtre cherche.
/// </summary>
public sealed class RechercheConcurrenteTests : IDisposable
{
    private readonly DepotDeTest _banc = new();

    public void Dispose() => _banc.Dispose();

    [Fact]
    public void Une_recherche_pendant_des_ajouts_sur_un_autre_fil_ne_leve_jamais()
    {
        var moteur = new MoteurRecherche(_banc.Base);
        var erreurs = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var ecritureFinie = false;

        // Comme la capture : un ajout puis le plafond de Temporaire, qui supprime dans une transaction dès qu'il est
        // dépassé. Sans verrou commun, une recherche lancée pendant cette transaction sur la même connexion échoue.
        var ecrivain = new Thread(() =>
        {
            try
            {
                for (var i = 0; i < 400; i++)
                {
                    _banc.Depot.AjouterCopie(ContenusDeTest.Texte($"copie numéro {i} du texte cherché"));
                    _banc.Depot.AppliquerPlafond(50);
                }
            }
            catch (Exception exception)
            {
                erreurs.Enqueue(exception);
            }
            finally
            {
                Volatile.Write(ref ecritureFinie, true);
            }
        });

        var lecteur = new Thread(() =>
        {
            try
            {
                while (!Volatile.Read(ref ecritureFinie))
                {
                    moteur.Chercher("texte cherché");
                }
            }
            catch (Exception exception)
            {
                erreurs.Enqueue(exception);
            }
        });

        ecrivain.Start();
        lecteur.Start();
        ecrivain.Join();
        lecteur.Join();

        Assert.Empty(erreurs);
        Assert.Equal(50, moteur.Chercher("texte cherché").Count);
    }
}
```

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
```

Attendu avant l'implémentation (la coquille lève `NotImplementedException("Tâche 9")` dans le constructeur) : la solution
compile, puis

```
Résumé de série de tests : Échec!
  total : 350
  échec : 63
  réussie : 287
  ignoré : 0
```

(`System.NotImplementedException : Tâche 9` sur `MoteurRecherche..ctor` : les 63 nouveaux tests échouent, 62 de `MoteurRechercheTests` et le test de charge, les 287 des tâches 1 à 8 passent.)

- [ ] **Étape 3 : Implémenter**

`src/PressePapiers.Coeur/Recherche/MoteurRecherche.cs` (remplace la coquille, contenu complet)

```csharp
using System.Text;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Coeur.Recherche;

/// <summary>
/// Recherche plein texte dans tous les onglets (spec, section 7). Aucune saisie de l'utilisateur n'est jamais lue
/// comme de la syntaxe : les mots longs passent entre guillemets doubles, les mots courts par des paramètres LIKE.
/// </summary>
public sealed class MoteurRecherche
{
    // Sous trois caractères, l'index trigram ne trouve rien : on se rabat sur LIKE.
    private const int LongueurMinimaleIndex = 3;

    // Borne le nombre de conditions SQL (et de paramètres) quelle que soit la saisie ; une phrase de plus
    // de 16 mots distincts ne sert pas à chercher, elle vient d'un collage maladroit.
    private const int NombreMaxMots = 16;

    // Longueur maximale (en unités UTF-16) d'un mot pris en compte.
    private const int LongueurMaxMot = 128;

    private readonly BaseHistorique _base;

    /// <summary>Crée le moteur sur une base ouverte.</summary>
    public MoteurRecherche(BaseHistorique baseHistorique)
    {
        ArgumentNullException.ThrowIfNull(baseHistorique);
        _base = baseHistorique;
    }

    /// <summary>
    /// Tous les mots sont requis ; un mot de 3 caractères ou plus passe par MATCH (entre guillemets doubles),
    /// un mot de 1 ou 2 caractères par LIKE avec ESCAPE ; liste vide si la requête est vide ; DateUsage décroissante,
    /// puis Id décroissant à date égale. Seuls les 16 premiers mots distincts comptent.
    /// </summary>
    /// <param name="requete">Saisie brute de l'utilisateur, normalisée ici comme l'est le texte indexé.</param>
    /// <param name="limite">Nombre maximal de résultats ; zéro ou moins donne une liste vide.</param>
    public IReadOnlyList<Element> Chercher(string requete, int limite = 500)
    {
        if (limite <= 0)
        {
            return [];
        }

        var mots = Decouper(requete);
        if (mots.Count == 0)
        {
            return [];
        }

        // La connexion est partagée avec le dépôt, qui la verrouille aussi : on ne l'utilise pas deux fois en même temps.
        // Le verrou couvre toute la vie de la commande, car la connexion tient la liste de ses commandes sans protection.
        lock (_base.Connexion)
        {
            return ChercherSousVerrou(mots, limite);
        }
    }

    private List<Element> ChercherSousVerrou(List<string> mots, int limite)
    {
        using var commande = _base.Connexion.CreateCommand();
        var conditions = new List<string>();

        var longs = mots.Where(m => NombreDeCaracteres(m) >= LongueurMinimaleIndex).ToList();
        if (longs.Count > 0)
        {
            conditions.Add("id IN (SELECT rowid FROM element_fts WHERE element_fts MATCH $match)");
            commande.Parameters.AddWithValue("$match", string.Join(" AND ", longs.Select(EntreGuillemets)));
        }

        var courts = mots.Where(m => NombreDeCaracteres(m) < LongueurMinimaleIndex).ToList();
        for (var i = 0; i < courts.Count; i++)
        {
            conditions.Add($"texte_recherche LIKE $court{i} ESCAPE '\\'");
            commande.Parameters.AddWithValue($"$court{i}", "%" + EchapperLike(courts[i]) + "%");
        }

        commande.CommandText = $"""
            SELECT {LecteurElement.Colonnes}
            FROM element
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY date_usage DESC, id DESC
            LIMIT $limite
            """;
        commande.Parameters.AddWithValue("$limite", limite);

        var resultats = new List<Element>();
        using var lecteur = commande.ExecuteReader();
        while (lecteur.Read())
        {
            resultats.Add(LecteurElement.Lire(lecteur));
        }

        return resultats;
    }

    // Mots distincts de la requête normalisée, dans l'ordre de saisie.
    // Un caractère nul est traité en séparateur : SQLite lit les textes jusqu'au premier nul et refuserait la chaîne.
    private static List<string> Decouper(string? requete) =>
        Normalisation.Normaliser(requete?.Replace('\0', ' '))
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(Raccourcir)
            .Distinct(StringComparer.Ordinal)
            .Take(NombreMaxMots)
            .ToList();

    // Un mot de plusieurs millions de caractères viendrait d'un collage accidentel et ferait travailler l'index
    // pendant des minutes ; ses premiers caractères suffisent à retrouver l'élément voulu.
    private static string Raccourcir(string mot)
    {
        if (mot.Length <= LongueurMaxMot)
        {
            return mot;
        }

        // On ne coupe pas au milieu d'une paire de substituts (un emoji).
        var fin = char.IsHighSurrogate(mot[LongueurMaxMot - 1]) ? LongueurMaxMot - 1 : LongueurMaxMot;
        return mot[..fin];
    }

    // L'index trigram compte des caractères Unicode entiers : un emoji (deux unités UTF-16) n'en vaut qu'un.
    private static int NombreDeCaracteres(string mot)
    {
        var total = 0;
        foreach (var _ in mot.EnumerateRunes())
        {
            total++;
        }

        return total;
    }

    // Une chaîne FTS5 entre guillemets n'a pas d'autre syntaxe : on double seulement le guillemet.
    private static string EntreGuillemets(string mot) => "\"" + mot.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    // L'antislash sert d'échappement (clause ESCAPE) : lui-même, % et _ deviennent des caractères ordinaires.
    private static string EchapperLike(string mot)
    {
        var resultat = new StringBuilder(mot.Length + 2);
        foreach (var c in mot)
        {
            if (c is '\\' or '%' or '_')
            {
                resultat.Append('\\');
            }

            resultat.Append(c);
        }

        return resultat.ToString();
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
  total : 350
  échec : 0
  réussie : 350
  ignoré : 0
```

Les 63 nouveaux tests : 62 de `MoteurRechercheTests`, 18 tests unitaires (dont celui du gros historique, 3000 lignes, sous 2 s),
une `[Theory]` de 4 requêtes vides et la saisie hostile en `[Theory]` avec 40 saisies (guillemets, `*`, `:`, AND, OR, NOT, NEAR, parenthèses, `%`, `_`, antislash,
injection SQL, emoji, substituts isolés, caractère nul, chaîne très longue), et le test de charge de `RechercheConcurrenteTests`.

Contrôles du verrou (faits sur ce code) : avec un verrou privé dans `DepotHistorique` à la place de la connexion, le test
de charge a échoué 5 fois sur 6 ; sans aucun des deux changements (verrou privé du dépôt, commande créée hors du verrou du
moteur), 3 fois sur 3. Ce test dépend de l'ordonnancement des fils : il ne prouve pas l'absence de course, il la rend très
probable à détecter.

Contrôles de l'ordre (faits sur ce code) : en remplaçant `date_usage DESC, id DESC` par `date_usage DESC, id ASC`, le test
`L_ordre_est_DateUsage_decroissante_puis_Id_decroissant` échoue ; en passant `date_usage` en `ASC`, ce même test et
`La_limite_borne_le_nombre_de_resultats_en_gardant_les_plus_recents` échouent. Le test des emojis échoue si la longueur d'un
mot est comptée en unités UTF-16 (« 😀😀 » partirait en MATCH et ne trouverait rien).

- [ ] **Étape 5 : Vérifier sur le poste**

Sans objet : le moteur ne touche ni Windows ni l'interface, les tests utilisent une vraie base SQLite dans un dossier temporaire.

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Recherche/MoteurRecherche.cs tests/PressePapiers.Tests/Recherche/MoteurRechercheTests.cs tests/PressePapiers.Tests/Recherche/RechercheConcurrenteTests.cs
git commit -m "Ajouter le moteur de recherche plein texte"
```
