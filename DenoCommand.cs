using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OsLib
{
	public sealed record DenoCommandOptions
	{
		public string RemoteTarget { get; init; }
		public int TimeoutMilliseconds { get; init; } = 120000;
	}

	public sealed record DenoRunRequest(RaiPath ScriptPath)
	{
		public string ContextJson { get; init; }
		public RaiPath ContextFile { get; init; }
		public string AllowNet { get; init; }
		public string AllowRead { get; init; }
		public string DenyRead { get; init; }
		public bool AllowEnv { get; init; }
		public bool AllowRun { get; init; }
		public IReadOnlyList<string> AdditionalArgs { get; init; }
		public DenoCommandOptions Options { get; init; }
	}

	/// <summary>Typed boundary for sandboxed local or SSH-dispatched Deno scripts.</summary>
	public sealed class DenoCommand : CliCommand
	{
		private readonly RaiPath commandPath;
		private readonly string commandName;

		public DenoCommand(RaiPath commandPath = null, string commandName = "deno")
			: base(string.IsNullOrWhiteSpace(commandName) ? "deno" : commandName)
		{
			this.commandPath = commandPath;
			this.commandName = string.IsNullOrWhiteSpace(commandName) ? "deno" : commandName;
		}

		public DenoCommand OverSsh(string remoteTarget) => base.OverSsh<DenoCommand>(remoteTarget);

		public override IEnumerable<string> CandidateExecutables
		{
			get
			{
				if (commandPath != null)
					yield return new RaiFile(commandName) { Path = commandPath }.FullName;
				yield return commandName;
			}
		}

		public IReadOnlyList<string> BuildRunArguments(DenoRunRequest request)
		{
			if (request == null) throw new ArgumentNullException(nameof(request));
			RequirePath(request.ScriptPath, nameof(request.ScriptPath));
			if (request.ContextJson is not null && request.ContextFile is not null)
				throw new ArgumentException("Specify ContextJson or ContextFile, not both.", nameof(request));
			if (request.Options?.TimeoutMilliseconds <= 0)
				throw new ArgumentOutOfRangeException(nameof(request.Options.TimeoutMilliseconds));

			var arguments = new List<string> { "run" };
			AppendPermission(arguments, "--allow-net", request.AllowNet);
			AppendPermission(arguments, "--allow-read", request.AllowRead);
			AppendPermission(arguments, "--deny-read", request.DenyRead);
			if (request.AllowEnv) arguments.Add("--allow-env");
			if (request.AllowRun) arguments.Add("--allow-run");
			arguments.Add(request.ScriptPath.FullPath);
			if (request.ContextJson is not null) arguments.AddRange(["--context", request.ContextJson]);
			if (request.ContextFile is not null) arguments.AddRange(["--context-file", request.ContextFile.FullPath]);
			if (request.AdditionalArgs is not null) arguments.AddRange(request.AdditionalArgs);
			return arguments;
		}

		public RaiSystemResult Run(DenoRunRequest request)
		{
			var arguments = BuildRunArguments(request);
			return RunWithRemoteTarget(arguments, request.Options?.RemoteTarget, request.Options?.TimeoutMilliseconds ?? 120000);
		}

		public Task<RaiSystemResult> RunAsync(DenoRunRequest request, CancellationToken cancellationToken = default)
		{
			var arguments = BuildRunArguments(request);
			return RunWithRemoteTargetAsync(
				arguments,
				request.Options?.RemoteTarget,
				request.Options?.TimeoutMilliseconds ?? 120000,
				cancellationToken);
		}

		private static void AppendPermission(List<string> arguments, string name, string value)
		{
			if (value is null) return;
			if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"A value is required for {name}.");
			arguments.Add($"{name}={value}");
		}

		private static void RequirePath(RaiPath path, string parameterName)
		{
			if (path == null || string.IsNullOrWhiteSpace(path.FullPath))
				throw new ArgumentException("A path is required.", parameterName);
		}
	}
}
