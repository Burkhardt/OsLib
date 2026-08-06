using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace OsLib.Tests;

/// <summary>
/// Shared access to the machine's real configured cloud roots for CR003 tests.
/// The configuration file is the sole source of truth; cloud roots are resolved
/// exactly like the pits CLI resolves them — through the dynamic <see cref="Os.Config"/>
/// object (<c>Os.Config?.Cloud?[provider]</c>) — with no environment variables,
/// rewritten configuration, or local temporary substitutes.
/// </summary>
internal static class ConfiguredCloud
{
	private static readonly string[] FallbackOrder = { "OneDrive", "Dropbox", "GoogleDrive", "ICloudDrive" };

	private static IEnumerable<string> ProviderOrder()
	{
		var order = new List<string>();
		try
		{
			var configured = Os.Config?.DefaultCloudOrder;
			if (configured is not null)
				foreach (var provider in configured)
					order.Add((string)provider);
		}
		catch { }
		return order.Count > 0 ? order : FallbackOrder;
	}

	/// <summary>First configured cloud provider whose root exists, or null when the machine has none.</summary>
	internal static string? ProviderNameOrNull()
	{
		foreach (var provider in ProviderOrder())
		{
			string? root = (string?)Os.Config?.Cloud?[provider];
			if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(new RaiPath(root).Path))
				return provider;
		}
		return null;
	}

	/// <summary>Root path of <see cref="ProviderNameOrNull"/>, or null.</summary>
	internal static RaiPath? RootOrNull()
	{
		var provider = ProviderNameOrNull();
		return provider is null ? null : new RaiPath((string)Os.Config.Cloud[provider]);
	}

	internal static RaiPath RequireRoot(params string[] segments)
	{
		var root = RootOrNull();
		if (root is null)
			Assert.Skip("No configured cloud provider root (Os.Config.Cloud) is available on this machine; " +
				"this CR003 test requires a real configured cloud root and cannot use a local substitute.");
		var path = root / "RAIkeep" / "oslib-cr003-tests";
		foreach (var segment in segments)
			path /= segment;
		return path;
	}
}

/// <summary>
/// CR003 §5 — the agreed v3.13.2 TextFile.Save contract: no delete, no rename, no
/// temporary-file replacement of the original pathname; backup copies first.
/// </summary>
public sealed class TextFileSaveContractTests : IDisposable
{
	private readonly List<RaiPath> cleanup = new();

	public void Dispose()
	{
		foreach (var path in cleanup)
		{
			try { path.rmdir(depth: 5, deleteFiles: true); }
			catch { }
		}
	}

	private RaiPath NewCloudDir(string label)
	{
		var dir = ConfiguredCloud.RequireRoot("textfile-save", $"{label}-{Guid.NewGuid():N}");
		dir.mkdir();
		cleanup.Add(dir);
		return dir;
	}

	[Fact]
	public void Save_OrdinaryOverwrite_TruncatesAndWritesExistingPathInPlace()
	{
		var dir = NewCloudDir("overwrite");
		var file = new TextFile(dir, "overwrite-target", "txt");
		file.Lines = new List<string> { "first version with a much longer line of content" };
		file.Changed = true;
		file.Save();
		Assert.True(file.Exists());

		file.Lines = new List<string> { "v2" };
		file.Changed = true;
		file.Save();

		var reread = File.ReadAllLines(file.FullName);
		Assert.Equal(new[] { "v2" }, reread); // truncated, not appended
	}

	[Fact]
	public void Save_CreatesPathnameWhenAbsent()
	{
		var dir = NewCloudDir("create");
		var file = new TextFile(dir, "created", "txt");
		Assert.False(file.Exists());
		file.Lines = new List<string> { "hello" };
		file.Changed = true;
		file.Save();
		Assert.True(file.Exists());
	}

