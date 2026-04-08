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
		Home = (root / "home").Path;
		AppData = (root / "app-data").Path;
		LocalAppData = (root / "local-app-data").Path;
		ConfigPath = new RaiFile(new RaiPath(Home) / ".config" / "RAIkeep", "osconfig", "json5").FullName;

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
	internal string ConfigPath { get; }

	internal void WriteConfig(
		string? dropbox = null,
		string? oneDrive = null,
		string? googleDrive = null,
		string? tempDir = null,
		string? localBackupDir = null,
		IEnumerable<Cloud>? defaultCloudOrder = null,
		Dictionary<string, string>? observers = null,
		bool loadConfig = false)
	{
		var cloud = new JObject
		{
			["Dropbox"] = NormalizeForJson(dropbox),
			["OneDrive"] = NormalizeForJson(oneDrive),
			["GoogleDrive"] = NormalizeForJson(googleDrive)
		};

		var json = new JObject
		{
			["Cloud"] = cloud
		};

		if (!string.IsNullOrWhiteSpace(tempDir))
			json["TempDir"] = NormalizeDirectoryValue(tempDir);
		if (!string.IsNullOrWhiteSpace(localBackupDir))
			json["LocalBackupDir"] = NormalizeDirectoryValue(localBackupDir);
		if (defaultCloudOrder != null)
			json["DefaultCloudOrder"] = new JArray(defaultCloudOrder.Select(x => x.ToString()));

		if (observers != null && observers.Count > 0)
		{
			var observersJson = new JArray();
			foreach (var kvp in observers)
				observersJson.Add(new JObject
				{
					["Name"] = kvp.Key,
					["SshTarget"] = kvp.Value
				});
			json["Observers"] = observersJson;
		}

		PersistTestConfigFile(json, loadConfig);
	}

	private void PersistTestConfigFile(JObject json, bool loadConfig)
	{

		var configFile = new RaiFile(ConfigPath);
		Directory.CreateDirectory(configFile.Path.Path);
		var textFile = new TextFile(configFile.FullName)
		{
			Lines = json.ToString().Replace("\r\n", "\n").Split('\n').ToList()
		};
		textFile.Changed = true;
		textFile.Save();
		if (loadConfig)
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
		osType.GetField("dIRSEPERATOR", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, System.IO.Path.DirectorySeparatorChar.ToString());
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
		return string.IsNullOrWhiteSpace(value) ? string.Empty : NormalizeDirectoryValue(value);
	}

	private static string NormalizeDirectoryValue(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		var normalized = Os.NormSeperator(value);
		if (!normalized.EndsWith(Os.DIR, StringComparison.Ordinal))
			normalized += Os.DIR;

		return new RaiPath(normalized).Path;
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