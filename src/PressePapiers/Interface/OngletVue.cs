using PressePapiers.Coeur.Modele;

namespace PressePapiers.Interface;

/// <summary>Un onglet tel que le volet de navigation l'affiche : son identité, son nom et le nombre d'éléments qu'il contient.</summary>
public sealed record OngletVue(long Id, string Nom, bool EstTemporaire, int Compteur)
{
    /// <summary>Construit la ligne du volet à partir de l'onglet du dépôt et de son compteur.</summary>
    public static OngletVue De(Onglet onglet, int compteur) => new(onglet.Id, onglet.Nom, onglet.EstTemporaire, compteur);

    /// <summary>Vrai si le volet doit être reconstruit : un onglet est apparu, a disparu, a changé de place ou de nom.</summary>
    public static bool StructureDifferente(IReadOnlyList<OngletVue> a, IReadOnlyList<OngletVue> b) =>
        a.Count != b.Count || a.Zip(b).Any(p => p.First.Id != p.Second.Id || p.First.Nom != p.Second.Nom);
}
