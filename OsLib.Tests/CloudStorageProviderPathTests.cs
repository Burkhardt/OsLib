using System.IO;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class CloudStorageProviderPathTests
{
	[Theory]
	[InlineData(Cloud.Dropbox)]
	[InlineData(Cloud.OneDrive)]
	[InlineData(Cloud.GoogleDrive)]
	public void GetCloudStorageProviderForPath_ReturnsConfiguredProvider_ForConfiguredRoot(Cloud provider)
	{
		using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();
		var root = CloudStorageRealTestEnvironment.GetConfiguredCloudTestRoot(provider, "cloud-providers", out var providerRoot);

		var cloudDir = root / "Workspace" / "Project";
		Directory.CreateDirectory(cloudDir.Path);

		Assert.Equal(provider, Os.GetCloudStorageProviderForPath(providerRoot));
		Assert.Equal(provider, Os.GetCloudStorageProviderForPath(cloudDir.Path));
	}

	[Theory]
	[InlineData(Cloud.Dropbox)]
	[InlineData(Cloud.OneDrive)]
	[InlineData(Cloud.GoogleDrive)]
	public void RaiFile_CloudFlag_DetectsFilesUnderConfiguredProvider(Cloud provider)
	{
		using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();
		var root = CloudStorageRealTestEnvironment.GetConfiguredCloudTestRoot(provider, "cloud-providers", out _);

		var cloudDir = root / "Workspace" / "Project";
		var localDir = Os.TempDir / "RAIkeep" / "cloud-providers" / "LocalFiles" / provider.ToString();
		cloudDir.mkdir();
		localDir.mkdir();

		var cloudFile = new RaiFile(cloudDir.Path + "sample.txt");
		var localFile = new RaiFile(localDir.Path + "sample.txt");

		Assert.True(cloudFile.Cloud);
		Assert.False(localFile.Cloud);
	}
}
