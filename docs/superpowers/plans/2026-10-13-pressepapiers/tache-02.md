### Tâche 2 : DossierDonnees, Empreinte, Normalisation

**Fichiers :**
- Modifier : `src/PressePapiers.Coeur/Recherche/Normalisation.cs`
- Modifier : `src/PressePapiers.Coeur/Stockage/DossierDonnees.cs`
- Modifier : `src/PressePapiers.Coeur/Stockage/Empreinte.cs`
- Créer : aucun
- Tests : `tests/PressePapiers.Tests/Recherche/NormalisationTests.cs`
- Tests : `tests/PressePapiers.Tests/Stockage/DossierDonneesTests.cs`
- Tests : `tests/PressePapiers.Tests/Stockage/EmpreinteTests.cs`

**Interfaces :**
- Utilise (tâche 1) : `ContenuCopie`, `TypeElement` (`PressePapiers.Coeur.Modele`) ; les coquilles `DossierDonnees`,
  `Empreinte`, `Normalisation` ; l'assistant de test `PressePapiers.Tests.Outils.DossierTemporaire`.
- Fournit :
  - `public sealed record DossierDonnees(string Racine)` avec `string Base` (`Racine\historique.db`), `string Images`
    (`Racine\images`), `string Sauvegardes` (`Racine\sauvegardes`), `string Journal` (`Racine\journal`),
    `string Parametres` (`Racine\parametres.json`), `static DossierDonnees ParDefaut()`, `void CreerDossiers()`
    (crée `images`, `sauvegardes` et `journal`, donc la racine ; sans effet sur l'existant) ; interne :
    `static DossierDonnees ParDefaut(Func<string, string?> lireVariable, string dossierDonneesLocales)`.
    `ParDefaut()` lit `PRESSEPAPIERS_DONNEES` (chemin rendu absolu), à défaut `%LOCALAPPDATA%\PressePapiers`.
  - `public static byte[] Empreinte.Calculer(ContenuCopie contenu)` : SHA-256 ; lève `ArgumentException` si le contenu
    principal du type est absent (`Texte` pour Texte et MisEnForme, `ImagePng` pour Image, `Fichiers` pour Fichiers).
  - `public static string Normalisation.Normaliser(string? texte)` : jamais null, jamais d'exception (un substitut UTF-16
    isolé devient U+FFFD) ; `public static string Normalisation.TexteRecherche(string? nom, string? texte,
    IReadOnlyList<string>? fichiers, string? application)` : parties réunies dans l'ordre nom, texte, chemins, application.

Remarques pour les tâches suivantes :
- L'empreinte ne tient compte ni du type ni de la mise en forme : un texte "C:\a.txt" copié comme texte et le fichier
  `C:\a.txt` copié dans l'Explorateur ont la même empreinte. La tâche 5 (dédoublonnage) doit donc comparer aussi `Type`
  si elle veut les distinguer.
- Les ligatures traitées sont œ et æ (spec, section 7) ; ß, ø, đ, ł ne sont pas ramenés.

- [ ] **Étape 1 : Écrire le test qui échoue**

`tests/PressePapiers.Tests/Recherche/NormalisationTests.cs`

