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
