using System.IO;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class CloudStoragePathMechanicsTests
{
	[Theory]
	[InlineData(CloudStorageType.Dropbox, "DropboxRoot")]
	[InlineData(CloudStorageType.OneDrive, "OneDriveRoot")]
	[InlineData(CloudStorageType.GoogleDrive, "GoogleDriveRoot")]
	[InlineData(CloudStorageType.ICloud, "ICloudRoot")]
	public void GetCloudStorageRoot_UsesConfiguredRoot_ForEachProvider(CloudStorageType provider, string dirName)
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-providers");
		using var env = new OsTestEnvironment(root);

		var providerRoot = (root / dirName).Path;
		new RaiPath(providerRoot).mkdir();
		WriteProviderConfig(env, provider, providerRoot);

		Assert.Equal(new RaiPath(providerRoot).Path, Os.GetCloudStorageRoot(provider, refresh: true));
	}

	[Theory]
	[InlineData(CloudStorageType.Dropbox, "DropboxRoot")]
	[InlineData(CloudStorageType.OneDrive, "OneDriveRoot")]
	[InlineData(CloudStorageType.GoogleDrive, "GoogleDriveRoot")]
	[InlineData(CloudStorageType.ICloud, "ICloudRoot")]
	public void GetCloudStorageProviderForPath_ReturnsConfiguredProvider_ForEachProvider(CloudStorageType provider, string dirName)
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

		Assert.Equal(provider, Os.GetCloudStorageProviderForPath(providerRoot.Path));
		Assert.Equal(provider, Os.GetCloudStorageProviderForPath(cloudDir.Path));
		Assert.Null(Os.GetCloudStorageProviderForPath(localDir.Path));
	}

	[Theory]
	[InlineData(CloudStorageType.Dropbox, "DropboxRoot")]
	[InlineData(CloudStorageType.OneDrive, "OneDriveRoot")]
	[InlineData(CloudStorageType.GoogleDrive, "GoogleDriveRoot")]
	[InlineData(CloudStorageType.ICloud, "ICloudRoot")]
	public void RaiFile_CloudFlag_DetectsFilesUnderConfiguredProvider(CloudStorageType provider, string dirName)
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

		Assert.Equal(new RaiPath(providerRoot.Path).Path, Os.GetCloudStorageRoot(provider, refresh: true));
		Assert.True(cloudFile.Cloud);
		Assert.False(localFile.Cloud);
	}

	private static void WriteProviderConfig(OsTestEnvironment env, CloudStorageType provider, string providerRoot)
	{
		switch (provider)
		{
			case CloudStorageType.Dropbox:
				env.WriteConfig(dropbox: providerRoot);
				break;
			case CloudStorageType.OneDrive:
				env.WriteConfig(oneDrive: providerRoot);
				break;
			case CloudStorageType.GoogleDrive:
				env.WriteConfig(googleDrive: providerRoot);
				break;
			case CloudStorageType.ICloud:
				env.WriteConfig(iCloud: providerRoot);
				break;
		}
	}
}