using System;
using Newtonsoft.Json.Linq;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class RemoteSshRealEnvironmentTests
{
	[Fact]
	public void Configured_RemoteObserver_Config_Exposes_UsableDirectories()
	{
		using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();

		if (!CloudStorageRealTestEnvironment.TryGetReachableRemoteObserver(out var observer, out var reason))
			Assert.Skip(reason);

		var sshTarget = observer.SshTarget;
		dynamic remoteConfig = Os.GetRemoteConfig(observer.Name);

		JObject remoteObject;
		JObject cloud;

		try
		{
			remoteObject = (JObject)remoteConfig;
			cloud = remoteObject["Cloud"] as JObject
				?? throw new InvalidOperationException("Cloud is missing or is not a JSON object.");
		}
		catch (Exception ex)
		{
			Assert.Fail($"Failed to access remote config properties: {ex.Message}");
			return;
		}

		if (!TryValidateRemoteDirectoryIsUsable(sshTarget, remoteObject["TempDir"]?.ToString(), "TempDir", requireWriteAccess: true, out var directoryReason))
			Assert.Skip(directoryReason);

		if (!TryValidateRemoteDirectoryIsUsable(sshTarget, cloud["OneDrive"]?.ToString(), "Cloud.OneDrive", requireWriteAccess: false, out directoryReason))
			Assert.Skip(directoryReason);
		if (!TryValidateRemoteDirectoryIsUsable(sshTarget, cloud["GoogleDrive"]?.ToString(), "Cloud.GoogleDrive", requireWriteAccess: false, out directoryReason))
			Assert.Skip(directoryReason);
		if (!TryValidateRemoteDirectoryIsUsable(sshTarget, cloud["Dropbox"]?.ToString(), "Cloud.Dropbox", requireWriteAccess: false, out directoryReason))
			Assert.Skip(directoryReason);

		Console.WriteLine($"Validated remote config usability for observer '{observer.Name}' at {observer.SshTarget}");
	}

	private static bool TryValidateRemoteDirectoryIsUsable(string sshTarget, string? configuredDirectory, string configName, bool requireWriteAccess, out string reason)
	{
		if (string.IsNullOrWhiteSpace(configuredDirectory))
		{
			reason = $"{configName} is missing or blank in remote config.";
			return false;
		}

		var escapedConfiguredDirectory = EscapeForSingleQuotedBash(configuredDirectory!);
		var probeDirectoryName = $"raikeep-remote-probe-{Guid.NewGuid():N}";
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

		reason = $"{configName} directory '{configuredDirectory}' is not usable via {sshTarget}. exit={result.ExitCode}; stdout={result.StandardOutput?.Trim()}; stderr={result.StandardError?.Trim()}";
		return false;
	}

	private static string EscapeForSingleQuotedBash(string value)
	{
		return (value ?? string.Empty).Replace("'", "'\"'\"'");
	}
}