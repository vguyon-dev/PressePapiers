using PressePapiers.Coeur.Capture;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Coeur.Modele;
using PressePapiers.Coeur.Reglages;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Systeme;

namespace PressePapiers.Capture;

/// <summary>
/// Enregistre chaque copie détectée dans l'historique : relève l'application d'origine, lit le presse-papiers puis ajoute
/// l'élément à Temporaire. Une seule capture à la fois ; une rafale de notifications pendant une capture ne provoque qu'une
/// lecture de plus. Une lecture en retard (propriétaire lent) relance d'elle-même une capture quand elle finit, pour que
/// son résultat ne soit pas perdu.
/// </summary>
public sealed class ServiceCapture : IDisposable
{
    private readonly RelaisWindows _relais;
    private readonly LecteurPressePapiers _lecteur;
    private readonly DepotHistorique _depot;
    private readonly Func<Parametres> _parametres;
    private readonly IJournal _journal;
    private readonly ExecutionUnique _execution;
    private readonly SynchronizationContext? _filInterface;
    private volatile string? _application;
    private volatile bool _libere;

    /// <summary>Branche la capture sur le relais. À créer sur le fil d'interface, où le relais lève ses événements.</summary>
    /// <param name="relais">Relais qui signale chaque copie.</param>
    /// <param name="lecteur">Lecteur du presse-papiers.</param>
    /// <param name="depot">Historique où ranger les copies.</param>
    /// <param name="parametres">Fournit les réglages courants (plafond de Temporaire), relus à chaque copie.</param>
    /// <param name="journal">Journal des captures en échec.</param>
    public ServiceCapture(RelaisWindows relais, LecteurPressePapiers lecteur, DepotHistorique depot,
                          Func<Parametres> parametres, IJournal journal)
    {
        ArgumentNullException.ThrowIfNull(relais);
        ArgumentNullException.ThrowIfNull(lecteur);
        ArgumentNullException.ThrowIfNull(depot);
        ArgumentNullException.ThrowIfNull(parametres);
        ArgumentNullException.ThrowIfNull(journal);
        _relais = relais;
        _lecteur = lecteur;
        _depot = depot;
        _parametres = parametres;
        _journal = journal;
        _execution = new ExecutionUnique(CapturerAsync, exception => _journal.Erreur("Capture d'une copie en échec.", exception));
        _filInterface = SynchronizationContext.Current;
        _relais.CopieDetectee += SurCopieDetectee;
        _lecteur.RelectureDemandee += SurRelectureDemandee;
    }

    /// <summary>
    /// Levé après l'ajout (ou la remontée) d'un élément, sur le fil qui a reçu la notification (le fil d'interface).
    /// </summary>
    public event Action<Element>? ElementAjoute;

    /// <summary>Cesse d'écouter le relais. Une capture déjà commencée peut finir de lire mais n'écrit plus dans l'historique.</summary>
    public void Dispose()
    {
        _libere = true;
        _relais.CopieDetectee -= SurCopieDetectee;
        _lecteur.RelectureDemandee -= SurRelectureDemandee;
    }

    // L'origine est relevée tout de suite, avant que l'utilisateur ne change de fenêtre ; chaque notification la remplace,
    // la lecture de relance utilisera donc la plus récente.
    private void SurCopieDetectee()
    {
        if (_libere)
        {
            return;
        }

        _application = ApplicationOrigine.DeterminerPourCopie();
        _execution.Demander();
    }

    // Levé sur le fil de lecture : la capture est ramenée sur le fil d'interface, où ElementAjoute doit être levé.
    // L'origine n'est pas relevée de nouveau : le lecteur livre le résultat tardif avec celle de sa propre copie.
    private void SurRelectureDemandee()
    {
        if (_libere)
        {
            return;
        }

        if (_filInterface is null)
        {
            _execution.Demander();
            return;
        }

        _filInterface.Post(_ =>
        {
            if (!_libere)
            {
                _execution.Demander();
            }
        }, null);
    }

    private async Task CapturerAsync()
    {
        var contenu = await _lecteur.LireAsync(_application);
        if (contenu is null || _libere)
        {
            return;
        }

        var element = _depot.AjouterCopie(contenu);
        AppliquerPlafond();
        ElementAjoute?.Invoke(element);
    }

    // Le plafond est un ménage : son échec ne doit pas faire perdre la copie qui vient d'être enregistrée.
    private void AppliquerPlafond()
    {
        try
        {
            _depot.AppliquerPlafond(_parametres().PlafondTemporaire);
        }
        catch (Exception exception)
        {
            _journal.Avertissement("Plafond de Temporaire non appliqué après une copie.", exception);
        }
    }
}
