namespace PressePapiers.Coeur.Clavier;

/// <summary>Résultat de <see cref="TableDesTouches.Interpreter"/> : l'action, et pour <see cref="ActionTouche.AllerAOnglet"/> le numéro d'onglet (1 à 9).</summary>
/// <param name="Action">Action à faire.</param>
/// <param name="Numero">Numéro d'onglet, de 1 à 9 ; zéro pour toute autre action.</param>
public readonly record struct CommandeClavier(ActionTouche Action, int Numero = 0);
