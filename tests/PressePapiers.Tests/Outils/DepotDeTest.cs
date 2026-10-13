using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Tests.Outils;

/// <summary>
/// Dépôt prêt à l'emploi sur une base et un dossier de données jetables, avec une horloge que le test règle.
/// L'horloge démarre le 13/10/2026 à 08:00 UTC.
/// </summary>
public sealed class DepotDeTest : IDisposable
{
    private readonly DossierTemporaire _racine = new();

    /// <summary>Crée le dossier de données, la base et le dépôt.</summary>
    /// <param name="journal">Journal du dépôt ; <see cref="JournalNul"/> par défaut.</param>
    /// <param name="creerDossiers">Faux pour laisser le dossier des images absent (le dépôt doit alors le créer).</param>
    public DepotDeTest(IJournal? journal = null, bool creerDossiers = true)
    {
        Dossiers = new DossierDonnees(_racine.Fichier("donnees"));
        if (creerDossiers)
        {
            Dossiers.CreerDossiers();
        }
        else
        {
            Directory.CreateDirectory(Dossiers.Racine);
        }

        Base = BaseHistorique.Ouvrir(Dossiers.Base);
        Horloge = new FakeTimeProvider(new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero));
        Depot = new DepotHistorique(Base, Dossiers, Horloge, journal ?? JournalNul.Instance);
    }

    /// <summary>Emplacements des données du test.</summary>
    public DossierDonnees Dossiers { get; }

    /// <summary>Base ouverte.</summary>
    public BaseHistorique Base { get; }

    /// <summary>Connexion de la base, pour les contrôles en SQL direct.</summary>
    public SqliteConnection Connexion => Base.Connexion;

    /// <summary>Horloge du dépôt, que le test avance.</summary>
    public FakeTimeProvider Horloge { get; }

    /// <summary>Dépôt testé.</summary>
    public DepotHistorique Depot { get; }

    /// <summary>
    /// Insère un élément texte directement en SQL, sans passer par le dépôt : on choisit la date, et le nom du PNG
    /// (même invalide) qu'une base abîmée pourrait contenir. L'empreinte dérive du texte, donc deux textes différents
    /// ont deux empreintes différentes. Renvoie l'identifiant de la ligne.
    /// </summary>
    public long InsererElement(long ongletId, string texte, DateTimeOffset? date = null, string? imageFichier = null)
    {
        using var commande = Connexion.CreateCommand();
        commande.CommandText = """
            INSERT INTO element (onglet_id, type, texte, texte_recherche, image_fichier, empreinte, date_copie, date_usage, taille)
            VALUES ($onglet, 1, $texte, $texte, $image, $empreinte, $date, $date, 1);
            SELECT last_insert_rowid();
            """;
        commande.Parameters.AddWithValue("$onglet", ongletId);
        commande.Parameters.AddWithValue("$texte", texte);
        commande.Parameters.AddWithValue("$image", (object?)imageFichier ?? DBNull.Value);
        commande.Parameters.AddWithValue("$empreinte", System.Text.Encoding.UTF8.GetBytes(texte));
        commande.Parameters.AddWithValue("$date", (date ?? Horloge.GetUtcNow()).ToString("O", CultureInfo.InvariantCulture));
        return Convert.ToInt64(commande.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>Noms des fichiers présents dans le dossier des images, triés.</summary>
    public string[] NomsImages() =>
        Directory.Exists(Dossiers.Images)
            ? [.. Directory.GetFiles(Dossiers.Images).Select(chemin => Path.GetFileName(chemin)).Order()]
            : [];

    /// <inheritdoc />
    public void Dispose()
    {
        Base.Dispose();
        _racine.Dispose();
    }
}
