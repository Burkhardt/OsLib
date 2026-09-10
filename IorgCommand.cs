using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OsLib
{
	public sealed record IorgCommandOptions
	{
		private string tenant;

		/// <summary>Subscriber/tenant below the selected ImageTree root.</summary>
		public string Tenant { get => tenant; init => tenant = value; }
		/// <summary>Compatibility alias for <see cref="Tenant"/>.</summary>
		public string Subscriber { get => tenant; init => tenant = value; }
		/// <summary>
		/// Treat the request's Root as an application root and emit <c>--app</c>;
		/// iorg appends its conventional <c>Image</c> segment.
		/// </summary>
		public bool RootIsApplicationRoot { get; init; }
		public string CloudProvider { get; init; }
		public bool Debug { get; init; }
		public bool NoLogo { get; init; }
	}

	public sealed record IorgOrganizeRequest(
		RaiPath Source,
		RaiPath Root,
		int PathConvention,
		int NamingConvention)
	{
		public IorgCommandOptions Options { get; init; }
	}

	public sealed record IorgCleanRequest(string ItemId, RaiPath Root)
	{
		public bool Cache { get; init; }
		public bool Force { get; init; }
		public IorgCommandOptions Options { get; init; }
	}

	public sealed record IorgListRequest(string FileNamePattern, RaiPath Root)
	{
		public bool Json { get; init; }
		public bool Quiet { get; init; }
		public IorgCommandOptions Options { get; init; }
	}

	public sealed record IorgMoveRequest(
		string SourceItemId,
		string TargetItemId,
		RaiPath Root,
		PathConventionType PathConvention = PathConventionType.ItemIdTree8x2)
	{
		public bool Json { get; init; }
		public bool Quiet { get; init; }
		public IorgCommandOptions Options { get; init; }
	}

	public sealed class IorgCommand : CliCommand
	{
		private readonly RaiPath commandPath;
		private readonly string commandName;
		private readonly RaiFile managedAssembly;

		public IorgCommand(RaiPath commandPath = null, string commandName = "iorg")
			: base(string.IsNullOrWhiteSpace(commandName) ? "iorg" : commandName)
		{
			this.commandPath = commandPath;
			this.commandName = string.IsNullOrWhiteSpace(commandName) ? "iorg" : commandName;
		}

		private IorgCommand(RaiFile managedAssembly, string hostCommand, bool managed)
			: base(string.IsNullOrWhiteSpace(hostCommand) ? "dotnet" : hostCommand)
		{
			if (managedAssembly == null || string.IsNullOrWhiteSpace(managedAssembly.FullName))
				throw new ArgumentException("A managed iorg assembly is required.", nameof(managedAssembly));
			this.managedAssembly = managedAssembly;
			commandName = string.IsNullOrWhiteSpace(hostCommand) ? "dotnet" : hostCommand;
		}

		public static IorgCommand ForManagedAssembly(RaiFile managedAssembly, string hostCommand = "dotnet")
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

		public IReadOnlyList<string> BuildOrganizeArguments(IorgOrganizeRequest request)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			RequirePath(request.Source, nameof(request.Source));
			RequirePath(request.Root, nameof(request.Root));
			RequireConvention(request.PathConvention, nameof(request.PathConvention), 4);
			RequireConvention(request.NamingConvention, nameof(request.NamingConvention), 3);

			var arguments = new List<string>
			{
				"organize",
				"--source", request.Source.FullPath,
				RootOption(request.Options), request.Root.FullPath,
				"--pathconv", request.PathConvention.ToString(),
				"--nameconv", request.NamingConvention.ToString()
			};
			AppendOptions(arguments, request.Options);
			return arguments;
		}

		public IReadOnlyList<string> BuildCleanArguments(IorgCleanRequest request)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			RequirePath(request.Root, nameof(request.Root));
			if (request.Cache)
			{
				if (!string.IsNullOrWhiteSpace(request.ItemId))
					throw new ArgumentException("Cache cleanup does not accept an ItemId.", nameof(request.ItemId));
				if (request.Force)
					throw new ArgumentException("Cache cleanup is already explicit and does not accept Force.", nameof(request.Force));
			}
			else
			{
				RequireItemId(request.ItemId, nameof(request.ItemId));
			}

			var arguments = new List<string> { "clean" };
			if (!request.Cache)
				arguments.Add(request.ItemId);
			arguments.AddRange([RootOption(request.Options), request.Root.FullPath]);
			AppendOptions(arguments, request.Options);
			if (request.Cache) arguments.Add("--cache");
			if (request.Force) arguments.Add("--force");
			return arguments;
		}

		public IReadOnlyList<string> BuildListArguments(IorgListRequest request)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			RequireValue(request.FileNamePattern, nameof(request.FileNamePattern));
			if (request.FileNamePattern.Contains('/') || request.FileNamePattern.Contains('\\'))
				throw new ArgumentException("FileNamePattern must be a filename pattern, not a path.", nameof(request.FileNamePattern));
			RequirePath(request.Root, nameof(request.Root));
			RequireOutputMode(request.Json, request.Quiet);

			var arguments = new List<string>
			{
				"list", request.FileNamePattern,
				RootOption(request.Options), request.Root.FullPath
			};
			AppendOptions(arguments, request.Options);
			if (request.Json) arguments.Add("--json");
			if (request.Quiet) arguments.Add("--quiet");
			return arguments;
		}

		public IReadOnlyList<string> BuildMoveArguments(IorgMoveRequest request)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			RequireItemId(request.SourceItemId, nameof(request.SourceItemId));
			if (request.TargetItemId != null)
				RequireItemId(request.TargetItemId, nameof(request.TargetItemId));
			RequirePath(request.Root, nameof(request.Root));
			if (!Enum.IsDefined(request.PathConvention))
				throw new ArgumentOutOfRangeException(nameof(request.PathConvention), request.PathConvention, "Unknown path convention.");
			RequireOutputMode(request.Json, request.Quiet);

			var arguments = new List<string>
			{
				"move", request.SourceItemId
			};
			if (!string.IsNullOrWhiteSpace(request.TargetItemId))
				arguments.Add(request.TargetItemId);
			arguments.AddRange([RootOption(request.Options), request.Root.FullPath, "--pathconv", request.PathConvention.ToString()]);
			AppendOptions(arguments, request.Options);
			if (request.Json) arguments.Add("--json");
			if (request.Quiet) arguments.Add("--quiet");
			return arguments;
		}

		public RaiSystemResult Organize(IorgOrganizeRequest request) => Run(BuildOrganizeArguments(request));
		public Task<RaiSystemResult> OrganizeAsync(
			IorgOrganizeRequest request,
			CancellationToken cancellationToken = default)
			=> RunAsync(BuildOrganizeArguments(request), cancellationToken);

		public RaiSystemResult Clean(IorgCleanRequest request) => Run(BuildCleanArguments(request));
		public Task<RaiSystemResult> CleanAsync(
			IorgCleanRequest request,
			CancellationToken cancellationToken = default)
			=> RunAsync(BuildCleanArguments(request), cancellationToken);

		public RaiSystemResult List(IorgListRequest request) => Run(BuildListArguments(request));
		public Task<RaiSystemResult> ListAsync(
			IorgListRequest request,
			CancellationToken cancellationToken = default)
			=> RunAsync(BuildListArguments(request), cancellationToken);

		public RaiSystemResult Move(IorgMoveRequest request) => Run(BuildMoveArguments(request));
		public Task<RaiSystemResult> MoveAsync(
			IorgMoveRequest request,
			CancellationToken cancellationToken = default)
			=> RunAsync(BuildMoveArguments(request), cancellationToken);

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

		private static void AppendOptions(List<string> arguments, IorgCommandOptions options)
		{
			if (options == null)
				return;
			AppendOptionalValue(arguments, "--tenant", options.Tenant, nameof(options.Tenant));
			AppendOptionalValue(arguments, "--cloud", options.CloudProvider, nameof(options.CloudProvider));
			if (options.Debug) arguments.Add("--debug");
			if (options.NoLogo) arguments.Add("--nologo");
		}

		private static string RootOption(IorgCommandOptions options)
		{
			if (options?.RootIsApplicationRoot != true) return "--root";
			if (string.IsNullOrWhiteSpace(options.Tenant))
				throw new ArgumentException(
					"Application-root addressing requires Tenant.",
					nameof(options.Tenant));
			return "--app";
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

		private static void RequirePath(RaiPath path, string parameterName)
		{
			if (path == null || string.IsNullOrWhiteSpace(path.FullPath))
				throw new ArgumentException($"A path is required for {parameterName}.", parameterName);
		}

		private static void RequireConvention(int value, string parameterName, int maximum)
		{
			if (value < 1 || value > maximum)
				throw new ArgumentOutOfRangeException(parameterName, value, $"Convention must be between 1 and {maximum}.");
		}

		private static void RequireValue(string value, string parameterName)
		{
			if (string.IsNullOrWhiteSpace(value))
				throw new ArgumentException($"A value is required for {parameterName}.", parameterName);
			if (value.StartsWith("-", StringComparison.Ordinal))
				throw new ArgumentException($"The value for {parameterName} cannot be parsed as an option.", parameterName);
		}

		private static void RequireItemId(string value, string parameterName)
		{
			RequireValue(value, parameterName);
			if (value.Contains('/') || value.Contains('\\'))
				throw new ArgumentException("ItemId must be a plain item identifier, not a path.", parameterName);
		}

		private static void RequireOutputMode(bool json, bool quiet)
		{
			if (json && quiet)
				throw new ArgumentException("Use only one of Json or Quiet output mode.");
		}
	}
}
