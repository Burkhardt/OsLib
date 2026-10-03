using Newtonsoft.Json.Linq;
namespace OsLib.Tests;

public sealed class ConfiguredCloudPathsTests
{
	[Fact]
	public void AllAmafuAccountLabelsAreRoots_IndependentOfDefaultOrder()
	{
		var home = Path.Combine(Path.GetTempPath(), "cloud-contract-fixture");
		var config = JObject.Parse("""
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
			value => Path.GetFullPath(value.Replace("~/", home + Path.DirectorySeparatorChar)), StringComparer.Ordinal);
		foreach (var name in new[] { "rainer", "yebo", "personal", "business", "custom" })
		{
			Assert.True(ConfiguredCloudPaths.Contains(roots, Path.Combine(home, name), StringComparison.Ordinal));
			Assert.True(ConfiguredCloudPaths.Contains(roots, Path.Combine(home, name, "Pits", "future.pit"), StringComparison.Ordinal));
		}
		Assert.False(ConfiguredCloudPaths.Contains(roots, Path.Combine(home, "rainer-neighbor", "Pits"), StringComparison.Ordinal));
		Assert.False(ConfiguredCloudPaths.Contains(roots, Path.Combine(home, "wrong"), StringComparison.Ordinal));
	}

	[Fact]
	public void ShortcutAndItsNewDescendantsHaveCloudClassification()
	{
		if (OperatingSystem.IsWindows()) Assert.Skip("Symlinks require Windows developer mode.");
		var home = Path.Combine(Path.GetTempPath(), "oslib-cloud-links-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(home);
		try
		{
			var root = Path.Combine(home, "real-root");
			Directory.CreateDirectory(root);
			var shortcuts = Directory.CreateDirectory(Path.Combine(home, ".CloudStorage")).FullName;
			var link = Path.Combine(shortcuts, "GoogleDriveRainer");
			Directory.CreateSymbolicLink(link, root);
			foreach (var configured in new[] { root, link })
			{
				var config = new JObject { ["Cloud"] = new JObject { ["GoogleDriveRainer"] = configured } };
				var roots = ConfiguredCloudPaths.Read(config, Path.GetFullPath, StringComparer.Ordinal);
				Assert.True(ConfiguredCloudPaths.Contains(roots, Path.Combine(link, "not-created", "Item.pit"), StringComparison.Ordinal));
				Assert.True(ConfiguredCloudPaths.Contains(roots, Path.Combine(root, "Item.pit"), StringComparison.Ordinal));
				Assert.False(ConfiguredCloudPaths.Contains(roots, Path.Combine(home, "real-root-other"), StringComparison.Ordinal));
			}
		}
		finally { Directory.Delete(home, recursive: true); }
	}
}
