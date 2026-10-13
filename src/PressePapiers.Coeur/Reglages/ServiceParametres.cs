using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Coeur.Reglages;

/// <summary>
/// Lecture et écriture du fichier <c>parametres.json</c>. Un fichier absent, illisible ou aux valeurs absurdes ne bloque
/// jamais le démarrage : on retombe sur les valeurs par défaut.
/// </summary>
public sealed class ServiceParametres
{
    // Le fichier se modifie à la main : énumérations en texte (refus des nombres), casse libre, commentaires tolérés.
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    private static readonly UTF8Encoding Utf8SansMarque = new(encoderShouldEmitUTF8Identifier: false);

    private const int LargeurMinimale = 400;
    private const int HauteurMinimale = 300;

    private readonly string _cheminFichier;
    private readonly IJournal _journal;

    /// <summary>Crée le service sur un fichier de réglages.</summary>
    public ServiceParametres(string cheminFichier, IJournal journal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cheminFichier);
        ArgumentNullException.ThrowIfNull(journal);
        _cheminFichier = cheminFichier;
        _journal = journal;
    }

    /// <summary>Lit les réglages ; les valeurs par défaut si le fichier est absent ou illisible (avertissement au journal).</summary>
    public Parametres Charger()
    {
        if (!File.Exists(_cheminFichier))
        {
            // Cas normal au premier lancement : pas un problème à signaler.
            _journal.Info($"Aucun fichier de réglages ({_cheminFichier}) : valeurs par défaut.");
            return new Parametres();
        }

        try
        {
            var json = File.ReadAllText(_cheminFichier, Encoding.UTF8);
            var lus = JsonSerializer.Deserialize<Parametres>(json, Options);
            if (lus is null)
            {
                _journal.Avertissement($"Fichier de réglages vide ({_cheminFichier}) : valeurs par défaut.");
                return new Parametres();
            }

            return Corriger(lus);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _journal.Avertissement($"Fichier de réglages illisible ({_cheminFichier}) : valeurs par défaut.", ex);
            return new Parametres();
        }
    }

    /// <summary>Écrit les réglages de façon atomique : fichier temporaire, puis <c>File.Move(..., true)</c>.</summary>
    public void Enregistrer(Parametres parametres)
    {
        ArgumentNullException.ThrowIfNull(parametres);

        var dossier = Path.GetDirectoryName(Path.GetFullPath(_cheminFichier));
        if (!string.IsNullOrEmpty(dossier))
        {
            Directory.CreateDirectory(dossier);
        }

        // Le fichier définitif n'est remplacé que par un fichier complet : une coupure en cours d'écriture
        // laisse l'ancien contenu intact, jamais un fichier tronqué.
        var temporaire = _cheminFichier + ".tmp";
        try
        {
            File.WriteAllText(temporaire, JsonSerializer.Serialize(parametres, Options), Utf8SansMarque);
            File.Move(temporaire, _cheminFichier, overwrite: true);
        }
        catch
        {
            TenterSupprimer(temporaire);
            throw;
        }
    }

    // Une valeur lisible mais inutilisable (null écrit à la main, plafond négatif, fenêtre minuscule)
    // est remplacée par son défaut, sans perdre le reste des réglages.
    private Parametres Corriger(Parametres lus)
    {
        var defauts = new Parametres();
        var corriges = lus with
        {
            RaccourciSecours = string.IsNullOrWhiteSpace(lus.RaccourciSecours) ? defauts.RaccourciSecours : lus.RaccourciSecours,
            ApplicationsChasseFixe = lus.ApplicationsChasseFixe is null ? defauts.ApplicationsChasseFixe : lus.ApplicationsChasseFixe,
            PlafondTemporaire = lus.PlafondTemporaire < 0 ? defauts.PlafondTemporaire : lus.PlafondTemporaire,
            LargeurFenetre = lus.LargeurFenetre < LargeurMinimale ? defauts.LargeurFenetre : lus.LargeurFenetre,
            HauteurFenetre = lus.HauteurFenetre < HauteurMinimale ? defauts.HauteurFenetre : lus.HauteurFenetre,
        };

        if (!ReferenceEquals(corriges.ApplicationsChasseFixe, lus.ApplicationsChasseFixe)
            || corriges.PlafondTemporaire != lus.PlafondTemporaire
            || corriges.LargeurFenetre != lus.LargeurFenetre
            || corriges.HauteurFenetre != lus.HauteurFenetre
            || corriges.RaccourciSecours != lus.RaccourciSecours)
        {
            _journal.Avertissement($"Valeurs de réglages inutilisables dans {_cheminFichier} : remplacées par les valeurs par défaut.");
        }

        return corriges;
    }

    private static void TenterSupprimer(string chemin)
    {
        try
        {
            File.Delete(chemin);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
