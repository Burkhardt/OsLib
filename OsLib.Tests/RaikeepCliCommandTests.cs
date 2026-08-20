using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OsLib.Tests
{
	public sealed class RaikeepCliCommandTests : IDisposable
	{
		private readonly RaiPath root = Os.TempDir / "RAIkeep" / "oslib-tests" / "raikeep-cli-wrappers";

		public RaikeepCliCommandTests()
		{
			Cleanup();
			root.mkdir();
		}

		public void Dispose() => Cleanup();

		[Fact]
		public async Task PitsCommand_SeedAsync_PassesExactTokenizedArguments()
		{
			var command = CreatePitsCaptureCommand(exitCode: 0);
			var source = new RaiFile(root / "source files", "Activity seed", "json5");
			var pitRoot = root / "pit root";
			var request = PitsSeedRequest.ForPit("Activity Schedule", source) with
			{
				Options = new PitsCommandOptions
				{
					PitRoot = pitRoot,
					CloudProvider = "One Drive",
					Debug = true,
					NoLogo = true,
					RetainWindow = true
				}
			};

			var result = await command.SeedAsync(request, TestContext.Current.CancellationToken);

			Assert.Equal(0, result.ExitCode);
			Assert.Equal(
				new[]
				{
					"seed", "Activity Schedule", "--source", source.FullName,
					"--pitroot", pitRoot.FullPath, "--cloud", "One Drive",
					"--debug", "--nologo", "--retain-window"
				},
				CapturedArguments(result));
		}

		[Fact]
		public void PitsCommand_BuildsWwwaExportAndAuditForms()
		{
			var command = new PitsCommand();
			var output = root / "export output";

			Assert.Equal(
				new[] { "export", "--wwwa", "--out-dir", output.FullPath },
				command.BuildExportArguments(PitsExportRequest.ToDirectory(PitsTarget.Wwwa(), output)));
			Assert.Equal(
				new[] { "export", "Activity", "--json" },
				command.BuildExportArguments(PitsExportRequest.ToJson(PitsTarget.Pit("Activity"))));
			Assert.Equal(
				new[] { "audit", "Activity", "--machine", "local", "--level", "Warning", "--json" },
				command.BuildAuditArguments(new PitsAuditRequest(PitsTarget.Pit("Activity"))
				{
					Machine = "local",
					MinimumLevel = "Warning",
					Json = true
				}));
		}

		[Fact]
		public void PitsCommand_RejectsMissingMandatoryAndBlankOptionalValuesBeforeExecution()
		{
			var command = new PitsCommand();

			Assert.Throws<ArgumentException>(() => PitsTarget.Pit(" "));
			Assert.Throws<ArgumentException>(() => command.BuildSeedArguments(
				PitsSeedRequest.ForPit("Activity", source: null)));
			Assert.Throws<ArgumentException>(() => command.BuildAuditArguments(
				new PitsAuditRequest(target: null)));
			Assert.Throws<ArgumentException>(() => command.BuildAuditArguments(
				new PitsAuditRequest(PitsTarget.Pit("Activity"))
				{
					Options = new PitsCommandOptions { CloudProvider = " " }
				}));
		}

		[Fact]
		public async Task IorgCommand_OrganizeAsync_PassesExactTokenizedArguments()
		{
			var command = CreateIorgCaptureCommand(exitCode: 0);
			var source = root / "source images";
			var destination = root / "image tree";
			var request = new IorgOrganizeRequest(source, destination, PathConvention: 3, NamingConvention: 2)
			{
				Options = new IorgCommandOptions
				{
					Subscriber = "AIA Tenant",
					CloudProvider = "Google Drive",
					Debug = true,
					NoLogo = true
				}
			};

			var result = await command.OrganizeAsync(request, TestContext.Current.CancellationToken);

			Assert.Equal(0, result.ExitCode);
			Assert.Equal(
				new[]
				{
					"organize", "--source", source.FullPath, "--root", destination.FullPath,
					"--pathconv", "3", "--nameconv", "2", "--subscriber", "AIA Tenant",
					"--cloud", "Google Drive", "--debug", "--nologo"
				},
				CapturedArguments(result));
		}

		[Fact]
		public void IorgCommand_BuildCleanArguments_IncludesOnlyRequestedOptions()
		{
			var command = new IorgCommand();
			var request = new IorgCleanRequest("ScheduleRehearsal_1", root)
			{
				Cache = true,
				Force = false,
				Options = new IorgCommandOptions { Subscriber = "AIA" }
			};

			Assert.Equal(
				new[]
				{
					"clean", "ScheduleRehearsal_1", "--root", root.FullPath,
					"--subscriber", "AIA", "--cache"
				},
				command.BuildCleanArguments(request));
		}

		[Theory]
		[InlineData(0)]
		[InlineData(4)]
		public void IorgCommand_RejectsInvalidConventionBeforeExecution(int convention)
		{
			var command = new IorgCommand();
			Assert.Throws<ArgumentOutOfRangeException>(() => command.BuildOrganizeArguments(
				new IorgOrganizeRequest(root, root, convention, NamingConvention: 1)));
		}

		[Fact]
		public void IorgCommand_RejectsPathLikeShortNameBeforeExecution()
		{
			var command = new IorgCommand();
			Assert.Throws<ArgumentException>(() => command.BuildCleanArguments(
				new IorgCleanRequest("folder/Item", root)));
		}

		[Fact]
		public async Task WrapperExecution_ReturnsExitCodeStandardOutputAndStandardError()
		{
			var command = CreateIorgCaptureCommand(exitCode: 7);
			var request = new IorgCleanRequest("ScheduleRehearsal", root);

			var result = await command.CleanAsync(request, TestContext.Current.CancellationToken);

			Assert.Equal(7, result.ExitCode);
			Assert.False(result.Succeeded);
			Assert.False(result.TimedOut);
			Assert.Contains("arg[0]=clean", result.StandardOutput);
			Assert.Contains("wrapper-stderr", result.StandardError);
		}

		[Fact]
		public void TokenizedExecution_PreservesEmptyRepeatedSpecialAndUnicodeArguments()
		{
			if (OperatingSystem.IsWindows())
				return; // cmd.exe cannot represent an empty positional token in this capture fixture.

			var command = CreatePitsCaptureCommand(exitCode: 0);
			string[] arguments =
			[
				string.Empty,
				"with spaces",
				"\"double quotes\"",
				"'single quotes'",
				@"back\\slash",
				"$HOME",
				"semi;colon",
				"amp&ersand",
				"pipe|value",
				"less<than",
				"greater>than",
				"`command`",
				"--repeated",
				"--repeated",
				"Grüße 🌍"
			];

			var result = command.Run(arguments);

			Assert.True(result.Succeeded);
			Assert.Equal(arguments, result.ArgumentList);
			Assert.Equal(arguments, CapturedArguments(result));
		}

		[Fact]
		public void WrapperExecution_CapturesUnicodeLongAndMixedOutput()
		{
			var script = CreateOutputScript(
				"mixed-output",
				OperatingSystem.IsWindows()
					? "@echo off\r\necho Grüß Gott 🌍\r\necho warning-λ 1>&2\r\nfor /L %%i in (1,1,12000) do <nul set /p=x\r\necho.\r\nexit /b 0\r\n"
					: "#!/bin/sh\nprintf 'Grüß Gott 🌍\\n'\nprintf 'warning-λ\\n' >&2\ni=0; while [ \"$i\" -lt 12000 ]; do printf x; i=$((i + 1)); done; printf '\\n'\nexit 0\n");
			var command = new PitsCommand(script.ScriptFile.Path, script.ScriptFile.NameWithExtension);

			var result = command.Run(Array.Empty<string>());

			Assert.True(result.Succeeded);
			Assert.Contains("Grüß Gott", result.StandardOutput, StringComparison.Ordinal);
			Assert.Contains("warning-λ", result.StandardError, StringComparison.Ordinal);
			Assert.True(result.StandardOutput.Length > 12000);
		}

		[Fact]
		public void WrapperExecution_CapturesSuccessfulNoOutputResult()
		{
			var script = CreateOutputScript(
				"no-output",
				OperatingSystem.IsWindows() ? "@echo off\r\nexit /b 0\r\n" : "#!/bin/sh\nexit 0\n");
			var command = new IorgCommand(script.ScriptFile.Path, script.ScriptFile.NameWithExtension);

			var result = command.Run(Array.Empty<string>());

			Assert.True(result.Succeeded);
			Assert.Equal(string.Empty, result.StandardOutput);
			Assert.Equal(string.Empty, result.StandardError);
			Assert.Empty(result.ArgumentList);
		}

		[Fact]
		public void WrapperExecution_CapturesWarningOnlyWithoutTreatingItAsFailure()
		{
			var script = CreateOutputScript(
				"warning-only",
				OperatingSystem.IsWindows()
					? "@echo off\r\necho warning-only 1>&2\r\nexit /b 0\r\n"
					: "#!/bin/sh\nprintf 'warning-only\\n' >&2\nexit 0\n");
			var command = new PitsCommand(script.ScriptFile.Path, script.ScriptFile.NameWithExtension);

			var result = command.Run(Array.Empty<string>());

			Assert.True(result.Succeeded);
			Assert.Equal(string.Empty, result.StandardOutput);
			Assert.Contains("warning-only", result.StandardError, StringComparison.Ordinal);
		}

		[Fact]
		public void WrapperExecution_CapturesErrorOnlyAndExactNonzeroExitCode()
		{
			var script = CreateOutputScript(
				"error-only",
				OperatingSystem.IsWindows()
					? "@echo off\r\necho error-only 1>&2\r\nexit /b 23\r\n"
					: "#!/bin/sh\nprintf 'error-only\\n' >&2\nexit 23\n");
			var command = new IorgCommand(script.ScriptFile.Path, script.ScriptFile.NameWithExtension);

			var result = command.Run(Array.Empty<string>());

			Assert.False(result.Succeeded);
			Assert.Equal(23, result.ExitCode);
			Assert.Equal(string.Empty, result.StandardOutput);
			Assert.Contains("error-only", result.StandardError, StringComparison.Ordinal);
		}

		[Fact]
		public void WrapperExecution_ReportsTimeoutSeparatelyFromCliExit()
		{
			var script = CreateOutputScript(
				"timeout",
				OperatingSystem.IsWindows()
					? "@echo off\r\nping 127.0.0.1 -n 6 >nul\r\nexit /b 0\r\n"
					: "#!/bin/sh\nsleep 5\nexit 0\n");
			var command = new PitsCommand(script.ScriptFile.Path, script.ScriptFile.NameWithExtension);

			var result = command.Run(Array.Empty<string>(), timeoutMilliseconds: 100);

			Assert.True(result.TimedOut);
			Assert.False(result.Succeeded);
			Assert.Equal(-1, result.ExitCode);
		}

		[Fact]
		public async Task WrapperExecution_HonorsPreCanceledToken()
		{
			var command = CreatePitsCaptureCommand(exitCode: 0);
			using var cancellation = new CancellationTokenSource();
			cancellation.Cancel();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command.AuditAsync(
				new PitsAuditRequest(PitsTarget.Pit("Activity")),
				cancellation.Token));
		}

		[Fact]
		public async Task WrapperExecution_CancelsAndKillsAProcessAfterLaunch()
		{
			var script = CreateOutputScript(
				"cancel-after-launch",
				OperatingSystem.IsWindows()
					? "@echo off\r\necho started\r\nping 127.0.0.1 -n 11 >nul\r\nexit /b 0\r\n"
					: "#!/bin/sh\nprintf 'started\\n'\nsleep 10\nexit 0\n");
			var command = new IorgCommand(script.ScriptFile.Path, script.ScriptFile.NameWithExtension);
			using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
			var stopwatch = Stopwatch.StartNew();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
				command.RunAsync(Array.Empty<string>(), cancellation.Token));

			Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
		}

		[Fact]
		public void WrapperExecution_DistinguishesMissingExecutableFromCompletedCliResult()
		{
			var command = new PitsCommand(root, "missing-pits-executable-for-cr014");

			Assert.Throws<Win32Exception>(() => command.Run(Array.Empty<string>()));
		}

		[Fact]
		public void WrapperExecution_SurfacesPermissionDeniedAsAStartFailure()
		{
			if (OperatingSystem.IsWindows())
				return;

			var script = CreateOutputScript("not-executable", "#!/bin/sh\nexit 0\n");
			File.SetUnixFileMode(
				script.FullName,
				UnixFileMode.UserRead | UnixFileMode.UserWrite |
				UnixFileMode.GroupRead | UnixFileMode.OtherRead);
			var command = new IorgCommand(script.ScriptFile.Path, script.ScriptFile.NameWithExtension);

			Assert.Throws<Win32Exception>(() => command.Run(Array.Empty<string>()));
		}

		[Fact]
		public void ManagedAssemblyMode_PrefixesTheDllAsOneArgument()
		{
			var host = CreateCaptureScript("dotnet-host-capture", exitCode: 0);
			var assembly = new RaiFile(root / "compiled tools", "pits server tool", "dll");
			_ = new TextFile(assembly.FullName, "capture fixture");
			var command = PitsCommand.ForManagedAssembly(assembly, host.FullName);

			var result = command.Run(new[] { "--version" });

			Assert.Equal(new[] { assembly.FullName, "--version" }, CapturedArguments(result));
		}

		[Fact]
		public void PosixShellCommand_QuotesValuesWithoutChangingArgumentMeaning()
		{
			var command = new PitsCommand();
			var arguments = command.BuildAuditArguments(new PitsAuditRequest(PitsTarget.Pit("Activity Schedule"))
			{
				Machine = "tenant's server"
			});

			Assert.Equal(
				"pits audit 'Activity Schedule' --machine 'tenant'\"'\"'s server'",
				command.BuildPosixShellCommand(arguments));
		}

		private PitsCommand CreatePitsCaptureCommand(int exitCode)
		{
			var script = CreateCaptureScript("pits-capture", exitCode);
			return new PitsCommand(script.ScriptFile.Path, script.ScriptFile.NameWithExtension);
		}

		private IorgCommand CreateIorgCaptureCommand(int exitCode)
		{
			var script = CreateCaptureScript("iorg-capture", exitCode);
			return new IorgCommand(script.ScriptFile.Path, script.ScriptFile.NameWithExtension);
		}

		private Script CreateCaptureScript(string name, int exitCode)
		{
			if (OperatingSystem.IsWindows())
			{
				return RaiSystem.CreateScript(
					root,
					name,
					"cmd",
					$"@echo off\r\nsetlocal EnableDelayedExpansion\r\nset i=0\r\n:loop\r\nif \"%~1\"==\"\" goto done\r\necho arg[!i!]=%~1\r\nset /a i+=1\r\nshift\r\ngoto loop\r\n:done\r\necho wrapper-stderr 1>&2\r\nexit /b {exitCode}\r\n");
			}

			return RaiSystem.CreateScript(
				root,
				name,
				"sh",
				$"#!/bin/sh\nindex=0\nfor argument in \"$@\"; do\n  printf 'arg[%s]=%s\\n' \"$index\" \"$argument\"\n  index=$((index + 1))\ndone\nprintf 'wrapper-stderr\\n' >&2\nexit {exitCode}\n");
		}

		private Script CreateOutputScript(string name, string content)
		{
			return RaiSystem.CreateScript(
				root,
				name,
				OperatingSystem.IsWindows() ? "cmd" : "sh",
				content);
		}

		private static IReadOnlyList<string> CapturedArguments(RaiSystemResult result)
		{
			return result.StandardOutput
				.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
				.Where(line => line.StartsWith("arg[", StringComparison.Ordinal))
				.Select(line => line[(line.IndexOf('=') + 1)..])
				.ToArray();
		}

		private void Cleanup()
		{
			try
			{
				if (root.Exists())
					root.rmdir(depth: int.MaxValue, deleteFiles: true);
			}
			catch
			{
			}
		}
	}
}
