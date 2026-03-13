using System;
using System.IO;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class CloudStorageAgreementTests
{
	[Fact]
	public void CloudStorageRoot_UsesDocumentedDefaultOrder()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-agreement");
		using var env = new OsTestEnvironment(root);

		var dropbox = (root / "DropboxRoot").Path;
		var googleDrive = (root / "GoogleDriveRoot").Path;
		Directory.CreateDirectory(dropbox);
		Directory.CreateDirectory(googleDrive);
		env.WriteConfig(dropbox: dropbox, googleDrive: googleDrive);

		Assert.Equal(new RaiPath(googleDrive).Path, Os.CloudStorageRoot);
		Assert.Equal(new RaiPath(googleDrive).Path, Os.GetCloudStorageRoot(CloudStorageType.GoogleDrive));
		Assert.Null(Os.GetCloudStorageRoot(CloudStorageType.ICloud));
	}

	[Fact]
	public void CloudStorageRoot_ThrowsWithSetupGuidance_WhenNothingIsDiscovered()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-agreement");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig();

		var ex = Assert.Throws<DirectoryNotFoundException>(() => _ = Os.CloudStorageRoot);
		Assert.Contains("Configure Os.Config", ex.Message);
		Assert.Contains("CLOUD_STORAGE_DISCOVERY.md", ex.Message);
	}

	[Fact]
	public void GetCloudStorageRoots_UsesConfiguredDefaultUserConfig()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-agreement");
		using var env = new OsTestEnvironment(root);

		var configuredGoogle = (root / "ConfiguredGoogleDrive").Path;
		var configuredICloud = (root / "ConfiguredICloud").Path;
		Directory.CreateDirectory(configuredGoogle);
		Directory.CreateDirectory(configuredICloud);
		env.WriteConfig(googleDrive: configuredGoogle, iCloud: configuredICloud);

		var roots = Os.GetCloudStorageRoots(refresh: true);

		Assert.Equal(new RaiPath(configuredGoogle).Path, roots[CloudStorageType.GoogleDrive]);
		Assert.Equal(new RaiPath(configuredICloud).Path, roots[CloudStorageType.ICloud]);
		Assert.False(roots.ContainsKey(CloudStorageType.Dropbox));
	}

	[Fact]
	public void GetCloudStorageRoots_ProbesHomeOneDriveVariants_OnUnix()
	{
		if (!Os.IsUnixLike)
			return;

		var root = OsTestEnvironment.NewTestRoot("cloud-agreement");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig();

		var oneDriveVariant = new RaiPath(env.Home) / "OneDrive - Mzansi";
		Directory.CreateDirectory(oneDriveVariant.Path);

		Assert.Equal(oneDriveVariant.Path, Os.GetCloudStorageRoot(CloudStorageType.OneDrive, refresh: true));
	}

	[Fact]
	public void RaiFile_UsesDiscoveredGoogleDriveRoot_ForCloudAwareFlag()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-agreement");
		using var env = new OsTestEnvironment(root);

		var googleRoot = new RaiPath((root / "GoogleDriveCustom").Path);
		var projectDir = googleRoot / "Mzansi" / "JsonPit";
		var metadataDir = googleRoot / ".dropbox" / "cache";
		Directory.CreateDirectory(projectDir.Path);
		Directory.CreateDirectory(metadataDir.Path);
		env.WriteConfig(googleDrive: googleRoot.Path);

		var projectFile = new RaiFile(projectDir.Path + "project.json");
		var metadataFile = new RaiFile(metadataDir.Path + "state.db");

		Assert.True(projectFile.Cloud);
		Assert.False(metadataFile.Cloud);
	}

	[Fact]
	public void LocalBackupDir_UsesExplicitNonCloudConfigValue()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-agreement");
		using var env = new OsTestEnvironment(root);

		var localBackup = (root / "local-backup").Path;
		Directory.CreateDirectory(localBackup);
		env.WriteConfig(localBackupDir: localBackup);

		Assert.Equal(new RaiPath(localBackup).Path, Os.LocalBackupDir);
		Assert.False(new RaiFile(Os.LocalBackupDir).Cloud);
	}

	[Fact]
	public void LocalBackupDir_RejectsCloudBackedConfiguredValue()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-agreement");
		using var env = new OsTestEnvironment(root);

		var googleDrive = (root / "GoogleDriveRoot").Path;
		var cloudBackedBackup = (new RaiPath(googleDrive) / "Backups").Path;
		Directory.CreateDirectory(googleDrive);
		Directory.CreateDirectory(cloudBackedBackup);
		env.WriteConfig(localBackupDir: cloudBackedBackup, googleDrive: googleDrive);

		Assert.NotEqual(new RaiPath(cloudBackedBackup).Path, Os.LocalBackupDir);
		Assert.False(new RaiFile(Os.LocalBackupDir).Cloud);
	}
}
