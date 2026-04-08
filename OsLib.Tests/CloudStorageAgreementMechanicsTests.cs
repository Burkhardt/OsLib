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
		env.WriteConfig(dropbox: dropbox, googleDrive: googleDrive);
		Directory.CreateDirectory(dropbox);
		Directory.CreateDirectory(googleDrive);
		var expectedRoot = string.Empty;
		var providerNames = new List<string>();
		try
		{
			foreach (var providerEntry in Os.Config.DefaultCloudOrder)
			{
				var providerName = providerEntry?.ToString();
				if (!string.IsNullOrWhiteSpace(providerName))
					providerNames.Add(providerName);
			}
		}
		catch
		{
		}
		if (!providerNames.Any())
			providerNames.AddRange(new[] { nameof(Cloud.OneDrive), nameof(Cloud.Dropbox), nameof(Cloud.GoogleDrive) });
		foreach (var providerName in providerNames)
		{
			if (!Enum.TryParse(providerName, ignoreCase: true, out Cloud provider))
				continue;
			var candidate = Os.GetCloudStorageRoot(provider)?.Path ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(candidate))
			{
				expectedRoot = candidate;
				break;
			}
		}
		Assert.Equal(expectedRoot, Os.CloudStorageRootDir.Path);
		Assert.Equal(new RaiPath(googleDrive).Path, Os.GetCloudStorageRoot(Cloud.GoogleDrive).Path);
	}
	[Fact]
	public void CloudStorageRoot_ThrowsWithSetupGuidance_WhenNothingIsDiscovered()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-agreement");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig();
		var ex = Assert.Throws<DirectoryNotFoundException>(() => _ = Os.CloudStorageRootDir);
		Assert.Contains("Configure Os.Config", ex.Message);
		Assert.Contains("CLOUD_STORAGE_DISCOVERY.md", ex.Message);
	}
	[Fact]
	public void GetCloudStorageRoots_UsesConfiguredDefaultUserConfig()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-agreement");
		using var env = new OsTestEnvironment(root);
		var configuredGoogle = (root / "ConfiguredGoogleDrive").Path;
		env.WriteConfig(googleDrive: configuredGoogle);
		Directory.CreateDirectory(configuredGoogle);
		var googleRoot = Os.GetCloudStorageRoot(Cloud.GoogleDrive).Path;
		Assert.Equal(new RaiPath(configuredGoogle).Path, googleRoot);
		var dropboxConfigured = true;
		try
		{
			var dropboxRoot = Os.GetCloudStorageRoot(Cloud.Dropbox).Path;
			dropboxConfigured = !string.IsNullOrWhiteSpace(dropboxRoot);
		}
		catch
		{
			dropboxConfigured = false;
		}
		Assert.False(dropboxConfigured);
	}
	[Fact]
	public void GetCloudStorageRoots_ProbesHomeOneDriveVariants_OnUnix()
	{
		if (!Os.IsUnixLike)
			return;
		var root = OsTestEnvironment.NewTestRoot("cloud-agreement");
		using var env = new OsTestEnvironment(root);

		var oneDriveVariant = new RaiPath(env.Home) / "OneDrive - Mzansi";
		env.WriteConfig(oneDrive: oneDriveVariant.Path);
		Directory.CreateDirectory(oneDriveVariant.Path);
		Assert.Equal(oneDriveVariant.Path, Os.GetCloudStorageRoot(Cloud.OneDrive).Path);
	}
	[Fact]
	public void LocalBackupDir_UsesExplicitNonCloudConfigValue()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-agreement");
		using var env = new OsTestEnvironment(root);
		var localBackup = (root / "local-backup").Path;
		env.WriteConfig(localBackupDir: localBackup);
		Directory.CreateDirectory(localBackup);
		Assert.Equal(new RaiPath(localBackup).Path, Os.LocalBackupDir.Path);
		Assert.False(new RaiFile(Os.LocalBackupDir.Path).Cloud);
	}
	[Fact]
	public void LocalBackupDir_RejectsCloudBackedConfiguredValue()
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-agreement");
		using var env = new OsTestEnvironment(root);
		var googleDrive = (root / "GoogleDriveRoot").Path;
		var cloudBackedBackup = (new RaiPath(googleDrive) / "Backups").Path;
		env.WriteConfig(localBackupDir: cloudBackedBackup, googleDrive: googleDrive);
		Directory.CreateDirectory(googleDrive);
		Directory.CreateDirectory(cloudBackedBackup);
		Assert.NotEqual(new RaiPath(cloudBackedBackup).Path, Os.LocalBackupDir.Path);
		Assert.False(new RaiFile(Os.LocalBackupDir.Path).Cloud);
	}
	[Theory]
	[InlineData(Cloud.Dropbox, "DropboxRoot")]
	[InlineData(Cloud.OneDrive, "OneDriveRoot")]
	[InlineData(Cloud.GoogleDrive, "GoogleDriveRoot")]
	public void Backup_StripsConfiguredCloudRoot_FromBackupTargetPath(Cloud provider, string rootName)
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-agreement");
		using var env = new OsTestEnvironment(root);
		var localBackup = root / "local-backup";
		var cloudRoot = root / rootName;
		var projectDir = cloudRoot / "Work" / "Reports";
		switch (provider)
		{
			case Cloud.Dropbox:
				env.WriteConfig(localBackupDir: localBackup.ToString(), dropbox: cloudRoot.ToString());
				break;
			case Cloud.OneDrive:
				env.WriteConfig(localBackupDir: localBackup.ToString(), oneDrive: cloudRoot.ToString());
				break;
			case Cloud.GoogleDrive:
				env.WriteConfig(localBackupDir: localBackup.ToString(), googleDrive: cloudRoot.ToString());
				break;
		}
		Directory.CreateDirectory(localBackup.Path);
		Directory.CreateDirectory(projectDir.Path);
		var source = new TextFile(projectDir, "report.txt", content: "provider backup").Save();
		Assert.Equal($"Work{Os.DIR}Reports{Os.DIR}", RaiFile.BackupRelativePath(source.Path).Path);
		var backup = source.backup(copy: true);
		Assert.Equal((localBackup / "Work" / "Reports").ToString(), backup.Path.ToString());
		Assert.True(File.Exists(backup.FullName));
		Assert.True(File.Exists(source.FullName));
	}
	[Fact]
	public void Backup_MirrorsLocalDirectoryShape_ForNonCloudFiles()
	{
		var root = OsTestEnvironment.NewTestRoot("backup", testName: "local-shape");
		using var env = new OsTestEnvironment(root);
		var localBackup = root / "bak";
		var sourceDir = root / "src" / "logs";
		env.WriteConfig(localBackupDir: localBackup.Path);
		Directory.CreateDirectory(localBackup.Path);
		Directory.CreateDirectory(sourceDir.Path);
		var source = new TextFile(sourceDir, "app.log", content: "local backup").Save();

		Assert.Equal(sourceDir.Path, new RaiPath(source.FullName).Path);
		Assert.Equal(GetMirroredAbsoluteDirectoryTail(sourceDir), RaiFile.BackupRelativePath(source.Path).Path);

		var backup = source.backup(copy: true);
		Assert.Equal(MirrorAbsoluteDirectoryUnder(Os.LocalBackupDir, sourceDir), backup.Path.ToString());
		Assert.True(File.Exists(backup.FullName));
		Assert.True(File.Exists(source.FullName));
	}
	[Fact]
	public void RaiPath_SlashString_AppendsRelativeDirectorySegments()
	{
		var root = OsTestEnvironment.NewTestRoot("backup", testName: "slash-string");
		var appended = root / "src" / "logs";

		Assert.StartsWith(root.Path, appended.Path);
		Assert.EndsWith($"src{Os.DIR}logs{Os.DIR}", appended.Path);
	}

	private static string MirrorAbsoluteDirectoryUnder(RaiPath root, RaiPath absoluteDirectory)
	{
		return new RaiPath(root.Path + GetMirroredAbsoluteDirectoryTail(absoluteDirectory)).Path;
	}

	private static string GetMirroredAbsoluteDirectoryTail(RaiPath absoluteDirectory)
	{
		return absoluteDirectory.Path.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	}
}
