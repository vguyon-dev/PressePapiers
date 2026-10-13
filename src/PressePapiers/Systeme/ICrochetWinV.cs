namespace PressePapiers.Systeme;

/// <summary>Hook clavier qui reprend Win+V, isolé derrière une interface pour pouvoir être remplacé.</summary>
public interface ICrochetWinV : IDisposable
{
    /// <summary>Vrai tant que le hook est posé.</summary>
    bool EstActif { get; }

    /// <summary>Raison de l'inactivité, affichée dans les Paramètres ; null si le hook est actif.</summary>
    string? RaisonInactif { get; }

    /// <summary>
    /// Levé sur le fil du hook ; l'argument est la fenêtre au premier plan. L'abonné doit rendre la main tout de suite
    /// (en postant le travail vers son propre fil) : tant qu'il s'exécute, le fil du hook ne traite plus les frappes,
    /// le clavier de toute la session se fige et Windows finit par retirer le hook sans prévenir.
    /// </summary>
    event Action<nint>? WinVPresse;

    /// <summary>Démarre le fil du hook.</summary>
    void Demarrer();

    /// <summary>Pose un nouveau hook avant de retirer l'ancien (spec, section 11).</summary>
    void Reinstaller();
}
