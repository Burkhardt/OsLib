using System;
using System.Text;

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

		public static string ReadRemoteConfigJson5(string target, int timeoutMilliseconds = 120000)
		{
			var result = ExecuteScript(target, "if [ -f ~/.config/RAIkeep/osconfig.json5 ]; then cat ~/.config/RAIkeep/osconfig.json5; else printf missing; fi", timeoutMilliseconds);
			if (result.ExitCode != 0)
				throw new InvalidOperationException($"Could not read remote osconfig.json5 via ssh target '{target}'. exit={result.ExitCode} stderr={result.StandardError?.Trim()}");

			var content = result.StandardOutput?.Trim();
			if (string.Equals(content, "missing", StringComparison.Ordinal))
				throw new InvalidOperationException("Remote osconfig.json5 is missing at ~/.config/RAIkeep/osconfig.json5.");

			if (string.IsNullOrWhiteSpace(result.StandardOutput))
				throw new InvalidOperationException("Remote osconfig.json5 is empty.");

			return result.StandardOutput;
		}
	}
}