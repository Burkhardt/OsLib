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

		public bool IsAvailable() => TryResolveExecutable(out _);

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
			return RunAsync(arguments).GetAwaiter().GetResult();
		}

		public virtual Task<RaiSystemResult> RunAsync(string arguments = "", CancellationToken cancellationToken = default)
		{
			var rs = new RaiSystem(ResolveExecutable(), arguments ?? string.Empty);
			return rs.ExecAsync(cancellationToken);
		}

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