using System.IO;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class ConfiguredCloudStorageRootTests
{
	[Theory]
	[InlineData(Cloud.Dropbox)]
	[InlineData(Cloud.OneDrive)]
	[InlineData(Cloud.GoogleDrive)]
	public void GetCloudStorageRoots_ReturnsConfiguredProviderRoots_AsCloudPaths(Cloud provider)
	{
		using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();
		var root = CloudStorageRealTestEnvironment.GetConfiguredCloudTestRoot(provider, "configured-cloud-roots", out var providerRoot);

		var resolvedRoot = Os.GetCloudStorageRoot(provider);

		Assert.Equal(new RaiPath(providerRoot).Path, resolvedRoot.Path);
		Assert.True(Directory.Exists(providerRoot));
		Assert.True(Os.IsCloudPath(providerRoot));
		Assert.True(Os.IsCloudPath(root.Path));
	}

	[Theory]
	[InlineData(Cloud.Dropbox)]
	[InlineData(Cloud.OneDrive)]
	[InlineData(Cloud.GoogleDrive)]
	public void GetCloudStorageRoot_ReturnsConfiguredProviderRoot_WhenAvailable(Cloud provider)
	{
		using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();
		var root = CloudStorageRealTestEnvironment.GetConfiguredCloudTestRoot(provider, "configured-cloud-roots", out var providerRoot);

		var resolvedRoot = Os.GetCloudStorageRoot(provider);

		Assert.Equal(new RaiPath(providerRoot).Path, resolvedRoot.Path);
		Assert.True(Os.IsCloudPath(resolvedRoot.Path));
		Assert.True(Os.IsCloudPath(root.Path));
	}
}