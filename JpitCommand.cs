using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OsLib
{
	public sealed record JpitCommandOptions
	{
		public RaiPath Root { get; init; }
		public string CloudProvider { get; init; }
		public string RemoteTarget { get; init; }
		public bool NoLogo { get; init; }
		public bool RetainWindow { get; init; }
	}

	public sealed record JpitPutRequest(string PitName, string Source)
	{
		public bool RequireExisting { get; init; }
		public JpitCommandOptions Options { get; init; }
	}

	public sealed record JpitListRequest(RaiPath Root)
	{
		public bool All { get; init; }
		public bool Long { get; init; }
		public bool Json { get; init; }
		public JpitCommandOptions Options { get; init; }
	}

	public sealed record JpitDeletePropertyRequest(string PitName, string ItemId, string PropertyPath)
	{
		public JpitCommandOptions Options { get; init; }
	}

	/// <summary>Typed command boundary for the jsonpit-python <c>jpit</c> CLI.</summary>
	public sealed class JpitCommand : CliCommand
	{
		public JpitCommand(string commandName = "jpit") : base(commandName, packageName: "jsonpit") { }

		public JpitCommand OverSsh(string remoteTarget) => base.OverSsh<JpitCommand>(remoteTarget);

		public IReadOnlyList<string> BuildPutArguments(JpitPutRequest request)
		{
			if (request == null) throw new ArgumentNullException(nameof(request));
			RequireValue(request.PitName, nameof(request.PitName));
			RequireValue(request.Source, nameof(request.Source), allowStandardInput: true);
			var arguments = new List<string> { "put", request.PitName, request.Source };
			if (request.RequireExisting) arguments.Add("--require-existing");
			AppendOptions(arguments, request.Options);
			return arguments;
		}

		public IReadOnlyList<string> BuildListArguments(JpitListRequest request)
		{
			if (request == null) throw new ArgumentNullException(nameof(request));
			RequirePath(request.Root, nameof(request.Root));
			var arguments = new List<string> { "list", "--root", request.Root.FullPath };
			if (request.All) arguments.Add("--all");
			if (request.Long) arguments.Add("--long");
			if (request.Json) arguments.Add("--json");
			AppendOptions(arguments, request.Options, includeRoot: false);
			return arguments;
		}

		public IReadOnlyList<string> BuildDeletePropertyArguments(JpitDeletePropertyRequest request)
		{
			if (request == null) throw new ArgumentNullException(nameof(request));
			RequireValue(request.PitName, nameof(request.PitName));
			RequireValue(request.ItemId, nameof(request.ItemId));
			if (string.IsNullOrWhiteSpace(request.PropertyPath) || request.PropertyPath.Split('.').Length == 0)
				throw new ArgumentException("PropertyPath is required.", nameof(request.PropertyPath));
			var arguments = new List<string> { "del-prop", request.PitName, request.ItemId, request.PropertyPath };
			AppendOptions(arguments, request.Options);
			return arguments;
		}

		public RaiSystemResult Put(JpitPutRequest request)
			=> RunWithRemoteTarget(BuildPutArguments(request), request?.Options?.RemoteTarget);
		public Task<RaiSystemResult> PutAsync(JpitPutRequest request, CancellationToken cancellationToken = default)
			=> RunWithRemoteTargetAsync(BuildPutArguments(request), request?.Options?.RemoteTarget, cancellationToken: cancellationToken);
		public RaiSystemResult List(JpitListRequest request)
			=> RunWithRemoteTarget(BuildListArguments(request), request?.Options?.RemoteTarget);
		public Task<RaiSystemResult> ListAsync(JpitListRequest request, CancellationToken cancellationToken = default)
			=> RunWithRemoteTargetAsync(BuildListArguments(request), request?.Options?.RemoteTarget, cancellationToken: cancellationToken);
		public RaiSystemResult DeleteProperty(JpitDeletePropertyRequest request)
			=> RunWithRemoteTarget(BuildDeletePropertyArguments(request), request?.Options?.RemoteTarget);
		public Task<RaiSystemResult> DeletePropertyAsync(JpitDeletePropertyRequest request, CancellationToken cancellationToken = default)
			=> RunWithRemoteTargetAsync(BuildDeletePropertyArguments(request), request?.Options?.RemoteTarget, cancellationToken: cancellationToken);

		private static void AppendOptions(List<string> arguments, JpitCommandOptions options, bool includeRoot = true)
		{
			if (options is null) return;
			if (includeRoot && options.Root is not null)
			{
				RequirePath(options.Root, nameof(options.Root));
				arguments.AddRange(["--root", options.Root.FullPath]);
			}
			if (options.CloudProvider is not null)
			{
				RequireValue(options.CloudProvider, nameof(options.CloudProvider));
				arguments.AddRange(["--cloud", options.CloudProvider]);
			}
			if (options.RetainWindow) arguments.Add("--retain-window");
			if (options.NoLogo) arguments.Add("--nologo");
		}

		private static void RequirePath(RaiPath path, string parameterName)
		{
			if (path == null || string.IsNullOrWhiteSpace(path.FullPath))
				throw new ArgumentException("A path is required.", parameterName);
		}

		private static void RequireValue(string value, string parameterName, bool allowStandardInput = false)
		{
			if (string.IsNullOrWhiteSpace(value) || (!allowStandardInput && value.StartsWith("-", StringComparison.Ordinal)) ||
				(allowStandardInput && value != "-" && value.StartsWith("-", StringComparison.Ordinal)))
				throw new ArgumentException("A non-option value is required.", parameterName);
		}
	}
}
