using SyncTClient.Bep;
using ProtoFileInfo = SyncTClient.Bep.Proto.FileInfo;

namespace SyncTClient.Tests;

public class FileFetcherTests
{
    [Fact]
    public async Task Leere_Datei_liefert_leeren_Inhalt_ohne_Peeranfrage()
    {
        var file = new ProtoFileInfo { Name = "empty.txt", Size = 0 };

        var result = await FileFetcher.FetchRangeAsync(
            null!, "folder", file, 0, 0);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Leere_Datei_erlaubt_keinen_nichtleeren_Bereich()
    {
        var file = new ProtoFileInfo { Name = "empty.txt", Size = 0 };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => FileFetcher.FetchRangeAsync(null!, "folder", file, 0, 1));
    }

    [Fact]
    public async Task Bereich_mit_long_Ueberlauf_wird_abgewiesen()
    {
        var file = new ProtoFileInfo { Name = "large.bin", Size = long.MaxValue };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => FileFetcher.FetchRangeAsync(
                null!, "folder", file, long.MaxValue - 4, 10));
    }

    [Fact]
    public async Task Negativer_Dateiumfang_wird_als_ungueltige_Metadaten_abgewiesen()
    {
        var file = new ProtoFileInfo { Name = "invalid.bin", Size = -1 };

        await Assert.ThrowsAsync<InvalidDataException>(
            () => FileFetcher.FetchRangeAsync(null!, "folder", file, 0, 0));
    }

    [Fact]
    public async Task Datei_ohne_Bloecke_aber_mit_Inhalt_bleibt_ein_Fehler()
    {
        var file = new ProtoFileInfo { Name = "missing-blocks.bin", Size = 1 };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => FileFetcher.FetchRangeAsync(null!, "folder", file, 0, 1));
    }
}
