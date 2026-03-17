using System;
using System.IO;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class CloudStorageAgreementMechanicsTests
{
	[Fact]
	public void CloudStorageRoot_UsesDocumentedDefaultOrder_ForConfiguredRoots()
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

	[Theory]
	[InlineData(CloudStorageType.Dropbox, "DropboxRoot")]
	[InlineData(CloudStorageType.OneDrive, "OneDriveRoot")]
	[InlineData(CloudStorageType.GoogleDrive, "GoogleDriveRoot")]
	[InlineData(CloudStorageType.ICloud, "ICloudRoot")]
	public void Backup_StripsConfiguredCloudRoot_FromBackupTargetPath(CloudStorageType provider, string rootName)
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-agreement");
		using var env = new OsTestEnvironment(root);

		var localBackup = (root / "local-backup").Path;
		var cloudRoot = (root / rootName).Path;
		var projectDir = new RaiPath(cloudRoot) / "Work" / "Reports";
		Directory.CreateDirectory(localBackup);
		Directory.CreateDirectory(projectDir.Path);

		switch (provider)
		{
			case CloudStorageType.Dropbox:
				env.WriteConfig(localBackupDir: localBackup, dropbox: cloudRoot);
				break;
			case CloudStorageType.OneDrive:
				env.WriteConfig(localBackupDir: localBackup, oneDrive: cloudRoot);
				break;
			case CloudStorageType.GoogleDrive:
				env.WriteConfig(localBackupDir: localBackup, googleDrive: cloudRoot);
				break;
			case CloudStorageType.ICloud:
				env.WriteConfig(localBackupDir: localBackup, iCloud: cloudRoot);
				break;
		}

		var source = new TextFile(projectDir, "report.txt", "provider backup").Save();

		Assert.Equal(new RaiPath("Work/Reports").Path, RaiFile.GetBackupRelativeDirectoryPath(source.Path));

		var backup = new RaiFile(source.backup(copy: true));

		Assert.Equal((new RaiPath(localBackup) / "Work" / "Reports").Path, backup.Path);
		Assert.True(File.Exists(backup.FullName));
		Assert.True(File.Exists(source.FullName));
	}

	[Fact]
	public void Backup_PreservesExistingLocalPathShaping_ForNonCloudFiles()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-agreement");
		using var env = new OsTestEnvironment(root);

		var localBackup = (root / "local-backup").Path;
		var sourceDir = root / "project" / "logs";
		Directory.CreateDirectory(localBackup);
		Directory.CreateDirectory(sourceDir.Path);
		env.WriteConfig(localBackupDir: localBackup);

		var source = new TextFile(sourceDir, "app.log", "local backup").Save();
		var expectedRelativeDirectory = source.Path;

		Assert.Equal(expectedRelativeDirectory, RaiFile.GetBackupRelativeDirectoryPath(source.Path));

		var backup = new RaiFile(source.backup(copy: true));

		Assert.Equal(Os.LocalBackupDir + expectedRelativeDirectory, backup.Path);
		Assert.True(File.Exists(backup.FullName));
		Assert.True(File.Exists(source.FullName));
	}
}