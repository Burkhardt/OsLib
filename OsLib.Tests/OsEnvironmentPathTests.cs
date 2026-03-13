using System.IO;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class OsEnvironmentPathTests
{
	[Fact]
	public void TempDir_MatchesSystemTempPath_WhenNotConfigured()
	{
		OsTestEnvironment.ResetOsCaches();

		Assert.Equal(Path.GetTempPath(), Os.TempDir);
		Assert.True(Path.IsPathRooted(Os.TempDir));
	}

	[Fact]
	public void HomeDir_FallsBackToUserProfileSpecialFolder_WhenPrimaryVariablesMissing()
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

			Assert.Equal(expected, Os.HomeDir);
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

		Assert.Equal(expected, Os.LocalBackupDir);
	}

	[Fact]
	public void LocalBackupDir_DefaultResolution_IsRooted_AndNotCloudBacked()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig();

		Assert.True(Path.IsPathRooted(Os.LocalBackupDir));
		Assert.False(new RaiFile(Os.LocalBackupDir).Cloud);
		Assert.Contains("backup", Os.LocalBackupDir, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void LocalBackupDir_PrefersConfiguredValue_OverLocalApplicationDataCandidate()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root);

		var overrideDir = (root / "override-backup").Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		new RaiPath(overrideDir).mkdir();
		env.WriteConfig(localBackupDir: overrideDir);

		Assert.Equal(new RaiPath(overrideDir).Path, Os.LocalBackupDir);
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

		Assert.Equal(new RaiPath(first).Path, Os.LocalBackupDir);

		env.WriteConfig(localBackupDir: second);
		Assert.Equal(new RaiPath(second).Path, Os.LocalBackupDir);

		OsTestEnvironment.ResetOsCaches();
		Assert.Equal(new RaiPath(second).Path, Os.LocalBackupDir);
	}

	[Fact]
	public void GetDefaultConfigPath_UsesUnixConfigDirectory()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root);

		Assert.Equal(new RaiFile(Path.Combine(env.Home, ".config", "RAIkeep", "osconfig.json")).FullName, Os.GetDefaultConfigPath());
	}

	[Fact]
	public void GetDefaultConfigPath_UsesWindowsAppDataLocation()
	{
		var root = OsTestEnvironment.NewTestRoot("env-paths");
		using var env = new OsTestEnvironment(root, forcedType: OsType.Windows);

		Assert.Equal(Path.Combine(env.AppData, "RAIkeep", "osconfig.json"), Os.GetDefaultConfigPath());
	}
}
