using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Recherche;

namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// Dépôt des onglets et des éléments de l'historique : ajout avec dédoublonnage dans Temporaire, nommage, suppression,
/// purge, plafond de Temporaire, index plein texte et table <c>meta</c>. Sûr entre fils : la connexion SQLite, qui ne l'est
/// pas, sert elle-même de verrou, partagé avec <see cref="Recherche.MoteurRecherche"/>. Les dates sont écrites en UTC au format « O » pour que leur tri par texte soit chronologique.
/// </summary>
public sealed class DepotHistorique
{
    private const string NomTemporaire = "Temporaire";

    /// <summary>Recherche d'un doublon dans Temporaire ; interne pour qu'un test vérifie qu'elle passe par l'index des empreintes.</summary>
    internal const string RequeteDoublon =
        """
        SELECT MAX(id) FROM element
        WHERE onglet_id = $onglet AND empreinte = $empreinte AND type BETWEEN $premier AND $dernier
        """;

    // La connexion elle-même : MoteurRecherche la verrouille aussi, donc une recherche n'utilise jamais la connexion
    // pendant une écriture du dépôt faite sur un autre fil (la capture).
    private readonly object _verrou;
    private readonly SqliteConnection _connexion;
    private readonly DossierDonnees _dossiers;
    private readonly TimeProvider _horloge;
    private readonly IJournal _journal;

    /// <summary>Crée le dépôt sur une base ouverte et y crée l'onglet Temporaire s'il n'existe pas encore.</summary>
    public DepotHistorique(BaseHistorique baseHistorique, DossierDonnees dossiers, TimeProvider horloge, IJournal journal)
    {
        ArgumentNullException.ThrowIfNull(baseHistorique);
        ArgumentNullException.ThrowIfNull(dossiers);
        ArgumentNullException.ThrowIfNull(horloge);
        ArgumentNullException.ThrowIfNull(journal);

        _connexion = baseHistorique.Connexion;
        _verrou = _connexion;
        _dossiers = dossiers;
        _horloge = horloge;
        _journal = journal;
        OngletTemporaire = LireOuCreerTemporaire();
    }

    /// <summary>Onglet Temporaire, créé à la première ouverture (nom « Temporaire », ordre 0).</summary>
    public Onglet OngletTemporaire { get; }

    /// <summary>
    /// Après toute écriture réussie ; peut être levé sur n'importe quel fil, une fois le verrou du dépôt libéré
    /// (un abonné peut donc rappeler le dépôt). Un abonné qui lève une exception est noté au journal et n'arrête
    /// ni l'écriture ni les autres abonnés.
    /// </summary>
    public event Action? Modifie;

    /// <summary>Onglets par ordre, Temporaire en premier.</summary>
    public IReadOnlyList<Onglet> ListerOnglets()
    {
        lock (_verrou)
        {
            return LireOnglets(null);
        }
    }

