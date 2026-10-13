using PressePapiers.Coeur.Stockage;
using PressePapiers.Natif;

namespace PressePapiers.Systeme;

/// <summary>
/// Dialogues du démarrage par la boîte de message native (MessageBoxW) : elle n'exige ni fenêtre ni XAML, donc elle sert
/// même quand l'interface n'a pas encore pu démarrer.
/// </summary>
public sealed class DialoguesWindows : IDialogueDemarrage
{
    private const string Titre = "PressePapiers";

    /// <inheritdoc />
    public void Informer(string message) =>
        NatifDemarrage.AfficherMessage(0, message, Titre,
            NatifDemarrage.MB_OK | NatifDemarrage.MB_ICONERROR | NatifDemarrage.MB_SETFOREGROUND);

    /// <summary>
    /// Affiche un message d'information simple (icône d'information, bouton OK), qui ne signale pas une panne : par exemple
    /// la confirmation qu'un choix de l'utilisateur a été respecté. Prêt à servir si <see cref="IDialogueDemarrage"/> le
    /// demande un jour.
    /// </summary>
    /// <param name="message">Texte clair, en français.</param>
    public void Signaler(string message) =>
        NatifDemarrage.AfficherMessage(0, message, Titre,
            NatifDemarrage.MB_OK | NatifDemarrage.MB_ICONINFORMATION | NatifDemarrage.MB_SETFOREGROUND);

    /// <inheritdoc />
    public bool Confirmer(string question) =>
        // Le bouton par défaut est Non : un Entrée distrait ne remplace jamais la base.
        NatifDemarrage.AfficherMessage(0, question, Titre,
            NatifDemarrage.MB_YESNO | NatifDemarrage.MB_ICONWARNING | NatifDemarrage.MB_DEFBUTTON2 | NatifDemarrage.MB_SETFOREGROUND)
        == NatifDemarrage.IDYES;
}
