using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class DossierDonneesTests : IDisposable
{
    private readonly DossierTemporaire _dossier = new();

    public void Dispose() => _dossier.Dispose();

    [Fact]
    public void Les_chemins_derivent_de_la_racine()
    {
        var dossiers = new DossierDonnees(@"C:\Donnees");

        Assert.Equal(@"C:\Donnees", dossiers.Racine);
        Assert.Equal(@"C:\Donnees\historique.db", dossiers.Base);
        Assert.Equal(@"C:\Donnees\images", dossiers.Images);
        Assert.Equal(@"C:\Donnees\sauvegardes", dossiers.Sauvegardes);
        Assert.Equal(@"C:\Donnees\journal", dossiers.Journal);
        Assert.Equal(@"C:\Donnees\parametres.json", dossiers.Parametres);
    }

    [Fact]
    public void CreerDossiers_cree_la_racine_et_les_trois_sous_dossiers_sans_la_base()
    {
        var dossiers = new DossierDonnees(_dossier.Fichier("donnees"));

        dossiers.CreerDossiers();

        Assert.True(Directory.Exists(dossiers.Racine));
        Assert.True(Directory.Exists(dossiers.Images));
        Assert.True(Directory.Exists(dossiers.Sauvegardes));
        Assert.True(Directory.Exists(dossiers.Journal));
        Assert.False(File.Exists(dossiers.Base));
        Assert.False(File.Exists(dossiers.Parametres));
    }

    [Fact]
    public void CreerDossiers_peut_etre_rappele_sans_effacer_l_existant()
    {
        var dossiers = new DossierDonnees(_dossier.Fichier("donnees"));
        dossiers.CreerDossiers();
        var image = Path.Combine(dossiers.Images, "a.png");
        File.WriteAllBytes(image, [1, 2, 3]);

        dossiers.CreerDossiers();

        Assert.True(File.Exists(image));
    }

    [Fact]
    public void ParDefaut_prend_la_variable_d_environnement_quand_elle_est_definie()
    {
        var voulu = _dossier.Fichier("essai");

        var dossiers = DossierDonnees.ParDefaut(nom => nom == "PRESSEPAPIERS_DONNEES" ? voulu : null, @"C:\Local");

        Assert.Equal(voulu, dossiers.Racine);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParDefaut_retombe_sur_le_dossier_local_quand_la_variable_est_absente_ou_vide(string? valeur)
    {
        var dossiers = DossierDonnees.ParDefaut(_ => valeur, @"C:\Local");

        Assert.Equal(@"C:\Local\PressePapiers", dossiers.Racine);
    }

    [Fact]
    public void ParDefaut_rend_absolu_un_chemin_relatif_de_la_variable()
    {
        var dossiers = DossierDonnees.ParDefaut(_ => @"essai\donnees", @"C:\Local");

        Assert.True(Path.IsPathRooted(dossiers.Racine));
        Assert.EndsWith(@"essai\donnees", dossiers.Racine);
    }

    [Fact]
    public void ParDefaut_public_lit_la_vraie_variable_d_environnement()
    {
        var ancienne = Environment.GetEnvironmentVariable("PRESSEPAPIERS_DONNEES");
        var voulu = _dossier.Fichier("reel");
        try
        {
            Environment.SetEnvironmentVariable("PRESSEPAPIERS_DONNEES", voulu);
            Assert.Equal(voulu, DossierDonnees.ParDefaut().Racine);

            Environment.SetEnvironmentVariable("PRESSEPAPIERS_DONNEES", null);
            var sansVariable = DossierDonnees.ParDefaut().Racine;
            Assert.EndsWith("PressePapiers", sansVariable);
            Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), sansVariable);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PRESSEPAPIERS_DONNEES", ancienne);
        }
    }
}
