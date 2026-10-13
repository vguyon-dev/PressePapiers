using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Tests.Stockage;

public sealed class EmpreinteTests
{
    private static string Hex(byte[] octets) => Convert.ToHexStringLower(octets);

    [Fact]
    public void Un_texte_donne_le_sha256_de_son_utf8()
    {
        var empreinte = Empreinte.Calculer(new ContenuCopie { Type = TypeElement.Texte, Texte = "abc" });

        // Vecteur de test officiel de SHA-256.
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Hex(empreinte));
    }

    [Fact]
    public void Le_texte_est_haché_en_utf8_avec_accents_et_emoji()
    {
        const string texte = "Été à l'œuvre 😀";

        var empreinte = Empreinte.Calculer(new ContenuCopie { Type = TypeElement.Texte, Texte = texte });

        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(texte)), empreinte);
    }

    [Fact]
    public void La_mise_en_forme_ne_change_pas_l_empreinte()
    {
        var simple = new ContenuCopie { Type = TypeElement.Texte, Texte = "bonjour" };
        var enrichi = new ContenuCopie
        {
            Type = TypeElement.MisEnForme,
            Texte = "bonjour",
            Rtf = [1, 2, 3],
            Html = [4, 5, 6],
        };

        Assert.Equal(Empreinte.Calculer(simple), Empreinte.Calculer(enrichi));
    }

    [Fact]
    public void Deux_textes_differents_ont_des_empreintes_differentes()
    {
        var a = Empreinte.Calculer(new ContenuCopie { Type = TypeElement.Texte, Texte = "un" });
        var b = Empreinte.Calculer(new ContenuCopie { Type = TypeElement.Texte, Texte = "Un" });

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Une_image_donne_le_sha256_de_ses_octets_png()
    {
        byte[] png = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3];

        var empreinte = Empreinte.Calculer(new ContenuCopie { Type = TypeElement.Image, ImagePng = png, Texte = "ignoré" });

        Assert.Equal(SHA256.HashData(png), empreinte);
    }

    [Fact]
    public void Une_liste_de_fichiers_donne_le_sha256_des_chemins_joints_par_un_retour_a_la_ligne()
    {
        var empreinte = Empreinte.Calculer(new ContenuCopie
        {
            Type = TypeElement.Fichiers,
            Fichiers = [@"C:\a.txt", @"C:\b.txt"],
        });

        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes("C:\\a.txt\nC:\\b.txt")), empreinte);
    }

    [Fact]
    public void L_ordre_des_fichiers_compte()
    {
        var ab = Empreinte.Calculer(new ContenuCopie { Type = TypeElement.Fichiers, Fichiers = ["a", "b"] });
        var ba = Empreinte.Calculer(new ContenuCopie { Type = TypeElement.Fichiers, Fichiers = ["b", "a"] });

        Assert.NotEqual(ab, ba);
    }

    [Theory]
    [InlineData(TypeElement.Texte)]
    [InlineData(TypeElement.MisEnForme)]
    [InlineData(TypeElement.Image)]
    [InlineData(TypeElement.Fichiers)]
    public void Un_contenu_principal_absent_est_refuse(TypeElement type) =>
        Assert.Throws<ArgumentException>(() => Empreinte.Calculer(new ContenuCopie { Type = type }));

    [Fact]
    public void Un_texte_de_9_Mo_et_une_image_4K_tiennent_en_moins_d_une_seconde()
    {
        // Échauffement : le premier appel paie la compilation à la volée, pas le calcul.
        Empreinte.Calculer(new ContenuCopie { Type = TypeElement.Texte, Texte = "chauffe" });
        Empreinte.Calculer(new ContenuCopie { Type = TypeElement.Image, ImagePng = [1] });

        var gros = new ContenuCopie { Type = TypeElement.Texte, Texte = new string('é', 9 * 1024 * 1024) };
        var chrono = Stopwatch.StartNew();
        var empreinteTexte = Empreinte.Calculer(gros);
        var dureeTexte = chrono.Elapsed;

        // Un PNG 3840 x 2160 pèse au plus ses pixels bruts : 4 octets par pixel, le pire cas.
        var pixels = new byte[3840 * 2160 * 4];
        new Random(1).NextBytes(pixels);
        chrono.Restart();
        var empreinteImage = Empreinte.Calculer(new ContenuCopie { Type = TypeElement.Image, ImagePng = pixels });
        var dureeImage = chrono.Elapsed;

        TestContext.Current.SendDiagnosticMessage(
            $"Empreinte : texte 9 Mo {dureeTexte.TotalMilliseconds:F0} ms, image 4K {dureeImage.TotalMilliseconds:F0} ms");

        Assert.Equal(32, empreinteTexte.Length);
        Assert.Equal(SHA256.HashData(pixels), empreinteImage);
        Assert.True(dureeTexte < TimeSpan.FromSeconds(1), $"Texte de 9 Mo : {dureeTexte.TotalMilliseconds:F0} ms");
        Assert.True(dureeImage < TimeSpan.FromSeconds(1), $"Image 4K : {dureeImage.TotalMilliseconds:F0} ms");
    }
}
