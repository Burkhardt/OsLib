using System.IO;
using System.Linq;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class OsEnvironmentPathTests
{
	[Fact]
	public void TempDir_MatchesSystemTempPath_WhenNotConfigured()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root);
		env.DeleteConfig();

		Assert.Equal(Path.GetTempPath(), Os.TempDir.Path);
		Assert.True(Path.IsPathRooted(Os.TempDir.Path));
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
		Assert.Contains("backup", Os.LocalBackupDir.Path, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void LocalBackupDir_PrefersConfiguredValue_OverLocalApplicationDataCandidate()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root);

		var overrideDir = (root / "override-backup").Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		new RaiPath(overrideDir).mkdir();
		env.WriteConfig(localBackupDir: overrideDir);

		Assert.Equal(new RaiPath(overrideDir).Path, Os.LocalBackupDir.Path);
	}

	[Fact]
	public void LocalBackupDir_ReevaluatesAfterCacheReset_WhenConfigChanges()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root);

		var first = (root / "first-backup").Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		var second = (root / "second-backup").Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		new RaiPath(first).mkdir();
		new RaiPath(second).mkdir();
		env.WriteConfig(localBackupDir: first);

		Assert.Equal(new RaiPath(first).Path, Os.LocalBackupDir.Path);

		env.WriteConfig(localBackupDir: second);
		Assert.Equal(new RaiPath(second).Path, Os.LocalBackupDir.Path);

		OsTestEnvironment.ResetOsCaches();
		Assert.Equal(new RaiPath(second).Path, Os.LocalBackupDir.Path);
	}

	[Fact]
	public void AppRootDir_UsesCurrentWorkingDirectory()
	{
		OsTestEnvironment.ResetOsCaches();

		Assert.Equal(new RaiPath(Directory.GetCurrentDirectory()).Path, Os.AppRootDir.Path);
	}

	[Fact]
	public void GetDefaultConfigPath_UsesFixedRAIkeepConfigLocation()
	{
		OsTestEnvironment.ResetOsCaches();

		var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? string.Empty;
		var expected = new RaiFile(new RaiPath(home) / ".config" / "RAIkeep", "osconfig", "json").FullName;

		Assert.Equal(expected, Os.GetDefaultConfigPath());
	}

	[Fact]
	public void GetDefaultConfigPath_UsesConfiguredOverride_WhenPresent()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root);

		Assert.Equal(env.ConfigPath, Os.GetDefaultConfigPath());
	}

	[Fact]
	public void ConfiguredOnlyCloudResolution_DoesNotCreateMissingConfigFile()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root);
		env.DeleteConfig();
		using var configuredCloud = Os.PushCloudRootResolutionMode(CloudRootResolutionMode.ConfiguredOnly);

		var roots = Os.GetCloudStorageRoots(refresh: true);

		Assert.Empty(roots);
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
			Assert.Equal(new RaiPath(google1).Path, Os.LoadConfig(refresh: true).GooglePath!.Path);
		}

		var root2 = OsTestEnvironment.NewTestRoot("env-paths", "second");
		var google2 = (root2 / "google-two").Path;
		Directory.CreateDirectory(google2);
		using var env2 = new OsTestEnvironment(root2);
		env2.WriteConfig(googleDrive: google2);

		Assert.Equal(new RaiPath(google2).Path, Os.LoadConfig(refresh: true).GooglePath!.Path);
	}

	[Fact]
	public void LoadRemoteTestConfig_UsesUpdatedDefaultConfigPath_AfterEnvironmentSwitch()
	{
		var root1 = OsTestEnvironment.NewTestRoot("env-paths", "first");
		using (var env1 = new OsTestEnvironment(root1))
		{
			env1.WriteConfig();
			WriteRemoteTestConfig(Os.GetDefaultRemoteTestConfigPath(), "alpha@host");
			Assert.Equal("alpha@host", Os.LoadRemoteTestConfig(refresh: true).GetObserver("mzansi")!.SshTarget);
		}

		var root2 = OsTestEnvironment.NewTestRoot("env-paths", "second");
		using var env2 = new OsTestEnvironment(root2);
		env2.WriteConfig();
		WriteRemoteTestConfig(Os.GetDefaultRemoteTestConfigPath(), "beta@host");

		Assert.Equal("beta@host", Os.LoadRemoteTestConfig(refresh: true).GetObserver("mzansi")!.SshTarget);
	}

	private static void WriteRemoteTestConfig(string path, string sshTarget)
	{
		var file = new RaiFile(path);
		RaiFile.mkdir(file.Path);
		File.WriteAllText(
			file.FullName,
			"{\n" +
			"  \"observers\": {\n" +
			$"    \"mzansi\": {{ \"sshTarget\": \"{sshTarget}\" }}\n" +
			"  }\n" +
			"}\n");
	}
}