```csharp
using System.Diagnostics;
using PressePapiers.Coeur.Recherche;

namespace PressePapiers.Tests.Recherche;

public sealed class NormalisationTests
{
    [Theory]
    [InlineData("Éàçü", "eacu")]
    [InlineData("ÉCOLE", "ecole")]
    [InlineData("naïve  café", "naive cafe")]
    [InlineData("Ça va", "ca va")]
    [InlineData("cœur", "coeur")]
    [InlineData("Œuvre", "oeuvre")]
    [InlineData("ŒUF", "oeuf")]
    [InlineData("Æon", "aeon")]
    [InlineData("æther", "aether")]
    [InlineData("e\u0301", "e")]
    [InlineData("  a \t\r\n b  ", "a b")]
    [InlineData("\u00A0a\u2003b", "a b")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("MAJUSCULES et minuscules", "majuscules et minuscules")]
    [InlineData("a😀b", "a😀b")]
    [InlineData("100 % sûr_", "100 % sur_")]
    public void Normaliser_ramene_casse_accents_ligatures_et_espaces(string entree, string attendu) =>
        Assert.Equal(attendu, Normalisation.Normaliser(entree));

    [Fact]
    public void Normaliser_rend_une_chaine_vide_pour_null() =>
        Assert.Equal("", Normalisation.Normaliser(null));

    [Fact]
    public void Normaliser_remplace_un_substitut_isole_au_lieu_de_lever_une_exception()
    {
        // Un presse-papiers peut contenir un demi-emoji : la saisie hostile ne doit jamais faire planter la capture.
        var resultat = Normalisation.Normaliser("a\uD83Db\uDE00c");

        Assert.Equal("a\uFFFDb\uFFFDc", resultat);
    }

    [Theory]
    [InlineData("Éàçü  Œuvre")]
    [InlineData("Déjà   vu\n\tà bientôt")]
    [InlineData("déjà normalisé")]
    public void Normaliser_est_idempotent(string entree)
    {
        var une = Normalisation.Normaliser(entree);

        Assert.Equal(une, Normalisation.Normaliser(une));
    }

    [Fact]
    public void TexteRecherche_reunit_nom_texte_fichiers_et_application_dans_cet_ordre()
    {
        var resultat = Normalisation.TexteRecherche(
            "Mon Café",
            "Ligne\nDeux",
            [@"C:\Dossier\Éé.txt", @"D:\Autre.doc"],
            "WindowsTerminal");

        Assert.Equal(@"mon cafe ligne deux c:\dossier\ee.txt d:\autre.doc windowsterminal", resultat);
    }

    [Fact]
    public void TexteRecherche_ignore_les_parties_absentes()
    {
        Assert.Equal("", Normalisation.TexteRecherche(null, null, null, null));
        Assert.Equal("", Normalisation.TexteRecherche("", "  ", [], ""));
        Assert.Equal("seulement le nom", Normalisation.TexteRecherche("Seulement le NOM", null, null, null));
        Assert.Equal("texte code", Normalisation.TexteRecherche(null, "Texte", null, "Code"));
    }

    [Fact]
    public void TexteRecherche_donne_le_meme_texte_que_Normaliser_de_la_concatenation()
    {
        var attendu = Normalisation.Normaliser("Nom Œuvre texte Été chemin Application");

        Assert.Equal(attendu, Normalisation.TexteRecherche("Nom Œuvre", "texte Été", ["chemin"], "Application"));
    }

    [Fact]
    public void Un_texte_de_9_Mo_se_normalise_en_moins_d_une_seconde()
    {
        // Échauffement : le premier appel paie la compilation à la volée, pas le calcul.
        Normalisation.Normaliser("chauffe é");
        Normalisation.TexteRecherche("n", "chauffe é", ["f"], "a");

        var accentue = Repeter("Été à l'œuvre, déjà ", 9 * 1024 * 1024);
        var simple = Repeter("Bonjour le monde ", 9 * 1024 * 1024);

        var chrono = Stopwatch.StartNew();
        var normaliseAccentue = Normalisation.Normaliser(accentue);
        var dureeAccentue = chrono.Elapsed;

        chrono.Restart();
        var normaliseSimple = Normalisation.Normaliser(simple);
        var dureeSimple = chrono.Elapsed;

        chrono.Restart();
        var recherche = Normalisation.TexteRecherche("nom", accentue, [@"C:\a.txt"], "Code");
        var dureeRecherche = chrono.Elapsed;

        TestContext.Current.SendDiagnosticMessage(
            $"9 Mo : accentué {dureeAccentue.TotalMilliseconds:F0} ms, simple {dureeSimple.TotalMilliseconds:F0} ms, " +
            $"TexteRecherche {dureeRecherche.TotalMilliseconds:F0} ms");

        Assert.StartsWith("ete a l'oeuvre, deja ete", normaliseAccentue);
        Assert.StartsWith("bonjour le monde bonjour", normaliseSimple);
        Assert.StartsWith("nom ete a l'oeuvre", recherche);
        Assert.True(dureeAccentue < TimeSpan.FromSeconds(1), $"Texte accentué de 9 Mo : {dureeAccentue.TotalMilliseconds:F0} ms");
        Assert.True(dureeSimple < TimeSpan.FromSeconds(1), $"Texte simple de 9 Mo : {dureeSimple.TotalMilliseconds:F0} ms");
        Assert.True(dureeRecherche < TimeSpan.FromSeconds(1), $"TexteRecherche de 9 Mo : {dureeRecherche.TotalMilliseconds:F0} ms");
    }

    // Texte de longueur donnée fait de la répétition d'un motif.
    private static string Repeter(string motif, int longueur) =>
        string.Concat(Enumerable.Repeat(motif, (longueur / motif.Length) + 1))[..longueur];
}
```

`tests/PressePapiers.Tests/Stockage/DossierDonneesTests.cs`

```csharp
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
```

`tests/PressePapiers.Tests/Stockage/EmpreinteTests.cs`

```csharp
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
```

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : la compilation réussit (les coquilles ont les bonnes signatures) et les 47 nouveaux tests échouent avec
`System.NotImplementedException : Tâche 2` ; les 25 tests de la tâche 1 restent verts :

```
Résumé de série de tests : Échec!
  total : 72
  échec : 47
  réussie : 25
```