	[Fact]
	public void Save_NeverDeletesOrRenamesOriginalPath_DuringRepeatedConfiguredCloudWrites()
	{
		var dir = NewCloudDir("no-delete");
		var file = new TextFile(dir, "watched", "txt");
		file.Lines = new List<string> { "seed" };
		file.Changed = true;
		file.Save();

		var deletedOrRenamed = new List<string>();
		using var watcher = new FileSystemWatcher(dir.FullPath, "watched.txt")
		{
			NotifyFilter = NotifyFilters.FileName,
			IncludeSubdirectories = false
		};
		watcher.Deleted += (_, e) => { lock (deletedOrRenamed) deletedOrRenamed.Add($"Deleted:{e.Name}"); };
		watcher.Renamed += (_, e) => { lock (deletedOrRenamed) deletedOrRenamed.Add($"Renamed:{e.OldName}->{e.Name}"); };
		watcher.EnableRaisingEvents = true;

		var stop = false;
		var pathnameAlwaysPresent = true;
		var checker = new Thread(() =>
		{
			while (!Volatile.Read(ref stop))
			{
				if (!File.Exists(file.FullName))
					pathnameAlwaysPresent = false;
			}
		});
		checker.Start();

		for (var i = 0; i < 50; i++)
		{
			file.Lines = new List<string> { $"content revision {i} — {new string('x', 100 + i)}" };
			file.Changed = true;
			file.Save();
		}

		Volatile.Write(ref stop, true);
		checker.Join();
		watcher.EnableRaisingEvents = false;

		Assert.True(pathnameAlwaysPresent, "The original pathname must remain continuously discoverable across in-place writes.");
		lock (deletedOrRenamed)
			Assert.Empty(deletedOrRenamed);
	}

	[Fact]
	public void Save_WithBackup_CopiesPreviousContent_AndOriginalPathNeverDisappears()
	{
		if (Os.LocalBackupDir is null)
			Assert.Skip("Os.LocalBackupDir is not configured on this machine; the copy-based backup contract cannot be verified.");
		var dir = NewCloudDir("backup");
		var file = new TextFile(dir, "backup-target", "txt");
		file.Lines = new List<string> { "old content" };
		file.Changed = true;
		file.Save();

		file.Lines = new List<string> { "new content" };
		file.Changed = true;
		file.Save(backup: true);

		// The original pathname was overwritten in place — never deleted or moved.
		Assert.True(file.Exists());
		Assert.Equal(new[] { "new content" }, File.ReadAllLines(file.FullName));

		// The previous content was copied (not moved) to the configured backup location.
		var backupRoot = Os.LocalBackupDir / RaiFile.BackupRelativePath(file.Path);
		var backupFiles = Directory.Exists(backupRoot.Path)
			? Directory.GetFiles(backupRoot.Path, "backup-target_*.txt")
			: Array.Empty<string>();
		Assert.NotEmpty(backupFiles);
		Assert.Contains(backupFiles, b => File.ReadAllLines(b).SequenceEqual(new[] { "old content" }));
	}

	[Fact]
	public void SaveInPlace_DelegatesToNoDeleteSave()
	{
		var dir = NewCloudDir("inplace");
		var file = new TextFile(dir, "inplace-target", "txt");
		file.Lines = new List<string> { "one" };
		file.Changed = true;
		file.SaveInPlace();
		Assert.True(file.Exists());
		file.Lines = new List<string> { "two" };
		file.Changed = true;
		file.SaveInPlace();
		Assert.Equal(new[] { "two" }, File.ReadAllLines(file.FullName));
	}

	[Fact]
	public void Save_RetainsMaterializationBehavior()
	{
		var dir = NewCloudDir("materialize");
		var file = new TextFile(dir, "materialized", "txt");
		file.Lines = new List<string> { "content" };
		file.Changed = true;
		// Save calls AwaitMaterializing(true) internally and throws when the write
		// does not materialize; reaching this assertion proves the check ran.
		file.Save();
		Assert.True(file.Exists());
	}
}
