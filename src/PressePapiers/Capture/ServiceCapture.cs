#pragma warning disable CS0067 // Coquille : l'événement est levé par la tâche 23.
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Systeme;

namespace PressePapiers.Capture;

/// <summary>Enregistre chaque copie détectée dans l'historique. Coquille : remplacée par la tâche 23.</summary>
public sealed class ServiceCapture : IDisposable
{
    /// <summary>Branche la capture sur le relais.</summary>
    public ServiceCapture(RelaisWindows relais, LecteurPressePapiers lecteur, DepotHistorique depot,
                          Func<Parametres> parametres, IJournal journal) =>
        throw new NotImplementedException("Tâche 23");

    /// <summary>Levé après l'ajout (ou la remontée) d'un élément.</summary>
    public event Action<Element>? ElementAjoute;

    /// <inheritdoc />
    public void Dispose() => throw new NotImplementedException("Tâche 23");
}
