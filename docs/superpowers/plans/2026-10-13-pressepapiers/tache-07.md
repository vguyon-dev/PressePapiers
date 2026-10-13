### Tâche 7 : Sauvegarde de la base

**Fichiers :**
- Créer : aucun
- Modifier : `src/PressePapiers.Coeur/Stockage/Sauvegarde.cs` (le plan donne le contenu complet du fichier après modification)
- Tests : `tests/PressePapiers.Tests/Stockage/SauvegardeTests.cs`

**Interfaces :**
- Utilise : (tâche 1) `BaseHistorique.Ouvrir` ; (tâches 4 et 5) `DepotHistorique.AjouterCopie` pour remplir la base de test ; `DossierTemporaire`, `InsertionsSql`, `ContenusDeTest`.
- Fournit : `public static string? Sauvegarde.Creer(string cheminBase, string dossierSauvegardes, TimeProvider horloge, int nombreGarde = 3)` ; `public static string? Sauvegarde.PlusRecente(string dossierSauvegardes)` ; `public static void Sauvegarde.Restaurer(string cheminSauvegarde, string cheminBase)`.

Remarques pour les tâches suivantes (tâche 16 : copie de sécurité au démarrage, proposition de restauration) :
- `Creer` copie par `SqliteConnection.BackupDatabase` (source ouverte en lecture seule, `Pooling=false`), base ouverte ou non : le contenu encore dans le `-wal` est copié, et une base laissée par un plantage (fichiers `.db`, `-wal`, `-shm` d'un processus mort) se copie aussi (tests). La copie est un fichier seul, sans `-wal` ni `-shm`.
- Nom `historique-AAAAMMJJ-HHmmss.db` en **UTC** (`GetUtcNow`), pour que l'ordre des noms soit l'ordre chronologique même au changement d'heure. Deux copies dans la même seconde donnent un seul fichier.
- Renvoie `null` si le fichier de base n'existe pas ou est vide (rien à protéger ; le dossier des copies n'est pas créé). `nombreGarde` inférieur à 1 = `ArgumentOutOfRangeException`, avant toute écriture.
- **Base illisible** (point de vigilance 4, fichier qui n'est pas une base SQLite) : `Creer` lève `SqliteException`, ne laisse aucun fichier partiel (la copie est écrite sous `.partiel` puis déplacée) et n'applique PAS la rotation, pour qu'une base abîmée ne chasse pas les bonnes copies. L'appelant (tâche 16) attrape l'exception, note le problème et propose `Restaurer`.
- **Rotation** : seuls les fichiers nommés `historique-AAAAMMJJ-HHmmss.db` comptent ; on garde la copie qui vient d'être créée plus les `nombreGarde - 1` plus récentes des autres. Une horloge qui recule ne supprime donc jamais la copie qu'on vient de faire. Une ancienne copie impossible à supprimer reste là sans erreur.
- `PlusRecente` : le plus grand nom au format ci-dessus, `null` si le dossier n'existe pas ou ne contient aucune copie.
- `Restaurer` : la base doit être fermée. La copie est d'abord posée à côté (`historique.db.restauration`), puis les fichiers `-wal` et `-shm` sont supprimés, puis la copie remplace la base. Si la base est encore ouverte, l'opération échoue (`IOException` ou `UnauthorizedAccessException`) en laissant la base intacte et sans fichier provisoire. Copie absente = `FileNotFoundException`. Le dossier de la base est créé au besoin.

- [ ] **Étape 1 : Écrire le test qui échoue**

`SauvegardeTests` couvre le nom horodaté UTC, le contenu (dont le `-wal` et l'instantané d'un plantage), la rotation (3 copies, réglage, fichiers étrangers préservés, horloge qui recule, même seconde), la base illisible, `PlusRecente` et `Restaurer` (dont base ouverte, copie absente et le scénario complet « fichier qui n'est pas une base SQLite »).

`tests/PressePapiers.Tests/Stockage/SauvegardeTests.cs`

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Tests.Outils;

namespace PressePapiers.Tests.Stockage;

public sealed class SauvegardeTests : IDisposable
{
    private static readonly DateTimeOffset Debut = new(2026, 10, 13, 8, 15, 30, TimeSpan.Zero);

    private readonly DossierTemporaire _racine = new();
    private readonly FakeTimeProvider _horloge = new(Debut);

    private string CheminBase => _racine.Fichier("historique.db");

    private string Sauvegardes => _racine.Fichier("sauvegardes");

    public void Dispose() => _racine.Dispose();

    // ---- Creer ----

    [Fact]
    public void Sans_base_il_n_y_a_rien_a_sauvegarder()
    {
        Assert.Null(Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge));

        Assert.False(Directory.Exists(Sauvegardes));
    }

    [Fact]
    public void Un_fichier_vide_n_est_pas_une_base_a_sauvegarder()
    {
        File.WriteAllBytes(CheminBase, []);

        Assert.Null(Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge));
    }

    [Fact]
    public void La_copie_porte_la_date_et_l_heure_UTC_dans_son_nom_et_contient_les_donnees()
    {
        using (var origine = CreerBase(3))
        {
            var chemin = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);

            Assert.Equal(Path.Combine(Sauvegardes, "historique-20261013-081530.db"), chemin);
            Assert.True(File.Exists(chemin));
            Assert.Equal(3, CompterElementsDans(chemin));
        }
    }

    [Fact]
    public void Le_nom_suit_le_fuseau_UTC_et_non_le_fuseau_local()
    {
        CreerBase(1).Dispose();
        // 23:59:59 UTC le 31/12 : en heure locale d'un fuseau en avance, ce serait déjà l'année suivante.
        var horloge = new FakeTimeProvider(new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero));
        horloge.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Avance", TimeSpan.FromHours(5), "Avance", "Avance"));

        var chemin = Sauvegarde.Creer(CheminBase, Sauvegardes, horloge);

        Assert.Equal("historique-20261231-235959.db", Path.GetFileName(chemin));
    }

    [Fact]
    public void La_copie_contient_aussi_ce_qui_est_encore_dans_le_fichier_wal()
    {
        using var origine = CreerBase(5);
        // La base est ouverte : ses dernières écritures sont dans historique.db-wal, pas encore dans historique.db.
        Assert.True(File.Exists(CheminBase + "-wal"));

        var chemin = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);

        Assert.Equal(5, CompterElementsDans(chemin!));
    }

    [Fact]
    public void La_copie_marche_sur_une_base_laissee_par_un_plantage()
    {
        using var origine = CreerBase(5);
        // Un instantané des trois fichiers pris base ouverte vaut un plantage : historique.db, -wal et -shm d'un processus mort.
        var plante = _racine.Fichier("plante");
        Directory.CreateDirectory(plante);
        foreach (var suffixe in new[] { "", "-wal", "-shm" })
        {
            using var lecture = new FileStream(CheminBase + suffixe, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var ecriture = File.Create(Path.Combine(plante, "historique.db" + suffixe));
            lecture.CopyTo(ecriture);
        }

        var chemin = Sauvegarde.Creer(Path.Combine(plante, "historique.db"), Sauvegardes, _horloge);

        Assert.Equal(5, CompterElementsDans(chemin!));
    }

    [Fact]
    public void La_copie_est_un_fichier_seul_sans_wal_ni_shm()
    {
        using var origine = CreerBase(2);

        Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);

        Assert.Equal(["historique-20261013-081530.db"], Directory.GetFiles(Sauvegardes).Select(f => Path.GetFileName(f)));
    }

    [Fact]
    public void La_sauvegarde_ne_modifie_pas_la_base_d_origine()
    {
        using var origine = CreerBase(4);
        var avant = LireOctetsDeLaBaseOuverte();

        Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);

        Assert.Equal(avant, LireOctetsDeLaBaseOuverte());
        Assert.Equal("4", InsertionsSql.Scalaire(origine.Connexion, "SELECT COUNT(*) FROM element"));
    }

    // ---- Rotation ----

    [Fact]
    public void Seules_les_trois_dernieres_copies_sont_gardees()
    {
        CreerBase(1).Dispose();
        for (var i = 0; i < 5; i++)
        {
            Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);
            _horloge.Advance(TimeSpan.FromSeconds(10));
        }

        // Cinq copies à 08:15:30, :40, :50, 08:16:00, :10 ; les plus récentes restent.
        Assert.Equal(
            ["historique-20261013-081550.db", "historique-20261013-081600.db", "historique-20261013-081610.db"],
            NomsSauvegardes());
    }

    [Theory]
    [InlineData(1, "historique-20261013-081600.db")]
    [InlineData(2, "historique-20261013-081550.db,historique-20261013-081600.db")]
    public void Le_nombre_de_copies_gardees_est_reglable(int nombreGarde, string attendu)
    {
        CreerBase(1).Dispose();
        for (var i = 0; i < 4; i++)
        {
            Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge, nombreGarde);
            _horloge.Advance(TimeSpan.FromSeconds(10));
        }

        Assert.Equal(attendu.Split(','), NomsSauvegardes());
    }

    [Fact]
    public void La_rotation_ne_touche_pas_aux_fichiers_qui_ne_sont_pas_des_copies_horodatees()
    {
        CreerBase(1).Dispose();
        Directory.CreateDirectory(Sauvegardes);
        foreach (var nom in new[] { "notes.txt", "historique-manuel.db", "historique-2026.db", "historique-20261013-081530.db.bak" })
        {
            File.WriteAllText(Path.Combine(Sauvegardes, nom), "a garder");
        }

        for (var i = 0; i < 5; i++)
        {
            Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);
            _horloge.Advance(TimeSpan.FromSeconds(10));
        }

        var presents = Directory.GetFiles(Sauvegardes).Select(f => Path.GetFileName(f)).Order().ToArray();
        Assert.Contains("notes.txt", presents);
        Assert.Contains("historique-manuel.db", presents);
        Assert.Contains("historique-2026.db", presents);
        Assert.Contains("historique-20261013-081530.db.bak", presents);
        Assert.Equal(3, NomsSauvegardes().Length);
    }

    [Fact]
    public void La_copie_qui_vient_d_etre_creee_n_est_jamais_supprimee_meme_si_l_horloge_a_recule()
    {
        CreerBase(1).Dispose();
        foreach (var secondes in new[] { 100, 110, 120 })
        {
            Sauvegarde.Creer(CheminBase, Sauvegardes, new FakeTimeProvider(Debut.AddSeconds(secondes)));
        }

        // L'horloge revient dix minutes en arrière : la nouvelle copie est la plus ancienne par son nom.
        var chemin = Sauvegarde.Creer(CheminBase, Sauvegardes, new FakeTimeProvider(Debut.AddMinutes(-10)));

        Assert.True(File.Exists(chemin));
        Assert.Equal(3, NomsSauvegardes().Length);
        Assert.Contains(Path.GetFileName(chemin), NomsSauvegardes());
    }

    [Fact]
    public void Deux_copies_dans_la_meme_seconde_donnent_un_seul_fichier()
    {
        CreerBase(1).Dispose();

        var premiere = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);
        var seconde = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);

        Assert.Equal(premiere, seconde);
        Assert.Single(NomsSauvegardes());
        Assert.Equal(1, CompterElementsDans(seconde!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Garder_moins_d_une_copie_est_refuse_sans_rien_creer(int nombreGarde)
    {
        CreerBase(1).Dispose();

        Assert.Throws<ArgumentOutOfRangeException>(() => Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge, nombreGarde));

        Assert.False(Directory.Exists(Sauvegardes));
    }

    [Fact]
    public void Une_base_illisible_leve_SqliteException_sans_laisser_de_fichier_partiel_ni_perdre_les_copies_saines()
    {
        CreerBase(2).Dispose();
        for (var i = 0; i < 3; i++)
        {
            Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);
            _horloge.Advance(TimeSpan.FromSeconds(10));
        }

        var saines = NomsSauvegardes();
        File.WriteAllText(CheminBase, "ceci n'est pas une base SQLite, juste du texte qui traine dans le fichier d'historique");

        Assert.Throws<SqliteException>(() => Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge));

        // Aucune copie de la base abîmée, aucun reste, et la rotation n'a pas mangé les bonnes copies.
        Assert.Equal(saines, NomsSauvegardes());
        Assert.Equal(3, Directory.GetFiles(Sauvegardes).Length);
    }

    // ---- PlusRecente ----

    [Fact]
    public void PlusRecente_sans_dossier_ou_sans_copie_donne_null()
    {
        Assert.Null(Sauvegarde.PlusRecente(Sauvegardes));

        Directory.CreateDirectory(Sauvegardes);
        File.WriteAllText(Path.Combine(Sauvegardes, "notes.txt"), "pas une copie");
        Assert.Null(Sauvegarde.PlusRecente(Sauvegardes));
    }

    [Fact]
    public void PlusRecente_donne_la_copie_au_nom_le_plus_recent_quel_que_soit_l_ordre_de_creation()
    {
        Directory.CreateDirectory(Sauvegardes);
        foreach (var nom in new[] { "historique-20261013-081600.db", "historique-20261112-000000.db", "historique-20260101-235959.db" })
        {
            File.WriteAllText(Path.Combine(Sauvegardes, nom), "x");
        }

        File.WriteAllText(Path.Combine(Sauvegardes, "historique-99999999-999999.db.bak"), "pas une copie");

        Assert.Equal(Path.Combine(Sauvegardes, "historique-20261112-000000.db"), Sauvegarde.PlusRecente(Sauvegardes));
    }

    // ---- Restaurer ----

    [Fact]
    public void Restaurer_remplace_la_base_par_la_copie_et_supprime_wal_et_shm()
    {
        string copie;
        using (var origine = CreerBase(3))
        {
            copie = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge)!;
        }

        File.WriteAllText(CheminBase, "base abimee");
        File.WriteAllText(CheminBase + "-wal", "journal d'une autre base");
        File.WriteAllText(CheminBase + "-shm", "memoire partagee d'une autre base");

        Sauvegarde.Restaurer(copie, CheminBase);

        Assert.False(File.Exists(CheminBase + "-wal"));
        Assert.False(File.Exists(CheminBase + "-shm"));
        Assert.True(File.Exists(copie));
        Assert.False(File.Exists(CheminBase + ".restauration"));
        using var restauree = BaseHistorique.Ouvrir(CheminBase);
        Assert.Equal("3", InsertionsSql.Scalaire(restauree.Connexion, "SELECT COUNT(*) FROM element"));
    }

    [Fact]
    public void Restaurer_cree_le_dossier_de_la_base_s_il_manque()
    {
        string copie;
        using (var origine = CreerBase(1))
        {
            copie = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge)!;
        }

        var neuve = Path.Combine(_racine.Fichier("autre"), "dossier", "historique.db");

        Sauvegarde.Restaurer(copie, neuve);

        using var restauree = BaseHistorique.Ouvrir(neuve);
        Assert.Equal("1", InsertionsSql.Scalaire(restauree.Connexion, "SELECT COUNT(*) FROM element"));
    }

    [Fact]
    public void Restaurer_une_copie_absente_leve_FileNotFoundException_et_laisse_la_base_intacte()
    {
        File.WriteAllText(CheminBase, "base actuelle");

        Assert.Throws<FileNotFoundException>(() => Sauvegarde.Restaurer(_racine.Fichier("absente.db"), CheminBase));

        Assert.Equal("base actuelle", File.ReadAllText(CheminBase));
    }

    [Fact]
    public void Restaurer_sur_une_base_encore_ouverte_echoue_sans_rien_casser()
    {
        string copie;
        using (var autre = CreerBase(1))
        {
            copie = Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge)!;
        }

        using var ouverte = BaseHistorique.Ouvrir(CheminBase);
        new DepotHistorique(ouverte, new DossierDonnees(_racine.Chemin), _horloge, JournalNul.Instance)
            .AjouterCopie(ContenusDeTest.Texte("ecrit apres la copie"));

        var exception = Record.Exception(() => Sauvegarde.Restaurer(copie, CheminBase));

        Assert.True(exception is IOException or UnauthorizedAccessException, $"exception inattendue : {exception}");
        Assert.False(File.Exists(CheminBase + ".restauration"));
        Assert.Equal("2", InsertionsSql.Scalaire(ouverte.Connexion, "SELECT COUNT(*) FROM element"));
    }

    [Fact]
    public void Une_base_qui_n_est_pas_une_base_SQLite_se_repare_depuis_la_copie_la_plus_recente()
    {
        using (var origine = CreerBase(6))
        {
            Sauvegarde.Creer(CheminBase, Sauvegardes, _horloge);
        }

        File.WriteAllText(CheminBase, "du texte dans le fichier d'historique, pas une base SQLite");
        Assert.Throws<SqliteException>(() => BaseHistorique.Ouvrir(CheminBase).Dispose());

        var plusRecente = Sauvegarde.PlusRecente(Sauvegardes);
        Assert.NotNull(plusRecente);
        Sauvegarde.Restaurer(plusRecente, CheminBase);

        using var reparee = BaseHistorique.Ouvrir(CheminBase);
        Assert.Equal("6", InsertionsSql.Scalaire(reparee.Connexion, "SELECT COUNT(*) FROM element"));
    }

    // ---- Assistants ----

    // Base de l'historique au chemin habituel, avec n textes ; laissée ouverte pour que ses écritures restent dans le wal.
    private BaseHistorique CreerBase(int elements)
    {
        var dossiers = new DossierDonnees(_racine.Chemin);
        var baseHistorique = BaseHistorique.Ouvrir(CheminBase);
        var depot = new DepotHistorique(baseHistorique, dossiers, _horloge, JournalNul.Instance);
        for (var i = 0; i < elements; i++)
        {
            depot.AjouterCopie(ContenusDeTest.Texte("element " + i));
        }

        return baseHistorique;
    }

    // Octets du fichier de la base alors qu'une connexion l'a ouvert : File.ReadAllBytes refuserait de le lire.
    private byte[] LireOctetsDeLaBaseOuverte()
    {
        using var flux = new FileStream(CheminBase, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var memoire = new MemoryStream();
        flux.CopyTo(memoire);
        return memoire.ToArray();
    }

    // Nombre d'éléments d'une copie, lu sur un double pour ne pas ajouter de wal ni de shm à côté de la copie.
    private int CompterElementsDans(string chemin)
    {
        var copieLecture = _racine.Fichier("lecture-" + Guid.NewGuid().ToString("N") + ".db");
        File.Copy(chemin, copieLecture);
        using var baseHistorique = BaseHistorique.Ouvrir(copieLecture);
        return int.Parse(InsertionsSql.Scalaire(baseHistorique.Connexion, "SELECT COUNT(*) FROM element")!, System.Globalization.CultureInfo.InvariantCulture);
    }

    private string[] NomsSauvegardes() =>
        Directory.Exists(Sauvegardes)
            ? [.. Directory.GetFiles(Sauvegardes, "historique-????????-??????.db").Select(f => Path.GetFileName(f)).Order()]
            : [];
}
```

- [ ] **Étape 2 : Lancer le test et constater l'échec**

```bash
dotnet test --solution PressePapiers.slnx -c Debug
```

Résultat attendu : les 24 nouveaux tests échouent avec `System.NotImplementedException : Tâche 7` ; les 250 tests précédents restent verts :

```
Résumé de série de tests : Échec!
  total : 274
  échec : 24
  réussie : 250
  ignoré : 0
