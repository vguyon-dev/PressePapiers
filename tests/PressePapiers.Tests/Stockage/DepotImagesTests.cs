using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

/// <summary>PNG du dépôt : écriture hors du verrou et sans fichier partiel, ménage des images orphelines.</summary>
public sealed class DepotImagesTests : IDisposable
{
    private readonly JournalCollecte _journal = new();
    private readonly DepotDeTest _banc;

    public DepotImagesTests() => _banc = new DepotDeTest(_journal);

    private DepotHistorique Depot => _banc.Depot;

    private string Images => _banc.Dossiers.Images;

    public void Dispose() => _banc.Dispose();

    private string Poser(string nom, string contenu = "x")
    {
        var chemin = Path.Combine(Images, nom);
        File.WriteAllText(chemin, contenu);
        return chemin;
    }

    // ---- Images orphelines ----

    [Fact]
    public void Le_menage_supprime_les_png_sans_ligne_et_garde_ceux_qui_sont_references()
    {
        var garde = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(1)));
        var orpheline = Poser("orpheline.png");
        var autre = Poser("autre.png");

        var supprimes = Depot.NettoyerImagesOrphelines();

        Assert.Equal(2, supprimes);
        Assert.False(File.Exists(orpheline));
        Assert.False(File.Exists(autre));
        Assert.Equal([garde.ImageFichier!], _banc.NomsImages());
        Assert.Contains(_journal.Infos, message => message.Contains('2'));
    }

    [Fact]
    public void Le_menage_garde_aussi_les_images_des_elements_nommes_d_un_autre_onglet()
    {
        var element = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(1)));
        var onglet = Depot.CreerOnglet("Projets");
        Depot.Nommer(element.Id, "Ma capture", onglet.Id);
        Poser("orpheline.png");

        Assert.Equal(1, Depot.NettoyerImagesOrphelines());

        Assert.Equal([element.ImageFichier!], _banc.NomsImages());
    }

    [Fact]
    public void Le_menage_ne_touche_qu_aux_fichiers_png_places_directement_dans_le_dossier()
    {
        var texte = Poser("notes.txt");
        var provisoire = Poser("ancien.png.tmp");
        var autreExtension = Poser("image.png2");
        var sansExtension = Poser("png");
        var sousDossier = Path.Combine(Images, "sous-dossier");
        Directory.CreateDirectory(sousDossier);
        var dansSousDossier = Path.Combine(sousDossier, "profonde.png");
        File.WriteAllText(dansSousDossier, "x");
        Poser("orpheline.png");

        Assert.Equal(1, Depot.NettoyerImagesOrphelines());

        Assert.True(File.Exists(texte));
        Assert.True(File.Exists(provisoire));
        Assert.True(File.Exists(autreExtension));
        Assert.True(File.Exists(sansExtension));
        Assert.True(File.Exists(dansSousDossier));
        Assert.False(File.Exists(Path.Combine(Images, "orpheline.png")));
    }

    [Fact]
    public void Le_menage_sans_dossier_des_images_ou_sans_orpheline_renvoie_zero()
    {
        using var sansDossier = new DepotDeTest(creerDossiers: false);
        Assert.Equal(0, sansDossier.Depot.NettoyerImagesOrphelines());

        Assert.Equal(0, Depot.NettoyerImagesOrphelines());
        Assert.Empty(_journal.Infos);
    }

    [Fact]
    public void Une_orpheline_verrouillee_est_notee_au_journal_et_n_est_pas_comptee()
    {
        var chemin = Poser("verrouillee.png");
        Poser("libre.png");

        int supprimes;
        using (new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            supprimes = Depot.NettoyerImagesOrphelines();
        }

        Assert.Equal(1, supprimes);
        Assert.True(File.Exists(chemin));
        Assert.Contains(_journal.Avertissements, message => message.Contains("verrouillee.png"));
    }

    [Fact]
    public async Task Le_menage_epargne_le_png_d_une_capture_dont_la_ligne_n_est_pas_encore_enregistree()
    {
        var contenu = ContenusDeTest.Image(ContenusDeTest.Png(5));
        Task<Element> capture;

        // Tenir le verrou du dépôt retarde l'enregistrement de la ligne, pas l'écriture du PNG.
        lock (_banc.Connexion)
        {
            capture = Task.Run(() => Depot.AjouterCopie(contenu));
            Assert.True(Attendre(() => _banc.NomsImages().Length == 1));
            Assert.False(capture.IsCompleted);

            // Le ménage (même fil, le verrou est réentrant) voit un PNG sans ligne : il ne doit pas l'emporter.
            Assert.Equal(0, Depot.NettoyerImagesOrphelines());
        }

        var element = await capture;

        Assert.Equal([element.ImageFichier!], _banc.NomsImages());
        Assert.Equal(ContenusDeTest.Png(5), await File.ReadAllBytesAsync(Depot.CheminImage(element), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Le_png_est_ecrit_avant_la_prise_du_verrou_du_depot()
    {
        Task<Element> capture;

        lock (_banc.Connexion)
        {
            capture = Task.Run(() => Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(6, 4096))));

            // Si l'écriture se faisait sous le verrou, le fichier n'apparaîtrait qu'après la libération.
            Assert.True(Attendre(() => _banc.NomsImages().Length == 1));
            Assert.False(capture.IsCompleted);
        }

        await capture;
        Assert.Equal(1, Depot.CompterElements(Depot.OngletTemporaire.Id));
    }

    // ---- Écriture sans fichier partiel ----

    [Fact]
    public void Une_ecriture_reussie_ne_laisse_que_le_png_final()
    {
        var png = ContenusDeTest.Png(2, 5000);

        var element = Depot.AjouterCopie(ContenusDeTest.Image(png));

        Assert.Equal([element.ImageFichier!], _banc.NomsImages());
        Assert.Equal(png, File.ReadAllBytes(Depot.CheminImage(element)));
    }

    [Fact]
    public void Une_ecriture_impossible_ne_laisse_ni_fichier_ni_ligne()
    {
        using var banc = new DepotDeTest(creerDossiers: false);
        // Un fichier à la place du dossier des images : l'écriture échoue avant d'avoir créé quoi que ce soit.
        File.WriteAllText(banc.Dossiers.Images, "pas un dossier");

        Assert.ThrowsAny<IOException>(() => banc.Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(3))));

        Assert.Equal(0, banc.Depot.CompterElements(banc.Depot.OngletTemporaire.Id));
        Assert.Equal(["pas un dossier"], [File.ReadAllText(banc.Dossiers.Images)]);
    }

    [Fact]
    public void Un_echec_de_l_insertion_retire_le_png_deja_ecrit()
    {
        // La base se ferme sous le dépôt : l'insertion échoue après l'écriture du fichier.
        _banc.Base.Dispose();

        Assert.ThrowsAny<Exception>(() => Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(4))));

        Assert.Empty(_banc.NomsImages());
    }

    [Fact]
    public void Recopier_une_image_identique_supprime_le_png_ecrit_en_trop()
    {
        var png = ContenusDeTest.Png(7);
        var premiere = Depot.AjouterCopie(ContenusDeTest.Image(png));

        var seconde = Depot.AjouterCopie(ContenusDeTest.Image(png));

        Assert.Equal(premiere.Id, seconde.Id);
        Assert.Equal(premiere.ImageFichier, seconde.ImageFichier);
        Assert.Equal([premiere.ImageFichier!], _banc.NomsImages());
    }

    private static bool Attendre(Func<bool> condition)
    {
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < limite)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return condition();
    }
}
