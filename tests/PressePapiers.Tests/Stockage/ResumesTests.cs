using System.Diagnostics;
using System.Globalization;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Recherche;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class ResumesTests : IDisposable
{
    private readonly ITestOutputHelper _sortie;
    private readonly DepotDeTest _depotDeTest = new();
    private readonly MoteurRecherche _moteur;

    public ResumesTests(ITestOutputHelper sortie)
    {
        _sortie = sortie;
        _moteur = new MoteurRecherche(_depotDeTest.Base);
    }

    public void Dispose() => _depotDeTest.Dispose();

    private PressePapiers.Coeur.Stockage.DepotHistorique Depot => _depotDeTest.Depot;

    private long Temporaire => Depot.OngletTemporaire.Id;

    [Fact]
    public void ListerResumes_donne_les_memes_identifiants_dans_le_meme_ordre_que_ListerElements()
    {
        var premier = Depot.AjouterCopie(ContenusDeTest.Texte("alpha"));
        _depotDeTest.Horloge.Advance(TimeSpan.FromMinutes(1));
        Depot.AjouterCopie(ContenusDeTest.MisEnForme("beta"));
        _depotDeTest.Horloge.Advance(TimeSpan.FromMinutes(1));
        Depot.AjouterCopie(ContenusDeTest.Fichiers("C:\\a.txt"));
        _depotDeTest.Horloge.Advance(TimeSpan.FromMinutes(1));
        Depot.MarquerUtilise(premier.Id);

        var complets = Depot.ListerElements(Temporaire).Select(e => e.Id).ToList();
        var resumes = Depot.ListerResumes(Temporaire).Select(r => r.Id).ToList();

        Assert.Equal(3, resumes.Count);
        Assert.Equal(complets, resumes);
        Assert.Equal(premier.Id, resumes[0]);
    }

    [Fact]
    public void ListerResumes_reprend_les_champs_communs_de_l_element()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Texte("bonjour", "Code"));

        var resume = Assert.Single(Depot.ListerResumes(Temporaire));

        Assert.Equal(element.Id, resume.Id);
        Assert.Equal(element.OngletId, resume.OngletId);
        Assert.Equal(element.Nom, resume.Nom);
        Assert.Equal(element.Type, resume.Type);
        Assert.Equal("bonjour", resume.Extrait);
        Assert.Equal("Code", resume.Application);
        Assert.Equal(element.DateCopie, resume.DateCopie);
        Assert.Equal(element.DateUsage, resume.DateUsage);
        Assert.Equal(element.Taille, resume.Taille);
    }

    [Fact]
    public void L_extrait_est_coupe_a_la_longueur_maximale()
    {
        Depot.AjouterCopie(ContenusDeTest.Texte(new string('x', 5000)));

        var resume = Assert.Single(Depot.ListerResumes(Temporaire));

        Assert.Equal(500, ElementResume.LongueurMaxExtrait);
        Assert.Equal(new string('x', 500), resume.Extrait);
    }

    [Fact]
    public void L_extrait_ne_coupe_jamais_une_paire_de_substituts()
    {
        // Un emoji compte pour un caractère et occupe deux unités UTF-16 : couper au 500e char en couperait un en deux.
        var texte = new string('a', 499) + char.ConvertFromUtf32(0x1F600) + "fin";
        Depot.AjouterCopie(ContenusDeTest.Texte(texte));

        var extrait = Assert.Single(Depot.ListerResumes(Temporaire)).Extrait!;

        Assert.Equal(new string('a', 499) + char.ConvertFromUtf32(0x1F600), extrait);
    }

    [Fact]
    public void Les_colonnes_du_resume_ne_comprennent_aucun_gros_contenu()
    {
        var colonnes = LecteurElement.ColonnesResume;

        Assert.Contains("extrait", colonnes);
        Assert.DoesNotContain("substr", colonnes);
        Assert.DoesNotContain("texte", colonnes);
        Assert.DoesNotContain("rtf", colonnes);
        Assert.DoesNotContain("html", colonnes);
    }

    [Fact]
    public void Un_texte_court_est_garde_en_entier_et_un_texte_absent_donne_un_extrait_nul()
    {
        Depot.AjouterCopie(ContenusDeTest.Texte(new string('y', ElementResume.LongueurMaxExtrait)));
        Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(1)));

        var resumes = Depot.ListerResumes(Temporaire);

        Assert.Contains(resumes, r => r.Type == TypeElement.Texte && r.Extrait!.Length == ElementResume.LongueurMaxExtrait);
        Assert.Contains(resumes, r => r.Type == TypeElement.Image && r.Extrait is null);
    }

    [Fact]
    public void La_miniature_n_est_lue_que_pour_une_image()
    {
        Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(1)));
        var texte = Depot.AjouterCopie(ContenusDeTest.Texte("texte"));
        // Une miniature posée par erreur sur un texte ne doit pas être relue.
        using (var commande = _depotDeTest.Connexion.CreateCommand())
        {
            commande.CommandText = "UPDATE element SET miniature = x'010203' WHERE id = $id";
            commande.Parameters.AddWithValue("$id", texte.Id);
            commande.ExecuteNonQuery();
        }

        var resumes = Depot.ListerResumes(Temporaire);

        var image = Assert.Single(resumes, r => r.Type == TypeElement.Image);
        Assert.Equal([9, 8, 7], image.Miniature);
        Assert.Null(Assert.Single(resumes, r => r.Type == TypeElement.Texte).Miniature);
    }

    [Fact]
    public void AMiseEnForme_vaut_vrai_des_qu_un_rtf_ou_un_html_est_present()
    {
        Depot.AjouterCopie(ContenusDeTest.Texte("simple"));
        Depot.AjouterCopie(ContenusDeTest.MisEnForme("riche"));
        Depot.AjouterCopie(new ContenuCopie { Type = TypeElement.MisEnForme, Texte = "html seul", Html = [1, 2] });
        Depot.AjouterCopie(new ContenuCopie { Type = TypeElement.MisEnForme, Texte = "rtf seul", Rtf = [1, 2] });

        var resumes = Depot.ListerResumes(Temporaire);

        Assert.False(Assert.Single(resumes, r => r.Extrait == "simple").AMiseEnForme);
        Assert.True(Assert.Single(resumes, r => r.Extrait == "riche").AMiseEnForme);
        Assert.True(Assert.Single(resumes, r => r.Extrait == "html seul").AMiseEnForme);
        Assert.True(Assert.Single(resumes, r => r.Extrait == "rtf seul").AMiseEnForme);
    }

    [Fact]
    public void Une_liste_de_fichiers_donne_ses_chemins_et_leur_nombre()
    {
        Depot.AjouterCopie(ContenusDeTest.Fichiers("C:\\a.txt", "C:\\dossier\\b.txt", "D:\\c.txt"));
        Depot.AjouterCopie(ContenusDeTest.Fichiers("C:\\seul.txt"));
        Depot.AjouterCopie(ContenusDeTest.Texte("a\nb\nc"));

        var resumes = Depot.ListerResumes(Temporaire);

        var trois = Assert.Single(resumes, r => r.NombreFichiers == 3);
        Assert.Equal("C:\\a.txt\nC:\\dossier\\b.txt\nD:\\c.txt", trois.Extrait);
        Assert.Single(resumes, r => r.NombreFichiers == 1);
        Assert.Equal(0, Assert.Single(resumes, r => r.Type == TypeElement.Texte).NombreFichiers);
    }

    [Fact]
    public void Le_nombre_de_fichiers_correspond_a_celui_de_l_element_complet()
    {
        Depot.AjouterCopie(ContenusDeTest.Fichiers("C:\\a", "C:\\b", "C:\\c", "C:\\d"));
        Depot.AjouterCopie(ContenusDeTest.Fichiers("C:\\z"));

        var complets = Depot.ListerElements(Temporaire).Select(e => e.Fichiers.Count);
        var resumes = Depot.ListerResumes(Temporaire).Select(r => r.NombreFichiers);

        Assert.Equal(complets, resumes);
    }

    [Fact]
    public void ListerResumes_ne_liste_que_l_onglet_demande()
    {
        var autre = Depot.CreerOnglet("Projets");
        Depot.AjouterCopie(ContenusDeTest.Texte("ici"));
        var ailleurs = Depot.AjouterCopie(ContenusDeTest.Texte("la-bas"));
        Depot.Nommer(ailleurs.Id, "Nom", autre.Id);

        Assert.Equal("ici", Assert.Single(Depot.ListerResumes(Temporaire)).Extrait);
        Assert.Equal("la-bas", Assert.Single(Depot.ListerResumes(autre.Id)).Extrait);
        Assert.Empty(Depot.ListerResumes(long.MaxValue));
    }

    [Fact]
    public void ChercherResumes_donne_les_memes_identifiants_dans_le_meme_ordre_que_Chercher()
    {
        Depot.AjouterCopie(ContenusDeTest.Texte("le chat dort"));
        _depotDeTest.Horloge.Advance(TimeSpan.FromMinutes(1));
        Depot.AjouterCopie(ContenusDeTest.MisEnForme("un chat riche"));
        _depotDeTest.Horloge.Advance(TimeSpan.FromMinutes(1));
        Depot.AjouterCopie(ContenusDeTest.Fichiers("C:\\chat.txt"));
        Depot.AjouterCopie(ContenusDeTest.Texte("le chien aboie"));

        foreach (var requete in new[] { "chat", "CHAT dort", "ch", "c", "inconnu", "", "   " })
        {
            var complets = _moteur.Chercher(requete).Select(e => e.Id).ToList();
            var resumes = _moteur.ChercherResumes(requete).Select(r => r.Id).ToList();
            Assert.Equal(complets, resumes);
        }

        Assert.Equal(3, _moteur.ChercherResumes("chat").Count);
    }

    [Fact]
    public void ChercherResumes_respecte_la_limite_et_porte_les_champs_du_resume()
    {
        for (var i = 0; i < 5; i++)
        {
            Depot.AjouterCopie(ContenusDeTest.Texte("mot commun numero " + i.ToString(CultureInfo.InvariantCulture) + new string('z', 3000)));
            _depotDeTest.Horloge.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.Empty(_moteur.ChercherResumes("commun", 0));
        var deux = _moteur.ChercherResumes("commun", 2);

        Assert.Equal(2, deux.Count);
        Assert.All(deux, r => Assert.Equal(ElementResume.LongueurMaxExtrait, r.Extrait!.Length));
        Assert.Equal(Depot.ListerElements(Temporaire).Take(2).Select(e => e.Id), deux.Select(r => r.Id));
    }

    [Theory]
    [InlineData("\"")]
    [InlineData("\"\"\"")]
    [InlineData("a\"b OR NOT c*")]
    [InlineData("NEAR(a b)")]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    [InlineData("a\0b")]
    [InlineData("'; DROP TABLE element; --")]
    [InlineData("col:valeur")]
    [InlineData("^debut *")]
    public void ChercherResumes_accepte_les_saisies_hostiles_sans_exception(string saisie)
    {
        Depot.AjouterCopie(ContenusDeTest.Texte("contenu banal 100% sur_ \\ \" a"));

        var resumes = _moteur.ChercherResumes(saisie);
        var complets = _moteur.Chercher(saisie);

        Assert.Equal(complets.Select(e => e.Id), resumes.Select(r => r.Id));
        Assert.Single(Depot.ListerResumes(Temporaire));
    }

    [Fact]
    public void ChercherResumes_traite_une_saisie_enorme_comme_Chercher()
    {
        Depot.AjouterCopie(ContenusDeTest.Texte("contenu banal"));
        var saisie = string.Join(' ', Enumerable.Range(0, 100).Select(i => "mot" + i.ToString(CultureInfo.InvariantCulture))) + new string('q', 100_000);

        Assert.Equal(_moteur.Chercher(saisie).Count, _moteur.ChercherResumes(saisie).Count);
    }

    // Mesure sans assertion, longue (une trentaine de secondes) : lancée à la demande seulement, pour que la suite reste
    // rapide à chaque tâche. Pour la jouer : dotnet test --solution PressePapiers.slnx -- --explicit on
    [Fact(Explicit = true)]
    public void Mesure_resumes_contre_elements_complets_sur_5000_elements_de_20_Ko()
    {
        var onglet = Temporaire;
        InsererEnMasse(onglet, 5000, 20 * 1024);

        // Un premier passage chauffe le cache de pages et le JIT ; la mesure garde la médiane de cinq passages.
        _ = Depot.ListerElements(onglet);
        _ = Depot.ListerResumes(onglet);
        _ = _moteur.Chercher("zebre");
        _ = _moteur.ChercherResumes("zebre");

        var listeComplete = Mediane(() => Depot.ListerElements(onglet).Count);
        var listeResume = Mediane(() => Depot.ListerResumes(onglet).Count);
        var chercheComplete = Mediane(() => _moteur.Chercher("zebre", 5000).Count);
        var chercheResume = Mediane(() => _moteur.ChercherResumes("zebre", 5000).Count);

        _sortie.WriteLine($"MESURE ListerElements {listeComplete:0} ms | ListerResumes {listeResume:0} ms");
        _sortie.WriteLine($"MESURE Chercher {chercheComplete:0} ms | ChercherResumes {chercheResume:0} ms");

        Assert.Equal(Depot.ListerElements(onglet).Select(e => e.Id), Depot.ListerResumes(onglet).Select(r => r.Id));
        Assert.Equal(_moteur.Chercher("zebre", 5000).Select(e => e.Id), _moteur.ChercherResumes("zebre", 5000).Select(r => r.Id));
    }

    private static double Mediane(Func<int> action)
    {
        var durees = new List<double>();
        for (var i = 0; i < 5; i++)
        {
            var chrono = Stopwatch.StartNew();
            _ = action();
            durees.Add(chrono.Elapsed.TotalMilliseconds);
        }

        durees.Sort();
        return durees[durees.Count / 2];
    }

    // Insertion SQL directe dans une seule transaction : le dépôt n'a pas besoin d'être sollicité 5 000 fois.
    private void InsererEnMasse(long onglet, int nombre, int taille)
    {
        var date = new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);
        var bloc = string.Concat(Enumerable.Repeat("zebre lorem ipsum dolor sit amet ", taille / 33 + 1))[..taille];
        using var transaction = _depotDeTest.Connexion.BeginTransaction();
        using var commande = _depotDeTest.Connexion.CreateCommand();
        commande.Transaction = transaction;
        commande.CommandText = """
            INSERT INTO element (onglet_id, type, extrait, texte, texte_recherche, empreinte, date_copie, date_usage, taille)
            VALUES ($onglet, 1, substr($texte, 1, 500), $texte, $recherche, $empreinte, $date, $date, $taille)
            """;
        commande.Parameters.AddWithValue("$onglet", onglet);
        var pTexte = commande.Parameters.AddWithValue("$texte", bloc);
        var pRecherche = commande.Parameters.AddWithValue("$recherche", bloc);
        var pEmpreinte = commande.Parameters.AddWithValue("$empreinte", new byte[] { 0 });
        var pDate = commande.Parameters.AddWithValue("$date", "");
        commande.Parameters.AddWithValue("$taille", taille);
        for (var i = 0; i < nombre; i++)
        {
            var numero = i.ToString(CultureInfo.InvariantCulture);
            pTexte.Value = numero + " " + bloc;
            pRecherche.Value = numero + " " + bloc;
            pEmpreinte.Value = System.Text.Encoding.UTF8.GetBytes(numero);
            pDate.Value = date.AddSeconds(i).ToString("O", CultureInfo.InvariantCulture);
            commande.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}
