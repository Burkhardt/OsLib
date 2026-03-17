using System.IO;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class CloudStorageProviderPathTests
{
	[Theory]
	[InlineData(CloudStorageType.Dropbox)]
	[InlineData(CloudStorageType.OneDrive)]
	[InlineData(CloudStorageType.GoogleDrive)]
	[InlineData(CloudStorageType.ICloud)]
	public void GetCloudStorageProviderForPath_ReturnsInstalledProvider_ForDiscoveredRoot(CloudStorageType provider)
	{
		if (!CloudStorageRealTestEnvironment.TryGetCloudTestRoot(provider, "cloud-providers", out var root, out var providerRoot, out var reason))
			Assert.Skip($"Provider {provider}: {reason}. {Os.GetCloudStorageSetupGuidance()}");

		var cloudDir = root / "Workspace" / "Project";
		Directory.CreateDirectory(cloudDir.Path);

		Assert.Equal(provider, Os.GetCloudStorageProviderForPath(providerRoot));
		Assert.Equal(provider, Os.GetCloudStorageProviderForPath(cloudDir.Path));
	}

	[Theory]
	[InlineData(CloudStorageType.Dropbox)]
	[InlineData(CloudStorageType.OneDrive)]
	[InlineData(CloudStorageType.GoogleDrive)]
	[InlineData(CloudStorageType.ICloud)]
	public void RaiFile_CloudFlag_DetectsFilesUnderInstalledProvider(CloudStorageType provider)
	{
		if (!CloudStorageRealTestEnvironment.TryGetCloudTestRoot(provider, "cloud-providers", out var root, out _, out var reason))
			Assert.Skip($"Provider {provider}: {reason}. {Os.GetCloudStorageSetupGuidance()}");

		var cloudDir = root / "Workspace" / "Project";
		var localDir = new RaiPath(Os.TempDir) / "RAIkeep" / "cloud-providers" / "LocalFiles" / provider.ToString();
		cloudDir.mkdir();
		localDir.mkdir();

		var cloudFile = new RaiFile(cloudDir.Path + "sample.txt");
		var localFile = new RaiFile(localDir.Path + "sample.txt");

		Assert.True(cloudFile.Cloud);
		Assert.False(localFile.Cloud);
	}
}
