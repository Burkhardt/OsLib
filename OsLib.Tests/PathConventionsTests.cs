using OsLib;

namespace OsLib.Tests;

public class PathConventionsTests
{
    private static RaiPath NewTestRoot()
    {
        var guidSegment = Guid.NewGuid().ToString("N");
        return new RaiPath(Os.TempDir) / "oslib-tests" / guidSegment;
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

    private static string FileAt(RaiPath path, string nameWithExt)
    {
        return new RaiFile(path.Path + nameWithExt).FullName;
    }

    [Fact]
    public void ItemTreePath_BuildsPartitionedPath_FromItemId()
    {
        var root = new RaiPath("/tmp/storage/").Path;
        var sut = new ItemTreePath(root, "12345678");

        Assert.Equal(root, sut.RootPath);
        Assert.Equal("123", sut.Topdir);
        Assert.Equal("123456", sut.Subdir);
        Assert.Equal(new RaiPath(root + "123/123456/").Path, sut.Path);
    }

    [Fact]
    public void RaiPath_Mkdir_CreatesDirectory_ForPathCompositionStyle()
    {
        var root = NewTestRoot();
        var nested = root / "AfricaStage" / "configs";

        try
        {
            nested.mkdir();

            var probe = new TextFile(FileAt(nested, "probe.txt"));
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
    public void ItemTreePath_NormalizesRoot_WhenRootAlreadyContainsSegments()
    {
        var rootWithSegments = new RaiPath("/tmp/storage/123/123456/").Path;
        var sut = new ItemTreePath(rootWithSegments, "12345678");

        Assert.Equal(new RaiPath("/tmp/storage/").Path, sut.RootPath);
        Assert.Equal(new RaiPath("/tmp/storage/123/123456/").Path, sut.Path);
    }

    [Fact]
    public void ItemTreePath_Uses_C0N_ForConTopdir()
    {
        var root = new RaiPath("/tmp/storage/").Path;
        var sut = new ItemTreePath(root, "con12345");

        Assert.Equal("C0N", sut.Topdir);
        Assert.Equal("con123", sut.Subdir);
        Assert.Equal(new RaiPath(root + "C0N/con123/").Path, sut.Path);
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
        Assert.Contains(PathConventionType.ItemIdTree, Enum.GetValues<PathConventionType>());
    }

    [Fact]
    public void CanonicalFile_ConstructedFromFlatFile_CanonicalizesPathAndCreatesDestination()
    {
        var root = NewTestRoot();
        EnsureDir(root);
        try
        {
            var input = FileAt(root, "AfricaStage.pit");
            var sut = new CanonicalFile(input);
            var concatenatedPath = root / "AfricaStage";

            Assert.Equal("AfricaStage", sut.Name);
            Assert.Equal("pit", sut.Ext);
            Assert.Equal(concatenatedPath.Path, sut.Path);
            Assert.Equal(FileAt(concatenatedPath, "AfricaStage.pit"), sut.FullName);
            Assert.True(new RaiFile(sut.FullName).Exists());
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
            var input = FileAt(root, "NoExt");
            var sut = new CanonicalFile(input);
            var concatenatedPath = root / "NoExt";

            Assert.Equal("json", sut.Ext);
            Assert.Equal(FileAt(concatenatedPath, "NoExt.json"), sut.FullName);
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
        var canonicalFile = FileAt(canonicalDir, "Canon.txt");
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
    public void ImageTreeFile_ConstructedFromFlatPath_PartitionsByItemId()
    {
        var root = new RaiPath("/tmp/storage/").Path;
        var sut = new ImageTreeFile("123456_01.jpg", root, null, null);

        Assert.Equal("123456", sut.ItemId);
        Assert.Equal("123", sut.Topdir);
        Assert.Equal("123456", sut.Subdir);
        Assert.Equal(new RaiPath(root + "123/123456/").Path, sut.Path);
    }

    [Fact]
    public void ImageTreeFile_SetItemId_RebuildsTreePath()
    {
        var root = new RaiPath("/tmp/storage/").Path;
        var sut = new ImageTreeFile("123456_01.jpg", root, null, null);

        sut.ItemId = "654321";

        Assert.Equal("654", sut.Topdir);
        Assert.Equal("654321", sut.Subdir);
        Assert.Equal(new RaiPath(root + "654/654321/").Path, sut.Path);
    }

    [Fact]
    public void ImageTreeFile_SetPathWithEmbeddedSegments_NormalizesToRoot()
    {
        var sut = new ImageTreeFile("123456_01.jpg", "/tmp/storage/123/123456/", null, null);

        Assert.Equal(new RaiPath("/tmp/storage/123/123456/").Path, sut.Path);
        Assert.Equal(new RaiPath("/tmp/storage/").Path, sut.TopdirRoot);
    }

    [Fact]
    public void ImageTreeFile_ApplyPathConvention_UsesCurrentItemId()
    {
        var sut = new ImageTreeFile("abc999_01.jpg", "/tmp/base/", null, null);

        sut.ApplyPathConvention();

        Assert.Equal("abc", sut.Topdir);
        Assert.Equal("abc999", sut.Subdir);
        Assert.Equal(new RaiPath("/tmp/base/abc/abc999/").Path, sut.Path);
    }

    [Fact]
    public void ConventionName_ReportsExpectedEnumForImplementations()
    {
        var root = NewTestRoot();
        EnsureDir(root);
        var canonical = new CanonicalFile(FileAt(root, "File.pit"));
        var image = new ImageTreeFile("123456_01.jpg", "/tmp/storage/", null, null);

        Assert.Equal(PathConventionType.CanonicalByName, canonical.ConventionName);
        Assert.Equal(PathConventionType.ItemIdTree, image.ConventionName);

        CleanupDir(root);
    }
}
