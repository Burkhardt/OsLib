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

		var configuredOrder = new List<string>();
		try
		{
			foreach (var providerEntry in Os.Config.DefaultCloudOrder)
			{
				var providerName = providerEntry?.ToString();
				if (string.IsNullOrWhiteSpace(providerName))
					continue;
				configuredOrder.Add(providerName);
			}
		}
		catch
		{
		}

		if (!configuredOrder.Any())
			configuredOrder = new[] { nameof(Cloud.OneDrive), nameof(Cloud.Dropbox), nameof(Cloud.GoogleDrive) }.ToList();

		var expectedRoot = string.Empty;
		foreach (var providerName in configuredOrder)
		{
			if (!Enum.TryParse(providerName, ignoreCase: true, out Cloud provider))
				continue;

			var candidate = Os.GetCloudStorageRoot(provider)?.Path ?? string.Empty;
			if (string.IsNullOrWhiteSpace(candidate) || !Directory.Exists(candidate))
				continue;

			expectedRoot = candidate;
			break;
		}

		if (string.IsNullOrWhiteSpace(expectedRoot))
			Assert.Skip($"No configured provider roots are available. {Os.GetCloudStorageSetupGuidance()}");

		Assert.Equal(expectedRoot, Os.CloudStorageRootDir.Path);
		Assert.True(Os.IsCloudPath(Os.CloudStorageRootDir.Path));
	}

	[Theory]
	[InlineData(Cloud.Dropbox)]
	[InlineData(Cloud.OneDrive)]
	[InlineData(Cloud.GoogleDrive)]
	public void RaiFile_UsesConfiguredProviderRoot_ForCloudAwareFlag(Cloud provider)
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
