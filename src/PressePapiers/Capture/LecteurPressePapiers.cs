using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Capture;

/// <summary>Lecture du presse-papiers à chaque copie. Coquille : remplacée par la tâche 21.</summary>
public sealed class LecteurPressePapiers
{
    /// <summary>Crée le lecteur ; <paramref name="hwndRelais"/> est la fenêtre qui ouvre le presse-papiers.</summary>
    public LecteurPressePapiers(nint hwndRelais, IJournal journal) => throw new NotImplementedException("Tâche 21");

    /// <summary>
    /// Lit le contenu : 10 essais espacés de 25 ms ; null si la marque interne est présente, si le contenu est illisible ou vide.
    /// Les formats sont lus dans l'ordre de la spec, section 4.
    /// </summary>
    public Task<ContenuCopie?> LireAsync(string? application) => throw new NotImplementedException("Tâche 21");
}
