using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OsLib.Tests;

internal static class CloudStorageRealTestEnvironment
{
	internal static bool TryGetCloudTestRoot(
		CloudStorageType provider,
		string area,
		out RaiPath root,
		out string providerRoot,
		out string reason,
		[CallerMemberName] string testName = "")
	{
		Os.ResetCloudStorageCache();
		providerRoot = Os.GetCloudStorageRoot(provider, refresh: true);
		if (string.IsNullOrWhiteSpace(providerRoot))
		{
			root = new RaiPath(Os.TempDir) / "RAIkeep" / "missing-cloud-root";
			reason = "provider root is not configured or not discoverable on this machine";
			return false;
		}

		providerRoot = new RaiPath(providerRoot).Path;
		root = new RaiPath(providerRoot) / "RAIkeep" / SanitizeSegment(area) / SanitizeSegment(testName);
		if (!Directory.Exists(providerRoot))
		{
			reason = $"provider root does not exist: {providerRoot}";
			return false;
		}

		reason = string.Empty;
		return true;
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