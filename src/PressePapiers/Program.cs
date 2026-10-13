using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using PressePapiers.Coeur.Stockage;
using PressePapiers.Natif;
using PressePapiers.Systeme;

namespace PressePapiers;

/// <summary>
/// Point d'entrée personnalisé (symbole DISABLE_XAML_GENERATED_MAIN) : instance unique avant toute création de fenêtre,
/// selon la documentation du Windows App SDK sur l'instanciation d'une application (spec, section 12).
/// </summary>
public static class Program
{
    // Clé d'instance : deux lancements qui la partagent sont la même application.
    private const string CleInstance = "PressePapiers";

    // Au-delà, la première instance est jugée muette : la seconde s'arrête quand même, en le notant.
    private const uint DelaiRedirectionMs = 15_000;

    /// <summary>Démarre l'application WinUI 3 sur un fil à appartement unique, ou redirige vers l'instance déjà lancée.</summary>
    [STAThread]
    public static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        // Avant toute fenêtre : la décision se prend tant que le processus n'a rien créé.
        var principale = AppInstance.FindOrRegisterForKey(CleInstance);
        if (!principale.IsCurrent)
        {
            RedirigerVers(principale);
            return;
        }

        // L'événement arrive sur un fil quelconque : App le ramène sur le fil d'interface.
        principale.Activated += (_, _) => App.SignalerActivation();

        Application.Start(parametres =>
        {
            // Sans contexte de synchronisation, les await de l'interface reprendraient sur un fil du pool.
            var contexte = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(contexte);
            _ = new App();
        });
    }

    // Transmet l'activation à l'instance principale puis rend la main (le processus se termine ensuite). L'appel est lancé
    // dans une tâche, car RedirectActivationToAsync a besoin du fil STA : on attend donc par CoWaitForMultipleObjects, qui
    // laisse ce fil pomper ses messages, au lieu d'un Wait() qui le bloquerait.
    private static void RedirigerVers(AppInstance principale)
    {
        var arguments = AppInstance.GetCurrent().GetActivatedEventArgs();
        var termine = NatifDemarrage.CreerEvenement(0, manuel: true, signale: false, 0);
        if (termine == 0)
        {
            return;
        }

        Exception? echec = null;
        _ = Task.Run(async () =>
        {
            try
            {
                await principale.RedirectActivationToAsync(arguments);
            }
            catch (Exception exception)
            {
                echec = exception;
            }
            finally
            {
                NatifDemarrage.SetEvent(termine);
            }
        });

        var resultat = NatifDemarrage.AttendreSansBloquer(termine, DelaiRedirectionMs);

        // Le handle reste ouvert en cas de délai dépassé : la tâche peut encore le signaler, et le processus s'arrête de toute façon.
        if (resultat == 0)
        {
            NatifDemarrage.CloseHandle(termine);
        }

        NoterRedirection(resultat == 0 ? echec : new TimeoutException("L'instance principale n'a pas répondu."));
    }

    // Une trace au journal de l'instance principale (fichier partagé). Jamais d'exception : cette instance va s'arrêter.
    private static void NoterRedirection(Exception? echec)
    {
        try
        {
            using var journal = new JournalSerilog(DossierDonnees.ParDefaut().Journal);
            if (echec is null)
            {
                journal.Info($"Seconde instance (processus {Environment.ProcessId}) : activation redirigée vers l'instance principale, arrêt.");
            }
            else
            {
                journal.Avertissement($"Seconde instance (processus {Environment.ProcessId}) : redirection impossible, arrêt.", echec);
            }
        }
        catch (Exception)
        {
            // Pas de journal possible (dossier inaccessible) : la seconde instance s'arrête sans rien dire.
        }
    }
}
