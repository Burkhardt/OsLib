using System.IO;
using Newtonsoft.Json.Linq;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class CloudStorageDiscoveryTests
{
	[Fact]
	public void GetCloudStorageRoots_UsesConfiguredRoots()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-discovery");
		using var env = new OsTestEnvironment(root);

		var dropbox = (root / "DropboxRoot").Path;
		var oneDrive = (root / "OneDriveRoot").Path;
		var googleDrive = (root / "GoogleDriveRoot").Path;
		var iCloud = (root / "ICloudRoot").Path;

		Directory.CreateDirectory(dropbox);
		Directory.CreateDirectory(oneDrive);
		Directory.CreateDirectory(googleDrive);
		Directory.CreateDirectory(iCloud);
		env.WriteConfig(dropbox: dropbox, oneDrive: oneDrive, googleDrive: googleDrive, iCloud: iCloud);

		var roots = Os.GetCloudStorageRoots(refresh: true);

		Assert.Equal(new RaiPath(dropbox).Path, roots[CloudStorageType.Dropbox]);
		Assert.Equal(new RaiPath(oneDrive).Path, roots[CloudStorageType.OneDrive]);
		Assert.Equal(new RaiPath(googleDrive).Path, roots[CloudStorageType.GoogleDrive]);
		Assert.Equal(new RaiPath(iCloud).Path, roots[CloudStorageType.ICloud]);
	}

	[Fact]
	public void GetPreferredCloudStorageRoot_RespectsPreferredOrder()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-discovery");
		using var env = new OsTestEnvironment(root);

		var dropbox = (root / "DropboxRoot").Path;
		var googleDrive = (root / "GoogleDriveRoot").Path;
		Directory.CreateDirectory(dropbox);
		Directory.CreateDirectory(googleDrive);
		env.WriteConfig(dropbox: dropbox, googleDrive: googleDrive);

		var preferredDropbox = Os.GetPreferredCloudStorageRoot(CloudStorageType.Dropbox, CloudStorageType.GoogleDrive);
		var preferredGoogle = Os.GetPreferredCloudStorageRoot(CloudStorageType.GoogleDrive, CloudStorageType.Dropbox);

		Assert.Equal(new RaiPath(dropbox).Path, preferredDropbox);
		Assert.Equal(new RaiPath(googleDrive).Path, preferredGoogle);
	}

	[Fact]
	public void LoadConfig_LoadsConfiguredJsonConfiguration()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-discovery");
		using var env = new OsTestEnvironment(root);

		var googleDrive = (root / "GoogleDriveIni").Path;
		var iCloud = (root / "ICloudIni").Path;
		Directory.CreateDirectory(googleDrive);
		Directory.CreateDirectory(iCloud);
		env.WriteConfig(googleDrive: googleDrive, iCloud: iCloud);

		var config = Os.LoadConfig(refresh: true);

		Assert.Equal(new RaiPath(googleDrive).Path, config.GooglePath!.Path);
		Assert.Equal(new RaiPath(iCloud).Path, config.ICloudPath!.Path);
		Assert.Equal(new RaiPath(googleDrive).Path, config.GetCloudDirPath(CloudStorageType.GoogleDrive)!.Path);
		Assert.Equal(new RaiPath(iCloud).Path, config.CloudDirPaths[CloudStorageType.ICloud].Path);
	}

	[Fact]
	public void GetCloudStorageRoots_LoadsDefaultUserConfigLocation()
	{
		if (!Os.IsUnixLike)
			return;

		var root = OsTestEnvironment.NewTestRoot("cloud-discovery");
		using var env = new OsTestEnvironment(root);

		var googleDrive = (root / "GoogleDriveDefaultIni").Path;
		Directory.CreateDirectory(googleDrive);
		env.WriteConfig(googleDrive: googleDrive);

		var config = Os.LoadConfig(refresh: true);

		Assert.Equal(new RaiPath(googleDrive).Path, config.GooglePath!.Path);
	}

	[Fact]
	public void LoadConfig_AutoCreatesDefaultUserConfigFile_WhenMissing()
	{
		if (!Os.IsUnixLike)
			return;

		var root = OsTestEnvironment.NewTestRoot("cloud-discovery");
		using var env = new OsTestEnvironment(root);

		var googleDrive = new RaiPath(env.Home) / "GoogleDrive";
		Directory.CreateDirectory(googleDrive.Path);
		env.DeleteConfig();

		var config = Os.LoadConfig(refresh: true);
		var configPath = Os.GetDefaultConfigPath();

		Assert.True(File.Exists(configPath));
		Assert.Equal(new RaiPath(googleDrive.Path).Path, config.GooglePath!.Path);

		var configJson = JObject.Parse(File.ReadAllText(configPath));
		Assert.Equal(new RaiPath(env.Home).Path, configJson["homeDir"]!.ToString());
		Assert.Equal(string.Empty, configJson["cloud"]!["dropbox"]!.ToString());
		Assert.Equal(string.Empty, configJson["cloud"]!["onedrive"]!.ToString());
		Assert.Equal(new RaiPath(googleDrive.Path).Path, configJson["cloud"]!["googledrive"]!.ToString());
		Assert.Equal(string.Empty, configJson["cloud"]!["icloud"]!.ToString());
	}

	[Fact]
	public void GetCloudStorageRoots_DoesNotOverwriteExistingDefaultUserConfigFile()
	{
		if (!Os.IsUnixLike)
			return;

		var root = OsTestEnvironment.NewTestRoot("cloud-discovery");
		using var env = new OsTestEnvironment(root);

		var googleDrive = new RaiPath(env.Home) / "GoogleDriveExistingConfig";
		Directory.CreateDirectory(googleDrive.Path);
		env.WriteConfig(googleDrive: "/manual/path/");

		_ = Os.GetCloudStorageRoots(refresh: true);

		var config = JObject.Parse(File.ReadAllText(Os.GetDefaultConfigPath()));
		Assert.Equal("/manual/path/", config["cloud"]!["googledrive"]!.ToString());
		Assert.DoesNotContain(new RaiPath(googleDrive.Path).Path, File.ReadAllText(Os.GetDefaultConfigPath()));
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

	[Fact]
	public void GetMacGoogleDriveProbeTarget_PrefersMyDrive_WhenPresent()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-discovery");
		root.mkdir();

		var container = root / "GoogleDrive-rainer.burkhardt@gmail.com";
		var myDrive = container / "My Drive";
		Directory.CreateDirectory(myDrive.Path);

		try
		{
			var resolved = Os.GetMacGoogleDriveProbeTarget(container.Path);

			Assert.Equal(myDrive.Path, resolved);
		}
		finally
		{
			OsTestEnvironment.Cleanup(root);
		}
	}

	[Fact]
	public void GetMacGoogleDriveProbeTarget_FallsBackToContainer_WhenMyDriveIsMissing()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-discovery");
		root.mkdir();

		var container = root / "GoogleDrive-rainer.burkhardt@gmail.com";
		Directory.CreateDirectory(container.Path);

		try
		{
			var resolved = Os.GetMacGoogleDriveProbeTarget(container.Path);

			Assert.Equal(container.Path, resolved);
		}
		finally
		{
			OsTestEnvironment.Cleanup(root);
		}
	}
}
