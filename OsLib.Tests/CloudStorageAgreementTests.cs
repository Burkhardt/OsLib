using System;
using System.Linq;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class CloudStorageAgreementTests
{
	[Fact]
	public void CloudStorageRoot_UsesDocumentedDefaultOrder_ForAvailableConfiguredProviders()
	{
		using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();
		var roots = Os.GetCloudStorageRoots(refresh: true);
		var configuredOrder = Os.TryLoadExistingConfig(out var config, refresh: true)
			? config.DefaultCloudOrder ?? Os.CreateDefaultCloudOrder().ToList()
			: Os.CreateDefaultCloudOrder().ToList();
		var expectedProvider = configuredOrder.FirstOrDefault(roots.ContainsKey);

		if (!roots.Any())
			Assert.Skip($"No configured provider roots are available. {Os.GetCloudStorageSetupGuidance()}");

		Assert.Equal(roots[expectedProvider], Os.CloudStorageRootDir.Path);
		Assert.True(Os.IsCloudPath(Os.CloudStorageRootDir.Path));
	}

	[Theory]
	[InlineData(CloudStorageType.Dropbox)]
	[InlineData(CloudStorageType.OneDrive)]
	[InlineData(CloudStorageType.GoogleDrive)]
	public void RaiFile_UsesConfiguredProviderRoot_ForCloudAwareFlag(CloudStorageType provider)
	{
		using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();
		var root = CloudStorageRealTestEnvironment.GetConfiguredCloudTestRoot(provider, "cloud-agreement", out _);

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
