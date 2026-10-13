namespace PressePapiers.Coeur.Modele;

/// <summary>Façon de remettre un élément dans le presse-papiers au moment du collage.</summary>
public enum ModeCollage
{
    /// <summary>Texte brut uniquement.</summary>
    TexteSeul = 0,

    /// <summary>Texte brut plus la mise en forme conservée (RTF et HTML).</summary>
    AvecMiseEnForme = 1,
}
