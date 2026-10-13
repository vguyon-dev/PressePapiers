### Tâche 1 : Socle : solution, paquets, modèle, base SQLite et coquilles

Pose tout ce que les tâches suivantes supposent présent : la solution et ses trois projets, la gestion centralisée des
paquets, le modèle, le journal, la base SQLite avec son schéma complet et sa lecture d'un élément, puis des coquilles
compilables de tous les autres types publics du contrat (corps `throw new NotImplementedException("Tâche NN")`, NN étant la
tâche qui les remplace). Chaque commit suivant se construit donc seul, sans type manquant.

**Versions retenues** (dernière version stable publiée depuis au moins deux semaines au 13/10/2026, soit au plus tard le
29/09/2026, relevées sur nuget.org) :

| Paquet | Version | Publiée le |
|---|---|---|
| Microsoft.WindowsAppSDK | 2.5.1 | imposée par la spec (couple validé par l'essai) |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2705 | imposée par la spec (couple validé par l'essai) |
| Microsoft.Data.Sqlite | 10.0.12 | 08/09/2026 |
| CommunityToolkit.Mvvm | 8.4.2 | 25/03/2026 |
| Serilog | 4.4.0 | 10/07/2026 |
| Serilog.Sinks.File | 7.0.0 | 28/04/2025 |
| xunit.v3 | 4.0.1 | 12/09/2026 (la 4.0.2 du 09/10/2026 est trop récente) |
| xunit.runner.visualstudio | 4.0.0 | 15/08/2026 (la 4.0.1 du 09/10/2026 est trop récente) |
| Microsoft.NET.Test.Sdk | 18.10.1 | 15/09/2026 |
| Microsoft.Extensions.TimeProvider.Testing | 10.10.0 | 09/09/2026 |

**Commandes de référence** (à la racine du dépôt, utilisées dans toutes les tâches) :

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Le SDK .NET 10 n'exécute plus les projets xunit.v3 par VSTest : le fichier `global.json` active le mode
Microsoft.Testing.Platform pour `dotnet test` (sans lui : erreur « Testing with VSTest target is no longer supported »).
Ce mode a sa propre syntaxe : `--solution` pour une solution, `--project` pour un projet. La solution compile l'application
en x64 d'elle-même (le projet déclare `<Platforms>x64</Platforms>`), aucun `-p:Platform` n'est nécessaire.

**Fichiers :**
- Créer : `Directory.Build.props`
- Créer : `Directory.Packages.props`
- Créer : `PressePapiers.slnx`
- Créer : `global.json`
- Créer : `src/PressePapiers.Coeur/Clavier/ActionCrochet.cs`
- Créer : `src/PressePapiers.Coeur/Clavier/AntiRebond.cs`
- Créer : `src/PressePapiers.Coeur/Clavier/AutomateWinV.cs`
- Créer : `src/PressePapiers.Coeur/Collage/DecisionCollage.cs`
- Créer : `src/PressePapiers.Coeur/Collage/IssueCollage.cs`
- Créer : `src/PressePapiers.Coeur/Formats/ConvertisseurImage.cs`
- Créer : `src/PressePapiers.Coeur/Formats/FormatHtml.cs`
- Créer : `src/PressePapiers.Coeur/Formats/ListeFichiers.cs`
- Créer : `src/PressePapiers.Coeur/Journalisation/IJournal.cs`
- Créer : `src/PressePapiers.Coeur/Journalisation/JournalNul.cs`
- Créer : `src/PressePapiers.Coeur/Modele/ContenuCopie.cs`
- Créer : `src/PressePapiers.Coeur/Modele/Element.cs`
- Créer : `src/PressePapiers.Coeur/Modele/ModeCollage.cs`
- Créer : `src/PressePapiers.Coeur/Modele/Onglet.cs`
- Créer : `src/PressePapiers.Coeur/Modele/OrigineOuverture.cs`
- Créer : `src/PressePapiers.Coeur/Modele/TypeElement.cs`
- Créer : `src/PressePapiers.Coeur/PressePapiers.Coeur.csproj`
- Créer : `src/PressePapiers.Coeur/Recherche/MoteurRecherche.cs`
- Créer : `src/PressePapiers.Coeur/Recherche/Normalisation.cs`
- Créer : `src/PressePapiers.Coeur/Reglages/Parametres.cs`
- Créer : `src/PressePapiers.Coeur/Reglages/Raccourci.cs`
- Créer : `src/PressePapiers.Coeur/Reglages/ServiceParametres.cs`
- Créer : `src/PressePapiers.Coeur/Reglages/ThemeApplication.cs`
- Créer : `src/PressePapiers.Coeur/Session/DemarrageSession.cs`
- Créer : `src/PressePapiers.Coeur/Session/EtatSession.cs`
- Créer : `src/PressePapiers.Coeur/Session/ISourceSession.cs`
- Créer : `src/PressePapiers.Coeur/Session/ResultatSession.cs`
- Créer : `src/PressePapiers.Coeur/Stockage/BaseHistorique.cs`
- Créer : `src/PressePapiers.Coeur/Stockage/BaseIncompatibleException.cs`
- Créer : `src/PressePapiers.Coeur/Stockage/DepotHistorique.cs`
- Créer : `src/PressePapiers.Coeur/Stockage/DossierDonnees.cs`
- Créer : `src/PressePapiers.Coeur/Stockage/Empreinte.cs`
- Créer : `src/PressePapiers.Coeur/Stockage/LecteurElement.cs`
- Créer : `src/PressePapiers.Coeur/Stockage/Sauvegarde.cs`
- Créer : `src/PressePapiers.Coeur/Stockage/SchemaBase.cs`
- Créer : `src/PressePapiers/App.xaml`
- Créer : `src/PressePapiers/App.xaml.cs`
- Créer : `src/PressePapiers/Capture/ApplicationOrigine.cs`
- Créer : `src/PressePapiers/Capture/EcrivainPressePapiers.cs`
- Créer : `src/PressePapiers/Capture/LecteurPressePapiers.cs`
- Créer : `src/PressePapiers/Capture/MarqueInterne.cs`
- Créer : `src/PressePapiers/Capture/ServiceCapture.cs`
- Créer : `src/PressePapiers/Capture/ServiceRecollage.cs`
- Créer : `src/PressePapiers/Interface/FenetrePrincipale.cs`
- Créer : `src/PressePapiers/Interface/FenetrePrincipaleVue.cs`
- Créer : `src/PressePapiers/PressePapiers.csproj`
- Créer : `src/PressePapiers/Program.cs`
- Créer : `src/PressePapiers/Systeme/CrochetWinV.cs`
- Créer : `src/PressePapiers/Systeme/DemarrageWindows.cs`
- Créer : `src/PressePapiers/Systeme/ICrochetWinV.cs`
- Créer : `src/PressePapiers/Systeme/IconeNotification.cs`
- Créer : `src/PressePapiers/Systeme/JournalSerilog.cs`
- Créer : `src/PressePapiers/Systeme/NiveauIntegrite.cs`
- Créer : `src/PressePapiers/Systeme/PremierPlan.cs`
- Créer : `src/PressePapiers/Systeme/RelaisWindows.cs`
- Créer : `src/PressePapiers/Systeme/SourceSessionWts.cs`
- Créer : `src/PressePapiers/app.manifest`
- Créer : `tests/PressePapiers.Tests/PressePapiers.Tests.csproj`
- Modifier : aucun
- Tests : `tests/PressePapiers.Tests/Modele/ElementTests.cs`
- Tests : `tests/PressePapiers.Tests/Outils/DossierTemporaire.cs`
- Tests : `tests/PressePapiers.Tests/Outils/InsertionsSql.cs`
- Tests : `tests/PressePapiers.Tests/Stockage/BaseHistoriqueTests.cs`
- Tests : `tests/PressePapiers.Tests/Stockage/LecteurElementTests.cs`

**Interfaces :**
- Utilise : rien d'autre que le SDK .NET 10 (10.0.401 sur le poste de référence) et la restauration NuGet.
- Fournit (complets, jamais remplacés) :
  - `PressePapiers.Coeur.Modele` : `TypeElement`, `ModeCollage`, `OrigineOuverture`, `Onglet`, `Element` (avec
    `IReadOnlyList<string> Fichiers`, découpe de `Texte` si `Type == TypeElement.Fichiers`), `ContenuCopie`, tels que
    définis au contrat, section 3.1.
  - `PressePapiers.Coeur.Journalisation` : `IJournal`, `JournalNul.Instance`.
  - `PressePapiers.Coeur.Stockage` : `BaseIncompatibleException(string message)` ;
    `BaseHistorique` avec `static BaseHistorique Ouvrir(string cheminFichier)`, `SqliteConnection Connexion`,
    `string VersionSqlite`, `Dispose()`, plus `internal static void ControlerVersion(string version)`,
    `internal const string CleVersionSchema = "schema.version"` et `internal const string VersionSchema = "1"` ;
    `internal static class LecteurElement` (`Colonnes`, `Element Lire(SqliteDataReader)`) ;
    `internal static class SchemaBase` (`Creation`, le SQL de la spec rejouable grâce à `IF NOT EXISTS`).
  - `PressePapiers.Coeur.Session` : `ISourceSession`, `EtatSession`, `ResultatSession`, `DemarrageSession.CleRepere`.
  - `PressePapiers.Coeur.Reglages` : `ThemeApplication`, `Parametres` (valeurs par défaut du contrat).
  - `PressePapiers.Coeur.Clavier` : `ActionCrochet`, constantes `AutomateWinV.VkWinGauche/VkWinDroite/VkV`.
  - `PressePapiers.Coeur.Collage` : `IssueCollage`.
  - `PressePapiers.Systeme` : `ICrochetWinV`, `RelaisWindows.MessageIcone`.
  - `PressePapiers.Capture` : `MarqueInterne.NomFormat`.
  - Application : `App` minimale (se termine dans `OnLaunched`), `Program.Main`, `FenetrePrincipaleVue.EtatCrochet`.
  - Tests : `PressePapiers.Tests.Outils.DossierTemporaire` (dossier jetable : `Chemin`, `Fichier(string nom)`, `Dispose()`)
    et `PressePapiers.Tests.Outils.InsertionsSql` (`InsererOnglet`, `InsererElementTexte`, `Trouver`, `Scalaire`).
  - Coquilles, remplacées plus tard sans changer de signature :