- [ ] **Étape 3 : Implémenter**

`src/PressePapiers.Coeur/Recherche/Normalisation.cs`

```csharp
using System.Globalization;
using System.Text;

namespace PressePapiers.Coeur.Recherche;

/// <summary>Normalisation du texte de recherche, partagée entre l'écriture de l'index et la requête.</summary>
public static class Normalisation
{
    /// <summary>
    /// Minuscules, accents retirés (décomposition Unicode), ligatures ramenées (œ en oe, æ en ae), espaces réduits à un seul.
    /// Un substitut UTF-16 isolé (un demi-emoji copié) est remplacé par U+FFFD au lieu de lever une exception.
    /// </summary>
    public static string Normaliser(string? texte)
    {
        if (string.IsNullOrEmpty(texte))
        {
            return "";
        }

        var decompose = Decomposer(texte.ToLowerInvariant());
        var resultat = new StringBuilder(decompose.Length);
        var dernierEstEspace = true; // vrai au départ : les espaces de tête sont supprimés

        foreach (var c in decompose)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!dernierEstEspace)
                {
                    resultat.Append(' ');
                    dernierEstEspace = true;
                }

                continue;
            }

            // Les accents sont des marques séparées après la décomposition : on les écarte.
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            switch (c)
            {
                case 'œ':
                    resultat.Append("oe");
                    break;
                case 'æ':
                    resultat.Append("ae");
                    break;
                default:
                    resultat.Append(c);
                    break;
            }

            dernierEstEspace = false;
        }

        // Au plus un espace final, issu d'un séparateur en fin de texte.
        if (resultat.Length > 0 && resultat[^1] == ' ')
        {
            resultat.Length--;
        }

        return resultat.ToString();
    }

    /// <summary>
    /// Texte normalisé qui réunit, dans cet ordre, le nom, le texte, les chemins de fichiers et l'application d'origine.
    /// Les parties absentes ou vides sont ignorées.
    /// </summary>
    public static string TexteRecherche(string? nom, string? texte, IReadOnlyList<string>? fichiers, string? application)
    {
        var parties = new List<string>(4);
        Ajouter(parties, nom);
        Ajouter(parties, texte);
        if (fichiers is { Count: > 0 })
        {
            Ajouter(parties, string.Join(' ', fichiers));
        }

        Ajouter(parties, application);

        // Une seule normalisation de l'ensemble : le texte copié est la partie volumineuse.
        return Normaliser(string.Join(' ', parties));
    }

    private static void Ajouter(List<string> parties, string? partie)
    {
        if (!string.IsNullOrWhiteSpace(partie))
        {
            parties.Add(partie);
        }
    }

    // Normalize lève ArgumentException devant un substitut isolé : on le remplace d'abord, puis on réessaie.
    private static string Decomposer(string texte)
    {
        try
        {
            return texte.Normalize(NormalizationForm.FormD);
        }
        catch (ArgumentException)
        {
            return RemplacerSubstitutsIsoles(texte).Normalize(NormalizationForm.FormD);
        }
    }

    private static string RemplacerSubstitutsIsoles(string texte)
    {
        var resultat = new StringBuilder(texte.Length);
        for (var i = 0; i < texte.Length; i++)
        {
            var c = texte[i];
            if (char.IsHighSurrogate(c) && i + 1 < texte.Length && char.IsLowSurrogate(texte[i + 1]))
            {
                resultat.Append(c).Append(texte[++i]);
            }
            else if (char.IsSurrogate(c))
            {
                resultat.Append('�');
            }
            else
            {
                resultat.Append(c);
            }
        }

        return resultat.ToString();
    }
}
```

`src/PressePapiers.Coeur/Stockage/DossierDonnees.cs`

