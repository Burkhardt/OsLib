using Newtonsoft.Json.Linq;

namespace OsLib.Tests;

public class RemoteSshRealEnvironmentTests
{
	[Fact]
	public void Mzansi_RemoteConfig_Matches_Blueprint_When_Observer_Is_Configured()
	{
		dynamic remoteConfig = Os.RemoteConfigs["mzansi"];

		JObject remoteObject;
		JObject cloud;

		try
		{
			remoteObject = (JObject)remoteConfig;
			cloud = remoteObject["Cloud"] as JObject
				?? throw new InvalidOperationException("Cloud is missing or is not a JSON object.");
		}
		catch (Exception ex)
		{
			Assert.Fail($"Failed to access remote config properties: {ex.Message}");
			return;
		}

		Assert.Equal("~/temp/", remoteObject["TempDir"]?.ToString());
		Assert.Equal("~/backup/", remoteObject["LocalBackupDir"]?.ToString());

		Assert.Equal("/srv/ServerData/OneDriveData/", cloud["OneDrive"]?.ToString());
		Assert.Equal("/srv/ServerData/GDriveData/", cloud["GoogleDrive"]?.ToString());
		Assert.Equal("/srv/ServerData/DropboxData/", cloud["Dropbox"]?.ToString());
	}
}