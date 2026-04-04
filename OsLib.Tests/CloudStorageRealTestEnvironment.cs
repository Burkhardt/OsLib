using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace OsLib.Tests;

internal static class CloudStorageRealTestEnvironment
{
	internal static IDisposable BeginConfiguredCloudResolution()
	{
		_ = Os.Config;
		return new StringReader(string.Empty);
	}

	internal static RaiPath GetConfiguredCloudTestRoot(
		Cloud provider,
		string area,
		out string providerRoot,
		[CallerMemberName] string testName = "")
	{
		var configPath = Os.ConfigFileFullName;
		if (!File.Exists(configPath))
			Assert.Skip($"Required cloud config file is missing: {configPath}. {Os.GetCloudStorageSetupGuidance()}");

		dynamic config = Os.Config;
		var configuredRoot = Os.GetCloudStorageRoot(provider);
		providerRoot = configuredRoot?.Path ?? string.Empty;

		if (string.IsNullOrWhiteSpace(providerRoot))
			Assert.Skip($"Provider {provider} is not configured in {configPath}. {Os.GetCloudStorageSetupGuidance()}");

		providerRoot = new RaiPath(providerRoot).Path;
		if (!Directory.Exists(providerRoot))
			Assert.Skip($"Configured provider root does not exist: {providerRoot}. {Os.GetCloudStorageSetupGuidance()}");

		if (!Os.IsCloudPath(providerRoot))
			Assert.Skip($"Configured provider root is not recognized as a cloud path: {providerRoot}. {Os.GetCloudStorageSetupGuidance()}");

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