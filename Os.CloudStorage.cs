using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using Newtonsoft.Json;

namespace OsLib
{
	public enum Cloud { Dropbox, OneDrive, GoogleDrive };
	public static partial class Os
	{
		public static string[] CloudProviders => Enum.GetNames(typeof(Cloud));
		private static readonly Cloud[] defaultCloudOrder = new[] { Cloud.OneDrive, Cloud.Dropbox, Cloud.GoogleDrive };

		private static dynamic config;
		private static ConcurrentDictionary<string, dynamic> remoteConfigs = null;
		/// <summary>
		/// lazy loading to please reflection based test isolation
		/// </summary>
		public static IReadOnlyDictionary<string, dynamic> RemoteConfigs
		{
			get
			{
				if (remoteConfigs == null)
					remoteConfigs = new ConcurrentDictionary<string, dynamic>(StringComparer.OrdinalIgnoreCase);
				if (remoteConfigs.Count == 0)
				{
					var observers = Config.Observers;
					foreach (var observer in observers)
					{
						var json = SshSystem.ReadRemoteConfigJson5(observer.SshTarget.ToString());
						dynamic rconf = JsonConvert.DeserializeObject<dynamic>(json);
						remoteConfigs.TryAdd(observer.Name.ToString(), rconf);
					}
				}
				return remoteConfigs;
			}
		}
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

		public static dynamic LoadConfig(string configFullName = null)
		{
			configLoadDepth++;
			try
			{
				var cf = configFullName ?? ConfigFileFullName;
				// read it from disk
				try
				{
					if (!File.Exists(cf))
					{
						config = null;
						InvalidateConfiguredPathCaches();
						ReportStartupCritical<OsDiagnosticsLogScope>(
							"config:missing",
							$"Config file missing at {cf}. Startup continues in degraded mode.",
							"Config file missing at {ConfigPath}. Startup continues in degraded mode.",
							cf);
						return config;
					}

					var json = File.ReadAllText(cf);
					if (string.IsNullOrWhiteSpace(json))
					{
						config = null;
						InvalidateConfiguredPathCaches();
						ReportStartupCritical<OsDiagnosticsLogScope>(
							"config:empty",
							$"Config file malformed or empty at {cf}. Startup continues in degraded mode.",
							"Config file malformed or empty at {ConfigPath}. Startup continues in degraded mode.",
							cf);
						return config;
					}

					config = JsonConvert.DeserializeObject<dynamic>(json);
					if (config == null)
					{
						InvalidateConfiguredPathCaches();
						ReportStartupCritical<OsDiagnosticsLogScope>(
							"config:malformed-null",
							$"Config file malformed at {cf}. Startup continues in degraded mode.",
							"Config file malformed at {ConfigPath}. Startup continues in degraded mode.",
							cf);
						return config;
					}

					InvalidateConfiguredPathCaches();

					try
					{
						var configuredHomeDir = (string)config.HomeDir;
						if (!string.IsNullOrWhiteSpace(configuredHomeDir))
							LogWarningOnce<OsDiagnosticsLogScope>("config:homeDir-ignored", "Ignoring HomeDir config value for intrinsic user home resolution.");
					}
					catch
					{
					}
				}
				catch (Exception ex)
				{
					config = null;
					InvalidateConfiguredPathCaches();
					ReportStartupCritical<OsDiagnosticsLogScope>(
						"config:malformed",
						ex,
						$"Config file malformed at {cf}. Startup continues in degraded mode.",
						"Config file malformed at {ConfigPath}. Startup continues in degraded mode.",
						cf);
				}
			}
			finally
			{
				configLoadDepth--;
			}
			return config;
		}

