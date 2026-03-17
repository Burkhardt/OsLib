using System.IO;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class CloudStorageDiscoveryTests
{
	[Theory]
	[InlineData(CloudStorageType.Dropbox)]
	[InlineData(CloudStorageType.OneDrive)]
	[InlineData(CloudStorageType.GoogleDrive)]
	[InlineData(CloudStorageType.ICloud)]
	public void GetCloudStorageRoots_ReturnsAvailableProviderRoots_AsCloudPaths(CloudStorageType provider)
	{
		if (!CloudStorageRealTestEnvironment.TryGetCloudTestRoot(provider, "cloud-discovery", out var root, out var providerRoot, out var reason))
			Assert.Skip($"Provider {provider}: {reason}. {Os.GetCloudStorageSetupGuidance()}");

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
	[InlineData(CloudStorageType.ICloud)]
	public void GetCloudStorageRoot_ReturnsInstalledProviderRoot_WhenAvailable(CloudStorageType provider)
	{
		if (!CloudStorageRealTestEnvironment.TryGetCloudTestRoot(provider, "cloud-discovery", out var root, out var providerRoot, out var reason))
			Assert.Skip($"Provider {provider}: {reason}. {Os.GetCloudStorageSetupGuidance()}");

		var resolvedRoot = Os.GetCloudStorageRoot(provider, refresh: true);

		Assert.Equal(new RaiPath(providerRoot).Path, resolvedRoot);
		Assert.True(Os.IsCloudPath(resolvedRoot));
		Assert.True(Os.IsCloudPath(root.Path));
	}
}
