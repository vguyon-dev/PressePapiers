using Microsoft.UI.Xaml;

namespace PressePapiers;

/// <summary>
/// Application WinUI 3. Version minimale : elle se termine dès son lancement ;
/// le cycle de vie complet (journal, base, fenêtre, icône) vient avec la tâche 16, l'assemblage avec la tâche 29.
/// </summary>
public sealed partial class App : Application
{
    /// <summary>Charge les ressources XAML de l'application.</summary>
    public App()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Exit();
    }
}
