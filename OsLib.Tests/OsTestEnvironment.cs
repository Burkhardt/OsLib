using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OsLib.Tests;

internal sealed class OsTestEnvironment : IDisposable
{
	private readonly Dictionary<string, string?> before = new();
	private readonly object? originalType;
	private readonly OsType? forcedType;

	internal OsTestEnvironment(RaiPath root, OsType? forcedType = null)
	{
		this.forcedType = forcedType;
		Root = root;
		Home = (root / "home").Path;
		AppData = (root / "app-data").Path;
		LocalAppData = (root / "local-app-data").Path;

		Directory.CreateDirectory(Home);
		Directory.CreateDirectory(AppData);
		Directory.CreateDirectory(LocalAppData);

		SetEnvironmentVariable("HOME", Home);
		SetEnvironmentVariable("USERPROFILE", Home);
		SetEnvironmentVariable("APPDATA", AppData);
		SetEnvironmentVariable("LOCALAPPDATA", LocalAppData);
		SetEnvironmentVariable("HOMEDRIVE", null);
		SetEnvironmentVariable("HOMEPATH", null);
		SetEnvironmentVariable("OneDrive", null);
		SetEnvironmentVariable("OneDriveCommercial", null);
		SetEnvironmentVariable("OneDriveConsumer", null);

		var osTypeField = typeof(Os).GetField("type", BindingFlags.Static | BindingFlags.NonPublic);
		originalType = osTypeField?.GetValue(null);

		ResetOsCaches();
		if (forcedType != null)
			osTypeField?.SetValue(null, forcedType);
	}

	internal RaiPath Root { get; }
	internal string Home { get; }
	internal string AppData { get; }
	internal string LocalAppData { get; }

	internal static RaiPath NewTestRoot(string area, string? suffix = null, [CallerMemberName] string testName = "")
	{
		var root = new RaiPath(Path.GetTempPath()) / "RAIkeep" / "oslib-tests" / SanitizeSegment(area) / SanitizeSegment(testName);
		if (!string.IsNullOrWhiteSpace(suffix))
			root /= SanitizeSegment(suffix);

		Cleanup(root);
		return root;
	}

	internal static void Cleanup(RaiPath root)
	{
		try
		{
			if (Directory.Exists(root.Path))
				new RaiFile(root.Path).rmdir(depth: 10, deleteFiles: true);
		}
		catch
		{
		}
	}

	internal static void ResetOsCaches()
	{
		var osType = typeof(Os);
		osType.GetField("type", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
		osType.GetField("dIRSEPERATOR", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, System.IO.Path.DirectorySeparatorChar.ToString());
		Os.resetDiagnosticsForTesting();
	}

	public void Dispose()
	{
		typeof(Os).GetField("type", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, originalType);
		foreach (var kvp in before)
			Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
		ResetOsCaches();
		Cleanup(Root);
	}

	private void SetEnvironmentVariable(string name, string? value)
	{
		before[name] = Environment.GetEnvironmentVariable(name);
		Environment.SetEnvironmentVariable(name, value);
	}

	private static string SanitizeSegment(string? value)
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