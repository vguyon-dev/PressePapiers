using System.Globalization;
using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Session;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Session;

public sealed class DemarrageSessionTests : IDisposable
{
    // Une valeur réaliste de FILETIME : 13/10/2026 environ, en intervalles de 100 ns depuis 1601.
    private const long OuvertureA = 134_050_000_000_000_000;
    private const long OuvertureB = 134_050_500_000_000_000;

    private readonly JournalCollecte _journal = new();
    private readonly DepotDeTest _banc;
    private readonly SourceFictive _source = new();

    public DemarrageSessionTests() => _banc = new DepotDeTest(_journal);

    private DepotHistorique Depot => _banc.Depot;

    public void Dispose() => _banc.Dispose();

    // ---- Les quatre situations de la spec ----

    [Fact]
    public void Au_premier_lancement_rien_n_est_supprime_et_le_repere_est_enregistre()
    {
        Depot.AjouterCopie(ContenusDeTest.Texte("copie avant le premier lancement"));
        _source.Valeur = OuvertureA;

        var resultat = Appliquer();

        Assert.Equal(new ResultatSession(EtatSession.PremierLancement, 0), resultat);
        Assert.Equal(1, Depot.CompterElements(Depot.OngletTemporaire.Id));
        Assert.Equal(OuvertureA.ToString(CultureInfo.InvariantCulture), Depot.LireMeta(DemarrageSession.CleRepere));
    }

    [Fact]
    public void Dans_la_meme_session_rien_n_est_supprime()
    {
        _source.Valeur = OuvertureA;
        Appliquer();
        Depot.AjouterCopie(ContenusDeTest.Texte("copie de la session"));

        var resultat = Appliquer();

        Assert.Equal(new ResultatSession(EtatSession.MemeSession, 0), resultat);
        Assert.Equal(1, Depot.CompterElements(Depot.OngletTemporaire.Id));
        Assert.Equal(OuvertureA.ToString(CultureInfo.InvariantCulture), Depot.LireMeta(DemarrageSession.CleRepere));
    }

