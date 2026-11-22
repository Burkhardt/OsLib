using Newtonsoft.Json.Linq;

namespace OsLib.Tests;

public class CloudRoleTests
{
	[Fact]
	public void MissingRolesPreserveOrderAndNullableTestCloud()
	{
		var config = TestJson.ParseObject("{DefaultCloudOrder:['OneDrive','Dropbox'],Cloud:{OneDrive:'/live/',Dropbox:'/test/'}}");
		CloudRoles.Validate(config);
		Assert.Equal("OneDrive", CloudRoles.Live(config));
		Assert.Null(CloudRoles.Test(config));
		config["LiveCloud"] = "Dropbox";
		config["TestCloud"] = "OneDrive";
		CloudRoles.Validate(config);
		Assert.Equal("Dropbox", CloudRoles.Live(config));
		Assert.Equal("OneDrive", CloudRoles.Test(config));
		Assert.Equal("/test/", CloudRoles.Root(config, "Dropbox"));
	}
	[Theory]
	[InlineData("LiveCloud")]
	[InlineData("TestCloud")]
	public void InvalidRolesNameKeyAndAvailableProviders(string key)
	{
		var config = TestJson.ParseObject("{Cloud:{OneDrive:'/live/'}}");
		config[key] = "/not/a/provider/";
		var error = Assert.Throws<InvalidOperationException>(() => CloudRoles.Validate(config));
		Assert.Contains(key, error.Message);
		Assert.Contains("OneDrive", error.Message);
		Assert.Contains("Cloud.Missing", Assert.Throws<InvalidOperationException>(() => CloudRoles.Root(config, "Missing")).Message);
	}
}
