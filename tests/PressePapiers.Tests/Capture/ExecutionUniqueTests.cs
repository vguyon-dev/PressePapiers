using PressePapiers.Coeur.Capture;

namespace PressePapiers.Tests.Capture;

public sealed class ExecutionUniqueTests
{
    private static readonly TimeSpan Delai = TimeSpan.FromSeconds(10);

    // Un travail dont on maîtrise le début et la fin : il se bloque jusqu'à ce que le test le libère.
    private sealed class TravailPilote
    {
        public int Debuts;
        public int Fins;
        public int EnCours;
        public int MaxSimultanes;
        public readonly List<string> Journal = [];
        private readonly Queue<TaskCompletionSource> _portes = new();
        public TaskCompletionSource Commencee = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ExecuterAsync()
        {
            TaskCompletionSource? porte;
            lock (this)
            {
                Debuts++;
                var numero = Debuts;
                Journal.Add($"debut{numero}");
                EnCours++;
                MaxSimultanes = Math.Max(MaxSimultanes, EnCours);
                _portes.TryDequeue(out porte);
                Commencee.TrySetResult();
            }

            return Terminer(porte);
        }

        private async Task Terminer(TaskCompletionSource? porte)
        {
            if (porte is not null)
            {
                await porte.Task;
            }

            lock (this)
            {
                Fins++;
                Journal.Add($"fin{Fins}");
                EnCours--;
            }
        }

        // La prochaine exécution restera bloquée jusqu'à l'appel de Liberer sur la porte rendue.
        public TaskCompletionSource Bloquer()
        {
            var porte = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (this)
            {
                _portes.Enqueue(porte);
            }

            return porte;
        }
    }

    private static ExecutionUnique Creer(Func<Task> travail, Action<Exception>? surErreur = null) =>
        new(travail, surErreur ?? (_ => { }));

    [Fact]
    public async Task Une_demande_lance_le_travail_une_fois()
    {
        var travail = new TravailPilote();
        var execution = Creer(travail.ExecuterAsync);

        execution.Demander();
        await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);

        Assert.Equal(1, travail.Debuts);
        Assert.Equal(1, travail.Fins);
    }

    [Fact]
    public async Task Une_rafale_pendant_un_travail_ne_relance_qu_une_seule_fois()
    {
        var travail = new TravailPilote();
        var porte = travail.Bloquer();
        var execution = Creer(travail.ExecuterAsync);

        execution.Demander();
        await travail.Commencee.Task.WaitAsync(Delai, TestContext.Current.CancellationToken);
        for (var i = 0; i < 50; i++)
        {
            execution.Demander();
        }

        porte.SetResult();
        await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);

        // Le travail en cours, plus une seule relance pour les 50 demandes : ni 51 (une par demande), ni 1 (relance perdue).
        Assert.Equal(2, travail.Debuts);
        Assert.Equal(2, travail.Fins);
    }

    [Fact]
    public async Task La_relance_commence_apres_la_fin_du_travail_en_cours()
    {
        var travail = new TravailPilote();
        var porte = travail.Bloquer();
        var execution = Creer(travail.ExecuterAsync);

        execution.Demander();
        await travail.Commencee.Task.WaitAsync(Delai, TestContext.Current.CancellationToken);
        execution.Demander();
        porte.SetResult();
        await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);

        // Deux lectures du presse-papiers ne se chevauchent jamais : l'ordre est strict.
        Assert.Equal(["debut1", "fin1", "debut2", "fin2"], travail.Journal);
        Assert.Equal(1, travail.MaxSimultanes);
    }

    [Fact]
    public async Task Une_demande_apres_la_fin_relance_normalement()
    {
        var travail = new TravailPilote();
        var execution = Creer(travail.ExecuterAsync);

        for (var i = 0; i < 3; i++)
        {
            execution.Demander();
            await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);
        }

        Assert.Equal(3, travail.Debuts);
    }

    [Fact]
    public async Task Une_exception_est_signalee_sans_bloquer_les_demandes_suivantes()
    {
        var erreurs = new List<Exception>();
        var appels = 0;
        var execution = Creer(() =>
        {
            appels++;
            // Premier appel : exception levée avant même de rendre une tâche ; deuxième : tâche en échec.
            if (appels == 1)
            {
                throw new InvalidOperationException("synchrone");
            }

            return appels == 2 ? Task.FromException(new InvalidOperationException("asynchrone")) : Task.CompletedTask;
        }, erreurs.Add);

        for (var i = 0; i < 3; i++)
        {
            execution.Demander();
            await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);
        }

        Assert.Equal(3, appels);
        Assert.Equal(["synchrone", "asynchrone"], erreurs.Select(e => e.Message));
    }

    [Fact]
    public async Task Un_signalement_d_erreur_qui_leve_ne_bloque_pas_l_executeur()
    {
        var appels = 0;
        var execution = Creer(() =>
        {
            appels++;
            throw new InvalidOperationException("boum");
        }, _ => throw new InvalidOperationException("le signalement lui-même échoue"));

        execution.Demander();
        await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);
        execution.Demander();
        await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);

        Assert.Equal(2, appels);
    }

    [Fact]
    public async Task Des_demandes_venues_de_plusieurs_fils_ne_se_chevauchent_jamais()
    {
        var travail = new TravailPilote();
        var execution = Creer(async () =>
        {
            await travail.ExecuterAsync();
            await Task.Delay(1);
        });

        var fils = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                execution.Demander();
            }
        })).ToArray();
        await Task.WhenAll(fils).WaitAsync(Delai, TestContext.Current.CancellationToken);
        await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);

        Assert.Equal(1, travail.MaxSimultanes);
        Assert.InRange(travail.Debuts, 1, 1600);
        Assert.Equal(travail.Debuts, travail.Fins);

        // Après le repos, une demande de plus donne exactement une exécution de plus.
        var avant = travail.Debuts;
        execution.Demander();
        await execution.AttendreRepos().WaitAsync(Delai, TestContext.Current.CancellationToken);
        Assert.Equal(avant + 1, travail.Debuts);
    }

    [Fact]
    public void Les_arguments_nuls_sont_refuses()
    {
        Assert.Throws<ArgumentNullException>(() => new ExecutionUnique(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new ExecutionUnique(() => Task.CompletedTask, null!));
    }
}