| Type (fichier sous `src/`) | Membres en coquille | Tâche |
|---|---|---|
| `DossierDonnees` (`Coeur/Stockage`) | propriétés, `ParDefaut()`, `ParDefaut(Func<string,string?>, string)` interne, `CreerDossiers()` | 2 |
| `Empreinte` (`Coeur/Stockage`) | `Calculer` | 2 |
| `Normalisation` (`Coeur/Recherche`) | `Normaliser`, `TexteRecherche` | 2 |
| `Raccourci` (`Coeur/Reglages`) | `TryAnalyser`, `Modificateurs`, `ToString` | 3 |
| `DepotHistorique` (`Coeur/Stockage`) | constructeur, `OngletTemporaire`, `ListerOnglets`, `CreerOnglet`, `RenommerOnglet`, `DeplacerOnglet`, `SupprimerOnglet` (événement `Modifie` déclaré) | 4 |
| `DepotHistorique` | `CompterElements`, `ListerElements`, `Lire`, `AjouterCopie`, `Nommer`, `MarquerUtilise`, `Supprimer`, `CheminImage` | 5 |
| `DepotHistorique` | `PurgerTemporaire`, `AppliquerPlafond`, `ReconstruireIndex`, `LireMeta`, `EcrireMeta` | 6 |
| `Sauvegarde` (`Coeur/Stockage`) | `Creer`, `PlusRecente`, `Restaurer` | 7 |
| `DemarrageSession` (`Coeur/Session`) | `Appliquer` | 8 |
| `MoteurRecherche` (`Coeur/Recherche`) | constructeur, `Chercher` | 9 |
| `ServiceParametres` (`Coeur/Reglages`) | constructeur, `Charger`, `Enregistrer` | 10 |
| `AutomateWinV`, `AntiRebond` (`Coeur/Clavier`) | `Traiter`, `Reinitialiser` ; constructeur, `Accepter` | 11 |
| `DecisionCollage` (`Coeur/Collage`) | `Decider` | 12 |
| `FormatHtml` (`Coeur/Formats`) | `Encoder`, `ExtraireFragment` | 13 |
| `ListeFichiers` (`Coeur/Formats`) | `EncoderDropFiles`, `DecoderDropFiles` | 14 |
| `ConvertisseurImage` (`Coeur/Formats`) | `DibVersPngAsync`, `PngVersDibAsync`, `MiniatureAsync` | 15 |
| `JournalSerilog`, `SourceSessionWts`, `DemarrageWindows` (`Systeme`) | tout | 16 |
| `RelaisWindows` (`Systeme`) | constructeur, `Handle`, `EcouterPressePapiers`, `EnregistrerRaccourci`, `RetirerRaccourci`, `Dispose` (six événements déclarés) | 17 |
| `IconeNotification` (`Systeme`) | constructeur, `Afficher`, `Notifier`, `Dispose` (trois événements déclarés) | 18 |
| `PremierPlan`, `NiveauIntegrite` (`Systeme`) | tout | 19 |
| `CrochetWinV` (`Systeme`) | constructeur, `EstActif`, `RaisonInactif`, `Demarrer`, `Reinstaller`, `Dispose` | 20 |
| `MarqueInterne.Format`, `ApplicationOrigine`, `LecteurPressePapiers` (`Capture`) | tout | 21 |
| `EcrivainPressePapiers` (`Capture`) | constructeur, `EcrireAsync` | 22 |
| `ServiceCapture` (`Capture`) | constructeur, `Dispose` (événement `ElementAjoute` déclaré) | 23 |
| `ServiceRecollage` (`Capture`) | constructeur, `CollerAsync` | 24 |
| `FenetrePrincipaleVue`, `FenetrePrincipale` (`Interface`) | constructeurs, `Handle`, `EstVisible` | 25 |
| `FenetrePrincipale` | `AfficherPour`, `Masquer` | 27 |
| `FenetrePrincipale` | `AfficherParametres` | 28 |

Remarques pour les tâches suivantes :
- Le fichier `Coeur/Stockage/SchemaBase.cs` et `ControlerVersion` ne figurent pas dans le contrat : ce sont des détails internes
  de la tâche 1.
- Les dates sont triées comme du texte par SQLite (`ORDER BY date_usage`). Pour que l'ordre texte soit l'ordre
  chronologique, `DepotHistorique` doit toujours écrire `horloge.GetUtcNow().ToString("O")` (décalage `+00:00` partout).
- Une coquille dont le constructeur lève ne peut pas être instanciée : les types de l'application ne sont donc pas encore
  assemblés ; l'assemblage vient avec les tâches 16 et 29.

- [ ] **Étape 1 : Écrire le test qui échoue**

Crée les dossiers, les fichiers de configuration qui hébergent les tests, puis les tests eux-mêmes.

```bash
mkdir -p src/PressePapiers.Coeur src/PressePapiers tests/PressePapiers.Tests/Outils tests/PressePapiers.Tests/Modele tests/PressePapiers.Tests/Stockage
```

`global.json`

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

`Directory.Build.props`

