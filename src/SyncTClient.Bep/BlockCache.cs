using BepFileInfo = SyncTClient.Bep.Proto.FileInfo;

namespace SyncTClient.Bep;

/// <summary>
/// Ein kurzer Vorrat der zuletzt geholten Bloecke.
/// </summary>
/// <remarks>
/// Windows fragt sektorenweise; ein Block ist ein Vielfaches davon. Ohne
/// diesen Vorrat wird derselbe Block fuer jeden Sektor erneut geholt, und beim
/// naechsten Oeffnen der Datei noch einmal. Gemessen an einer 150-KB-Datei:
/// 38 Anfragen und 4,3 MB fuer 150 KB.
///
/// Das ist nicht die Zwischenspeicherung auf der Platte. Der Vorrat haelt nur,
/// was in den letzten Sekunden gebraucht wurde, und ist hart begrenzt; was am
/// laengsten nicht mehr gefragt wurde, faellt zuerst heraus. Er kann deshalb
/// nicht vollaufen.
///
/// Jeder Eintrag traegt die Pruefsumme seines Blocks. Bei einer Aenderung an
/// der Gegenstelle stimmt sie nicht mehr, der Eintrag gilt als nicht vorhanden
/// und wird verworfen -- statt eine alte Fassung zu liefern.
/// </remarks>
public sealed class BlockCache
{
    /// <summary>So viel darf der Vorrat insgesamt halten.</summary>
    /// <remarks>
    /// Bei 128-KiB-Bloecken sind das 256 Stueck. Das ist wenig genug, um neben
    /// jeder anderen Belegung zu bestehen, und viel genug, um die Dateien
    /// eines Durchgangs zu halten.
    /// </remarks>
    public const long MaximumBytes = 32L * 1024 * 1024;

    private sealed record Eintrag(string Name, long Offset, byte[] Daten, byte[] Hash);

    private readonly Lock _gate = new();

    /// <summary>Zeiger je Block, damit ein Treffer nicht gesucht werden muss.</summary>
    private readonly Dictionary<(string Name, long Offset), LinkedListNode<Eintrag>> _index = [];

    /// <summary>Reihenfolge: vorn, was zuletzt gebraucht wurde.</summary>
    private readonly LinkedList<Eintrag> _reihe = [];

    private long _belegt;

    /// <summary>Was der Vorrat gerade haelt. Fuer das Protokoll.</summary>
    public long Bytes { get { lock (_gate) return _belegt; } }

    public int Count { get { lock (_gate) return _index.Count; } }

    /// <summary>
    /// Setzt einen blockbuendigen Bereich aus dem Vorrat zusammen.
    /// </summary>
    /// <remarks>
    /// Erst wird geprueft, dann kopiert: eine halb gefuellte Antwort waere
    /// schlimmer als gar keine. Fehlt auch nur ein Block, wird der ganze
    /// Bereich geholt -- billiger als ein zweiter Weg durch die Blockliste.
    /// </remarks>
    public bool TryAssemble(
        string name, BepFileInfo file, long anfang, long laenge, out byte[] daten)
    {
        daten = [];
        if (laenge <= 0 || laenge > int.MaxValue) return false;

        lock (_gate)
        {
            foreach (var block in file.Blocks)
            {
                if (block.Offset < anfang || block.Offset + block.Size > anfang + laenge) continue;

                if (!_index.TryGetValue((name, block.Offset), out var knoten)) return false;
                if (!knoten.Value.Hash.AsSpan().SequenceEqual(block.Hash.Span)) return false;
            }

            var ziel = new byte[laenge];
            var gefuellt = false;

            foreach (var block in file.Blocks)
            {
                if (block.Offset < anfang || block.Offset + block.Size > anfang + laenge) continue;

                var knoten = _index[(name, block.Offset)];
                knoten.Value.Daten.AsSpan().CopyTo(ziel.AsSpan((int)(block.Offset - anfang)));

                _reihe.Remove(knoten);
                _reihe.AddFirst(knoten);
                gefuellt = true;
            }

            if (!gefuellt) return false;

            daten = ziel;
            return true;
        }
    }

    /// <summary>Legt die Bloecke eines soeben geholten Bereichs ab.</summary>
    public void Ablegen(string name, BepFileInfo file, long anfang, byte[] daten)
    {
        foreach (var block in file.Blocks)
        {
            if (block.Offset < anfang || block.Offset + block.Size > anfang + daten.Length) continue;

            var teil = new byte[block.Size];
            Buffer.BlockCopy(daten, (int)(block.Offset - anfang), teil, 0, block.Size);
            Put(name, block.Offset, teil, block.Hash.Span);
        }
    }

    private void Put(string name, long offset, byte[] daten, ReadOnlySpan<byte> hash)
    {
        // Ein einzelner Block ueber dem ganzen Vorrat wird nicht gehalten:
        // er verdraengte alles andere und waere beim naechsten Mal doch weg.
        if (daten.Length == 0 || daten.Length > MaximumBytes) return;

        var kopie = hash.ToArray();

        lock (_gate)
        {
            if (_index.TryGetValue((name, offset), out var vorhanden)) Entfernen(vorhanden);

            while (_belegt + daten.Length > MaximumBytes && _reihe.Last is { } letzter)
                Entfernen(letzter);

            var knoten = _reihe.AddFirst(new Eintrag(name, offset, daten, kopie));
            _index[(name, offset)] = knoten;
            _belegt += daten.Length;
        }
    }

    public void Leeren()
    {
        lock (_gate)
        {
            _index.Clear();
            _reihe.Clear();
            _belegt = 0;
        }
    }

    private void Entfernen(LinkedListNode<Eintrag> knoten)
    {
        _reihe.Remove(knoten);
        _index.Remove((knoten.Value.Name, knoten.Value.Offset));
        _belegt -= knoten.Value.Daten.Length;
    }
}