    [Fact]
    public void Dans_une_nouvelle_session_Temporaire_est_purge_avec_ses_images_puis_le_repere_est_mis_a_jour()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var nomme = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(1)));
        Depot.Nommer(nomme.Id, "Logo", onglet.Id);
        _source.Valeur = OuvertureA;
        Appliquer();
        Depot.AjouterCopie(ContenusDeTest.Texte("copie de l'ancienne session"));
        Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(2)));
        _source.Valeur = OuvertureB;

        var resultat = Appliquer();

        Assert.Equal(new ResultatSession(EtatSession.NouvelleSession, 2), resultat);
        Assert.Equal(0, Depot.CompterElements(Depot.OngletTemporaire.Id));
        // L'élément nommé et son image survivent à la purge.
        Assert.Equal(1, Depot.CompterElements(onglet.Id));
        Assert.Equal([nomme.ImageFichier], _banc.NomsImages());
        Assert.Equal(OuvertureB.ToString(CultureInfo.InvariantCulture), Depot.LireMeta(DemarrageSession.CleRepere));
    }

    [Fact]
    public void Une_nouvelle_session_sans_rien_a_purger_met_quand_meme_le_repere_a_jour()
    {
        _source.Valeur = OuvertureA;
        Appliquer();
        _source.Valeur = OuvertureB;

        var resultat = Appliquer();

        Assert.Equal(new ResultatSession(EtatSession.NouvelleSession, 0), resultat);
        Assert.Equal(OuvertureB.ToString(CultureInfo.InvariantCulture), Depot.LireMeta(DemarrageSession.CleRepere));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public void Une_lecture_nulle_ou_a_zero_ne_purge_rien_note_un_avertissement_et_ne_touche_pas_au_repere(long? valeur)
    {
        // Le repère existe déjà : une lecture impossible ne doit ni purger ni l'effacer.
        _source.Valeur = OuvertureA;
        Appliquer();
        Depot.AjouterCopie(ContenusDeTest.Texte("a conserver"));
        _source.Valeur = valeur;

        var resultat = Appliquer();

        Assert.Equal(new ResultatSession(EtatSession.Indetermine, 0), resultat);
        Assert.Equal(1, Depot.CompterElements(Depot.OngletTemporaire.Id));
        Assert.Equal(OuvertureA.ToString(CultureInfo.InvariantCulture), Depot.LireMeta(DemarrageSession.CleRepere));
        Assert.Single(_journal.Avertissements);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public void Une_lecture_nulle_au_premier_lancement_n_ecrit_aucun_repere(long? valeur)
    {
        _source.Valeur = valeur;

        var resultat = Appliquer();

        Assert.Equal(EtatSession.Indetermine, resultat.Etat);
        Assert.Null(Depot.LireMeta(DemarrageSession.CleRepere));
    }

    [Fact]
    public void Une_source_qui_leve_une_exception_donne_Indetermine_avec_un_seul_avertissement()
    {
        _source.Echec = new InvalidOperationException("service Bureau à distance arrêté");
        Depot.AjouterCopie(ContenusDeTest.Texte("a conserver"));

        var resultat = Appliquer();

        Assert.Equal(new ResultatSession(EtatSession.Indetermine, 0), resultat);
        Assert.Equal(1, Depot.CompterElements(Depot.OngletTemporaire.Id));
        Assert.Single(_journal.Avertissements);
    }

    // ---- Ordre des opérations ----

    [Fact]
    public void Si_la_purge_echoue_le_repere_n_est_pas_mis_a_jour_et_la_purge_est_retentee_au_lancement_suivant()
    {
        _source.Valeur = OuvertureA;
        Appliquer();
        Depot.AjouterCopie(ContenusDeTest.Texte("ancienne session"));
        _source.Valeur = OuvertureB;
        Executer("CREATE TRIGGER refus BEFORE DELETE ON element BEGIN SELECT RAISE(ABORT, 'suppression refusée'); END");

        Assert.Throws<SqliteException>(Appliquer);

        // Mis à jour avant la purge, le repère aurait fait croire à une session déjà traitée : l'élément resterait pour toujours.
        Assert.Equal(OuvertureA.ToString(CultureInfo.InvariantCulture), Depot.LireMeta(DemarrageSession.CleRepere));
        Assert.Equal(1, Depot.CompterElements(Depot.OngletTemporaire.Id));

        Executer("DROP TRIGGER refus");
        var resultat = Appliquer();

        Assert.Equal(new ResultatSession(EtatSession.NouvelleSession, 1), resultat);
        Assert.Equal(0, Depot.CompterElements(Depot.OngletTemporaire.Id));
    }

    [Fact]
    public void Une_suite_de_lancements_se_comporte_comme_les_sessions_Windows_qui_les_portent()
    {
        var etats = new List<EtatSession>();
        foreach (var heure in new long?[] { OuvertureA, OuvertureA, null, OuvertureA, OuvertureB, OuvertureB })
        {
            _source.Valeur = heure;
            etats.Add(Appliquer().Etat);
        }

        Assert.Equal(
            [
                EtatSession.PremierLancement,
                EtatSession.MemeSession,
                EtatSession.Indetermine,
                EtatSession.MemeSession,
                EtatSession.NouvelleSession,
                EtatSession.MemeSession,
            ],
            etats);
    }

    [Fact]
    public void Un_repere_illisible_en_base_est_traite_comme_une_autre_session()
    {
        Depot.EcrireMeta(DemarrageSession.CleRepere, "pas un nombre");
        Depot.AjouterCopie(ContenusDeTest.Texte("copie"));
        _source.Valeur = OuvertureA;

        var resultat = Appliquer();

        Assert.Equal(new ResultatSession(EtatSession.NouvelleSession, 1), resultat);
        Assert.Equal(OuvertureA.ToString(CultureInfo.InvariantCulture), Depot.LireMeta(DemarrageSession.CleRepere));
    }

    [Fact]
    public void Le_repere_est_garde_dans_la_cle_prevue_par_le_contrat()
    {
        Assert.Equal("session.heure_ouverture", DemarrageSession.CleRepere);
    }

    // ---- Assistants ----

    private ResultatSession Appliquer() => DemarrageSession.Appliquer(_source, Depot, _journal);

    private void Executer(string sql)
    {
        using var commande = _banc.Connexion.CreateCommand();
        commande.CommandText = sql;
        commande.ExecuteNonQuery();
    }

    private sealed class SourceFictive : ISourceSession
    {
        public long? Valeur { get; set; }

        public Exception? Echec { get; set; }

        public long? LireHeureOuverture() => Echec is null ? Valeur : throw Echec;
    }
}