```xml
<Project>
  <!-- Réglages communs aux trois projets : une seule cible, mêmes règles de langage. -->
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

`Directory.Packages.props`

```xml
<Project>
  <!-- Gestion centralisée des versions NuGet : les projets référencent les paquets sans version. -->
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Application WinUI 3 (couple validé par l'essai du 13/10/2026). -->
    <PackageVersion Include="Microsoft.WindowsAppSDK" Version="2.5.1" />
    <PackageVersion Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.28000.2705" />
    <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.2" />
    <PackageVersion Include="Serilog" Version="4.4.0" />
    <PackageVersion Include="Serilog.Sinks.File" Version="7.0.0" />
    <!-- Coeur. -->
    <PackageVersion Include="Microsoft.Data.Sqlite" Version="10.0.12" />
    <!-- Tests. -->
    <PackageVersion Include="xunit.v3" Version="4.0.1" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="4.0.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.10.1" />
    <PackageVersion Include="Microsoft.Extensions.TimeProvider.Testing" Version="10.10.0" />
  </ItemGroup>
</Project>
```

`src/PressePapiers.Coeur/PressePapiers.Coeur.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>PressePapiers.Coeur</RootNamespace>
    <AssemblyName>PressePapiers.Coeur</AssemblyName>
    <!-- La documentation XML des membres publics est exigée : un membre non documenté avertit (CS1591). -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Data.Sqlite" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="PressePapiers.Tests" />
  </ItemGroup>

</Project>
```

`tests/PressePapiers.Tests/PressePapiers.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <!-- xunit.v3 exige un projet de test exécutable. -->
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <RootNamespace>PressePapiers.Tests</RootNamespace>
    <AssemblyName>PressePapiers.Tests</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\PressePapiers.Coeur\PressePapiers.Coeur.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

`tests/PressePapiers.Tests/Modele/ElementTests.cs`

```csharp
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
```

`tests/PressePapiers.Tests/Outils/DossierTemporaire.cs`

```csharp
using Microsoft.Data.Sqlite;

namespace PressePapiers.Tests.Outils;

/// <summary>Dossier jetable, propre à un test, supprimé avec tout son contenu à la fin.</summary>
public sealed class DossierTemporaire : IDisposable
{
    /// <summary>Crée un dossier neuf sous le dossier temporaire du système.</summary>
    public DossierTemporaire()
    {
        Chemin = Path.Combine(Path.GetTempPath(), "pp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Chemin);
    }

    /// <summary>Chemin complet du dossier.</summary>
    public string Chemin { get; }

    /// <summary>Chemin d'un fichier ou d'un sous-dossier du dossier jetable (rien n'est créé).</summary>
    public string Fichier(string nom) => Path.Combine(Chemin, nom);

    /// <inheritdoc />
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Chemin, recursive: true);
        }
        catch (IOException)
        {
            // Un fichier encore verrouillé ne doit pas faire échouer un test déjà jugé.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
```

`tests/PressePapiers.Tests/Outils/InsertionsSql.cs`

```csharp
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace PressePapiers.Tests.Outils;

/// <summary>Insertions SQL directes pour les tests qui ne passent pas encore par le dépôt.</summary>
public static class InsertionsSql
{
    /// <summary>Insère un onglet et renvoie son identifiant.</summary>
    public static long InsererOnglet(SqliteConnection connexion, string nom, int ordre, bool temporaire = false)
    {
        using var commande = connexion.CreateCommand();
        commande.CommandText = "INSERT INTO onglet (nom, ordre, est_temporaire) VALUES ($nom, $ordre, $temp); SELECT last_insert_rowid();";
        commande.Parameters.AddWithValue("$nom", nom);
        commande.Parameters.AddWithValue("$ordre", ordre);
        commande.Parameters.AddWithValue("$temp", temporaire ? 1 : 0);
        return Convert.ToInt64(commande.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>Insère un élément texte minimal et renvoie son identifiant.</summary>
    public static long InsererElementTexte(SqliteConnection connexion, long ongletId, string texteRecherche)
    {
        using var commande = connexion.CreateCommand();
        commande.CommandText = """
            INSERT INTO element (onglet_id, type, texte, texte_recherche, empreinte, date_copie, date_usage, taille)
            VALUES ($onglet, 1, $texte, $recherche, x'01', $date, $date, 10);
            SELECT last_insert_rowid();
            """;
        commande.Parameters.AddWithValue("$onglet", ongletId);
        commande.Parameters.AddWithValue("$texte", texteRecherche);
        commande.Parameters.AddWithValue("$recherche", texteRecherche);
        commande.Parameters.AddWithValue("$date", new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero).ToString("O", CultureInfo.InvariantCulture));
        return Convert.ToInt64(commande.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>Identifiants des lignes de l'index plein texte qui correspondent à une requête MATCH.</summary>
    public static List<long> Trouver(SqliteConnection connexion, string requeteMatch)
    {
        using var commande = connexion.CreateCommand();
        commande.CommandText = "SELECT rowid FROM element_fts WHERE element_fts MATCH $requete ORDER BY rowid";
        commande.Parameters.AddWithValue("$requete", requeteMatch);
        using var lecteur = commande.ExecuteReader();
        var ids = new List<long>();
        while (lecteur.Read())
        {
            ids.Add(lecteur.GetInt64(0));
        }

        return ids;
    }

    /// <summary>Valeur unique renvoyée par une requête, en texte.</summary>
    public static string? Scalaire(SqliteConnection connexion, string sql)
    {
        using var commande = connexion.CreateCommand();
        commande.CommandText = sql;
        return Convert.ToString(commande.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}
```

`tests/PressePapiers.Tests/Stockage/BaseHistoriqueTests.cs`

```csharp
using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class BaseHistoriqueTests : IDisposable
{
    private readonly DossierTemporaire _dossier = new();

    public void Dispose() => _dossier.Dispose();

    [Fact]
    public void Ouvrir_cree_le_schema_complet()
    {
        using var baseHistorique = BaseHistorique.Ouvrir(_dossier.Fichier("historique.db"));

        var objets = new List<string>();
        using (var commande = baseHistorique.Connexion.CreateCommand())
        {
            commande.CommandText = "SELECT name FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' AND name NOT LIKE 'element_fts_%'";
            using var lecteur = commande.ExecuteReader();
            while (lecteur.Read())
            {
                objets.Add(lecteur.GetString(0));
            }
        }

        foreach (var attendu in new[] { "onglet", "element", "element_fts", "meta", "element_ai", "element_ad", "element_au" })
        {
            Assert.Contains(attendu, objets);
        }

        Assert.Equal("1", InsertionsSql.Scalaire(baseHistorique.Connexion, "SELECT valeur FROM meta WHERE cle = 'schema.version'"));
    }

    [Fact]
    public void Ouvrir_active_le_mode_wal_et_les_cles_etrangeres()
    {
        using var baseHistorique = BaseHistorique.Ouvrir(_dossier.Fichier("historique.db"));

        Assert.Equal("wal", InsertionsSql.Scalaire(baseHistorique.Connexion, "PRAGMA journal_mode"));
        Assert.Equal("1", InsertionsSql.Scalaire(baseHistorique.Connexion, "PRAGMA foreign_keys"));
    }

    [Fact]
    public void Une_cle_etrangere_inconnue_est_refusee()
    {
        using var baseHistorique = BaseHistorique.Ouvrir(_dossier.Fichier("historique.db"));

        // L'onglet 999 n'existe pas : sans foreign_keys=ON, la ligne serait acceptée.
        Assert.Throws<SqliteException>(() => InsertionsSql.InsererElementTexte(baseHistorique.Connexion, 999, "orphelin"));
    }

    [Fact]
    public void Rouvrir_une_base_existante_garde_les_donnees()
    {
        var chemin = _dossier.Fichier("historique.db");
        using (var premiere = BaseHistorique.Ouvrir(chemin))
        {
            InsertionsSql.InsererOnglet(premiere.Connexion, "Temporaire", 0, temporaire: true);
        }

        using var seconde = BaseHistorique.Ouvrir(chemin);

        Assert.Equal("1", InsertionsSql.Scalaire(seconde.Connexion, "SELECT COUNT(*) FROM onglet"));
        Assert.Equal("1", InsertionsSql.Scalaire(seconde.Connexion, "SELECT valeur FROM meta WHERE cle = 'schema.version'"));
    }

    [Fact]
    public void L_index_trigram_trouve_un_mot_de_trois_lettres_et_suit_les_ecritures()
    {
        using var baseHistorique = BaseHistorique.Ouvrir(_dossier.Fichier("historique.db"));
        var connexion = baseHistorique.Connexion;
        var onglet = InsertionsSql.InsererOnglet(connexion, "Temporaire", 0, temporaire: true);

        var id = InsertionsSql.InsererElementTexte(connexion, onglet, "bonjour le monde");

        // Insertion : le mot de trois lettres est trouvé, un mot absent ne l'est pas.
        Assert.Equal([id], InsertionsSql.Trouver(connexion, "\"jou\""));
        Assert.Empty(InsertionsSql.Trouver(connexion, "\"zzz\""));

        // Mise à jour : l'ancien texte disparaît de l'index, le nouveau y entre.
        using (var maj = connexion.CreateCommand())
        {
            maj.CommandText = "UPDATE element SET texte_recherche = 'au revoir' WHERE id = $id";
            maj.Parameters.AddWithValue("$id", id);
            maj.ExecuteNonQuery();
        }

        Assert.Empty(InsertionsSql.Trouver(connexion, "\"jou\""));
        Assert.Equal([id], InsertionsSql.Trouver(connexion, "\"rev\""));

        // Suppression : plus rien dans l'index.
        using (var suppression = connexion.CreateCommand())
        {
            suppression.CommandText = "DELETE FROM element WHERE id = $id";
            suppression.Parameters.AddWithValue("$id", id);
            suppression.ExecuteNonQuery();
        }

        Assert.Empty(InsertionsSql.Trouver(connexion, "\"rev\""));
    }

    [Fact]
    public void La_version_de_sqlite_est_au_moins_3_34_0()
    {
        // Point 8 de la section 16 de la spec : la version réelle du paquet Microsoft.Data.Sqlite.
        using var baseHistorique = BaseHistorique.Ouvrir(_dossier.Fichier("historique.db"));

        TestContext.Current.SendDiagnosticMessage($"Version de SQLite embarquée : {baseHistorique.VersionSqlite}");

        Assert.True(Version.Parse(baseHistorique.VersionSqlite) >= new Version(3, 34, 0));
    }

    [Theory]
    [InlineData("3.34.0")]
    [InlineData("3.45.1")]
    [InlineData("4.0.0")]
    public void ControlerVersion_accepte_une_version_recente(string version) =>
        BaseHistorique.ControlerVersion(version);

    [Theory]
    [InlineData("3.33.9")]
    [InlineData("2.8.17")]
    [InlineData("n'importe quoi")]
    [InlineData("")]
    public void ControlerVersion_refuse_une_version_ancienne_ou_illisible(string version)
    {
        var exception = Assert.Throws<BaseIncompatibleException>(() => BaseHistorique.ControlerVersion(version));

        Assert.Contains("3.34.0", exception.Message);
    }

    [Theory]
    [InlineData("pas une base")]
    [InlineData("Ceci est un fichier texte ordinaire, bien plus long qu'un en-tête SQLite de seize octets.")]
    public void Un_fichier_texte_n_est_pas_ouvert_comme_une_base_et_reste_intact(string contenu)
    {
        var chemin = _dossier.Fichier("texte.db");
        File.WriteAllText(chemin, contenu);

        Assert.Throws<SqliteException>(() => BaseHistorique.Ouvrir(chemin));

        // Le fichier de l'utilisateur n'est ni modifié ni gardé ouvert.
        Assert.Equal(contenu, File.ReadAllText(chemin));
        File.Delete(chemin);
        Assert.False(File.Exists(chemin));
    }

    [Fact]
    public void Un_dossier_absent_leve_une_exception_sqlite()
    {
        var chemin = Path.Combine(_dossier.Chemin, "absent", "historique.db");

        Assert.Throws<SqliteException>(() => BaseHistorique.Ouvrir(chemin));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_chemin_vide_est_refuse(string chemin) =>
        Assert.Throws<ArgumentException>(() => BaseHistorique.Ouvrir(chemin));
}
```

`tests/PressePapiers.Tests/Stockage/LecteurElementTests.cs`

```csharp
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
```

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --project tests/PressePapiers.Tests/PressePapiers.Tests.csproj
```

Résultat attendu : échec de compilation du projet de tests, car `PressePapiers.Coeur.Modele`, `Element`, `TypeElement`,
`BaseHistorique`, `LecteurElement` n'existent pas encore. Les messages ressemblent à :

```
error CS0234: Le nom de type ou d'espace de noms 'Coeur' n'existe pas dans l'espace de noms 'PressePapiers'
error CS0246: Le nom de type ou d'espace de noms 'Element' est introuvable
error CS0103: Le nom 'TypeElement' n'existe pas dans le contexte actuel
```

- [ ] **Étape 3 : Implémenter**

Crée les fichiers suivants (contenu complet). Les types qui portent un corps `throw new NotImplementedException("Tâche NN")`
sont des coquilles : ne change ni leurs signatures ni leur emplacement, les tâches suivantes les remplacent.

`PressePapiers.slnx`

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/PressePapiers.Coeur/PressePapiers.Coeur.csproj" />
    <Project Path="src/PressePapiers/PressePapiers.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/PressePapiers.Tests/PressePapiers.Tests.csproj" />
  </Folder>
</Solution>
```

`src/PressePapiers.Coeur/Clavier/ActionCrochet.cs`

```csharp
namespace PressePapiers.Coeur.Clavier;

/// <summary>Décision de l'automate Win+V pour une frappe vue par le hook.</summary>
public enum ActionCrochet
{
    /// <summary>Laisser la frappe suivre son chemin.</summary>
    Laisser,

    /// <summary>Avaler la frappe.</summary>
    Avaler,

    /// <summary>Avaler la frappe et ouvrir la fenêtre.</summary>
    AvalerEtOuvrir,

    /// <summary>Envoyer la touche sans effet pour masquer le menu Démarrer, puis laisser passer le relâchement de Win.</summary>
    MasquerPuisLaisser,
}
```

`src/PressePapiers.Coeur/Clavier/AntiRebond.cs`

```csharp
namespace PressePapiers.Coeur.Clavier;

/// <summary>Ignore un signal trop proche du précédent. Coquille : remplacée par la tâche 11.</summary>
public sealed class AntiRebond
{
    /// <summary>Crée l'anti-rebond avec son horloge et son délai.</summary>
    public AntiRebond(TimeProvider horloge, TimeSpan delai) => throw new NotImplementedException("Tâche 11");

    /// <summary>Faux si le dernier signal accepté date de moins que le délai.</summary>
    public bool Accepter() => throw new NotImplementedException("Tâche 11");
}
```

`src/PressePapiers.Coeur/Clavier/AutomateWinV.cs`

```csharp
namespace PressePapiers.Coeur.Clavier;

/// <summary>Logique pure du hook Win+V (spec, section 11), testable sans Windows. Coquille : remplacée par la tâche 11.</summary>
public sealed class AutomateWinV
{
    /// <summary>Touche Windows gauche.</summary>
    public const uint VkWinGauche = 0x5B;

    /// <summary>Touche Windows droite.</summary>
    public const uint VkWinDroite = 0x5C;

    /// <summary>Touche V.</summary>
    public const uint VkV = 0x56;

    /// <summary>
    /// Décide du sort d'une frappe. <paramref name="injectee"/> : frappe portant LLKHF_INJECTED (toujours laissée) ;
    /// <paramref name="winReellementEnfoncee"/> : résultat de GetAsyncKeyState au moment de V.
    /// </summary>
    public ActionCrochet Traiter(uint touche, bool enfoncee, bool injectee, bool winReellementEnfoncee) =>
        throw new NotImplementedException("Tâche 11");

    /// <summary>Oublie l'état « Win enfoncée » : déverrouillage, sortie de veille, réinstallation du hook.</summary>
    public void Reinitialiser() => throw new NotImplementedException("Tâche 11");
}
```

`src/PressePapiers.Coeur/Collage/DecisionCollage.cs`

```csharp
namespace PressePapiers.Coeur.Collage;

/// <summary>Choix entre coller et seulement copier (spec, sections 10.2 et 10.3). Coquille : remplacée par la tâche 12.</summary>
public static class DecisionCollage
{
    /// <summary>
    /// Décide de l'issue d'un collage. <paramref name="niveauCible"/> null (lecture impossible) est traité
    /// comme un niveau plus élevé que celui de l'outil.
    /// </summary>
    public static IssueCollage Decider(bool copieSeuleDemandee, bool fenetreOrigineConnue, bool fenetreExiste,
                                       int? niveauCible, int niveauOutil) =>
        throw new NotImplementedException("Tâche 12");
}
```

`src/PressePapiers.Coeur/Collage/IssueCollage.cs`

```csharp
namespace PressePapiers.Coeur.Collage;

/// <summary>Résultat d'une demande de collage.</summary>
public enum IssueCollage
{
    /// <summary>Ctrl+V a été envoyé à la fenêtre d'origine.</summary>
    Colle,

    /// <summary>Copie seule demandée par l'utilisateur (Ctrl+Entrée).</summary>
    CopieDemandee,

    /// <summary>Pas de fenêtre d'origine fiable : copie seulement.</summary>
    CopieSansFenetreOrigine,

    /// <summary>La fenêtre d'origine n'existe plus : copie seulement.</summary>
    CopieFenetreFermee,

    /// <summary>La fenêtre d'origine est de niveau d'intégrité supérieur : copie seulement.</summary>
    CopieFenetreElevee,

    /// <summary>L'écriture dans le presse-papiers ou le retour au premier plan a échoué.</summary>
    Echec,
}
```

`src/PressePapiers.Coeur/Formats/ConvertisseurImage.cs`

```csharp
namespace PressePapiers.Coeur.Formats;

/// <summary>Conversions entre DIB du presse-papiers et PNG, par WIC. Coquille : remplacée par la tâche 15.</summary>
public static class ConvertisseurImage
{
    /// <summary>Convertit un CF_DIB ou CF_DIBV5 en PNG ; l'alpha est conservé pour un V5 avec masque alpha.</summary>
    public static Task<byte[]> DibVersPngAsync(byte[] dib) => throw new NotImplementedException("Tâche 15");

    /// <summary>Convertit un PNG en DIB, pour écrire CF_DIB.</summary>
    public static Task<byte[]> PngVersDibAsync(byte[] png) => throw new NotImplementedException("Tâche 15");

    /// <summary>Réduit un PNG à une miniature de <paramref name="largeurMax"/> pixels de large au plus.</summary>
    public static Task<byte[]> MiniatureAsync(byte[] png, uint largeurMax = 160) => throw new NotImplementedException("Tâche 15");
}
```

`src/PressePapiers.Coeur/Formats/FormatHtml.cs`

```csharp
namespace PressePapiers.Coeur.Formats;

/// <summary>Format « HTML Format » du presse-papiers. Coquille : remplacée par la tâche 13.</summary>
public static class FormatHtml
{
    /// <summary>Encode un fragment : en-tête <c>Version:1.0</c>, offsets en octets UTF-8, lignes terminées par CRLF.</summary>
    public static byte[] Encoder(string fragmentHtml) => throw new NotImplementedException("Tâche 13");

    /// <summary>Extrait le fragment d'un « HTML Format » ; null si les données sont invalides.</summary>
    public static string? ExtraireFragment(ReadOnlySpan<byte> donnees) => throw new NotImplementedException("Tâche 13");
}
```

`src/PressePapiers.Coeur/Formats/ListeFichiers.cs`

```csharp
namespace PressePapiers.Coeur.Formats;

/// <summary>Format CF_HDROP (structure DROPFILES). Coquille : remplacée par la tâche 14.</summary>
public static class ListeFichiers
{
    /// <summary>Encode des chemins en structure DROPFILES, chemins en UTF-16.</summary>
    public static byte[] EncoderDropFiles(IReadOnlyList<string> chemins) => throw new NotImplementedException("Tâche 14");

    /// <summary>Décode une structure DROPFILES en liste de chemins.</summary>
    public static IReadOnlyList<string> DecoderDropFiles(ReadOnlySpan<byte> donnees) => throw new NotImplementedException("Tâche 14");
}
```

`src/PressePapiers.Coeur/Journalisation/IJournal.cs`

```csharp
namespace PressePapiers.Coeur.Journalisation;

/// <summary>Journal des événements notables ; Coeur n'en connaît que cette interface.</summary>
public interface IJournal
{
    /// <summary>Note un événement ordinaire.</summary>
    void Info(string message);

    /// <summary>Note un problème qui n'empêche pas de continuer.</summary>
    void Avertissement(string message, Exception? exception = null);

    /// <summary>Note une erreur.</summary>
    void Erreur(string message, Exception? exception = null);
}
```

`src/PressePapiers.Coeur/Journalisation/JournalNul.cs`

```csharp
namespace PressePapiers.Coeur.Journalisation;

/// <summary>Journal qui ne retient rien, pour les tests et les cas sans journal.</summary>
public sealed class JournalNul : IJournal
{
    /// <summary>Instance partagée.</summary>
    public static readonly JournalNul Instance = new();

    /// <inheritdoc />
    public void Info(string message)
    {
    }

    /// <inheritdoc />
    public void Avertissement(string message, Exception? exception = null)
    {
    }

    /// <inheritdoc />
    public void Erreur(string message, Exception? exception = null)
    {
    }
}
```

`src/PressePapiers.Coeur/Modele/ContenuCopie.cs`

```csharp
namespace PressePapiers.Coeur.Modele;

/// <summary>Ce que la capture a lu dans le presse-papiers, avant enregistrement.</summary>
public sealed record ContenuCopie
{
    /// <summary>Nature du contenu.</summary>
    public required TypeElement Type { get; init; }

    /// <summary>Texte brut.</summary>
    public string? Texte { get; init; }

    /// <summary>Contenu RTF brut.</summary>
    public byte[]? Rtf { get; init; }

    /// <summary>Octets bruts du format « HTML Format ».</summary>
    public byte[]? Html { get; init; }

    /// <summary>Image convertie en PNG.</summary>
    public byte[]? ImagePng { get; init; }

    /// <summary>Miniature PNG de l'image.</summary>
    public byte[]? Miniature { get; init; }

    /// <summary>Chemins des fichiers copiés.</summary>
    public IReadOnlyList<string>? Fichiers { get; init; }

    /// <summary>Nom du processus d'origine, indication au mieux.</summary>
    public string? Application { get; init; }
}
```

`src/PressePapiers.Coeur/Modele/Element.cs`

```csharp
namespace PressePapiers.Coeur.Modele;

/// <summary>Un élément de l'historique, tel qu'il est lu en base.</summary>
public sealed record Element
{
    /// <summary>Identifiant en base.</summary>
    public required long Id { get; init; }

    /// <summary>Onglet qui contient l'élément.</summary>
    public required long OngletId { get; init; }

    /// <summary>Nom choisi par l'utilisateur ; absent tant que l'élément est temporaire.</summary>
    public string? Nom { get; init; }

    /// <summary>Nature du contenu.</summary>
    public required TypeElement Type { get; init; }

    /// <summary>Texte brut ; pour <see cref="TypeElement.Fichiers"/>, les chemins séparés par un retour à la ligne.</summary>
    public string? Texte { get; init; }

    /// <summary>Contenu RTF brut.</summary>
    public byte[]? Rtf { get; init; }

    /// <summary>Octets bruts du format « HTML Format ».</summary>
    public byte[]? Html { get; init; }

    /// <summary>Nom du PNG dans le dossier des images.</summary>
    public string? ImageFichier { get; init; }

    /// <summary>Miniature PNG, 160 pixels de large au plus.</summary>
    public byte[]? Miniature { get; init; }

    /// <summary>Empreinte SHA-256 du contenu principal, utilisée pour le dédoublonnage.</summary>
    public required byte[] Empreinte { get; init; }

    /// <summary>Nom du processus d'origine, indication au mieux.</summary>
    public string? Application { get; init; }

    /// <summary>Date de la copie.</summary>
    public required DateTimeOffset DateCopie { get; init; }

    /// <summary>Date du dernier usage ; elle fixe l'ordre d'affichage dans l'onglet.</summary>
    public required DateTimeOffset DateUsage { get; init; }

    /// <summary>Taille en octets du contenu principal.</summary>
    public required long Taille { get; init; }

    /// <summary>Chemins de la liste de fichiers (découpe de <see cref="Texte"/>) ; vide pour les autres types.</summary>
    public IReadOnlyList<string> Fichiers =>
        Type == TypeElement.Fichiers && !string.IsNullOrEmpty(Texte)
            ? Texte.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            : [];
}
```

`src/PressePapiers.Coeur/Modele/ModeCollage.cs`

```csharp
namespace PressePapiers.Coeur.Modele;

/// <summary>Façon de remettre un élément dans le presse-papiers au moment du collage.</summary>
public enum ModeCollage
{
    /// <summary>Texte brut uniquement.</summary>
    TexteSeul = 0,

    /// <summary>Texte brut plus la mise en forme conservée (RTF et HTML).</summary>
    AvecMiseEnForme = 1,
}
```

`src/PressePapiers.Coeur/Modele/Onglet.cs`

```csharp
namespace PressePapiers.Coeur.Modele;

/// <summary>Un onglet de rangement ; un seul est temporaire, il ne se renomme ni ne se supprime.</summary>
/// <param name="Id">Identifiant en base.</param>
/// <param name="Nom">Nom affiché.</param>
/// <param name="Ordre">Position d'affichage, l'onglet Temporaire valant 0.</param>
/// <param name="EstTemporaire">Vrai pour l'onglet Temporaire, vidé à chaque nouvelle session Windows.</param>
public sealed record Onglet(long Id, string Nom, int Ordre, bool EstTemporaire);
```

`src/PressePapiers.Coeur/Modele/OrigineOuverture.cs`

```csharp
namespace PressePapiers.Coeur.Modele;

/// <summary>Ce qui a demandé l'ouverture de la fenêtre principale.</summary>
public enum OrigineOuverture
{
    /// <summary>Win+V intercepté par le hook clavier.</summary>
    WinV,

    /// <summary>Raccourci de secours enregistré par <c>RegisterHotKey</c>.</summary>
    RaccourciSecours,

    /// <summary>Clic sur l'icône de notification ou commande « Ouvrir » de son menu.</summary>
    Icone,

    /// <summary>Second lancement de l'application, redirigé vers l'instance principale.</summary>
    Relance,
}
```

`src/PressePapiers.Coeur/Modele/TypeElement.cs`

```csharp
namespace PressePapiers.Coeur.Modele;

/// <summary>Nature du contenu d'un élément ; les valeurs sont celles stockées dans la colonne <c>type</c>.</summary>
public enum TypeElement
{
    /// <summary>Texte brut seul.</summary>
    Texte = 1,

    /// <summary>Texte accompagné de RTF et/ou de HTML.</summary>
    MisEnForme = 2,

    /// <summary>Image, conservée en PNG dans le dossier des images.</summary>
    Image = 3,

    /// <summary>Liste de fichiers (chemins séparés par des retours à la ligne).</summary>
    Fichiers = 4,
}
```

`src/PressePapiers.Coeur/Recherche/MoteurRecherche.cs`

```csharp
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Coeur.Recherche;

/// <summary>Recherche plein texte dans tous les onglets. Coquille : remplacée par la tâche 9.</summary>
public sealed class MoteurRecherche
{
    /// <summary>Crée le moteur sur une base ouverte.</summary>
    public MoteurRecherche(BaseHistorique baseHistorique) => throw new NotImplementedException("Tâche 9");

    /// <summary>
    /// Tous les mots sont requis ; un mot de 3 caractères ou plus passe par MATCH (entre guillemets doubles),
    /// un mot de 1 ou 2 caractères par LIKE avec ESCAPE ; liste vide si la requête est vide ; DateUsage décroissante.
    /// </summary>
    public IReadOnlyList<Element> Chercher(string requete, int limite = 500) => throw new NotImplementedException("Tâche 9");
}
```

`src/PressePapiers.Coeur/Recherche/Normalisation.cs`

```csharp
namespace PressePapiers.Coeur.Recherche;

/// <summary>Normalisation du texte de recherche, partagée entre l'écriture de l'index et la requête. Coquille : tâche 2.</summary>
public static class Normalisation
{
    /// <summary>Minuscules, accents retirés (décomposition Unicode), ligatures ramenées (œ en oe, æ en ae), espaces réduits.</summary>
    public static string Normaliser(string? texte) => throw new NotImplementedException("Tâche 2");

    /// <summary>Texte normalisé qui réunit le nom, le texte, les chemins de fichiers et l'application d'origine.</summary>
    public static string TexteRecherche(string? nom, string? texte, IReadOnlyList<string>? fichiers, string? application) =>
        throw new NotImplementedException("Tâche 2");
}
```

`src/PressePapiers.Coeur/Reglages/Parametres.cs`

```csharp
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Coeur.Reglages;

/// <summary>Réglages de l'utilisateur, avec les valeurs par défaut de la spec.</summary>
public sealed record Parametres
{
    /// <summary>Raccourci de secours, toujours actif en plus de Win+V.</summary>
    public string RaccourciSecours { get; init; } = "Ctrl+Alt+Maj+V";

    /// <summary>Lancer l'application à l'ouverture de session Windows.</summary>
    public bool DemarrageAvecWindows { get; init; } = true;

    /// <summary>Mode de collage quand l'utilisateur n'en choisit pas.</summary>
    public ModeCollage MiseEnFormeParDefaut { get; init; } = ModeCollage.TexteSeul;

    /// <summary>Nombre maximal d'éléments dans Temporaire ; 0 désactive le plafond.</summary>
    public int PlafondTemporaire { get; init; } = 5000;

    /// <summary>Thème de la fenêtre.</summary>
    public ThemeApplication Theme { get; init; } = ThemeApplication.Systeme;

    /// <summary>Applications dont le texte s'affiche en police à chasse fixe.</summary>
    public IReadOnlyList<string> ApplicationsChasseFixe { get; init; } =
        ["WindowsTerminal", "Code", "devenv", "powershell", "pwsh", "cmd", "Ssms", "rider64", "notepad++"];

    /// <summary>Largeur mémorisée de la fenêtre, en pixels logiques (multipliée par l'échelle de l'écran à l'affichage).</summary>
    public int LargeurFenetre { get; init; } = 920;

    /// <summary>Hauteur mémorisée de la fenêtre, en pixels logiques (multipliée par l'échelle de l'écran à l'affichage).</summary>
    public int HauteurFenetre { get; init; } = 560;
}
```

`src/PressePapiers.Coeur/Reglages/Raccourci.cs`

```csharp
namespace PressePapiers.Coeur.Reglages;

/// <summary>Raccourci clavier global. Coquille : remplacée par la tâche 3.</summary>
/// <param name="Ctrl">Touche Ctrl.</param>
/// <param name="Alt">Touche Alt.</param>
/// <param name="Maj">Touche Maj.</param>
/// <param name="ToucheVirtuelle">Code de touche virtuelle de la touche principale.</param>
public readonly record struct Raccourci(bool Ctrl, bool Alt, bool Maj, uint ToucheVirtuelle)
{
    /// <summary>
    /// Analyse un texte du type « Ctrl+Alt+Maj+V » (ordre et casse libres, « Shift » accepté pour Maj) : au moins Ctrl ou Alt,
    /// jamais la touche Windows, touche principale parmi A-Z, 0-9, F1-F24 et Espace.
    /// </summary>
    public static bool TryAnalyser(string texte, out Raccourci raccourci) => throw new NotImplementedException("Tâche 3");

    /// <summary>MOD_CONTROL, MOD_ALT et MOD_SHIFT selon les drapeaux, plus MOD_NOREPEAT (0x4000).</summary>
    public uint Modificateurs => throw new NotImplementedException("Tâche 3");

    /// <summary>Forme canonique « Ctrl+Alt+Maj+V ».</summary>
    public override string ToString() => throw new NotImplementedException("Tâche 3");
}
```

`src/PressePapiers.Coeur/Reglages/ServiceParametres.cs`

```csharp
using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Coeur.Reglages;

/// <summary>Lecture et écriture du fichier <c>parametres.json</c>. Coquille : remplacée par la tâche 10.</summary>
public sealed class ServiceParametres
{
    /// <summary>Crée le service sur un fichier de réglages.</summary>
    public ServiceParametres(string cheminFichier, IJournal journal) => throw new NotImplementedException("Tâche 10");

    /// <summary>Lit les réglages ; les valeurs par défaut si le fichier est absent ou illisible (avertissement au journal).</summary>
    public Parametres Charger() => throw new NotImplementedException("Tâche 10");

    /// <summary>Écrit les réglages de façon atomique : fichier temporaire, puis <c>File.Move(..., true)</c>.</summary>
    public void Enregistrer(Parametres parametres) => throw new NotImplementedException("Tâche 10");
}
```

`src/PressePapiers.Coeur/Reglages/ThemeApplication.cs`

```csharp
namespace PressePapiers.Coeur.Reglages;

/// <summary>Thème de la fenêtre principale.</summary>
public enum ThemeApplication
{
    /// <summary>Suit le thème de Windows.</summary>
    Systeme,

    /// <summary>Thème clair.</summary>
    Clair,

    /// <summary>Thème sombre.</summary>
    Sombre,
}
```

`src/PressePapiers.Coeur/Session/DemarrageSession.cs`

```csharp
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Coeur.Session;

/// <summary>Détection d'une nouvelle session Windows et purge de Temporaire. Coquille : remplacée par la tâche 8.</summary>
public static class DemarrageSession
{
    /// <summary>Clé de <c>meta</c> qui garde l'heure d'ouverture de la session.</summary>
    public const string CleRepere = "session.heure_ouverture";

    /// <summary>
    /// Applique les règles de la spec (section 6) : la purge a lieu avant la mise à jour du repère ;
    /// une valeur nulle ou 0 donne <see cref="EtatSession.Indetermine"/> sans purge.
    /// </summary>
    public static ResultatSession Appliquer(ISourceSession source, DepotHistorique depot, IJournal journal) =>
        throw new NotImplementedException("Tâche 8");
}
```

`src/PressePapiers.Coeur/Session/EtatSession.cs`

```csharp
namespace PressePapiers.Coeur.Session;

/// <summary>Situation de la session Windows au démarrage de l'outil.</summary>
public enum EtatSession
{
    /// <summary>Aucun repère enregistré : rien n'est supprimé.</summary>
    PremierLancement,

    /// <summary>Même repère qu'au lancement précédent : rien n'est supprimé.</summary>
    MemeSession,

    /// <summary>Repère différent : Temporaire est purgé.</summary>
    NouvelleSession,

    /// <summary>Repère illisible ou nul : aucune purge.</summary>
    Indetermine,
}
```

`src/PressePapiers.Coeur/Session/ISourceSession.cs`

```csharp
namespace PressePapiers.Coeur.Session;

/// <summary>Source du repère de session Windows ; remplaçable dans les tests.</summary>
public interface ISourceSession
{
    /// <summary>FILETIME brut de <c>WTSINFOW.LogonTime</c> ; null si la lecture est impossible.</summary>
    long? LireHeureOuverture();
}
```

`src/PressePapiers.Coeur/Session/ResultatSession.cs`

```csharp
namespace PressePapiers.Coeur.Session;

/// <summary>Issue de la détection de session au démarrage.</summary>
/// <param name="Etat">Situation constatée.</param>
/// <param name="ElementsPurges">Nombre d'éléments supprimés de Temporaire.</param>
public sealed record ResultatSession(EtatSession Etat, int ElementsPurges);
```

`src/PressePapiers.Coeur/Stockage/BaseHistorique.cs`

```csharp
using Microsoft.Data.Sqlite;

namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// La base SQLite de l'historique : une connexion ouverte, le schéma en place, le mode WAL et les clés étrangères actifs.
/// La connexion n'est pas sûre entre fils : <see cref="DepotHistorique"/> la protège par un verrou.
/// </summary>
public sealed class BaseHistorique : IDisposable
{
    /// <summary>Clé de <c>meta</c> qui porte la version du schéma, pour les migrations futures.</summary>
    internal const string CleVersionSchema = "schema.version";

    /// <summary>Version du schéma créé par cette version de l'application.</summary>
    internal const string VersionSchema = "1";

    /// <summary>Première version de SQLite qui connaît le tokenizer <c>trigram</c>.</summary>
    internal static readonly Version VersionMinimale = new(3, 34, 0);

    private BaseHistorique(SqliteConnection connexion, string versionSqlite)
    {
        Connexion = connexion;
        VersionSqlite = versionSqlite;
    }

    /// <summary>Connexion ouverte sur la base.</summary>
    public SqliteConnection Connexion { get; }

    /// <summary>Valeur de <c>sqlite_version()</c> de la bibliothèque embarquée.</summary>
    public string VersionSqlite { get; }

    /// <summary>
    /// Ouvre ou crée la base : schéma de la spec (section 5), <c>journal_mode=WAL</c>, <c>foreign_keys=ON</c>.
    /// </summary>
    /// <param name="cheminFichier">Chemin du fichier de base ; le dossier doit exister.</param>
    /// <exception cref="BaseIncompatibleException">SQLite inférieure à 3.34.0, ou FTS5 absent.</exception>
    /// <exception cref="SqliteException">Le fichier n'est pas une base SQLite, ou n'est pas lisible.</exception>
    public static BaseHistorique Ouvrir(string cheminFichier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cheminFichier);

        // Pas de réserve de connexions : fermer la base libère le fichier tout de suite (sauvegarde, restauration, tests).
        var chaine = new SqliteConnectionStringBuilder
        {
            DataSource = cheminFichier,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();

        var connexion = new SqliteConnection(chaine);
        try
        {
            connexion.Open();
            var version = Scalaire(connexion, "SELECT sqlite_version()");
            ControlerVersion(version);
            ControlerFts5(connexion);

            Executer(connexion, "PRAGMA journal_mode = WAL");
            Executer(connexion, "PRAGMA foreign_keys = ON");
            CreerSchema(connexion);
            return new BaseHistorique(connexion, version);
        }
        catch
        {
            connexion.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose() => Connexion.Dispose();

    /// <summary>Refuse une version de SQLite qui ne connaît pas le tokenizer <c>trigram</c>.</summary>
    internal static void ControlerVersion(string version)
    {
        if (!Version.TryParse(version, out var lue) || lue < VersionMinimale)
        {
            throw new BaseIncompatibleException(
                $"La version de SQLite embarquée ({version}) est trop ancienne : la version {VersionMinimale} ou plus est nécessaire pour la recherche.");
        }
    }

    // Une table temporaire jetable prouve à la fois la présence de FTS5 et celle du tokenizer trigram.
    private static void ControlerFts5(SqliteConnection connexion)
    {
        try
        {
            Executer(connexion, "CREATE VIRTUAL TABLE temp.essai_fts5 USING fts5(x, tokenize='trigram')");
            Executer(connexion, "DROP TABLE temp.essai_fts5");
        }
        catch (SqliteException exception)
        {
            throw new BaseIncompatibleException(
                $"La SQLite embarquée ne gère pas l'index de recherche FTS5 avec trigrammes : {exception.Message}");
        }
    }

    private static void CreerSchema(SqliteConnection connexion)
    {
        using var transaction = connexion.BeginTransaction();
        Executer(connexion, SchemaBase.Creation, transaction);

        using var commande = connexion.CreateCommand();
        commande.Transaction = transaction;
        commande.CommandText = "INSERT OR IGNORE INTO meta (cle, valeur) VALUES ($cle, $valeur)";
        commande.Parameters.AddWithValue("$cle", CleVersionSchema);
        commande.Parameters.AddWithValue("$valeur", VersionSchema);
        commande.ExecuteNonQuery();

        transaction.Commit();
    }

    private static void Executer(SqliteConnection connexion, string sql, SqliteTransaction? transaction = null)
    {
        using var commande = connexion.CreateCommand();
        commande.Transaction = transaction;
        commande.CommandText = sql;
        commande.ExecuteNonQuery();
    }

    private static string Scalaire(SqliteConnection connexion, string sql)
    {
        using var commande = connexion.CreateCommand();
        commande.CommandText = sql;
        return Convert.ToString(commande.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }
}
```

`src/PressePapiers.Coeur/Stockage/BaseIncompatibleException.cs`

```csharp
namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// La SQLite embarquée ne convient pas : version inférieure à 3.34.0 (tokenizer <c>trigram</c>) ou FTS5 absent.
/// L'application affiche le message et s'arrête proprement.
/// </summary>
/// <param name="message">Explication destinée à l'utilisateur.</param>
public sealed class BaseIncompatibleException(string message) : Exception(message);
```

`src/PressePapiers.Coeur/Stockage/DepotHistorique.cs`

```csharp
#pragma warning disable CS0067 // Coquille : l'événement Modifie est levé par la tâche 4.
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// Dépôt des onglets et des éléments, sûr entre fils (verrou interne). Coquille : onglets par la tâche 4,
/// éléments par la tâche 5, purge, plafond, index et méta par la tâche 6.
/// </summary>
public sealed class DepotHistorique
{
    /// <summary>Crée le dépôt sur une base ouverte.</summary>
    public DepotHistorique(BaseHistorique baseHistorique, DossierDonnees dossiers, TimeProvider horloge, IJournal journal) =>
        throw new NotImplementedException("Tâche 4");

    /// <summary>Onglet Temporaire, créé à la première ouverture (nom « Temporaire », ordre 0).</summary>
    public Onglet OngletTemporaire => throw new NotImplementedException("Tâche 4");

    /// <summary>Après toute écriture réussie ; peut être levé sur n'importe quel fil.</summary>
    public event Action? Modifie;

    /// <summary>Onglets par ordre, Temporaire en premier.</summary>
    public IReadOnlyList<Onglet> ListerOnglets() => throw new NotImplementedException("Tâche 4");

    /// <summary>Crée un onglet à la fin ; le nom ne peut pas être vide.</summary>
    public Onglet CreerOnglet(string nom) => throw new NotImplementedException("Tâche 4");

    /// <summary>Renomme un onglet ; <see cref="InvalidOperationException"/> pour Temporaire.</summary>
    public void RenommerOnglet(long ongletId, string nom) => throw new NotImplementedException("Tâche 4");

    /// <summary>Déplace un onglet à une nouvelle position.</summary>
    public void DeplacerOnglet(long ongletId, int nouvelOrdre) => throw new NotImplementedException("Tâche 4");

    /// <summary>Supprime un onglet, ses éléments et leurs PNG ; <see cref="InvalidOperationException"/> pour Temporaire.</summary>
    public void SupprimerOnglet(long ongletId) => throw new NotImplementedException("Tâche 4");

    /// <summary>Nombre d'éléments d'un onglet.</summary>
    public int CompterElements(long ongletId) => throw new NotImplementedException("Tâche 5");

    /// <summary>Éléments d'un onglet, par DateUsage décroissante puis Id décroissant (ordre stable à date égale).</summary>
    public IReadOnlyList<Element> ListerElements(long ongletId) => throw new NotImplementedException("Tâche 5");

    /// <summary>Lit un élément, ou null s'il n'existe pas.</summary>
    public Element? Lire(long elementId) => throw new NotImplementedException("Tâche 5");

    /// <summary>
    /// Ajoute une copie à Temporaire avec dédoublonnage, écrit le PNG d'une image ;
    /// renvoie l'élément créé ou remonté.
    /// </summary>
    public Element AjouterCopie(ContenuCopie contenu) => throw new NotImplementedException("Tâche 5");

    /// <summary>Donne un nom à un élément et le range dans un onglet autre que Temporaire.</summary>
    public Element Nommer(long elementId, string nom, long ongletId) => throw new NotImplementedException("Tâche 5");

    /// <summary>Met à jour la date de dernier usage d'un élément.</summary>
    public void MarquerUtilise(long elementId) => throw new NotImplementedException("Tâche 5");

    /// <summary>Supprime un élément et son PNG.</summary>
    public void Supprimer(long elementId) => throw new NotImplementedException("Tâche 5");

    /// <summary>Chemin complet du PNG d'un élément ; <see cref="InvalidOperationException"/> s'il n'a pas d'image.</summary>
    public string CheminImage(Element element) => throw new NotImplementedException("Tâche 5");

    /// <summary>Supprime les éléments de Temporaire et leurs PNG ; renvoie leur nombre.</summary>
    public int PurgerTemporaire() => throw new NotImplementedException("Tâche 6");

    /// <summary>Supprime les plus anciens éléments de Temporaire au-delà du plafond (0 = désactivé) ; renvoie leur nombre.</summary>
    public int AppliquerPlafond(int plafond) => throw new NotImplementedException("Tâche 6");

    /// <summary>Reconstruit l'index plein texte.</summary>
    public void ReconstruireIndex() => throw new NotImplementedException("Tâche 6");

    /// <summary>Lit une valeur de la table <c>meta</c>.</summary>
    public string? LireMeta(string cle) => throw new NotImplementedException("Tâche 6");

    /// <summary>Écrit une valeur dans la table <c>meta</c>.</summary>
    public void EcrireMeta(string cle, string valeur) => throw new NotImplementedException("Tâche 6");
}
```

`src/PressePapiers.Coeur/Stockage/DossierDonnees.cs`

```csharp
namespace PressePapiers.Coeur.Stockage;

/// <summary>Emplacements des données de l'application. Coquille : remplacée par la tâche 2.</summary>
/// <param name="Racine">Dossier racine des données.</param>
public sealed record DossierDonnees(string Racine)
{
    /// <summary>Fichier de la base : <c>Racine\historique.db</c>.</summary>
    public string Base => throw new NotImplementedException("Tâche 2");

    /// <summary>Dossier des images PNG : <c>Racine\images</c>.</summary>
    public string Images => throw new NotImplementedException("Tâche 2");

    /// <summary>Dossier des copies de sécurité : <c>Racine\sauvegardes</c>.</summary>
    public string Sauvegardes => throw new NotImplementedException("Tâche 2");

    /// <summary>Dossier du journal : <c>Racine\journal</c>.</summary>
    public string Journal => throw new NotImplementedException("Tâche 2");

    /// <summary>Fichier des réglages : <c>Racine\parametres.json</c>.</summary>
    public string Parametres => throw new NotImplementedException("Tâche 2");

    /// <summary>
    /// <c>%LOCALAPPDATA%\PressePapiers</c>, ou le dossier de la variable d'environnement <c>PRESSEPAPIERS_DONNEES</c>
    /// si elle est définie (essais et recette sans toucher aux données réelles).
    /// </summary>
    public static DossierDonnees ParDefaut() => throw new NotImplementedException("Tâche 2");

    /// <summary>Variante testable de <see cref="ParDefaut()"/> : la lecture des variables d'environnement est injectée.</summary>
    internal static DossierDonnees ParDefaut(Func<string, string?> lireVariable, string dossierDonneesLocales) =>
        throw new NotImplementedException("Tâche 2");

    /// <summary>Crée la racine et les trois sous-dossiers s'ils n'existent pas.</summary>
    public void CreerDossiers() => throw new NotImplementedException("Tâche 2");
}
```

`src/PressePapiers.Coeur/Stockage/Empreinte.cs`

```csharp
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Coeur.Stockage;

/// <summary>Empreinte du contenu principal, pour le dédoublonnage. Coquille : remplacée par la tâche 2.</summary>
public static class Empreinte
{
    /// <summary>
    /// SHA-256 : texte en UTF-8 (Texte, MisEnForme), octets du PNG (Image), chemins joints par un retour à la ligne (Fichiers).
    /// </summary>
    /// <param name="contenu">Contenu copié.</param>
    public static byte[] Calculer(ContenuCopie contenu) => throw new NotImplementedException("Tâche 2");
}
```

`src/PressePapiers.Coeur/Stockage/LecteurElement.cs`

```csharp
using System.Globalization;
using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Coeur.Stockage;

/// <summary>Transforme une ligne de la table <c>element</c> en <see cref="Element"/>.</summary>
internal static class LecteurElement
{
    /// <summary>Colonnes à sélectionner, dans l'ordre que lit <see cref="Lire"/>.</summary>
    public const string Colonnes = "id, onglet_id, nom, type, texte, rtf, html, image_fichier, miniature, empreinte, application, date_copie, date_usage, taille";

    /// <summary>Lit la ligne courante du lecteur ; les colonnes suivent l'ordre de <see cref="Colonnes"/>.</summary>
    public static Element Lire(SqliteDataReader lecteur) => new()
    {
        Id = lecteur.GetInt64(0),
        OngletId = lecteur.GetInt64(1),
        Nom = Chaine(lecteur, 2),
        Type = (TypeElement)lecteur.GetInt32(3),
        Texte = Chaine(lecteur, 4),
        Rtf = Octets(lecteur, 5),
        Html = Octets(lecteur, 6),
        ImageFichier = Chaine(lecteur, 7),
        Miniature = Octets(lecteur, 8),
        Empreinte = lecteur.GetFieldValue<byte[]>(9),
        Application = Chaine(lecteur, 10),
        DateCopie = Date(lecteur, 11),
        DateUsage = Date(lecteur, 12),
        Taille = lecteur.GetInt64(13),
    };

    private static string? Chaine(SqliteDataReader lecteur, int colonne) =>
        lecteur.IsDBNull(colonne) ? null : lecteur.GetString(colonne);

    private static byte[]? Octets(SqliteDataReader lecteur, int colonne) =>
        lecteur.IsDBNull(colonne) ? null : lecteur.GetFieldValue<byte[]>(colonne);

    // Les dates sont stockées en texte ISO 8601 aller-retour (format "O").
    private static DateTimeOffset Date(SqliteDataReader lecteur, int colonne) =>
        DateTimeOffset.ParseExact(lecteur.GetString(colonne), "O", CultureInfo.InvariantCulture);
}
```

`src/PressePapiers.Coeur/Stockage/Sauvegarde.cs`

```csharp
namespace PressePapiers.Coeur.Stockage;

/// <summary>Copies de sécurité de la base. Coquille : remplacée par la tâche 7.</summary>
public static class Sauvegarde
{
    /// <summary>
    /// Copie par <c>SqliteConnection.BackupDatabase</c> dans <paramref name="dossierSauvegardes"/>
    /// (<c>historique-AAAAMMJJ-HHmmss.db</c>) et garde les <paramref name="nombreGarde"/> plus récentes.
    /// Renvoie le chemin créé, ou null s'il n'y a pas de base.
    /// </summary>
    public static string? Creer(string cheminBase, string dossierSauvegardes, TimeProvider horloge, int nombreGarde = 3) =>
        throw new NotImplementedException("Tâche 7");

    /// <summary>Chemin de la sauvegarde la plus récente, ou null s'il n'y en a pas.</summary>
    public static string? PlusRecente(string dossierSauvegardes) => throw new NotImplementedException("Tâche 7");

    /// <summary>Remplace la base par une sauvegarde, la base étant fermée ; supprime les fichiers <c>-wal</c> et <c>-shm</c>.</summary>
    public static void Restaurer(string cheminSauvegarde, string cheminBase) => throw new NotImplementedException("Tâche 7");
}
```

`src/PressePapiers.Coeur/Stockage/SchemaBase.cs`

```csharp
namespace PressePapiers.Coeur.Stockage;

/// <summary>Schéma SQL de la base (spec, section 5), écrit pour pouvoir être rejoué sans effet sur une base existante.</summary>
internal static class SchemaBase
{
    /// <summary>Instructions de création : tables, index plein texte et déclencheurs de synchronisation.</summary>
    public const string Creation = """
        CREATE TABLE IF NOT EXISTS onglet (
          id              INTEGER PRIMARY KEY,
          nom             TEXT NOT NULL,
          ordre           INTEGER NOT NULL,
          est_temporaire  INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS element (
          id              INTEGER PRIMARY KEY,
          onglet_id       INTEGER NOT NULL REFERENCES onglet(id),
          nom             TEXT,
          type            INTEGER NOT NULL,
          texte           TEXT,
          texte_recherche TEXT NOT NULL,
          rtf             BLOB,
          html            BLOB,
          image_fichier   TEXT,
          miniature       BLOB,
          empreinte       BLOB NOT NULL,
          application     TEXT,
          date_copie      TEXT NOT NULL,
          date_usage      TEXT NOT NULL,
          taille          INTEGER NOT NULL
        );

        CREATE VIRTUAL TABLE IF NOT EXISTS element_fts USING fts5(
          texte_recherche, content='element', content_rowid='id', tokenize='trigram'
        );

        CREATE TRIGGER IF NOT EXISTS element_ai AFTER INSERT ON element BEGIN
          INSERT INTO element_fts(rowid, texte_recherche) VALUES (new.id, new.texte_recherche);
        END;

        CREATE TRIGGER IF NOT EXISTS element_ad AFTER DELETE ON element BEGIN
          INSERT INTO element_fts(element_fts, rowid, texte_recherche) VALUES ('delete', old.id, old.texte_recherche);
        END;

        CREATE TRIGGER IF NOT EXISTS element_au AFTER UPDATE ON element BEGIN
          INSERT INTO element_fts(element_fts, rowid, texte_recherche) VALUES ('delete', old.id, old.texte_recherche);
          INSERT INTO element_fts(rowid, texte_recherche) VALUES (new.id, new.texte_recherche);
        END;

        CREATE TABLE IF NOT EXISTS meta (cle TEXT PRIMARY KEY, valeur TEXT);
        """;
}
```

`src/PressePapiers/App.xaml`

```xml
<Application
    x:Class="PressePapiers.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

`src/PressePapiers/App.xaml.cs`

```csharp
using Microsoft.UI.Xaml;

namespace PressePapiers;

/// <summary>
/// Application WinUI 3. Version minimale : elle se termine dès son lancement ;
/// le cycle de vie complet (journal, base, fenêtre, icône) vient avec la tâche 16, l'assemblage avec la tâche 29.
/// </summary>
public sealed partial class App : Application
{
    /// <summary>Charge les ressources XAML de l'application.</summary>
    public App()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Exit();
    }
}
```

`src/PressePapiers/Capture/ApplicationOrigine.cs`

```csharp
namespace PressePapiers.Capture;

