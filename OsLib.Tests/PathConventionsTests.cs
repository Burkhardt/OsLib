using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using OsLib;

namespace OsLib.Tests;

public class PathConventionsTests
{
	private static RaiPath NewTestRoot([CallerMemberName] string testName = "")
	{
		var root = Os.TempDir / "RAIkeep" / "oslib-tests" / "path-conventions" / SanitizeSegment(testName);
		CleanupDir(root);
		return root;
	}

	private static void EnsureDir(RaiPath path)
	{
		RaiFile.mkdir(path.Path);
	}

	private static void CleanupDir(RaiPath path)
	{
		var root = new RaiFile(path.Path);
		try
		{
			root.rmdir(depth: 8, deleteFiles: true);
		}
		catch
		{
		}
	}

	[Obsolete("hallucinated? use new RaiFile(path, nameWithExt).FullName")]
	private static string FileAt(RaiPath path, string nameWithExt) => new RaiFile(path, nameWithExt).FullName;

	private static string SanitizeSegment(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "test";

		var invalid = Path.GetInvalidFileNameChars();
		var cleaned = new string(value
			.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch)
			.ToArray())
			.Trim('-');

		return string.IsNullOrWhiteSpace(cleaned) ? "test" : cleaned;
	}

	private static string RequireConfiguredCloudRoot()
	{
		var config = Os.Config as JObject;
		var cloud = config?["Cloud"] as JObject;
		foreach (var provider in new[] { "GoogleDrive", "OneDrive", "Dropbox" })
		{
			var root = cloud?[provider]?.ToString();
			if (!string.IsNullOrWhiteSpace(root))
				return root;
		}

		Assert.Skip($"No cloud provider configured in {Os.ConfigFileFullName}.");
		return string.Empty;
	}

	[Fact]
	public void CloudPathWiring_Initialize_IsCompatibilityNoOp()
	{
		var pathBefore = new RaiPath(Path.GetTempPath()) / "RAIkeep" / "local-probe";

		CloudPathWiring.Initialize();

		var pathAfter = new RaiPath(pathBefore.Path);
		Assert.Equal(pathBefore.Cloud, pathAfter.Cloud);
	}

	[Fact]
	public void OsConfig_IsLoaded_AndAvailableAsReadOnlyRuntimeSnapshot()
	{
		Assert.True(Os.IsConfigLoaded, $"CRITICAL: Config file '{Os.ConfigFileFullName}' is missing or malformed.");
		Assert.NotNull(Os.Config);
		Assert.NotNull(Os.TempDir);
	}

	[Fact]
	public void RaiPath_ProperWayToSplitFullName()
	{
		var fullName = "/Users/RSB/Projects/PitSeeder/pits/sample/Person.json5";
		// Proper Way: Use the static splitter to distinguish between the directory and the file.
		(RaiPath p, string name) = RaiPath.SplitRaiPathAndName(fullName);
		Assert.Equal("/Users/RSB/Projects/PitSeeder/pits/sample/", p.Path);
		Assert.Equal("Person.json5", name);
		// Proper Way #2: Let the RaiFile object own the discovery logic.
		var file = new RaiFile(fullName);
		// The .Path property is already a strongly-typed RaiPath.
		Assert.Equal("/Users/RSB/Projects/PitSeeder/pits/sample/", file.Path.Path);
		Assert.Equal("Person.json5", file.NameWithExtension);
	}
	[Fact]
	public void RaiPath_CreateFromFullName()
	{
		var p = new RaiPath("/Users/RSB/Projects/PitSeeder/pits/sample/Person.json5");
		Assert.Equal("/Users/RSB/Projects/PitSeeder/pits/sample/Person.json5/", p.ToString());
		Assert.Equal("/Users/RSB/Projects/PitSeeder/pits/sample/Person.json5/", p.Path);
	}
	[Fact]
	public void RaiPath_CloudStatus_PersistsThroughNavigation()
	{
		var root = new RaiPath(RequireConfiguredCloudRoot());
		var child = root / "workspace";

		Assert.True(root.Cloud);
		Assert.True(child.Cloud);
	}

	[Fact]
	public void RaiPath_StringAssignment_ReevaluatesCloudState_FromRuntimeSnapshot()
	{
		var path = new RaiPath(Path.GetTempPath()) / "RAIkeep" / "local-probe";
		var cloudPath = new RaiPath(RequireConfiguredCloudRoot()) / "workspace";

		Assert.False(path.Cloud);

		path.Path = cloudPath.Path;

		Assert.True(path.Cloud);
	}

	[Fact]
	public void RaiPath_StringAssignment_ReevaluatesCloudState()
	{
		var path = new RaiPath(Path.GetTempPath()) / "RAIkeep" / "local-workspace";
		var changedPath = (new RaiPath(RequireConfiguredCloudRoot()) / "workspace").FullPath;

		path.Path = changedPath;

		Assert.True(path.Cloud);
	}

	[Fact]
	public void RaiPath_CopyConstruction_UsesBufferedCloudState()
	{
		var source = new RaiPath(RequireConfiguredCloudRoot());

		var copy = new RaiPath(source);

		Assert.True(source.Cloud);
		Assert.True(copy.Cloud);
	}
	[Fact]
	public void RaiPath_Mkdir_CreatesDirectory_ForPathCompositionStyle()
	{
		var root = NewTestRoot();
		var nested = root / "AfricaStage" / "configs";

		try
		{
			nested.mkdir();

			var probe = new TextFile(nested, "probe.txt");
			probe.Append("ok");
			probe.Save();

			Assert.True(new RaiFile(probe.FullName).Exists());
		}
		finally
		{
			CleanupDir(root);
		}
	}

	[Fact]
	public void RaiPath_Rmdir_RemovesDirectoryTree()
	{
		var root = NewTestRoot();
		var nested = root / "cleanup" / "deep";

		try
		{
			nested.mkdir();
			var probe = new TextFile(nested, "probe.txt");
			probe.Append("ok");
			probe.Save();

			root.rmdir(depth: 8, deleteFiles: true);

			Assert.False(Directory.Exists(root.Path));
		}
		finally
		{
			if (Directory.Exists(root.Path))
			{
				CleanupDir(root);
			}
		}
	}

	[Fact]
	public void RaiPath_Mv_MovesDirectory_WhenTargetDoesNotExist()
	{
		var root = NewTestRoot();
		var source = root / "source";
		var target = root / "target";

		try
		{
			source.mkdir();
			var probe = new TextFile(source, "probe.txt");
			probe.Append("payload");
			probe.Save();

			var result = target.mv(source, replace: false, keepBackup: false);

			Assert.Equal(0, result);
			Assert.False(Directory.Exists(source.Path));
			Assert.True(Directory.Exists(target.Path));
			Assert.True(new RaiFile(target.Path + "probe.txt").Exists());
		}
		finally
		{
			CleanupDir(root);
		}
	}

	[Fact]
	public void RaiPath_Mv_Throws_WhenTargetExists_AndReplaceFalse()
	{
		var root = NewTestRoot();
		var source = root / "source";
		var target = root / "target";

		try
		{
			source.mkdir();
			target.mkdir();

			Assert.Throws<IOException>(() => target.mv(source, replace: false, keepBackup: false));
			Assert.True(Directory.Exists(source.Path));
			Assert.True(Directory.Exists(target.Path));
		}
		finally
		{
			CleanupDir(root);
		}
	}

	[Fact]
	public void RaiPath_Mv_ReplacesTarget_WithoutBackup_WhenReplaceTrue()
	{
		var root = NewTestRoot();
		var source = root / "source";
		var target = root / "target";

		try
		{
			source.mkdir();
			var sourceProbe = new TextFile(source, "src.txt");
			sourceProbe.Append("from-source");
			sourceProbe.Save();

			target.mkdir();
			var targetProbe = new TextFile(target, "tgt.txt");
			targetProbe.Append("from-target");
			targetProbe.Save();

			var result = target.mv(source, replace: true, keepBackup: false);

			Assert.Equal(0, result);
			Assert.False(Directory.Exists(source.Path));
			Assert.True(Directory.Exists(target.Path));
			Assert.True(new RaiFile(target.Path + "src.txt").Exists());
			Assert.False(new RaiFile(target.Path + "tgt.txt").Exists());
		}
		finally
		{
			CleanupDir(root);
		}
	}

	[Fact]
	public void RaiPath_Mv_ReplacesTarget_AndKeepsBackup_WhenKeepBackupTrue()
	{
		var root = NewTestRoot();
		var source = root / "source";
		var target = root / "target";

		var backupRoot = Os.LocalBackupDir;
		Assert.NotNull(backupRoot); // requires LocalBackupDir to be configured

		var backupMirrorParent = backupRoot! / RaiFile.BackupRelativePath(root);
		// Clear any stale backups from previous runs of this test
		if (backupMirrorParent.Exists())
			backupMirrorParent.rmdir(depth: int.MaxValue, deleteFiles: true);
		RaiPath createdBackup = null!;

		try
		{
			source.mkdir();
			var sourceProbe = new TextFile(source, "src.txt");
			sourceProbe.Append("from-source");
			sourceProbe.Save();

			target.mkdir();
			var targetProbe = new TextFile(target, "tgt.txt");
			targetProbe.Append("from-target");
			targetProbe.Save();

			var result = target.mv(source, replace: true, keepBackup: true);

			Assert.Equal(0, result);
			Assert.False(Directory.Exists(source.Path));
			Assert.True(Directory.Exists(target.Path));
			Assert.True(new RaiFile(target.Path + "src.txt").Exists());

			Assert.True(backupMirrorParent.Exists(),
				$"Expected backup mirror parent to exist: {backupMirrorParent.Path}");
			var backups = Directory.EnumerateDirectories(backupMirrorParent.Path, "target_*").ToList();
			Assert.Single(backups);
			createdBackup = new RaiPath(backups[0]);
			Assert.True(new RaiFile(createdBackup.Path + "tgt.txt").Exists());
		}
		finally
		{
			if (createdBackup != null) createdBackup.rmdir(depth: int.MaxValue, deleteFiles: true);
			if (backupMirrorParent.Exists()) backupMirrorParent.rmdir(depth: int.MaxValue, deleteFiles: true);
			CleanupDir(root);
		}
	}

	[Fact]
	public void RaiPath_Mv_Throws_WhenSourceMissing()
	{
		var root = NewTestRoot();
		var source = root / "missing-source";
		var target = root / "target";

		try
		{
			root.mkdir();
			Assert.Throws<DirectoryNotFoundException>(() => target.mv(source, replace: false, keepBackup: false));
		}
		finally
		{
			CleanupDir(root);
		}
	}

	[Fact]
	public void RaiPath_Mv_Throws_WhenSourceIsNull()
	{
		var root = NewTestRoot();
		var target = root / "target";

		try
		{
			Assert.Throws<ArgumentNullException>(() => target.mv(null!, replace: false, keepBackup: false));
		}
		finally
		{
			CleanupDir(root);
		}
	}

	[Fact]
	public void CanonicalFile_Appends_Folder()
	{
		var root = new RaiPath("/tmp/storage/");
		var p = root / "otw.software";  // this is a path
		var can1 = new CanonicalFile(p, "AfricaStage.json");
		var can2 = new CanonicalFile(p, "AfricaStage", "json");
		var can3 = new CanonicalFile(p, "Nomsa.net", "json");
		var can4 = new CanonicalFile(p / "Nomsa.net", "Nomsa.net.json");
		var can5 = new CanonicalFile(p / "Nomsa.net", "Nomsa.net", "json");
		var can6 = new CanonicalFile(p / "Nomsa.net", "Nomsa.net"); // the second "Nomsa.net" will be interpreted as Name.Ext and split into Name="Nomsa" and Ext="net"
		var can7 = new CanonicalFile("/tmp/storage/otw.software/Nomsa.net/Nomsa.net.json");

		Assert.Equal("/tmp/storage/otw.software/AfricaStage/AfricaStage.json", can1.FullName);
		Assert.Equal("json", can2.Ext);
		Assert.Equal("/tmp/storage/otw.software/AfricaStage/AfricaStage.json", can2.FullName);
		Assert.Equal("/tmp/storage/otw.software/Nomsa.net/Nomsa.net.json", can3.FullName);
		Assert.Equal("/tmp/storage/otw.software/Nomsa.net/Nomsa.net.json", can4.FullName);
		Assert.Equal("/tmp/storage/otw.software/Nomsa.net/Nomsa.net.json", can5.FullName);
		Assert.Equal("json", can5.Ext);
		Assert.Equal("/tmp/storage/otw.software/Nomsa.net/Nomsa/Nomsa.net", can6.FullName);
		Assert.Equal("net", can6.Ext);
		Assert.Equal("/tmp/storage/otw.software/Nomsa.net/Nomsa.net.json", can7.FullName);
		Assert.Equal("json", can7.Ext);
	}

	[Fact]
	public void ApplyPathConvention_CanonicalFile()
	{
		string s = "/Users/RSB/Library/CloudStorage/OneDrive/OneDriveData/pits/output/Person.pit";
		string expected = "/Users/RSB/Library/CloudStorage/OneDrive/OneDriveData/pits/output/Person/Person.pit";
		var can = new CanonicalFile(s);
		Assert.Equal(expected, can.FullName);
	}

	[Fact]
	public void PathConventionType_Contains_ExpectedMembers()
	{
		Assert.Contains(PathConventionType.CanonicalByName, Enum.GetValues<PathConventionType>());
		Assert.Contains(PathConventionType.ItemIdTree3x3, Enum.GetValues<PathConventionType>());
		Assert.Contains(PathConventionType.ItemIdTree8x2, Enum.GetValues<PathConventionType>());
	}

	[Fact]
	public void CanonicalFile_ConstructedFromFlatFile_CanonicalizesPathAndCreatesDestination()
	{
		var root = NewTestRoot();
		EnsureDir(root);
		try
		{
			//var input = new RaiFile(root, "AfricaStage.pit");
			var sut = new CanonicalFile(root, "AfricaStage.pit");
			var concatenatedPath = root / "AfricaStage";

			Assert.Equal("AfricaStage", sut.Name);
			Assert.Equal("pit", sut.Ext);
			Assert.Equal(concatenatedPath.ToString(), sut.Path.ToString());
			Assert.Equal(new RaiFile(concatenatedPath, "AfricaStage.pit").FullName, sut.FullName);
			//Assert.True(new RaiFile(sut.FullName).Exists());	// BS: creating a CanonicalFile object does not create the file on disk, calling .Save() does
		}
		finally
		{
			CleanupDir(root);
		}
	}

	[Fact]
	public void CanonicalFile_WithoutExtension()
	{
		var root = NewTestRoot();
		EnsureDir(root);
		try
		{
			var input = new RaiFile(root, "NoExt").FullName;
			var sut = new CanonicalFile(input);
			var concatenatedPath = root / "NoExt";

			Assert.True(string.IsNullOrEmpty(sut.Ext));
			Assert.EndsWith($"{Os.DIR}NoExt{Os.DIR}NoExt", sut.FullName);
			// Assert.True(new RaiFile(sut.FullName).Exists()); // BS: creating a CanonicalFile object does not create the file on disk, calling .Save() does
		}
		finally
		{
			CleanupDir(root);
		}
	}

	[Fact]
	public void CanonicalFile_AlreadyCanonical_StaysCanonical()
	{
		var root = NewTestRoot();
		var canonicalDir = root / "Canon";
		EnsureDir(canonicalDir);
		var canonicalFile = new RaiFile(canonicalDir, "Canon.txt").FullName;
		var seedFile = new TextFile(canonicalFile);
		seedFile.Append("x");
		seedFile.Save();

		try
		{
			var sut = new CanonicalFile(canonicalFile);

			Assert.Equal("Canon", sut.Name);
			Assert.Equal("txt", sut.Ext);
			Assert.Equal(canonicalFile, sut.FullName);
			Assert.True(new RaiFile(sut.FullName).Exists());
		}
		finally
		{
			CleanupDir(root);
		}
	}

	[Fact]
	public void ConventionName_ReportsExpectedEnumForCanonicalFile()
	{
		var root = NewTestRoot();
		EnsureDir(root);
		var canonical = new CanonicalFile(new RaiFile(root, "File.pit").FullName);

		Assert.Equal(PathConventionType.CanonicalByName, canonical.Convention);

		CleanupDir(root);
	}

	[Fact]
	public void CanonicalFile_StringConstructor_WithNomsaNetDirectoryPath_PreservesDirectoryPath()
	{
		var fullName = "/Users/RSB/Library/CloudStorage/OneDrive/OneDriveData/Nomsa.net/";

		var sut = new CanonicalFile(fullName);

		Assert.Equal(fullName, sut.Path.ToString());
		Assert.Equal(string.Empty, sut.Name);
		Assert.Equal(string.Empty, sut.Ext);
		Assert.Equal(fullName, sut.FullName);
	}

	[Fact]
	public void CanonicalFile_RaiPathConstructor_WithNomsaNetDirectoryPathAndName_KeepsCanonicalByStem()
	{
		var path = new RaiPath("/Users/RSB/Library/CloudStorage/OneDrive/OneDriveData/Nomsa.net/");

		var sut = new CanonicalFile(path, "Nomsa.net");

		Assert.Equal("Nomsa", sut.Name);
		Assert.Equal("net", sut.Ext);
		Assert.Equal("/Users/RSB/Library/CloudStorage/OneDrive/OneDriveData/Nomsa.net/Nomsa/", sut.Path.ToString());
		Assert.Equal("/Users/RSB/Library/CloudStorage/OneDrive/OneDriveData/Nomsa.net/Nomsa/Nomsa.net", sut.FullName);
	}
}
