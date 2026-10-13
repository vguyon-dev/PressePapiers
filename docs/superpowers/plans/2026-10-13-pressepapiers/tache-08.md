### Tâche 8 : DemarrageSession

**Fichiers :**
- Créer : aucun
- Modifier : `src/PressePapiers.Coeur/Session/DemarrageSession.cs` (le plan donne le contenu complet du fichier après modification)
- Tests : `tests/PressePapiers.Tests/Session/DemarrageSessionTests.cs`

**Interfaces :**
- Utilise : (tâche 1) `ISourceSession { long? LireHeureOuverture(); }`, `EtatSession { PremierLancement, MemeSession, NouvelleSession, Indetermine }`, `ResultatSession(EtatSession Etat, int ElementsPurges)`, `IJournal` ; (tâches 4 à 6) `DepotHistorique.LireMeta`, `EcrireMeta`, `PurgerTemporaire`, `AjouterCopie`, `Nommer`, `CompterElements` ; `DepotDeTest`, `JournalCollecte`, `ContenusDeTest`.
- Fournit : `public const string DemarrageSession.CleRepere = "session.heure_ouverture"` ; `public static ResultatSession DemarrageSession.Appliquer(ISourceSession source, DepotHistorique depot, IJournal journal)`.

Remarques pour les tâches suivantes (tâche 16 : `App` appelle `Appliquer` avant de démarrer le relais ; tâche 16 aussi pour `SourceSessionWts`) :
- Règles de la spec, section 6 : pas de repère enregistré = `PremierLancement` (rien supprimé, repère écrit) ; repère identique = `MemeSession` ; repère différent = purge de Temporaire **puis** mise à jour du repère = `NouvelleSession` ; lecture `null` ou égale à 0 = `Indetermine`, aucune purge, repère inchangé, un avertissement au journal.
- Le repère est la valeur 64 bits brute écrite en décimal avec la culture invariante, comparée en égalité de texte. Un repère illisible en base est simplement différent de celui de la session : nouvelle session.
- Si la purge échoue (`SqliteException`), l'exception remonte à l'appelant, le repère garde l'ancienne valeur et la purge est retentée au lancement suivant (test avec un déclencheur qui refuse les suppressions). Entre la fin de la purge et l'écriture du repère, un échec ferait purger deux fois : le risque est négligeable et sans conséquence grave.
- Ajout au contrat : une exception levée par `ISourceSession.LireHeureOuverture` est traitée comme une lecture impossible (`Indetermine` et un seul avertissement qui porte l'exception), pour qu'un échec de Windows ne bloque pas le démarrage.
- La lecture réelle de `LogonTime` (tâche 16) et les points de recette de la section 16 de la spec (verrouillage, veille, Bureau à distance, démarrage rapide) ne sont pas couverts ici.

- [ ] **Étape 1 : Écrire le test qui échoue**

`DemarrageSessionTests` utilise une source fictive (`long? Valeur`, `Exception? Echec`) et un vrai dépôt : quatre situations de la spec, lecture nulle ou à 0 (avec et sans repère), source en exception, ordre purge puis repère, suite de lancements, repère illisible, clé du repère.

`tests/PressePapiers.Tests/Session/DemarrageSessionTests.cs`

```csharp
using System.Globalization;
using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Session;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Session;

public sealed class DemarrageSessionTests : IDisposable
{
    // Une valeur réaliste de FILETIME : 13/10/2026 environ, en intervalles de 100 ns depuis 1601.
    private const long OuvertureA = 134_050_000_000_000_000;
    private const long OuvertureB = 134_050_500_000_000_000;

    private readonly JournalCollecte _journal = new();
    private readonly DepotDeTest _banc;
    private readonly SourceFictive _source = new();

    public DemarrageSessionTests() => _banc = new DepotDeTest(_journal);

    private DepotHistorique Depot => _banc.Depot;

    public void Dispose() => _banc.Dispose();

    // ---- Les quatre situations de la spec ----

    [Fact]
    public void Au_premier_lancement_rien_n_est_supprime_et_le_repere_est_enregistre()
    {
        Depot.AjouterCopie(ContenusDeTest.Texte("copie avant le premier lancement"));
        _source.Valeur = OuvertureA;

        var resultat = Appliquer();

        Assert.Equal(new ResultatSession(EtatSession.PremierLancement, 0), resultat);
        Assert.Equal(1, Depot.CompterElements(Depot.OngletTemporaire.Id));
        Assert.Equal(OuvertureA.ToString(CultureInfo.InvariantCulture), Depot.LireMeta(DemarrageSession.CleRepere));
    }

    [Fact]
    public void Dans_la_meme_session_rien_n_est_supprime()
    {
        _source.Valeur = OuvertureA;
        Appliquer();
        Depot.AjouterCopie(ContenusDeTest.Texte("copie de la session"));

        var resultat = Appliquer();

        Assert.Equal(new ResultatSession(EtatSession.MemeSession, 0), resultat);
        Assert.Equal(1, Depot.CompterElements(Depot.OngletTemporaire.Id));
        Assert.Equal(OuvertureA.ToString(CultureInfo.InvariantCulture), Depot.LireMeta(DemarrageSession.CleRepere));
    }

    [Fact]
    public void Dans_une_nouvelle_session_Temporaire_est_purge_avec_ses_images_puis_le_repere_est_mis_a_jour()
    {
        var onglet = Depot.CreerOnglet("Clients");
        var nomme = Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(1)));
        Depot.Nommer(nomme.Id, "Logo", onglet.Id);
        _source.Valeur = OuvertureA;
        Appliquer();
        Depot.AjouterCopie(ContenusDeTest.Texte("copie de l'ancienne session"));
        Depot.AjouterCopie(ContenusDeTest.Image(ContenusDeTest.Png(2)));
        _source.Valeur = OuvertureB;

        var resultat = Appliquer();

        Assert.Equal(new ResultatSession(EtatSession.NouvelleSession, 2), resultat);
        Assert.Equal(0, Depot.CompterElements(Depot.OngletTemporaire.Id));
        // L'élément nommé et son image survivent à la purge.
        Assert.Equal(1, Depot.CompterElements(onglet.Id));
        Assert.Equal([nomme.ImageFichier], _banc.NomsImages());
        Assert.Equal(OuvertureB.ToString(CultureInfo.InvariantCulture), Depot.LireMeta(DemarrageSession.CleRepere));
    }

    [Fact]
    public void Une_nouvelle_session_sans_rien_a_purger_met_quand_meme_le_repere_a_jour()
    {
        _source.Valeur = OuvertureA;
        Appliquer();
        _source.Valeur = OuvertureB;

        var resultat = Appliquer();

        Assert.Equal(new ResultatSession(EtatSession.NouvelleSession, 0), resultat);
        Assert.Equal(OuvertureB.ToString(CultureInfo.InvariantCulture), Depot.LireMeta(DemarrageSession.CleRepere));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public void Une_lecture_nulle_ou_a_zero_ne_purge_rien_note_un_avertissement_et_ne_touche_pas_au_repere(long? valeur)
    {
        // Le repère existe déjà : une lecture impossible ne doit ni purger ni l'effacer.
        _source.Valeur = OuvertureA;
        Appliquer();
        Depot.AjouterCopie(ContenusDeTest.Texte("a conserver"));
        _source.Valeur = valeur;

        var resultat = Appliquer();

        Assert.Equal(new ResultatSession(EtatSession.Indetermine, 0), resultat);
        Assert.Equal(1, Depot.CompterElements(Depot.OngletTemporaire.Id));
        Assert.Equal(OuvertureA.ToString(CultureInfo.InvariantCulture), Depot.LireMeta(DemarrageSession.CleRepere));
        Assert.Single(_journal.Avertissements);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public void Une_lecture_nulle_au_premier_lancement_n_ecrit_aucun_repere(long? valeur)
    {
        _source.Valeur = valeur;

        var resultat = Appliquer();

        Assert.Equal(EtatSession.Indetermine, resultat.Etat);
        Assert.Null(Depot.LireMeta(DemarrageSession.CleRepere));
    }

    [Fact]
    public void Une_source_qui_leve_une_exception_donne_Indetermine_avec_un_seul_avertissement()
    {
        _source.Echec = new InvalidOperationException("service Bureau à distance arrêté");
        Depot.AjouterCopie(ContenusDeTest.Texte("a conserver"));

        var resultat = Appliquer();

        Assert.Equal(new ResultatSession(EtatSession.Indetermine, 0), resultat);
        Assert.Equal(1, Depot.CompterElements(Depot.OngletTemporaire.Id));
        Assert.Single(_journal.Avertissements);
    }

    // ---- Ordre des opérations ----

    [Fact]
    public void Si_la_purge_echoue_le_repere_n_est_pas_mis_a_jour_et_la_purge_est_retentee_au_lancement_suivant()
    {
        _source.Valeur = OuvertureA;
        Appliquer();
        Depot.AjouterCopie(ContenusDeTest.Texte("ancienne session"));
        _source.Valeur = OuvertureB;
        Executer("CREATE TRIGGER refus BEFORE DELETE ON element BEGIN SELECT RAISE(ABORT, 'suppression refusée'); END");

        Assert.Throws<SqliteException>(Appliquer);

        // Mis à jour avant la purge, le repère aurait fait croire à une session déjà traitée : l'élément resterait pour toujours.
        Assert.Equal(OuvertureA.ToString(CultureInfo.InvariantCulture), Depot.LireMeta(DemarrageSession.CleRepere));
        Assert.Equal(1, Depot.CompterElements(Depot.OngletTemporaire.Id));

        Executer("DROP TRIGGER refus");
        var resultat = Appliquer();

        Assert.Equal(new ResultatSession(EtatSession.NouvelleSession, 1), resultat);
        Assert.Equal(0, Depot.CompterElements(Depot.OngletTemporaire.Id));
    }

    [Fact]
    public void Une_suite_de_lancements_se_comporte_comme_les_sessions_Windows_qui_les_portent()
    {
        var etats = new List<EtatSession>();
        foreach (var heure in new long?[] { OuvertureA, OuvertureA, null, OuvertureA, OuvertureB, OuvertureB })
        {
            _source.Valeur = heure;
            etats.Add(Appliquer().Etat);
        }

        Assert.Equal(
            [
                EtatSession.PremierLancement,
                EtatSession.MemeSession,
                EtatSession.Indetermine,
                EtatSession.MemeSession,
                EtatSession.NouvelleSession,
                EtatSession.MemeSession,
            ],
            etats);
    }

    [Fact]
    public void Un_repere_illisible_en_base_est_traite_comme_une_autre_session()
    {
        Depot.EcrireMeta(DemarrageSession.CleRepere, "pas un nombre");
        Depot.AjouterCopie(ContenusDeTest.Texte("copie"));
        _source.Valeur = OuvertureA;

        var resultat = Appliquer();

        Assert.Equal(new ResultatSession(EtatSession.NouvelleSession, 1), resultat);
        Assert.Equal(OuvertureA.ToString(CultureInfo.InvariantCulture), Depot.LireMeta(DemarrageSession.CleRepere));
    }

    [Fact]
    public void Le_repere_est_garde_dans_la_cle_prevue_par_le_contrat()
    {
        Assert.Equal("session.heure_ouverture", DemarrageSession.CleRepere);
    }

    // ---- Assistants ----

    private ResultatSession Appliquer() => DemarrageSession.Appliquer(_source, Depot, _journal);

    private void Executer(string sql)
    {
        using var commande = _banc.Connexion.CreateCommand();
        commande.CommandText = sql;
        commande.ExecuteNonQuery();
    }

    private sealed class SourceFictive : ISourceSession
    {
        public long? Valeur { get; set; }

        public Exception? Echec { get; set; }

        public long? LireHeureOuverture() => Echec is null ? Valeur : throw Echec;
    }
}
```

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : 12 des 13 nouveaux tests échouent avec `System.NotImplementedException : Tâche 8` (le 13e, `Le_repere_est_garde_dans_la_cle_prevue_par_le_contrat`, ne lit qu'une constante) ; les 274 tests précédents restent verts :

```
Résumé de série de tests : Échec!
  total : 287
  échec : 12
  réussie : 275
  ignoré : 0
```

- [ ] **Étape 3 : Implémenter**

Remplacer la coquille par l'implémentation.

`src/PressePapiers.Coeur/Session/DemarrageSession.cs`

```csharp
using System.Globalization;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Coeur.Session;

/// <summary>
/// Détection d'une nouvelle session Windows au démarrage de l'outil, et purge de Temporaire (spec, section 6).
/// Le repère est l'heure d'ouverture de la session, gardée telle quelle et comparée en égalité exacte.
/// </summary>
public static class DemarrageSession
{
    /// <summary>Clé de <c>meta</c> qui garde l'heure d'ouverture de la session.</summary>
    public const string CleRepere = "session.heure_ouverture";

    /// <summary>
    /// Applique les règles de la spec : aucun repère enregistré (premier lancement) ou repère identique (même session),
    /// rien n'est supprimé ; repère différent (nouvelle session), Temporaire est purgé puis le repère mis à jour ;
    /// lecture impossible, nulle ou égale à 0, aucune purge, aucun changement du repère, et un avertissement au journal.
    /// La purge a lieu avant la mise à jour du repère : si elle échoue, le repère garde l'ancienne valeur et la purge
    /// est retentée au lancement suivant, au lieu d'être oubliée.
    /// </summary>
    /// <param name="source">Source de l'heure d'ouverture de la session.</param>
    /// <param name="depot">Dépôt qui porte Temporaire et la table <c>meta</c>.</param>
    /// <param name="journal">Journal des événements notables.</param>
    public static ResultatSession Appliquer(ISourceSession source, DepotHistorique depot, IJournal journal)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(depot);
        ArgumentNullException.ThrowIfNull(journal);

        long? heure;
        Exception? echec = null;
        try
        {
            heure = source.LireHeureOuverture();
        }
        catch (Exception exception)
        {
            // La source appelle Windows : sa panne ne doit pas empêcher le démarrage, seulement la purge.
            heure = null;
            echec = exception;
        }

        if (heure is null or 0)
        {
            journal.Avertissement(
                "Heure d'ouverture de la session Windows illisible : Temporaire n'est pas purgé, nouvel essai au prochain lancement.",
                echec);
            return new ResultatSession(EtatSession.Indetermine, 0);
        }

        var repere = heure.Value.ToString(CultureInfo.InvariantCulture);
        var precedent = depot.LireMeta(CleRepere);

        if (precedent is null)
        {
            depot.EcrireMeta(CleRepere, repere);
            journal.Info("Premier lancement : repère de session enregistré, Temporaire conservé.");
            return new ResultatSession(EtatSession.PremierLancement, 0);
        }

        if (precedent == repere)
        {
            return new ResultatSession(EtatSession.MemeSession, 0);
        }

        var purges = depot.PurgerTemporaire();
        depot.EcrireMeta(CleRepere, repere);
        journal.Info($"Nouvelle session Windows : {purges} élément(s) de Temporaire supprimé(s).");
        return new ResultatSession(EtatSession.NouvelleSession, purges);
    }
}
```

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : `0 Avertissement(s)`, `0 Erreur(s)`, puis

```
Résumé de série de tests : Réussite!
  total : 287
  échec : 0
  réussie : 287
  ignoré : 0
```

Les 13 nouveaux tests : `DemarrageSessionTests` 13.

Contrôle de l'ordre (vérifié par mutation) : `Si_la_purge_echoue_le_repere_n_est_pas_mis_a_jour_et_la_purge_est_retentee_au_lancement_suivant` échoue si le repère est écrit avant la purge.

- [ ] **Étape 5 : Vérifier sur le poste**

Sans objet : la logique ne dépend que de `ISourceSession`, remplacée dans les tests. La lecture réelle du repère se valide à la tâche 16 et à la recette (section 16, point 1).

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Session/DemarrageSession.cs tests/PressePapiers.Tests/Session/DemarrageSessionTests.cs
git commit -m "Détecter la nouvelle session Windows et purger Temporaire" -m "DemarrageSession compare l'heure d'ouverture de la session au repère gardé dans
meta : premier lancement et même session ne suppriment rien, une nouvelle session
purge Temporaire avant de mettre le repère à jour, et une lecture nulle, à 0 ou
en erreur n'efface rien et note un avertissement. Si la purge échoue, le repère
garde sa valeur et la purge est retentée au lancement suivant."
```