```

- [ ] **Étape 3 : Implémenter**

Remplacer la coquille par l'implémentation.

`src/PressePapiers.Coeur/Stockage/Sauvegarde.cs`

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// Copies de sécurité de la base, par l'API de sauvegarde de SQLite : le fichier <c>-wal</c> fait partie de l'état de la base,
/// donc le fichier n'est jamais copié à la main.
/// </summary>
public static class Sauvegarde
{
    // Seules les copies que cette classe crée sont comptées et supprimées par la rotation : rien d'autre dans le dossier.
    private static readonly Regex NomDeCopie = new(@"^historique-\d{8}-\d{6}\.db$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Copie la base dans <paramref name="dossierSauvegardes"/> (<c>historique-AAAAMMJJ-HHmmss.db</c>, heure UTC) puis garde les
    /// <paramref name="nombreGarde"/> plus récentes, celle qui vient d'être créée comprise. Renvoie le chemin créé, ou null
    /// s'il n'y a pas de base (fichier absent ou vide). Une base illisible lève <see cref="SqliteException"/> :
    /// aucune copie n'est alors créée et la rotation ne supprime rien, pour que les bonnes copies survivent à une base abîmée.
    /// </summary>
    /// <param name="cheminBase">Fichier de la base, ouverte ou non.</param>
    /// <param name="dossierSauvegardes">Dossier des copies, créé au besoin.</param>
    /// <param name="horloge">Source de l'heure du nom.</param>
    /// <param name="nombreGarde">Nombre de copies gardées, au moins 1.</param>
    /// <exception cref="SqliteException">Le fichier n'est pas une base SQLite lisible.</exception>
    public static string? Creer(string cheminBase, string dossierSauvegardes, TimeProvider horloge, int nombreGarde = 3)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cheminBase);
        ArgumentException.ThrowIfNullOrWhiteSpace(dossierSauvegardes);
        ArgumentNullException.ThrowIfNull(horloge);
        ArgumentOutOfRangeException.ThrowIfLessThan(nombreGarde, 1);

        var fichier = new FileInfo(cheminBase);
        if (!fichier.Exists || fichier.Length == 0)
        {
            return null;
        }

        Directory.CreateDirectory(dossierSauvegardes);
        var nom = $"historique-{horloge.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.db";
        var chemin = Path.Combine(dossierSauvegardes, nom);

        // Écrite sous un nom provisoire : une copie qui échoue ne détruit pas une bonne copie de la même seconde.
        var provisoire = chemin + ".partiel";
        try
        {
            Copier(cheminBase, provisoire);
            File.Move(provisoire, chemin, overwrite: true);
        }
        catch
        {
            SupprimerSansBruit(provisoire);
            throw;
        }

        AppliquerRotation(dossierSauvegardes, nom, nombreGarde);
        return chemin;
    }

    /// <summary>Chemin de la copie la plus récente (par son nom), ou null s'il n'y en a pas.</summary>
    public static string? PlusRecente(string dossierSauvegardes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dossierSauvegardes);

        var nom = NomsDeCopies(dossierSauvegardes).FirstOrDefault();
        return nom is null ? null : Path.Combine(dossierSauvegardes, nom);
    }

    /// <summary>
    /// Remplace la base par une copie. La base doit être fermée. Les fichiers <c>-wal</c> et <c>-shm</c> de l'ancienne base
    /// sont supprimés : appliqués à la base restaurée, ils la corrompraient. La copie est d'abord posée à côté de la base,
    /// puis mise en place d'un seul geste ; si quelque chose échoue (base encore ouverte), la base actuelle est laissée telle quelle.
    /// </summary>
    /// <exception cref="FileNotFoundException">La copie n'existe pas.</exception>
    public static void Restaurer(string cheminSauvegarde, string cheminBase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cheminSauvegarde);
        ArgumentException.ThrowIfNullOrWhiteSpace(cheminBase);

        if (!File.Exists(cheminSauvegarde))
        {
            throw new FileNotFoundException("La copie de sécurité à restaurer n'existe pas.", cheminSauvegarde);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(cheminBase))!);
        var provisoire = cheminBase + ".restauration";
        File.Copy(cheminSauvegarde, provisoire, overwrite: true);
        try
        {
            File.Delete(cheminBase + "-wal");
            File.Delete(cheminBase + "-shm");
            File.Move(provisoire, cheminBase, overwrite: true);
        }
        catch
        {
            SupprimerSansBruit(provisoire);
            throw;
        }
    }

    // Source en lecture seule : la sauvegarde ne peut rien écrire dans la base qu'elle copie.
    private static void Copier(string cheminBase, string destination)
    {
        var chaineSource = new SqliteConnectionStringBuilder
        {
            DataSource = cheminBase,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();
        var chaineDestination = new SqliteConnectionStringBuilder
        {
            DataSource = destination,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();

        using var source = new SqliteConnection(chaineSource);
        using var cible = new SqliteConnection(chaineDestination);
        source.Open();
        cible.Open();
        source.BackupDatabase(cible);
    }

    // Garde la copie qui vient d'être créée et les (nombreGarde - 1) plus récentes parmi les autres. Si l'horloge a reculé,
    // la nouvelle copie peut être la plus ancienne par son nom : elle reste quand même.
    private static void AppliquerRotation(string dossierSauvegardes, string nomCree, int nombreGarde)
    {
        var aSupprimer = NomsDeCopies(dossierSauvegardes).Where(nom => nom != nomCree).Skip(nombreGarde - 1);
        foreach (var nom in aSupprimer.ToList())
        {
            SupprimerSansBruit(Path.Combine(dossierSauvegardes, nom));
        }
    }

    // Noms des copies horodatées, de la plus récente à la plus ancienne (l'horodatage en tête du nom se trie comme du texte).
    private static IEnumerable<string> NomsDeCopies(string dossierSauvegardes) =>
        Directory.Exists(dossierSauvegardes)
            ? Directory.EnumerateFiles(dossierSauvegardes)
                .Select(chemin => Path.GetFileName(chemin))
                .Where(nom => NomDeCopie.IsMatch(nom))
                .OrderDescending(StringComparer.Ordinal)
            : [];

    // Une ancienne copie qu'on ne peut pas supprimer (ouverte ailleurs) reste simplement là jusqu'à la prochaine rotation.
    private static void SupprimerSansBruit(string chemin)
    {
        try
        {
            File.Delete(chemin);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
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
  total : 274
  échec : 0
  réussie : 274
  ignoré : 0
```

