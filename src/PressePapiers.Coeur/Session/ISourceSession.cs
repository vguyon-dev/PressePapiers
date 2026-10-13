namespace PressePapiers.Coeur.Session;

/// <summary>Source du repère de session Windows ; remplaçable dans les tests.</summary>
public interface ISourceSession
{
    /// <summary>FILETIME brut de <c>WTSINFOW.LogonTime</c> ; null si la lecture est impossible.</summary>
    long? LireHeureOuverture();
}