```csharp
namespace PressePapiers.Coeur.Stockage;

/// <summary>Emplacements des données de l'application, tous dérivés d'un dossier racine.</summary>
/// <param name="Racine">Dossier racine des données.</param>
public sealed record DossierDonnees(string Racine)
{
    /// <summary>Nom de la variable d'environnement qui redirige les données (essais et recette).</summary>
    private const string VariableDonnees = "PRESSEPAPIERS_DONNEES";

    /// <summary>Fichier de la base : <c>Racine\historique.db</c>.</summary>
    public string Base => Path.Combine(Racine, "historique.db");

    /// <summary>Dossier des images PNG : <c>Racine\images</c>.</summary>
    public string Images => Path.Combine(Racine, "images");

    /// <summary>Dossier des copies de sécurité : <c>Racine\sauvegardes</c>.</summary>
    public string Sauvegardes => Path.Combine(Racine, "sauvegardes");

    /// <summary>Dossier du journal : <c>Racine\journal</c>.</summary>
    public string Journal => Path.Combine(Racine, "journal");

    /// <summary>Fichier des réglages : <c>Racine\parametres.json</c>.</summary>
    public string Parametres => Path.Combine(Racine, "parametres.json");

    /// <summary>
    /// <c>%LOCALAPPDATA%\PressePapiers</c>, ou le dossier de la variable d'environnement <c>PRESSEPAPIERS_DONNEES</c>
    /// si elle est définie (essais et recette sans toucher aux données réelles).
    /// </summary>
    public static DossierDonnees ParDefaut() =>
        ParDefaut(Environment.GetEnvironmentVariable,
                  Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    /// <summary>Variante testable de <see cref="ParDefaut()"/> : la lecture des variables d'environnement est injectée.</summary>
    internal static DossierDonnees ParDefaut(Func<string, string?> lireVariable, string dossierDonneesLocales)
    {
        var redirection = lireVariable(VariableDonnees);
        return string.IsNullOrWhiteSpace(redirection)
            ? new DossierDonnees(Path.Combine(dossierDonneesLocales, "PressePapiers"))
            : new DossierDonnees(Path.GetFullPath(redirection));
    }

    /// <summary>Crée la racine et les trois sous-dossiers s'ils n'existent pas ; sans effet sur l'existant.</summary>
    public void CreerDossiers()
    {
        Directory.CreateDirectory(Images);
        Directory.CreateDirectory(Sauvegardes);
        Directory.CreateDirectory(Journal);
    }
}
```

`src/PressePapiers.Coeur/Stockage/Empreinte.cs`

```csharp
using System.Security.Cryptography;
using System.Text;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Coeur.Stockage;

/// <summary>Empreinte du contenu principal, pour le dédoublonnage dans Temporaire.</summary>
public static class Empreinte
{
    /// <summary>
    /// SHA-256 : texte en UTF-8 (Texte, MisEnForme), octets du PNG (Image), chemins joints par un retour à la ligne (Fichiers).
    /// La mise en forme (RTF, HTML) n'entre pas dans l'empreinte : un même texte copié avec ou sans elle reste un seul élément.
    /// </summary>
    /// <param name="contenu">Contenu copié.</param>
    /// <exception cref="ArgumentException">Le contenu principal du type demandé est absent.</exception>
    public static byte[] Calculer(ContenuCopie contenu)
    {
        ArgumentNullException.ThrowIfNull(contenu);

        return contenu.Type switch
        {
            TypeElement.Texte or TypeElement.MisEnForme =>
                SHA256.HashData(Encoding.UTF8.GetBytes(Requis(contenu.Texte, "Texte"))),
            TypeElement.Image =>
                SHA256.HashData(Requis(contenu.ImagePng, "ImagePng")),
            TypeElement.Fichiers =>
                SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', Requis(contenu.Fichiers, "Fichiers")))),
            _ => throw new ArgumentException($"Type d'élément inconnu : {contenu.Type}.", nameof(contenu)),
        };
    }

    // Un contenu sans sa partie principale est un bogue de l'appelant : on le dit au lieu de hacher du vide.
    private static T Requis<T>(T? valeur, string nom) where T : class =>
        valeur ?? throw new ArgumentException($"Le contenu copié n'a pas de {nom}.", "contenu");
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
  total : 72
  échec : 0
  réussie : 72
  ignoré : 0
```

Les 47 nouveaux tests : `DossierDonneesTests` 9, `EmpreinteTests` 12, `NormalisationTests` 26.

- [ ] **Étape 5 : Vérifier sur le poste**

Point de vigilance 2 : relever les durées mesurées par les tests de gros contenus (texte de 9 Mo, image 3840 x 2160). Elles
doivent être très inférieures à la seconde exigée :

```bash
tests/PressePapiers.Tests/bin/Debug/net10.0-windows10.0.19041.0/PressePapiers.Tests.exe -diagnostics -method "*9_Mo*" -method "*4K*"
```

À observer, sur le poste de référence : `Empreinte : texte 9 Mo 43 ms, image 4K 22 ms` et
`9 Mo : accentué 189 ms, simple 101 ms, TexteRecherche 188 ms` (ordres de grandeur : environ 5 fois de marge).

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Recherche/Normalisation.cs src/PressePapiers.Coeur/Stockage/DossierDonnees.cs src/PressePapiers.Coeur/Stockage/Empreinte.cs tests/PressePapiers.Tests/Recherche/NormalisationTests.cs tests/PressePapiers.Tests/Stockage/DossierDonneesTests.cs tests/PressePapiers.Tests/Stockage/EmpreinteTests.cs
git commit -m "Ajouter DossierDonnees, Empreinte et Normalisation"
```
