using OsLib;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class CloudStorageDiscoveryTests
{
    [Fact]
    public void GetCloudStorageRoots_UsesEnvironmentOverrides()
    {
        var root = NewTestRoot();
        Directory.CreateDirectory(root.Path);

        var dropbox = (root / "DropboxRoot").Path;
        var oneDrive = (root / "OneDriveRoot").Path;
        var googleDrive = (root / "GoogleDriveRoot").Path;
        var iCloud = (root / "ICloudRoot").Path;

        Directory.CreateDirectory(dropbox);
        Directory.CreateDirectory(oneDrive);
        Directory.CreateDirectory(googleDrive);
        Directory.CreateDirectory(iCloud);

        var env = new EnvScope(new Dictionary<string, string?>
        {
            ["OSLIB_CLOUD_ROOT_DROPBOX"] = dropbox,
            ["OSLIB_CLOUD_ROOT_ONEDRIVE"] = oneDrive,
            ["OSLIB_CLOUD_ROOT_GOOGLEDRIVE"] = googleDrive,
            ["OSLIB_CLOUD_ROOT_ICLOUD"] = iCloud,
            ["OSLIB_CLOUD_CONFIG"] = null
        });

        try
        {
            Os.ResetCloudStorageCache();
            var roots = Os.GetCloudStorageRoots(refresh: true);

            Assert.Equal(new RaiPath(dropbox).Path, roots[CloudStorageType.Dropbox]);
            Assert.Equal(new RaiPath(oneDrive).Path, roots[CloudStorageType.OneDrive]);
            Assert.Equal(new RaiPath(googleDrive).Path, roots[CloudStorageType.GoogleDrive]);
            Assert.Equal(new RaiPath(iCloud).Path, roots[CloudStorageType.ICloud]);
        }
        finally
        {
            env.Dispose();
            Cleanup(root);
        }
    }

    [Fact]
    public void GetPreferredCloudStorageRoot_RespectsPreferredOrder()
    {
        var root = NewTestRoot();
        Directory.CreateDirectory(root.Path);

        var dropbox = (root / "DropboxRoot").Path;
        var googleDrive = (root / "GoogleDriveRoot").Path;
        Directory.CreateDirectory(dropbox);
        Directory.CreateDirectory(googleDrive);

        var env = new EnvScope(new Dictionary<string, string?>
        {
            ["OSLIB_CLOUD_ROOT_DROPBOX"] = dropbox,
            ["OSLIB_CLOUD_ROOT_GOOGLEDRIVE"] = googleDrive,
            ["OSLIB_CLOUD_ROOT_ONEDRIVE"] = null,
            ["OSLIB_CLOUD_ROOT_ICLOUD"] = null,
            ["OSLIB_CLOUD_CONFIG"] = null
        });

        try
        {
            Os.ResetCloudStorageCache();

            var preferredDropbox = Os.GetPreferredCloudStorageRoot(CloudStorageType.Dropbox, CloudStorageType.GoogleDrive);
            var preferredGoogle = Os.GetPreferredCloudStorageRoot(CloudStorageType.GoogleDrive, CloudStorageType.Dropbox);

            Assert.Equal(new RaiPath(dropbox).Path, preferredDropbox);
            Assert.Equal(new RaiPath(googleDrive).Path, preferredGoogle);
        }
        finally
        {
            env.Dispose();
            Cleanup(root);
        }
    }

    [Fact]
    public void GetCloudStorageRoots_LoadsIniConfiguration()
    {
        var root = NewTestRoot();
        Directory.CreateDirectory(root.Path);

        var googleDrive = (root / "GoogleDriveIni").Path;
        var iCloud = (root / "ICloudIni").Path;
        Directory.CreateDirectory(googleDrive);
        Directory.CreateDirectory(iCloud);

        var iniFile = new RaiFile((root / "cfg").Path + "cloudstorage.ini");
        RaiFile.mkdir(iniFile.Path);
        File.WriteAllLines(iniFile.FullName,
        [
            "# oslib cloud configuration",
            $"googledrive={googleDrive}",
            $"icloud={iCloud}"
        ]);

        var env = new EnvScope(new Dictionary<string, string?>
        {
            ["OSLIB_CLOUD_CONFIG"] = iniFile.FullName,
            ["OSLIB_CLOUD_ROOT_DROPBOX"] = null,
            ["OSLIB_CLOUD_ROOT_ONEDRIVE"] = null,
            ["OSLIB_CLOUD_ROOT_GOOGLEDRIVE"] = null,
            ["OSLIB_CLOUD_ROOT_ICLOUD"] = null
        });

        try
        {
            Os.ResetCloudStorageCache();
            var roots = Os.GetCloudStorageRoots(refresh: true);

            Assert.Equal(new RaiPath(googleDrive).Path, roots[CloudStorageType.GoogleDrive]);
            Assert.Equal(new RaiPath(iCloud).Path, roots[CloudStorageType.ICloud]);
        }
        finally
        {
            env.Dispose();
            Cleanup(root);
        }
    }

    [Fact]
    public void GetCloudDiscoveryReport_IncludesProviderLines()
    {
        var report = Os.GetCloudDiscoveryReport(refresh: true);

        Assert.Contains("Dropbox", report);
        Assert.Contains("OneDrive", report);
        Assert.Contains("GoogleDrive", report);
        Assert.Contains("ICloud", report);
    }

    private static RaiPath NewTestRoot()
    {
        return new RaiPath(Os.TempDir) / "oslib-tests" / "cloud-discovery" / Guid.NewGuid().ToString("N");
    }

    private static void Cleanup(RaiPath root)
    {
        try
        {
            if (Directory.Exists(root.Path))
                new RaiFile(root.Path).rmdir(depth: 10, deleteFiles: true);
        }
        catch
        {
        }
    }

    private sealed class EnvScope : IDisposable
    {
        private readonly Dictionary<string, string?> _before = new();

        public EnvScope(Dictionary<string, string?> vars)
        {
            foreach (var kvp in vars)
            {
                _before[kvp.Key] = Environment.GetEnvironmentVariable(kvp.Key);
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }
        }

        public void Dispose()
        {
            foreach (var kvp in _before)
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
        }
    }
}
