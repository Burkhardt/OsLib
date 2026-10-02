using System.IO.Compression;

namespace OsLib.Tests;

public sealed class UnzipCommandTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "RAIkeep-unzip-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task NativeExtractionPreservesNestedNamesAndBytesOnWorkerThread()
    {
        Directory.CreateDirectory(root);
        var archive = new RaiFile(Path.Combine(root, "photos ; $(not-a-command).zip"));
        var payload = new byte[] { 0, 1, 13, 10, 255 };
        using (var zip = ZipFile.Open(archive.FullName, ZipArchiveMode.Create))
        using (var stream = zip.CreateEntry("nested/Nomsa San Diego_0001.jpg").Open()) stream.Write(payload);
        var destination = new RaiPath(Path.Combine(root, "extract with spaces"));
        var caller = Environment.CurrentManagedThreadId;
        var result = await new UnzipCommand().ExtractAsync(archive, destination, TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded, result.Output);
        Assert.NotEqual(caller, result.WorkerThreadId);
        Assert.Equal(payload, File.ReadAllBytes(Path.Combine(destination.FullPath, "nested/Nomsa San Diego_0001.jpg")));
        await Assert.ThrowsAsync<IOException>(() => new UnzipCommand().ExtractAsync(archive, destination));
    }

    [Fact]
    public async Task CorruptArchiveReportsNonzeroExitWithoutInteractiveInput()
    {
        Directory.CreateDirectory(root);
        var archive = new RaiFile(Path.Combine(root, "broken.zip"));
        File.WriteAllText(archive.FullName, "not a ZIP archive");
        var result = await new UnzipCommand().ExtractAsync(archive, new RaiPath(Path.Combine(root, "extract")), TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Output));
    }

    [Fact]
    public async Task CancellationTerminatesTheChildAndReturnsPromptly()
    {
        Directory.CreateDirectory(root);
        var archive = new RaiFile(Path.Combine(root, "input.zip"));
        File.WriteAllText(archive.FullName, "input");
        // Exercise the same RaiSystem launch/cancellation path without timing a large real archive.
        var script = RaiSystem.CreateScript(new RaiPath(root), "slow-unzip.sh", "#!/bin/sh\nsleep 30\n");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new UnzipCommand(script.FullName)
            .ExtractAsync(archive, new RaiPath(Path.Combine(root, "extract")), cancellation.Token)
            .WaitAsync(TimeSpan.FromSeconds(10)));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
