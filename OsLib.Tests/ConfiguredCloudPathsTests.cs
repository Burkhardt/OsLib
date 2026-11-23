using Newtonsoft.Json.Linq;

namespace OsLib.Tests;

public sealed class ConfiguredCloudPathsTests
{
	[Fact]
	public void ConfiguredTestCloudAndItsDescendantsHaveCloudClassification()
	{
		if (!Os.IsConfigLoaded)
			Assert.Skip("Cloud path tests require the real RAIkeep configuration.");
		var config = Assert.IsType<JObject>((object)Os.Config);

		var provider = (string?)config["TestCloud"];
		if (string.IsNullOrWhiteSpace(provider) || config["Cloud"]?[provider]?.Type != JTokenType.String)
			Assert.Skip("Cloud path tests require a configured TestCloud.");

		var root = Os.CloudRoot(provider);
		if (!root.Exists())
			Assert.Skip($"Configured TestCloud {provider} is unavailable.");

		Assert.True(Os.IsCloudPath(root.FullPath));
		Assert.True(Os.IsCloudPath(new RaiFile(root / "not-created", "Item.pit").FullName));
	}
}
