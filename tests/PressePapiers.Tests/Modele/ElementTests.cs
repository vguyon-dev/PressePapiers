using PressePapiers.Coeur.Modele;

namespace PressePapiers.Tests.Modele;

public sealed class ElementTests
{
    private static Element Creer(TypeElement type, string? texte) => new()
    {
        Id = 1,
        OngletId = 1,
        Type = type,
        Texte = texte,
        Empreinte = [1],
        DateCopie = DateTimeOffset.UnixEpoch,
        DateUsage = DateTimeOffset.UnixEpoch,
        Taille = 1,
    };

    [Fact]
    public void Fichiers_decoupe_le_texte_d_une_liste_de_fichiers()
    {
        var element = Creer(TypeElement.Fichiers, "C:\\a.txt\nC:\\dossier\\b.txt");

        Assert.Equal(["C:\\a.txt", "C:\\dossier\\b.txt"], element.Fichiers);
    }

    [Theory]
    [InlineData(TypeElement.Texte, "ligne un\nligne deux")]
    [InlineData(TypeElement.MisEnForme, "ligne un\nligne deux")]
    [InlineData(TypeElement.Fichiers, null)]
    [InlineData(TypeElement.Fichiers, "")]
    public void Fichiers_est_vide_hors_liste_de_fichiers(TypeElement type, string? texte)
    {
        Assert.Empty(Creer(type, texte).Fichiers);
    }
}
