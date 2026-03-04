using OsLib;

namespace OsLib.Tests;

public class TmpFileTests
{
    private static RaiPath NewTestRoot()
    {
        var guidSegment = Guid.NewGuid().ToString("N");
        return new RaiPath(Os.TempDir) / "oslib-tests" / "tmpfile" / guidSegment;
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
