using System.Globalization;
using Microsoft.Data.Sqlite;
using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// Ouverture de la base au démarrage (spec, sections 5 et 12) : copie de sécurité, ouverture, et, si la base est
/// illisible, proposition de restaurer la copie la plus récente. Rien n'est jamais écrasé sans l'accord de l'utilisateur.
/// </summary>
public static class OuvertureBase
{
    // Codes primaires de SQLite pour « base corrompue » (SQLITE_CORRUPT) et « fichier qui n'est pas une base » (SQLITE_NOTADB).
    private const int CodeCorrompue = 11;
    private const int CodePasUneBase = 26;

    /// <summary>
    /// Copie la base, l'ouvre, et gère les échecs. Renvoie null quand l'application ne peut pas démarrer : l'utilisateur
    /// en a alors été informé par <paramref name="dialogue"/> et le détail est au journal. Une copie de sécurité impossible
    /// n'empêche pas de démarrer (avertissement seulement). La restauration n'est proposée que pour une base abîmée,
    /// jamais pour un autre échec (accès refusé, base verrouillée, SQLite trop ancienne).
    /// </summary>
    /// <param name="dossiers">Emplacements des données (base et copies).</param>
    /// <param name="horloge">Source de l'heure du nom des copies.</param>
    /// <param name="journal">Journal des événements notables.</param>
    /// <param name="dialogue">Moyen de parler à l'utilisateur.</param>
    public static BaseHistorique? Ouvrir(DossierDonnees dossiers, TimeProvider horloge, IJournal journal, IDialogueDemarrage dialogue) =>
        Ouvrir(dossiers, horloge, journal, dialogue, BaseHistorique.Ouvrir);

    /// <summary>Variante testable : l'ouverture de la base est injectée.</summary>
    internal static BaseHistorique? Ouvrir(DossierDonnees dossiers, TimeProvider horloge, IJournal journal,
                                           IDialogueDemarrage dialogue, Func<string, BaseHistorique> ouvrir)
    {
        ArgumentNullException.ThrowIfNull(dossiers);
        ArgumentNullException.ThrowIfNull(horloge);
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(ouvrir);

        // La copie passe avant l'ouverture : pas de connexion d'écriture tant que l'état d'origine n'est pas à l'abri.
        var abimee = false;
        try
        {
            Sauvegarde.Creer(dossiers.Base, dossiers.Sauvegardes, horloge);
        }
        catch (SqliteException exception) when (EstBaseAbimee(exception))
        {
            journal.Erreur("La base de l'historique est illisible : aucune copie de sécurité n'a été faite.", exception);
            abimee = true;
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            journal.Avertissement("Copie de sécurité impossible : le démarrage continue sans elle.", exception);
        }

        if (!abimee)
        {
            try
            {
                return ouvrir(dossiers.Base);
            }
            catch (BaseIncompatibleException exception)
            {
                journal.Erreur("Version de SQLite incompatible.", exception);
                dialogue.Informer($"{exception.Message}{Environment.NewLine}{Environment.NewLine}PressePapiers ne peut pas démarrer.");
                return null;
            }
            catch (SqliteException exception) when (EstBaseAbimee(exception))
            {
                journal.Erreur("La base de l'historique est illisible.", exception);
            }
            catch (SqliteException exception)
            {
                journal.Erreur("La base de l'historique ne peut pas être ouverte.", exception);
                dialogue.Informer(
                    $"La base de l'historique ne peut pas être ouverte ({exception.Message}).{Environment.NewLine}{Environment.NewLine}"
                    + $"Fichier : {dossiers.Base}{Environment.NewLine}Détail dans le journal : {dossiers.Journal}");
                return null;
            }
        }

        return Restaurer(dossiers, horloge, journal, dialogue, ouvrir);
    }

    // Vrai pour une base abîmée, seul cas où une restauration a un sens.
    private static bool EstBaseAbimee(SqliteException exception) =>
        exception.SqliteErrorCode is CodeCorrompue or CodePasUneBase;

    private static BaseHistorique? Restaurer(DossierDonnees dossiers, TimeProvider horloge, IJournal journal,
                                             IDialogueDemarrage dialogue, Func<string, BaseHistorique> ouvrir)
    {
        var copie = Sauvegarde.PlusRecente(dossiers.Sauvegardes);
        if (copie is null)
        {
            dialogue.Informer(
                $"La base de l'historique est illisible et il n'existe aucune copie de sécurité.{Environment.NewLine}{Environment.NewLine}"
                + $"Fichier : {dossiers.Base}{Environment.NewLine}"
                + "Déplacez ce fichier pour repartir d'un historique vide, puis relancez PressePapiers.");
            return null;
        }

        var dateCopie = File.GetLastWriteTime(copie).ToString("g", CultureInfo.CurrentCulture);
        var accepte = dialogue.Confirmer(
            $"La base de l'historique est illisible.{Environment.NewLine}{Environment.NewLine}"
            + $"Restaurer la copie de sécurité la plus récente ({dateCopie}) ? Les éléments copiés depuis cette date seront perdus."
            + $"{Environment.NewLine}Le fichier abîmé est gardé dans le dossier des sauvegardes.");
        if (!accepte)
        {
            journal.Info("Restauration refusée par l'utilisateur : l'application ne démarre pas.");
            return null;
        }

        try
        {
            // Le fichier abîmé est copié à côté avant d'être remplacé : rien n'est perdu sans retour possible.
            var ecartee = Path.Combine(dossiers.Sauvegardes,
                $"illisible-{horloge.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.db");
            File.Copy(dossiers.Base, ecartee, overwrite: true);

            Sauvegarde.Restaurer(copie, dossiers.Base);
            var restauree = ouvrir(dossiers.Base);
            journal.Info($"Base restaurée depuis la copie de sécurité {Path.GetFileName(copie)}.");
            return restauree;
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            journal.Erreur("La restauration de la copie de sécurité a échoué.", exception);
            dialogue.Informer(
                $"La restauration de la copie de sécurité a échoué ({exception.Message}).{Environment.NewLine}{Environment.NewLine}"
                + $"Détail dans le journal : {dossiers.Journal}");
            return null;
        }
    }
}
