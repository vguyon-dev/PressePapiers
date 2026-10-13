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

    private static readonly object VerrouEcriture = new();

    private static readonly JsonDocumentOptions OptionsDocument = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
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

    /// <summary>
    /// Lit les réglages. Les valeurs par défaut si le fichier est absent, vide ou n'est pas du JSON (avertissement au journal).
    /// Une valeur inconnue ou inutilisable (texte d'énumération inconnu, type faux, nombre absurde, null) ne perd que sa propre
    /// propriété, qui reprend son défaut : les autres réglages du fichier sont gardés. Un seul avertissement liste les corrections.
    /// </summary>
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
            using var document = JsonDocument.Parse(json, OptionsDocument);
            if (document.RootElement.ValueKind == JsonValueKind.Null)
            {
                _journal.Avertissement($"Fichier de réglages vide ({_cheminFichier}) : valeurs par défaut.");
                return new Parametres();
            }

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Le fichier de réglages doit contenir un objet JSON.");
            }

            return Lire(document.RootElement);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _journal.Avertissement($"Fichier de réglages illisible ({_cheminFichier}) : valeurs par défaut.", ex);
            return new Parametres();
        }
    }

    /// <summary>
    /// Écrit les réglages de façon atomique : fichier temporaire au nom unique (un reste verrouillé d'une coupure ne
    /// bloque rien), puis <c>File.Move(..., true)</c>. Les enregistrements simultanés du processus sont mis en file :
    /// Windows refuse que deux déplacements remplacent en même temps le même fichier.
    /// </summary>
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
        var temporaire = $"{_cheminFichier}.{Guid.NewGuid():N}.tmp";
        var contenu = JsonSerializer.Serialize(parametres, Options);
        lock (VerrouEcriture)
        {
            try
            {
                File.WriteAllText(temporaire, contenu, Utf8SansMarque);
                File.Move(temporaire, _cheminFichier, overwrite: true);
            }
            catch
            {
                TenterSupprimer(temporaire);
                throw;
            }
        }
    }

    // Chaque propriété est lue pour elle-même : celle qui est inutilisable reprend son défaut et sa seule valeur est perdue.
    private Parametres Lire(JsonElement racine)
    {
        var defauts = new Parametres();
        var remplacees = new List<string>();

        var lus = new Parametres
        {
            RaccourciSecours = Valeur(racine, nameof(Parametres.RaccourciSecours), defauts.RaccourciSecours, remplacees,
                                      v => !string.IsNullOrWhiteSpace(v)),
            DemarrageAvecWindows = Valeur(racine, nameof(Parametres.DemarrageAvecWindows), defauts.DemarrageAvecWindows, remplacees),
            MiseEnFormeParDefaut = Valeur(racine, nameof(Parametres.MiseEnFormeParDefaut), defauts.MiseEnFormeParDefaut, remplacees),
            PlafondTemporaire = Valeur(racine, nameof(Parametres.PlafondTemporaire), defauts.PlafondTemporaire, remplacees,
                                       v => v >= 0),
            Theme = Valeur(racine, nameof(Parametres.Theme), defauts.Theme, remplacees),
            ApplicationsChasseFixe = ListeApplications(racine, defauts.ApplicationsChasseFixe, remplacees),
            LargeurFenetre = Valeur(racine, nameof(Parametres.LargeurFenetre), defauts.LargeurFenetre, remplacees,
                                    v => v >= LargeurMinimale),
            HauteurFenetre = Valeur(racine, nameof(Parametres.HauteurFenetre), defauts.HauteurFenetre, remplacees,
                                    v => v >= HauteurMinimale),
        };

        if (remplacees.Count > 0)
        {
            _journal.Avertissement($"Réglages inutilisables dans {_cheminFichier}, corrigés un à un ({string.Join(" ; ", remplacees)}).");
        }

        return lus;
    }

    // Les éléments null d'une liste écrite à la main (["Code", null]) sont écartés ; le reste de la liste est gardé.
    private static IReadOnlyList<string> ListeApplications(JsonElement racine, IReadOnlyList<string> defaut, List<string> remplacees)
    {
        var lue = Valeur<List<string?>?>(racine, nameof(Parametres.ApplicationsChasseFixe), null, remplacees);
        if (lue is null)
        {
            return defaut;
        }

        var propres = lue.OfType<string>().ToList();
        if (propres.Count != lue.Count)
        {
            remplacees.Add($"{nameof(Parametres.ApplicationsChasseFixe)} : éléments null ignorés");
        }

        return propres;
    }

    // Valeur d'une propriété (nom cherché sans tenir compte de la casse, la dernière occurrence l'emporte comme dans
    // System.Text.Json). Absente : le défaut, sans bruit. Présente mais inutilisable : le défaut, et le nom est noté.
    private static T Valeur<T>(JsonElement racine, string nom, T defaut, List<string> remplacees, Func<T, bool>? estValide = null)
    {
        JsonElement? trouvee = null;
        foreach (var propriete in racine.EnumerateObject())
        {
            if (propriete.Name.Equals(nom, StringComparison.OrdinalIgnoreCase))
            {
                trouvee = propriete.Value;
            }
        }

        if (trouvee is null)
        {
            return defaut;
        }

        try
        {
            var valeur = trouvee.Value.Deserialize<T>(Options);
            if (valeur is not null && (estValide?.Invoke(valeur) ?? true))
            {
                return valeur;
            }
        }
        catch (JsonException)
        {
        }

        remplacees.Add($"{nom} : valeur par défaut");
        return defaut;
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
