#pragma warning disable CS0067 // Coquille : l'événement Modifie est levé par la tâche 4.
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;

namespace PressePapiers.Coeur.Stockage;

/// <summary>
/// Dépôt des onglets et des éléments, sûr entre fils (verrou interne). Coquille : onglets par la tâche 4,
/// éléments par la tâche 5, purge, plafond, index et méta par la tâche 6.
/// </summary>
public sealed class DepotHistorique
{
    /// <summary>Crée le dépôt sur une base ouverte.</summary>
    public DepotHistorique(BaseHistorique baseHistorique, DossierDonnees dossiers, TimeProvider horloge, IJournal journal) =>
        throw new NotImplementedException("Tâche 4");

    /// <summary>Onglet Temporaire, créé à la première ouverture (nom « Temporaire », ordre 0).</summary>
    public Onglet OngletTemporaire => throw new NotImplementedException("Tâche 4");

    /// <summary>Après toute écriture réussie ; peut être levé sur n'importe quel fil.</summary>
    public event Action? Modifie;

    /// <summary>Onglets par ordre, Temporaire en premier.</summary>
    public IReadOnlyList<Onglet> ListerOnglets() => throw new NotImplementedException("Tâche 4");

    /// <summary>Crée un onglet à la fin ; le nom ne peut pas être vide.</summary>
    public Onglet CreerOnglet(string nom) => throw new NotImplementedException("Tâche 4");

    /// <summary>Renomme un onglet ; <see cref="InvalidOperationException"/> pour Temporaire.</summary>
    public void RenommerOnglet(long ongletId, string nom) => throw new NotImplementedException("Tâche 4");

    /// <summary>Déplace un onglet à une nouvelle position.</summary>
    public void DeplacerOnglet(long ongletId, int nouvelOrdre) => throw new NotImplementedException("Tâche 4");

    /// <summary>Supprime un onglet, ses éléments et leurs PNG ; <see cref="InvalidOperationException"/> pour Temporaire.</summary>
    public void SupprimerOnglet(long ongletId) => throw new NotImplementedException("Tâche 4");

    /// <summary>Nombre d'éléments d'un onglet.</summary>
    public int CompterElements(long ongletId) => throw new NotImplementedException("Tâche 5");

    /// <summary>Éléments d'un onglet, par DateUsage décroissante puis Id décroissant (ordre stable à date égale).</summary>
    public IReadOnlyList<Element> ListerElements(long ongletId) => throw new NotImplementedException("Tâche 5");

    /// <summary>Lit un élément, ou null s'il n'existe pas.</summary>
    public Element? Lire(long elementId) => throw new NotImplementedException("Tâche 5");

    /// <summary>
    /// Ajoute une copie à Temporaire avec dédoublonnage, écrit le PNG d'une image ;
    /// renvoie l'élément créé ou remonté.
    /// </summary>
    public Element AjouterCopie(ContenuCopie contenu) => throw new NotImplementedException("Tâche 5");

    /// <summary>Donne un nom à un élément et le range dans un onglet autre que Temporaire.</summary>
    public Element Nommer(long elementId, string nom, long ongletId) => throw new NotImplementedException("Tâche 5");

    /// <summary>Met à jour la date de dernier usage d'un élément.</summary>
    public void MarquerUtilise(long elementId) => throw new NotImplementedException("Tâche 5");

    /// <summary>Supprime un élément et son PNG.</summary>
    public void Supprimer(long elementId) => throw new NotImplementedException("Tâche 5");

    /// <summary>Chemin complet du PNG d'un élément ; <see cref="InvalidOperationException"/> s'il n'a pas d'image.</summary>
    public string CheminImage(Element element) => throw new NotImplementedException("Tâche 5");

    /// <summary>Supprime les éléments de Temporaire et leurs PNG ; renvoie leur nombre.</summary>
    public int PurgerTemporaire() => throw new NotImplementedException("Tâche 6");

    /// <summary>Supprime les plus anciens éléments de Temporaire au-delà du plafond (0 = désactivé) ; renvoie leur nombre.</summary>
    public int AppliquerPlafond(int plafond) => throw new NotImplementedException("Tâche 6");

    /// <summary>Reconstruit l'index plein texte.</summary>
    public void ReconstruireIndex() => throw new NotImplementedException("Tâche 6");

    /// <summary>Lit une valeur de la table <c>meta</c>.</summary>
    public string? LireMeta(string cle) => throw new NotImplementedException("Tâche 6");

    /// <summary>Écrit une valeur dans la table <c>meta</c>.</summary>
    public void EcrireMeta(string cle, string valeur) => throw new NotImplementedException("Tâche 6");
}
