using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Xunit;

namespace OsLib.Tests;

/// <summary>CR022 regression coverage for the universal cloud-pathname contract.</summary>
public sealed class CloudSafeFileOperationsTests : IDisposable
{
	private readonly List<RaiPath> localCleanup = new();
	private readonly List<RaiPath> cloudCleanup = new();

	public void Dispose()
	{
		foreach (var path in localCleanup)
		{
			try { path.rmdir(depth: 8, deleteFiles: true); } catch { }
		}
		foreach (var path in cloudCleanup)
		{
			try { path.rmdir(depth: 8, deleteFiles: true); } catch { }
		}
	}

	[Fact]
	public void RaiFileMove_TempToCloud_FailsBeforeEitherPathChanges()
	{
		var sourceRoot = NewTempRoot("file-move");
		var source = new TextFile(sourceRoot, "staged", "txt", "source remains");
		var destinationRoot = NewUncreatedCloudRoot("file-move");
		var destination = new RaiFile(destinationRoot, "final", "txt");

		var exception = Assert.Throws<RaiCloudStorageException>(() => destination.mv(source));

		Assert.Equal("RaiFile.mv", exception.Operation);
		Assert.Equal(source.FullName, exception.SourcePath);
		Assert.Equal(destination.FullName, exception.DestinationPath);
		Assert.True(source.Exists());
		Assert.False(destination.Exists());
		Assert.False(destinationRoot.Exists());
	}

	[Fact]
	public void RaiPathMove_TempToCloud_FailsBeforeEitherTreeChanges()
	{
		var source = NewTempRoot("directory-move");
		new TextFile(source, "keep", "txt", "source remains");
		var destination = NewUncreatedCloudRoot("directory-move");

		var exception = Assert.Throws<RaiCloudStorageException>(() =>
			destination.mv(source, replace: false, keepBackup: false));

		Assert.Equal("RaiPath.mv", exception.Operation);
		Assert.True(source.Exists());
		Assert.True(new RaiFile(source, "keep", "txt").Exists());
		Assert.False(destination.Exists());
	}

	[Fact]
	public void RaiFileCopy_OverExistingCloudFile_KeepsPathContinuouslyPresent()
	{
		var sourceRoot = NewTempRoot("file-copy-source");
		var source = new TextFile(sourceRoot, "source", "txt", new string('x', 256_000));
		var cloudRoot = NewCloudRoot("file-copy-destination");
		var destination = new TextFile(cloudRoot, "destination", "txt", "old content");
		var events = new ConcurrentQueue<string>();

		using var watcher = new FileSystemWatcher(cloudRoot.FullPath, destination.NameWithExtension)
		{
			NotifyFilter = NotifyFilters.FileName,
			IncludeSubdirectories = false,
			EnableRaisingEvents = true
		};
		watcher.Deleted += (_, e) => events.Enqueue($"Deleted:{e.Name}");
		watcher.Renamed += (_, e) => events.Enqueue($"Renamed:{e.OldName}->{e.Name}");

		var stop = false;
		var alwaysPresent = true;
		var observer = new Thread(() =>
		{
			while (!Volatile.Read(ref stop))
				if (!File.Exists(destination.FullName)) alwaysPresent = false;
		});
		observer.Start();
		try
		{
			destination.cp(source);
		}
		finally
		{
			Volatile.Write(ref stop, true);
			observer.Join();
			watcher.EnableRaisingEvents = false;
		}

		Assert.True(alwaysPresent);
		Assert.Empty(events);
		Assert.Equal(source.ReadAllText(), destination.ReadAllText());
	}

