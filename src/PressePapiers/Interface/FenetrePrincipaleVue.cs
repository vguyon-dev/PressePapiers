using CommunityToolkit.Mvvm.ComponentModel;
using PressePapiers.Capture;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Recherche;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Coeur.Stockage;

namespace PressePapiers.Interface;

/// <summary>Modèle de vue de la fenêtre principale. Coquille : complétée par les tâches 25 à 28.</summary>
public sealed partial class FenetrePrincipaleVue : ObservableObject
{
    /// <summary>Crée le modèle de vue.</summary>
    public FenetrePrincipaleVue(DepotHistorique depot, MoteurRecherche recherche, ServiceRecollage recollage,
                                ServiceParametres parametres, IJournal journal) =>
        throw new NotImplementedException("Tâche 25");

    /// <summary>Texte d'état du hook Win+V affiché dans les Paramètres, fourni par l'application.</summary>
    [ObservableProperty]
    public partial string EtatCrochet { get; set; }
}
