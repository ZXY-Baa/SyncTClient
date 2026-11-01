using System.ComponentModel;
using System.Runtime.CompilerServices;
using SyncTClient.Mount;

namespace SyncTClient.Gui;

/// <summary>Eine Gegenstelle.</summary>
public sealed class PeerItem(PeerHost host) : INotifyPropertyChanged
{
    public PeerHost Host { get; } = host;
    public PeerConfig Config => Host.Config;

    public string Display => Host.Display;

    public string StateText => Host.State switch
    {
        PeerState.Verbindet => App.S("I.Connecting"),
        PeerState.Verbunden => App.S("I.Connected"),
        PeerState.Fehler => Fehlertext,
        _ => App.S("I.Disconnected")
    };

    /// <summary>
    /// Der Grund des Fehlschlags, so weit er sich benennen laesst.
    /// </summary>
    /// <remarks>
    /// Fuer die bekannten Faelle steht hier ein eigener Satz. Nur fuer den Rest
    /// wird die Meldung der Ausnahme gezeigt -- sie kommt aus dem
    /// Laufzeitpaket, dessen Sprachdateien nicht mitgeliefert werden, und
    /// stuende sonst auf Englisch da.
    /// </remarks>
    private string Fehlertext => Host.Fehlerart switch
    {
        PeerFehler.Abbruch => App.S("I.Canceled"),
        PeerFehler.Zeitueberschreitung => App.S("I.Timeout"),
        PeerFehler.Netz => App.S("I.NetworkError"),
        PeerFehler.Adresse => App.S("I.NoAddress"),
        _ => Host.LastError is null
            ? App.S("I.Error")
            : App.S("I.ErrorWith", Kurz(Host.LastError))
    };

    public string Detail => Host.State == PeerState.Verbunden
        ? $"{Host.Config.Address} · {Host.ClientVersion}"
        : Host.Config.Address;

    private static string Kurz(string text)
        => text.Length <= 60 ? text : text[..57] + "...";

    public void Refresh()
    {
        Notify(nameof(Display));
        Notify(nameof(StateText));
        Notify(nameof(Detail));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
