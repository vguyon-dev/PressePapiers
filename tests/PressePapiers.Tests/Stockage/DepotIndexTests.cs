using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

/// <summary>
/// Garde-fous de performance du dépôt : index présents, utilisés par les requêtes de Temporaire, et déclencheur de mise à jour
/// limité au texte de recherche (sinon une simple nouvelle date d'usage réindexerait un texte de plusieurs mégaoctets).
/// </summary>
public sealed class DepotIndexTests : IDisposable
{
    private readonly DepotDeTest _banc = new();

    public void Dispose() => _banc.Dispose();

    [Theory]
    [InlineData("element_onglet_empreinte")]
    [InlineData("element_onglet_usage")]
    public void Le_schema_porte_les_index_du_depot(string nom)
    {
        Assert.Equal(nom, InsertionsSql.Scalaire(_banc.Connexion, $"SELECT name FROM sqlite_master WHERE type = 'index' AND name = '{nom}'"));
    }

    [Fact]
    public void Le_declencheur_de_mise_a_jour_ne_reagit_qu_au_texte_de_recherche()
    {
        var sql = InsertionsSql.Scalaire(_banc.Connexion, "SELECT sql FROM sqlite_master WHERE name = 'element_au'");

        Assert.Contains("AFTER UPDATE OF texte_recherche ON element", sql);
    }

    [Fact]
    public void La_recherche_de_doublon_passe_par_l_index_des_empreintes()
    {
        var plan = Plan(DepotHistorique.RequeteDoublon, ("$onglet", 1L), ("$empreinte", new byte[] { 1 }), ("$premier", 1), ("$dernier", 2));

        Assert.Contains("element_onglet_empreinte", plan);
    }

    [Fact]
    public void La_liste_d_un_onglet_se_lit_dans_l_ordre_de_l_index_sans_tri()
    {
        var plan = Plan(
            $"SELECT {LecteurElement.Colonnes} FROM element WHERE onglet_id = $onglet ORDER BY date_usage DESC, id DESC",
            ("$onglet", 1L));

        Assert.Contains("element_onglet_usage", plan);
        Assert.DoesNotContain("TEMP B-TREE", plan);
    }

    private string Plan(string sql, params (string Nom, object Valeur)[] parametres)
    {
        using var commande = _banc.Connexion.CreateCommand();
        commande.CommandText = "EXPLAIN QUERY PLAN " + sql;
        foreach (var (nom, valeur) in parametres)
        {
            commande.Parameters.AddWithValue(nom, valeur);
        }

        using var lecteur = commande.ExecuteReader();
        var details = new List<string>();
        while (lecteur.Read())
        {
            details.Add(lecteur.GetString(3));
        }

        return string.Join(" | ", details);
    }
}
