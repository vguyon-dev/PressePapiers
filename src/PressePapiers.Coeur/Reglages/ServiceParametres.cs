using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Coeur.Reglages;

/// <summary>Lecture et écriture du fichier <c>parametres.json</c>. Coquille : remplacée par la tâche 10.</summary>
public sealed class ServiceParametres
{
    /// <summary>Crée le service sur un fichier de réglages.</summary>
    public ServiceParametres(string cheminFichier, IJournal journal) => throw new NotImplementedException("Tâche 10");

    /// <summary>Lit les réglages ; les valeurs par défaut si le fichier est absent ou illisible (avertissement au journal).</summary>
    public Parametres Charger() => throw new NotImplementedException("Tâche 10");

    /// <summary>Écrit les réglages de façon atomique : fichier temporaire, puis <c>File.Move(..., true)</c>.</summary>
    public void Enregistrer(Parametres parametres) => throw new NotImplementedException("Tâche 10");
}
