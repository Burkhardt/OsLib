using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OsLib
{
	public enum RaidExportFormat
	{
		Raid,
		PlantUml,
		Svg,
		All
	}

	public enum RaidSvgProfile
	{
		Hydratable,
		Plain
	}

	public sealed record RaidArtifactIdentity(string ItemId)
	{
		public string NameExt { get; init; }
		public int? Number { get; init; }
	}

	public sealed record RaidCommandOptions
	{
		private string tenant;

		public string Tenant { get => tenant; init => tenant = value; }
		public string Subscriber { get => tenant; init => tenant = value; }
		public bool RootIsApplicationRoot { get; init; }
		public string CloudProvider { get; init; }
		public PathConventionType PathConvention { get; init; } = PathConventionType.ItemIdTree8x2;
		public bool Debug { get; init; }
		public bool NoLogo { get; init; }
	}

	public sealed record RaidImportRequest(RaiFile PlantUmlSource)
	{
		public string ItemId { get; init; }
		public string NameExt { get; init; }
		public int? Number { get; init; }
		public RaiPath Root { get; init; }
		public RaiPath OutputDirectory { get; init; }
		public RaidCommandOptions Options { get; init; }
	}

	public sealed record RaidExportRequest(RaidArtifactIdentity Identity, RaiPath Root)
	{
		public RaidExportFormat Format { get; init; } = RaidExportFormat.All;
		public RaidSvgProfile SvgProfile { get; init; } = RaidSvgProfile.Hydratable;
		public RaiPath OutputDirectory { get; init; }
		public RaidCommandOptions Options { get; init; }
	}

	public sealed record RaidRefreshRequest(RaidArtifactIdentity Identity, RaiPath Root)
	{
		public RaidSvgProfile SvgProfile { get; init; } = RaidSvgProfile.Hydratable;
		public RaidCommandOptions Options { get; init; }
	}

	public sealed record RaidValidateRequest(RaiFile Target);

	/// <summary>Typed command boundary for the RAIkeep <c>raid</c> tool.</summary>
	public sealed class RaidCommand : CliCommand
	{
		private static readonly SemaphoreSlim ExecutionGate = new(1, 1);
		private readonly RaiPath commandPath;
		private readonly string commandName;
		private readonly RaiFile managedAssembly;

		public RaidCommand(RaiPath commandPath = null, string commandName = "raid")
			: base(string.IsNullOrWhiteSpace(commandName) ? "raid" : commandName)
		{
			this.commandPath = commandPath;
			this.commandName = string.IsNullOrWhiteSpace(commandName) ? "raid" : commandName;
		}

		private RaidCommand(RaiFile managedAssembly, string hostCommand, bool managed)
			: base(string.IsNullOrWhiteSpace(hostCommand) ? "dotnet" : hostCommand)
		{
			if (managedAssembly == null || string.IsNullOrWhiteSpace(managedAssembly.FullName))
				throw new ArgumentException("A managed raid assembly is required.", nameof(managedAssembly));
			this.managedAssembly = managedAssembly;
			commandName = string.IsNullOrWhiteSpace(hostCommand) ? "dotnet" : hostCommand;
		}

		public static RaidCommand ForManagedAssembly(RaiFile managedAssembly, string hostCommand = "dotnet")
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

		public IReadOnlyList<string> BuildImportArguments(RaidImportRequest request)
		{
			if (request == null) throw new ArgumentNullException(nameof(request));
			RequireFile(request.PlantUmlSource, nameof(request.PlantUmlSource));
			if (request.Root != null && request.OutputDirectory != null)
				throw new ArgumentException("Import accepts either an ImageTree root or an output directory, not both.");
			var arguments = new List<string> { "import", "--puml", request.PlantUmlSource.FullName };
			AppendOptionalValue(arguments, "--name", request.ItemId, nameof(request.ItemId));
			AppendIdentity(arguments, request.NameExt, request.Number);
			if (request.OutputDirectory != null)
			{
				RequirePath(request.OutputDirectory, nameof(request.OutputDirectory));
				arguments.AddRange(["--out", request.OutputDirectory.FullPath]);
			}
			else if (request.Root != null)
				AppendAddress(arguments, request.Root, request.Options);
			AppendCommon(arguments, request.Options, includePathConvention: request.Root != null);
			return arguments;
		}

		public IReadOnlyList<string> BuildExportArguments(RaidExportRequest request)
		{
			if (request == null) throw new ArgumentNullException(nameof(request));
			RequireIdentity(request.Identity);
			RequirePath(request.Root, nameof(request.Root));
			var arguments = new List<string> { "export", request.Identity.ItemId };
			AppendIdentity(arguments, request.Identity.NameExt, request.Identity.Number);
			arguments.AddRange(["--format", Format(request.Format), "--svg-profile", Profile(request.SvgProfile)]);
			if (request.OutputDirectory != null)
			{
				RequirePath(request.OutputDirectory, nameof(request.OutputDirectory));
				arguments.AddRange(["--out", request.OutputDirectory.FullPath]);
			}
			AppendAddress(arguments, request.Root, request.Options);
			AppendCommon(arguments, request.Options, includePathConvention: true);
			return arguments;
		}

		public IReadOnlyList<string> BuildRefreshArguments(RaidRefreshRequest request)
		{
			if (request == null) throw new ArgumentNullException(nameof(request));
			RequireIdentity(request.Identity);
			RequirePath(request.Root, nameof(request.Root));
			var arguments = new List<string>
			{
				"refresh", request.Identity.ItemId,
				"--svg-profile", Profile(request.SvgProfile)
			};
			AppendIdentity(arguments, request.Identity.NameExt, request.Identity.Number);
			AppendAddress(arguments, request.Root, request.Options);
			AppendCommon(arguments, request.Options, includePathConvention: true);
			return arguments;
		}

		public IReadOnlyList<string> BuildValidateArguments(RaidValidateRequest request)
		{
			if (request == null) throw new ArgumentNullException(nameof(request));
			RequireFile(request.Target, nameof(request.Target));
			return ["validate", request.Target.FullName];
		}

		public RaiSystemResult Import(RaidImportRequest request) => RunSerialized(BuildImportArguments(request));
		public Task<RaiSystemResult> ImportAsync(RaidImportRequest request, CancellationToken cancellationToken = default)
			=> RunSerializedAsync(BuildImportArguments(request), cancellationToken);
		public RaiSystemResult Export(RaidExportRequest request) => RunSerialized(BuildExportArguments(request));
		public Task<RaiSystemResult> ExportAsync(RaidExportRequest request, CancellationToken cancellationToken = default)
			=> RunSerializedAsync(BuildExportArguments(request), cancellationToken);
		public RaiSystemResult Refresh(RaidRefreshRequest request) => RunSerialized(BuildRefreshArguments(request));
		public Task<RaiSystemResult> RefreshAsync(RaidRefreshRequest request, CancellationToken cancellationToken = default)
			=> RunSerializedAsync(BuildRefreshArguments(request), cancellationToken);
		public RaiSystemResult Validate(RaidValidateRequest request) => RunSerialized(BuildValidateArguments(request));
		public Task<RaiSystemResult> ValidateAsync(RaidValidateRequest request, CancellationToken cancellationToken = default)
			=> RunSerializedAsync(BuildValidateArguments(request), cancellationToken);

		public override RaiSystemResult Run(IEnumerable<string> arguments)
			=> base.RunAsync(WithManagedAssembly(arguments)).GetAwaiter().GetResult();

		public override Task<RaiSystemResult> RunAsync(
			IEnumerable<string> arguments,
			CancellationToken cancellationToken = default)
			=> base.RunAsync(WithManagedAssembly(arguments), cancellationToken);

		private RaiSystemResult RunSerialized(IEnumerable<string> arguments)
		{
			ExecutionGate.Wait();
			try { return Run(arguments); }
			finally { ExecutionGate.Release(); }
		}

		private async Task<RaiSystemResult> RunSerializedAsync(
			IEnumerable<string> arguments,
			CancellationToken cancellationToken)
		{
			await ExecutionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try { return await RunAsync(arguments, cancellationToken).ConfigureAwait(false); }
			finally { ExecutionGate.Release(); }
		}

		private IEnumerable<string> WithManagedAssembly(IEnumerable<string> arguments)
		{
			if (managedAssembly != null) yield return managedAssembly.FullName;
			if (arguments == null) yield break;
			foreach (var argument in arguments) yield return argument;
		}

		private static void AppendAddress(List<string> arguments, RaiPath root, RaidCommandOptions options)
		{
			RequirePath(root, nameof(root));
			if (options == null || string.IsNullOrWhiteSpace(options.Tenant))
				throw new ArgumentException("ImageTree addressing requires Tenant.", nameof(options));
			arguments.Add(options.RootIsApplicationRoot ? "--app" : "--root");
			arguments.Add(root.FullPath);
			arguments.Add("--tenant");
			arguments.Add(options.Tenant);
		}

		private static void AppendCommon(List<string> arguments, RaidCommandOptions options, bool includePathConvention)
		{
			if (options == null) return;
			AppendOptionalValue(arguments, "--cloud", options.CloudProvider, nameof(options.CloudProvider));
			if (includePathConvention)
			{
				if (!Enum.IsDefined(options.PathConvention))
					throw new ArgumentOutOfRangeException(nameof(options.PathConvention));
				arguments.Add("--pathconv");
				arguments.Add(((int)options.PathConvention + 1).ToString());
			}
			if (options.Debug) arguments.Add("--debug");
			if (options.NoLogo) arguments.Add("--nologo");
		}

		private static void AppendIdentity(List<string> arguments, string nameExt, int? number)
		{
			AppendOptionalValue(arguments, "--name-ext", nameExt, nameof(nameExt));
			if (number is not null)
			{
				if (number < 0) throw new ArgumentOutOfRangeException(nameof(number));
				arguments.Add("--number");
				arguments.Add(number.Value.ToString());
			}
		}

		private static string Format(RaidExportFormat value) => value switch
		{
			RaidExportFormat.Raid => "raid",
			RaidExportFormat.PlantUml => "puml",
			RaidExportFormat.Svg => "svg",
			RaidExportFormat.All => "all",
			_ => throw new ArgumentOutOfRangeException(nameof(value))
		};

		private static string Profile(RaidSvgProfile value) => value switch
		{
			RaidSvgProfile.Hydratable => "hydratable",
			RaidSvgProfile.Plain => "plain",
			_ => throw new ArgumentOutOfRangeException(nameof(value))
		};

		private static void RequireIdentity(RaidArtifactIdentity identity)
		{
			if (identity == null) throw new ArgumentNullException(nameof(identity));
			RequireValue(identity.ItemId, nameof(identity.ItemId));
			if (identity.ItemId.Contains('/') || identity.ItemId.Contains('\\'))
				throw new ArgumentException("ItemId must be a plain identifier.", nameof(identity));
		}

		private static void RequireFile(RaiFile file, string parameterName)
		{
			if (file == null || string.IsNullOrWhiteSpace(file.FullName))
				throw new ArgumentException("A file is required.", parameterName);
		}

		private static void RequirePath(RaiPath path, string parameterName)
		{
			if (path == null || string.IsNullOrWhiteSpace(path.FullPath))
				throw new ArgumentException("A path is required.", parameterName);
		}

		private static void AppendOptionalValue(
			List<string> arguments,
			string option,
			string value,
			string parameterName)
		{
			if (value == null) return;
			RequireValue(value, parameterName);
			arguments.Add(option);
			arguments.Add(value);
		}

		private static void RequireValue(string value, string parameterName)
		{
			if (string.IsNullOrWhiteSpace(value) || value.StartsWith("-", StringComparison.Ordinal))
				throw new ArgumentException("A non-option value is required.", parameterName);
		}
	}
}
