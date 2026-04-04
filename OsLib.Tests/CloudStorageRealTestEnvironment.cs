using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace OsLib.Tests;

internal readonly record struct ConfiguredRemoteObserver(string Name, string SshTarget);

internal static class CloudStorageRealTestEnvironment
{
	internal const string RemoteSetupGuidePath = "OsLib/SSH_SETUP.md";

	internal static IDisposable BeginConfiguredCloudResolution()
	{
		_ = Os.Config;
		return new StringReader(string.Empty);
	}

	internal static string GetRemoteObserverSetupGuidance()
	{
		return $"See {RemoteSetupGuidePath} for SSH client and server setup.";
	}

	internal static IReadOnlyList<ConfiguredRemoteObserver> GetConfiguredRemoteObservers()
	{
		var configPath = Os.ConfigFileFullName;
		if (!File.Exists(configPath))
			Assert.Skip($"Required config file is missing: {configPath}. {Os.GetCloudStorageSetupGuidance()} {GetRemoteObserverSetupGuidance()}");

		var observers = new List<ConfiguredRemoteObserver>();
		try
		{
			foreach (var observer in Os.Config.Observers)
			{
				var name = observer.Name?.ToString()?.Trim() ?? string.Empty;
				var sshTarget = observer.SshTarget?.ToString()?.Trim() ?? string.Empty;
				if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(sshTarget))
					observers.Add(new ConfiguredRemoteObserver(name, sshTarget));
			}
		}
		catch (Exception ex)
		{
			Assert.Skip($"Configured observers could not be read from {configPath}. {GetRemoteObserverSetupGuidance()} {ex.Message}");
		}

		if (!observers.Any())
			Assert.Skip($"No configured observers with a usable SshTarget were found in {configPath}. {GetRemoteObserverSetupGuidance()}");

		return observers;
	}

	internal static bool TryGetReachableRemoteObserver(out ConfiguredRemoteObserver observer, out string reason)
	{
		var failures = new List<string>();
		foreach (var candidate in GetConfiguredRemoteObservers())
		{
			var result = SshSystem.ExecuteScript(candidate.SshTarget, "printf ready", 30000);
			if (result.ExitCode == 0 && result.StandardOutput.Trim() == "ready")
			{
				observer = candidate;
				reason = string.Empty;
				return true;
			}

			failures.Add($"{candidate.Name} ({candidate.SshTarget}): exit={result.ExitCode}; stdout={NormalizeProbeOutput(result.StandardOutput)}; stderr={NormalizeProbeOutput(result.StandardError)}");
		}

		observer = default;
		reason = $"No configured remote observer is reachable via ssh. {GetRemoteObserverSetupGuidance()}";
		if (failures.Any())
			reason += Environment.NewLine + string.Join(Environment.NewLine, failures);
		return false;
	}

	internal static bool TryCreateRemoteCloudSyncProbe(Cloud provider, out ConfiguredRemoteObserver observer, out RemoteCloudSyncProbe probe, out string reason)
	{
		var failures = new List<string>();
		foreach (var candidate in GetConfiguredRemoteObservers())
		{
			if (RemoteCloudSyncProbe.TryCreate(provider, candidate.Name, out probe, out var candidateReason))
			{
				observer = candidate;
				reason = string.Empty;
				return true;
			}

			failures.Add($"{candidate.Name} ({candidate.SshTarget}): {candidateReason}");
		}

		observer = default;
		probe = default!;
		reason = $"No configured remote observer is reachable and usable for {provider}. {GetRemoteObserverSetupGuidance()}";
		if (failures.Any())
			reason += Environment.NewLine + string.Join(Environment.NewLine, failures);
		return false;
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

	private static string NormalizeProbeOutput(string? value)
	{
		var trimmed = value?.Trim() ?? string.Empty;
		return string.IsNullOrWhiteSpace(trimmed) ? "<empty>" : trimmed;
	}
}