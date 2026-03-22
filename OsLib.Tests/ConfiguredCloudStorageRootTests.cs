using System.IO;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class ConfiguredCloudStorageRootTests
{
	[Theory]
	[InlineData(CloudStorageType.Dropbox)]
	[InlineData(CloudStorageType.OneDrive)]
	[InlineData(CloudStorageType.GoogleDrive)]
	public void GetCloudStorageRoots_ReturnsConfiguredProviderRoots_AsCloudPaths(CloudStorageType provider)
	{
		using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();
		var root = CloudStorageRealTestEnvironment.GetConfiguredCloudTestRoot(provider, "configured-cloud-roots", out var providerRoot);

		var roots = Os.GetCloudStorageRoots(refresh: true);

		Assert.True(roots.ContainsKey(provider));
		Assert.Equal(new RaiPath(providerRoot).Path, roots[provider]);
		Assert.True(Directory.Exists(providerRoot));
		Assert.True(Os.IsCloudPath(providerRoot));
		Assert.True(Os.IsCloudPath(root.Path));
	}

	[Theory]
	[InlineData(CloudStorageType.Dropbox)]
	[InlineData(CloudStorageType.OneDrive)]
	[InlineData(CloudStorageType.GoogleDrive)]
	public void GetCloudStorageRoot_ReturnsConfiguredProviderRoot_WhenAvailable(CloudStorageType provider)
	{
		using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();
		var root = CloudStorageRealTestEnvironment.GetConfiguredCloudTestRoot(provider, "configured-cloud-roots", out var providerRoot);

		var resolvedRoot = Os.GetCloudStorageRoot(provider, refresh: true);

		Assert.Equal(new RaiPath(providerRoot).Path, resolvedRoot);
		Assert.True(Os.IsCloudPath(resolvedRoot));
		Assert.True(Os.IsCloudPath(root.Path));
	}
}