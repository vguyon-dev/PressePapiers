### Tâche 14 : ListeFichiers

**Fichiers :**
- Modifier : `src/PressePapiers.Coeur/Formats/ListeFichiers.cs` (remplace la coquille ; contenu complet à l'étape 3)
- Créer : aucun
- Tests : `tests/PressePapiers.Tests/Formats/ListeFichiersTests.cs`

**Interfaces :**
- Utilise (tâche 1) : la coquille `ListeFichiers` (mêmes signatures).
- Fournit :
  - `public static byte[] EncoderDropFiles(IReadOnlyList<string> chemins)` : structure DROPFILES de 20 octets
    (`pFiles` = 20, `pt` = 0, `fNC` = 0, `fWide` = 1) suivie des chemins en UTF-16, chacun terminé par un nul, puis un
    nul de plus (liste vide : deux nuls). Lève `ArgumentNullException` pour `null` et `ArgumentException` pour un chemin
    vide ou contenant un nul (il couperait la liste) ;
  - `public static IReadOnlyList<string> DecoderDropFiles(ReadOnlySpan<byte> donnees)` : les chemins dans l'ordre ; lit
    `fWide` (UTF-16 ou page de code ANSI du système) et respecte `pFiles` ; liste vide si les données sont trop courtes ou
    si `pFiles` est inférieur à 20 ou hors des données ; un dernier chemin non terminé (données tronquées) est abandonné ;
    jamais d'exception.
  - Consommateurs : tâche 21 (`LecteurPressePapiers`, lecture de CF_HDROP), tâche 22 (`EcrivainPressePapiers`, écriture de
    CF_HDROP).

- [ ] **Étape 1 : Écrire le test qui échoue**

`tests/PressePapiers.Tests/Formats/ListeFichiersTests.cs`

```csharp
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
```

`Les_chemins_sont_separes_par_un_nul_avec_un_double_nul_final` compare les octets exacts : un séparateur oublié, un double
nul manquant ou un encodage UTF-8 au lieu d'UTF-16 le fait échouer.

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : la compilation réussit et les 15 nouveaux tests échouent avec
`System.NotImplementedException : Tâche 14` ; les 467 tests précédents restent verts :

```
Résumé de série de tests : Échec!
  total : 482
  échec : 15
  réussie : 467
```

- [ ] **Étape 3 : Implémenter**

`src/PressePapiers.Coeur/Formats/ListeFichiers.cs`

```csharp
using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace PressePapiers.Coeur.Formats;

/// <summary>
/// Format CF_HDROP : une structure DROPFILES (20 octets : <c>pFiles</c>, point, <c>fNC</c>, <c>fWide</c>)
/// suivie des chemins terminés chacun par un nul, la liste se finissant par un nul supplémentaire.
/// </summary>
public static class ListeFichiers
{
    private const int TailleEnTete = 20;

    /// <summary>Encode des chemins en structure DROPFILES, chemins en UTF-16.</summary>
    /// <param name="chemins">Chemins complets, sans chemin vide ni caractère nul.</param>
    /// <exception cref="ArgumentException">Un chemin est vide ou contient un nul : il casserait le découpage de la liste.</exception>
    public static byte[] EncoderDropFiles(IReadOnlyList<string> chemins)
    {
        ArgumentNullException.ThrowIfNull(chemins);
        foreach (var chemin in chemins)
        {
            if (string.IsNullOrEmpty(chemin) || chemin.Contains('\0'))
            {
                throw new ArgumentException("Un chemin de fichier est vide ou contient un caractère nul.", nameof(chemins));
            }
        }

        // Liste vide : deux nuls quand même, pour que la structure reste un double nul valide pour les lecteurs.
        var texte = chemins.Count == 0 ? "\0\0" : string.Concat(chemins.Select(c => c + "\0")) + "\0";
        var corps = Encoding.Unicode.GetBytes(texte);
        var donnees = new byte[TailleEnTete + corps.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(donnees, TailleEnTete);          // pFiles
        BinaryPrimitives.WriteInt32LittleEndian(donnees.AsSpan(16), 1);            // fWide : chemins en UTF-16
        corps.CopyTo(donnees, TailleEnTete);
        return donnees;
    }

    /// <summary>Décode une structure DROPFILES en liste de chemins.</summary>
    /// <param name="donnees">Octets du format CF_HDROP.</param>
    /// <returns>
    /// Les chemins dans l'ordre. Liste vide si la structure est invalide ; un dernier chemin sans terminateur
    /// (données tronquées) est abandonné plutôt que rendu coupé.
    /// </returns>
    public static IReadOnlyList<string> DecoderDropFiles(ReadOnlySpan<byte> donnees)
    {
        if (donnees.Length < TailleEnTete)
        {
            return [];
        }

        var debut = BinaryPrimitives.ReadUInt32LittleEndian(donnees);
        if (debut < TailleEnTete || debut > donnees.Length)
        {
            return [];
        }

        var large = BinaryPrimitives.ReadInt32LittleEndian(donnees[16..]) != 0;
        var corps = donnees[(int)debut..];
        return large ? DecoderUtf16(corps) : DecoderAnsi(corps);
    }

    private static List<string> DecoderUtf16(ReadOnlySpan<byte> corps)
    {
        var chemins = new List<string>();
        var debutChemin = 0;
        // Un octet isolé en fin de données est ignoré : on ne lit que des unités UTF-16 entières.
        for (var i = 0; i + 1 < corps.Length; i += 2)
        {
            if (corps[i] != 0 || corps[i + 1] != 0)
            {
                continue;
            }

            if (i == debutChemin)
            {
                break;   // chemin vide : fin de liste (le double nul)
            }

            chemins.Add(Encoding.Unicode.GetString(corps[debutChemin..i]));
            debutChemin = i + 2;
        }

        return chemins;
    }

    private static List<string> DecoderAnsi(ReadOnlySpan<byte> corps)
    {
        var encodage = EncodageAnsi();
        var chemins = new List<string>();
        var debutChemin = 0;
        for (var i = 0; i < corps.Length; i++)
        {
            if (corps[i] != 0)
            {
                continue;
            }

            if (i == debutChemin)
            {
                break;
            }

            chemins.Add(encodage.GetString(corps[debutChemin..i]));
            debutChemin = i + 1;
        }

        return chemins;
    }

    // Page de code ANSI du système ; Latin-1 si elle n'est pas disponible (jamais d'exception pour une lecture).
    private static Encoding EncodageAnsi()
    {
        try
        {
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return Encoding.Latin1;
        }
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
  total : 482
  échec : 0
  réussie : 482
  ignoré : 0
```

Les 15 nouveaux tests : `ListeFichiersTests` 15 (les cas `InlineData` comptent chacun).

- [ ] **Étape 5 : Vérifier sur le poste**

Sans objet : code pur couvert par les tests. L'aller-retour par le vrai presse-papiers (CF_HDROP) est vérifié par le
programme jetable des tâches 21 et 22.

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Formats/ListeFichiers.cs tests/PressePapiers.Tests/Formats/ListeFichiersTests.cs
git commit -m "Ajouter la liste de fichiers CF_HDROP" -m "Structure DROPFILES (pFiles = 20, fWide = 1, chemins UTF-16 séparés par un nul, double nul final) ; décodage aussi des listes ANSI, des pFiles inhabituels et des données tronquées sans exception."
```
