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
