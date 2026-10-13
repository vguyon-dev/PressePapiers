### Tâche 13 : FormatHtml

**Fichiers :**
- Modifier : `src/PressePapiers.Coeur/Formats/FormatHtml.cs` (remplace la coquille ; contenu complet à l'étape 3)
- Créer : aucun
- Tests : `tests/PressePapiers.Tests/Formats/FormatHtmlTests.cs`

**Interfaces :**
- Utilise (tâche 1) : la coquille `FormatHtml` (mêmes signatures).
- Fournit :
  - `public static byte[] Encoder(string fragmentHtml)` : octets UTF-8 du format « HTML Format », sans octet nul final
    (l'écrivain du presse-papiers, tâche 22, l'ajoute) ; lève `ArgumentNullException` pour `null` ;
  - `public static string? ExtraireFragment(ReadOnlySpan<byte> donnees)` : le fragment entre `StartFragment` et
    `EndFragment`, ou `null` si ces offsets manquent, sont illisibles, négatifs, inversés ou hors des données ; jamais
    d'exception ; accepte les fins de ligne LF, des lignes d'en-tête supplémentaires (`SourceURL`) et un octet nul final.
  - Consommateurs : aucun pour l'instant dans l'application (tâche 21 : `LecteurPressePapiers` garde les octets bruts dans
    `ContenuCopie.Html` ; tâche 22 : `EcrivainPressePapiers` les réécrit tels quels). `Encoder` et `ExtraireFragment`
    restent disponibles pour construire ou lire un fragment (tests, aperçu éventuel de la tâche 26).

Choix de format (spec, section 4) : en-tête `Version:1.0`, puis `StartHTML`, `EndHTML`, `StartFragment`, `EndFragment`
avec des offsets de **10 chiffres** (largeur fixe : la longueur de l'en-tête est connue avant de calculer les offsets),
lignes terminées par CRLF, offsets en **octets UTF-8** depuis le tout début des données, en-tête compris ; document
`<html><body><!--StartFragment-->fragment<!--EndFragment--></body></html>`.

- [ ] **Étape 1 : Écrire le test qui échoue**

`tests/PressePapiers.Tests/Formats/FormatHtmlTests.cs`

```csharp
using System.Text;
using PressePapiers.Coeur.Formats;

namespace PressePapiers.Tests.Formats;

public sealed class FormatHtmlTests
{
    // Lit la valeur numérique d'une ligne d'en-tête « Nom:0000000123 » dans les octets encodés.
    private static int Offset(byte[] donnees, string nom)
    {
        var texte = Encoding.UTF8.GetString(donnees);
        foreach (var ligne in texte.Split("\r\n"))
        {
            if (ligne.StartsWith(nom + ":", StringComparison.Ordinal))
            {
                return int.Parse(ligne[(nom.Length + 1)..]);
            }
        }

        throw new InvalidOperationException($"Ligne {nom} absente de l'en-tête.");
    }

    private static string Tranche(byte[] donnees, int debut, int fin) =>
        Encoding.UTF8.GetString(donnees, debut, fin - debut);

    [Theory]
    [InlineData("<b>bonjour</b>")]
    [InlineData("<p>Été à l'œuvre, naïveté : çà et là</p>")]
    [InlineData("<p>Fête 🎉 et 😀 : quatre octets chacun</p>")]
    [InlineData("<i>日本語</i>")]
    [InlineData("")]
    public void Les_offsets_sont_des_octets_utf8_depuis_le_debut_en_tete_compris(string fragment)
    {
        var donnees = FormatHtml.Encoder(fragment);

        var debutFragment = Offset(donnees, "StartFragment");
        var finFragment = Offset(donnees, "EndFragment");
        var debutHtml = Offset(donnees, "StartHTML");
        var finHtml = Offset(donnees, "EndHTML");

        Assert.Equal(fragment, Tranche(donnees, debutFragment, finFragment));
        Assert.StartsWith("<html>", Tranche(donnees, debutHtml, finHtml), StringComparison.Ordinal);
        Assert.EndsWith("</html>", Tranche(donnees, debutHtml, finHtml), StringComparison.Ordinal);
        Assert.Equal(donnees.Length, finHtml);
        Assert.Equal(Encoding.UTF8.GetByteCount(fragment), finFragment - debutFragment);
    }

    [Fact]
    public void Les_marqueurs_encadrent_exactement_le_fragment()
    {
        var donnees = FormatHtml.Encoder("<b>x</b>");

        var debut = Offset(donnees, "StartFragment");
        var fin = Offset(donnees, "EndFragment");

        Assert.Equal("<!--StartFragment-->", Tranche(donnees, debut - "<!--StartFragment-->".Length, debut));
        Assert.Equal("<!--EndFragment-->", Tranche(donnees, fin, fin + "<!--EndFragment-->".Length));
    }

    [Fact]
    public void L_en_tete_commence_par_la_version_et_chaque_ligne_finit_par_crlf()
    {
        var texte = Encoding.UTF8.GetString(FormatHtml.Encoder("<b>x</b>"));

        Assert.StartsWith("Version:1.0\r\nStartHTML:", texte, StringComparison.Ordinal);
        var finEnTete = texte.IndexOf("<html>", StringComparison.Ordinal);
        var enTete = texte[..finEnTete];
        Assert.EndsWith("\r\n", enTete, StringComparison.Ordinal);
        Assert.Equal(5, enTete.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        // Aucun saut de ligne isolé dans l'en-tête.
        Assert.Equal(enTete.Count(c => c == '\n'), enTete.Count(c => c == '\r'));
    }

    [Fact]
    public void Les_offsets_ont_une_largeur_fixe_de_dix_chiffres()
    {
        var texte = Encoding.UTF8.GetString(FormatHtml.Encoder("<b>x</b>"));

        Assert.Matches(@"StartHTML:\d{10}\r\n", texte);
        Assert.Matches(@"EndHTML:\d{10}\r\n", texte);
        Assert.Matches(@"StartFragment:\d{10}\r\n", texte);
        Assert.Matches(@"EndFragment:\d{10}\r\n", texte);
    }

    [Theory]
    [InlineData("<b>bonjour</b>")]
    [InlineData("<p>Été à l'œuvre 😀 日本語</p>")]
    [InlineData("")]
    public void Un_fragment_encode_puis_extrait_revient_identique(string fragment)
    {
        var extrait = FormatHtml.ExtraireFragment(FormatHtml.Encoder(fragment));

        Assert.Equal(fragment, extrait);
    }

    [Fact]
    public void Un_octet_nul_final_est_toleré_a_l_extraction()
    {
        // Le presse-papiers termine souvent les données par un octet nul.
        var donnees = FormatHtml.Encoder("<b>é</b>").Append((byte)0).ToArray();

        Assert.Equal("<b>é</b>", FormatHtml.ExtraireFragment(donnees));
    }

    [Fact]
    public void Un_format_produit_par_un_navigateur_est_extrait()
    {
        // Forme réelle : lignes supplémentaires (SourceURL) et offsets sur 10 chiffres, fins de ligne LF.
        const string corps = "<html>\n<body>\n<!--StartFragment--><span>Texte é</span><!--EndFragment-->\n</body>\n</html>";
        var entete = "Version:0.9\nStartHTML:{0}\nEndHTML:{1}\nStartFragment:{2}\nEndFragment:{3}\nSourceURL:https://exemple.fr/\n";
        var blanc = string.Format(entete, "0000000000", "0000000000", "0000000000", "0000000000");
        var debutHtml = Encoding.UTF8.GetByteCount(blanc);
        var debutFragment = debutHtml + Encoding.UTF8.GetByteCount("<html>\n<body>\n<!--StartFragment-->");
        var finFragment = debutFragment + Encoding.UTF8.GetByteCount("<span>Texte é</span>");
        var finHtml = debutHtml + Encoding.UTF8.GetByteCount(corps);
        var texte = string.Format(entete, debutHtml.ToString("D10"), finHtml.ToString("D10"),
            debutFragment.ToString("D10"), finFragment.ToString("D10")) + corps;

        var extrait = FormatHtml.ExtraireFragment(Encoding.UTF8.GetBytes(texte));

        Assert.Equal("<span>Texte é</span>", extrait);
    }

    [Fact]
    public void Un_ordre_debut_fin_inverse_est_refuse()
    {
        var donnees = FormatHtml.Encoder("<b>bonjour</b>");
        var debut = Offset(donnees, "StartFragment");
        var fin = Offset(donnees, "EndFragment");
        var texte = Encoding.UTF8.GetString(donnees)
            .Replace($"StartFragment:{debut:D10}", $"StartFragment:{fin:D10}", StringComparison.Ordinal)
            .Replace($"EndFragment:{fin:D10}", $"EndFragment:{debut:D10}", StringComparison.Ordinal);

        Assert.Null(FormatHtml.ExtraireFragment(Encoding.UTF8.GetBytes(texte)));
    }

    [Fact]
    public void Des_offsets_hors_des_donnees_sont_refuses()
    {
        var texte = Encoding.UTF8.GetString(FormatHtml.Encoder("<b>x</b>"))
            .Replace("EndFragment:", "EndFragment:9", StringComparison.Ordinal);

        Assert.Null(FormatHtml.ExtraireFragment(Encoding.UTF8.GetBytes(texte)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("n'importe quoi")]
    [InlineData("Version:1.0\r\nStartHTML:0000000000\r\n")]
    [InlineData("Version:1.0\r\nStartFragment:abc\r\nEndFragment:def\r\n")]
    [InlineData("Version:1.0\r\nStartFragment:-5\r\nEndFragment:10\r\n<html>")]
    public void Des_donnees_invalides_donnent_null_sans_exception(string texte)
    {
        Assert.Null(FormatHtml.ExtraireFragment(Encoding.UTF8.GetBytes(texte)));
    }

    [Fact]
    public void Un_fragment_null_est_refuse_a_l_encodage()
    {
        Assert.Throws<ArgumentNullException>(() => FormatHtml.Encoder(null!));
    }
}
```

Chaque test d'offset compare des **octets** : avec accents et emoji (4 octets en UTF-8, 2 unités en UTF-16), un calcul en
caractères donnerait un décalage et ferait échouer `Les_offsets_sont_des_octets_utf8_depuis_le_debut_en_tete_compris`.
`Un_ordre_debut_fin_inverse_est_refuse` échoue si l'extraction ne contrôle pas `fin >= debut`.

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : la compilation réussit (la coquille a les bonnes signatures) et les 21 nouveaux tests échouent avec
`System.NotImplementedException : Tâche 13` ; les 446 tests précédents restent verts :

```
Résumé de série de tests : Échec!
  total : 467
  échec : 21
  réussie : 446
```

- [ ] **Étape 3 : Implémenter**

`src/PressePapiers.Coeur/Formats/FormatHtml.cs`

```csharp
using System.Globalization;
using System.Text;

namespace PressePapiers.Coeur.Formats;

/// <summary>
/// Format « HTML Format » du presse-papiers : un en-tête texte (<c>Version</c>, <c>StartHTML</c>, <c>EndHTML</c>,
/// <c>StartFragment</c>, <c>EndFragment</c>) suivi d'un document HTML, le tout en UTF-8.
/// Les offsets sont des nombres d'octets depuis le tout début des données, en-tête compris.
/// </summary>
public static class FormatHtml
{
    private const string DebutMarqueur = "<!--StartFragment-->";
    private const string FinMarqueur = "<!--EndFragment-->";
    private const string Prefixe = "<html>\r\n<body>\r\n";
    private const string Suffixe = "\r\n</body>\r\n</html>";

    // Les offsets de l'en-tête ont une largeur fixe (10 chiffres) : la longueur de l'en-tête est connue avant de les calculer.
    private const string ModeleEnTete =
        "Version:1.0\r\nStartHTML:{0}\r\nEndHTML:{1}\r\nStartFragment:{2}\r\nEndFragment:{3}\r\n";

    private const string Zeros = "0000000000";

    /// <summary>Encode un fragment : en-tête <c>Version:1.0</c>, offsets en octets UTF-8, lignes terminées par CRLF.</summary>
    /// <param name="fragmentHtml">Fragment HTML à placer entre les marqueurs StartFragment et EndFragment.</param>
    /// <returns>Les octets UTF-8 du format, sans octet nul final (l'écrivain du presse-papiers l'ajoute).</returns>
    public static byte[] Encoder(string fragmentHtml)
    {
        ArgumentNullException.ThrowIfNull(fragmentHtml);

        var longueurEnTete = Encoding.UTF8.GetByteCount(string.Format(ModeleEnTete, Zeros, Zeros, Zeros, Zeros));
        var debutHtml = longueurEnTete;
        var debutFragment = debutHtml + Encoding.UTF8.GetByteCount(Prefixe + DebutMarqueur);
        var finFragment = debutFragment + Encoding.UTF8.GetByteCount(fragmentHtml);
        var finHtml = finFragment + Encoding.UTF8.GetByteCount(FinMarqueur + Suffixe);

        var texte = string.Format(ModeleEnTete, Offset(debutHtml), Offset(finHtml), Offset(debutFragment), Offset(finFragment))
                    + Prefixe + DebutMarqueur + fragmentHtml + FinMarqueur + Suffixe;
        return Encoding.UTF8.GetBytes(texte);
    }

    /// <summary>Extrait le fragment d'un « HTML Format » ; null si les données sont invalides.</summary>
    /// <param name="donnees">Octets bruts du format, avec ou sans octet nul final.</param>
    /// <returns>Le fragment décodé en UTF-8, ou null si les offsets manquent, sortent des données ou sont inversés.</returns>
    public static string? ExtraireFragment(ReadOnlySpan<byte> donnees)
    {
        var debut = LireOffset(donnees, "StartFragment:");
        var fin = LireOffset(donnees, "EndFragment:");
        if (debut is null || fin is null || debut < 0 || fin < debut || fin > donnees.Length)
        {
            return null;
        }

        return Encoding.UTF8.GetString(donnees[debut.Value..fin.Value]);
    }

    private static string Offset(int valeur) => valeur.ToString("D10", CultureInfo.InvariantCulture);

    // Cherche « Nom: » en début de ligne dans l'en-tête et lit l'entier qui suit. Fins de ligne CRLF ou LF acceptées :
    // les applications ne respectent pas toutes les CRLF.
    private static int? LireOffset(ReadOnlySpan<byte> donnees, string nom)
    {
        var motif = Encoding.ASCII.GetBytes(nom);
        var position = 0;
        while (position < donnees.Length)
        {
            var reste = donnees[position..];
            var finLigne = reste.IndexOf((byte)'\n');
            var ligne = finLigne < 0 ? reste : reste[..finLigne];

            if (ligne.StartsWith(motif))
            {
                var valeur = ligne[motif.Length..];
                if (valeur.Length > 0 && valeur[^1] == (byte)'\r')
                {
                    valeur = valeur[..^1];
                }

                return int.TryParse(Encoding.ASCII.GetString(valeur), NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out var entier)
                    ? entier
                    : null;
            }

            // L'en-tête s'arrête au premier document : inutile de parcourir le contenu HTML.
            if (ligne.StartsWith("<"u8))
            {
                return null;
            }

            if (finLigne < 0)
            {
                break;
            }

            position += finLigne + 1;
        }

        return null;
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
  total : 467
  échec : 0
  réussie : 467
  ignoré : 0
```

Les 21 nouveaux tests : `FormatHtmlTests` 21 (les cas `InlineData` comptent chacun).

- [ ] **Étape 5 : Vérifier sur le poste**

Sans objet : la tâche est du code pur, entièrement couvert par les tests. Le format est relu pour de bon par le
programme jetable des tâches 21 et 22 (HTML écrit puis relu à travers le vrai presse-papiers).

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Formats/FormatHtml.cs tests/PressePapiers.Tests/Formats/FormatHtmlTests.cs
git commit -m "Ajouter le format HTML Format du presse-papiers" -m "Encodage avec offsets en octets UTF-8 depuis le début (en-tête compris), CRLF et marqueurs StartFragment/EndFragment ; extraction tolérante (LF, lignes supplémentaires, octet nul final) qui rend null si les offsets sont invalides."
```