/// <summary>Nom du processus d'où vient une copie, indication au mieux. Coquille : remplacée par la tâche 21.</summary>
public static class ApplicationOrigine
{
    /// <summary>Nom du processus de la fenêtre (ApplicationFrameHost résolu) ; null si inconnu.</summary>
    public static string? Determiner(nint fenetre) => throw new NotImplementedException("Tâche 21");
}
```

`src/PressePapiers/Capture/EcrivainPressePapiers.cs`

```csharp
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Capture;

/// <summary>Écriture d'un élément dans le presse-papiers. Coquille : remplacée par la tâche 22.</summary>
public sealed class EcrivainPressePapiers
{
    /// <summary>Crée l'écrivain ; <paramref name="hwndRelais"/> est la fenêtre qui ouvre le presse-papiers.</summary>
    public EcrivainPressePapiers(nint hwndRelais, IJournal journal) => throw new NotImplementedException("Tâche 22");

    /// <summary>Écrit l'élément dans le mode demandé, en posant toujours la marque interne.</summary>
    public Task<bool> EcrireAsync(Element element, ModeCollage mode, string? cheminImage) =>
        throw new NotImplementedException("Tâche 22");
}
```

`src/PressePapiers/Capture/LecteurPressePapiers.cs`

```csharp
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Capture;

