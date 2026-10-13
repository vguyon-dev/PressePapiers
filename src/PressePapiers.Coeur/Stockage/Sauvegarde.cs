namespace PressePapiers.Coeur.Stockage;

/// <summary>Copies de sécurité de la base. Coquille : remplacée par la tâche 7.</summary>
public static class Sauvegarde
{
    /// <summary>
    /// Copie par <c>SqliteConnection.BackupDatabase</c> dans <paramref name="dossierSauvegardes"/>
    /// (<c>historique-AAAAMMJJ-HHmmss.db</c>) et garde les <paramref name="nombreGarde"/> plus récentes.
    /// Renvoie le chemin créé, ou null s'il n'y a pas de base.
    /// </summary>
    public static string? Creer(string cheminBase, string dossierSauvegardes, TimeProvider horloge, int nombreGarde = 3) =>
        throw new NotImplementedException("Tâche 7");

    /// <summary>Chemin de la sauvegarde la plus récente, ou null s'il n'y en a pas.</summary>
    public static string? PlusRecente(string dossierSauvegardes) => throw new NotImplementedException("Tâche 7");

    /// <summary>Remplace la base par une sauvegarde, la base étant fermée ; supprime les fichiers <c>-wal</c> et <c>-shm</c>.</summary>
    public static void Restaurer(string cheminSauvegarde, string cheminBase) => throw new NotImplementedException("Tâche 7");
}
