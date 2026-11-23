using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OsLib.Tests;

internal static class OsTestEnvironment
{
	internal static RaiPath NewTestRoot(string area, string? suffix = null, [CallerMemberName] string testName = "")
	{
		var root = Os.SystemTempDir / "RAIkeep" / "oslib-tests" / SanitizeSegment(area) / SanitizeSegment(testName);
		if (!string.IsNullOrWhiteSpace(suffix))
			root /= SanitizeSegment(suffix);

		Cleanup(root);
		return root;
	}

	internal static void Cleanup(RaiPath root)
	{
		try
		{
			root.rmdir(depth: 10, deleteFiles: true);
		}
		catch
		{
		}
	}

	internal static void ResetOsCaches()
	{
		var osType = typeof(Os);
		osType.GetField("type", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
		Os.resetDiagnosticsForTesting();
	}

	private static string SanitizeSegment(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "test";

		var invalid = RaiPath.InvalidFileNameChars;
		var cleaned = new string(value
			.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch)
			.ToArray())
			.Trim('-');

		return string.IsNullOrWhiteSpace(cleaned) ? "test" : cleaned;
	}
}