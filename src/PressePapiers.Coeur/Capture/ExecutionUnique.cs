namespace PressePapiers.Coeur.Capture;

/// <summary>
/// Exécute un travail asynchrone une seule fois à la fois. Les demandes reçues pendant qu'il s'exécute ne s'empilent pas :
/// elles se fondent en une seule relance, lancée quand le travail en cours est fini. Sert à la capture du presse-papiers :
/// une rafale de notifications pendant une lecture ne provoque qu'une lecture de plus, qui verra le dernier contenu.
/// </summary>
public sealed class ExecutionUnique
{
    private readonly Func<Task> _travail;
    private readonly Action<Exception> _surErreur;
    private readonly object _verrou = new();
    private bool _enCours;
    private bool _relanceDemandee;
    private TaskCompletionSource? _repos;

    /// <summary>Crée l'exécuteur.</summary>
    /// <param name="travail">Travail à exécuter ; il peut lever une exception, y compris avant de rendre sa tâche.</param>
    /// <param name="surErreur">Appelé avec l'exception d'un travail en échec ; l'exécuteur continue ensuite normalement.</param>
    public ExecutionUnique(Func<Task> travail, Action<Exception> surErreur)
    {
        ArgumentNullException.ThrowIfNull(travail);
        ArgumentNullException.ThrowIfNull(surErreur);
        _travail = travail;
        _surErreur = surErreur;
    }

    /// <summary>
    /// Demande une exécution. Sans travail en cours, il démarre aussitôt sur le fil appelant jusqu'à son premier point
    /// d'attente ; sinon la demande est mémorisée et une seule relance suivra. Sûr entre fils, ne bloque jamais.
    /// </summary>
    public void Demander()
    {
        lock (_verrou)
        {
            if (_enCours)
            {
                _relanceDemandee = true;
                return;
            }

            _enCours = true;
            _repos = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        _ = ExecuterAsync();
    }

    /// <summary>Tâche terminée quand plus aucun travail n'est en cours ni en attente de relance (pour les tests et l'arrêt).</summary>
    public Task AttendreRepos()
    {
        lock (_verrou)
        {
            return _enCours ? _repos!.Task : Task.CompletedTask;
        }
    }

    // Ne lève jamais : toute exception du travail est confiée à _surErreur, sinon l'état « en cours » resterait vrai à jamais.
    private async Task ExecuterAsync()
    {
        while (true)
        {
            try
            {
                await _travail();
            }
            catch (Exception exception)
            {
                Signaler(exception);
            }

            TaskCompletionSource repos;
            lock (_verrou)
            {
                if (_relanceDemandee)
                {
                    _relanceDemandee = false;
                    continue;
                }

                _enCours = false;
                repos = _repos!;
            }

            repos.SetResult();
            return;
        }
    }

    private void Signaler(Exception exception)
    {
        try
        {
            _surErreur(exception);
        }
        catch
        {
            // Le signalement ne doit jamais bloquer l'exécuteur.
        }
    }
}
