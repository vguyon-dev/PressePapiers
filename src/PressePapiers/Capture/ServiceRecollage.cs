using PressePapiers.Coeur.Collage;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Capture;

/// <summary>Remet un élément dans le presse-papiers et le colle dans la fenêtre d'origine. Coquille : tâche 24.</summary>
public sealed class ServiceRecollage
{
    /// <summary>Crée le service de recollage.</summary>
    public ServiceRecollage(EcrivainPressePapiers ecrivain, DepotHistorique depot, IJournal journal) =>
        throw new NotImplementedException("Tâche 24");

    /// <summary>
    /// Spec, section 10.2 : écriture, contrôle de la fenêtre et de son niveau, premier plan, attente des modificateurs, Ctrl+V.
    /// </summary>
    public Task<IssueCollage> CollerAsync(Element element, ModeCollage mode, nint fenetreOrigine, bool copieSeule,
                                          nint hwndOutil, Action masquerFenetre) =>
        throw new NotImplementedException("Tâche 24");
}
