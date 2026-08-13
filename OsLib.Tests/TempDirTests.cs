using OsLib;

namespace OsLib.Tests;

public class TempDirTests
{
	[Fact]
	public void TempDir_IsConfiguredExistingAndWritableThroughOsLibAbstractions()
	{
		var tempDir = Os.TempDir;
		Assert.NotNull(tempDir);
		Assert.True(tempDir.Exists(), $"Configured temporary directory must exist: {tempDir.FullPath}");

		var probe = new TmpFile(tempDir);
		try
		{
			probe.create();
			Assert.True(probe.Exists(), $"Configured temporary directory must be writable: {tempDir.FullPath}");
		}
		finally
		{
			if (probe.Exists())
				probe.rm();
		}
	}
}
