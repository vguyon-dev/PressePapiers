using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// Les PNG du dossier des images : écriture sans fichier partiel, suppression tolérante et ménage des fichiers que
/// plus aucune ligne de la base ne référence. Sûre entre fils.
/// </summary>
internal sealed class FichiersImages(DossierDonnees dossiers, IJournal journal)
{
    private const string Extension = ".png";
    private const string ExtensionProvisoire = ".png.tmp";

    // Noms écrits ou en cours d'écriture dont la ligne n'est peut-être pas encore en base : le ménage les laisse en place.
    private readonly HashSet<string> _enCours = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Écrit un PNG sous un nom neuf et renvoie ce nom. Le fichier est d'abord écrit sous un nom provisoire puis déplacé :
    /// un échec au milieu de l'écriture ne laisse aucun fichier partiel. Le nom reste « en cours » jusqu'à
    /// <see cref="Terminer"/>, que l'appelant exécute une fois la ligne enregistrée (ou l'écriture abandonnée).
    /// </summary>
    public string Ecrire(byte[] png)
    {
        Directory.CreateDirectory(dossiers.Images);
        var nom = Guid.NewGuid().ToString("N") + Extension;
        lock (_enCours)
        {
            _enCours.Add(nom);
        }

        var chemin = Path.Combine(dossiers.Images, nom);
        var provisoire = chemin + ".tmp";
        try
        {
            File.WriteAllBytes(provisoire, png);
            File.Move(provisoire, chemin);
            return nom;
        }
        catch
        {
            SupprimerFichier(provisoire);
            Terminer(nom);
            throw;
        }
    }

    /// <summary>Le PNG écrit par <see cref="Ecrire"/> est désormais référencé par la base (ou abandonné) : le ménage peut le juger.</summary>
    public void Terminer(string nom)
    {
        lock (_enCours)
        {
            _enCours.Remove(nom);
        }
    }

    /// <summary>
    /// Supprime des PNG par leur nom. Un fichier déjà absent n'est pas une erreur, un fichier verrouillé est noté au
    /// journal et laissé en place. Seul le nom compte : jamais un chemin venu d'une base abîmée.
    /// </summary>
    public void Supprimer(IEnumerable<string> noms)
    {
        foreach (var nom in noms)
        {
            if (!string.IsNullOrEmpty(nom))
            {
                SupprimerFichier(Path.Combine(dossiers.Images, Path.GetFileName(nom)), avertir: true);
            }
        }
    }

    /// <summary>
    /// Supprime les fichiers <c>.png</c> placés directement dans le dossier des images et absents de
    /// <paramref name="references"/>, ainsi que les <c>.png.tmp</c> d'une écriture interrompue (arrêt brutal entre la
    /// création du fichier provisoire et son déplacement) ; rien d'autre n'est touché (autres extensions, sous-dossiers).
    /// Les PNG et leurs fichiers provisoires en cours d'écriture sont épargnés. Renvoie le nombre de fichiers supprimés
    /// et note ce nombre au journal.
    /// </summary>
    public int SupprimerOrphelines(IEnumerable<string> references)
    {
        if (!Directory.Exists(dossiers.Images))
        {
            return 0;
        }

        var connus = new HashSet<string>(references.Select(r => Path.GetFileName(r)), StringComparer.OrdinalIgnoreCase);
        var supprimes = 0;
        foreach (var chemin in Directory.EnumerateFiles(dossiers.Images))
        {
            var nom = Path.GetFileName(chemin);
            if (!EstAbandonne(nom, connus))
            {
                continue;
            }

            if (SupprimerFichier(chemin, avertir: true))
            {
                supprimes++;
            }
        }

        if (supprimes > 0)
        {
            journal.Info($"{supprimes} image(s) orpheline(s) supprimée(s) du dossier des images.");
        }

        return supprimes;
    }

    // Un PNG que la base ne référence pas et qu'aucune écriture n'a en main ; un provisoire dont le PNG final n'est pas
    // en cours d'écriture. Le filtre de l'énumération accepterait aussi « .png2 » (noms courts 8.3) : les extensions
    // se vérifient ici, en entier.
    private bool EstAbandonne(string nom, HashSet<string> connus)
    {
        if (nom.EndsWith(ExtensionProvisoire, StringComparison.OrdinalIgnoreCase))
        {
            return !EstEnCours(nom[..^".tmp".Length]);
        }

        return string.Equals(Path.GetExtension(nom), Extension, StringComparison.OrdinalIgnoreCase)
               && !connus.Contains(nom)
               && !EstEnCours(nom);
    }

    private bool EstEnCours(string nom)
    {
        lock (_enCours)
        {
            return _enCours.Contains(nom);
        }
    }

    // Vrai si le fichier a bien été supprimé. Absent : rien à faire, donc faux ; verrouillé : noté, laissé en place.
    private bool SupprimerFichier(string chemin, bool avertir = false)
    {
        try
        {
            if (!File.Exists(chemin))
            {
                return false;
            }

            File.Delete(chemin);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (avertir)
            {
                journal.Avertissement($"Image non supprimée : {chemin}", exception);
            }

            return false;
        }
    }
}
