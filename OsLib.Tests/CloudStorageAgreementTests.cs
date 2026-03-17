using System;
using System.Linq;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class CloudStorageAgreementTests
{
	[Fact]
	public void CloudStorageRoot_UsesDocumentedDefaultOrder_ForAvailableInstalledProviders()
	{
		var roots = Os.GetCloudStorageRoots(refresh: true);
		var configuredOrder = Os.LoadConfig(refresh: true).DefaultCloudOrder ?? Os.CreateDefaultCloudOrder().ToList();
		var expectedProvider = configuredOrder.FirstOrDefault(roots.ContainsKey);

		if (!roots.Any())
			Assert.Skip($"No installed provider roots are available. {Os.GetCloudStorageSetupGuidance()}");

		Assert.Equal(roots[expectedProvider], Os.CloudStorageRoot);
		Assert.True(Os.IsCloudPath(Os.CloudStorageRoot));
	}

	[Theory]
	[InlineData(CloudStorageType.Dropbox)]
	[InlineData(CloudStorageType.OneDrive)]
	[InlineData(CloudStorageType.GoogleDrive)]
	[InlineData(CloudStorageType.ICloud)]
	public void RaiFile_UsesInstalledProviderRoot_ForCloudAwareFlag(CloudStorageType provider)
	{
		if (!CloudStorageRealTestEnvironment.TryGetCloudTestRoot(provider, "cloud-agreement", out var root, out _, out var reason))
			Assert.Skip($"Provider {provider}: {reason}. {Os.GetCloudStorageSetupGuidance()}");

		var projectDir = root / "Mzansi" / "JsonPit";
		var metadataDir = root / ".dropbox" / "cache";
		Directory.CreateDirectory(projectDir.Path);
		Directory.CreateDirectory(metadataDir.Path);

		var projectFile = new RaiFile(projectDir.Path + "project.json");
		var metadataFile = new RaiFile(metadataDir.Path + "state.db");

		Assert.True(projectFile.Cloud);
		Assert.False(metadataFile.Cloud);
	}
}
