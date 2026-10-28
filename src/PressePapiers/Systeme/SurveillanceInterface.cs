using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using PressePapiers.Coeur.Journalisation;
using PressePapiers.Natif;

namespace PressePapiers.Systeme;

/// <summary>
/// Chien de garde du fil d'interface. Un fil d'arrière-plan lui poste un signe de vie toutes les 5 s ; sans réponse depuis
/// 15 s, il note le gel et écrit un dump complet (lisible par dotnet-dump : pile du fil figé), puis note la reprise et la
/// durée du gel. Le gel du 28/10/2026 n'avait laissé aucune
/// trace : le journal s'arrêtait sept minutes avant, et le processus n'a jamais pu être arrêté.
/// </summary>
internal sealed class SurveillanceInterface : IDisposable
{
    private static readonly TimeSpan _periode = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _seuil = TimeSpan.FromSeconds(15);

    // Un dump avec le tas pèse quelques centaines de Mo : on garde les plus récents.
    private const int DumpsGardes = 3;

    private readonly DispatcherQueue _fil;
    private readonly IJournal _journal;
    private readonly string _dossierDumps;
    private readonly ManualResetEventSlim _arret = new(false);
    private readonly Thread _surveillant;
    private long _derniereReponse = Environment.TickCount64;

    /// <summary>Démarre la surveillance du fil d'interface donné.</summary>
    /// <param name="fil">File du fil d'interface.</param>
    /// <param name="journal">Journal de l'application.</param>
    /// <param name="dossierDumps">Dossier des dumps de gel (créé au besoin).</param>
    public SurveillanceInterface(DispatcherQueue fil, IJournal journal, string dossierDumps)
    {
        _fil = fil;
        _journal = journal;
        _dossierDumps = dossierDumps;
        _surveillant = new Thread(Boucle) { Name = "SurveillanceInterface", IsBackground = true };
        _surveillant.Start();
    }

    /// <inheritdoc />
    public void Dispose() => _arret.Set();

    private void Boucle()
    {
        var gelSignale = false;
        long debutGel = 0;
        while (!_arret.Wait(_periode))
        {
            try
            {
                _fil.TryEnqueue(DispatcherQueuePriority.High, () => Volatile.Write(ref _derniereReponse, Environment.TickCount64));

                var maintenant = Environment.TickCount64;
                var silence = TimeSpan.FromMilliseconds(maintenant - Volatile.Read(ref _derniereReponse));
                if (silence > _seuil && !gelSignale)
                {
                    gelSignale = true;
                    debutGel = Volatile.Read(ref _derniereReponse);
                    _journal.Erreur($"Fil d'interface sans réponse depuis {silence.TotalSeconds:0} s : gel en cours, dump demandé.");
                    EcrireDump();
                }
                else if (silence <= _seuil && gelSignale)
                {
                    gelSignale = false;
                    _journal.Avertissement(
                        $"Fil d'interface de nouveau réactif après un gel d'environ {(maintenant - debutGel) / 1000.0:0} s.");
                }
            }
            catch (Exception exception)
            {
                // La surveillance ne doit jamais arrêter le processus.
                _journal.Erreur("Surveillance du fil d'interface en échec.", exception);
            }
        }
    }

    // Depuis ce fil : le fil d'interface est figé, mais le reste du processus répond. createdump n'est pas utilisable,
    // .NET 10 refuse qu'on le lance sur un numéro de processus.
    private void EcrireDump()
    {
        Directory.CreateDirectory(_dossierDumps);
        var chemin = Path.Combine(_dossierDumps, $"gel-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.dmp");
        var chrono = Stopwatch.StartNew();
        bool ecrit;
        int erreur;
        using (var fichier = new FileStream(chemin, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            ecrit = NatifDump.MiniDumpWriteDump(NatifDump.GetCurrentProcess(), (uint)Environment.ProcessId,
                fichier.SafeFileHandle, NatifDump.TypeComplet, 0, 0, 0);
            erreur = Marshal.GetLastPInvokeError();
        }

        if (!ecrit)
        {
            File.Delete(chemin);
            _journal.Erreur($"Dump du gel en échec (MiniDumpWriteDump, erreur Windows {erreur}).");
            return;
        }

        _journal.Erreur($"Dump du gel écrit en {chrono.Elapsed.TotalSeconds:0} s : {chemin}");
        SupprimerAnciensDumps();
    }

    private void SupprimerAnciensDumps()
    {
        foreach (var ancien in new DirectoryInfo(_dossierDumps).GetFiles("gel-*.dmp")
                     .OrderByDescending(f => f.LastWriteTimeUtc).Skip(DumpsGardes))
        {
            try
            {
                ancien.Delete();
            }
            catch (IOException)
            {
                // Un dump encore ouvert (analyse en cours) sera supprimé la prochaine fois.
            }
        }
    }
}
