using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace OsLib
{
	public sealed class SshSystem : RaiSystem
	{
		public string Target { get; }

		public SshSystem(string target, string remoteCommand)
			: base("ssh", new[] { target ?? string.Empty, remoteCommand ?? string.Empty })
		{
			Target = target ?? string.Empty;
		}

		public static RaiSystemResult ExecuteRemoteCommand(string target, string remoteCommand, int timeoutMilliseconds = 120000)
		{
			return new SshSystem(target, remoteCommand).ExecResult(timeoutMilliseconds);
		}

		public static RaiSystemResult ExecuteScript(string target, string script, int timeoutMilliseconds = 120000)
		{
			var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(script ?? string.Empty));
			var remoteCommand = $"printf '%s' '{encoded}' | base64 --decode | /bin/bash";
			return ExecuteRemoteCommand(target, remoteCommand, timeoutMilliseconds);
		}
	}

	public sealed class SshFileProbe
	{
		const int DefaultPollIntervalMilliseconds = 1000;

		public string SshTarget { get; }
		public string LastFailure { get; private set; } = string.Empty;

		public SshFileProbe(string sshTarget)
		{
			SshTarget = sshTarget ?? string.Empty;
		}

		public RaiSystemResult ExecuteScript(string script, int timeoutMilliseconds = 120000)
		{
			var result = SshSystem.ExecuteScript(SshTarget, script, timeoutMilliseconds);
			LastFailure = FormatFailure(result, $"executing ssh script on {SshTarget}");
			return result;
		}

		public bool DirectoryExists(string remoteDirectory)
		{
			var result = ExecuteScript($"if [ -d {QuoteForBash(remoteDirectory)} ]; then printf ready; else printf missing; fi");
			LastFailure = FormatFailure(result, $"checking directory {remoteDirectory}");
			return result.ExitCode == 0 && result.StandardOutput.Trim() == "ready";
		}

		public string ReadFile(string remoteFile)
		{
			var result = ExecuteScript($"if [ -f {QuoteForBash(remoteFile)} ]; then cat {QuoteForBash(remoteFile)}; fi");
			LastFailure = FormatFailure(result, $"reading file {remoteFile}");
			return result.StandardOutput;
		}

		public bool FileExists(string remoteFile)
		{
			var result = ExecuteScript($"if [ -f {QuoteForBash(remoteFile)} ]; then printf present; else printf missing; fi");
			LastFailure = FormatFailure(result, $"checking file {remoteFile}");
			return result.ExitCode == 0 && result.StandardOutput.Trim() == "present";
		}

		public string ListDirectory(string remoteDirectory)
		{
			var result = ExecuteScript($"if [ -d {QuoteForBash(remoteDirectory)} ]; then ls -la {QuoteForBash(remoteDirectory)}; else printf missing; fi");
			LastFailure = FormatFailure(result, $"listing directory {remoteDirectory}");
			return result.StandardOutput;
		}

		public void RemoveDirectory(string remoteDirectory)
		{
			var result = ExecuteScript($"rm -rf {QuoteForBash(remoteDirectory)}");
			LastFailure = FormatFailure(result, $"removing directory {remoteDirectory}");
		}

		public bool WaitForFileContainingAll(string remoteFile, TimeSpan timeout, out TimeSpan elapsed, params string[] expectedFragments)
		{
			var watch = Stopwatch.StartNew();
			while (watch.Elapsed <= timeout)
			{
				var result = ExecuteScript($"if [ -f {QuoteForBash(remoteFile)} ]; then cat {QuoteForBash(remoteFile)}; fi");
				if (result.ExitCode == 0 && expectedFragments.Length > 0 && expectedFragments.All(fragment => result.StandardOutput.Contains(fragment)))
				{
					elapsed = watch.Elapsed;
					LastFailure = string.Empty;
					return true;
				}

				LastFailure = FormatFailure(result, $"waiting for file {remoteFile} to contain [{string.Join(", ", expectedFragments)}], observed: {result.StandardOutput.Trim()}");
				Thread.Sleep(DefaultPollIntervalMilliseconds);
			}

			elapsed = watch.Elapsed;
			return false;
		}

		public bool WaitForMissing(string remotePath, TimeSpan timeout, out TimeSpan elapsed)
		{
			var watch = Stopwatch.StartNew();
			while (watch.Elapsed <= timeout)
			{
				var result = ExecuteScript($"if [ ! -e {QuoteForBash(remotePath)} ]; then printf missing; else printf present; fi");
				if (result.ExitCode == 0 && result.StandardOutput.Trim() == "missing")
				{
					elapsed = watch.Elapsed;
					LastFailure = string.Empty;
					return true;
				}

				LastFailure = FormatFailure(result, $"waiting for {remotePath} to vanish");
				Thread.Sleep(DefaultPollIntervalMilliseconds);
			}

			elapsed = watch.Elapsed;
			return false;
		}

		public string DescribePathState(string remotePath)
		{
			var result = ExecuteScript($"if [ -f {QuoteForBash(remotePath)} ]; then printf 'file\n'; ls -la {QuoteForBash(remotePath)}; printf '\n--content--\n'; cat {QuoteForBash(remotePath)}; elif [ -d {QuoteForBash(remotePath)} ]; then printf 'directory\n'; ls -la {QuoteForBash(remotePath)}; else printf 'missing'; fi");
			LastFailure = FormatFailure(result, $"describing path {remotePath}");
			return result.StandardOutput;
		}

		private static string QuoteForBash(string value)
		{
			return $"'{(value ?? string.Empty).Replace("'", "'\"'\"'")}'";
		}

		private static string FormatFailure(RaiSystemResult result, string context)
		{
			var stderr = string.IsNullOrWhiteSpace(result.StandardError) ? string.Empty : $" stderr={result.StandardError.Trim()}";
			var stdout = string.IsNullOrWhiteSpace(result.StandardOutput) ? string.Empty : $" stdout={result.StandardOutput.Trim()}";
			var timeout = result.TimedOut ? " timedOut=true" : string.Empty;
			return $"{context}; exit={result.ExitCode}.{stdout}{stderr}{timeout}";
		}
	}

	public sealed class RemoteCloudSyncProbe
	{
		private RemoteCloudSyncProbe(Cloud provider, RaiPath localCloudRoot, RaiPath remoteCloudRoot, SshFileProbe observer)
		{
			Provider = provider;
			LocalCloudRoot = localCloudRoot;
			RemoteCloudRoot = remoteCloudRoot;
			Observer = observer;
		}

		public Cloud Provider { get; }
		public RaiPath LocalCloudRoot { get; }
		public RaiPath RemoteCloudRoot { get; }
		public SshFileProbe Observer { get; }
		public string SshTarget => Observer.SshTarget;
		public string LastFailure => Observer.LastFailure;

		public string GetRemoteFullName(string relativePath)
		{
			return new RaiFile(RemoteCloudRoot.Path + NormalizeRelativePath(relativePath)).FullName;
		}

		public RaiPath GetRemoteDirectory(string relativePath)
		{
			return new RaiPath(RemoteCloudRoot.Path + NormalizeRelativePath(relativePath));
		}

		public string GetRelativePathForLocalFile(string localFullName)
		{
			var normalizedLocalFullName = new RaiFile(localFullName).FullName;
			if (!normalizedLocalFullName.StartsWith(LocalCloudRoot.Path, StringComparison.OrdinalIgnoreCase))
				throw new ArgumentOutOfRangeException(nameof(localFullName), $"File '{localFullName}' is not under local cloud root '{LocalCloudRoot.Path}'.");

			return normalizedLocalFullName.Substring(LocalCloudRoot.Path.Length);
		}

		public static bool TryCreate(Cloud provider, string observerName, out RemoteCloudSyncProbe probe, out string reason)
		{
			probe = default!;

			var localCloudRoot = Os.GetCloudStorageRoot(provider, refresh: true);
			if (string.IsNullOrWhiteSpace(localCloudRoot.Path) || !localCloudRoot.Exists())
			{
				reason = $"{provider} is not configured or not accessible on this machine. {Os.GetCloudStorageSetupGuidance()}";
				return false;
			}

			if (!TryVerifyLocalWriteAccess(localCloudRoot, out var localWriteAccessFailure))
			{
				reason = $"Local {provider} root is not writable: {localWriteAccessFailure}";
				return false;
			}

			var observerModel = Os.LoadRemoteTestConfig(refresh: true).GetObserver(observerName);
			if (observerModel == null || string.IsNullOrWhiteSpace(observerModel.SshTarget))
			{
				reason = $"Configure observer '{observerName}' with a valid sshTarget in {Os.GetDefaultRemoteTestConfigPath()}.";
				return false;
			}

			var observer = new SshFileProbe(observerModel.SshTarget.Trim());
			var ping = observer.ExecuteScript("printf ready");
			if (ping.ExitCode != 0 || ping.StandardOutput.Trim() != "ready")
			{
				reason = $"SSH access failed. {observer.LastFailure}";
				return false;
			}

			var normalizedRemoteCloudRoot = ResolveRemoteCloudRoot(observer, observerModel, provider, out reason);
			if (string.IsNullOrWhiteSpace(normalizedRemoteCloudRoot))
				return false;

			if (!observer.DirectoryExists(normalizedRemoteCloudRoot))
			{
				reason = $"Remote {provider} root is not accessible. {observer.LastFailure}";
				return false;
			}

			probe = new RemoteCloudSyncProbe(provider, localCloudRoot, new RaiPath(normalizedRemoteCloudRoot), observer);
			reason = string.Empty;
			return true;
		}

		private static string NormalizeRelativePath(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				return string.Empty;

			var normalized = Os.NormSeperator(relativePath.Trim());
			while (normalized.StartsWith(Os.DIRSEPERATOR))
				normalized = normalized.Substring(1);

			return normalized;
		}

		private static string ResolveRemoteCloudRoot(SshFileProbe observer, RemoteObserverModel observerModel, Cloud provider, out string reason)
		{
			var configuredRoot = observerModel.GetCloudRoot(provider);
			if (!string.IsNullOrWhiteSpace(configuredRoot))
			{
				reason = string.Empty;
				return new RaiPath(configuredRoot).Path;
			}

			var remoteConfigResult = observer.ExecuteScript("if [ -f ~/.config/RAIkeep/osconfig.json ]; then cat ~/.config/RAIkeep/osconfig.json; else printf missing; fi");
			if (remoteConfigResult.ExitCode != 0)
			{
				reason = $"Could not read remote osconfig.json. {observer.LastFailure}";
				return string.Empty;
			}

			if (remoteConfigResult.StandardOutput.Trim() == "missing")
			{
				reason = "Remote osconfig.json is missing at ~/.config/RAIkeep/osconfig.json.";
				return string.Empty;
			}

			try
			{
				var config = JObject.Parse(remoteConfigResult.StandardOutput);
				var cloudKey = provider switch
				{
					Cloud.Dropbox => "dropbox",
					Cloud.OneDrive => "onedrive",
					Cloud.GoogleDrive => "googledrive",
					_ => string.Empty
				};

				var remoteRoot = config["cloud"]?[cloudKey]?.ToString();
				if (string.IsNullOrWhiteSpace(remoteRoot))
				{
					reason = $"Remote osconfig.json does not define cloud.{cloudKey}.";
					return string.Empty;
				}

				reason = string.Empty;
				return new RaiPath(remoteRoot).Path;
			}
			catch (Exception ex)
			{
				reason = $"Remote osconfig.json could not be parsed: {ex.Message}";
				return string.Empty;
			}
		}

		private static bool TryVerifyLocalWriteAccess(RaiPath localCloudRoot, out string failure)
		{
			var probeDirectory = (localCloudRoot / "RAIkeep" / ".write-access-probe" / Os.NewShortId());
			try
			{
				probeDirectory.mkdir();
				probeDirectory.rmdir(2, true);
				failure = string.Empty;
				return true;
			}
			catch (Exception ex)
			{
				failure = ex.Message;
				return false;
			}
		}
	}
}