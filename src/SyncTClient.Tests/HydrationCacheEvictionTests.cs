using SyncTClient.Vfs;
using Xunit;

namespace SyncTClient.Tests;

public sealed class HydrationCacheEvictionTests
{
    [Fact]
    public void Unveraenderte_Datei_wird_nach_erfolgreichem_Dehydrate_ausgetragen()
    {
        using var temp = new TemporaryDirectory();
        var (cache, file) = CreateCache(temp.Path);

        var dehydrateCalled = false;
        cache.Wiederbeschaffbar = _ => true;

        var result = cache.Evict("file.bin", "test", _ =>
        {
            dehydrateCalled = true;
            return true;
        });

        Assert.True(result);
        Assert.True(dehydrateCalled);
        Assert.False(cache.Hat("file.bin"));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(file));
    }

    [Fact]
    public void Groessenaenderung_waehrend_des_Nachweises_verhindert_Dehydrate()
    {
        using var temp = new TemporaryDirectory();
        var (cache, file) = CreateCache(temp.Path);
        var dehydrateCalled = false;

        cache.Wiederbeschaffbar = _ =>
        {
            File.WriteAllBytes(file, [1, 2, 3, 4]);
            return true;
        };

        var result = cache.Evict("file.bin", "test", _ =>
        {
            dehydrateCalled = true;
            return true;
        });

        Assert.False(result);
        Assert.False(dehydrateCalled);
        Assert.True(cache.Hat("file.bin"));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(file));
    }

    [Fact]
    public void Aenderung_der_Schreibzeit_bei_gleicher_Groesse_verhindert_Dehydrate()
    {
        using var temp = new TemporaryDirectory();
        var (cache, file) = CreateCache(temp.Path);
        var dehydrateCalled = false;

        cache.Wiederbeschaffbar = _ =>
        {
            var previous = File.GetLastWriteTimeUtc(file);
            File.SetLastWriteTimeUtc(file, previous.AddMinutes(5));
            return true;
        };

        var result = cache.Evict("file.bin", "test", _ =>
        {
            dehydrateCalled = true;
            return true;
        });

        Assert.False(result);
        Assert.False(dehydrateCalled);
        Assert.True(cache.Hat("file.bin"));
    }

    [Fact]
    public void Geloeschte_Datei_waehrend_des_Nachweises_wird_nicht_freigegeben()
    {
        using var temp = new TemporaryDirectory();
        var (cache, file) = CreateCache(temp.Path);
        var dehydrateCalled = false;

        cache.Wiederbeschaffbar = _ =>
        {
            File.Delete(file);
            return true;
        };

        var result = cache.Evict("file.bin", "test", _ =>
        {
            dehydrateCalled = true;
            return true;
        });

        Assert.False(result);
        Assert.False(dehydrateCalled);
        Assert.True(cache.Hat("file.bin"));
    }

    [Fact]
    public void Fehlgeschlagenes_Dehydrate_laesst_den_Cacheeintrag_bestehen()
    {
        using var temp = new TemporaryDirectory();
        var (cache, _) = CreateCache(temp.Path);
        cache.Wiederbeschaffbar = _ => true;

        var result = cache.Evict("file.bin", "test", _ => false);

        Assert.False(result);
        Assert.True(cache.Hat("file.bin"));
    }

    [Fact]
    public void Fehlender_Wiederbeschaffbarkeitsnachweis_verhindert_Dehydrate()
    {
        using var temp = new TemporaryDirectory();
        var (cache, _) = CreateCache(temp.Path);
        var dehydrateCalled = false;

        var result = cache.Evict("file.bin", "test", _ =>
        {
            dehydrateCalled = true;
            return true;
        });

        Assert.False(result);
        Assert.False(dehydrateCalled);
        Assert.True(cache.Hat("file.bin"));
    }

    private static (HydrationCache Cache, string File) CreateCache(string root)
    {
        var file = Path.Combine(root, "file.bin");
        File.WriteAllBytes(file, [1, 2, 3]);

        var cache = new HydrationCache(
            root,
            limits: null,
            statePath: Path.Combine(root, "cache-state.json"));
        cache.NoteContent("file.bin", 3);

        return (cache, file);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "SyncTClient-tests-" + Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
