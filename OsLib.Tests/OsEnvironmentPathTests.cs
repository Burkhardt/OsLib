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
	public void AppRootDir_UsesCurrentWorkingDirectory()
	{
		OsTestEnvironment.ResetOsCaches();

		Assert.Equal(new RaiPath(EnsureTrailingSeparator(Directory.GetCurrentDirectory())).Path, Os.AppRootDir.Path);
	}
}
