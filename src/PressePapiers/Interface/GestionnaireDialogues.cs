using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PressePapiers.Interface;

/// <summary>
/// Ouvre les <see cref="ContentDialog"/> sur la racine du contenu de la fenêtre (spec, section 9) : un seul à la fois,
/// et seulement quand la fenêtre est affichée (WinUI 3 refuse d'ouvrir une boîte sur une fenêtre masquée).
/// </summary>
public sealed class GestionnaireDialogues : IDialogues
{
    private readonly Func<FrameworkElement?> _racine;
    private readonly Func<bool> _fenetreAffichee;
    private ContentDialog? _ouverte;

    /// <summary>Crée le gestionnaire.</summary>
    /// <param name="racine">Racine du contenu de la fenêtre : elle fournit le XamlRoot et le thème.</param>
    /// <param name="fenetreAffichee">Dit si la fenêtre est affichée.</param>
    public GestionnaireDialogues(Func<FrameworkElement?> racine, Func<bool> fenetreAffichee)
    {
        _racine = racine;
        _fenetreAffichee = fenetreAffichee;
    }

    /// <inheritdoc />
    public bool EnCours => _ouverte is not null;

    /// <inheritdoc />
    public async Task<bool> ConfirmerAsync(string titre, string message, string libelleConfirmation)
    {
        var dialogue = Creer(titre, libelleConfirmation);
        dialogue.Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
        return await AfficherAsync(dialogue) == ContentDialogResult.Primary;
    }

    /// <inheritdoc />
    public async Task<string?> DemanderNomAsync(string titre, string nomInitial, string libelleValidation)
    {
        var dialogue = Creer(titre, libelleValidation);
        var champ = new TextBox { Text = nomInitial, PlaceholderText = "Nom", MaxLength = 120 };
        dialogue.IsPrimaryButtonEnabled = nomInitial.Trim().Length > 0;
        champ.TextChanged += (_, _) => dialogue.IsPrimaryButtonEnabled = champ.Text.Trim().Length > 0;
        dialogue.Content = champ;
        dialogue.Opened += (_, _) =>
        {
            champ.Focus(FocusState.Programmatic);
            champ.SelectAll();
        };

        return await AfficherAsync(dialogue) == ContentDialogResult.Primary ? champ.Text.Trim() : null;
    }

    private static ContentDialog Creer(string titre, string libelleConfirmation) => new()
    {
        Title = titre,
        PrimaryButtonText = libelleConfirmation,
        CloseButtonText = "Annuler",
        DefaultButton = ContentDialogButton.Primary,
    };

    private async Task<ContentDialogResult> AfficherAsync(ContentDialog dialogue)
    {
        if (_ouverte is not null || !_fenetreAffichee() || _racine() is not { XamlRoot: { } racine } element)
        {
            return ContentDialogResult.None;
        }

        dialogue.XamlRoot = racine;
        dialogue.RequestedTheme = element.ActualTheme;
        _ouverte = dialogue;
        try
        {
            return await dialogue.ShowAsync();
        }
        finally
        {
            _ouverte = null;
        }
    }
}
