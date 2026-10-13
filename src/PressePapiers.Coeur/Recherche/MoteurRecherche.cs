using System.Text;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Coeur.Recherche;

/// <summary>
/// Recherche plein texte dans tous les onglets (spec, section 7). Aucune saisie de l'utilisateur n'est jamais lue
/// comme de la syntaxe : les mots longs passent entre guillemets doubles, les mots courts par des paramètres LIKE.
/// </summary>
public sealed class MoteurRecherche
{
    // Sous trois caractères, l'index trigram ne trouve rien : on se rabat sur LIKE.
    private const int LongueurMinimaleIndex = 3;

    // Borne le nombre de conditions SQL (et de paramètres) quelle que soit la saisie ; une phrase de plus
    // de 16 mots distincts ne sert pas à chercher, elle vient d'un collage maladroit.
    private const int NombreMaxMots = 16;

    // Longueur maximale (en unités UTF-16) d'un mot pris en compte.
    private const int LongueurMaxMot = 128;

    private readonly BaseHistorique _base;

    /// <summary>Crée le moteur sur une base ouverte.</summary>
    public MoteurRecherche(BaseHistorique baseHistorique)
    {
        ArgumentNullException.ThrowIfNull(baseHistorique);
        _base = baseHistorique;
    }

    /// <summary>
    /// Tous les mots sont requis ; un mot de 3 caractères ou plus passe par MATCH (entre guillemets doubles),
    /// un mot de 1 ou 2 caractères par LIKE avec ESCAPE ; liste vide si la requête est vide ; DateUsage décroissante,
    /// puis Id décroissant à date égale. Seuls les 16 premiers mots distincts comptent.
    /// </summary>
    /// <param name="requete">Saisie brute de l'utilisateur, normalisée ici comme l'est le texte indexé.</param>
    /// <param name="limite">Nombre maximal de résultats ; zéro ou moins donne une liste vide.</param>
    public IReadOnlyList<Element> Chercher(string requete, int limite = 500)
    {
        if (limite <= 0)
        {
            return [];
        }

        var mots = Decouper(requete);
        if (mots.Count == 0)
        {
            return [];
        }

        // La connexion est partagée avec le dépôt, qui la verrouille aussi : on ne l'utilise pas deux fois en même temps.
        // Le verrou couvre toute la vie de la commande, car la connexion tient la liste de ses commandes sans protection.
        lock (_base.Connexion)
        {
            return ChercherSousVerrou(mots, limite);
        }
    }

    private List<Element> ChercherSousVerrou(List<string> mots, int limite)
    {
        using var commande = _base.Connexion.CreateCommand();
        var conditions = new List<string>();

        var longs = mots.Where(m => NombreDeCaracteres(m) >= LongueurMinimaleIndex).ToList();
        if (longs.Count > 0)
        {
            conditions.Add("id IN (SELECT rowid FROM element_fts WHERE element_fts MATCH $match)");
            commande.Parameters.AddWithValue("$match", string.Join(" AND ", longs.Select(EntreGuillemets)));
        }

        var courts = mots.Where(m => NombreDeCaracteres(m) < LongueurMinimaleIndex).ToList();
        for (var i = 0; i < courts.Count; i++)
        {
            conditions.Add($"texte_recherche LIKE $court{i} ESCAPE '\\'");
            commande.Parameters.AddWithValue($"$court{i}", "%" + EchapperLike(courts[i]) + "%");
        }

        commande.CommandText = $"""
            SELECT {LecteurElement.Colonnes}
            FROM element
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY date_usage DESC, id DESC
            LIMIT $limite
            """;
        commande.Parameters.AddWithValue("$limite", limite);

        var resultats = new List<Element>();
        using var lecteur = commande.ExecuteReader();
        while (lecteur.Read())
        {
            resultats.Add(LecteurElement.Lire(lecteur));
        }

        return resultats;
    }

    // Mots distincts de la requête normalisée, dans l'ordre de saisie.
    // Un caractère nul est traité en séparateur : SQLite lit les textes jusqu'au premier nul et refuserait la chaîne.
    private static List<string> Decouper(string? requete) =>
        Normalisation.Normaliser(requete?.Replace('\0', ' '))
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(Raccourcir)
            .Distinct(StringComparer.Ordinal)
            .Take(NombreMaxMots)
            .ToList();

    // Un mot de plusieurs millions de caractères viendrait d'un collage accidentel et ferait travailler l'index
    // pendant des minutes ; ses premiers caractères suffisent à retrouver l'élément voulu.
    private static string Raccourcir(string mot)
    {
        if (mot.Length <= LongueurMaxMot)
        {
            return mot;
        }

        // On ne coupe pas au milieu d'une paire de substituts (un emoji).
        var fin = char.IsHighSurrogate(mot[LongueurMaxMot - 1]) ? LongueurMaxMot - 1 : LongueurMaxMot;
        return mot[..fin];
    }

    // L'index trigram compte des caractères Unicode entiers : un emoji (deux unités UTF-16) n'en vaut qu'un.
    private static int NombreDeCaracteres(string mot)
    {
        var total = 0;
        foreach (var _ in mot.EnumerateRunes())
        {
            total++;
        }

        return total;
    }

    // Une chaîne FTS5 entre guillemets n'a pas d'autre syntaxe : on double seulement le guillemet.
    private static string EntreGuillemets(string mot) => "\"" + mot.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    // L'antislash sert d'échappement (clause ESCAPE) : lui-même, % et _ deviennent des caractères ordinaires.
    private static string EchapperLike(string mot)
    {
        var resultat = new StringBuilder(mot.Length + 2);
        foreach (var c in mot)
        {
            if (c is '\\' or '%' or '_')
            {
                resultat.Append('\\');
            }

            resultat.Append(c);
        }

        return resultat.ToString();
    }
}
