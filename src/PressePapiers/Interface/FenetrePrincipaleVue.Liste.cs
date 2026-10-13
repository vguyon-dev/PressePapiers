namespace PressePapiers.Interface;

// Relecture du dépôt : onglets, éléments, recherche différée, regroupement des écritures venues d'un autre fil.
public sealed partial class FenetrePrincipaleVue
{
    /// <summary>Choisit l'onglet à afficher : la recherche est effacée et le premier élément est choisi.</summary>
    public void ChoisirOnglet(OngletVue onglet)
    {
        ArgumentNullException.ThrowIfNull(onglet);
        ChangerRecherche(string.Empty);
        OngletSelectionne = onglet;
        Rafraichir(premierElement: true);
    }

    /// <summary>Efface la recherche sans attendre le délai de frappe.</summary>
    public void EffacerRecherche()
    {
        ChangerRecherche(string.Empty);
        Rafraichir(premierElement: true);
    }

    /// <summary>Relit les onglets, leurs compteurs et la liste. À appeler sur le fil d'interface.</summary>
    /// <param name="premierElement">Vrai pour choisir le premier élément ; faux pour garder l'élément courant s'il existe encore.</param>
    public void Rafraichir(bool premierElement = false)
    {
        _rafraichissementEnAttente = false;
        try
        {
            var onglets = _depot.ListerOnglets().Select(o => OngletVue.De(o, _depot.CompterElements(o.Id))).ToList();
            var courant = onglets.FirstOrDefault(o => o.Id == OngletSelectionne?.Id) ?? onglets[0];
            Onglets = onglets;
            OngletSelectionne = courant;
            ReconstruireElements(onglets, courant, premierElement);
        }
        catch (Exception exception)
        {
            _journal.Erreur("Impossible de relire l'historique pour l'afficher.", exception);
        }

        Rafraichi?.Invoke();
    }

    /// <summary>Mémorise la taille de la fenêtre (pixels logiques) dans les réglages, si elle a changé.</summary>
    public void MemoriserTaille(int largeur, int hauteur)
    {
        if (largeur <= 0 || hauteur <= 0 || (largeur == Reglages.LargeurFenetre && hauteur == Reglages.HauteurFenetre))
        {
            return;
        }

        var nouveaux = Reglages with { LargeurFenetre = largeur, HauteurFenetre = hauteur };
        try
        {
            _parametres.Enregistrer(nouveaux);
            Reglages = nouveaux;
        }
        catch (Exception exception)
        {
            _journal.Avertissement("Impossible de mémoriser la taille de la fenêtre.", exception);
        }
    }

    // Change la saisie sans déclencher la relecture différée : l'appelant relit lui-même.
    private void ChangerRecherche(string valeur)
    {
        _minuteurRecherche.Stop();
        _rechercheInterne = true;
        try
        {
            Recherche = valeur;
        }
        finally
        {
            _rechercheInterne = false;
        }
    }

    partial void OnRechercheChanged(string value)
    {
        if (_rechercheInterne)
        {
            return;
        }

        // Chaque frappe repousse l'échéance ; une saisie vidée met la liste à jour tout de suite.
        _minuteurRecherche.Stop();
        if (string.IsNullOrWhiteSpace(value))
        {
            Rafraichir(premierElement: true);
        }
        else
        {
            _minuteurRecherche.Start();
        }
    }

    private void ReconstruireElements(List<OngletVue> onglets, OngletVue courant, bool premierElement)
    {
        var idAvant = ElementSelectionne?.Id;
        var indexAvant = -1;
        for (var i = 0; i < Elements.Count; i++)
        {
            if (Elements[i].Id == idAvant)
            {
                indexAvant = i;
                break;
            }
        }

        IReadOnlyList<PressePapiers.Coeur.Modele.Element> source;
        Dictionary<long, string>? noms = null;
        if (EnRecherche)
        {
            source = _recherche.Chercher(Recherche);
            noms = onglets.ToDictionary(o => o.Id, o => o.Nom);
        }
        else
        {
            source = _depot.ListerElements(courant.Id);
        }

        var lignes = source
            .Select(e => new ElementVue(e, noms?.GetValueOrDefault(e.OngletId),
                                        ResumeElement.EstChasseFixe(e.Application, _chasseFixe), Horloge))
            .ToList();

        _relecture = true;
        try
        {
            Elements = lignes;
        }
        finally
        {
            _relecture = false;
        }

        if (lignes.Count == 0)
        {
            SetProperty(ref _elementSelectionne, null, nameof(ElementSelectionne));
        }
        else if (premierElement || idAvant is null)
        {
            ElementSelectionne = lignes[0];
        }
        else
        {
            // L'élément courant reste choisi ; s'il a disparu, le suivant prend sa place (le dernier si on était en bas).
            ElementSelectionne = lignes.FirstOrDefault(l => l.Id == idAvant) ?? lignes[Math.Clamp(indexAvant, 0, lignes.Count - 1)];
        }
    }

    private void OnDepotModifie()
    {
        // Peut arriver de n'importe quel fil (la capture écrit hors du fil d'interface).
        if (_file.HasThreadAccess)
        {
            PlanifierRafraichissement();
        }
        else
        {
            _file.TryEnqueue(PlanifierRafraichissement);
        }
    }

    private void PlanifierRafraichissement()
    {
        if (!Affichee)
        {
            _rafraichissementEnAttente = true;
            return;
        }

        if (_rafraichissementPlanifie)
        {
            return;
        }

        // Plusieurs écritures de suite donnent une seule relecture.
        _rafraichissementPlanifie = true;
        _file.TryEnqueue(() =>
        {
            _rafraichissementPlanifie = false;
            Rafraichir();
        });
    }
}