    /// <summary>Crée un onglet à la fin ; le nom ne peut pas être vide.</summary>
    /// <exception cref="ArgumentException">Le nom est vide ou ne contient que des espaces.</exception>
    public Onglet CreerOnglet(string nom)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nom);

        Onglet cree;
        lock (_verrou)
        {
            // Une seule instruction calcule l'ordre et insère : deux créations ne peuvent pas obtenir le même ordre.
            Executer(
                "INSERT INTO onglet (nom, ordre, est_temporaire) SELECT $nom, COALESCE(MAX(ordre), 0) + 1, 0 FROM onglet",
                null, ("$nom", nom.Trim()));
            var id = Convert.ToInt64(Scalaire("SELECT last_insert_rowid()", null), CultureInfo.InvariantCulture);
            cree = LireOnglet(id)!;
        }

        Notifier();
        return cree;
    }

    /// <summary>Renomme un onglet.</summary>
    /// <exception cref="ArgumentException">Le nom est vide, ou l'onglet n'existe pas.</exception>
    /// <exception cref="InvalidOperationException">L'onglet est Temporaire.</exception>
    public void RenommerOnglet(long ongletId, string nom)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nom);

        lock (_verrou)
        {
            ExigerOngletModifiable(ongletId, "renommé");
            Executer("UPDATE onglet SET nom = $nom WHERE id = $id", null, ("$nom", nom.Trim()), ("$id", ongletId));
        }

        Notifier();
    }

    /// <summary>
    /// Place un onglet à une position de la liste, Temporaire valant 0 : une position inférieure à 1 donne la première
    /// place après Temporaire, une position trop grande la dernière. Les ordres sont renumérotés de 1 à n sans trou.
    /// </summary>
    /// <exception cref="ArgumentException">L'onglet n'existe pas.</exception>
    /// <exception cref="InvalidOperationException">L'onglet est Temporaire, qui reste toujours en tête.</exception>
    public void DeplacerOnglet(long ongletId, int nouvelOrdre)
    {
        lock (_verrou)
        {
            ExigerOngletModifiable(ongletId, "déplacé");

            // On sort l'onglet de la file des onglets utilisateur, puis on le réinsère à sa place avant de tout renuméroter.
            var file = LireOnglets(null).Where(o => !o.EstTemporaire && o.Id != ongletId).Select(o => o.Id).ToList();
            file.Insert(Math.Clamp(nouvelOrdre - 1, 0, file.Count), ongletId);

            using var transaction = _connexion.BeginTransaction();
            for (var i = 0; i < file.Count; i++)
            {
                Executer("UPDATE onglet SET ordre = $ordre WHERE id = $id AND ordre <> $ordre", transaction,
                         ("$ordre", i + 1), ("$id", file[i]));
            }

            transaction.Commit();
        }

        Notifier();
    }

    /// <summary>
    /// Supprime un onglet, ses éléments et leurs PNG ; sans effet si l'onglet n'existe pas.
    /// Les lignes partent d'abord (transaction), les fichiers ensuite : un PNG orphelin est sans conséquence,
    /// une ligne sans PNG ne le serait pas.
    /// </summary>
    /// <exception cref="InvalidOperationException">L'onglet est Temporaire.</exception>
    public void SupprimerOnglet(long ongletId)
    {
        lock (_verrou)
        {
            var onglet = LireOnglet(ongletId);
            if (onglet is null)
            {
                return;
            }

            if (onglet.EstTemporaire)
            {
                throw new InvalidOperationException("L'onglet Temporaire ne peut pas être supprimé.");
            }

            var images = new List<string>();
            using (var transaction = _connexion.BeginTransaction())
            {
                SupprimerElementsDe(ongletId, transaction, images);
                Executer("DELETE FROM onglet WHERE id = $id", transaction, ("$id", ongletId));
                transaction.Commit();
            }

            SupprimerFichiersImages(images);
        }

        Notifier();
    }

    /// <summary>Nombre d'éléments d'un onglet.</summary>
    public int CompterElements(long ongletId)
    {
        lock (_verrou)
        {
            return Compter(ongletId);
        }
    }

    /// <summary>Éléments d'un onglet, par DateUsage décroissante puis Id décroissant (ordre stable à date égale).</summary>
    public IReadOnlyList<Element> ListerElements(long ongletId)
    {
        lock (_verrou)
        {
            return LireElements(null, "WHERE onglet_id = $onglet", ("$onglet", ongletId));
        }
    }

    /// <summary>Lit un élément, ou null s'il n'existe pas.</summary>
    public Element? Lire(long elementId)
    {
        lock (_verrou)
        {
            return LireElement(elementId);
        }
    }

    /// <summary>
    /// Ajoute une copie à Temporaire, écrit le PNG d'une image et renvoie l'élément créé ou remonté.
    /// Un contenu identique déjà présent dans Temporaire (même empreinte, même sorte de contenu) n'est pas copié :
    /// l'élément existant passe en tête et reçoit la nouvelle application d'origine, si elle est connue.
    /// Le texte brut, avec ou sans mise en forme, est une seule sorte de contenu ; le premier enregistrement est gardé tel quel.
    /// </summary>
    /// <exception cref="ArgumentException">Le contenu principal du type demandé est absent.</exception>
    public Element AjouterCopie(ContenuCopie contenu)
    {
        ArgumentNullException.ThrowIfNull(contenu);

        // Hors du verrou : hacher un gros contenu ne doit pas bloquer les autres opérations.
        var empreinte = Empreinte.Calculer(contenu);
        var texte = contenu.Type == TypeElement.Fichiers ? string.Join('\n', contenu.Fichiers!) : contenu.Texte;

        Element resultat;
        lock (_verrou)
        {
            var maintenant = Maintenant();
            var doublon = TrouverDoublon(empreinte, contenu.Type);
            resultat = doublon is null
                ? Inserer(contenu, empreinte, texte, maintenant)
                : Remonter(doublon.Value, contenu.Application, maintenant);
        }

        Notifier();
        return resultat;
    }

    /// <summary>
    /// Donne un nom à un élément et le range dans un onglet autre que Temporaire ; vaut aussi pour renommer
    /// ou déplacer un élément déjà nommé. Le nom est débarrassé des espaces de bord ; sa date d'usage ne change pas.
    /// </summary>
    /// <exception cref="ArgumentException">Le nom est vide, ou l'élément ou l'onglet n'existe pas.</exception>
    /// <exception cref="InvalidOperationException">L'onglet demandé est Temporaire.</exception>
    public Element Nommer(long elementId, string nom, long ongletId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nom);

        Element nomme;
        lock (_verrou)
        {
            var onglet = LireOnglet(ongletId)
                ?? throw new ArgumentException($"L'onglet {ongletId} n'existe pas.", nameof(ongletId));
            if (onglet.EstTemporaire)
            {
                throw new InvalidOperationException("Un élément nommé ne peut pas être rangé dans Temporaire.");
            }

            var courant = LireElement(elementId)
                ?? throw new ArgumentException($"L'élément {elementId} n'existe pas.", nameof(elementId));
            var nomPropre = nom.Trim();

            Executer(
                "UPDATE element SET nom = $nom, onglet_id = $onglet, texte_recherche = $recherche WHERE id = $id",
                null,
                ("$nom", nomPropre), ("$onglet", ongletId), ("$id", elementId),
                ("$recherche", TexteRecherche(nomPropre, courant.Type, courant.Texte, courant.Application)));
            nomme = courant with { Nom = nomPropre, OngletId = ongletId };
        }

        Notifier();
        return nomme;
    }

    /// <summary>
    /// Met à jour la date de dernier usage d'un élément, qui passe en tête de son onglet ; sans effet s'il n'existe pas.
    /// </summary>
    public void MarquerUtilise(long elementId)
    {
        lock (_verrou)
        {
            var ongletId = Scalaire("SELECT onglet_id FROM element WHERE id = $id", null, ("$id", elementId));
            if (ongletId is null)
            {
                return;
            }

            var date = DateUsageSuivante(Convert.ToInt64(ongletId, CultureInfo.InvariantCulture), Maintenant(), null);
            Executer("UPDATE element SET date_usage = $date WHERE id = $id", null, ("$date", EnTexte(date)), ("$id", elementId));
        }

        Notifier();
    }

    /// <summary>
    /// Supprime un élément et son PNG ; sans effet si l'élément n'existe pas. Un PNG déjà absent n'est pas une erreur,
    /// un PNG verrouillé est noté au journal et laissé en place : la ligne part dans les deux cas.
    /// </summary>
    public void Supprimer(long elementId)
    {
        bool supprime;
        lock (_verrou)
        {
            var image = Scalaire("SELECT image_fichier FROM element WHERE id = $id", null, ("$id", elementId));
            supprime = Executer("DELETE FROM element WHERE id = $id", null, ("$id", elementId)) > 0;
            if (supprime && image is string nom)
            {
                SupprimerFichiersImages([nom]);
            }
        }

        if (supprime)
        {
            Notifier();
        }
    }

    /// <summary>Chemin complet du PNG d'un élément, toujours dans le dossier des images.</summary>
    /// <exception cref="InvalidOperationException">L'élément n'a pas d'image.</exception>
    public string CheminImage(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (string.IsNullOrEmpty(element.ImageFichier))
        {
            throw new InvalidOperationException("Cet élément n'a pas d'image.");
        }

        // Même précaution qu'à la suppression : seul le nom du fichier compte, jamais un chemin venu de la base.
        return Path.Combine(_dossiers.Images, Path.GetFileName(element.ImageFichier));
    }

    /// <summary>
    /// Supprime les éléments de Temporaire et leurs PNG ; renvoie leur nombre.
    /// Les éléments nommés, rangés dans d'autres onglets, ne sont jamais touchés.
    /// </summary>
    public int PurgerTemporaire()
    {
        int supprimes;
        lock (_verrou)
        {
            var images = new List<string>();
            using (var transaction = _connexion.BeginTransaction())
            {
                supprimes = SupprimerElementsDe(OngletTemporaire.Id, transaction, images);
                transaction.Commit();
            }

            SupprimerFichiersImages(images);
        }

        if (supprimes > 0)
        {
            Notifier();
        }

        return supprimes;
    }

    /// <summary>
    /// Supprime les plus anciens éléments de Temporaire (par date d'usage, puis par Id) au-delà du plafond ;
    /// un plafond de 0 ou moins est désactivé. Renvoie le nombre d'éléments supprimés.
    /// </summary>
    public int AppliquerPlafond(int plafond)
    {
        if (plafond <= 0)
        {
            return 0;
        }

        int supprimes;
        lock (_verrou)
        {
            var exces = Compter(OngletTemporaire.Id) - plafond;
            if (exces <= 0)
            {
                return 0;
            }

            var images = new List<string>();
            using (var transaction = _connexion.BeginTransaction())
            {
                // Les plus anciens sont ceux qui viennent en dernier dans ListerElements : même tri, à l'envers.
                const string plusAnciens =
                    "SELECT id FROM element WHERE onglet_id = $onglet ORDER BY date_usage ASC, id ASC LIMIT $exces";
                using (var commande = Preparer(
                           $"SELECT image_fichier FROM element WHERE image_fichier IS NOT NULL AND id IN ({plusAnciens})",
                           transaction, ("$onglet", OngletTemporaire.Id), ("$exces", exces)))
                using (var lecteur = commande.ExecuteReader())
                {
                    while (lecteur.Read())
                    {
                        images.Add(lecteur.GetString(0));
                    }
                }

                supprimes = Executer($"DELETE FROM element WHERE id IN ({plusAnciens})", transaction,
                                     ("$onglet", OngletTemporaire.Id), ("$exces", exces));
                transaction.Commit();
            }

            SupprimerFichiersImages(images);
        }

        Notifier();
        return supprimes;
    }

    /// <summary>
    /// Reconstruit l'index plein texte à partir de la table <c>element</c> (réparation d'un index désynchronisé).
    /// </summary>
    public void ReconstruireIndex()
    {
        lock (_verrou)
        {
            Executer("INSERT INTO element_fts(element_fts) VALUES ('rebuild')", null);
        }

        Notifier();
    }

    /// <summary>Lit une valeur de la table <c>meta</c> ; null si la clé n'existe pas.</summary>
    public string? LireMeta(string cle)
    {
        ArgumentException.ThrowIfNullOrEmpty(cle);

        lock (_verrou)
        {
            return Scalaire("SELECT valeur FROM meta WHERE cle = $cle", null, ("$cle", cle)) as string;
        }
    }

    /// <summary>Écrit une valeur dans la table <c>meta</c>, en remplaçant celle de la clé si elle existe.</summary>
    public void EcrireMeta(string cle, string valeur)
    {
        ArgumentException.ThrowIfNullOrEmpty(cle);
        ArgumentNullException.ThrowIfNull(valeur);

        lock (_verrou)
        {
            Executer(
                "INSERT INTO meta (cle, valeur) VALUES ($cle, $valeur) ON CONFLICT(cle) DO UPDATE SET valeur = excluded.valeur",
                null, ("$cle", cle), ("$valeur", valeur));
        }

        Notifier();
    }

    // ---- Onglets : lecture ----

    private Onglet LireOuCreerTemporaire()
    {
        lock (_verrou)
        {
            using var transaction = _connexion.BeginTransaction();
            var existant = LireOnglets(transaction, "WHERE est_temporaire = 1").FirstOrDefault();
            if (existant is not null)
            {
                return existant;
            }

            Executer("INSERT INTO onglet (nom, ordre, est_temporaire) VALUES ($nom, 0, 1)", transaction, ("$nom", NomTemporaire));
            var id = Convert.ToInt64(Scalaire("SELECT last_insert_rowid()", transaction), CultureInfo.InvariantCulture);
            transaction.Commit();
            return new Onglet(id, NomTemporaire, 0, true);
        }
    }

    private Onglet? LireOnglet(long id) => LireOnglets(null, "WHERE id = $id", ("$id", id)).FirstOrDefault();

    // La clause est toujours un texte écrit ici, jamais une saisie : les valeurs passent par des paramètres.
    private List<Onglet> LireOnglets(SqliteTransaction? transaction, string clause = "", params (string Nom, object? Valeur)[] parametres)
    {
        using var commande = Preparer($"SELECT id, nom, ordre, est_temporaire FROM onglet {clause} ORDER BY ordre, id", transaction, parametres);
        using var lecteur = commande.ExecuteReader();
        var onglets = new List<Onglet>();
        while (lecteur.Read())
        {
            onglets.Add(new Onglet(lecteur.GetInt64(0), lecteur.GetString(1), lecteur.GetInt32(2), lecteur.GetInt64(3) != 0));
        }

        return onglets;
    }

    // Vérifie qu'un onglet existe et n'est pas Temporaire, avant de le renommer ou de le déplacer.
    private void ExigerOngletModifiable(long ongletId, string action)
    {
        var onglet = LireOnglet(ongletId)
            ?? throw new ArgumentException($"L'onglet {ongletId} n'existe pas.", nameof(ongletId));
        if (onglet.EstTemporaire)
        {
            throw new InvalidOperationException($"L'onglet Temporaire ne peut pas être {action}.");
        }
    }

    // ---- Éléments : lecture ----

    private int Compter(long ongletId) =>
        Convert.ToInt32(
            Scalaire("SELECT COUNT(*) FROM element WHERE onglet_id = $onglet", null, ("$onglet", ongletId)),
            CultureInfo.InvariantCulture);

    private Element? LireElement(long id) => LireElements(null, "WHERE id = $id", ("$id", id)).FirstOrDefault();

    // Même clause que pour les onglets : un texte écrit ici, les valeurs passent par des paramètres.
    private List<Element> LireElements(SqliteTransaction? transaction, string clause, params (string Nom, object? Valeur)[] parametres)
    {
        using var commande = Preparer(
            $"SELECT {LecteurElement.Colonnes} FROM element {clause} ORDER BY date_usage DESC, id DESC", transaction, parametres);
        using var lecteur = commande.ExecuteReader();
        var elements = new List<Element>();
        while (lecteur.Read())
        {
            elements.Add(LecteurElement.Lire(lecteur));
        }

        return elements;
    }

    // ---- Éléments : ajout et dédoublonnage ----

    // Dates toujours en UTC et en texte « O » : le tri par texte de SQLite suit alors l'ordre chronologique.
    private DateTimeOffset Maintenant() => _horloge.GetUtcNow().ToUniversalTime();

    private static string EnTexte(DateTimeOffset date) => date.ToString("O", CultureInfo.InvariantCulture);

    // Texte brut avec ou sans mise en forme : une seule famille de contenu pour le dédoublonnage. Une liste de fichiers
    // et un texte qui reproduit ses chemins ont la même empreinte mais restent deux éléments.
    private long? TrouverDoublon(byte[] empreinte, TypeElement type)
    {
        var (premier, dernier) = type switch
        {
            TypeElement.Texte or TypeElement.MisEnForme => ((int)TypeElement.Texte, (int)TypeElement.MisEnForme),
            _ => ((int)type, (int)type),
        };

        // Pas de tri : avec un ORDER BY, SQLite parcourrait l'onglet dans l'ordre des dates au lieu d'utiliser l'index
        // des empreintes. Il n'y a normalement qu'un doublon ; s'il y en avait plusieurs, on prend le plus récent par Id.
        var id = Scalaire(
            RequeteDoublon, null,
            ("$onglet", OngletTemporaire.Id), ("$empreinte", empreinte), ("$premier", premier), ("$dernier", dernier));
        return id is long trouve ? trouve : null;
    }

    // Date d'usage d'un élément qui passe en tête : l'heure courante, mais jamais avant ni à égalité avec le plus récent
    // de l'onglet (rafale dans la même milliseconde, horloge qui recule). L'ordre d'usage ne dépend ainsi jamais d'une égalité.
    private DateTimeOffset DateUsageSuivante(long ongletId, DateTimeOffset maintenant, SqliteTransaction? transaction)
    {
        var plusRecente = Scalaire("SELECT MAX(date_usage) FROM element WHERE onglet_id = $onglet", transaction, ("$onglet", ongletId));
        if (plusRecente is not string texte)
        {
            return maintenant;
        }

        var derniere = DateTimeOffset.ParseExact(texte, "O", CultureInfo.InvariantCulture);
        return maintenant > derniere ? maintenant : derniere.AddTicks(1);
    }

    private Element Inserer(ContenuCopie contenu, byte[] empreinte, string? texte, DateTimeOffset maintenant)
    {
        // Le PNG est écrit avant la ligne : un échec de l'insertion retire le fichier, jamais l'inverse.
        string? imageFichier = null;
        if (contenu.Type == TypeElement.Image)
        {
            Directory.CreateDirectory(_dossiers.Images);
            imageFichier = Guid.NewGuid().ToString("N") + ".png";
            File.WriteAllBytes(Path.Combine(_dossiers.Images, imageFichier), contenu.ImagePng!);
        }

        try
        {
            var dateUsage = DateUsageSuivante(OngletTemporaire.Id, maintenant, null);
            var taille = contenu.Type == TypeElement.Image ? contenu.ImagePng!.Length : Encoding.UTF8.GetByteCount(texte!);

            Executer(
                """
                INSERT INTO element (onglet_id, nom, type, texte, texte_recherche, rtf, html, image_fichier, miniature,
                                     empreinte, application, date_copie, date_usage, taille)
                VALUES ($onglet, NULL, $type, $texte, $recherche, $rtf, $html, $image, $miniature,
                        $empreinte, $application, $copie, $usage, $taille)
                """,
                null,
                ("$onglet", OngletTemporaire.Id), ("$type", (int)contenu.Type), ("$texte", texte),
                ("$recherche", TexteRecherche(null, contenu.Type, texte, contenu.Application)),
                ("$rtf", contenu.Rtf), ("$html", contenu.Html), ("$image", imageFichier), ("$miniature", contenu.Miniature),
                ("$empreinte", empreinte), ("$application", contenu.Application),
                ("$copie", EnTexte(maintenant)), ("$usage", EnTexte(dateUsage)), ("$taille", taille));
            var id = Convert.ToInt64(Scalaire("SELECT last_insert_rowid()", null), CultureInfo.InvariantCulture);

            return new Element
            {
                Id = id,
                OngletId = OngletTemporaire.Id,
                Type = contenu.Type,
                Texte = texte,
                Rtf = contenu.Rtf,
                Html = contenu.Html,
                ImageFichier = imageFichier,
                Miniature = contenu.Miniature,
                Empreinte = empreinte,
                Application = contenu.Application,
                DateCopie = maintenant,
                DateUsage = dateUsage,
                Taille = taille,
            };
        }
        catch
        {
            if (imageFichier is not null)
            {
                SupprimerFichiersImages([imageFichier]);
            }

            throw;
        }
    }

    // Un doublon n'est pas recopié : sa date d'usage avance, et son application d'origine est remplacée si la nouvelle est connue.
    // Le texte de recherche porte l'application, il est donc recalculé avec elle.
    private Element Remonter(long id, string? application, DateTimeOffset maintenant)
    {
        var courant = LireElement(id)!;
        var dateUsage = DateUsageSuivante(courant.OngletId, maintenant, null);

        if (application is null || application == courant.Application)
        {
            // Le texte de recherche n'est pas touché : le déclencheur de l'index ne réindexe pas un gros texte pour une date.
            Executer("UPDATE element SET date_usage = $usage WHERE id = $id", null, ("$usage", EnTexte(dateUsage)), ("$id", id));
            return courant with { DateUsage = dateUsage };
        }

        Executer(
            "UPDATE element SET date_usage = $usage, application = $application, texte_recherche = $recherche WHERE id = $id",
            null,
            ("$usage", EnTexte(dateUsage)), ("$application", application), ("$id", id),
            ("$recherche", TexteRecherche(courant.Nom, courant.Type, courant.Texte, application)));
        return courant with { DateUsage = dateUsage, Application = application };
    }

    // Pour une liste de fichiers, le texte est la liste des chemins : on la passe comme telle, pas aussi comme texte.
    private static string TexteRecherche(string? nom, TypeElement type, string? texte, string? application) =>
        type == TypeElement.Fichiers
            ? Normalisation.TexteRecherche(nom, null, texte?.Split('\n', StringSplitOptions.RemoveEmptyEntries), application)
            : Normalisation.TexteRecherche(nom, texte, null, application);

    // ---- Suppression partagée entre les onglets et la purge de Temporaire ----

    // Supprime les éléments d'un onglet dans la transaction donnée et ajoute les noms de leurs PNG à la liste.
    // Les déclencheurs de la base tiennent l'index plein texte à jour ligne par ligne.
    private int SupprimerElementsDe(long ongletId, SqliteTransaction transaction, List<string> images)
    {
        using (var commande = Preparer(
                   "SELECT image_fichier FROM element WHERE onglet_id = $onglet AND image_fichier IS NOT NULL",
                   transaction, ("$onglet", ongletId)))
        using (var lecteur = commande.ExecuteReader())
        {
            while (lecteur.Read())
            {
                images.Add(lecteur.GetString(0));
            }
        }

        return Executer("DELETE FROM element WHERE onglet_id = $onglet", transaction, ("$onglet", ongletId));
    }

    // Un PNG déjà absent n'est pas une erreur ; un PNG verrouillé est noté au journal et laissé en place.
    private void SupprimerFichiersImages(IEnumerable<string> noms)
    {
        foreach (var nom in noms)
        {
            if (string.IsNullOrEmpty(nom))
            {
                continue;
            }

            // GetFileName écarte tout chemin qu'une base abîmée aurait pu y mettre : on ne supprime que dans le dossier des images.
            var chemin = Path.Combine(_dossiers.Images, Path.GetFileName(nom));
            try
            {
                File.Delete(chemin);
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _journal.Avertissement($"Image non supprimée : {chemin}", exception);
            }
        }
    }

    // ---- Événement et accès SQL ----

    // Appelé hors du verrou. Chaque abonné est isolé : une exception de l'un n'empêche ni l'écriture ni les autres.
    private void Notifier()
    {
        var abonnes = Modifie;
        if (abonnes is null)
        {
            return;
        }

        foreach (var abonne in abonnes.GetInvocationList().Cast<Action>())
        {
            try
            {
                abonne();
            }
            catch (Exception exception)
            {
                _journal.Erreur("Un abonné de l'événement Modifie a levé une exception.", exception);
            }
        }
    }

    private SqliteCommand Preparer(string sql, SqliteTransaction? transaction, params (string Nom, object? Valeur)[] parametres)
    {
        var commande = _connexion.CreateCommand();
        commande.Transaction = transaction;
        commande.CommandText = sql;
        foreach (var (nom, valeur) in parametres)
        {
            commande.Parameters.AddWithValue(nom, valeur ?? DBNull.Value);
        }

        return commande;
    }

    private int Executer(string sql, SqliteTransaction? transaction, params (string Nom, object? Valeur)[] parametres)
    {
        using var commande = Preparer(sql, transaction, parametres);
        return commande.ExecuteNonQuery();
    }

    private object? Scalaire(string sql, SqliteTransaction? transaction, params (string Nom, object? Valeur)[] parametres)
    {
        using var commande = Preparer(sql, transaction, parametres);
        return commande.ExecuteScalar();
    }
}
