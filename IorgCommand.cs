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
		/// <summary>Optional SSH target for this typed invocation.</summary>
		public string RemoteTarget { get; init; }
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
		/// <summary>Direct HTTPS ZIP source; when set, Source must be null.</summary>
		public Uri SourceUrl { get; init; }
		public string ImportId { get; init; }
		public string ActivityId { get; init; }
		public string Exif { get; init; }
		public bool Json { get; init; }
		public long? MaxArchiveBytes { get; init; }
		public long? MaxExpandedBytes { get; init; }
		public int? MaxEntries { get; init; }
		public int? DownloadTimeoutSeconds { get; init; }
	}

	public sealed record IorgCleanRequest(string ItemId, RaiPath Root)
	{
		public bool Cache { get; init; }
		public bool Force { get; init; }
		public IorgCommandOptions Options { get; init; }
	}

	public sealed record IorgListRequest(string FileNamePattern, RaiPath Root)
	{
		public bool Long { get; init; }
		public bool AllFiles { get; init; }
		public bool IgnoreCase { get; init; }
		public string Checksum { get; init; }
		/// <summary>Cloud-relative spelling, resolved only on the execution host. Root must be null.</summary>
		public string RootAddress { get; init; }
		public static IorgListRequest ForCloud(string pattern, string cloud, string relativeRoot = ".")
			=> new(pattern, null) { RootAddress = relativeRoot, Options = new() { CloudProvider = cloud } };

		public string Exif { get; init; }
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

	public class IorgCommand : CliCommand
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

		public IorgCommand OverSsh(string remoteTarget) => base.OverSsh<IorgCommand>(remoteTarget);

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
			if (request.SourceUrl is null) RequirePath(request.Source, nameof(request.Source));
			else if (request.Source is not null || !request.SourceUrl.IsAbsoluteUri || request.SourceUrl.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(request.SourceUrl.UserInfo))
				throw new ArgumentException("Specify Source or a direct HTTPS SourceUrl without embedded credentials.");
			if (request.Json || request.Exif is not null) RequireValue(request.ImportId, nameof(request.ImportId));
			RequirePath(request.Root, nameof(request.Root));
			RequireConvention(request.PathConvention, nameof(request.PathConvention), 4);
			RequireConvention(request.NamingConvention, nameof(request.NamingConvention), 3);

			var arguments = new List<string>
			{
				"organize",
				request.SourceUrl is null ? "--source" : "--source-url", request.SourceUrl?.AbsoluteUri ?? request.Source.FullPath,
				RootOption(request.Options), request.Root.FullPath,
				"--pathconv", request.PathConvention.ToString(),
				"--nameconv", request.NamingConvention.ToString()
			};
			AppendOptionalValue(arguments, "--import-id", request.ImportId, nameof(request.ImportId));
			AppendOptionalValue(arguments, "--activity-id", request.ActivityId, nameof(request.ActivityId));
			AppendOptionalValue(arguments, "--exif", request.Exif, nameof(request.Exif));
			if (request.Json) arguments.Add("--json");
			AppendLimit(arguments, "--max-archive-bytes", request.MaxArchiveBytes);
			AppendLimit(arguments, "--max-expanded-bytes", request.MaxExpandedBytes);
			AppendLimit(arguments, "--max-entries", request.MaxEntries);
			AppendLimit(arguments, "--download-timeout-seconds", request.DownloadTimeoutSeconds);
			AppendOptions(arguments, request.Options);
			return arguments;
		}

		private static void AppendLimit(List<string> arguments, string option, long? value)
		{
			if (value is null) return;
			if (value <= 0) throw new ArgumentException($"{option} must be positive.");
			arguments.AddRange([option, value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
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
			var root = RequestRoot(request.Root, request.RootAddress, request.Options);
			RequireOutputMode(request.Json, request.Quiet);
			if (request.Exif is not null && request.Quiet) throw new ArgumentException("Exif cannot be combined with Quiet.");

			if ((request.Long || request.AllFiles || request.Checksum is not null) && (!request.Json || !request.Long || request.Exif is not null))
				throw new ArgumentException("Structured inventory requires --long --json, without --exif.");
			if (request.AllFiles && request.Checksum is not null) throw new ArgumentException("All-files census is metadata-only.");
			if (request.Checksum is not null && request.Checksum != "sha256") throw new ArgumentException("Only sha256 is supported.");
			var arguments = new List<string>
			{
				"list", request.FileNamePattern,
				RootOption(request.Options), root
			};
			AppendOptions(arguments, request.Options);
			AppendOptionalValue(arguments, "--exif", request.Exif, nameof(request.Exif));
			if (request.Long) arguments.Add("--long");
			if (request.AllFiles) arguments.Add("--all-files");
			if (request.IgnoreCase) arguments.Add("--ignore-case");
			AppendOptionalValue(arguments, "--checksum", request.Checksum, nameof(request.Checksum));
			if (request.Json) arguments.Add("--json");
			if (request.Quiet) arguments.Add("--quiet");
			return arguments;
		}

		private static string RequestRoot(RaiPath root, string address, IorgCommandOptions options)
		{
			if (address is null) { RequirePath(root, nameof(root)); return root.FullPath; }
			if (root is not null || string.IsNullOrWhiteSpace(options?.CloudProvider))
				throw new ArgumentException("RootAddress requires a cloud provider and no locally resolved Root.");
			RequireValue(address, nameof(address));
			_ = new RaiRelPath(address);
			if (address.Replace('\\', '/').Split('/').Contains("..")) throw new ArgumentException("Cloud-relative root cannot escape the cloud.");
			return address;
		}

		public IReadOnlyList<string> BuildDuplicateArguments(IorgDuplicateRequest request)
		{
			if (request is null) throw new ArgumentNullException(nameof(request));
			RequireValue(request.FileOrPattern, nameof(request.FileOrPattern));
			var root = RequestRoot(request.Root, request.RootAddress, request.Options);
			if (request.Match is not ("bytes" or "visual")) throw new ArgumentException("Match must be bytes or visual.");
			if (request.Match == "visual" ? request.VisualProfile != "strict-v1" : request.VisualProfile is not null)
				throw new ArgumentException("Visual mode requires --visual-profile strict-v1; byte mode has no visual profile.");
			var args = new List<string> { "duplicates", request.FileOrPattern, RootOption(request.Options), root, "--match", request.Match };
			AppendOptions(args, request.Options);
			AppendOptionalValue(args, "--visual-profile", request.VisualProfile, nameof(request.VisualProfile));
			if (request.Json) args.Add("--json");
			if (request.IgnoreCase) args.Add("--ignore-case");
			return args;
		}
		public RaiSystemResult Duplicates(IorgDuplicateRequest request)
			=> RunWithRequestTarget(BuildDuplicateArguments(request), request?.Options);
		public Task<RaiSystemResult> DuplicatesAsync(IorgDuplicateRequest request, CancellationToken cancellationToken = default)
			=> RunWithRequestTargetAsync(BuildDuplicateArguments(request), request?.Options, cancellationToken);

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

		public RaiSystemResult Organize(IorgOrganizeRequest request)
			=> RunWithRequestTarget(BuildOrganizeArguments(request), request?.Options);
		public Task<RaiSystemResult> OrganizeAsync(
			IorgOrganizeRequest request,
			CancellationToken cancellationToken = default)
			=> RunWithRequestTargetAsync(BuildOrganizeArguments(request), request?.Options, cancellationToken);

		public RaiSystemResult Clean(IorgCleanRequest request)
			=> RunWithRequestTarget(BuildCleanArguments(request), request?.Options);
		public Task<RaiSystemResult> CleanAsync(
			IorgCleanRequest request,
			CancellationToken cancellationToken = default)
			=> RunWithRequestTargetAsync(BuildCleanArguments(request), request?.Options, cancellationToken);

		public RaiSystemResult List(IorgListRequest request)
			=> RunWithRequestTarget(BuildListArguments(request), request?.Options);
		public Task<RaiSystemResult> ListAsync(
			IorgListRequest request,
			CancellationToken cancellationToken = default)
			=> RunWithRequestTargetAsync(BuildListArguments(request), request?.Options, cancellationToken);

		public RaiSystemResult Move(IorgMoveRequest request)
			=> RunWithRequestTarget(BuildMoveArguments(request), request?.Options);
		public Task<RaiSystemResult> MoveAsync(
			IorgMoveRequest request,
			CancellationToken cancellationToken = default)
			=> RunWithRequestTargetAsync(BuildMoveArguments(request), request?.Options, cancellationToken);

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

		private RaiSystemResult RunWithRequestTarget(
			IEnumerable<string> arguments,
			IorgCommandOptions options)
			=> RunWithRemoteTarget(WithManagedAssembly(arguments), options?.RemoteTarget);

		private Task<RaiSystemResult> RunWithRequestTargetAsync(
			IEnumerable<string> arguments,
			IorgCommandOptions options,
			CancellationToken cancellationToken)
			=> RunWithRemoteTargetAsync(
				WithManagedAssembly(arguments),
				options?.RemoteTarget,
				cancellationToken: cancellationToken);

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
