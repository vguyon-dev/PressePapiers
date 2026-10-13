using PressePapiers.Coeur.Collage;

namespace PressePapiers.Tests.Collage;

/// <summary>
/// Table de vérité complète de la décision de collage (spec, sections 10.2 et 10.3) : 2 x 2 x 2 x 4 = 32 cas.
/// L'outil tourne au niveau moyen 0x2000 ; la cible est plus basse (0x1000), égale (0x2000), plus haute (0x3000)
/// ou illisible (null).
/// </summary>
public sealed class DecisionCollageTests
{
    private const int NiveauOutil = 0x2000;

    [Theory]
    // Copie seule demandée : prioritaire sur tout le reste, même fenêtre inconnue, fermée ou élevée.
    [InlineData(true, true, true, 0x1000, IssueCollage.CopieDemandee)]
    [InlineData(true, true, true, 0x2000, IssueCollage.CopieDemandee)]
    [InlineData(true, true, true, 0x3000, IssueCollage.CopieDemandee)]
    [InlineData(true, true, true, null, IssueCollage.CopieDemandee)]
    [InlineData(true, true, false, 0x1000, IssueCollage.CopieDemandee)]
    [InlineData(true, true, false, 0x2000, IssueCollage.CopieDemandee)]
    [InlineData(true, true, false, 0x3000, IssueCollage.CopieDemandee)]
    [InlineData(true, true, false, null, IssueCollage.CopieDemandee)]
    [InlineData(true, false, true, 0x1000, IssueCollage.CopieDemandee)]
    [InlineData(true, false, true, 0x2000, IssueCollage.CopieDemandee)]
    [InlineData(true, false, true, 0x3000, IssueCollage.CopieDemandee)]
    [InlineData(true, false, true, null, IssueCollage.CopieDemandee)]
    [InlineData(true, false, false, 0x1000, IssueCollage.CopieDemandee)]
    [InlineData(true, false, false, 0x2000, IssueCollage.CopieDemandee)]
    [InlineData(true, false, false, 0x3000, IssueCollage.CopieDemandee)]
    [InlineData(true, false, false, null, IssueCollage.CopieDemandee)]
    // Pas de fenêtre d'origine connue : prioritaire sur « fermée » et sur « élevée ».
    [InlineData(false, false, true, 0x1000, IssueCollage.CopieSansFenetreOrigine)]
    [InlineData(false, false, true, 0x2000, IssueCollage.CopieSansFenetreOrigine)]
    [InlineData(false, false, true, 0x3000, IssueCollage.CopieSansFenetreOrigine)]
    [InlineData(false, false, true, null, IssueCollage.CopieSansFenetreOrigine)]
    [InlineData(false, false, false, 0x1000, IssueCollage.CopieSansFenetreOrigine)]
    [InlineData(false, false, false, 0x2000, IssueCollage.CopieSansFenetreOrigine)]
    [InlineData(false, false, false, 0x3000, IssueCollage.CopieSansFenetreOrigine)]
    [InlineData(false, false, false, null, IssueCollage.CopieSansFenetreOrigine)]
    // Fenêtre connue mais disparue : prioritaire sur « élevée » (le niveau d'une fenêtre fermée est illisible).
    [InlineData(false, true, false, 0x1000, IssueCollage.CopieFenetreFermee)]
    [InlineData(false, true, false, 0x2000, IssueCollage.CopieFenetreFermee)]
    [InlineData(false, true, false, 0x3000, IssueCollage.CopieFenetreFermee)]
    [InlineData(false, true, false, null, IssueCollage.CopieFenetreFermee)]
    // Fenêtre vivante : niveau plus bas ou égal, on colle ; plus haut ou illisible, copie seule.
    [InlineData(false, true, true, 0x1000, IssueCollage.Colle)]
    [InlineData(false, true, true, 0x2000, IssueCollage.Colle)]
    [InlineData(false, true, true, 0x3000, IssueCollage.CopieFenetreElevee)]
    [InlineData(false, true, true, null, IssueCollage.CopieFenetreElevee)]
    public void Decider_suit_la_table_de_verite(bool copieSeule, bool origineConnue, bool existe, int? niveauCible,
                                                IssueCollage attendu)
    {
        var issue = DecisionCollage.Decider(copieSeule, origineConnue, existe, niveauCible, NiveauOutil);

        Assert.Equal(attendu, issue);
    }

    [Theory]
    [InlineData(0x2000, 0x1000, IssueCollage.Colle)]
    [InlineData(0x2000, 0x2000, IssueCollage.Colle)]
    [InlineData(0x2000, 0x3000, IssueCollage.CopieFenetreElevee)]
    [InlineData(0x3000, 0x2000, IssueCollage.Colle)]
    [InlineData(0x3000, 0x4000, IssueCollage.CopieFenetreElevee)]
    public void Decider_compare_la_cible_au_niveau_de_l_outil_donne(int niveauOutil, int niveauCible, IssueCollage attendu)
    {
        // Le niveau de l'outil n'est pas figé : un outil lancé en administrateur (0x3000) colle partout en dessous.
        var issue = DecisionCollage.Decider(false, true, true, niveauCible, niveauOutil);

        Assert.Equal(attendu, issue);
    }

    [Fact]
    public void Decider_ne_renvoie_jamais_Echec_ni_CollageNonEnvoye()
    {
        // Echec (écriture ou premier plan impossibles) et CollageNonEnvoye (modificateurs restés enfoncés) sont réservés
        // au service, jamais à la décision préalable.
        foreach (var copie in new[] { true, false })
        foreach (var connue in new[] { true, false })
        foreach (var existe in new[] { true, false })
        foreach (var niveau in new int?[] { null, 0x1000, 0x2000, 0x3000 })
        {
            var issue = DecisionCollage.Decider(copie, connue, existe, niveau, NiveauOutil);
            Assert.NotEqual(IssueCollage.Echec, issue);
            Assert.NotEqual(IssueCollage.CollageNonEnvoye, issue);
        }
    }
}
