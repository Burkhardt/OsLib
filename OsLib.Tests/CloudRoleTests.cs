using Newtonsoft.Json.Linq;

namespace OsLib.Tests;

public class CloudRoleTests
{
	[Fact]
	public void ConfiguredRolesResolveToTheirCloudRoots()
	{
		if (!Os.IsConfigLoaded)
			Assert.Skip("Cloud role tests require the real RAIkeep configuration.");
		var config = Assert.IsType<JObject>((object)Os.Config);

		CloudRoles.Validate(config);
		var cloud = config["Cloud"] as JObject;
		var test = (string?)config["TestCloud"];
		if (string.IsNullOrWhiteSpace(test) || cloud?[test]?.Type != JTokenType.String)
			Assert.Skip("Cloud role tests require a configured TestCloud.");

		Assert.Equal(test, CloudRoles.Test(config));
		Assert.Equal((string?)cloud[test], CloudRoles.Root(config, test));
		Assert.Equal(CloudRoles.Root(config, test), CloudRoles.Root(config, test.ToLowerInvariant()));
		var live = (string?)config["LiveCloud"] ?? (string?)(config["DefaultCloudOrder"] as JArray)?.FirstOrDefault();
		if (!string.IsNullOrWhiteSpace(live))
			Assert.Equal(live, CloudRoles.Live(config));
	}
}
