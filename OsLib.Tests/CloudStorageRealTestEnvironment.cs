using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OsLib.Tests;

internal static class CloudStorageRealTestEnvironment
{
	internal static IDisposable BeginConfiguredCloudResolution()
	{
		return Os.PushCloudRootResolutionMode(CloudRootResolutionMode.ConfiguredOnly);
	}

	internal static RaiPath GetConfiguredCloudTestRoot(
		CloudStorageType provider,
		string area,
		out string providerRoot,
		[CallerMemberName] string testName = "")
	{
		Os.ResetCloudStorageCache();
		var configPath = Os.GetDefaultConfigPath();
		if (!File.Exists(configPath))
			throw new FileNotFoundException($"Required cloud config file is missing: {configPath}. {Os.GetCloudStorageSetupGuidance()}", configPath);

		if (!Os.TryGetConfiguredCloudStorageRoot(provider, out providerRoot, refresh: true) || string.IsNullOrWhiteSpace(providerRoot))
			throw new InvalidOperationException($"Provider {provider} is not configured in {configPath}. {Os.GetCloudStorageSetupGuidance()}");

		providerRoot = new RaiPath(providerRoot).Path;
		if (!Directory.Exists(providerRoot))
			throw new DirectoryNotFoundException($"Configured provider root does not exist: {providerRoot}. {Os.GetCloudStorageSetupGuidance()}");

		return new RaiPath(providerRoot) / "RAIkeep" / SanitizeSegment(area) / SanitizeSegment(testName);
	}

	private static string SanitizeSegment(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "test";

		var invalid = Path.GetInvalidFileNameChars();
		var cleaned = new string(value
			.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch)
			.ToArray())
			.Trim('-');

		return string.IsNullOrWhiteSpace(cleaned) ? "test" : cleaned;
	}
}