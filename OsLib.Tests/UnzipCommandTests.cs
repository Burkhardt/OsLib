using System.IO.Compression;

namespace OsLib.Tests;

public sealed class UnzipCommandTests : IDisposable
{
	private readonly RaiPath root = Os.TempDir / ("RAIkeep-unzip-" + Guid.NewGuid().ToString("N"));

	[Fact]
	public async Task NativeExtractionPreservesNestedNamesAndBytesOnWorkerThread()
	{
		root.mkdir();
		var archive = new RaiFile(root, "photos ; $(not-a-command).zip");
		var payload = new byte[] { 0, 1, 13, 10, 255 };
		using (var zip = new ZipArchive(archive.OpenNew(), ZipArchiveMode.Create))
		using (var stream = zip.CreateEntry("nested/Nomsa San Diego_0001.jpg").Open()) stream.Write(payload);
		var destination = root / "extract with spaces";
		var caller = Environment.CurrentManagedThreadId;
		var result = await new UnzipCommand().ExtractAsync(archive, destination, TestContext.Current.CancellationToken);
		Assert.True(result.Succeeded, result.Output);
		Assert.NotEqual(caller, result.WorkerThreadId);
		Assert.Equal(payload, new RaiFile(destination / "nested", "Nomsa San Diego_0001.jpg").ReadAllBytes());
		await Assert.ThrowsAsync<IOException>(() => new UnzipCommand().ExtractAsync(archive, destination, TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task CorruptArchiveReportsNonzeroExitWithoutInteractiveInput()
	{
		root.mkdir();
		var archive = new RaiFile(root, "broken.zip");
		new TextFile(archive.FullName).WriteAllText("not a ZIP archive");
		var result = await new UnzipCommand().ExtractAsync(archive, root / "extract", TestContext.Current.CancellationToken);
		Assert.False(result.Succeeded);
		Assert.False(string.IsNullOrWhiteSpace(result.Output));
	}

	[Fact]
	public async Task CancellationTerminatesTheChildAndReturnsPromptly()
	{
		root.mkdir();
		var archive = new RaiFile(root, "input.zip");
		new TextFile(archive.FullName).WriteAllText("input");
		// Exercise the same RaiSystem launch/cancellation path without timing a large real archive.
		var script = RaiSystem.CreateScript(root, "slow-unzip.sh", "#!/bin/sh\nsleep 30\n");
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		cancellation.CancelAfter(TimeSpan.FromMilliseconds(300));
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new UnzipCommand(script.FullName)
			.ExtractAsync(archive, root / "extract", cancellation.Token)
			.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
	}

	public void Dispose() => root.rmdir(deleteFiles: true);
}
