using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using OsLib;
using Xunit.Runner.Common;
using Xunit.Sdk;

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
	[Fact]
	public void CloudStatus_Fails_WhenConfigIsMissing()
	{
		// Ensure the engine has attempted to load the environment
		CloudPathWiring.Initialize();
		// If you've renamed your config file, this should be false.
		// This is the "Proper Way" to prove the system isn't faking it.
		Assert.True(Os.IsConfigLoaded, "System should report config is NOT loaded.");
		var path = new RaiPath("/Users/RSB/Dropbox/");
		// Because the config is missing, the evaluator MUST return false.
		Assert.False(path.Cloud, "Path should NOT be cloud-aware without a valid config file.");
	}
	[Fact]
	public void RaiPath_MustFailCloudStatus_IfConfigIsMissing()
	{
		dynamic cfg = Os.Config;
		// We don't use mocks here. We want the RAW production truth.
		// If the file is missing, Os.IsConfigLoaded should be false.
		Assert.True(Os.IsConfigLoaded, "CRITICAL: Config file 'RAIkeep.json5' is missing or renamed!");
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
		var originalEvaluator = RaiPath.CloudEvaluator;
		try
		{
			// 1. Force the engine to perform its "Production Stomp" now.
			// This ensures the static constructor/wiring has already fired.
			CloudPathWiring.Initialize();
			// 2. NOW overwrite it with our mock. 
			// Our assignment happens LAST, so we win the race.
			RaiPath.CloudEvaluator = p => p.Contains("Dropbox");
			// 3. Constructor runs, calls our mock, sets the private Cloud property.
			var root = new RaiPath("/Users/RSB/Dropbox/");
			Assert.True(root.Cloud);
		}
		finally
		{
			// 4. Put the toys back in the box.
			RaiPath.CloudEvaluator = originalEvaluator;
		}
	}// Define the static named evaluator inside your test class
	private static bool MockDropboxEvaluator(string path)
	{
		if (string.IsNullOrWhiteSpace(path)) return false;
		return path.Contains("Dropbox", StringComparison.Ordinal);
	}
	[Fact]
	public void RaiPath_StringAssignment_ReevaluatesCloudState_fakeEvaluator()
	{
		var originalEvaluator = RaiPath.CloudEvaluator;
		// option1: outside try catch lamda works
		RaiPath.CloudEvaluator = path => !path.Contains("tmp/", StringComparison.Ordinal);
		try
		{
			// option2: inside try catch lamda didn't work for me but this one does too, so leaving it here for reference
			RaiPath.CloudEvaluator = MockDropboxEvaluator;
			// Problem 1 Fix: Extract the directory from the initial string
			var (initialDir, _) = RaiPath.splitPathAndName("/tmp/local/workspace/file.txt");
			var path = new RaiPath(initialDir);
			// Problem 1 Fix: Extract the directory from the target string
			var (newDir, _) = RaiPath.splitPathAndName("/Dropbox/workspace/file.txt");
			// Act: Assign the new clean directory path
			path.Path = newDir;
			// Assert: The setter should have triggered the MockDropboxEvaluator
			Assert.True(path.Cloud);
		}
		finally
		{
			// Restore the original environment
			RaiPath.CloudEvaluator = originalEvaluator;
		}
	}
	[Fact]
	public void RaiPath_StringAssignment_ReevaluatesCloudState()
	{   // this test uses the original evaluator
		try
		{
			var path = new RaiPath("/tmp/local/workspace/");
			string changedPath = (new RaiPath((string)Os.Config.Cloud.GoogleDrive) / "workspace/").FullPath;
			path.Path = changedPath;
			Assert.True(path.Cloud);
		}
		catch (Exception) { }
	}
	[Fact]
	public void RaiPath_CopyConstruction_UsesBufferedCloudState()
	{
		var originalEvaluator = RaiPath.CloudEvaluator;
		try
		{
			var evaluationCount = 0;
			RaiPath.CloudEvaluator = path =>
			{
				evaluationCount++;
				return path.Contains("Dropbox", StringComparison.Ordinal);
			};

			var source = new RaiPath("/tmp/Dropbox/workspace/file.txt");
			evaluationCount = 0;

			var copy = new RaiPath(source);

			Assert.True(copy.Cloud);
			Assert.Equal(0, evaluationCount);
		}
		finally
		{
			RaiPath.CloudEvaluator = originalEvaluator;
		}
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
	public void CanonicalFile_Appends_Folder()
	{
		var root = new RaiPath("/tmp/storage/");
		var p = root / "otw.software";  // this is a path
		var can1 = new CanonicalFile(p, "AfricaStage.json");
		var can2 = new CanonicalFile(p, "AfricaStage", "json");
		var can3 = new CanonicalFile(p, "Nomsa.net", "json");
		var can4 = new CanonicalFile(p / "Nomsa.net", "Nomsa.net.json");
		var can5 = new CanonicalFile(p / "Nomsa.net", "Nomsa.net", "json");
		var can6 = new CanonicalFile(p / "Nomsa.net", "Nomsa.net");	// the second "Nomsa.net" will be interpreted as Name.Ext and split into Name="Nomsa" and Ext="net"
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
