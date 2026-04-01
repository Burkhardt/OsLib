using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

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
		Home = (root / "home").Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		AppData = (root / "app-data").Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		LocalAppData = (root / "local-app-data").Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		ConfigPath = new RaiFile(new RaiPath(Home) / ".config" / "RAIkeep", "osconfig", "json").FullName;

		new RaiPath(Home).mkdir();
		new RaiPath(AppData).mkdir();
		new RaiPath(LocalAppData).mkdir();

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
	internal string ConfigPath { get; }

	internal void WriteConfig(
		string? dropbox = null,
		string? oneDrive = null,
		string? googleDrive = null,
		string? homeDir = null,
		string? tempDir = null,
		string? localBackupDir = null,
		IEnumerable<Cloud>? defaultCloudOrder = null)
	{
		var cloud = new JObject
		{
			["dropbox"] = NormalizeForJson(dropbox),
			["onedrive"] = NormalizeForJson(oneDrive),
			["googledrive"] = NormalizeForJson(googleDrive)
		};

		var json = new JObject
		{
			["cloud"] = cloud
		};

		if (!string.IsNullOrWhiteSpace(homeDir))
			json["homeDir"] = new RaiPath(homeDir).Path;
		if (!string.IsNullOrWhiteSpace(tempDir))
			json["tempDir"] = new RaiPath(tempDir).Path;
		if (!string.IsNullOrWhiteSpace(localBackupDir))
			json["localBackupDir"] = new RaiPath(localBackupDir).Path;
		if (defaultCloudOrder != null)
			json["defaultCloudOrder"] = new JArray(defaultCloudOrder.Select(x => x.ToString()));

		var configFile = new RaiFile(ConfigPath);
		RaiFile.mkdir(configFile.Path);
		var textFile = new TextFile(configFile.FullName)
		{
			Lines = json.ToString().Replace("\r\n", "\n").Split('\n').ToList()
		};
		textFile.Changed = true;
		textFile.Save();
		Os.LoadConfig();
	}

	internal void DeleteConfig()
	{
		var configPath = ConfigPath;
		if (File.Exists(configPath))
			File.Delete(configPath);
		ResetOsCaches();
	}

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
		osType.GetField("userHomeDir", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
		osType.GetField("appRootDir", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
		osType.GetField("type", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
		osType.GetField("dIRSEPERATOR", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
		osType.GetField("tempDir", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
		osType.GetField("localBackupDir", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
		osType.GetField("config", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
		osType.GetField("remoteTestConfig", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
		//Os.ResetCloudStorageCache();
		Os.ResetDiagnosticsForTesting();
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

	private static string NormalizeForJson(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? string.Empty : new RaiPath(value).Path;
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