using System.Globalization;
using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class LecteurElementTests : IDisposable
{
    private readonly DossierTemporaire _dossier = new();

    public void Dispose() => _dossier.Dispose();

    [Fact]
    public void Lire_relit_tous_les_champs_d_une_ligne_inseree()
    {
        using var baseHistorique = BaseHistorique.Ouvrir(_dossier.Fichier("historique.db"));
        var connexion = baseHistorique.Connexion;
        var onglet = InsertionsSql.InsererOnglet(connexion, "Notes", 1);
        var dateCopie = new DateTimeOffset(2026, 10, 13, 9, 30, 15, 123, TimeSpan.FromHours(2));
        var dateUsage = new DateTimeOffset(2026, 10, 13, 7, 45, 0, TimeSpan.Zero);

        using (var insertion = connexion.CreateCommand())
        {
            insertion.CommandText = """
                INSERT INTO element (onglet_id, nom, type, texte, texte_recherche, rtf, html, image_fichier, miniature,
                                     empreinte, application, date_copie, date_usage, taille)
                VALUES ($onglet, 'Mon nom', 2, 'bonjour', 'mon nom bonjour', $rtf, $html, 'a.png', $miniature,
                        $empreinte, 'Code', $copie, $usage, 42)
                """;
            insertion.Parameters.AddWithValue("$onglet", onglet);
            insertion.Parameters.AddWithValue("$rtf", new byte[] { 1, 2, 3 });
            insertion.Parameters.AddWithValue("$html", new byte[] { 4, 5 });
            insertion.Parameters.AddWithValue("$miniature", new byte[] { 6 });
            insertion.Parameters.AddWithValue("$empreinte", new byte[] { 7, 8, 9, 10 });
            insertion.Parameters.AddWithValue("$copie", dateCopie.ToString("O", CultureInfo.InvariantCulture));
            insertion.Parameters.AddWithValue("$usage", dateUsage.ToString("O", CultureInfo.InvariantCulture));
            insertion.ExecuteNonQuery();
        }

        var element = LireUnique(connexion);

        Assert.Equal(onglet, element.OngletId);
        Assert.Equal("Mon nom", element.Nom);
        Assert.Equal(TypeElement.MisEnForme, element.Type);
        Assert.Equal("bonjour", element.Texte);
        Assert.Equal(new byte[] { 1, 2, 3 }, element.Rtf);
        Assert.Equal(new byte[] { 4, 5 }, element.Html);
        Assert.Equal("a.png", element.ImageFichier);
        Assert.Equal(new byte[] { 6 }, element.Miniature);
        Assert.Equal(new byte[] { 7, 8, 9, 10 }, element.Empreinte);
        Assert.Equal("Code", element.Application);
        Assert.Equal(dateCopie, element.DateCopie);
        Assert.Equal(dateCopie.Offset, element.DateCopie.Offset);
        Assert.Equal(dateUsage, element.DateUsage);
        Assert.Equal(42, element.Taille);
    }

    [Fact]
    public void Lire_rend_null_les_colonnes_absentes()
    {
        using var baseHistorique = BaseHistorique.Ouvrir(_dossier.Fichier("historique.db"));
        var connexion = baseHistorique.Connexion;
        var onglet = InsertionsSql.InsererOnglet(connexion, "Temporaire", 0, temporaire: true);
        InsertionsSql.InsererElementTexte(connexion, onglet, "texte minimal");

        var element = LireUnique(connexion);

        Assert.Null(element.Nom);
        Assert.Null(element.Rtf);
        Assert.Null(element.Html);
        Assert.Null(element.ImageFichier);
        Assert.Null(element.Miniature);
        Assert.Null(element.Application);
        Assert.Equal(TypeElement.Texte, element.Type);
    }

    private static Element LireUnique(SqliteConnection connexion)
    {
        using var commande = connexion.CreateCommand();
        commande.CommandText = $"SELECT {LecteurElement.Colonnes} FROM element";
        using var lecteur = commande.ExecuteReader();
        Assert.True(lecteur.Read());
        var element = LecteurElement.Lire(lecteur);
        Assert.False(lecteur.Read());
        return element;
    }
}
