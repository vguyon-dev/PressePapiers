#pragma warning disable CS0067 // Coquille : les événements sont levés par la tâche 18.
using PressePapiers.Coeur.Journalisation;

namespace PressePapiers.Systeme;

/// <summary>Icône de la zone de notification et son menu natif. Coquille : remplacée par la tâche 18.</summary>
public sealed class IconeNotification : IDisposable
{
    /// <summary>Crée l'icône, qui reçoit ses messages par le relais.</summary>
    public IconeNotification(RelaisWindows relais, IJournal journal) => throw new NotImplementedException("Tâche 18");

    /// <summary>Clic sur l'icône ou commande « Ouvrir ».</summary>
    public event Action? OuvrirDemande;

    /// <summary>Commande « Paramètres ».</summary>
    public event Action? ParametresDemandes;

    /// <summary>Commande « Quitter ».</summary>
    public event Action? QuitterDemande;

    /// <summary>Affiche l'icône (NIM_ADD la première fois, NIM_MODIFY ensuite), en aspect d'alerte si demandé.</summary>
    public void Afficher(bool alerte, string infobulle) => throw new NotImplementedException("Tâche 18");

    /// <summary>Affiche une bulle de notification (NIF_INFO).</summary>
    public void Notifier(string titre, string message) => throw new NotImplementedException("Tâche 18");

    /// <inheritdoc />
    public void Dispose() => throw new NotImplementedException("Tâche 18");
}
