using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Capture;

/// <summary>Écriture d'un élément dans le presse-papiers. Coquille : remplacée par la tâche 22.</summary>
public sealed class EcrivainPressePapiers
{
    /// <summary>Crée l'écrivain ; <paramref name="hwndRelais"/> est la fenêtre qui ouvre le presse-papiers.</summary>
    public EcrivainPressePapiers(nint hwndRelais, IJournal journal) => throw new NotImplementedException("Tâche 22");

    /// <summary>Écrit l'élément dans le mode demandé, en posant toujours la marque interne.</summary>
    public Task<bool> EcrireAsync(Element element, ModeCollage mode, string? cheminImage) =>
        throw new NotImplementedException("Tâche 22");
}
