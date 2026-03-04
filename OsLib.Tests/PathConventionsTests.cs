using OsLib;

namespace OsLib.Tests;

public class PathConventionsTests
{
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
        var root = Path.Combine(Path.GetTempPath(), "oslib-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "AfricaStage.pit");
            var sut = new CanonicalFile(input);

            Assert.Equal("AfricaStage", sut.Name);
            Assert.Equal("pit", sut.Ext);
            Assert.Equal(new RaiPath(Path.Combine(root, "AfricaStage")).Path, sut.Path);
            Assert.Equal(Path.Combine(root, "AfricaStage", "AfricaStage.pit"), sut.FullName);
            Assert.True(File.Exists(sut.FullName));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CanonicalFile_WithoutExtension_UsesDefaultExtension()
    {
        var root = Path.Combine(Path.GetTempPath(), "oslib-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "NoExt");
            var sut = new CanonicalFile(input);

            Assert.Equal("json", sut.Ext);
            Assert.Equal(Path.Combine(root, "NoExt", "NoExt.json"), sut.FullName);
            Assert.True(File.Exists(sut.FullName));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CanonicalFile_AlreadyCanonical_StaysCanonical()
    {
        var root = Path.Combine(Path.GetTempPath(), "oslib-tests", Guid.NewGuid().ToString("N"));
        var canonicalDir = Path.Combine(root, "Canon");
        Directory.CreateDirectory(canonicalDir);
        var canonicalFile = Path.Combine(canonicalDir, "Canon.txt");
        File.WriteAllText(canonicalFile, "x");

        try
        {
            var sut = new CanonicalFile(canonicalFile);

            Assert.Equal("Canon", sut.Name);
            Assert.Equal("txt", sut.Ext);
            Assert.Equal(canonicalFile, sut.FullName);
            Assert.True(File.Exists(sut.FullName));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
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
        var canonical = new CanonicalFile(Path.Combine(Path.GetTempPath(), "oslib-tests", Guid.NewGuid().ToString("N"), "File.pit"));
        var image = new ImageTreeFile("123456_01.jpg", "/tmp/storage/", null, null);

        Assert.Equal(PathConventionType.CanonicalByName, canonical.ConventionName);
        Assert.Equal(PathConventionType.ItemIdTree, image.ConventionName);

        var root = new RaiFile(canonical.Path).Path;
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }
}
