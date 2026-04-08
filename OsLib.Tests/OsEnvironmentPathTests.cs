using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class OsEnvironmentPathTests
{
	private static string EnsureTrailingSeparator(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		var normalized = Os.NormSeperator(value);
		return normalized.EndsWith(Os.DIR, StringComparison.Ordinal) ? normalized : normalized + Os.DIR;
	}

	[Fact]
	public void TempDir_ThrowsWhenConfigFileMissing()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root);
		env.DeleteConfig();

		var ex = Assert.Throws<FileNotFoundException>(() => _ = Os.TempDir.Path);

		Assert.Contains(env.ConfigPath, ex.Message);
	}

	[Fact]
	public void UserHomeDir_FallsBackToUserProfileSpecialFolder_WhenPrimaryVariablesMissing()
	{
		var beforeHome = Environment.GetEnvironmentVariable("HOME");
		var beforeUserProfile = Environment.GetEnvironmentVariable("USERPROFILE");
		var beforeHomeDrive = Environment.GetEnvironmentVariable("HOMEDRIVE");
		var beforeHomePath = Environment.GetEnvironmentVariable("HOMEPATH");

		try
		{
			Environment.SetEnvironmentVariable("HOME", null);
			Environment.SetEnvironmentVariable("USERPROFILE", null);
			Environment.SetEnvironmentVariable("HOMEDRIVE", null);
			Environment.SetEnvironmentVariable("HOMEPATH", null);
			OsTestEnvironment.ResetOsCaches();

			var expected = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? string.Empty;

			Assert.Equal(expected, Os.UserHomeDir.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		}
		finally
		{
			Environment.SetEnvironmentVariable("HOME", beforeHome);
			Environment.SetEnvironmentVariable("USERPROFILE", beforeUserProfile);
			Environment.SetEnvironmentVariable("HOMEDRIVE", beforeHomeDrive);
			Environment.SetEnvironmentVariable("HOMEPATH", beforeHomePath);
			OsTestEnvironment.ResetOsCaches();
		}
	}

	[Fact]
	public void LocalBackupDir_ExpandsTildeConfigValue_ToHomeDirectory()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig(localBackupDir: "~/.local-backup");

		var expected = (new RaiPath(env.Home) / ".local-backup").Path;

		Assert.Equal(expected, Os.LocalBackupDir.Path);
	}

	[Fact]
	public void LocalBackupDir_DefaultResolution_IsRooted_AndNotCloudBacked()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig();

		Assert.True(Path.IsPathRooted(Os.LocalBackupDir.Path));
		Assert.False(new RaiFile(Os.LocalBackupDir.Path).Cloud);
		Assert.Equal(Os.TempDir.Path, Os.LocalBackupDir.Path);
	}

	[Fact]
	public void LocalBackupDir_PrefersConfiguredValue_OverLocalApplicationDataCandidate()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root);

		var overrideDir = (root / "override-backup").Path;
		new RaiPath(overrideDir).mkdir();
		env.WriteConfig(localBackupDir: overrideDir);

		Assert.Equal(new RaiPath(overrideDir).Path, Os.LocalBackupDir.Path);
	}

	[Fact]
	public void LocalBackupDir_ReevaluatesAfterExplicitReload_WhenConfigChanges()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root);

		var first = (root / "first-backup").Path;
		var second = (root / "second-backup").Path;
		new RaiPath(first).mkdir();
		new RaiPath(second).mkdir();
		env.WriteConfig(localBackupDir: first);

		Assert.Equal(new RaiPath(first).Path, Os.LocalBackupDir.Path);

		env.WriteConfig(localBackupDir: second);
		Assert.Equal(new RaiPath(first).Path, Os.LocalBackupDir.Path);

		_ = Os.LoadConfig();
		Assert.Equal(new RaiPath(second).Path, Os.LocalBackupDir.Path);
	}

	[Fact]
	public void AppRootDir_UsesCurrentWorkingDirectory()
	{
		OsTestEnvironment.ResetOsCaches();

		Assert.Equal(new RaiPath(EnsureTrailingSeparator(Directory.GetCurrentDirectory())).Path, Os.AppRootDir.Path);
	}

	[Fact]
	public void GetDefaultConfigPath_UsesFixedRAIkeepConfigLocation()
	{
		OsTestEnvironment.ResetOsCaches();

		var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? string.Empty;
		var homeDir = EnsureTrailingSeparator(home);
		var raiPath = new RaiPath(homeDir) / ".config" / "RAIkeep";
		var expected = new RaiFile(raiPath, name: "osconfig", ext: "json5").FullName;

		Assert.Equal(expected, Os.ConfigFileFullName);
	}

	[Fact]
	public void GetDefaultConfigPath_UsesConfiguredOverride_WhenPresent()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root);

		Assert.Equal(env.ConfigPath, Os.ConfigFileFullName);
	}

	[Fact]
	public void GetObserverSshTarget_ReturnsConfiguredValue()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig(observers: new Dictionary<string, string> { ["Mzansi"] = "admin@mzansi" });

		Assert.Equal("admin@mzansi", Os.GetObserverSshTarget("mzansi"));
	}

	[Fact]
	public void GetObserverSshTarget_ThrowsWhenObserverNotConfigured()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig();

		Assert.Throws<InvalidOperationException>(() => Os.GetObserverSshTarget("mzansi"));
	}

	[Fact]
	public void IsCloudPath_ThrowsWhenConfigFileMissing_AndDoesNotCreateConfigFile()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
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
		var root1 = OsTestEnvironment.NewTestRoot("env-paths", "first");
		var google1 = (root1 / "google-one").Path;
		Directory.CreateDirectory(google1);
		using (var env1 = new OsTestEnvironment(root1))
		{
			env1.WriteConfig(googleDrive: google1);
			dynamic config = Os.LoadConfig();
			Assert.Equal(new RaiPath(google1).Path, new RaiPath((string)config.Cloud.GoogleDrive).Path);
		}

		var root2 = OsTestEnvironment.NewTestRoot("env-paths", "second");
		var google2 = (root2 / "google-two").Path;
		Directory.CreateDirectory(google2);
		using var env2 = new OsTestEnvironment(root2);
		env2.WriteConfig(googleDrive: google2);

		dynamic configAfterSwitch = Os.LoadConfig();
		Assert.Equal(new RaiPath(google2).Path, new RaiPath((string)configAfterSwitch.Cloud.GoogleDrive).Path);
	}

	[Fact]
	public void GetObserverSshTarget_ReadsFromUpdatedConfig_AfterEnvironmentSwitch()
	{
		var root1 = OsTestEnvironment.NewTestRoot("env-paths", "first");
		using (var env1 = new OsTestEnvironment(root1))
		{
			env1.WriteConfig(observers: new Dictionary<string, string> { ["Mzansi"] = "alpha@host" });
			Assert.Equal("alpha@host", Os.GetObserverSshTarget("mzansi"));
		}

		var root2 = OsTestEnvironment.NewTestRoot("env-paths", "second");
		using var env2 = new OsTestEnvironment(root2);
		env2.WriteConfig(observers: new Dictionary<string, string> { ["Mzansi"] = "beta@host" });

		Assert.Equal("beta@host", Os.GetObserverSshTarget("mzansi"));
	}
}
