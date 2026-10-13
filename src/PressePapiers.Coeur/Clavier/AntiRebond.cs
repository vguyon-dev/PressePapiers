namespace PressePapiers.Coeur.Clavier;

/// <summary>Ignore un signal trop proche du précédent. Coquille : remplacée par la tâche 11.</summary>
public sealed class AntiRebond
{
    /// <summary>Crée l'anti-rebond avec son horloge et son délai.</summary>
    public AntiRebond(TimeProvider horloge, TimeSpan delai) => throw new NotImplementedException("Tâche 11");

    /// <summary>Faux si le dernier signal accepté date de moins que le délai.</summary>
    public bool Accepter() => throw new NotImplementedException("Tâche 11");
}
