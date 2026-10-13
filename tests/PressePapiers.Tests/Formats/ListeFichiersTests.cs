using System.Buffers.Binary;
using System.Text;
using PressePapiers.Coeur.Formats;

namespace PressePapiers.Tests.Formats;

public sealed class ListeFichiersTests
{
    [Fact]
    public void L_en_tete_est_une_structure_DROPFILES_de_20_octets_en_utf16()
    {
        var donnees = ListeFichiers.EncoderDropFiles([@"C:\a.txt"]);

        Assert.Equal(20u, BinaryPrimitives.ReadUInt32LittleEndian(donnees.AsSpan(0, 4)));   // pFiles
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(donnees.AsSpan(4, 4)));      // pt.x
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(donnees.AsSpan(8, 4)));      // pt.y
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(donnees.AsSpan(12, 4)));     // fNC
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(donnees.AsSpan(16, 4)));     // fWide
    }

    [Fact]
    public void Les_chemins_sont_separes_par_un_nul_avec_un_double_nul_final()
    {
        var donnees = ListeFichiers.EncoderDropFiles([@"C:\a.txt", @"D:\b c\é.txt"]);

        var attendu = Encoding.Unicode.GetBytes("C:\\a.txt\0D:\\b c\\é.txt\0\0");
        Assert.Equal(attendu, donnees.AsSpan(20).ToArray());
    }

    [Fact]
    public void Une_liste_vide_donne_un_en_tete_et_un_double_nul()
    {
        var donnees = ListeFichiers.EncoderDropFiles([]);

        Assert.Equal(20 + 4, donnees.Length);
        Assert.Empty(ListeFichiers.DecoderDropFiles(donnees));
    }

    [Fact]
    public void Un_aller_retour_rend_les_memes_chemins_dans_le_meme_ordre()
    {
        string[] chemins = [@"C:\Users\moi\Documents\rapport final.docx", @"\\serveur\partage\été 😀.png", @"D:\x"];

        var relus = ListeFichiers.DecoderDropFiles(ListeFichiers.EncoderDropFiles(chemins));

        Assert.Equal(chemins, relus);
    }

    [Fact]
    public void Un_chemin_vide_ou_avec_un_nul_est_refuse_a_l_encodage()
    {
        // Un chemin vide terminerait la liste trop tôt ; un nul interne couperait le chemin en deux.
        Assert.Throws<ArgumentException>(() => ListeFichiers.EncoderDropFiles([@"C:\a", ""]));
        Assert.Throws<ArgumentException>(() => ListeFichiers.EncoderDropFiles(["C:\\a\0b"]));
        Assert.Throws<ArgumentNullException>(() => ListeFichiers.EncoderDropFiles(null!));
    }

    [Fact]
    public void Une_structure_en_ansi_est_decodee()
    {
        // fWide = 0 : certaines applications écrivent encore des chemins ANSI.
        var entete = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(entete, 20);
        var corps = Encoding.ASCII.GetBytes("C:\\a.txt\0D:\\b.txt\0\0");

        var relus = ListeFichiers.DecoderDropFiles([.. entete, .. corps]);

        Assert.Equal([@"C:\a.txt", @"D:\b.txt"], relus);
    }

    [Fact]
    public void Un_pFiles_different_de_20_est_respecte()
    {
        // Les chemins commencent là où pFiles le dit, pas forcément juste après l'en-tête.
        var entete = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(entete, 24);
        BinaryPrimitives.WriteInt32LittleEndian(entete.AsSpan(16), 1);
        var corps = Encoding.Unicode.GetBytes("C:\\a.txt\0\0");

        Assert.Equal([@"C:\a.txt"], ListeFichiers.DecoderDropFiles([.. entete, .. corps]));
    }

    [Fact]
    public void Un_dernier_chemin_sans_terminateur_est_abandonne()
    {
        var donnees = ListeFichiers.EncoderDropFiles([@"C:\a.txt", @"C:\b.txt"]);
        // On coupe le double nul final et le nul du dernier chemin : le dernier chemin est tronqué.
        var tronque = donnees.AsSpan(0, donnees.Length - 4 - 2).ToArray();

        Assert.Equal([@"C:\a.txt"], ListeFichiers.DecoderDropFiles(tronque));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(19)]
    [InlineData(20)]
    public void Des_donnees_trop_courtes_donnent_une_liste_vide(int longueur)
    {
        Assert.Empty(ListeFichiers.DecoderDropFiles(new byte[longueur]));
    }

    [Fact]
    public void Un_pFiles_hors_des_donnees_donne_une_liste_vide()
    {
        var donnees = ListeFichiers.EncoderDropFiles([@"C:\a.txt"]);
        BinaryPrimitives.WriteUInt32LittleEndian(donnees, 100000);

        Assert.Empty(ListeFichiers.DecoderDropFiles(donnees));
    }

    [Fact]
    public void Un_pFiles_inferieur_a_l_en_tete_donne_une_liste_vide()
    {
        var donnees = ListeFichiers.EncoderDropFiles([@"C:\a.txt"]);
        BinaryPrimitives.WriteUInt32LittleEndian(donnees, 4);

        Assert.Empty(ListeFichiers.DecoderDropFiles(donnees));
    }

    [Fact]
    public void Une_longueur_impaire_en_utf16_ne_leve_pas_d_exception()
    {
        var donnees = ListeFichiers.EncoderDropFiles([@"C:\a.txt"]);

        // Un octet en trop au bout : l'octet isolé est ignoré, le chemin terminé reste lu.
        var relus = ListeFichiers.DecoderDropFiles([.. donnees, 0x41]);

        Assert.Equal([@"C:\a.txt"], relus);
    }
}
