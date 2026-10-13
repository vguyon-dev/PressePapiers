using PressePapiers.Coeur.Modele;

namespace PressePapiers.Coeur.Stockage;

/// <summary>Empreinte du contenu principal, pour le dédoublonnage. Coquille : remplacée par la tâche 2.</summary>
public static class Empreinte
{
    /// <summary>
    /// SHA-256 : texte en UTF-8 (Texte, MisEnForme), octets du PNG (Image), chemins joints par un retour à la ligne (Fichiers).
    /// </summary>
    /// <param name="contenu">Contenu copié.</param>
    public static byte[] Calculer(ContenuCopie contenu) => throw new NotImplementedException("Tâche 2");
}