/// <summary>Lecture du presse-papiers à chaque copie. Coquille : remplacée par la tâche 21.</summary>
public sealed class LecteurPressePapiers
{
    /// <summary>Crée le lecteur ; <paramref name="hwndRelais"/> est la fenêtre qui ouvre le presse-papiers.</summary>
    public LecteurPressePapiers(nint hwndRelais, IJournal journal) => throw new NotImplementedException("Tâche 21");

    /// <summary>
    /// Lit le contenu : 10 essais espacés de 25 ms ; null si la marque interne est présente, si le contenu est illisible ou vide.
    /// Les formats sont lus dans l'ordre de la spec, section 4.
    /// </summary>
    public Task<ContenuCopie?> LireAsync(string? application) => throw new NotImplementedException("Tâche 21");
}
```

`src/PressePapiers/Capture/MarqueInterne.cs`

```csharp
namespace PressePapiers.Capture;

/// <summary>Format privé que l'outil pose sur chacune de ses écritures pour ignorer ses propres copies. Coquille : tâche 21.</summary>
public static class MarqueInterne
{
    /// <summary>Nom du format enregistré.</summary>
    public const string NomFormat = "PressePapiers.Interne";

    /// <summary>Identifiant du format (RegisterClipboardFormat), vérifié non nul.</summary>
    public static uint Format => throw new NotImplementedException("Tâche 21");
}
```

`src/PressePapiers/Capture/ServiceCapture.cs`

```csharp
#pragma warning disable CS0067 // Coquille : l'événement est levé par la tâche 23.
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Systeme;

