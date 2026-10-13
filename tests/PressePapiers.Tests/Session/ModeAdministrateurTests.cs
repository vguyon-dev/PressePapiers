using PressePapiers.Coeur.Session;

namespace PressePapiers.Tests.Session;

public sealed class ModeAdministrateurTests
{
    private static readonly string[] Proteges = [@"C:\Program Files", @"C:\Program Files (x86)", @"C:\Windows"];

    [Theory]
    [InlineData(@"C:\Program Files\PressePapiers\PressePapiers.exe")]
    [InlineData(@"c:\program files\pressepapiers\pressepapiers.exe")]       // casse libre
    [InlineData(@"C:\Program Files (x86)\PressePapiers\PressePapiers.exe")]
    [InlineData(@"C:\Program Files\PressePapiers\..\PressePapiers\PressePapiers.exe")]   // normalisé avant comparaison
    public void Un_processus_eleve_dans_un_dossier_protege_est_l_installation_administrateur(string chemin)
    {
        Assert.True(ModeAdministrateur.EstInstallationAdministrateur(true, chemin, Proteges));
    }

    [Fact]
    public void Un_processus_non_eleve_n_est_jamais_l_installation_administrateur()
    {
        Assert.False(ModeAdministrateur.EstInstallationAdministrateur(
            false, @"C:\Program Files\PressePapiers\PressePapiers.exe", Proteges));
    }

    [Theory]
    [InlineData(@"C:\Users\valentin\AppData\Local\Programs\PressePapiers\PressePapiers.exe")]
    [InlineData(@"C:\Mes Projets\Autres\PressePapiers\src\PressePapiers\bin\Debug\PressePapiers.exe")]
    [InlineData(@"C:\Program Files Extra\PressePapiers.exe")]                 // même début de nom, autre dossier
    [InlineData(@"C:\Program Files\..\Users\x\PressePapiers.exe")]              // remonte hors du dossier protégé
    [InlineData(@"D:\Program Files\PressePapiers\PressePapiers.exe")]           // autre disque
    public void Un_processus_eleve_hors_des_dossiers_proteges_n_est_pas_l_installation_administrateur(string chemin)
    {
        Assert.False(ModeAdministrateur.EstInstallationAdministrateur(true, chemin, Proteges));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_chemin_inconnu_n_est_pas_l_installation_administrateur(string? chemin)
    {
        Assert.False(ModeAdministrateur.EstInstallationAdministrateur(true, chemin, Proteges));
    }

    [Fact]
    public void Les_dossiers_proteges_vides_sont_ignores_et_une_liste_vide_ne_reconnait_rien()
    {
        var chemin = @"C:\Program Files\PressePapiers\PressePapiers.exe";

        Assert.False(ModeAdministrateur.EstInstallationAdministrateur(true, chemin, []));
        Assert.False(ModeAdministrateur.EstInstallationAdministrateur(true, chemin, [null, "", "  "]));
        Assert.True(ModeAdministrateur.EstInstallationAdministrateur(true, chemin, [null, @"C:\Program Files\"]));
    }

    [Fact]
    public void Un_dossier_protege_aux_caracteres_invalides_est_ignore_sans_exception()
    {
        var chemin = @"C:\Program Files\PressePapiers\PressePapiers.exe";

        Assert.True(ModeAdministrateur.EstInstallationAdministrateur(true, chemin, ["C:\\a\0b", @"C:\Program Files"]));
    }
}
