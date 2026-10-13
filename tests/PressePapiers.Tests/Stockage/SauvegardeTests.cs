using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class SauvegardeTests : IDisposable
{
    private static readonly DateTimeOffset Debut = new(2026, 10, 13, 8, 15, 30, TimeSpan.Zero);

    private readonly DossierTemporaire _racine = new();
    private readonly FakeTimeProvider _horloge = new(Debut);

    private string CheminBase => _racine.Fichier("historique.db");

    private string Sauvegardes => _racine.Fichier("sauvegardes");

    public void Dispose() => _racine.Dispose();

    // ---- Creer ----

    [Fact]
    public void Sans_base_il_n_y_a_rien_a_sauvegarder()
    {
        Assert.Null(Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge));

        Assert.False(Directory.Exists(Sauvegardes));
    }

    [Fact]
    public void Un_fichier_vide_n_est_pas_une_base_a_sauvegarder()
    {
        File.WriteAllBytes(CheminBase, []);

        Assert.Null(Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge));
    }

    [Fact]
    public void La_copie_porte_la_date_et_l_heure_UTC_dans_son_nom_et_contient_les_donnees()
    {
        using (var origine = CreerBase(3))
        {
            var chemin = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);

            Assert.Equal(Path.Combine(Sauvegardes, "historique-20261013-081530.db"), chemin);
            Assert.True(File.Exists(chemin));
            Assert.Equal(3, CompterElementsDans(chemin));
        }
    }

    [Fact]
    public void Le_nom_suit_le_fuseau_UTC_et_non_le_fuseau_local()
    {
        CreerBase(1).Dispose();
        // 23:59:59 UTC le 31/12 : en heure locale d'un fuseau en avance, ce serait déjà l'année suivante.
        var horloge = new FakeTimeProvider(new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero));
        horloge.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Avance", TimeSpan.FromHours(5), "Avance", "Avance"));

        var chemin = Sauvegarde.Creer(CheminBase, Sauvegardes, horloge);

        Assert.Equal("historique-20261231-235959.db", Path.GetFileName(chemin));
    }

    [Fact]
    public void La_copie_contient_aussi_ce_qui_est_encore_dans_le_fichier_wal()
    {
        using var origine = CreerBase(5);
        // La base est ouverte : ses dernières écritures sont dans historique.db-wal, pas encore dans historique.db.
        Assert.True(File.Exists(CheminBase + "-wal"));

        var chemin = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);

        Assert.Equal(5, CompterElementsDans(chemin!));
    }

    [Fact]
    public void La_copie_marche_sur_une_base_laissee_par_un_plantage()
    {
        using var origine = CreerBase(5);
        // Un instantané des trois fichiers pris base ouverte vaut un plantage : historique.db, -wal et -shm d'un processus mort.
        var plante = _racine.Fichier("plante");
        Directory.CreateDirectory(plante);
        foreach (var suffixe in new[] { "", "-wal", "-shm" })
        {
            using var lecture = new FileStream(CheminBase + suffixe, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var ecriture = File.Create(Path.Combine(plante, "historique.db" + suffixe));
            lecture.CopyTo(ecriture);
        }

        var chemin = Sauvegarde.Creer(Path.Combine(plante, "historique.db"), Sauvegardes, _horloge);

        Assert.Equal(5, CompterElementsDans(chemin!));
    }

    [Fact]
    public void La_copie_est_un_fichier_seul_sans_wal_ni_shm()
    {
        using var origine = CreerBase(2);

        Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);

        Assert.Equal(["historique-20261013-081530.db"], Directory.GetFiles(Sauvegardes).Select(f => Path.GetFileName(f)));
    }

    [Fact]
    public void La_sauvegarde_ne_modifie_pas_la_base_d_origine()
    {
        using var origine = CreerBase(4);
        var avant = LireOctetsDeLaBaseOuverte();

        Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);

        Assert.Equal(avant, LireOctetsDeLaBaseOuverte());
        Assert.Equal("4", InsertionsSql.Scalaire(origine.Connexion, "SELECT COUNT(*) FROM element"));
    }

    // ---- Rotation ----

    [Fact]
    public void Seules_les_trois_dernieres_copies_sont_gardees()
    {
        CreerBase(1).Dispose();
        for (var i = 0; i < 5; i++)
        {
            Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);
            _horloge.Advance(TimeSpan.FromSeconds(10));
        }

        // Cinq copies à 08:15:30, :40, :50, 08:16:00, :10 ; les plus récentes restent.
        Assert.Equal(
            ["historique-20261013-081550.db", "historique-20261013-081600.db", "historique-20261013-081610.db"],
            NomsSauvegardes());
    }

    [Theory]
    [InlineData(1, "historique-20261013-081600.db")]
    [InlineData(2, "historique-20261013-081550.db,historique-20261013-081600.db")]
    public void Le_nombre_de_copies_gardees_est_reglable(int nombreGarde, string attendu)
    {
        CreerBase(1).Dispose();
        for (var i = 0; i < 4; i++)
        {
            Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge, nombreGarde);
            _horloge.Advance(TimeSpan.FromSeconds(10));
        }

        Assert.Equal(attendu.Split(','), NomsSauvegardes());
    }

    [Fact]
    public void La_rotation_ne_touche_pas_aux_fichiers_qui_ne_sont_pas_des_copies_horodatees()
    {
        CreerBase(1).Dispose();
        Directory.CreateDirectory(Sauvegardes);
        foreach (var nom in new[] { "notes.txt", "historique-manuel.db", "historique-2026.db", "historique-20261013-081530.db.bak" })
        {
            File.WriteAllText(Path.Combine(Sauvegardes, nom), "a garder");
        }

        for (var i = 0; i < 5; i++)
        {
            Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);
            _horloge.Advance(TimeSpan.FromSeconds(10));
        }

        var presents = Directory.GetFiles(Sauvegardes).Select(f => Path.GetFileName(f)).Order().ToArray();
        Assert.Contains("notes.txt", presents);
        Assert.Contains("historique-manuel.db", presents);
        Assert.Contains("historique-2026.db", presents);
        Assert.Contains("historique-20261013-081530.db.bak", presents);
        Assert.Equal(3, NomsSauvegardes().Length);
    }

    [Fact]
    public void La_copie_qui_vient_d_etre_creee_n_est_jamais_supprimee_meme_si_l_horloge_a_recule()
    {
        CreerBase(1).Dispose();
        foreach (var secondes in new[] { 100, 110, 120 })
        {
            Sauvegarde.Creer(CheminBase, Sauvegardes, new FakeTimeProvider(Debut.AddSeconds(secondes)));
        }

        // L'horloge revient dix minutes en arrière : la nouvelle copie est la plus ancienne par son nom.
        var chemin = Sauvegarde.Creer(CheminBase, Sauvegardes, new FakeTimeProvider(Debut.AddMinutes(-10)));

        Assert.True(File.Exists(chemin));
        Assert.Equal(3, NomsSauvegardes().Length);
        Assert.Contains(Path.GetFileName(chemin), NomsSauvegardes());
    }

    [Fact]
    public void Deux_copies_dans_la_meme_seconde_donnent_un_seul_fichier()
    {
        CreerBase(1).Dispose();

        var premiere = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);
        var seconde = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);

        Assert.Equal(premiere, seconde);
        Assert.Single(NomsSauvegardes());
        Assert.Equal(1, CompterElementsDans(seconde!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Garder_moins_d_une_copie_est_refuse_sans_rien_creer(int nombreGarde)
    {
        CreerBase(1).Dispose();

        Assert.Throws<ArgumentOutOfRangeException>(() => Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge, nombreGarde));

        Assert.False(Directory.Exists(Sauvegardes));
    }

    [Fact]
    public void Une_base_illisible_leve_SqliteException_sans_laisser_de_fichier_partiel_ni_perdre_les_copies_saines()
    {
        CreerBase(2).Dispose();
        for (var i = 0; i < 3; i++)
        {
            Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);
            _horloge.Advance(TimeSpan.FromSeconds(10));
        }

        var saines = NomsSauvegardes();
        File.WriteAllText(CheminBase, "ceci n'est pas une base SQLite, juste du texte qui traine dans le fichier d'historique");

        Assert.Throws<SqliteException>(() => Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge));

        // Aucune copie de la base abîmée, aucun reste, et la rotation n'a pas mangé les bonnes copies.
        Assert.Equal(saines, NomsSauvegardes());
        Assert.Equal(3, Directory.GetFiles(Sauvegardes).Length);
    }

    [Fact]
    public void Une_copie_qui_echoue_au_controle_d_integrite_est_jetee_et_les_anciennes_sont_gardees()
    {
        var journal = new JournalCollecte();
        CreerBase(40, texteLong: true).Dispose();
        for (var i = 0; i < 3; i++)
        {
            Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);
            _horloge.Advance(TimeSpan.FromSeconds(10));
        }

        var saines = NomsSauvegardes();
        AbimerUnePageDeDonnees();

        var resultat = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge, journal: journal);

        // Rien de nouveau, rien de supprimé par la rotation (sans le contrôle, la plus ancienne des trois serait partie).
        Assert.Null(resultat);
        Assert.Equal(saines, NomsSauvegardes());
        Assert.Equal(3, Directory.GetFiles(Sauvegardes).Length);
        var avertissement = Assert.Single(journal.Avertissements);
        Assert.Contains("contrôle d'intégrité", avertissement);
    }

    [Fact]
    public void Une_copie_saine_passe_le_controle_sans_avertissement()
    {
        var journal = new JournalCollecte();
        CreerBase(5).Dispose();

        var copie = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge, journal: journal);

        Assert.NotNull(copie);
        Assert.Empty(journal.Avertissements);
    }

    // ---- PlusRecente ----

    [Fact]
    public void PlusRecente_sans_dossier_ou_sans_copie_donne_null()
    {
        Assert.Null(Sauvegarde.PlusRecente(Sauvegardes));

        Directory.CreateDirectory(Sauvegardes);
        File.WriteAllText(Path.Combine(Sauvegardes, "notes.txt"), "pas une copie");
        Assert.Null(Sauvegarde.PlusRecente(Sauvegardes));
    }

    [Fact]
    public void PlusRecente_donne_la_copie_au_nom_le_plus_recent_quel_que_soit_l_ordre_de_creation()
    {
        Directory.CreateDirectory(Sauvegardes);
        foreach (var nom in new[] { "historique-20261013-081600.db", "historique-20261112-000000.db", "historique-20260101-235959.db" })
        {
            File.WriteAllText(Path.Combine(Sauvegardes, nom), "x");
        }

        File.WriteAllText(Path.Combine(Sauvegardes, "historique-99999999-999999.db.bak"), "pas une copie");

        Assert.Equal(Path.Combine(Sauvegardes, "historique-20261112-000000.db"), Sauvegarde.PlusRecente(Sauvegardes));
    }

    // ---- Restaurer ----

    [Fact]
    public void Restaurer_remplace_la_base_par_la_copie_et_supprime_wal_et_shm()
    {
        string copie;
        using (var origine = CreerBase(3))
        {
            copie = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge)!;
        }

        File.WriteAllText(CheminBase, "base abimee");
        File.WriteAllText(CheminBase + "-wal", "journal d'une autre base");
        File.WriteAllText(CheminBase + "-shm", "memoire partagee d'une autre base");

        Sauvegarde.Restaurer(copie, CheminBase);

        Assert.False(File.Exists(CheminBase + "-wal"));
        Assert.False(File.Exists(CheminBase + "-shm"));
        Assert.True(File.Exists(copie));
        Assert.False(File.Exists(CheminBase + ".restauration"));
        using var restauree = BaseHistorique.Ouvrir(CheminBase);
        Assert.Equal("3", InsertionsSql.Scalaire(restauree.Connexion, "SELECT COUNT(*) FROM element"));
    }

    [Fact]
    public void Restaurer_ne_laisse_aucun_fichier_renomme_a_cote_de_la_base()
    {
        string copie;
        using (var origine = CreerBase(1))
        {
            copie = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge)!;
        }

        File.WriteAllText(CheminBase + "-wal", "wal");
        File.WriteAllText(CheminBase + "-shm", "shm");

        Sauvegarde.Restaurer(copie, CheminBase);

        Assert.Equal(["historique.db"], Directory.GetFiles(_racine.Chemin).Select(f => Path.GetFileName(f)));
    }

    [Fact]
    public void Un_echec_de_mise_en_place_remet_le_wal_et_le_shm_de_la_base_actuelle()
    {
        string copie;
        using (var origine = CreerBase(1))
        {
            copie = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge)!;
        }

        File.WriteAllText(CheminBase, "base actuelle");
        File.WriteAllText(CheminBase + "-wal", "journal de la base actuelle");
        File.WriteAllText(CheminBase + "-shm", "memoire de la base actuelle");

        // Un fichier verrouillé empêche de le remplacer, après que le wal et le shm ont déjà été écartés.
        using (new FileStream(CheminBase, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var exception = Record.Exception(() => Sauvegarde.Restaurer(copie, CheminBase));
            Assert.True(exception is IOException or UnauthorizedAccessException, $"exception inattendue : {exception}");
        }

        Assert.Equal("base actuelle", File.ReadAllText(CheminBase));
        Assert.Equal("journal de la base actuelle", File.ReadAllText(CheminBase + "-wal"));
        Assert.Equal("memoire de la base actuelle", File.ReadAllText(CheminBase + "-shm"));
        Assert.Equal(["historique.db", "historique.db-shm", "historique.db-wal"],
                     Directory.GetFiles(_racine.Chemin).Select(f => Path.GetFileName(f)).Order());
    }

    [Fact]
    public void Un_wal_impossible_a_ecarter_arrete_la_restauration_avant_de_toucher_a_la_base()
    {
        string copie;
        using (var origine = CreerBase(1))
        {
            copie = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge)!;
        }

        File.WriteAllText(CheminBase, "base actuelle");
        File.WriteAllText(CheminBase + "-wal", "journal");
        File.WriteAllText(CheminBase + "-shm", "memoire");

        // Le wal est tenu ouvert (comme par une base encore ouverte) : le renommer échoue, la base reste telle quelle.
        using (new FileStream(CheminBase + "-wal", FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var exception = Record.Exception(() => Sauvegarde.Restaurer(copie, CheminBase));
            Assert.True(exception is IOException or UnauthorizedAccessException, $"exception inattendue : {exception}");
        }

        Assert.Equal("base actuelle", File.ReadAllText(CheminBase));
        Assert.Equal("journal", File.ReadAllText(CheminBase + "-wal"));
        Assert.Equal("memoire", File.ReadAllText(CheminBase + "-shm"));
        Assert.False(File.Exists(CheminBase + ".restauration"));
    }

    [Fact]
    public void Restaurer_cree_le_dossier_de_la_base_s_il_manque()
    {
        string copie;
        using (var origine = CreerBase(1))
        {
            copie = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge)!;
        }

        var neuve = Path.Combine(_racine.Fichier("autre"), "dossier", "historique.db");

        Sauvegarde.Restaurer(copie, neuve);

        using var restauree = BaseHistorique.Ouvrir(neuve);
        Assert.Equal("1", InsertionsSql.Scalaire(restauree.Connexion, "SELECT COUNT(*) FROM element"));
    }

    [Fact]
    public void Restaurer_une_copie_absente_leve_FileNotFoundException_et_laisse_la_base_intacte()
    {
        File.WriteAllText(CheminBase, "base actuelle");

        Assert.Throws<FileNotFoundException>(() => Sauvegarde.Restaurer(_racine.Fichier("absente.db"), CheminBase));

        Assert.Equal("base actuelle", File.ReadAllText(CheminBase));
    }

    [Fact]
    public void Restaurer_sur_une_base_encore_ouverte_echoue_sans_rien_casser()
    {
        string copie;
        using (var autre = CreerBase(1))
        {
            copie = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge)!;
        }

        using var ouverte = BaseHistorique.Ouvrir(CheminBase);
        new DepotHistorique(ouverte, new DossierDonnees(_racine.Chemin), _horloge, JournalNul.Instance)
            .AjouterCopie(ContenusDeTest.Texte("ecrit apres la copie"));

        var exception = Record.Exception(() => Sauvegarde.Restaurer(copie, CheminBase));

        Assert.True(exception is IOException or UnauthorizedAccessException, $"exception inattendue : {exception}");
        Assert.False(File.Exists(CheminBase + ".restauration"));
        Assert.Equal("2", InsertionsSql.Scalaire(ouverte.Connexion, "SELECT COUNT(*) FROM element"));
    }

    [Fact]
    public void Une_base_qui_n_est_pas_une_base_SQLite_se_repare_depuis_la_copie_la_plus_recente()
    {
        using (var origine = CreerBase(6))
        {
            Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);
        }

        File.WriteAllText(CheminBase, "du texte dans le fichier d'historique, pas une base SQLite");
        Assert.Throws<SqliteException>(() => BaseHistorique.Ouvrir(CheminBase).Dispose());

        var plusRecente = Sauvegarde.PlusRecente(Sauvegardes);
        Assert.NotNull(plusRecente);
        Sauvegarde.Restaurer(plusRecente, CheminBase);

        using var reparee = BaseHistorique.Ouvrir(CheminBase);
        Assert.Equal("6", InsertionsSql.Scalaire(reparee.Connexion, "SELECT COUNT(*) FROM element"));
    }

    // ---- Assistants ----

    // Base de l'historique au chemin habituel, avec n textes ; laissée ouverte pour que ses écritures restent dans le wal.
    // Des textes longs étalent la base sur plusieurs pages, pour qu'on puisse en abîmer une sans toucher au schéma.
    private BaseHistorique CreerBase(int elements, bool texteLong = false)
    {
        var dossiers = new DossierDonnees(_racine.Chemin);
        var baseHistorique = BaseHistorique.Ouvrir(CheminBase);
        var depot = new DepotHistorique(baseHistorique, dossiers, _horloge, JournalNul.Instance);
        for (var i = 0; i < elements; i++)
        {
            depot.AjouterCopie(ContenusDeTest.Texte("element " + i + (texteLong ? new string('x', 3000) : "")));
        }

        return baseHistorique;
    }

    // Écrase l'en-tête de plusieurs pages du milieu du fichier (base fermée) : SQLite l'ouvre encore, mais le contrôle
    // d'intégrité échoue. Le test vérifie lui-même que la base est bien abîmée avant d'en tirer une conclusion.
    private void AbimerUnePageDeDonnees()
    {
        const int taillePage = 4096;
        var octets = File.ReadAllBytes(CheminBase);
        var pages = octets.Length / taillePage;
        Assert.True(pages >= 12, $"base trop petite pour le test : {pages} pages");
        for (var page = pages / 2; page < pages / 2 + 4; page++)
        {
            Array.Fill(octets, (byte)0xFF, page * taillePage, 64);
        }

        File.WriteAllBytes(CheminBase, octets);

        var chaine = new SqliteConnectionStringBuilder { DataSource = CheminBase, Pooling = false }.ToString();
        using var connexion = new SqliteConnection(chaine);
        connexion.Open();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "PRAGMA quick_check";
        Assert.NotEqual("ok", Convert.ToString(commande.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture));
    }

    // Octets du fichier de la base alors qu'une connexion l'a ouvert : File.ReadAllBytes refuserait de le lire.
    private byte[] LireOctetsDeLaBaseOuverte()
    {
        using var flux = new FileStream(CheminBase, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var memoire = new MemoryStream();
        flux.CopyTo(memoire);
        return memoire.ToArray();
    }

    // Nombre d'éléments d'une copie, lu sur un double pour ne pas ajouter de wal ni de shm à côté de la copie.
    private int CompterElementsDans(string chemin)
    {
        var copieLecture = _racine.Fichier("lecture-" + Guid.NewGuid().ToString("N") + ".db");
        File.Copy(chemin, copieLecture);
        using var baseHistorique = BaseHistorique.Ouvrir(copieLecture);
        return int.Parse(InsertionsSql.Scalaire(baseHistorique.Connexion, "SELECT COUNT(*) FROM element")!, System.Globalization.CultureInfo.InvariantCulture);
    }

    private string[] NomsSauvegardes() =>
        Directory.Exists(Sauvegardes)
            ? [.. Directory.GetFiles(Sauvegardes, "historique-????????-??????.db").Select(f => Path.GetFileName(f)).Order()]
            : [];
}