		private const string cloudDiscoveryGuidePath = "OsLib/CLOUD_STORAGE_DISCOVERY.md";
		public static bool IsCloudPath(RaiPath p) => IsCloudPath(p?.ToString());
		public static bool IsCloudPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path) || IsConfigLoading)
				return false;
			try
			{
				var normalizedPath = NormalizePathForComparison(path);
				if (string.IsNullOrWhiteSpace(normalizedPath))
					return false;
				if (IsDropboxMetadataPath(normalizedPath))
					return false;

				var activeConfig = Config;
				if (activeConfig == null)
					return false;

				foreach (var provider in GetEffectiveDefaultCloudOrder(activeConfig))
				{
					if (PathIsUnderCloudRoot(normalizedPath, GetConfiguredCloudRootOrEmpty(activeConfig, provider)))
						return true;
				}
			}
			catch (Exception ex)
			{
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
				var activeConfig = Config;
				if (activeConfig == null)
					return sb.ToString().TrimEnd();

				foreach (var provider in GetEffectiveDefaultCloudOrder(activeConfig))
				{
					var root = GetConfiguredCloudRootOrEmpty(activeConfig, provider);
					sb.AppendLine($"- {provider}: {(string.IsNullOrWhiteSpace(root) ? "<not configured>" : root)}");
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
			var order = new List<string>();
			sb.AppendLine("Cloud configuration diagnostics:");
			sb.AppendLine($"- active config path: {defaultConfigFileLocation}");
			sb.AppendLine($"- userHomeDir: {UserHomeDir?.Path}");
			sb.AppendLine($"- appRootDir: {AppRootDir?.Path}");
			sb.AppendLine($"- tempDir: {TempDir?.Path}");
			sb.AppendLine($"- localBackupDir: {LocalBackupDir?.Path}");
			try
			{
				var activeConfig = Config;
				if (activeConfig != null)
				{
					foreach (var provider in GetEffectiveDefaultCloudOrder(activeConfig))
						order.Add(provider.ToString());
				}
			}
			catch (Exception ex)
			{
				LogError<OsDiagnosticsLogScope>(ex, "Error reading configured default cloud order");
			}

			sb.AppendLine($"- configured default cloud order: {string.Join(", ", order)}");
			foreach (var provider in order)
			{
				sb.Append($"- {provider}: ");
				try
				{
					var root = Enum.TryParse<Cloud>(provider, ignoreCase: true, out var parsedProvider)
						? GetConfiguredCloudRootOrEmpty(Config, parsedProvider)
						: null;
					sb.AppendLine($" path: {root}");
				}
				catch
				{
					sb.AppendLine(" path: <invalid provider>");
				}
			}
			return sb.ToString().TrimEnd() + GetCloudDiscoveryReport(refresh);
		}
		public static string GetCloudStorageSetupGuidance()
		{
			return "Configure Os.Config in " + ConfigFileFullName + ". See " + cloudDiscoveryGuidePath;
		}
		public static Cloud GetCloudStorageProviderForPath(RaiPath path)
		{
			if (path == null || string.IsNullOrWhiteSpace(path.Path))
				throw new ArgumentNullException(nameof(path));
			var normalizedPath = NormalizePathForComparison(path.Path);
			if (string.IsNullOrWhiteSpace(normalizedPath))
				throw new ArgumentException("Invalid path", nameof(path));
			if (IsDropboxMetadataPath(normalizedPath))
				throw new InvalidOperationException($"Path '{path}' points to Dropbox metadata and is not treated as a cloud-backed content path.");
			foreach (var provider in GetEffectiveDefaultCloudOrder())
			{
				var root = GetConfiguredCloudRootOrEmpty(Config, provider);
				if (PathIsUnderCloudRoot(normalizedPath, root))
					return provider;
			}
			throw new InvalidOperationException($"Path '{path}' is not under any configured cloud storage root.");
		}
		public static RaiPath GetCloudStorageRoot(Cloud provider, bool refresh = false)
		{
			if (refresh)
				config = null;

			var root = GetConfiguredCloudRootOrEmpty(Config, provider);
			if (string.IsNullOrWhiteSpace(root))
				return null;

			return new RaiPath(root);
		}
		public static string ConfigFileFullName
		{
			get { return new RaiFile(defaultConfigFileLocation).FullName; }
		}
		public static string GetObserverSshTarget(string observerName)
		{
			var lookupName = observerName?.Trim() ?? string.Empty;
			var notFoundMessage = $"Observer '{observerName}' SshTarget not found in {ConfigFileFullName}.";

			try
			{
				foreach (var observer in Config.Observers)
				{
					var configuredName = observer.Name?.ToString()?.Trim() ?? string.Empty;
					if (!configuredName.Equals(lookupName, StringComparison.OrdinalIgnoreCase))
						continue;

					var sshTarget = observer.SshTarget?.ToString()?.Trim() ?? string.Empty;
					if (!string.IsNullOrWhiteSpace(sshTarget))
						return sshTarget;

					break;
				}
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException(notFoundMessage, ex);
			}

			throw new InvalidOperationException(notFoundMessage);
		}
		/// <summary>
		/// this method is just provided for the purpose of test isolation based on reflection
		/// the natural way to access a remote config is the object-oriented way (of course)
		/// dynamic remoteConfig = Os.RemoteConfigs["mzansi"];
		/// </summary>
		public static dynamic GetRemoteConfig(string observer, bool refresh = false)
		{
			//var key = observerName.Trim();	// redundant, Newtonsoft.json does this
			if (refresh && remoteConfigs != null)   // resetCache, the reflection based test isolation, just sets this to null
				remoteConfigs.TryRemove(observer, out _);
			return RemoteConfigs[observer];
			// return remoteConfigs.GetOrAdd(observer, k =>
			// {
			// 	var sshTarget = GetObserverSshTarget(k);
			// 	var json = SshSystem.ReadRemoteConfigJson5(sshTarget);
			// 	return JsonConvert.DeserializeObject<dynamic>(json);
			// });
		}
		public static string GetRemoteCloudRootFromConfig(string observerName, Cloud? provider = null, bool refresh = false)
		{
			dynamic remoteConfig = GetRemoteConfig(observerName, refresh);
			string cloudDir = string.Empty;
			string providerName = provider?.ToString();

			if (string.IsNullOrWhiteSpace(providerName))
			{
				var defaultOrder = remoteConfig.DefaultCloudOrder;
				if (defaultOrder == null || defaultOrder.Count == 0)
					throw new InvalidOperationException($"Remote config for observer '{observerName}' does not define DefaultCloudOrder.");
				providerName = defaultOrder[0]?.ToString();
			}

			switch (providerName)
			{
				case nameof(Cloud.GoogleDrive):
					cloudDir = (string)remoteConfig.Cloud.GoogleDrive;
					break;
				case nameof(Cloud.OneDrive):
					cloudDir = (string)remoteConfig.Cloud.OneDrive;
					break;
				case nameof(Cloud.Dropbox):
					cloudDir = (string)remoteConfig.Cloud.Dropbox;
					break;
				default:
					throw new InvalidOperationException($"Unsupported cloud provider: {providerName}");
			}

			if (string.IsNullOrWhiteSpace(cloudDir))
				throw new InvalidOperationException($"Remote config for observer '{observerName}' does not define Cloud.{providerName}.");

			return new RaiPath(cloudDir).Path;
		}
		public static string GetRemoteTempDirFromConfig(string observerName, bool refresh = false)
		{
			dynamic remoteConfig = GetRemoteConfig(observerName, refresh);
			var tempDir = (string)remoteConfig.TempDir;
			if (string.IsNullOrWhiteSpace(tempDir))
				throw new InvalidOperationException($"Remote config for observer '{observerName}' does not define TempDir.");

			return new RaiPath(tempDir).Path;
		}
		public static bool TryGetRemoteConfig(string observerName, out dynamic remoteConfig)
		{
			return remoteConfigs.TryGetValue(observerName ?? string.Empty, out remoteConfig);
		}
		public static void InvalidateRemoteConfig(string observerName = null)
		{
			if (string.IsNullOrWhiteSpace(observerName))
			{
				remoteConfigs.Clear();
				return;
			}

			remoteConfigs.TryRemove(observerName.Trim(), out _);
		}
		internal static string ParseCloudRootFromConfigJson(string json, Cloud provider)
		{
			var parsed = JsonConvert.DeserializeObject<dynamic>(json);
			if (parsed == null)
				throw new InvalidOperationException("Remote osconfig.json5 could not be parsed.");

			var root = provider switch
			{
				Cloud.GoogleDrive => (string)parsed.Cloud.GoogleDrive,
				Cloud.Dropbox => (string)parsed.Cloud.Dropbox,
				Cloud.OneDrive => (string)parsed.Cloud.OneDrive,
				_ => string.Empty
			};

			if (string.IsNullOrWhiteSpace(root))
				throw new InvalidOperationException($"Remote osconfig.json5 does not define a cloud root for provider '{provider}'. Ensure ~/.config/RAIkeep/osconfig.json5 on the remote machine has a 'cloud.{provider.ToString().ToLowerInvariant()}' entry.");

			return new RaiPath(root).Path;
		}
		internal static string ParseTempDirFromConfigJson(string json)
		{
			var parsed = JsonConvert.DeserializeObject<dynamic>(json);
			if (parsed == null)
				throw new InvalidOperationException("Remote osconfig.json5 could not be parsed.");

			var tempDir = (string)parsed.TempDir;
			if (string.IsNullOrWhiteSpace(tempDir))
				throw new InvalidOperationException("Remote osconfig.json5 does not define TempDir. Add TempDir to ~/.config/RAIkeep/osconfig.json5 on the remote machine.");

			return new RaiPath(tempDir).Path;
		}
		private static void InvalidateConfiguredPathCaches()
		{
			userHomeDir = null;
			appRootDir = null;
			cloudStorageRootDir = null;
			tempDir = null;
			localBackupDir = null;
		}
		private static bool TryAddRoot(Dictionary<Cloud, string> roots, Cloud provider, string candidate)
		{
			if (roots.ContainsKey(provider))
				return false;

			var candidatePath = new RaiPath(candidate);
			if (!candidatePath.Exists())
				return false;

			roots[provider] = candidatePath.Path;
			return true;
		}
		private static string NormalizePathForComparison(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return null;

			var normalized = NormSeperator(value.Trim());

			if (normalized == ".")
				normalized = EnsureTrailingDirectorySeparator(Directory.GetCurrentDirectory());
			else if (normalized.StartsWith("./", StringComparison.Ordinal))
				normalized = EnsureTrailingDirectorySeparator(Directory.GetCurrentDirectory()) + normalized.Substring(2);
			else if (normalized == "~")
				normalized = EnsureTrailingDirectorySeparator(GetIntrinsicUserHomePath());
			else if (normalized.StartsWith("~/", StringComparison.Ordinal))
				normalized = EnsureTrailingDirectorySeparator(GetIntrinsicUserHomePath()) + normalized.Substring(2);

			return normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		}
		private static string GetIntrinsicUserHomePath()
		{
			string resolved;
			if (Type == OsType.Windows)
			{
				resolved = EnsureTrailingDirectorySeparator(Environment.GetEnvironmentVariable("USERPROFILE"));
				if (string.IsNullOrWhiteSpace(resolved))
				{
					var homeDrive = Environment.GetEnvironmentVariable("HOMEDRIVE");
					var homePath = EnsureTrailingDirectorySeparator(Environment.GetEnvironmentVariable("HOMEPATH"));
					if (!string.IsNullOrEmpty(homeDrive) && !string.IsNullOrEmpty(homePath))
						resolved = NormSeperator(homeDrive + homePath);
				}
			}
			else
			{
				resolved = EnsureTrailingDirectorySeparator(Environment.GetEnvironmentVariable("HOME"));
			}

			if (string.IsNullOrWhiteSpace(resolved))
				resolved = EnsureTrailingDirectorySeparator(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? string.Empty);

			return resolved;
		}
		private static bool PathIsUnderCloudRoot(string normalizedCandidate, string root)
		{
			var normalizedRoot = NormalizePathForComparison(root);
			if (string.IsNullOrWhiteSpace(normalizedRoot))
				return false;
			return normalizedCandidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
				normalizedCandidate.StartsWith(normalizedRoot + DIR, StringComparison.OrdinalIgnoreCase);
		}
		private static bool IsDropboxMetadataPath(string normalizedPath)
		{
			var marker = DIR + ".dropbox";
			return normalizedPath.EndsWith(marker, StringComparison.OrdinalIgnoreCase) ||
				normalizedPath.Contains(marker + DIR, StringComparison.OrdinalIgnoreCase);
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
		private static IReadOnlyList<Cloud> GetEffectiveDefaultCloudOrder(dynamic activeConfig = null)
		{
			activeConfig ??= Config;

			var order = new List<Cloud>();
			try
			{
				if (activeConfig?.DefaultCloudOrder != null)
				{
					foreach (var providerEntry in activeConfig.DefaultCloudOrder)
					{
						var providerName = providerEntry?.ToString() ?? string.Empty;
						Cloud provider;
						if (Enum.TryParse<Cloud>(providerName, true, out provider) && !order.Contains(provider))
							order.Add(provider);
					}
				}
			}
			catch
			{
			}

			foreach (var provider in defaultCloudOrder)
			{
				if (!order.Contains(provider))
					order.Add(provider);
			}

			return order;
		}
		private static string GetConfiguredCloudRootOrEmpty(dynamic activeConfig, Cloud provider)
		{
			if (activeConfig == null)
				return string.Empty;

			try
			{
				return provider switch
				{
					Cloud.GoogleDrive => (string)activeConfig.Cloud.GoogleDrive,
					Cloud.Dropbox => (string)activeConfig.Cloud.Dropbox,
					Cloud.OneDrive => (string)activeConfig.Cloud.OneDrive,
					_ => string.Empty
				} ?? string.Empty;
			}
			catch
			{
				return string.Empty;
			}
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
	}
}