namespace PressePapiers.Capture;

/// <summary>Enregistre chaque copie détectée dans l'historique. Coquille : remplacée par la tâche 23.</summary>
public sealed class ServiceCapture : IDisposable
{
    /// <summary>Branche la capture sur le relais.</summary>
    public ServiceCapture(RelaisWindows relais, LecteurPressePapiers lecteur, DepotHistorique depot,
                          Func<Parametres> parametres, IJournal journal) =>
        throw new NotImplementedException("Tâche 23");

    /// <summary>Levé après l'ajout (ou la remontée) d'un élément.</summary>
    public event Action<Element>? ElementAjoute;

    /// <inheritdoc />
    public void Dispose() => throw new NotImplementedException("Tâche 23");
}
```

`src/PressePapiers/Capture/ServiceRecollage.cs`

```csharp
using PressePapiers.Coeur.Collage;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Capture;

/// <summary>Remet un élément dans le presse-papiers et le colle dans la fenêtre d'origine. Coquille : tâche 24.</summary>
public sealed class ServiceRecollage
{
    /// <summary>Crée le service de recollage.</summary>
    public ServiceRecollage(EcrivainPressePapiers ecrivain, DepotHistorique depot, IJournal journal) =>
        throw new NotImplementedException("Tâche 24");

    /// <summary>
    /// Spec, section 10.2 : écriture, contrôle de la fenêtre et de son niveau, premier plan, attente des modificateurs, Ctrl+V.
    /// </summary>
    public Task<IssueCollage> CollerAsync(Element element, ModeCollage mode, nint fenetreOrigine, bool copieSeule,
                                          nint hwndOutil, Action masquerFenetre) =>
        throw new NotImplementedException("Tâche 24");
}
```

`src/PressePapiers/Interface/FenetrePrincipale.cs`

```csharp
using Microsoft.UI.Xaml;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Interface;

