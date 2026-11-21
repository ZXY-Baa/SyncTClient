using System.Security.AccessControl;
using System.Security.Principal;

namespace SyncTClient.Mount;

/// <summary>
/// Die Zugriffsrechte auf dem Konfigurationsordner.
/// </summary>
/// <remarks>
/// In diesem Ordner liegt der private Schluessel des Geraets
/// (<c>device.pfx</c>, daneben <c>device.key</c>). Wer ihn lesen kann, kann
/// sich gegenueber den Gegenstellen als dieses Geraet ausgeben -- und die
/// haben ihm bereits vertraut.
///
/// Unter <c>%LOCALAPPDATA%</c> vergibt Windows das von selbst nur an den
/// angemeldeten Benutzer, die Administration und SYSTEM. Geprueft wird
/// trotzdem: eine Kopie des Ordners, ein Werkzeug oder eine aeltere Fassung
/// koennen die Vererbung veraendert haben.
///
/// Gesetzt wird nur an einem Ordner, den dieser Lauf selbst angelegt hat. An
/// einem bestehenden die Vererbung abzuschalten waere ein Eingriff, dessen
/// Folgen -- Sicherungswerkzeuge, Wanderprofile, Dienste -- niemand hier
/// ueberblickt.
/// </remarks>
public static class Konfigurationsrechte
{
    /// <summary>Legt den Ordner an und setzt seine Rechte, wenn er neu ist.</summary>
    public static void Anlegen(string ordner)
    {
        try
        {
            if (Directory.Exists(ordner)) return;
            Directory.CreateDirectory(ordner);
        }
        catch (Exception)
        {
            // Ohne Ordner kein Programm. Der Aufrufer merkt es beim Schreiben.
            return;
        }

        Setzen(ordner);
    }

    /// <summary>Meldet, wenn ein bestehender Ordner fuer alle lesbar steht.</summary>
    public static void Pruefen(string ordner, Action<string> melden)
    {
        try
        {
            var sicherheit = new DirectoryInfo(ordner).GetAccessControl();

            foreach (FileSystemAccessRule regel in
                     sicherheit.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                if (regel.AccessControlType != AccessControlType.Allow) continue;
                if (!IstOffen((SecurityIdentifier)regel.IdentityReference)) continue;

                melden($"Der Konfigurationsordner {ordner} steht fuer {regel.IdentityReference} offen. " +
                       "Darin liegt der private Schluessel dieses Geraets; wer ihn lesen kann, " +
                       "kann sich gegenueber den Gegenstellen als dieses Geraet ausgeben.");
                return;
            }
        }
        catch (Exception)
        {
            // Rechte nicht lesbar: kein Grund, den Start zu stoeren.
        }
    }

    /// <summary>
    /// Vererbung aus, dann ausdruecklich: der angemeldete Benutzer, die
    /// Administration und SYSTEM.
    /// </summary>
    /// <remarks>
    /// Das ist, was Windows an einem eigenen Ordner selbst vergibt -- ohne
    /// "Benutzer" und ohne "Jeder". Die Administration bleibt drin, weil ein
    /// Sicherungswerkzeug sonst nicht mehr an die Dateien kaeme.
    /// </remarks>
    private static void Setzen(string ordner)
    {
        try
        {
            var info = new DirectoryInfo(ordner);
            var sicherheit = info.GetAccessControl();
            sicherheit.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            var kennungen = new[]
            {
                WindowsIdentity.GetCurrent().User,
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)
            };

            foreach (var kennung in kennungen)
            {
                if (kennung is null) continue;

                sicherheit.AddAccessRule(new FileSystemAccessRule(
                    kennung,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
            }

            info.SetAccessControl(sicherheit);
        }
        catch (Exception)
        {
            // Nicht setzbar: die Rechte bleiben, wie sie waren.
        }
    }

    private static bool IstOffen(SecurityIdentifier kennung)
        => kennung.IsWellKnown(WellKnownSidType.WorldSid)
           || kennung.IsWellKnown(WellKnownSidType.BuiltinUsersSid)
           || kennung.IsWellKnown(WellKnownSidType.AuthenticatedUserSid)
           || kennung.IsWellKnown(WellKnownSidType.BuiltinGuestsSid);
}