Les 24 nouveaux tests : `SauvegardeTests` 24.

Contrôle de l'ordre (vérifié par mutation) : si la rotation garde les plus anciennes (tri croissant), `Seules_les_trois_dernieres_copies_sont_gardees`, `PlusRecente_donne_la_copie_au_nom_le_plus_recent_quel_que_soit_l_ordre_de_creation` et le cas `nombreGarde = 2` échouent ; si elle garde `nombreGarde` autres copies en plus de la nouvelle, 5 tests échouent ; si `Restaurer` oublie `-wal` et `-shm`, `Restaurer_remplace_la_base_par_la_copie_et_supprime_wal_et_shm` échoue.

- [ ] **Étape 5 : Vérifier sur le poste**

Sans objet : les comportements propres à Windows (fichiers verrouillés, `-wal` d'une base ouverte) sont reproduits par les tests, qui tournent sur le poste.

- [ ] **Étape 6 : Committer**

```bash
git add src/PressePapiers.Coeur/Stockage/Sauvegarde.cs tests/PressePapiers.Tests/Stockage/SauvegardeTests.cs
git commit -m "Sauvegarder et restaurer la base d'historique" -m "La copie passe par l'API de sauvegarde de SQLite, sous un nom provisoire puis
posée sous son nom horodaté en UTC. La rotation garde les trois dernières copies
dont celle qui vient d'être créée, ne touche qu'aux fichiers de ce format et ne
s'exécute pas si la base est illisible, pour que les bonnes copies survivent.
La restauration pose la copie à côté de la base, supprime -wal et -shm puis
met la copie en place."
```
