### Tâche 12 : DecisionCollage

**Fichiers :**
- Modifier : `src/PressePapiers.Coeur/Collage/DecisionCollage.cs` (le plan donne le contenu complet du fichier après modification)
- Créer : aucun
- Tests : `tests/PressePapiers.Tests/Collage/DecisionCollageTests.cs` (créé, 38 tests)

**Interfaces :**
- Utilise (tâche 1) : `enum IssueCollage { Colle, CopieDemandee, CopieSansFenetreOrigine, CopieFenetreFermee, CopieFenetreElevee, Echec }`
  (espace de noms `PressePapiers.Coeur.Collage`) et la coquille `DecisionCollage`.
- Fournit (signature du contrat, inchangée) :
  - `public static IssueCollage DecisionCollage.Decider(bool copieSeuleDemandee, bool fenetreOrigineConnue, bool fenetreExiste, int? niveauCible, int niveauOutil)`.
  - Ordre de décision, du plus prioritaire au moins prioritaire : copie seule demandée (`CopieDemandee`), pas de fenêtre
    d'origine (`CopieSansFenetreOrigine`), fenêtre disparue (`CopieFenetreFermee`), niveau d'intégrité illisible (null) ou
    supérieur à celui de l'outil (`CopieFenetreElevee`), sinon `Colle`. Un niveau égal ou inférieur colle. `Echec` n'est
    jamais renvoyé ici : il appartient au service (tâche 24).
  - Consommateur : tâche 24 (`ServiceRecollage.CollerAsync`).

- [ ] **Étape 1 : Écrire le test qui échoue**

`tests/PressePapiers.Tests/Collage/DecisionCollageTests.cs`

```csharp
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
    public void Decider_ne_renvoie_jamais_Echec()
    {
        // Echec est réservé au service (écriture ou premier plan impossibles), jamais à la décision préalable.
        foreach (var copie in new[] { true, false })
        foreach (var connue in new[] { true, false })
        foreach (var existe in new[] { true, false })
        foreach (var niveau in new int?[] { null, 0x1000, 0x2000, 0x3000 })
            Assert.NotEqual(IssueCollage.Echec, DecisionCollage.Decider(copie, connue, existe, niveau, NiveauOutil));
    }
}
```

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : les 38 nouveaux tests échouent avec `System.NotImplementedException : Tâche 12` ; les 408 tests des
tâches 1 à 11 restent verts :

```
Résumé de série de tests : Échec!
  total : 446
  échec : 38
  réussie : 408
```

- [ ] **Étape 3 : Implémenter**

`src/PressePapiers.Coeur/Collage/DecisionCollage.cs`

```csharp
namespace PressePapiers.Coeur.Collage;

/// <summary>Choix entre coller et seulement copier (spec, sections 10.2 et 10.3).</summary>
public static class DecisionCollage
{
    /// <summary>
    /// Décide de l'issue d'un collage. <paramref name="niveauCible"/> null (lecture impossible) est traité
    /// comme un niveau plus élevé que celui de l'outil : en cas de doute on ne colle pas, car Windows bloque
    /// sans rien signaler un Ctrl+V envoyé vers une fenêtre de niveau supérieur.
    /// </summary>
    /// <remarks>
    /// L'ordre des tests est le sens des causes : la demande de l'utilisateur d'abord, puis l'existence d'une
    /// fenêtre d'origine, puis sa survie, puis son niveau (illisible pour une fenêtre disparue).
    /// </remarks>
    public static IssueCollage Decider(bool copieSeuleDemandee, bool fenetreOrigineConnue, bool fenetreExiste,
                                       int? niveauCible, int niveauOutil)
    {
        if (copieSeuleDemandee) return IssueCollage.CopieDemandee;
        if (!fenetreOrigineConnue) return IssueCollage.CopieSansFenetreOrigine;
        if (!fenetreExiste) return IssueCollage.CopieFenetreFermee;
        if (niveauCible is not int niveau || niveau > niveauOutil) return IssueCollage.CopieFenetreElevee;
        return IssueCollage.Colle;
    }
}
```

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)`, `0 Erreur(s)`, puis tous les tests verts :

```
Résumé de série de tests : Réussite!
  total : 446
  échec : 0
  réussie : 446
  ignoré : 0
```

Les 38 nouveaux tests : `Decider_suit_la_table_de_verite` 32 (2 x 2 x 2 x 4 : copie seule, origine connue, fenêtre existante,
niveau cible plus bas, égal, plus haut, illisible), `Decider_compare_la_cible_au_niveau_de_l_outil_donne` 5,
`Decider_ne_renvoie_jamais_Echec` 1.

Contrôle de l'ordre, vérifié en inversant réellement l'implémentation puis en la restaurant : intervertir « origine inconnue »
et « fenêtre fermée » fait échouer les 4 lignes (origine inconnue, fenêtre fermée) ; placer « niveau élevé » avant « fenêtre
fermée » fait échouer les 2 lignes (fenêtre fermée, niveau supérieur ou illisible) ; ne tester la copie seule qu'en dernier fait
échouer 14 lignes (celles où une autre cause s'ajoute à la copie seule).

- [ ] **Étape 5 : Vérifier sur le poste**

Sans objet : logique pure, entièrement couverte par les tests. Le comportement réel se vérifie à la tâche 24.

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Collage/DecisionCollage.cs tests/PressePapiers.Tests/Collage/DecisionCollageTests.cs
git commit -m "Ajouter la decision de collage" -m "Table de verite complete (32 cas) : copie demandee, fenetre d'origine inconnue, fenetre fermee, niveau d'integrite superieur ou illisible. Un niveau illisible est traite comme superieur, car Windows bloque sans rien signaler un Ctrl+V vers une fenetre plus elevee."
```
