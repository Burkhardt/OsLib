using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
		/// <summary>
		/// Optional historical projection cutoff. When supplied, <c>pits</c> emits
		/// the CR017 point-in-time provenance envelope.
		/// </summary>
		public DateTimeOffset? At { get; init; }
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

	public sealed record PitsDeletePropertyRequest(
		string PitName,
		string ItemId,
		string PropertyPath)
	{
		public PitsCommandOptions Options { get; init; }
	}

	public sealed record PitsDeleteItemRequest(string PitName, string ItemId)
	{
		public PitsCommandOptions Options { get; init; }
	}

	public sealed record PitsMaintainRequest
	{
		public PitsMaintainRequest(PitsTarget target)
		{
			Target = target;
		}

		public PitsTarget Target { get; }
		public bool Apply { get; init; }
		public bool Json { get; init; }
		public bool PruneProcessFlags { get; init; }
		public TimeSpan? OlderThan { get; init; }
		public bool RepairLegacyExtensions { get; init; }
		public PitsCommandOptions Options { get; init; }
	}

	public sealed class PitsCommand : CliCommand
	{
		private sealed class GateEntry
		{
			public readonly SemaphoreSlim Semaphore = new(1, 1);
			public int Users;
		}

		private sealed class GateLease : IDisposable
		{
			private List<(string Key, GateEntry Entry)> acquired;

			public GateLease(List<(string Key, GateEntry Entry)> acquired)
			{
				this.acquired = acquired;
			}

			public void Dispose()
			{
				var entries = Interlocked.Exchange(ref acquired, null);
				if (entries == null) return;
				for (var i = entries.Count - 1; i >= 0; i--)
					ReleaseGate(entries[i].Key, entries[i].Entry, acquired: true);
			}
		}

		private static readonly object GateRegistryLock = new();
		private static readonly Dictionary<string, GateEntry> GateRegistry = new(StringComparer.Ordinal);
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
			if (request.At is { } at)
			{
				arguments.Add("--at");
				arguments.Add(at.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
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

		public IReadOnlyList<string> BuildDeletePropertyArguments(PitsDeletePropertyRequest request)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			RequireValue(request.PitName, nameof(request.PitName));
			RequireValue(request.ItemId, nameof(request.ItemId));
			RequirePropertyPath(request.PropertyPath);

			var arguments = new List<string>
			{
				"delete-property", request.PitName, request.ItemId, request.PropertyPath
			};
			AppendOptions(arguments, request.Options);
			return arguments;
		}

		public IReadOnlyList<string> BuildDeleteItemArguments(PitsDeleteItemRequest request)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			RequireValue(request.PitName, nameof(request.PitName));
			RequireValue(request.ItemId, nameof(request.ItemId));

			var arguments = new List<string> { "delete-item", request.PitName, request.ItemId };
			AppendOptions(arguments, request.Options);
			return arguments;
		}

		public IReadOnlyList<string> BuildMaintainArguments(PitsMaintainRequest request)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			RequireTarget(request.Target);
			if (request.PruneProcessFlags && !request.Apply)
				throw new ArgumentException("Process-flag pruning requires Apply.", nameof(request));
			if (request.PruneProcessFlags && request.OlderThan is null)
				throw new ArgumentException("Process-flag pruning requires OlderThan.", nameof(request));
			if (!request.PruneProcessFlags && request.OlderThan is not null)
				throw new ArgumentException("OlderThan applies only to process-flag pruning.", nameof(request));
			if (request.OlderThan is { } olderThan && olderThan <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException(nameof(request), "OlderThan must be positive.");
			if (request.RepairLegacyExtensions && !request.Apply)
				throw new ArgumentException("Legacy-extension repair requires Apply.", nameof(request));

			var arguments = new List<string> { "maintain" };
			request.Target.AppendTo(arguments);
			if (request.Apply) arguments.Add("--apply");
			if (request.PruneProcessFlags)
			{
				arguments.Add("--prune-process-flags");
				arguments.Add("--older-than");
				arguments.Add(request.OlderThan.Value.ToString("c", CultureInfo.InvariantCulture));
			}
			if (request.RepairLegacyExtensions) arguments.Add("--repair-legacy-extensions");
			if (request.Json) arguments.Add("--json");
			AppendOptions(arguments, request.Options);
			return arguments;
		}

		public RaiSystemResult Seed(PitsSeedRequest request)
		{
			var arguments = BuildSeedArguments(request);
			return RunCoordinated(request.Target, request.Options, arguments);
		}
		public Task<RaiSystemResult> SeedAsync(PitsSeedRequest request, CancellationToken cancellationToken = default)
		{
			var arguments = BuildSeedArguments(request);
			return RunCoordinatedAsync(request.Target, request.Options, arguments, cancellationToken);
		}

		public RaiSystemResult Export(PitsExportRequest request)
		{
			var arguments = BuildExportArguments(request);
			return RunCoordinated(request.Target, request.Options, arguments);
		}
		public Task<RaiSystemResult> ExportAsync(PitsExportRequest request, CancellationToken cancellationToken = default)
		{
			var arguments = BuildExportArguments(request);
			return RunCoordinatedAsync(request.Target, request.Options, arguments, cancellationToken);
		}

		public RaiSystemResult Audit(PitsAuditRequest request)
		{
			var arguments = BuildAuditArguments(request);
			return RunCoordinated(request.Target, request.Options, arguments);
		}
		public Task<RaiSystemResult> AuditAsync(PitsAuditRequest request, CancellationToken cancellationToken = default)
		{
			var arguments = BuildAuditArguments(request);
			return RunCoordinatedAsync(request.Target, request.Options, arguments, cancellationToken);
		}

		public RaiSystemResult DeleteProperty(PitsDeletePropertyRequest request)
		{
			var arguments = BuildDeletePropertyArguments(request);
			return RunCoordinated(PitsTarget.Pit(request.PitName), request.Options, arguments);
		}
		public Task<RaiSystemResult> DeletePropertyAsync(
			PitsDeletePropertyRequest request,
			CancellationToken cancellationToken = default)
		{
			var arguments = BuildDeletePropertyArguments(request);
			return RunCoordinatedAsync(PitsTarget.Pit(request.PitName), request.Options,
				arguments, cancellationToken);
		}

		public RaiSystemResult DeleteItem(PitsDeleteItemRequest request)
		{
			var arguments = BuildDeleteItemArguments(request);
			return RunCoordinated(PitsTarget.Pit(request.PitName), request.Options, arguments);
		}
		public Task<RaiSystemResult> DeleteItemAsync(
			PitsDeleteItemRequest request,
			CancellationToken cancellationToken = default)
		{
			var arguments = BuildDeleteItemArguments(request);
			return RunCoordinatedAsync(PitsTarget.Pit(request.PitName), request.Options,
				arguments, cancellationToken);
		}

		public RaiSystemResult Maintain(PitsMaintainRequest request)
		{
			var arguments = BuildMaintainArguments(request);
			return RunCoordinated(request.Target, request.Options, arguments);
		}
		public Task<RaiSystemResult> MaintainAsync(
			PitsMaintainRequest request,
			CancellationToken cancellationToken = default)
		{
			var arguments = BuildMaintainArguments(request);
			return RunCoordinatedAsync(request.Target, request.Options,
				arguments, cancellationToken);
		}

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

		private RaiSystemResult RunCoordinated(
			PitsTarget target,
			PitsCommandOptions options,
			IEnumerable<string> arguments)
			=> RunCoordinatedAsync(target, options, arguments).GetAwaiter().GetResult();

		private async Task<RaiSystemResult> RunCoordinatedAsync(
			PitsTarget target,
			PitsCommandOptions options,
			IEnumerable<string> arguments,
			CancellationToken cancellationToken = default)
		{
			using var gate = await AcquireGatesAsync(target, options, cancellationToken).ConfigureAwait(false);
			return await RunAsync(arguments, cancellationToken).ConfigureAwait(false);
		}

		private static async Task<GateLease> AcquireGatesAsync(
			PitsTarget target,
			PitsCommandOptions options,
			CancellationToken cancellationToken)
		{
			RequireTarget(target);
			var acquired = new List<(string Key, GateEntry Entry)>();
			try
			{
				foreach (var key in GateKeys(target, options))
				{
					var entry = RegisterGate(key);
					try
					{
						await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
						acquired.Add((key, entry));
					}
					catch
					{
						ReleaseGate(key, entry, acquired: false);
						throw;
					}
				}
				return new GateLease(acquired);
			}
			catch
			{
				for (var i = acquired.Count - 1; i >= 0; i--)
					ReleaseGate(acquired[i].Key, acquired[i].Entry, acquired: true);
				throw;
			}
		}

		private static IReadOnlyList<string> GateKeys(PitsTarget target, PitsCommandOptions options)
		{
			var provider = options?.CloudProvider?.Trim().ToUpperInvariant() ?? string.Empty;
			var root = options?.PitRoot?.FullPath?.TrimEnd('/', '\\') ?? string.Empty;
			var route = $"{provider}\u001f{root}".ToUpperInvariant();
			var pits = target.IsWwwa
				? new[] { "Person", "Object", "Place", "Activity" }
				: new[] { target.PitName };
			return pits.Select(name => $"{route}\u001f{name.ToUpperInvariant()}").ToArray();
		}

		private static GateEntry RegisterGate(string key)
		{
			lock (GateRegistryLock)
			{
				if (!GateRegistry.TryGetValue(key, out var entry))
				{
					entry = new GateEntry();
					GateRegistry.Add(key, entry);
				}
				entry.Users++;
				return entry;
			}
		}

		private static void ReleaseGate(string key, GateEntry entry, bool acquired)
		{
			if (acquired) entry.Semaphore.Release();
			lock (GateRegistryLock)
			{
				entry.Users--;
				if (entry.Users == 0 && GateRegistry.TryGetValue(key, out var current) &&
					ReferenceEquals(entry, current))
				{
					GateRegistry.Remove(key);
					entry.Semaphore.Dispose();
				}
			}
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

		private static void RequirePropertyPath(string propertyPath)
		{
			RequireValue(propertyPath, nameof(propertyPath));
			if (propertyPath.Split('.', StringSplitOptions.None).Any(string.IsNullOrWhiteSpace))
				throw new ArgumentException(
					"A property path must contain non-empty dot-delimited property names.",
					nameof(propertyPath));
		}
	}
}