	[Fact]
	public void RaiFileMove_OverExistingCloudFile_UpdatesInPlaceThenRemovesSource()
	{
		var cloudRoot = NewCloudRoot("file-move-replacement");
		var source = new TextFile(cloudRoot, "source", "txt", new string('n', 256_000));
		var destination = new TextFile(cloudRoot, "destination", "txt", "old content");
		var expected = source.ReadAllText();
		var events = new ConcurrentQueue<string>();

		using var watcher = new FileSystemWatcher(cloudRoot.FullPath, destination.NameWithExtension)
		{
			NotifyFilter = NotifyFilters.FileName,
			IncludeSubdirectories = false,
			EnableRaisingEvents = true
		};
		watcher.Deleted += (_, e) => events.Enqueue($"Deleted:{e.Name}");
		watcher.Renamed += (_, e) => events.Enqueue($"Renamed:{e.OldName}->{e.Name}");

		var stop = false;
		var alwaysPresent = true;
		var observer = new Thread(() =>
		{
			while (!Volatile.Read(ref stop))
				if (!File.Exists(destination.FullName)) alwaysPresent = false;
		});
		observer.Start();
		try
		{
			destination.mv(source, replace: true, keepBackup: false);
		}
		finally
		{
			Volatile.Write(ref stop, true);
			observer.Join();
			watcher.EnableRaisingEvents = false;
		}

		Assert.True(alwaysPresent);
		Assert.Empty(events);
		Assert.False(source.Exists());
		Assert.Equal(expected, destination.ReadAllText());
	}

	[Fact]
	public void RaiPathReplacement_ExistingCloudDirectory_FailsWithoutChangingChildren()
	{
		var cloudRoot = NewCloudRoot("directory-replacement");
		var source = (cloudRoot / "source").mkdir();
		var destination = (cloudRoot / "destination").mkdir();
		new TextFile(source, "source-child", "txt", "source");
		new TextFile(destination, "destination-child", "txt", "destination");

		var moveException = Assert.Throws<RaiCloudStorageException>(() =>
			destination.mv(source, replace: true, keepBackup: false));
		var copyException = Assert.Throws<RaiCloudStorageException>(() =>
			destination.cp(source, replace: true, keepBackup: false));

		Assert.Equal("RaiPath.mv", moveException.Operation);
		Assert.Equal("RaiPath.cp", copyException.Operation);
		Assert.True(new RaiFile(source, "source-child", "txt").Exists());
		Assert.True(new RaiFile(destination, "destination-child", "txt").Exists());
	}

	[Fact]
	public void RaiFileMove_SameCloudTreeToNewName_RemainsAvailableAsSemanticRename()
	{
		var cloudRoot = NewCloudRoot("semantic-rename");
		var source = new TextFile(cloudRoot, "old-item", "txt", "content");
		var destination = new RaiFile(cloudRoot, "new-item", "txt");

		destination.mv(source, replace: false);

		Assert.False(source.Exists());
		Assert.True(destination.Exists());
		Assert.Equal("content", new TextFile(destination.FullName).ReadAllText().Trim());
	}

	[Fact]
	public void RaiFileBackup_CloudSourceCopiesEvenWhenMoveWasRequested()
	{
		if (Os.LocalBackupDir is null)
			Assert.Skip("Os.LocalBackupDir is not configured; cloud backup behavior requires the configured backup boundary.");
		var cloudRoot = NewCloudRoot("backup");
		var source = new TextFile(cloudRoot, "live", "txt", "continuous");

		var backup = source.backup(copy: false);

		Assert.NotNull(backup);
		Assert.True(source.Exists());
		Assert.True(backup.Exists());
		Assert.Equal(source.ReadAllText(), new TextFile(backup.FullName).ReadAllText());
	}

	private RaiPath NewTempRoot(string label)
	{
		var root = (Os.TempDir / "RAIkeep" / "oslib-tests" / "cr022" / $"{label}-{Guid.NewGuid():N}").mkdir();
		localCleanup.Add(root);
		return root;
	}

	private RaiPath NewUncreatedCloudRoot(string label)
	{
		var root = ConfiguredCloud.RequireRoot("cr022", $"{label}-{Guid.NewGuid():N}");
		cloudCleanup.Add(root);
		return root;
	}

	private RaiPath NewCloudRoot(string label) => NewUncreatedCloudRoot(label).mkdir();
}