/// <summary>
/// Fenêtre principale (proposition B de la maquette). Coquille sans XAML : le contenu vient avec les tâches 25 à 28.
/// </summary>
public sealed partial class FenetrePrincipale : Window
{
    /// <summary>Crée la fenêtre, masquée, sur le modèle de vue.</summary>
    public FenetrePrincipale(FenetrePrincipaleVue vue) => throw new NotImplementedException("Tâche 25");

    /// <summary>Handle Win32 de la fenêtre.</summary>
    public nint Handle => throw new NotImplementedException("Tâche 25");

    /// <summary>Vrai tant que la fenêtre est affichée.</summary>
    public bool EstVisible => throw new NotImplementedException("Tâche 25");

    /// <summary>Affiche la fenêtre (AppWindow.Show puis PremierPlan.Amener) en notant la fenêtre d'origine du collage.</summary>
    public void AfficherPour(nint fenetreOrigine, OrigineOuverture origine) => throw new NotImplementedException("Tâche 27");

    /// <summary>Ouvre la fenêtre sur la page Paramètres.</summary>
    public void AfficherParametres() => throw new NotImplementedException("Tâche 28");

    /// <summary>Masque la fenêtre sans terminer l'application.</summary>
    public void Masquer() => throw new NotImplementedException("Tâche 27");
}
```

`src/PressePapiers/Interface/FenetrePrincipaleVue.cs`

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using PressePapiers.Capture;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Recherche;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Interface;

/// <summary>Modèle de vue de la fenêtre principale. Coquille : complétée par les tâches 25 à 28.</summary>
public sealed partial class FenetrePrincipaleVue : ObservableObject
{
    /// <summary>Crée le modèle de vue.</summary>
    public FenetrePrincipaleVue(DepotHistorique depot, MoteurRecherche recherche, ServiceRecollage recollage,
                                ServiceParametres parametres, IJournal journal) =>
        throw new NotImplementedException("Tâche 25");

    /// <summary>Texte d'état du hook Win+V affiché dans les Paramètres, fourni par l'application.</summary>
    [ObservableProperty]
    public partial string EtatCrochet { get; set; }
}
```

`src/PressePapiers/PressePapiers.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <RootNamespace>PressePapiers</RootNamespace>
    <AssemblyName>PressePapiers</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Platforms>x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <UseWinUI>true</UseWinUI>
    <EnableMsixTooling>true</EnableMsixTooling>
    <!-- Application non empaquetée et autonome : un dossier à copier, sans installation ni droits administrateur. -->
    <WindowsPackageType>None</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <SelfContained>true</SelfContained>
    <!-- Main personnalisé (Program.cs) : instance unique avant toute création de fenêtre. -->
    <DefineConstants>$(DefineConstants);DISABLE_XAML_GENERATED_MAIN</DefineConstants>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" />
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" />
    <PackageReference Include="CommunityToolkit.Mvvm" />
    <PackageReference Include="Serilog" />
    <PackageReference Include="Serilog.Sinks.File" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\PressePapiers.Coeur\PressePapiers.Coeur.csproj" />
  </ItemGroup>

</Project>
```

`src/PressePapiers/Program.cs`

```csharp
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace PressePapiers;

/// <summary>Point d'entrée personnalisé (symbole DISABLE_XAML_GENERATED_MAIN) : la tâche 16 y ajoutera l'instance unique.</summary>
public static class Program
{
    /// <summary>Démarre l'application WinUI 3 sur un fil à appartement unique.</summary>
    [STAThread]
    public static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(parametres =>
        {
            // Sans contexte de synchronisation, les await de l'interface reprendraient sur un fil du pool.
            var contexte = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(contexte);
            _ = new App();
        });
    }
}
```

`src/PressePapiers/Systeme/CrochetWinV.cs`

