using Microsoft.UI.Xaml;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Interface;

/// <summary>
/// Fenêtre principale (proposition B de la maquette). Coquille sans XAML : le contenu vient avec les tâches 25 à 28.
/// </summary>
public sealed partial class FenetrePrincipale : Window
{
    /// <summary>Crée la fenêtre, masquée, sur le modèle de vue.</summary>
    public FenetrePrincipale(FenetrePrincipaleVue vue) => throw new NotImplementedException("Tâche 25");

    /// <summary>Handle Win32 de la fenêtre.</summary>
    public nint Handle => throw new NotImplementedException("Tâche 25");

    /// <summary>Vrai tant que la fenêtre est affichée.</summary>
    public bool EstVisible => throw new NotImplementedException("Tâche 25");

    /// <summary>Affiche la fenêtre (AppWindow.Show puis PremierPlan.Amener) en notant la fenêtre d'origine du collage.</summary>
    public void AfficherPour(nint fenetreOrigine, OrigineOuverture origine) => throw new NotImplementedException("Tâche 27");

    /// <summary>Ouvre la fenêtre sur la page Paramètres.</summary>
    public void AfficherParametres() => throw new NotImplementedException("Tâche 28");

    /// <summary>Masque la fenêtre sans terminer l'application.</summary>
    public void Masquer() => throw new NotImplementedException("Tâche 27");
}
