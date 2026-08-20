using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OsLib
{
	public sealed record PitsCommandOptions
	{
		public RaiPath PitRoot { get; init; }
		public string CloudProvider { get; init; }
		public bool Debug { get; init; }
		public bool NoLogo { get; init; }
		public bool RetainWindow { get; init; }
	}

	public sealed class PitsTarget
	{
		private PitsTarget(string pitName, bool isWwwa)
		{
			PitName = pitName;
			IsWwwa = isWwwa;
		}

		public string PitName { get; }
		public bool IsWwwa { get; }

		public static PitsTarget Pit(string pitName)
		{
			RequireValue(pitName, nameof(pitName));
			return new PitsTarget(pitName, isWwwa: false);
		}

		public static PitsTarget Wwwa() => new(null, isWwwa: true);

		internal void AppendTo(List<string> arguments)
		{
			if (IsWwwa)
				arguments.Add("--wwwa");
			else
				arguments.Add(PitName);
		}

		private static void RequireValue(string value, string parameterName)
		{
			if (string.IsNullOrWhiteSpace(value))
				throw new ArgumentException("A pit name is required.", parameterName);
			if (value.StartsWith("-", StringComparison.Ordinal))
				throw new ArgumentException("A pit name cannot be parsed as an option.", parameterName);
		}
	}

	public sealed record PitsSeedRequest
	{
		private PitsSeedRequest(PitsTarget target, string source)
		{
			Target = target;
			Source = source;
		}

		public PitsTarget Target { get; }
		public string Source { get; }
		public PitsCommandOptions Options { get; init; }

		public static PitsSeedRequest ForPit(string pitName, RaiFile source)
			=> new(PitsTarget.Pit(pitName), source?.FullName);

		public static PitsSeedRequest ForWwwa(RaiPath sourceDirectory)
			=> new(PitsTarget.Wwwa(), sourceDirectory?.FullPath);
	}

	public sealed record PitsExportRequest
	{
		private PitsExportRequest(PitsTarget target, RaiPath outputDirectory, bool json)
		{
			Target = target;
			OutputDirectory = outputDirectory;
			Json = json;
		}

		public PitsTarget Target { get; }
		public RaiPath OutputDirectory { get; }
		public bool Json { get; }
		public PitsCommandOptions Options { get; init; }

		public static PitsExportRequest ToDirectory(PitsTarget target, RaiPath outputDirectory)
			=> new(target, outputDirectory, json: false);

		public static PitsExportRequest ToJson(PitsTarget target)
			=> new(target, outputDirectory: null, json: true);
	}

	public sealed record PitsAuditRequest
	{
		public PitsAuditRequest(PitsTarget target)
		{
			Target = target;
		}

		public PitsTarget Target { get; }
		public string Machine { get; init; }
		public string MinimumLevel { get; init; }
		public bool Json { get; init; }
		public PitsCommandOptions Options { get; init; }
	}

	public sealed class PitsCommand : CliCommand
	{
		private readonly RaiPath commandPath;
		private readonly string commandName;
		private readonly RaiFile managedAssembly;

		public PitsCommand(RaiPath commandPath = null, string commandName = "pits")
			: base(string.IsNullOrWhiteSpace(commandName) ? "pits" : commandName)
		{
			this.commandPath = commandPath;
			this.commandName = string.IsNullOrWhiteSpace(commandName) ? "pits" : commandName;
		}

		private PitsCommand(RaiFile managedAssembly, string hostCommand, bool managed)
			: base(string.IsNullOrWhiteSpace(hostCommand) ? "dotnet" : hostCommand)
		{
			if (managedAssembly == null || string.IsNullOrWhiteSpace(managedAssembly.FullName))
				throw new ArgumentException("A managed pits assembly is required.", nameof(managedAssembly));
			this.managedAssembly = managedAssembly;
			commandName = string.IsNullOrWhiteSpace(hostCommand) ? "dotnet" : hostCommand;
		}

		public static PitsCommand ForManagedAssembly(RaiFile managedAssembly, string hostCommand = "dotnet")
			=> new(managedAssembly, hostCommand, managed: true);

		public override IEnumerable<string> CandidateExecutables
		{
			get
			{
				if (commandPath != null)
				{
					var command = new RaiFile(commandName) { Path = commandPath };
					yield return command.FullName;
				}

				yield return commandName;
			}
		}

		public IReadOnlyList<string> BuildSeedArguments(PitsSeedRequest request)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			RequireTarget(request.Target);
			RequireValue(request.Source, "source");

			var arguments = new List<string> { "seed" };
			request.Target.AppendTo(arguments);
			arguments.Add("--source");
			arguments.Add(request.Source);
			AppendOptions(arguments, request.Options);
			return arguments;
		}

		public IReadOnlyList<string> BuildExportArguments(PitsExportRequest request)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			RequireTarget(request.Target);
			if (request.Json == (request.OutputDirectory != null))
				throw new ArgumentException(
					"Export requires exactly one output mode: JSON or an output directory.",
					nameof(request));

			var arguments = new List<string> { "export" };
			request.Target.AppendTo(arguments);
			if (request.Json)
				arguments.Add("--json");
			else
			{
				RequireValue(request.OutputDirectory.FullPath, "outputDirectory");
				arguments.Add("--out-dir");
				arguments.Add(request.OutputDirectory.FullPath);
			}
			AppendOptions(arguments, request.Options);
			return arguments;
		}

		public IReadOnlyList<string> BuildAuditArguments(PitsAuditRequest request)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			RequireTarget(request.Target);

			var arguments = new List<string> { "audit" };
			request.Target.AppendTo(arguments);
			AppendOptionalValue(arguments, "--machine", request.Machine, nameof(request.Machine));
			AppendOptionalValue(arguments, "--level", request.MinimumLevel, nameof(request.MinimumLevel));
			if (request.Json)
				arguments.Add("--json");
			AppendOptions(arguments, request.Options);
			return arguments;
		}

		public RaiSystemResult Seed(PitsSeedRequest request) => Run(BuildSeedArguments(request));
		public Task<RaiSystemResult> SeedAsync(PitsSeedRequest request, CancellationToken cancellationToken = default)
			=> RunAsync(BuildSeedArguments(request), cancellationToken);

		public RaiSystemResult Export(PitsExportRequest request) => Run(BuildExportArguments(request));
		public Task<RaiSystemResult> ExportAsync(PitsExportRequest request, CancellationToken cancellationToken = default)
			=> RunAsync(BuildExportArguments(request), cancellationToken);

		public RaiSystemResult Audit(PitsAuditRequest request) => Run(BuildAuditArguments(request));
		public Task<RaiSystemResult> AuditAsync(PitsAuditRequest request, CancellationToken cancellationToken = default)
			=> RunAsync(BuildAuditArguments(request), cancellationToken);

		public override RaiSystemResult Run(IEnumerable<string> arguments)
			=> base.RunAsync(WithManagedAssembly(arguments)).GetAwaiter().GetResult();

		public override Task<RaiSystemResult> RunAsync(
			IEnumerable<string> arguments,
			CancellationToken cancellationToken = default)
			=> base.RunAsync(WithManagedAssembly(arguments), cancellationToken);

		public override RaiSystemResult Run(IEnumerable<string> arguments, int timeoutMilliseconds)
			=> base.Run(WithManagedAssembly(arguments), timeoutMilliseconds);

		public override Task<RaiSystemResult> RunAsync(
			IEnumerable<string> arguments,
			int timeoutMilliseconds,
			CancellationToken cancellationToken = default)
			=> base.RunAsync(WithManagedAssembly(arguments), timeoutMilliseconds, cancellationToken);

		private IEnumerable<string> WithManagedAssembly(IEnumerable<string> arguments)
		{
			if (managedAssembly != null)
				yield return managedAssembly.FullName;
			if (arguments == null)
				yield break;
			foreach (var argument in arguments)
				yield return argument;
		}

		private static void AppendOptions(List<string> arguments, PitsCommandOptions options)
		{
			if (options == null)
				return;
			if (options.PitRoot != null)
			{
				RequireValue(options.PitRoot.FullPath, nameof(options.PitRoot));
				arguments.Add("--pitroot");
				arguments.Add(options.PitRoot.FullPath);
			}
			AppendOptionalValue(arguments, "--cloud", options.CloudProvider, nameof(options.CloudProvider));
			if (options.Debug) arguments.Add("--debug");
			if (options.NoLogo) arguments.Add("--nologo");
			if (options.RetainWindow) arguments.Add("--retain-window");
		}

		private static void AppendOptionalValue(
			List<string> arguments,
			string option,
			string value,
			string parameterName)
		{
			if (value == null)
				return;
			RequireValue(value, parameterName);
			arguments.Add(option);
			arguments.Add(value);
		}

		private static void RequireTarget(PitsTarget target)
		{
			if (target == null)
				throw new ArgumentException("A pit or WWWA target is required.", nameof(target));
		}

		private static void RequireValue(string value, string parameterName)
		{
			if (string.IsNullOrWhiteSpace(value))
				throw new ArgumentException($"A value is required for {parameterName}.", parameterName);
			if (value.StartsWith("-", StringComparison.Ordinal))
				throw new ArgumentException($"The value for {parameterName} cannot be parsed as an option.", parameterName);
		}
	}
}