```csharp
#pragma warning disable CS0067 // Coquille : l'événement est levé par la tâche 20.
using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Systeme;

/// <summary>Hook clavier bas niveau (WH_KEYBOARD_LL) sur un fil dédié. Coquille : remplacée par la tâche 20.</summary>
public sealed class CrochetWinV : ICrochetWinV
{
    /// <summary>Crée le hook, sans le démarrer.</summary>
    public CrochetWinV(IJournal journal) => throw new NotImplementedException("Tâche 20");

    /// <inheritdoc />
    public bool EstActif => throw new NotImplementedException("Tâche 20");

    /// <inheritdoc />
    public string? RaisonInactif => throw new NotImplementedException("Tâche 20");

    /// <inheritdoc />
    public event Action<nint>? WinVPresse;

    /// <inheritdoc />
    public void Demarrer() => throw new NotImplementedException("Tâche 20");

    /// <inheritdoc />
    public void Reinstaller() => throw new NotImplementedException("Tâche 20");

    /// <inheritdoc />
    public void Dispose() => throw new NotImplementedException("Tâche 20");
}
```

`src/PressePapiers/Systeme/DemarrageWindows.cs`

```csharp
namespace PressePapiers.Systeme;

/// <summary>Lancement avec Windows par la clé Run de l'utilisateur. Coquille : remplacée par la tâche 16.</summary>
public static class DemarrageWindows
{
    /// <summary>Vrai si la valeur « PressePapiers » existe dans HKCU\Software\Microsoft\Windows\CurrentVersion\Run.</summary>
    public static bool EstActive() => throw new NotImplementedException("Tâche 16");

    /// <summary>Crée ou supprime la valeur « PressePapiers » de la clé Run.</summary>
    public static void Appliquer(bool activer, string cheminExecutable) => throw new NotImplementedException("Tâche 16");
}
```

`src/PressePapiers/Systeme/ICrochetWinV.cs`

```csharp
namespace PressePapiers.Systeme;

/// <summary>Hook clavier qui reprend Win+V, isolé derrière une interface pour pouvoir être remplacé.</summary>
public interface ICrochetWinV : IDisposable
{
    /// <summary>Vrai tant que le hook est posé.</summary>
    bool EstActif { get; }

    /// <summary>Raison de l'inactivité, affichée dans les Paramètres ; null si le hook est actif.</summary>
    string? RaisonInactif { get; }

    /// <summary>Levé sur le fil du hook ; l'argument est la fenêtre au premier plan.</summary>
    event Action<nint>? WinVPresse;

    /// <summary>Démarre le fil du hook.</summary>
    void Demarrer();

    /// <summary>Pose un nouveau hook avant de retirer l'ancien (spec, section 11).</summary>
    void Reinstaller();
}
```

`src/PressePapiers/Systeme/IconeNotification.cs`

```csharp
#pragma warning disable CS0067 // Coquille : les événements sont levés par la tâche 18.
using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Systeme;

/// <summary>Icône de la zone de notification et son menu natif. Coquille : remplacée par la tâche 18.</summary>
public sealed class IconeNotification : IDisposable
{
    /// <summary>Crée l'icône, qui reçoit ses messages par le relais.</summary>
    public IconeNotification(RelaisWindows relais, IJournal journal) => throw new NotImplementedException("Tâche 18");

    /// <summary>Clic sur l'icône ou commande « Ouvrir ».</summary>
    public event Action? OuvrirDemande;

    /// <summary>Commande « Paramètres ».</summary>
    public event Action? ParametresDemandes;

    /// <summary>Commande « Quitter ».</summary>
    public event Action? QuitterDemande;

    /// <summary>Affiche l'icône (NIM_ADD la première fois, NIM_MODIFY ensuite), en aspect d'alerte si demandé.</summary>
    public void Afficher(bool alerte, string infobulle) => throw new NotImplementedException("Tâche 18");

    /// <summary>Affiche une bulle de notification (NIF_INFO).</summary>
    public void Notifier(string titre, string message) => throw new NotImplementedException("Tâche 18");

    /// <inheritdoc />
    public void Dispose() => throw new NotImplementedException("Tâche 18");
}
```

`src/PressePapiers/Systeme/JournalSerilog.cs`

```csharp
using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Systeme;

/// <summary>Journal fichier par Serilog, un fichier par jour, 14 gardés. Coquille : remplacée par la tâche 16.</summary>
public sealed class JournalSerilog : IJournal, IDisposable
{
    /// <summary>Crée le journal dans un dossier.</summary>
    public JournalSerilog(string dossierJournal) => throw new NotImplementedException("Tâche 16");

    /// <inheritdoc />
    public void Info(string message) => throw new NotImplementedException("Tâche 16");

    /// <inheritdoc />
    public void Avertissement(string message, Exception? exception = null) => throw new NotImplementedException("Tâche 16");

    /// <inheritdoc />
    public void Erreur(string message, Exception? exception = null) => throw new NotImplementedException("Tâche 16");

    /// <inheritdoc />
    public void Dispose() => throw new NotImplementedException("Tâche 16");
}
```

`src/PressePapiers/Systeme/NiveauIntegrite.cs`

```csharp
namespace PressePapiers.Systeme;

/// <summary>Niveau d'intégrité des processus (spec, section 10.3). Coquille : remplacée par la tâche 19.</summary>
public static class NiveauIntegrite
{
    /// <summary>RID du niveau d'intégrité du processus d'une fenêtre ; null si une étape de la lecture échoue.</summary>
    public static int? DeFenetre(nint hwnd) => throw new NotImplementedException("Tâche 19");

    /// <summary>RID du niveau d'intégrité du processus courant.</summary>
    public static int DuProcessusCourant() => throw new NotImplementedException("Tâche 19");
}
```

`src/PressePapiers/Systeme/PremierPlan.cs`

```csharp
namespace PressePapiers.Systeme;

/// <summary>Amène une fenêtre au premier plan (spec, section 10.1). Coquille : remplacée par la tâche 19.</summary>
public static class PremierPlan
{
    /// <summary>Restaure la fenêtre si besoin puis la met au premier plan ; renvoie faux si Windows refuse.</summary>
    public static bool Amener(nint hwnd) => throw new NotImplementedException("Tâche 19");

    /// <summary>Fenêtre actuellement au premier plan.</summary>
    public static nint FenetreActive() => throw new NotImplementedException("Tâche 19");

    /// <summary>Vrai si la fenêtre peut servir de fenêtre d'origine : ni l'outil, ni le bureau, ni la barre des tâches.</summary>
    public static bool EstOrigineValide(nint hwnd, nint hwndOutil) => throw new NotImplementedException("Tâche 19");
}
```

`src/PressePapiers/Systeme/RelaisWindows.cs`

```csharp
#pragma warning disable CS0067 // Coquille : les événements sont levés par la tâche 17.
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Reglages;

namespace PressePapiers.Systeme;

/// <summary>
/// Fenêtre Win32 de premier niveau invisible, sur le fil d'interface : reçoit les copies, le raccourci de secours,
/// les messages de l'icône et les changements de session. Coquille : remplacée par la tâche 17.
/// </summary>
public sealed class RelaisWindows : IDisposable
{
    /// <summary>Message de rappel de l'icône de notification (WM_APP + 1).</summary>
    public const uint MessageIcone = 0x8001;

    /// <summary>Crée la fenêtre sur le fil courant, qui doit être le fil d'interface.</summary>
    public RelaisWindows(IJournal journal) => throw new NotImplementedException("Tâche 17");

    /// <summary>Handle de la fenêtre relais.</summary>
    public nint Handle => throw new NotImplementedException("Tâche 17");

    /// <summary>WM_CLIPBOARDUPDATE.</summary>
    public event Action? CopieDetectee;

    /// <summary>WM_HOTKEY, identifiant 1.</summary>
    public event Action? RaccourciSecoursPresse;

    /// <summary>Message de l'icône ; l'argument est le LOWORD de lParam.</summary>
    public event Action<uint>? MessageIconeRecu;

    /// <summary>Message enregistré « TaskbarCreated » : l'Explorateur a redémarré.</summary>
    public event Action? BarreDesTachesRecreee;

    /// <summary>WM_WTSSESSION_CHANGE avec WTS_SESSION_UNLOCK.</summary>
    public event Action? SessionDeverrouillee;

    /// <summary>WM_POWERBROADCAST avec PBT_APMRESUMEAUTOMATIC.</summary>
    public event Action? SortieDeVeille;

    /// <summary>Commence à écouter le presse-papiers (AddClipboardFormatListener) ; faux en cas d'échec.</summary>
    public bool EcouterPressePapiers() => throw new NotImplementedException("Tâche 17");

    /// <summary>Enregistre le raccourci de secours ; renvoie faux avec le code d'erreur Windows en cas d'échec.</summary>
    public bool EnregistrerRaccourci(Raccourci raccourci, out int codeErreur) => throw new NotImplementedException("Tâche 17");

    /// <summary>Retire le raccourci de secours.</summary>
    public void RetirerRaccourci() => throw new NotImplementedException("Tâche 17");

    /// <inheritdoc />
    public void Dispose() => throw new NotImplementedException("Tâche 17");
}
```

`src/PressePapiers/Systeme/SourceSessionWts.cs`

```csharp
using PressePapiers.Coeur.Session;

namespace PressePapiers.Systeme;

/// <summary>Repère de session lu par WTSQuerySessionInformation. Coquille : remplacée par la tâche 16.</summary>
public sealed class SourceSessionWts : ISourceSession
{
    /// <inheritdoc />
    public long? LireHeureOuverture() => throw new NotImplementedException("Tâche 16");
}
```

`src/PressePapiers/app.manifest`

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="PressePapiers.app"/>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <!-- Windows 10 et 11. -->
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

- [ ] **Étape 4 : Compiler et lancer tous les tests**

```bash
dotnet build PressePapiers.slnx -c Debug
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : la compilation finit par `0 Avertissement(s)` et `0 Erreur(s)` pour les trois projets (Coeur, application
et tests) ; les tests finissent par :

```
Résumé de série de tests : Réussite!
  total : 25
  échec : 0
  réussie : 25
  ignoré : 0
```

Les 25 tests : `BaseHistoriqueTests` 18, `LecteurElementTests` 2, `ElementTests` 5.

- [ ] **Étape 5 : Vérifier sur le poste**

1. L'application minimale démarre, se termine d'elle-même et ne laisse aucun processus (PowerShell, à la racine du dépôt) :

```powershell
$exe = "src\PressePapiers\bin\Debug\net10.0-windows10.0.19041.0\win-x64\PressePapiers.exe"
$p = Start-Process -FilePath $exe -PassThru
if ($p.WaitForExit(20000)) { "Terminé, code de sortie : $($p.ExitCode)" } else { $p.Kill(); "Toujours en cours après 20 s : échec" }
(Get-Process PressePapiers -ErrorAction SilentlyContinue | Measure-Object).Count
```

À observer : `Terminé, code de sortie : 0`, puis `0` ; aucune fenêtre ne s'affiche.

2. Point 8 de la section 16 de la spec : la version réelle de SQLite embarquée dans Microsoft.Data.Sqlite 10.0.12 :

```bash
tests/PressePapiers.Tests/bin/Debug/net10.0-windows10.0.19041.0/PressePapiers.Tests.exe -diagnostics -method "*version_de_sqlite*"
```

À observer : une ligne `Version de SQLite embarquée : 3.53.3` (valeur relevée au 13/10/2026, au moins 3.34.0 est exigé), ce
qui confirme aussi que FTS5 et le tokenizer `trigram` fonctionnent (le test d'index passe).

- [ ] **Étape 6 : Committer**

```bash
git add PressePapiers.slnx global.json Directory.Build.props Directory.Packages.props src tests
git commit -m "Poser le socle : solution, paquets, modèle, base SQLite et coquilles"
```
