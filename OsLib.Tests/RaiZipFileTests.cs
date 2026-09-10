namespace OsLib.Tests;

public sealed class RaiZipFileTests : IDisposable
{
	private readonly RaiPath root = Os.TempDir / "RAIkeep" / "oslib-tests" / "immutable-zip";

	public RaiZipFileTests()
	{
		Cleanup();
		root.mkdir();
	}

	public void Dispose() => Cleanup();

	[Fact]
	public void CreateImmutable_WritesCollectionAtFinalPath_AndValidatesExactPayloads()
	{
		var source = new TextFile(root, "first", "event")
		{
			Lines = ["first payload"],
			Changed = true
		};
		source.Save();
		var archive = new RaiZipFile(root, "Events_20260910-1200_to_20260910-1201");

		var write = archive.CreateImmutable([
			RaiZipEntry.FromFile(source),
			RaiZipEntry.FromText("second.event", "second payload")
		]);

		Assert.Equal(RaiZipWriteStatus.Created, write.Status);
		Assert.True(archive.Exists());
		Assert.True(archive.TryReadEntries(out var entries, out var problem), problem);
		Assert.Equal(["first.event", "second.event"], entries.Keys);
		Assert.Equal(source.ReadAllText(), System.Text.Encoding.UTF8.GetString(entries["first.event"].Content));
		Assert.Empty(root.EnumerateFiles("*.tmp"));
	}

	[Fact]
	public void CreateImmutable_IdenticalRetryDoesNotRewrite_AndDifferentRetryDoesNotReplace()
	{
		var archive = new RaiZipFile(root, "Events_20260910-1200_to_20260910-1201");
		var original = new[] { RaiZipEntry.FromText("one.event", "one") };
		Assert.Equal(RaiZipWriteStatus.Created, archive.CreateImmutable(original).Status);
		var firstWrite = archive.LastWriteTimeUtc;

		Assert.Equal(RaiZipWriteStatus.ExistingIdentical, archive.CreateImmutable(original).Status);
		Assert.Equal(firstWrite, archive.LastWriteTimeUtc);

		var different = archive.CreateImmutable([RaiZipEntry.FromText("one.event", "changed")]);
		Assert.Equal(RaiZipWriteStatus.ExistingDifferent, different.Status);
		Assert.Equal(firstWrite, archive.LastWriteTimeUtc);
		Assert.True(archive.TryReadEntries(out var entries, out var problem), problem);
		Assert.Equal("one", System.Text.Encoding.UTF8.GetString(entries["one.event"].Content));
	}

	[Fact]
	public void CreateImmutable_CorruptExistingArchiveIsPreservedAndReportedInvalid()
	{
		var archive = new TextFile(root, "Events_20260910-1200_to_20260910-1201", "zip")
		{
			Lines = ["not a zip"],
			Changed = true
		};
		archive.Save();
		var before = archive.ReadAllText();

		var result = new RaiZipFile(archive.FullName)
			.CreateImmutable([RaiZipEntry.FromText("one.event", "one")]);

		Assert.Equal(RaiZipWriteStatus.ExistingInvalid, result.Status);
		Assert.Equal(before, archive.ReadAllText());
	}

	[Fact]
	public void CreateImmutable_MissingParentFailsWithoutCreatingDirectory()
	{
		var missing = root / "missing";
		var archive = new RaiZipFile(missing, "Events_20260910-1200_to_20260910-1201");

		Assert.Throws<RaiPathNotFoundException>(() =>
			archive.CreateImmutable([RaiZipEntry.FromText("one.event", "one")]));
		Assert.False(missing.Exists());
	}

	private void Cleanup()
	{
		try
		{
			if (root.Exists()) root.rmdir(depth: 10, deleteFiles: true);
		}
		catch { }
	}
}
