using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OsLib
{
	public abstract class CliCommand
	{
		private readonly string executableName;

		protected CliCommand(string executableName, string packageName = null)
		{
			this.executableName = executableName ?? string.Empty;
			PackageName = packageName;
		}

		public string ExecutableName => executableName;
		public string PackageName { get; }
		/// <summary>Optional SSH target. When set, execution is delegated to the target host.</summary>
		public string RemoteTarget { get; set; }
		public virtual string DisplayName => GetType().Name;
		public virtual IEnumerable<string> CandidateExecutables
		{
			get
			{
				yield return executableName;
			}
		}

		protected virtual string UbuntuPackageName => PackageName;
		protected virtual string MacPackageName => PackageName;
		protected virtual string WindowsPackageId => PackageName;

		/// <summary>Configures this command to execute on an SSH target.</summary>
		public TCommand OverSsh<TCommand>(string remoteTarget) where TCommand : CliCommand
		{
			if (string.IsNullOrWhiteSpace(remoteTarget))
				throw new ArgumentException("An SSH target is required.", nameof(remoteTarget));
			RemoteTarget = remoteTarget;
			return (TCommand)this;
		}

		public bool IsAvailable()
		{
			if (string.IsNullOrWhiteSpace(RemoteTarget))
				return TryResolveExecutable(out _);

			var result = ExecuteRemoteCommand(
				RemoteTarget,
				$"which {QuotePosixShellToken(ExecutableName)}",
				timeoutMilliseconds: 120000);
			return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);
		}

		public bool TryResolveExecutable(out string executable)
		{
			foreach (var candidate in CandidateExecutables.Where(c => !string.IsNullOrWhiteSpace(c)))
			{
				var resolved = ResolveCandidate(candidate);
				if (!string.IsNullOrWhiteSpace(resolved))
				{
					executable = resolved;
					return true;
				}
			}

			executable = string.Empty;
			return false;
		}

		public string ResolveExecutable()
		{
			if (TryResolveExecutable(out var executable))
				return executable;

			return CandidateExecutables.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? executableName;
		}

		public virtual RaiSystemResult Run(string arguments = "")
		{
			if (!string.IsNullOrWhiteSpace(RemoteTarget))
				return ExecuteRemoteCommand(RemoteTarget, BuildPosixShellCommand(arguments), 120000);
			return RunAsync(arguments).GetAwaiter().GetResult();
		}

		public virtual Task<RaiSystemResult> RunAsync(string arguments = "", CancellationToken cancellationToken = default)
		{
			if (!string.IsNullOrWhiteSpace(RemoteTarget))
				return ExecuteRemoteCommandAsync(RemoteTarget, BuildPosixShellCommand(arguments), 120000, cancellationToken);
			var rs = new RaiSystem(ResolveExecutable(), arguments ?? string.Empty);
			return rs.ExecAsync(cancellationToken);
		}

		public virtual RaiSystemResult Run(IEnumerable<string> arguments)
		{
			return RunAsync(arguments).GetAwaiter().GetResult();
		}

		public virtual Task<RaiSystemResult> RunAsync(
			IEnumerable<string> arguments,
			CancellationToken cancellationToken = default)
			=> RunWithRemoteTargetAsync(arguments, remoteTarget: null, 120000, cancellationToken);

		public virtual RaiSystemResult Run(IEnumerable<string> arguments, int timeoutMilliseconds)
		{
			return RunWithRemoteTarget(arguments, remoteTarget: null, timeoutMilliseconds);
		}

		public virtual Task<RaiSystemResult> RunAsync(
			IEnumerable<string> arguments,
			int timeoutMilliseconds,
			CancellationToken cancellationToken = default)
		{
			return RunWithRemoteTargetAsync(arguments, remoteTarget: null, timeoutMilliseconds, cancellationToken);
		}

		/// <summary>
		/// Executes typed arguments locally or over an explicitly supplied SSH target.
		/// A supplied target takes precedence over <see cref="RemoteTarget"/>.
		/// </summary>
		protected RaiSystemResult RunWithRemoteTarget(
			IEnumerable<string> arguments,
			string remoteTarget,
			int timeoutMilliseconds = 120000)
		{
			var target = string.IsNullOrWhiteSpace(remoteTarget) ? RemoteTarget : remoteTarget;
			if (!string.IsNullOrWhiteSpace(target))
				return ExecuteRemoteCommand(target, BuildPosixShellCommand(arguments), timeoutMilliseconds);
			return new RaiSystem(ResolveExecutable(), arguments ?? Enumerable.Empty<string>()).ExecResult(timeoutMilliseconds);
		}

		/// <summary>Asynchronously executes typed arguments locally or through SSH.</summary>
		protected Task<RaiSystemResult> RunWithRemoteTargetAsync(
			IEnumerable<string> arguments,
			string remoteTarget,
			int timeoutMilliseconds = 120000,
			CancellationToken cancellationToken = default)
		{
			var target = string.IsNullOrWhiteSpace(remoteTarget) ? RemoteTarget : remoteTarget;
			if (!string.IsNullOrWhiteSpace(target))
				return ExecuteRemoteCommandAsync(target, BuildPosixShellCommand(arguments), timeoutMilliseconds, cancellationToken);
			return new RaiSystem(ResolveExecutable(), arguments ?? Enumerable.Empty<string>())
				.ExecAsync(timeoutMilliseconds, cancellationToken);
		}

		public string BuildPosixShellCommand(IEnumerable<string> arguments)
		{
			return string.Join(
				" ",
				new[] { ExecutableName }
					.Concat(arguments ?? Enumerable.Empty<string>())
					.Select(QuotePosixShellToken));
		}

		/// <summary>
		/// Preserves the legacy string-argument form for remote execution. New typed
		/// wrappers should use the tokenized overload above.
		/// </summary>
		public string BuildPosixShellCommand(string arguments)
		{
			var executable = QuotePosixShellToken(ExecutableName);
			return string.IsNullOrWhiteSpace(arguments) ? executable : $"{executable} {arguments}";
		}

		/// <summary>SSH seam for command wrappers and deterministic unit tests.</summary>
		protected virtual RaiSystemResult ExecuteRemoteCommand(
			string remoteTarget,
			string remoteCommand,
			int timeoutMilliseconds)
			=> SshSystem.ExecuteRemoteCommand(remoteTarget, remoteCommand, timeoutMilliseconds);

		private Task<RaiSystemResult> ExecuteRemoteCommandAsync(
			string remoteTarget,
			string remoteCommand,
			int timeoutMilliseconds,
			CancellationToken cancellationToken)
			=> Task.Factory.StartNew(
				() =>
				{
					cancellationToken.ThrowIfCancellationRequested();
					return ExecuteRemoteCommand(remoteTarget, remoteCommand, timeoutMilliseconds);
				},
				cancellationToken,
				TaskCreationOptions.LongRunning,
				TaskScheduler.Default);

		public virtual string GetInstallCommand()
		{
			var package = GetPackageReferenceForCurrentOs();
			if (string.IsNullOrWhiteSpace(package))
				return null;

			return Os.Type switch
			{
				OsType.Windows => $"winget install --id {package} --accept-source-agreements --accept-package-agreements",
				OsType.MacOS => $"brew install {package}",
				OsType.Ubuntu => $"sudo apt-get update && sudo apt-get install -y {package}",
				_ => null
			};
		}

		public virtual string GetUpdateCommand()
		{
			var package = GetPackageReferenceForCurrentOs();
			if (string.IsNullOrWhiteSpace(package))
				return null;

			return Os.Type switch
			{
				OsType.Windows => $"winget upgrade --id {package} --accept-source-agreements --accept-package-agreements",
				OsType.MacOS => $"brew upgrade {package}",
				OsType.Ubuntu => $"sudo apt-get update && sudo apt-get install --only-upgrade -y {package}",
				_ => null
			};
		}

		private string GetPackageReferenceForCurrentOs()
		{
			return Os.Type switch
			{
				OsType.Windows => WindowsPackageId,
				OsType.MacOS => MacPackageName,
				OsType.Ubuntu => UbuntuPackageName,
				_ => null
			};
		}

		protected static string FindExecutableOnPath(string executable)
		{
			var path = Environment.GetEnvironmentVariable("PATH");
			if (string.IsNullOrWhiteSpace(path))
				return null;

			var names = OperatingSystem.IsWindows()
				? ExpandWindowsCandidates(executable)
				: new[] { executable };

			foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
			{
				foreach (var name in names)
				{
					var candidate = Path.Combine(dir, name);
					if (File.Exists(candidate))
						return candidate;
				}
			}

			return null;
		}

		private static IEnumerable<string> ExpandWindowsCandidates(string executable)
		{
			if (Path.HasExtension(executable))
			{
				yield return executable;
				yield break;
			}

			yield return executable + ".exe";
			yield return executable + ".cmd";
			yield return executable + ".bat";
			yield return executable;
		}

		private static string ResolveCandidate(string candidate)
		{
			if (Path.IsPathRooted(candidate))
				return File.Exists(candidate) ? candidate : null;

			return FindExecutableOnPath(candidate);
		}

		private static string QuotePosixShellToken(string value)
		{
			value ??= string.Empty;
			if (value.Length > 0 && value.All(ch =>
				char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.' or '/' or ':' or '@' or '%'))
				return value;
			return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
		}
	}

	public sealed class CurlCommand : CliCommand
	{
		public CurlCommand(string executableName = "curl") : base(executableName, packageName: "curl")
		{
		}

		protected override string WindowsPackageId => "cURL.cURL";
	}

	public sealed class ZipCommand : CliCommand
	{
		public ZipCommand(string executableName = "zip") : base(executableName, packageName: "zip")
		{
		}

		protected override string WindowsPackageId => "GnuWin32.Zip";
	}

	public sealed class SevenZipCommand : CliCommand
	{
		public SevenZipCommand(string executableName = "7z") : base(executableName, packageName: "p7zip-full")
		{
		}

		public override IEnumerable<string> CandidateExecutables
		{
			get
			{
				yield return ExecutableName;
				yield return "7z";
				yield return "7zz";
				yield return "7za";
			}
		}

		protected override string MacPackageName => "p7zip";
		protected override string WindowsPackageId => "7zip.7zip";
	}
}
