using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OsLib
{
	public sealed record AmafuCommandOptions
	{
		public string RemoteTarget { get; init; }
		public bool NoLogo { get; init; }
	}

	public sealed record AmafuDetectRequest
	{
		public string CloudProvider { get; init; }
		public bool All { get; init; }
		public bool Json { get; init; }
		public AmafuCommandOptions Options { get; init; }
	}

	public sealed record AmafuInitRequest
	{
		public string CloudProvider { get; init; }
		public bool Json { get; init; }
		public bool CreateLinks { get; init; }
		public AmafuCommandOptions Options { get; init; }
	}

	/// <summary>Typed command boundary for the Amafu cloud-drive CLI.</summary>
	public sealed class AmafuCommand : CliCommand
	{
		public AmafuCommand(string commandName = "amafu") : base(commandName, packageName: "amafu") { }

		public AmafuCommand OverSsh(string remoteTarget) => base.OverSsh<AmafuCommand>(remoteTarget);

		public IReadOnlyList<string> BuildDetectArguments(AmafuDetectRequest request)
		{
			if (request == null) throw new ArgumentNullException(nameof(request));
			var arguments = new List<string> { "detect" };
			AppendCloud(arguments, request.CloudProvider);
			if (request.All) arguments.Add("--all");
			if (request.Json) arguments.Add("--json");
			AppendOptions(arguments, request.Options);
			return arguments;
		}

		public IReadOnlyList<string> BuildInitArguments(AmafuInitRequest request)
		{
			if (request == null) throw new ArgumentNullException(nameof(request));
			var arguments = new List<string> { "init" };
			AppendCloud(arguments, request.CloudProvider);
			if (request.CreateLinks) arguments.Add("--create-links");
			if (request.Json) arguments.Add("--json");
			AppendOptions(arguments, request.Options);
			return arguments;
		}

		public RaiSystemResult Detect(AmafuDetectRequest request)
			=> RunWithRemoteTarget(BuildDetectArguments(request), request?.Options?.RemoteTarget);
		public Task<RaiSystemResult> DetectAsync(AmafuDetectRequest request, CancellationToken cancellationToken = default)
			=> RunWithRemoteTargetAsync(BuildDetectArguments(request), request?.Options?.RemoteTarget, cancellationToken: cancellationToken);
		public RaiSystemResult Init(AmafuInitRequest request)
			=> RunWithRemoteTarget(BuildInitArguments(request), request?.Options?.RemoteTarget);
		public Task<RaiSystemResult> InitAsync(AmafuInitRequest request, CancellationToken cancellationToken = default)
			=> RunWithRemoteTargetAsync(BuildInitArguments(request), request?.Options?.RemoteTarget, cancellationToken: cancellationToken);

		private static void AppendCloud(List<string> arguments, string cloudProvider)
		{
			if (cloudProvider is null) return;
			if (string.IsNullOrWhiteSpace(cloudProvider) || cloudProvider.StartsWith("-", StringComparison.Ordinal))
				throw new ArgumentException("CloudProvider must be a non-option value.", nameof(cloudProvider));
			arguments.AddRange(["--cloud", cloudProvider]);
		}

		private static void AppendOptions(List<string> arguments, AmafuCommandOptions options)
		{
			if (options?.NoLogo == true) arguments.Add("--nologo");
		}
	}
}
