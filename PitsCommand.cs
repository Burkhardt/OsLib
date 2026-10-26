using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OsLib
{
	/// <summary>Addressing, presentation, and execution options shared by typed pits requests.</summary>
	public sealed record PitsCommandOptions
	{
		/// <summary>Bare tenant directory name passed unchanged as <c>-r</c>; mutually exclusive with <see cref="PitRoot"/>.</summary>
		public string Tenant { get; init; }
		/// <summary>Explicit filesystem root containing pit directories. Mutually exclusive with Tenant; omit CloudProvider when addressing a local filesystem path.</summary>
		public RaiPath PitRoot { get; init; }
		/// <summary>Configured cloud provider passed as --cloud. Required for tenant-scoped mutations; the wrapper never selects a default provider.</summary>
		public string CloudProvider { get; init; }
		/// <summary>Optional SSH target for this typed invocation.</summary>
		public string RemoteTarget { get; init; }
		/// <summary>Whether to emit --debug for CLI diagnostics.</summary>
		public bool Debug { get; init; }
		/// <summary>Whether to emit --nologo to suppress the CLI banner.</summary>
		public bool NoLogo { get; init; }
		/// <summary>Whether to ask pits to retain its process activity window after execution.</summary>
		public bool RetainWindow { get; init; }
	}

	/// <summary>A validated single-pit target or the coordinated Person, Object, Place, and Activity target set.</summary>
	public sealed class PitsTarget
	{
		private PitsTarget(string pitName, bool isWwwa)
		{
			PitName = pitName;
			IsWwwa = isWwwa;
		}

		/// <summary>The validated pit name, or null for the WWWA target.</summary>
		public string PitName { get; }
		/// <summary>Whether the target addresses all four WWWA pits.</summary>
		public bool IsWwwa { get; }

		/// <summary>Creates a single-pit target without resolving the name as a filesystem path.</summary>
		public static PitsTarget Pit(string pitName)
		{
			RequireValue(pitName, nameof(pitName));
			return new PitsTarget(pitName, isWwwa: false);
		}

		/// <summary>Creates a target covering Person, Object, Place, and Activity.</summary>
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

	/// <summary>An immutable seed request created with a named factory and customized with a record with-expression.</summary>
	/// <example><code>
	/// var request = PitsSeedRequest.ForPit("Person", sourceFile) with
	/// {
	///     Options = new PitsCommandOptions { Tenant = "SampleTenant", CloudProvider = "OneDrive", NoLogo = true }
	/// };
	/// var result = new PitsCommand().Seed(request);
	/// </code></example>
	public sealed record PitsSeedRequest
	{
		private PitsSeedRequest(PitsTarget target, string source)
		{
			Target = target;
			Source = source;
		}

		/// <summary>The single-pit or WWWA target selected by the factory or constructor.</summary>
		public PitsTarget Target { get; }
		/// <summary>The absolute source filename, WWWA source directory, or a single hyphen for buffered standard input.</summary>
		public string Source { get; }
		/// <summary>Buffered payload sent to stdin when Source is "-".</summary>
		public string StandardInput { get; private init; }
		/// <summary>
		/// Requires every incoming entity ID to exist in current living state before
		/// <c>pits</c> opens the target for write (CR047). Emits the canonical
		/// <c>--require-existing</c> spelling; <c>--patch</c> remains a CLI-only alias.
		/// </summary>
		public bool RequireExisting { get; init; }
		/// <summary>Optional command settings. Use a with-expression to attach tenant or filesystem addressing to a factory-created request.</summary>
		public PitsCommandOptions Options { get; init; }

		/// <summary>Creates a seed request for one pit from a typed source file. Attach Options with a with-expression.</summary>
		public static PitsSeedRequest ForPit(string pitName, RaiFile source)
			=> new(PitsTarget.Pit(pitName), source?.FullName);

		/// <summary>Creates a single-pit seed request whose buffered payload is sent through --source -. Buffered stdin is supported for local execution.</summary>
		public static PitsSeedRequest FromStandardInput(string pitName, string payload)
			=> new(PitsTarget.Pit(pitName), "-") { StandardInput = payload ?? throw new ArgumentNullException(nameof(payload)) };

		/// <summary>Creates a bulk seed request from a typed source directory containing the WWWA input files.</summary>
		public static PitsSeedRequest ForWwwa(RaiPath sourceDirectory)
			=> new(PitsTarget.Wwwa(), sourceDirectory?.FullPath);
	}

	/// <summary>An immutable JSON or directory export request.</summary>
	public sealed record PitsExportRequest
	{
		private PitsExportRequest(PitsTarget target, RaiPath outputDirectory, bool json)
		{
			Target = target;
			OutputDirectory = outputDirectory;
			Json = json;
		}

		/// <summary>The single-pit or WWWA target selected by the factory or constructor.</summary>
		public PitsTarget Target { get; }
		/// <summary>The absolute export destination, or null when JSON is returned on stdout.</summary>
		public RaiPath OutputDirectory { get; }
		/// <summary>Whether the CLI returns machine-readable JSON on stdout.</summary>
		public bool Json { get; }
		/// <summary>
		/// Optional historical projection cutoff. When supplied, <c>pits</c> emits
		/// the CR017 point-in-time provenance envelope.
		/// </summary>
		public DateTimeOffset? At { get; init; }
		/// <summary>Optional command settings. Use a with-expression to attach tenant or filesystem addressing to a factory-created request.</summary>
		public PitsCommandOptions Options { get; init; }

		/// <summary>Creates an export request that writes to a typed output directory.</summary>
		public static PitsExportRequest ToDirectory(PitsTarget target, RaiPath outputDirectory)
			=> new(target, outputDirectory, json: false);

		/// <summary>Creates an export request that returns JSON on stdout.</summary>
		public static PitsExportRequest ToJson(PitsTarget target)
			=> new(target, outputDirectory: null, json: true);
	}

	/// <summary>A read-only recovery-event audit request.</summary>
	public sealed record PitsAuditRequest
	{
		/// <summary>Creates a request for the supplied target; customize optional settings with init properties.</summary>
		public PitsAuditRequest(PitsTarget target)
		{
			Target = target;
		}

		/// <summary>The single-pit or WWWA target selected by the factory or constructor.</summary>
		public PitsTarget Target { get; }
		/// <summary>Optional machine filter passed to the audit command.</summary>
		public string Machine { get; init; }
		/// <summary>Optional minimum event severity passed as --level.</summary>
		public string MinimumLevel { get; init; }
		/// <summary>Whether the CLI returns machine-readable JSON on stdout.</summary>
		public bool Json { get; init; }
		/// <summary>Optional command settings. Use a with-expression to attach tenant or filesystem addressing to a factory-created request.</summary>
		public PitsCommandOptions Options { get; init; }
	}

	/// <summary>A sparse property-deletion request for one item.</summary>
	public sealed record PitsDeletePropertyRequest(
		string PitName,
		string ItemId,
		string PropertyPath)
	{
		/// <summary>Optional command settings. Use a with-expression to attach tenant or filesystem addressing to a factory-created request.</summary>
		public PitsCommandOptions Options { get; init; }
	}

	/// <summary>A lifecycle deletion request for one item.</summary>
	public sealed record PitsDeleteItemRequest(string PitName, string ItemId)
	{
		/// <summary>Optional command settings. Use a with-expression to attach tenant or filesystem addressing to a factory-created request.</summary>
		public PitsCommandOptions Options { get; init; }
	}

	/// <summary>A maintenance preview or explicitly applied reconciliation request.</summary>
	public sealed record PitsMaintainRequest
	{
		/// <summary>Creates a request for the supplied target; customize optional settings with init properties.</summary>
		public PitsMaintainRequest(PitsTarget target)
		{
			Target = target;
		}

		/// <summary>The single-pit or WWWA target selected by the factory or constructor.</summary>
		public PitsTarget Target { get; }
		/// <summary>Whether maintenance changes are applied rather than previewed.</summary>
		public bool Apply { get; init; }
		/// <summary>Whether the CLI returns machine-readable JSON on stdout.</summary>
		public bool Json { get; init; }
		/// <summary>Whether applied maintenance prunes process flags older than OlderThan.</summary>
		public bool PruneProcessFlags { get; init; }
		/// <summary>Positive minimum age for process-flag pruning; requires Apply and PruneProcessFlags.</summary>
		public TimeSpan? OlderThan { get; init; }
		/// <summary>Whether applied maintenance repairs legacy event extensions.</summary>
		public bool RepairLegacyExtensions { get; init; }
		/// <summary>Preview or apply immutable compaction of loose recovery events.</summary>
		public bool ArchiveEvents { get; init; }
		/// <summary>Optional command settings. Use a with-expression to attach tenant or filesystem addressing to a factory-created request.</summary>
		public PitsCommandOptions Options { get; init; }
	}

	/// <summary>Runs tokenized pits requests with process-local concurrency gates for each execution target, cloud, root or tenant, and pit.</summary>
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

		/// <summary>Creates a pits wrapper; an optional executable directory and command name support configured installations and test hosts.</summary>
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

		/// <summary>Runs a compiled pits DLL through the specified host, normally dotnet; the DLL remains one argument token.</summary>
		public static PitsCommand ForManagedAssembly(RaiFile managedAssembly, string hostCommand = "dotnet")
			=> new(managedAssembly, hostCommand, managed: true);

		/// <summary>Selects an SSH execution target and returns this command for fluent composition. Source paths are interpreted on the remote host.</summary>
		public PitsCommand OverSsh(string remoteTarget) => base.OverSsh<PitsCommand>(remoteTarget);

		/// <summary>Executable candidates, including an explicitly supplied command directory before the command name on PATH.</summary>
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

		/// <summary>Validates a seed request and returns its exact argument tokens without starting a process. Tenant values are passed unchanged.</summary>
		/// <exception cref="ArgumentException">The request contains invalid or contradictory options.</exception>
		public IReadOnlyList<string> BuildSeedArguments(PitsSeedRequest request)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			RequireTarget(request.Target);
			if (request.Source != "-" || request.StandardInput is null) RequireValue(request.Source, "source");
			if (request.RequireExisting && request.Target.IsWwwa)
				throw new ArgumentException(
					"Strict patch mode applies to a single Pit target, not WWWA.",
					nameof(request));

			var arguments = new List<string> { "seed" };
			request.Target.AppendTo(arguments);
			arguments.Add("--source");
			arguments.Add(request.Source);
			if (request.RequireExisting)
				arguments.Add("--require-existing");
			AppendOptions(arguments, request.Options, mutating: true);
			return arguments;
		}

		/// <summary>Validates an export request and returns its exact argument tokens without starting a process.</summary>
		/// <exception cref="ArgumentException">The request contains invalid or contradictory options.</exception>
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

		/// <summary>Validates an audit request and returns its exact argument tokens without starting a process.</summary>
		/// <exception cref="ArgumentException">The request contains invalid or contradictory options.</exception>
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

		/// <summary>Validates a sparse property-deletion request and returns its exact argument tokens without starting a process.</summary>
		/// <exception cref="ArgumentException">The request contains invalid or contradictory options.</exception>
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
			AppendOptions(arguments, request.Options, mutating: true);
			return arguments;
		}

		/// <summary>Validates an item-deletion request and returns its exact argument tokens without starting a process.</summary>
		/// <exception cref="ArgumentException">The request contains invalid or contradictory options.</exception>
		public IReadOnlyList<string> BuildDeleteItemArguments(PitsDeleteItemRequest request)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			RequireValue(request.PitName, nameof(request.PitName));
			RequireValue(request.ItemId, nameof(request.ItemId));

			var arguments = new List<string> { "delete-item", request.PitName, request.ItemId };
			AppendOptions(arguments, request.Options, mutating: true);
			return arguments;
		}

		/// <summary>Validates maintenance options and returns exact argument tokens; destructive operations require Apply.</summary>
		/// <exception cref="ArgumentException">The request contains invalid or contradictory options.</exception>
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
			if (request.ArchiveEvents) arguments.Add("--archive-events");
			if (request.Json) arguments.Add("--json");
			AppendOptions(arguments, request.Options, mutating: request.Apply);
			return arguments;
		}

		/// <summary>Executes a validated seed request while holding its target gates and returns the captured exit code, stdout, and stderr.</summary>
		public RaiSystemResult Seed(PitsSeedRequest request)
		{
			return SeedAsync(request).GetAwaiter().GetResult();
		}
		/// <summary>Asynchronously executes a validated seed request while holding its target gates. Cancellation also applies while waiting for a gate.</summary>
		public async Task<RaiSystemResult> SeedAsync(PitsSeedRequest request, CancellationToken cancellationToken = default)
		{
			var arguments = BuildSeedArguments(request);
			if (request.StandardInput is null)
				return await RunCoordinatedAsync(request.Target, request.Options, arguments, cancellationToken).ConfigureAwait(false);
			if (!string.IsNullOrWhiteSpace(EffectiveRemoteTarget(request.Options)))
				throw new InvalidOperationException("Remote pits seed does not support buffered standard input; provide a remote-accessible source file.");
			using var gate = await AcquireGatesAsync(request.Target, request.Options, cancellationToken).ConfigureAwait(false);
			var process = new RaiSystem(ResolveExecutable(), WithManagedAssembly(arguments)) { StandardInput = request.StandardInput };
			return await process.ExecAsync(cancellationToken).ConfigureAwait(false);
		}

		/// <summary>Executes a validated export request while holding its target gates and returns captured process output.</summary>
		public RaiSystemResult Export(PitsExportRequest request)
		{
			var arguments = BuildExportArguments(request);
			return RunCoordinated(request.Target, request.Options, arguments);
		}
		/// <summary>Asynchronously executes an export request with target gates and cancellation.</summary>
		public Task<RaiSystemResult> ExportAsync(PitsExportRequest request, CancellationToken cancellationToken = default)
		{
			var arguments = BuildExportArguments(request);
			return RunCoordinatedAsync(request.Target, request.Options, arguments, cancellationToken);
		}

		/// <summary>Executes a read-only audit request with target gates and captured process output.</summary>
		public RaiSystemResult Audit(PitsAuditRequest request)
		{
			var arguments = BuildAuditArguments(request);
			return RunCoordinated(request.Target, request.Options, arguments);
		}
		/// <summary>Asynchronously executes a read-only audit request with target gates and cancellation.</summary>
		public Task<RaiSystemResult> AuditAsync(PitsAuditRequest request, CancellationToken cancellationToken = default)
		{
			var arguments = BuildAuditArguments(request);
			return RunCoordinatedAsync(request.Target, request.Options, arguments, cancellationToken);
		}

		/// <summary>Executes a validated sparse property-deletion request with target gates.</summary>
		public RaiSystemResult DeleteProperty(PitsDeletePropertyRequest request)
		{
			var arguments = BuildDeletePropertyArguments(request);
			return RunCoordinated(PitsTarget.Pit(request.PitName), request.Options, arguments);
		}
		/// <summary>Asynchronously executes a sparse property-deletion request with target gates and cancellation.</summary>
		public Task<RaiSystemResult> DeletePropertyAsync(
			PitsDeletePropertyRequest request,
			CancellationToken cancellationToken = default)
		{
			var arguments = BuildDeletePropertyArguments(request);
			return RunCoordinatedAsync(PitsTarget.Pit(request.PitName), request.Options,
				arguments, cancellationToken);
		}

		/// <summary>Executes a validated lifecycle item-deletion request with target gates.</summary>
		public RaiSystemResult DeleteItem(PitsDeleteItemRequest request)
		{
			var arguments = BuildDeleteItemArguments(request);
			return RunCoordinated(PitsTarget.Pit(request.PitName), request.Options, arguments);
		}
		/// <summary>Asynchronously executes an item-deletion request with target gates and cancellation.</summary>
		public Task<RaiSystemResult> DeleteItemAsync(
			PitsDeleteItemRequest request,
			CancellationToken cancellationToken = default)
		{
			var arguments = BuildDeleteItemArguments(request);
			return RunCoordinatedAsync(PitsTarget.Pit(request.PitName), request.Options,
				arguments, cancellationToken);
		}

		/// <summary>Executes a validated maintenance request with target gates and captured process output.</summary>
		public RaiSystemResult Maintain(PitsMaintainRequest request)
		{
			var arguments = BuildMaintainArguments(request);
			return RunCoordinated(request.Target, request.Options, arguments);
		}
		/// <summary>Asynchronously executes a maintenance request with target gates and cancellation.</summary>
		public Task<RaiSystemResult> MaintainAsync(
			PitsMaintainRequest request,
			CancellationToken cancellationToken = default)
		{
			var arguments = BuildMaintainArguments(request);
			return RunCoordinatedAsync(request.Target, request.Options,
				arguments, cancellationToken);
		}

		/// <summary>Executes already-tokenized arguments, prefixing a configured managed assembly. Use typed request methods when target coordination is required.</summary>
		public override RaiSystemResult Run(IEnumerable<string> arguments)
			=> base.RunAsync(WithManagedAssembly(arguments)).GetAwaiter().GetResult();

		/// <summary>Asynchronously executes already-tokenized arguments, prefixing a configured managed assembly. Use typed request methods when target coordination is required.</summary>
		public override Task<RaiSystemResult> RunAsync(
			IEnumerable<string> arguments,
			CancellationToken cancellationToken = default)
			=> base.RunAsync(WithManagedAssembly(arguments), cancellationToken);

		/// <summary>Executes already-tokenized arguments, prefixing a configured managed assembly. Use typed request methods when target coordination is required.</summary>
		public override RaiSystemResult Run(IEnumerable<string> arguments, int timeoutMilliseconds)
			=> base.Run(WithManagedAssembly(arguments), timeoutMilliseconds);

		/// <summary>Asynchronously executes already-tokenized arguments, prefixing a configured managed assembly. Use typed request methods when target coordination is required.</summary>
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
			return await RunWithRemoteTargetAsync(
				WithManagedAssembly(arguments),
				options?.RemoteTarget,
				cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		private string EffectiveRemoteTarget(PitsCommandOptions options)
			=> string.IsNullOrWhiteSpace(options?.RemoteTarget) ? RemoteTarget : options.RemoteTarget;

		private async Task<GateLease> AcquireGatesAsync(
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

		private IReadOnlyList<string> GateKeys(PitsTarget target, PitsCommandOptions options)
		{
			var provider = options?.CloudProvider?.Trim().ToUpperInvariant() ?? string.Empty;
			var root = options?.Tenant != null
				? "tenant:" + options.Tenant
				: "path:" + (options?.PitRoot?.FullPath?.TrimEnd('/', '\\') ?? string.Empty);
			var remote = EffectiveRemoteTarget(options)?.Trim().ToUpperInvariant() ?? string.Empty;
			var route = $"{remote}\u001f{provider}\u001f{root}".ToUpperInvariant();
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

		private static void AppendOptions(List<string> arguments, PitsCommandOptions options, bool mutating = false)
		{
			if (options == null)
				return;
			if (options.Tenant != null)
			{
				if (options.PitRoot != null)
					throw new ArgumentException("Tenant and PitRoot are mutually exclusive.", nameof(options));
				RequireValue(options.Tenant, nameof(options.Tenant));
				if (options.Tenant != options.Tenant.Trim() || options.Tenant is "." or ".." or "~" ||
					options.Tenant.IndexOfAny(new[] { '/', '\\', ':', '\0' }) >= 0)
					throw new ArgumentException("Tenant must be one plain directory name, not a filesystem path.", nameof(options.Tenant));
				if (mutating && string.IsNullOrWhiteSpace(options.CloudProvider))
					throw new ArgumentException("Tenant-scoped mutations require an explicit CloudProvider.", nameof(options.CloudProvider));
				arguments.Add("-r");
				arguments.Add(options.Tenant);
			}
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
