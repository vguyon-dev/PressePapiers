using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace PressePapiers;

/// <summary>Point d'entrée personnalisé (symbole DISABLE_XAML_GENERATED_MAIN) : la tâche 16 y ajoutera l'instance unique.</summary>
public static class Program
{
    /// <summary>Démarre l'application WinUI 3 sur un fil à appartement unique.</summary>
    [STAThread]
    public static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(parametres =>
        {
            // Sans contexte de synchronisation, les await de l'interface reprendraient sur un fil du pool.
            var contexte = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(contexte);
            _ = new App();
        });
    }
}
