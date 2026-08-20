using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OsLib
{
	public sealed record IorgCommandOptions
	{
		public string Subscriber { get; init; }
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

	public sealed record IorgCleanRequest(string ShortName, RaiPath Root)
	{
		public bool Cache { get; init; }
		public bool Force { get; init; }
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
			RequireConvention(request.PathConvention, nameof(request.PathConvention));
			RequireConvention(request.NamingConvention, nameof(request.NamingConvention));

			var arguments = new List<string>
			{
				"organize",
				"--source", request.Source.FullPath,
				"--root", request.Root.FullPath,
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
			RequireValue(request.ShortName, nameof(request.ShortName));
			if (request.ShortName.Contains('/') || request.ShortName.Contains('\\'))
				throw new ArgumentException("ShortName must be an item name, not a path.", nameof(request.ShortName));
			RequirePath(request.Root, nameof(request.Root));

			var arguments = new List<string>
			{
				"clean", request.ShortName,
				"--root", request.Root.FullPath
			};
			AppendOptions(arguments, request.Options);
			if (request.Cache) arguments.Add("--cache");
			if (request.Force) arguments.Add("--force");
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
			AppendOptionalValue(arguments, "--subscriber", options.Subscriber, nameof(options.Subscriber));
			AppendOptionalValue(arguments, "--cloud", options.CloudProvider, nameof(options.CloudProvider));
			if (options.Debug) arguments.Add("--debug");
			if (options.NoLogo) arguments.Add("--nologo");
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

		private static void RequireConvention(int value, string parameterName)
		{
			if (value is < 1 or > 3)
				throw new ArgumentOutOfRangeException(parameterName, value, "Convention must be between 1 and 3.");
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
