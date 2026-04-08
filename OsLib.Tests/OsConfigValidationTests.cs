using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class OsConfigValidationTests
{
	private static string EnsureTrailingSeparator(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		var normalized = Os.NormSeperator(value);
		return normalized.EndsWith(Os.DIR, StringComparison.Ordinal) ? normalized : normalized + Os.DIR;
	}

	[Fact]
	public void ConfigFileFullName_UsesFixedRAIkeepConfigLocation()
	{
		OsTestEnvironment.ResetOsCaches();

		var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? string.Empty;
		var homeDir = EnsureTrailingSeparator(home);
		var raiPath = new RaiPath(homeDir) / ".config" / "RAIkeep";
		var expected = new RaiFile(raiPath, name: "osconfig", ext: "json5").FullName;

		Assert.Equal(expected, Os.ConfigFileFullName);
	}

	[Fact]
	public void ConfigFileFullName_UsesSandboxOverride_WhenPresent()
	{
		var root = OsTestEnvironment.NewTestRoot("config-validation");
		using var env = new OsTestEnvironment(root);

		Assert.Equal(env.ConfigPath, Os.ConfigFileFullName);
	}

	[Fact]
	public void LoadConfig_ThrowsWhenConfigFileMissing_AndWritesStartupDiagnostic()
	{
		var root = OsTestEnvironment.NewTestRoot("config-validation");
		using var env = new OsTestEnvironment(root);
		env.DeleteConfig();

		var loggerFactory = new TestLoggerFactory();
		var startupSink = new TestStartupDiagnosticSink();
		Os.ConfigureDiagnostics(loggerFactory, startupSink);

		var ex = Assert.Throws<FileNotFoundException>(() => Os.LoadConfig());

		Assert.Contains(env.ConfigPath, ex.Message);
		Assert.Contains(loggerFactory.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("missing", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(startupSink.Messages, message => message.Contains("cannot continue", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void LoadConfig_ThrowsWhenConfigIsMalformed_AndWritesStartupDiagnostic()
	{
		var root = OsTestEnvironment.NewTestRoot("config-validation");
		using var env = new OsTestEnvironment(root);

		Directory.CreateDirectory(Path.GetDirectoryName(env.ConfigPath)!);
		File.WriteAllText(env.ConfigPath, "{ invalid json");
		OsTestEnvironment.ResetOsCaches();

		var loggerFactory = new TestLoggerFactory();
		var startupSink = new TestStartupDiagnosticSink();
		Os.ConfigureDiagnostics(loggerFactory, startupSink);

		var ex = Assert.Throws<InvalidDataException>(() => Os.LoadConfig());

		Assert.Contains(env.ConfigPath, ex.Message);
		Assert.Contains(loggerFactory.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("malformed", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(startupSink.Messages, message => message.Contains("cannot continue", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void LoadConfig_ThrowsWhenTempDirIsMissing()
	{
		var root = OsTestEnvironment.NewTestRoot("config-validation");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig(includeTempDir: false);

		var loggerFactory = new TestLoggerFactory();
		var startupSink = new TestStartupDiagnosticSink();
		Os.ConfigureDiagnostics(loggerFactory, startupSink);

		var ex = Assert.Throws<OsConfigValidationException>(() => Os.LoadConfig());

		Assert.Contains("TempDir is missing", ex.Message);
		Assert.Contains(startupSink.Messages, message => message.Contains("TempDir is missing", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void LoadConfig_ThrowsWhenTempDirDirectoryDoesNotExist()
	{
		var root = OsTestEnvironment.NewTestRoot("config-validation");
		using var env = new OsTestEnvironment(root);
		var missingTempDir = root / "missing-temp";
		env.WriteConfig(tempDir: missingTempDir.Path);
		Directory.Delete(missingTempDir.Path, recursive: true);
		OsTestEnvironment.ResetOsCaches();

		var ex = Assert.Throws<OsConfigValidationException>(() => Os.LoadConfig());

		Assert.Contains("TempDir directory", ex.Message);
		Assert.Contains(missingTempDir.Path, ex.Message);
	}

	[Fact]
	public void LoadConfig_UsesConfiguredLocalBackupDir_WhenValid()
	{
		var root = OsTestEnvironment.NewTestRoot("config-validation");
		using var env = new OsTestEnvironment(root);
		var localBackupDir = root / "local-backup";
		Directory.CreateDirectory(localBackupDir.Path);
		env.WriteConfig(localBackupDir: localBackupDir.Path);

		var config = Os.LoadConfig();

		Assert.NotNull(config);
		Assert.Equal(localBackupDir.Path, Os.LocalBackupDir.Path);
	}

	[Fact]
	public void LoadConfig_DisablesLocalBackup_WhenNotConfigured_AndLogsReducedFeatures()
	{
		var root = OsTestEnvironment.NewTestRoot("config-validation");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig();

		var loggerFactory = new TestLoggerFactory();
		var startupSink = new TestStartupDiagnosticSink();
		Os.ConfigureDiagnostics(loggerFactory, startupSink);

		var config = Os.LoadConfig();

		Assert.NotNull(config);
		Assert.Null(Os.LocalBackupDir);
		Assert.Contains(loggerFactory.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Backup features are disabled", StringComparison.OrdinalIgnoreCase));
		Assert.Empty(startupSink.Messages);
	}

	[Fact]
	public void LoadConfig_DisablesLocalBackup_WhenConfiguredPathIsCloudBacked_AndLogsReducedFeatures()
	{
		var root = OsTestEnvironment.NewTestRoot("config-validation");
		using var env = new OsTestEnvironment(root);
		var googleDrive = root / "GoogleDriveRoot";
		var cloudBackedBackup = googleDrive / "Backups";
		Directory.CreateDirectory(googleDrive.Path);
		Directory.CreateDirectory(cloudBackedBackup.Path);
		env.WriteConfig(googleDrive: googleDrive.Path, localBackupDir: cloudBackedBackup.Path);

		var loggerFactory = new TestLoggerFactory();
		var startupSink = new TestStartupDiagnosticSink();
		Os.ConfigureDiagnostics(loggerFactory, startupSink);

		var config = Os.LoadConfig();

		Assert.NotNull(config);
		Assert.Null(Os.LocalBackupDir);
		Assert.Contains(loggerFactory.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("cloud-backed", StringComparison.OrdinalIgnoreCase));
		Assert.Empty(startupSink.Messages);
	}

	[Fact]
	public void RaiFile_Backup_ThrowsWhenBackupFeatureIsDisabled()
	{
		var root = OsTestEnvironment.NewTestRoot("config-validation");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig();
		_ = Os.LoadConfig();

		var sourceDir = root / "source";
		Directory.CreateDirectory(sourceDir.Path);
		var source = new TextFile(sourceDir, "app.log", content: "backup disabled").Save();

		var ex = Assert.Throws<InvalidOperationException>(() => source.backup(copy: true));

		Assert.Contains("Local backup is disabled", ex.Message);
	}

	[Fact]
	public void LoadConfig_ThrowsWhenDefaultCloudOrderProviderHasNoConfiguredRoot()
	{
		var root = OsTestEnvironment.NewTestRoot("config-validation");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig(defaultCloudOrder: new[] { Cloud.GoogleDrive });

		var ex = Assert.Throws<OsConfigValidationException>(() => Os.LoadConfig());

		Assert.Contains("Cloud.GoogleDrive is missing", ex.Message);
	}

	[Fact]
	public void LoadConfig_ThrowsWhenConfiguredCloudRootDoesNotExist()
	{
		var root = OsTestEnvironment.NewTestRoot("config-validation");
		using var env = new OsTestEnvironment(root);
		var missingGoogleDrive = root / "MissingGoogleDrive";
		env.WriteConfig(googleDrive: missingGoogleDrive.Path, defaultCloudOrder: new[] { Cloud.GoogleDrive });

		var ex = Assert.Throws<OsConfigValidationException>(() => Os.LoadConfig());

		Assert.Contains("Cloud.GoogleDrive directory", ex.Message);
		Assert.Contains(missingGoogleDrive.Path, ex.Message);
	}

	[Fact]
	public void LoadConfig_SucceedsWithoutConfiguredCloudProviders_AndLogsReducedFeatures()
	{
		var root = OsTestEnvironment.NewTestRoot("config-validation");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig();

		var loggerFactory = new TestLoggerFactory();
		var startupSink = new TestStartupDiagnosticSink();
		Os.ConfigureDiagnostics(loggerFactory, startupSink);

		var config = Os.LoadConfig();

		Assert.NotNull(config);
		Assert.Null(Os.GetCloudStorageRoot(Cloud.Dropbox));
		Assert.Null(Os.GetCloudStorageRoot(Cloud.OneDrive));
		Assert.Null(Os.GetCloudStorageRoot(Cloud.GoogleDrive));
		Assert.Contains(loggerFactory.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Cloud features are disabled", StringComparison.OrdinalIgnoreCase));
		Assert.Empty(startupSink.Messages);
	}

	[Fact]
	public void LoadConfig_UsesConfiguredCloudRoots_AndDefaultOrder()
	{
		var root = OsTestEnvironment.NewTestRoot("config-validation");
		using var env = new OsTestEnvironment(root);
		var dropbox = root / "DropboxRoot";
		var googleDrive = root / "GoogleDriveRoot";
		Directory.CreateDirectory(dropbox.Path);
		Directory.CreateDirectory(googleDrive.Path);
		env.WriteConfig(dropbox: dropbox.Path, googleDrive: googleDrive.Path, defaultCloudOrder: new[] { Cloud.GoogleDrive, Cloud.Dropbox });

		var config = Os.LoadConfig();

		Assert.NotNull(config);
		Assert.Equal(googleDrive.Path, Os.CloudStorageRootDir.Path);
		Assert.Equal(dropbox.Path, Os.GetCloudStorageRoot(Cloud.Dropbox).Path);
		Assert.Equal(googleDrive.Path, Os.GetCloudStorageRoot(Cloud.GoogleDrive).Path);
	}

	[Fact]
	public void CloudStorageRootDir_ThrowsWithSetupGuidance_WhenNoCloudConfigured()
	{
		var root = OsTestEnvironment.NewTestRoot("config-validation");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig();
		_ = Os.LoadConfig();

		var ex = Assert.Throws<DirectoryNotFoundException>(() => _ = Os.CloudStorageRootDir);

		Assert.Contains("Configure Os.Config", ex.Message);
		Assert.Contains("CLOUD_STORAGE_DISCOVERY.md", ex.Message);
	}

	[Fact]
	public void IsCloudPath_ThrowsWhenConfigFileMissing_AndDoesNotCreateConfigFile()
	{
		var root = OsTestEnvironment.NewTestRoot("config-validation");
		using var env = new OsTestEnvironment(root);
		env.DeleteConfig();

		Assert.False(File.Exists(env.ConfigPath));

		var ex = Assert.Throws<FileNotFoundException>(() => Os.IsCloudPath((root / "some-path").Path));

		Assert.Contains(env.ConfigPath, ex.Message);
		Assert.False(File.Exists(env.ConfigPath));
	}

	[Fact]
	public void LoadConfig_UsesUpdatedDefaultConfigPath_AfterEnvironmentSwitch()
	{
		var root1 = OsTestEnvironment.NewTestRoot("config-validation", "first");
		var google1 = (root1 / "google-one").Path;
		Directory.CreateDirectory(google1);
		using (var env1 = new OsTestEnvironment(root1))
		{
			env1.WriteConfig(googleDrive: google1);
			dynamic config = Os.LoadConfig();
			Assert.Equal(new RaiPath(google1).Path, new RaiPath((string)config.Cloud.GoogleDrive).Path);
		}

		var root2 = OsTestEnvironment.NewTestRoot("config-validation", "second");
		var google2 = (root2 / "google-two").Path;
		Directory.CreateDirectory(google2);
		using var env2 = new OsTestEnvironment(root2);
		env2.WriteConfig(googleDrive: google2);

		dynamic configAfterSwitch = Os.LoadConfig();
		Assert.Equal(new RaiPath(google2).Path, new RaiPath((string)configAfterSwitch.Cloud.GoogleDrive).Path);
	}
}

[Collection("CloudStorageEnvironment")]
public class OsConfigRealEnvironmentValidationTests
{
	[Fact]
	public void MachineConfig_PassesStartupValidation_WhenPresent()
	{
		var configPath = Os.ConfigFileFullName;
		if (!File.Exists(configPath))
			Assert.Skip($"Required config file is missing: {configPath}. {Os.GetCloudStorageSetupGuidance()} {CloudStorageRealTestEnvironment.GetRemoteObserverSetupGuidance()}");

		OsTestEnvironment.ResetOsCaches();
		var config = Os.LoadConfig();

		Assert.NotNull(config);
	}
}