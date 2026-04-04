using System.IO;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class CloudStoragePathMechanicsTests
{
	[Theory]
	[InlineData(Cloud.Dropbox, "DropboxRoot")]
	[InlineData(Cloud.OneDrive, "OneDriveRoot")]
	[InlineData(Cloud.GoogleDrive, "GoogleDriveRoot")]
	public void GetCloudStorageRoot_UsesConfiguredRoot_ForEachProvider(Cloud provider, string dirName)
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-providers");
		using var env = new OsTestEnvironment(root);

		var providerRoot = (root / dirName).Path;
		new RaiPath(providerRoot).mkdir();
		WriteProviderConfig(env, provider, providerRoot);

		Assert.Equal(new RaiPath(providerRoot).Path, Os.GetCloudStorageRoot(provider).Path);
	}

	[Theory]
	[InlineData(Cloud.Dropbox, "DropboxRoot")]
	[InlineData(Cloud.OneDrive, "OneDriveRoot")]
	[InlineData(Cloud.GoogleDrive, "GoogleDriveRoot")]
	public void GetCloudStorageProviderForPath_ReturnsConfiguredProvider_ForEachProvider(Cloud provider, string dirName)
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-providers");
		using var env = new OsTestEnvironment(root);

		var providerRoot = new RaiPath((root / dirName).Path);
		var cloudDir = providerRoot / "Workspace" / "Project";
		var localDir = root / "LocalFiles";
		providerRoot.mkdir();
		cloudDir.mkdir();
		localDir.mkdir();
		WriteProviderConfig(env, provider, providerRoot.Path);

		Assert.Equal(provider, Os.GetCloudStorageProviderForPath(providerRoot));
		Assert.Equal(provider, Os.GetCloudStorageProviderForPath(cloudDir));
	}

	[Theory]
	[InlineData(Cloud.Dropbox, "DropboxRoot")]
	[InlineData(Cloud.OneDrive, "OneDriveRoot")]
	[InlineData(Cloud.GoogleDrive, "GoogleDriveRoot")]
	public void RaiFile_CloudFlag_DetectsFilesUnderConfiguredProvider(Cloud provider, string dirName)
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-providers");
		using var env = new OsTestEnvironment(root);

		var providerRoot = new RaiPath((root / dirName).Path);
		var cloudDir = providerRoot / "Workspace" / "Project";
		var localDir = root / "LocalFiles";
		providerRoot.mkdir();
		cloudDir.mkdir();
		localDir.mkdir();
		WriteProviderConfig(env, provider, providerRoot.Path);

		var cloudFile = new RaiFile(cloudDir.Path + "sample.txt");
		var localFile = new RaiFile(localDir.Path + "sample.txt");

		Assert.Equal(new RaiPath(providerRoot.Path).Path, Os.GetCloudStorageRoot(provider).Path);
		Assert.True(cloudFile.Cloud);
		Assert.False(localFile.Cloud);
	}

	private static void WriteProviderConfig(OsTestEnvironment env, Cloud provider, string providerRoot)
	{
		switch (provider)
		{
			case Cloud.Dropbox:
				env.WriteConfig(dropbox: providerRoot);
				break;
			case Cloud.OneDrive:
				env.WriteConfig(oneDrive: providerRoot);
				break;
			case Cloud.GoogleDrive:
				env.WriteConfig(googleDrive: providerRoot);
				break;
		}
	}
}