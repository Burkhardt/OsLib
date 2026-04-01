using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OsLib
{
	public enum Cloud { Dropbox, OneDrive, GoogleDrive };
	public static partial class Os
	{
		public static string[] CloudProviders => Enum.GetNames(typeof(Cloud));

		private static dynamic config;
		private static int configLoadDepth;
		private static bool IsConfigLoading => configLoadDepth > 0;
		public static dynamic Config
		{
			get
			{
				if (config == null)
				{
					config = LoadConfig();
				}
				return config;
			}
		}

		public static dynamic LoadConfig()
		{
			List<string> defaultCloudOrder = new List<string>();
			string oneDrive = string.Empty;
			string dropbox = string.Empty;
			string googleDrive = string.Empty;
			configLoadDepth++;
			try
			{
				// read it from disk
				try
				{
					var configPath = GetDefaultConfigPath();
					if (!File.Exists(configPath))
					{
						config = null;
						return config;
					}

					var json = File.ReadAllText(configPath);
					if (string.IsNullOrWhiteSpace(json))
					{
						config = null;
						return config;
					}

					var parsedObject = JsonConvert.DeserializeObject<JObject>(json);
					if (parsedObject == null)
					{
						config = null;
						return config;
					}

					var parsed = CanonicalizeConfig(parsedObject);
					config = parsed;

					var configuredHomeDir = ReadPathValue(parsed, "HomeDir", "homeDir");
					if (!string.IsNullOrWhiteSpace(configuredHomeDir))
						LogWarningOnce<OsDiagnosticsLogScope>("config:deprecated-homeDir", "Ignoring deprecated homeDir/HomeDir config value for intrinsic user home resolution.");

					var configuredTempDir = ReadPathValue(parsed, "TempDir", "tempDir");
					tempDir = string.IsNullOrWhiteSpace(configuredTempDir) ? null : new RaiPath(configuredTempDir);

					var configuredLocalBackupDir = ReadPathValue(parsed, "LocalBackupDir", "localBackupDir");
					localBackupDir = string.IsNullOrWhiteSpace(configuredLocalBackupDir) ? TempDir : new RaiPath(configuredLocalBackupDir);

					oneDrive = ReadCloudRootPath(parsed, Cloud.OneDrive);
					dropbox = ReadCloudRootPath(parsed, Cloud.Dropbox);
					googleDrive = ReadCloudRootPath(parsed, Cloud.GoogleDrive);
					defaultCloudOrder = ReadDefaultCloudOrder(parsed);
					var provider = defaultCloudOrder.FirstOrDefault();
					cloudStorageRootDir = provider switch
					{
						nameof(Cloud.GoogleDrive) => string.IsNullOrWhiteSpace(googleDrive) ? null : new RaiPath(googleDrive),
						nameof(Cloud.Dropbox) => string.IsNullOrWhiteSpace(dropbox) ? null : new RaiPath(dropbox),
						nameof(Cloud.OneDrive) => string.IsNullOrWhiteSpace(oneDrive) ? null : new RaiPath(oneDrive),
						_ => null
					};
				}
				catch (RuntimeBinderException ex)
				{
					// Missing property or unexpected shape in dynamic object
					Console.WriteLine("Config binding error: " + ex.Message);
				}
				catch (Exception ex)
				{
					// Parse/type conversion/etc.
					Console.WriteLine("Config read error: " + ex.Message);
				}
			}
			finally
			{
				configLoadDepth--;
			}
			return config;
		}

		private const string cloudDiscoveryGuidePath = "OsLib/CLOUD_STORAGE_DISCOVERY.md";
		public static bool IsCloudPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path) || IsConfigLoading)
				return false;
			try
			{
				var activeConfig = Config;
				if (activeConfig == null)
					return false;

				foreach (var provider in ReadDefaultCloudOrder(activeConfig))
				{
					switch (provider)
					{
						case nameof(Cloud.GoogleDrive):
							var googleRoot = ReadCloudRootPath(activeConfig, Cloud.GoogleDrive);
							if (!string.IsNullOrWhiteSpace(googleRoot) && path.StartsWith(googleRoot, StringComparison.OrdinalIgnoreCase))
								return true;
							break;
						case nameof(Cloud.Dropbox):
							var dropboxRoot = ReadCloudRootPath(activeConfig, Cloud.Dropbox);
							if (!string.IsNullOrWhiteSpace(dropboxRoot) && path.StartsWith(dropboxRoot, StringComparison.OrdinalIgnoreCase))
								return true;
							break;
						case nameof(Cloud.OneDrive):
							var oneDriveRoot = ReadCloudRootPath(activeConfig, Cloud.OneDrive);
							if (!string.IsNullOrWhiteSpace(oneDriveRoot) && path.StartsWith(oneDriveRoot, StringComparison.OrdinalIgnoreCase))
								return true;
							break;
					}
				}
			}
			catch (Exception ex) {
				LogError<OsDiagnosticsLogScope>(ex, "Error checking if path is a cloud path: {Path}", path);
			}
			return false;
		}
		public static string GetCloudDiscoveryReport(bool refresh = false)
		{
			if (refresh)
				config = null;
			var sb = new StringBuilder();
			sb.AppendLine("Discovered cloud storage roots:");
			try 
			{
				foreach (var provider in ReadDefaultCloudOrder(Config))
				{
					var providerName = provider?.ToString() ?? string.Empty;
					var parsed = Enum.TryParse(providerName, true, out Cloud providerEnum);
					var root = parsed ? ReadCloudRootPath(Config, providerEnum) : string.Empty;
					switch (provider)
					{
						case nameof(Cloud.GoogleDrive):
							sb.AppendLine($"- {provider}: {(string.IsNullOrWhiteSpace(root) ? "<not configured>" : root)}");
							break;
						case nameof(Cloud.Dropbox):
							sb.AppendLine($"- {provider}: {(string.IsNullOrWhiteSpace(root) ? "<not configured>" : root)}");
							break;
						case nameof(Cloud.OneDrive):
							sb.AppendLine($"- {provider}: {(string.IsNullOrWhiteSpace(root) ? "<not configured>" : root)}");
							break;
						default:
							sb.AppendLine($"- {provider}: <not found>");
							break;
					}
				}
			}
			catch (Exception ex)
			{
				LogError<OsDiagnosticsLogScope>(ex, "Error generating cloud discovery report");
			}
			return sb.ToString().TrimEnd();
		}
		public static string GetCloudConfigurationDiagnosticReport(bool refresh = false)
		{
			if (refresh)
				config = null;
			var sb = new StringBuilder();
			var order = ReadDefaultCloudOrder(Config);
			sb.AppendLine("Cloud configuration diagnostics:");
			sb.AppendLine($"- active config path: {defaultConfigFileLocation}");
			sb.AppendLine($"- userHomeDir: {UserHomeDir?.Path}");
			sb.AppendLine($"- appRootDir: {AppRootDir?.Path}");
			sb.AppendLine($"- tempDir: {TempDir?.Path}");
			sb.AppendLine($"- localBackupDir: {LocalBackupDir?.Path}");
			sb.AppendLine($"- configured default cloud order: {string.Join(", ", order)}");
			foreach (var provider in order)
			{
				sb.Append($"- {provider}: ");
				var providerName = provider?.ToString() ?? string.Empty;
				if (Enum.TryParse(providerName, true, out Cloud providerEnum))
					sb.AppendLine($" path: {ReadCloudRootPath(Config, providerEnum)}");
				else
					sb.AppendLine(" path: <invalid provider>");
			}
			return sb.ToString().TrimEnd() + GetCloudDiscoveryReport(refresh);
		}
		public static string GetCloudStorageSetupGuidance()
		{
			return "Configure Os.Config in " + GetDefaultConfigPath() + ". See " + cloudDiscoveryGuidePath;
		}
		public static Cloud GetCloudStorageProviderForPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				throw new ArgumentNullException(nameof(path));
			var normalizedPath = NormalizePathForComparison(path);
			if (string.IsNullOrWhiteSpace(normalizedPath))
				throw new ArgumentException("Invalid path", nameof(path));
			foreach (var provider in ReadDefaultCloudOrder(Config))
			{
				string root = provider switch
				{
					nameof(Cloud.GoogleDrive) => ReadCloudRootPath(Config, Cloud.GoogleDrive),
					nameof(Cloud.Dropbox) => ReadCloudRootPath(Config, Cloud.Dropbox),
					nameof(Cloud.OneDrive) => ReadCloudRootPath(Config, Cloud.OneDrive),
					_ => null
				};
				if (PathIsUnderCloudRoot(normalizedPath, root))
					return Enum.Parse<Cloud>(provider);
			}
			throw new InvalidOperationException($"Path '{path}' is not under any configured cloud storage root.");
		}
		public static RaiPath GetCloudStorageRoot(Cloud provider, bool refresh = false)
		{
			if (refresh)
				config = null;

			var root = ReadCloudRootPath(Config, provider);
			if (string.IsNullOrWhiteSpace(root))
				throw new InvalidOperationException($"Cloud storage provider '{provider}' is not configured.");

			return new RaiPath(root);
		}
		public static string GetDefaultConfigPath()
		{
			return NormalizeConfigPath(defaultConfigFileLocation);   // no DI here!!!!!
		}
		private static void InvalidateConfiguredPathCaches()
		{
			userHomeDir = null;
			appRootDir = null;
			tempDir = null;
			localBackupDir = null;
		}
		private static string NormalizeConfigPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return string.Empty;

			var expanded = ExpandUserHomePath(path);

			return Path.GetFullPath(expanded);
		}
		private static bool TryAddRoot(Dictionary<Cloud, string> roots, Cloud provider, string candidate)
		{
			if (roots.ContainsKey(provider))
				return false;

			var expanded = ExpandPath(candidate);
			if (string.IsNullOrWhiteSpace(expanded) || !Directory.Exists(expanded))
				return false;

			roots[provider] = new RaiPath(expanded).Path;
			return true;
		}		
		private static string ExpandPath(string candidate)
		{
			if (string.IsNullOrWhiteSpace(candidate))
				return null;
			return ExpandUserHomePath(candidate);
		}
		private static string ExpandUserHomePath(string path)
		{
			var expanded = path.Trim();
			if (!expanded.StartsWith("~/", StringComparison.Ordinal))
				return expanded;

			var home = ResolveSystemHomeDir();
			if (string.IsNullOrWhiteSpace(home))
				return expanded;

			return home.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + DIRSEPERATOR + expanded.Substring(2);
		}
		private static string NormalizePathForComparison(string value)
		{
			var expanded = ExpandPath(value);
			if (string.IsNullOrWhiteSpace(expanded))
				return null;

			return NormSeperator(expanded).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		}
		private static bool PathIsUnderCloudRoot(string normalizedCandidate, string root)
		{
			var normalizedRoot = NormalizePathForComparison(root);
			if (string.IsNullOrWhiteSpace(normalizedRoot))
				return false;
			return normalizedCandidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
				normalizedCandidate.StartsWith(normalizedRoot + DIRSEPERATOR, StringComparison.OrdinalIgnoreCase);
		}
		private static bool IsDropboxMetadataPath(string normalizedPath)
		{
			var marker = DIRSEPERATOR + ".dropbox";
			return normalizedPath.EndsWith(marker, StringComparison.OrdinalIgnoreCase) ||
				normalizedPath.Contains(marker + DIRSEPERATOR, StringComparison.OrdinalIgnoreCase);
		}
		private static IEnumerable<string> SafeEnumerateDirectories(string path, string pattern)
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
					return Directory.EnumerateDirectories(path, pattern);
			}
			catch (Exception ex)
			{
				LogError<OsDiagnosticsLogScope>(ex, "Failed to enumerate directories under {DirectoryPath} with pattern {DirectoryPattern}", path ?? string.Empty, pattern ?? string.Empty);
			}
			return Array.Empty<string>();
		}
		private sealed class DisposableScope : IDisposable
		{
			private readonly Action onDispose;
			private bool disposed;
			internal DisposableScope(Action onDispose)
			{
				this.onDispose = onDispose;
			}
			public void Dispose()
			{
				if (disposed)
					return;
				disposed = true;
				onDispose();
			}
		}

		private static JObject CanonicalizeConfig(JObject source)
		{
			if (source == null)
				return new JObject();

			var canonical = new JObject();

			var tempDir = source.GetValue("TempDir", StringComparison.OrdinalIgnoreCase)
				?? source.GetValue("tempDir", StringComparison.OrdinalIgnoreCase);
			if (tempDir != null)
				canonical["TempDir"] = tempDir;

			var localBackupDir = source.GetValue("LocalBackupDir", StringComparison.OrdinalIgnoreCase)
				?? source.GetValue("localBackupDir", StringComparison.OrdinalIgnoreCase);
			if (localBackupDir != null)
				canonical["LocalBackupDir"] = localBackupDir;

			var order = source.GetValue("DefaultCloudOrder", StringComparison.OrdinalIgnoreCase)
				?? source.GetValue("defaultCloudOrder", StringComparison.OrdinalIgnoreCase);
			if (order != null)
				canonical["DefaultCloudOrder"] = order;

			var cloudToken = source.GetValue("Cloud", StringComparison.OrdinalIgnoreCase)
				?? source.GetValue("cloud", StringComparison.OrdinalIgnoreCase);
			if (cloudToken is JObject cloud)
			{
				var canonicalCloud = new JObject();
				var dropbox = cloud.GetValue("Dropbox", StringComparison.OrdinalIgnoreCase)
					?? cloud.GetValue("dropbox", StringComparison.OrdinalIgnoreCase);
				var oneDrive = cloud.GetValue("OneDrive", StringComparison.OrdinalIgnoreCase)
					?? cloud.GetValue("onedrive", StringComparison.OrdinalIgnoreCase);
				var googleDrive = cloud.GetValue("GoogleDrive", StringComparison.OrdinalIgnoreCase)
					?? cloud.GetValue("googledrive", StringComparison.OrdinalIgnoreCase);

				if (dropbox != null)
					canonicalCloud["Dropbox"] = dropbox;
				if (oneDrive != null)
					canonicalCloud["OneDrive"] = oneDrive;
				if (googleDrive != null)
					canonicalCloud["GoogleDrive"] = googleDrive;

				canonical["Cloud"] = canonicalCloud;
			}

			return canonical;
		}

		private static string ReadPathValue(dynamic source, string primary, string legacy, string fallback = "")
		{
			var jObject = AsJObject(source);
			if (jObject != null)
			{
				var token = jObject.GetValue(primary, StringComparison.OrdinalIgnoreCase)
					?? jObject.GetValue(legacy, StringComparison.OrdinalIgnoreCase);
				var value = token?.ToString();
				return string.IsNullOrWhiteSpace(value) ? fallback : value;
			}

			try
			{
				var value = (string)source[primary];
				if (!string.IsNullOrWhiteSpace(value))
					return value;
			}
			catch
			{
			}

			try
			{
				var value = (string)source[legacy];
				if (!string.IsNullOrWhiteSpace(value))
					return value;
			}
			catch
			{
			}

			return fallback;
		}

		private static List<string> ReadDefaultCloudOrder(dynamic source)
		{
			var jObject = AsJObject(source);
			if (jObject != null)
			{
				var token = jObject.GetValue("DefaultCloudOrder", StringComparison.OrdinalIgnoreCase)
					?? jObject.GetValue("defaultCloudOrder", StringComparison.OrdinalIgnoreCase);
				if (token is JArray array)
				{
					var values = array
						.Select(x => NormalizeCloudName(x?.ToString()))
						.Where(x => !string.IsNullOrWhiteSpace(x))
						.ToList();
					if (values.Count > 0)
						return values;
				}
			}

			return new List<string>
			{
				nameof(Cloud.OneDrive),
				nameof(Cloud.Dropbox),
				nameof(Cloud.GoogleDrive)
			};
		}

		private static string NormalizeCloudName(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				return string.Empty;

			if (Enum.TryParse<Cloud>(name, ignoreCase: true, out var parsed))
				return parsed.ToString();

			return string.Empty;
		}

		private static string ReadCloudRootPath(dynamic source, Cloud provider)
		{
			var jObject = AsJObject(source);
			if (jObject == null)
				return string.Empty;

			var cloudToken = jObject.GetValue("Cloud", StringComparison.OrdinalIgnoreCase)
				?? jObject.GetValue("cloud", StringComparison.OrdinalIgnoreCase);
			if (cloudToken is not JObject cloudObject)
				return string.Empty;

			var token = provider switch
			{
				Cloud.GoogleDrive => cloudObject.GetValue("GoogleDrive", StringComparison.OrdinalIgnoreCase) ?? cloudObject.GetValue("googledrive", StringComparison.OrdinalIgnoreCase),
				Cloud.Dropbox => cloudObject.GetValue("Dropbox", StringComparison.OrdinalIgnoreCase) ?? cloudObject.GetValue("dropbox", StringComparison.OrdinalIgnoreCase),
				Cloud.OneDrive => cloudObject.GetValue("OneDrive", StringComparison.OrdinalIgnoreCase) ?? cloudObject.GetValue("onedrive", StringComparison.OrdinalIgnoreCase),
				_ => null
			};

			var value = token?.ToString();
			return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
		}

		private static JObject AsJObject(dynamic source)
		{
			if (source is JObject direct)
				return direct;

			if (source is JToken token && token.Type == JTokenType.Object)
				return (JObject)token;

			try
			{
				if (source == null)
					return null;

				var converted = JObject.FromObject(source);
				return converted;
			}
			catch
			{
				return null;
			}
		}
	}
}
