using Newtonsoft.Json.Linq;
namespace OsLib.Tests;

public sealed class ConfiguredCloudPathsTests
{
	[Fact]
	public void AllAmafuAccountLabelsAreRoots_IndependentOfDefaultOrder()
	{
		var home = Os.TempDir / "cloud-contract-fixture";
		var config = TestJson.ParseObject("""
		{
		  "Cloud": {
		    "GoogleDriveRainer": "~/rainer/", "GoogleDriveYebo": "~/yebo/",
		    "OneDrivePersonal": "~/personal/", "OneDriveAfricaStage": "~/business/",
		    "GoogleDrive": "~/rainer/", "CustomCloudName": "~/custom/",
		    "Empty": "", "Missing": null, "NotAPath": {"path":"~/wrong/"}
		  },
		  "DefaultCloudOrder": ["GoogleDriveRainer"]
		}
		""");
		var roots = ConfiguredCloudPaths.Read(config,
			value => new RaiPath(value.Replace("~/", home.FullPath)).FullPath, StringComparer.Ordinal);
		foreach (var name in new[] { "rainer", "yebo", "personal", "business", "custom" })
		{
			Assert.True(ConfiguredCloudPaths.Contains(roots, (home / name).FullPath, StringComparison.Ordinal));
			Assert.True(ConfiguredCloudPaths.Contains(roots, new RaiFile(home / name / "Pits", "future.pit").FullName, StringComparison.Ordinal));
		}
		Assert.False(ConfiguredCloudPaths.Contains(roots, (home / "rainer-neighbor" / "Pits").FullPath, StringComparison.Ordinal));
		Assert.False(ConfiguredCloudPaths.Contains(roots, (home / "wrong").FullPath, StringComparison.Ordinal));
	}

	[Fact]
	public void ShortcutAndItsNewDescendantsHaveCloudClassification()
	{
		if (OperatingSystem.IsWindows()) Assert.Skip("Symlinks require Windows developer mode.");
		var home = (Os.TempDir / ("oslib-cloud-links-" + Guid.NewGuid().ToString("N"))).mkdir();
		try
		{
			var root = (home / "real-root").mkdir();
			var shortcuts = (home / ".CloudStorage").mkdir();
			var link = shortcuts / "GoogleDriveRainer";
#pragma warning disable RAI001 // Create a physical OS link to test recognition of two names for the same cloud directory.
			Directory.CreateSymbolicLink(link.FullPath.TrimEnd(Os.DIR[0]), root.FullPath);
#pragma warning restore RAI001
			foreach (var configured in new[] { root, link })
			{
				var config = new JObject { ["Cloud"] = new JObject { ["GoogleDriveRainer"] = configured.FullPath } };
				var roots = ConfiguredCloudPaths.Read(config, value => new RaiPath(value).FullPath, StringComparer.Ordinal);
				Assert.True(ConfiguredCloudPaths.Contains(roots, new RaiFile(link / "not-created", "Item.pit").FullName, StringComparison.Ordinal));
				Assert.True(ConfiguredCloudPaths.Contains(roots, new RaiFile(root, "Item.pit").FullName, StringComparison.Ordinal));
				Assert.False(ConfiguredCloudPaths.Contains(roots, (home / "real-root-other").FullPath, StringComparison.Ordinal));
			}
		}
		finally { home.rmdir(deleteFiles: true); }
	}
}
