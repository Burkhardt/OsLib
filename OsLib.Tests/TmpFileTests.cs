using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using OsLib;

namespace OsLib.Tests;

public class TmpFileTests
{
    private static RaiPath NewTestRoot([CallerMemberName] string testName = "")
    {
        var root = new RaiPath(Os.TempDir) / "RAIkeep" / "oslib-tests" / "tmpfile" / SanitizeSegment(testName);
        CleanupDir(root);
        return root;
    }

    private static void CleanupDir(RaiPath path)
    {
        try
        {
            new RaiFile(path.Path).rmdir(depth: 10, deleteFiles: true);
        }
        catch
        {
        }
    }

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
    public void TmpFile_Create_CreatesFile_WhenParentDirectoryExists()
    {
        var root = NewTestRoot();
        var dir = root / "existing";
        dir.mkdir();

        try
        {
            var sut = new TmpFile(dir.Path + "probe.tmp");

            sut.create();

            Assert.True(sut.Exists());
            Assert.True(new RaiFile(sut.FullName).Exists());
        }
        finally
        {
            CleanupDir(root);
        }
    }

    [Fact]
    public void TmpFile_Create_CreatesMissingParentDirectoryTree()
    {
        var root = NewTestRoot();
        var deep = root / "no" / "such" / "tree";

        try
        {
            var sut = new TmpFile(deep.Path + "created-by-tmpfile.tmp");

            sut.create();

            Assert.True(sut.Exists());
            Assert.Equal(deep.Path, sut.Path);
            Assert.False(new RaiFile(deep.Path).dirEmpty);
        }
        finally
        {
            CleanupDir(root);
        }
    }
}
