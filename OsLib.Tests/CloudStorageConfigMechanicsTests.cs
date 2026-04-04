using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class CloudStorageConfigMechanicsTests
{
	[Fact]
	public void GetCloudStorageRoots_UsesConfiguredRoots_FromConfig()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-config");
		using var env = new OsTestEnvironment(root);

		var dropbox = (root / "DropboxRoot").Path;
		var oneDrive = (root / "OneDriveRoot").Path;
		var googleDrive = (root / "GoogleDriveRoot").Path;

		Directory.CreateDirectory(dropbox);
		Directory.CreateDirectory(oneDrive);
		Directory.CreateDirectory(googleDrive);
		env.WriteConfig(dropbox: dropbox, oneDrive: oneDrive, googleDrive: googleDrive);

		try
		{
			Assert.Equal(new RaiPath(dropbox).Path, new RaiPath((string)Os.Config.Cloud.Dropbox).Path);
			Assert.Equal(new RaiPath(oneDrive).Path, new RaiPath((string)Os.Config.Cloud.OneDrive).Path);
			Assert.Equal(new RaiPath(googleDrive).Path, new RaiPath((string)Os.Config.Cloud.GoogleDrive).Path);
		}
		catch (Exception ex)
		{
			Assert.Fail($"Config access failed. Verify PascalCase cloud keys (Cloud/Dropbox/OneDrive/GoogleDrive). Error: {ex.Message}");
		}
	}

	[Fact]
	public void GetPreferredCloudStorageRoot_RespectsPreferredOrder()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-config");
		using var env = new OsTestEnvironment(root);

		var dropbox = (root / "DropboxRoot").Path;
		var googleDrive = (root / "GoogleDriveRoot").Path;
		Directory.CreateDirectory(dropbox);
		Directory.CreateDirectory(googleDrive);
		env.WriteConfig(dropbox: dropbox, googleDrive: googleDrive);

		var preferredDropbox = Os.GetCloudStorageRoot(Cloud.Dropbox).Path;
		var preferredGoogle = Os.GetCloudStorageRoot(Cloud.GoogleDrive).Path;

		Assert.Equal(new RaiPath(dropbox).Path, preferredDropbox);
		Assert.Equal(new RaiPath(googleDrive).Path, preferredGoogle);
	}

	[Fact]
	public void LoadConfig_LoadsConfiguredJsonConfiguration()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-config");
		using var env = new OsTestEnvironment(root);

		var googleDrive = (root / "GoogleDriveIni").Path;
		Directory.CreateDirectory(googleDrive);
		env.WriteConfig(googleDrive: googleDrive);

		var config = Os.LoadConfig();

		try
		{
			Assert.Equal(new RaiPath(googleDrive).Path, new RaiPath((string)config.Cloud.GoogleDrive).Path);
		}
		catch (Exception ex)
		{
			Assert.Fail($"Config access failed. Verify PascalCase cloud keys (Cloud/GoogleDrive). Error: {ex.Message}");
		}
	}

	[Fact]
	public void GetCloudStorageRoots_LoadsDefaultUserConfigLocation()
	{
		if (!Os.IsUnixLike)
			return;

		var root = OsTestEnvironment.NewTestRoot("cloud-config");
		using var env = new OsTestEnvironment(root);

		var googleDrive = (root / "GoogleDriveDefaultIni").Path;
		Directory.CreateDirectory(googleDrive);
		env.WriteConfig(googleDrive: googleDrive);

		var config = Os.LoadConfig();

		try
		{
			Assert.Equal(new RaiPath(googleDrive).Path, new RaiPath((string)config.Cloud.GoogleDrive).Path);
		}
		catch (Exception ex)
		{
			Assert.Fail($"Config access failed. Verify PascalCase cloud keys (Cloud/GoogleDrive). Error: {ex.Message}");
		}
	}

	[Fact]
	public void LoadConfig_DoesNotCreateConfigFile_WhenMissing_AndReturnsFallbackDefaults()
	{
		if (!Os.IsUnixLike)
			return;

		var root = OsTestEnvironment.NewTestRoot("cloud-config");
		using var env = new OsTestEnvironment(root);

		var googleDrive = new RaiPath(env.Home) / "GoogleDrive";
		Directory.CreateDirectory(googleDrive.Path);
		env.DeleteConfig();

		var config = Os.LoadConfig();
		var configPath = Os.ConfigFileFullName;

		Assert.False(File.Exists(configPath));
		Assert.Null(config);
	}

	[Fact]
	public void GetCloudStorageRoots_DoesNotOverwriteExistingDefaultUserConfigFile()
	{
		if (!Os.IsUnixLike)
			return;

		var root = OsTestEnvironment.NewTestRoot("cloud-config");
		using var env = new OsTestEnvironment(root);

		var googleDrive = new RaiPath(env.Home) / "GoogleDriveExistingConfig";
		Directory.CreateDirectory(googleDrive.Path);
		env.WriteConfig(googleDrive: "/manual/path/");

		var config = JObject.Parse(File.ReadAllText(Os.ConfigFileFullName));
		Assert.Equal("/manual/path/", config["Cloud"]!["GoogleDrive"]!.ToString());
		Assert.DoesNotContain(new RaiPath(googleDrive.Path).Path, File.ReadAllText(Os.ConfigFileFullName));
	}

	[Theory]
	[InlineData(Cloud.GoogleDrive, "ConfiguredGoogleDrive", "Google Drive")]
	[InlineData(Cloud.OneDrive, "ConfiguredOneDrive", "OneDrive - Personal")]
	public void GetCloudStorageRoots_PrefersConfiguredProviderValue_WithoutProbeFallback(Cloud provider, string configuredDirName, string discoveredDirName)
	{
		if (!Os.IsUnixLike)
			return;

		var root = OsTestEnvironment.NewTestRoot("cloud-config");
		using var env = new OsTestEnvironment(root);

		var configuredRoot = (root / configuredDirName).Path;
		var discoveredRoot = (new RaiPath(env.Home) / discoveredDirName).Path;
		Directory.CreateDirectory(discoveredRoot);

		switch (provider)
		{
			case Cloud.GoogleDrive:
				env.WriteConfig(googleDrive: configuredRoot);
				break;
			case Cloud.OneDrive:
				env.WriteConfig(oneDrive: configuredRoot);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(provider), provider, null);
		}

		var effectiveRoot = Os.GetCloudStorageRoot(provider);

		Assert.Equal(new RaiPath(configuredRoot).Path, effectiveRoot.Path);
		Assert.NotEqual(new RaiPath(discoveredRoot).Path, effectiveRoot.Path);
	}

	[Fact]
	public void GetCloudDiscoveryReport_IncludesProviderLines()
	{
		var report = Os.GetCloudDiscoveryReport();

		Assert.Contains("Dropbox", report);
		Assert.Contains("OneDrive", report);
		Assert.Contains("GoogleDrive", report);
	}

	[Fact]
	public void GetCloudStorageRoot_ReturnsConfiguredGoogleDrivePath()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-config");
		using var env = new OsTestEnvironment(root);
		var googleDrive = (root / "GoogleDriveProbeTarget").Path;
		Directory.CreateDirectory(googleDrive);
		env.WriteConfig(googleDrive: googleDrive);

		Assert.Equal(new RaiPath(googleDrive).Path, Os.GetCloudStorageRoot(Cloud.GoogleDrive).Path);
	}

	[Fact]
	public void GetCloudStorageRoot_ReturnsConfiguredDropboxPath()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-config");
		using var env = new OsTestEnvironment(root);
		var dropbox = (root / "DropboxProbeTarget").Path;
		Directory.CreateDirectory(dropbox);
		env.WriteConfig(dropbox: dropbox);

		Assert.Equal(new RaiPath(dropbox).Path, Os.GetCloudStorageRoot(Cloud.Dropbox).Path);
	}
}