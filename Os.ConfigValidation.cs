using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace OsLib
{
	internal sealed class OsConfigValidationException : InvalidOperationException
	{
		internal OsConfigValidationException(string message)
			: base(message)
		{
		}
		internal OsConfigValidationException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
	public static partial class Os
	{
		private const string observerSetupGuidePath = "OsLib/SSH_SETUP.md";
		internal static string ObserverSetupGuidance => $"See {observerSetupGuidePath} for SSH client and remote observer setup.";
		private static void ValidateConfiguredEnvironment(JObject activeConfig, string configFullName)
		{
			var failures = new List<string>();
			var validatedTempDir = ValidateConfiguredTempDir(activeConfig, failures);
			var validatedCloudRoots = ValidateConfiguredCloudRoots(activeConfig, failures);
			var validatedLocalBackupDir = ValidateConfiguredLocalBackupDir(activeConfig, validatedCloudRoots);
			ValidateConfiguredObservers(activeConfig, failures);
			if (failures.Count > 0)
			{
				config = null;
				InvalidateConfiguredPathCaches();
				var message = BuildConfigValidationFailureMessage(configFullName, failures);
				ReportStartupCritical<OsDiagnosticsLogScope>(
					"config:validation",
					message,
					"{Message}",
					message);
				throw new OsConfigValidationException(message);
			}
			tempDir = validatedTempDir;
			localBackupDir = validatedLocalBackupDir;
			localBackupDirDisabled = validatedLocalBackupDir == null;
			cloudStorageRootDir = null;
		}
		private static RaiPath ValidateConfiguredTempDir(JObject activeConfig, List<string> failures)
		{
			var configuredTempDir = GetConfigString(activeConfig, "TempDir");
			if (string.IsNullOrWhiteSpace(configuredTempDir))
			{
				failures.Add($"TempDir is missing. {GetCloudStorageSetupGuidance()}");
				return null;
			}
			var resolvedTempDir = ResolveConfiguredDirectory(configuredTempDir);
			if (!TryProbeLocalDirectory("TempDir", resolvedTempDir, requireWriteAccess: true, out var reason))
				failures.Add(reason);
			return resolvedTempDir;
		}
		private static Dictionary<Cloud, RaiPath> ValidateConfiguredCloudRoots(JObject activeConfig, List<string> failures)
		{
			var configuredRoots = new Dictionary<Cloud, RaiPath>();
			var explicitOrder = GetExplicitConfiguredCloudOrder(activeConfig, "Config", GetCloudStorageSetupGuidance(), failures);
			var configuredProviders = GetConfiguredCloudProviders(activeConfig);
			var providersToValidate = explicitOrder.Concat(configuredProviders).Distinct().ToList();
			if (providersToValidate.Count == 0)
			{
				LogWarningOnce<OsDiagnosticsLogScope>(
					"config:cloud-disabled",
					"No cloud provider is configured. Cloud features are disabled. {Guidance}",
					GetCloudStorageSetupGuidance());
				return configuredRoots;
			}
			foreach (var provider in providersToValidate)
			{
				var configuredRoot = GetConfiguredCloudRootOrEmpty(activeConfig, provider);
				if (string.IsNullOrWhiteSpace(configuredRoot))
				{
					failures.Add($"Cloud.{provider} is missing although {provider} is listed in DefaultCloudOrder. {GetCloudStorageSetupGuidance()}");
					continue;
				}
				var resolvedRoot = ResolveConfiguredDirectory(configuredRoot);
				if (!TryProbeLocalDirectory($"Cloud.{provider}", resolvedRoot, requireWriteAccess: true, out var reason))
				{
					failures.Add(reason + " " + GetCloudStorageSetupGuidance());
					continue;
				}
				configuredRoots[provider] = resolvedRoot;
			}
			return configuredRoots;
		}
		private static RaiPath ValidateConfiguredLocalBackupDir(JObject activeConfig, IReadOnlyDictionary<Cloud, RaiPath> configuredCloudRoots)
		{
			var configuredLocalBackupDir = GetConfigString(activeConfig, "LocalBackupDir");
			if (string.IsNullOrWhiteSpace(configuredLocalBackupDir))
			{
				LogWarningOnce<OsDiagnosticsLogScope>(
					"config:localbackup-disabled",
					"LocalBackupDir is not configured. Backup features are disabled. {Guidance}",
					GetCloudStorageSetupGuidance());
				return null;
			}
			var resolvedBackupDir = ResolveConfiguredDirectory(configuredLocalBackupDir);
			if (IsUnderConfiguredCloudRoot(resolvedBackupDir.Path, configuredCloudRoots))
			{
				LogWarningOnce<OsDiagnosticsLogScope>(
					$"config:localbackup-disabled:{resolvedBackupDir.Path}",
					"Configured LocalBackupDir {LocalBackupDir} is cloud-backed. Backup features are disabled. {Guidance}",
					resolvedBackupDir.Path,
					GetCloudStorageSetupGuidance());
				return null;
			}
			if (!TryProbeLocalDirectory("LocalBackupDir", resolvedBackupDir, requireWriteAccess: true, out var reason))
			{
				LogWarningOnce<OsDiagnosticsLogScope>(
					$"config:localbackup-disabled:{resolvedBackupDir.Path}",
					"{Reason} Backup features are disabled. {Guidance}",
					reason,
					GetCloudStorageSetupGuidance());
				return null;
			}
			return resolvedBackupDir;
		}
		private static void ValidateConfiguredObservers(JObject activeConfig, List<string> failures)
		{
			if (!TryGetObservers(activeConfig, out var observers, out var malformedReason))
			{
				failures.Add(malformedReason + " " + ObserverSetupGuidance);
				return;
			}
			if (observers.Count == 0)
				return;
			foreach (var observer in observers)
			{
				var observerName = GetConfigString(observer, "Name");
				var sshTarget = GetConfigString(observer, "SshTarget");
				if (string.IsNullOrWhiteSpace(observerName) || string.IsNullOrWhiteSpace(sshTarget))
				{
					failures.Add($"Each configured observer must define Name and SshTarget. {ObserverSetupGuidance}");
					continue;
				}
				if (!TryPingRemoteObserver(observerName, sshTarget, out var pingReason))
				{
					failures.Add(pingReason + " " + ObserverSetupGuidance);
					continue;
				}
				if (!TryReadRemoteConfigJson(sshTarget, out var remoteConfigJson, out var remoteConfigReason))
				{
					failures.Add($"Observer '{observerName}' at {sshTarget} could not provide a readable remote osconfig.json5. {remoteConfigReason} {ObserverSetupGuidance} {GetCloudStorageSetupGuidance()}");
					continue;
				}
				ValidateRemoteObserverEnvironment(observerName, sshTarget, remoteConfigJson, failures);
			}
		}
		private static void ValidateRemoteObserverEnvironment(string observerName, string sshTarget, string remoteConfigJson, List<string> failures)
		{
			JObject remoteConfig;
			try
			{
				remoteConfig = JsonConvert.DeserializeObject<JObject>(remoteConfigJson)
					?? throw new InvalidDataException("Remote osconfig.json5 could not be parsed.");
			}
			catch (Exception ex)
			{
				failures.Add($"Observer '{observerName}' at {sshTarget} returned an invalid remote osconfig.json5. {ex.Message} {ObserverSetupGuidance} {GetCloudStorageSetupGuidance()}");
				return;
			}
			var remoteTempDir = GetConfigString(remoteConfig, "TempDir");
			if (string.IsNullOrWhiteSpace(remoteTempDir))
			{
				failures.Add($"Observer '{observerName}' at {sshTarget} has no remote TempDir configured. {ObserverSetupGuidance} {GetCloudStorageSetupGuidance()}");
			}
			else if (!TryProbeRemoteDirectory(sshTarget, remoteTempDir, $"Observer '{observerName}' TempDir", requireWriteAccess: true, out var remoteTempReason))
			{
				failures.Add(remoteTempReason + " " + ObserverSetupGuidance + " " + GetCloudStorageSetupGuidance());
			}
			var explicitOrder = GetExplicitConfiguredCloudOrder(remoteConfig, $"Observer '{observerName}' remote config", GetCloudStorageSetupGuidance(), failures);
			var configuredProviders = GetConfiguredCloudProviders(remoteConfig);
			foreach (var provider in explicitOrder.Concat(configuredProviders).Distinct())
			{
				var remoteCloudRoot = GetConfiguredCloudRootOrEmpty(remoteConfig, provider);
				if (string.IsNullOrWhiteSpace(remoteCloudRoot))
				{
					failures.Add($"Observer '{observerName}' remote config is missing Cloud.{provider} although {provider} is listed in DefaultCloudOrder. {ObserverSetupGuidance} {GetCloudStorageSetupGuidance()}");
					continue;
				}
				if (!TryProbeRemoteDirectory(sshTarget, remoteCloudRoot, $"Observer '{observerName}' Cloud.{provider}", requireWriteAccess: true, out var remoteCloudReason))
					failures.Add(remoteCloudReason + " " + ObserverSetupGuidance + " " + GetCloudStorageSetupGuidance());
			}
		}
		private static string BuildConfigValidationFailureMessage(string configFullName, IEnumerable<string> failures)
		{
			var sb = new StringBuilder();
			sb.AppendLine($"Config validation failed for {configFullName}. Startup cannot continue.");
			foreach (var failure in failures.Where(failure => !string.IsNullOrWhiteSpace(failure)).Distinct())
				sb.AppendLine($"- {failure}");
			return sb.ToString().TrimEnd();
		}
		private static RaiPath ResolveConfiguredDirectory(string configuredValue)
		{
			return new RaiPath(ExpandLeadingDirectorySymbols(NormSeperator(configuredValue?.Trim() ?? string.Empty)));
		}
		private static string GetConfigString(JObject configObject, string propertyName)
		{
			if (configObject == null || string.IsNullOrWhiteSpace(propertyName))
				return string.Empty;
			return configObject.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out var token)
				&& token != null
				&& token.Type != JTokenType.Null
					? token.ToString().Trim()
					: string.Empty;
		}
		private static bool TryGetObservers(JObject configObject, out IReadOnlyList<JObject> observers, out string reason)
		{
			observers = Array.Empty<JObject>();
			reason = string.Empty;
			if (configObject == null || !configObject.TryGetValue("Observers", StringComparison.OrdinalIgnoreCase, out var observersToken) || observersToken == null || observersToken.Type == JTokenType.Null)
				return true;
			if (observersToken is not JArray observersArray)
			{
				reason = "Observers must be a JSON array in osconfig.json5.";
				return false;
			}
			observers = observersArray.OfType<JObject>().ToList();
			if (observers.Count != observersArray.Count)
			{
				reason = "Each observer entry in osconfig.json5 must be a JSON object with Name and SshTarget.";
				return false;
			}
			return true;
		}
		private static IReadOnlyList<Cloud> GetConfiguredCloudProviders(JObject configObject)
		{
			return Enum.GetValues(typeof(Cloud))
				.Cast<Cloud>()
				.Where(provider => !string.IsNullOrWhiteSpace(GetConfiguredCloudRootOrEmpty(configObject, provider)))
				.ToList();
		}
		private static IReadOnlyList<Cloud> GetExplicitConfiguredCloudOrder(JObject configObject, string contextLabel, string guidance, List<string> failures)
		{
			var order = new List<Cloud>();
			if (configObject == null || !configObject.TryGetValue("DefaultCloudOrder", StringComparison.OrdinalIgnoreCase, out var orderToken) || orderToken == null || orderToken.Type == JTokenType.Null)
				return order;
			if (orderToken is not JArray orderArray)
			{
				failures.Add($"{contextLabel} DefaultCloudOrder must be a JSON array. {guidance}");
				return order;
			}
			foreach (var providerToken in orderArray)
			{
				var providerName = providerToken?.ToString()?.Trim() ?? string.Empty;
				if (string.IsNullOrWhiteSpace(providerName))
					continue;
				if (Enum.TryParse<Cloud>(providerName, ignoreCase: true, out var provider) && !order.Contains(provider))
				{
					order.Add(provider);
					continue;
				}
				LogWarningOnce<OsDiagnosticsLogScope>(
					$"config:unsupported-cloud:{contextLabel}:{providerName}",
					"Unsupported cloud provider {ProviderName} in DefaultCloudOrder is ignored. {Guidance}",
					providerName,
					guidance);
			}
			return order;
		}
		private static bool IsUnderConfiguredCloudRoot(string candidatePath, IReadOnlyDictionary<Cloud, RaiPath> configuredCloudRoots)
		{
			if (configuredCloudRoots == null || string.IsNullOrWhiteSpace(candidatePath))
				return false;
			foreach (var root in configuredCloudRoots.Values)
			{
				if (root != null && PathIsUnderCloudRoot(candidatePath, root.Path))
					return true;
			}
			return false;
		}
		private static bool TryProbeLocalDirectory(string label, RaiPath directoryPath, bool requireWriteAccess, out string reason)
		{
			if (directoryPath == null || string.IsNullOrWhiteSpace(directoryPath.Path))
			{
				reason = $"{label} is missing or blank.";
				return false;
			}
			if (!directoryPath.Exists())
			{
				reason = $"{label} directory '{directoryPath.Path}' does not exist.";
				return false;
			}
			if (!requireWriteAccess)
			{
				reason = string.Empty;
				return true;
			}
			var probeDir = directoryPath / $"RAIkeep.Test.{Guid.NewGuid():N}";
			try
			{
				probeDir.mkdir();
				var probeFile = new TextFile(probeDir, "probe", content: "ready");
				var content = probeFile.ReadAllText().Trim();
				if (!content.Equals("ready", StringComparison.Ordinal))
				{
					reason = $"{label} directory '{directoryPath.Path}' did not return the probe file content after writing.";
					return false;
				}
				reason = string.Empty;
				return true;
			}
			catch (Exception ex)
			{
				reason = $"{label} directory '{directoryPath.Path}' is not readable and writable. {ex.Message}";
				return false;
			}
			finally
			{
				try
				{
					if (probeDir.Exists())
						probeDir.rmdir(depth: 1, deleteFiles: true);
				}
				catch
				{
				}
			}
		}
		private static bool TryPingRemoteObserver(string observerName, string sshTarget, out string reason)
		{
			var result = SshSystem.ExecuteScript(sshTarget, "printf ready", 30000);
			if (result.ExitCode == 0 && result.StandardOutput.Trim() == "ready")
			{
				reason = string.Empty;
				return true;
			}
			reason = $"Observer '{observerName}' at {sshTarget} is not reachable via ssh. exit={result.ExitCode}; stdout={NormalizeProbeOutput(result.StandardOutput)}; stderr={NormalizeProbeOutput(result.StandardError)}";
			return false;
		}
		private static bool TryReadRemoteConfigJson(string sshTarget, out string remoteConfigJson, out string reason)
		{
			try
			{
				remoteConfigJson = SshSystem.ReadRemoteConfigJson5(sshTarget);
				reason = string.Empty;
				return true;
			}
			catch (Exception ex)
			{
				remoteConfigJson = string.Empty;
				reason = ex.Message;
				return false;
			}
		}
		private static bool TryProbeRemoteDirectory(string sshTarget, string configuredDirectory, string label, bool requireWriteAccess, out string reason)
		{
			if (string.IsNullOrWhiteSpace(configuredDirectory))
			{
				reason = $"{label} is missing or blank.";
				return false;
			}
			var escapedConfiguredDirectory = EscapeForSingleQuotedBash(configuredDirectory);
			var probeDirectoryName = $"RAIkeep.Test.{Guid.NewGuid():N}";
			var script = $"""
configured='{escapedConfiguredDirectory}'
if [ "$configured" = "~" ]; then
	resolved="$HOME/"
elif printf '%s' "$configured" | grep -q '^~/'; then
	suffix="$(printf '%s' "$configured" | sed 's#^~/##')"
	resolved="$HOME/$suffix"
else
	resolved="$configured"
fi
if [ ! -d "$resolved" ]; then
	printf 'missing-dir\nresolved=%s' "$resolved"
	exit 1
fi
""";
			if (requireWriteAccess)
			{
				script += $"""
probe_dir="$resolved/{probeDirectoryName}"
probe_file="$probe_dir/probe.txt"
mkdir -p "$probe_dir" &&
printf ready > "$probe_file" &&
cat "$probe_file" &&
rm -f "$probe_file" &&
rmdir "$probe_dir" &&
printf '\nresolved=%s' "$resolved"
""";
			}
			else
			{
				script += "printf 'ready\\nresolved=%s' \"$resolved\"";
			}
			var result = SshSystem.ExecuteScript(sshTarget, script);
			if (result.ExitCode == 0 && result.StandardOutput.Contains("ready", StringComparison.Ordinal))
			{
				reason = string.Empty;
				return true;
			}
			reason = $"{label} directory '{configuredDirectory}' is not usable via {sshTarget}. exit={result.ExitCode}; stdout={NormalizeProbeOutput(result.StandardOutput)}; stderr={NormalizeProbeOutput(result.StandardError)}";
			return false;
		}
		private static string EscapeForSingleQuotedBash(string value)
		{
			return (value ?? string.Empty).Replace("'", "'\"'\"'");
		}
		private static string NormalizeProbeOutput(string value)
		{
			var trimmed = value?.Trim() ?? string.Empty;
			return string.IsNullOrWhiteSpace(trimmed) ? "<empty>" : trimmed;
		}
	}
}