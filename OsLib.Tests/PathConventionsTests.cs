using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
	private static string 	FileAt(RaiPath path, string nameWithExt) => new RaiFile(path, nameWithExt).FullName;

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
	public void CanonicalPath_Appends_FileStem_Folder()
	{
		var root = new RaiPath("/tmp/storage/").Path;
		var sut = new CanonicalPath(root, "AfricaStage");

		Assert.Equal(new RaiPath(root + "AfricaStage/").Path, sut.Path);
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
			Assert.Equal(concatenatedPath.Path, sut.Path);
			Assert.Equal(new RaiFile(concatenatedPath, "AfricaStage.pit").FullName, sut.FullName);
			Assert.True(new RaiFile(sut.FullName).Exists());	// why would the file exist? Was it saved?
		}
		finally
		{
			CleanupDir(root);
		}
	}

	[Fact]
	public void CanonicalFile_WithoutExtension_UsesDefaultExtension()
	{
		var root = NewTestRoot();
		EnsureDir(root);
		try
		{
			var input = new RaiFile(root, "NoExt").FullName;
			var sut = new CanonicalFile(input);
			var concatenatedPath = root / "NoExt";

			Assert.Equal("json", sut.Ext);
			Assert.Equal(new RaiFile(concatenatedPath, "NoExt.json").FullName, sut.FullName);
			Assert.True(new RaiFile(sut.FullName).Exists());
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

		Assert.Equal(PathConventionType.CanonicalByName, canonical.ConventionName);

		CleanupDir(root);
	}
}
