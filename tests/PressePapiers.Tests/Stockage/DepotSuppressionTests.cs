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
