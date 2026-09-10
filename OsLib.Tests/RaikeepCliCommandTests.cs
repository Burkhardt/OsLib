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
			var at = new DateTimeOffset(2026, 8, 27, 14, 0, 0, TimeSpan.FromHours(2));

			Assert.Equal(
				new[] { "export", "--wwwa", "--out-dir", output.FullPath },
				command.BuildExportArguments(PitsExportRequest.ToDirectory(PitsTarget.Wwwa(), output)));
			Assert.Equal(
				new[] { "export", "Activity", "--json" },
				command.BuildExportArguments(PitsExportRequest.ToJson(PitsTarget.Pit("Activity"))));
			Assert.Equal(
				new[]
				{
					"export", "--wwwa", "--json", "--at",
					"2026-08-27T12:00:00.0000000Z"
				},
				command.BuildExportArguments(
					PitsExportRequest.ToJson(PitsTarget.Wwwa()) with { At = at }));
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
		public async Task PitsCommand_ExportAtAsync_PassesExactTokenizedArguments()
		{
			var command = CreatePitsCaptureCommand(exitCode: 0);
			var request = PitsExportRequest.ToDirectory(PitsTarget.Pit("Activity Schedule"), root / "exports") with
			{
				At = new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero),
				Options = new PitsCommandOptions
				{
					PitRoot = root / "pit root",
					NoLogo = true
				}
			};

			var result = await command.ExportAsync(request, TestContext.Current.CancellationToken);

			Assert.Equal(0, result.ExitCode);
			Assert.Equal(
				new[]
				{
					"export", "Activity Schedule", "--out-dir", request.OutputDirectory.FullPath,
					"--at", "2026-08-27T12:00:00.0000000Z",
					"--pitroot", request.Options.PitRoot.FullPath, "--nologo"
				},
				CapturedArguments(result));
		}

		[Fact]
		public void PitsCommand_BuildsTypedDeleteForms_WithExactTokenizedArguments()
		{
			var command = new PitsCommand();
			var options = new PitsCommandOptions
			{
				PitRoot = root / "tenant root",
				CloudProvider = "One Drive",
				NoLogo = true
			};

			Assert.Equal(
				new[]
				{
					"delete-property", "Activity", "UC16 Save", "What.Chat",
					"--pitroot", options.PitRoot.FullPath, "--cloud", "One Drive", "--nologo"
				},
				command.BuildDeletePropertyArguments(new PitsDeletePropertyRequest(
					"Activity", "UC16 Save", "What.Chat") { Options = options }));
			Assert.Equal(
				new[] { "delete-item", "Object", "Legacy Record", "--nologo" },
				command.BuildDeleteItemArguments(new PitsDeleteItemRequest(
					"Object", "Legacy Record")
				{
					Options = new PitsCommandOptions { NoLogo = true }
				}));
		}

		[Fact]
		public void PitsCommand_BuildsTypedMaintainForms_AndValidatesDestructiveOptions()
		{
			var command = new PitsCommand();
			var options = new PitsCommandOptions
			{
				PitRoot = root / "tenant root",
				CloudProvider = "One Drive",
				NoLogo = true
			};
			var request = new PitsMaintainRequest(PitsTarget.Pit("Activity"))
			{
				Apply = true,
				Json = true,
				PruneProcessFlags = true,
				OlderThan = TimeSpan.FromDays(7),
				RepairLegacyExtensions = true,
				Options = options
			};

			Assert.Equal(
				new[]
				{
					"maintain", "Activity", "--apply", "--prune-process-flags",
					"--older-than", "7.00:00:00", "--repair-legacy-extensions", "--json",
					"--pitroot", options.PitRoot.FullPath, "--cloud", "One Drive", "--nologo"
				},
				command.BuildMaintainArguments(request));

			Assert.Equal(
				new[] { "maintain", "--wwwa" },
				command.BuildMaintainArguments(new PitsMaintainRequest(PitsTarget.Wwwa())));

			Assert.Throws<ArgumentException>(() => command.BuildMaintainArguments(
				new PitsMaintainRequest(PitsTarget.Pit("Activity"))
				{
					PruneProcessFlags = true,
					OlderThan = TimeSpan.FromDays(1)
				}));
			Assert.Throws<ArgumentException>(() => command.BuildMaintainArguments(
				new PitsMaintainRequest(PitsTarget.Pit("Activity"))
				{
					Apply = true,
					OlderThan = TimeSpan.FromDays(1)
				}));
			Assert.Throws<ArgumentException>(() => command.BuildMaintainArguments(
				new PitsMaintainRequest(PitsTarget.Pit("Activity"))
				{
					RepairLegacyExtensions = true
				}));
		}

		[Fact]
		public async Task PitsCommand_DeletePropertyAsync_PassesExactTokensToExecutable()
		{
			var command = CreatePitsCaptureCommand(exitCode: 0);
			var request = new PitsDeletePropertyRequest("Activity", "UC16", "What.Chat")
			{
				Options = new PitsCommandOptions { PitRoot = root, NoLogo = true }
			};

			var result = await command.DeletePropertyAsync(
				request,
				TestContext.Current.CancellationToken);

			Assert.Equal(
				new[]
				{
					"delete-property", "Activity", "UC16", "What.Chat",
					"--pitroot", root.FullPath, "--nologo"
				},
				CapturedArguments(result));
		}

		[Theory]
		[InlineData("")]
		[InlineData("What..Chat")]
		[InlineData(".What")]
		[InlineData("What.")]
		public void PitsCommand_DeleteProperty_RejectsMalformedPathBeforeExecution(string path)
		{
			var command = new PitsCommand();
			Assert.Throws<ArgumentException>(() => command.BuildDeletePropertyArguments(
				new PitsDeletePropertyRequest("Activity", "UC16", path)));
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
			var request = new IorgCleanRequest(null, root)
			{
				Cache = true,
				Force = false,
				Options = new IorgCommandOptions { Subscriber = "AIA" }
			};

			Assert.Equal(
				new[]
				{
					"clean", "--root", root.FullPath,
					"--subscriber", "AIA", "--cache"
				},
				command.BuildCleanArguments(request));
		}

		[Fact]
		public void IorgCommand_BuildsListAndMoveArguments_WithExactTokens()
		{
			var command = new IorgCommand();
			var options = new IorgCommandOptions
			{
				Subscriber = "Nomsa",
				CloudProvider = "OneDrive",
				NoLogo = true
			};

			Assert.Equal(
				new[]
				{
					"list", "WorkInPro*", "--root", root.FullPath,
					"--subscriber", "Nomsa", "--cloud", "OneDrive", "--nologo", "--json"
				},
				command.BuildListArguments(new IorgListRequest("WorkInPro*", root)
				{
					Options = options,
					Json = true
				}));

			Assert.Equal(
				new[]
				{
					"move", "AfricanBrisket", "AfricanDinner",
					"--root", root.FullPath, "--pathconv", "Flat",
					"--subscriber", "Nomsa", "--cloud", "OneDrive", "--nologo", "--quiet"
				},
				command.BuildMoveArguments(new IorgMoveRequest(
					"AfricanBrisket",
					"AfricanDinner",
					root,
					PathConventionType.Flat)
				{
					Options = options,
					Quiet = true
				}));
		}

		[Fact]
		public async Task IorgCommand_MoveAsync_PassesExactTokensToExecutable()
		{
			var command = CreateIorgCaptureCommand(exitCode: 0);
			var request = new IorgMoveRequest(
				"AfricanBrisket",
				TargetItemId: null,
				root,
				PathConventionType.ItemIdTree3x3)
			{
				Options = new IorgCommandOptions { Subscriber = "Nomsa" }
			};

			var result = await command.MoveAsync(request, TestContext.Current.CancellationToken);

			Assert.Equal(
				new[]
				{
					"move", "AfricanBrisket", "--root", root.FullPath,
					"--pathconv", "ItemIdTree3x3", "--subscriber", "Nomsa"
				},
				CapturedArguments(result));
		}

		[Theory]
		[InlineData("folder/item")]
		[InlineData("folder\\item")]
		public void IorgCommand_ListAndMoveRejectPathInjection(string value)
		{
			var command = new IorgCommand();
			Assert.Throws<ArgumentException>(() =>
				command.BuildListArguments(new IorgListRequest(value, root)));
			Assert.Throws<ArgumentException>(() =>
				command.BuildMoveArguments(new IorgMoveRequest(value, null, root)));
		}

		[Fact]
		public void IorgCommand_ListAndMoveRejectConflictingMachineOutputModes()
		{
			var command = new IorgCommand();
			Assert.Throws<ArgumentException>(() => command.BuildListArguments(
				new IorgListRequest("*", root) { Json = true, Quiet = true }));
			Assert.Throws<ArgumentException>(() => command.BuildMoveArguments(
				new IorgMoveRequest("AfricanBrisket", null, root) { Json = true, Quiet = true }));
		}

		[Theory]
		[InlineData(0)]
		[InlineData(5)]
		public void IorgCommand_RejectsInvalidConventionBeforeExecution(int convention)
		{
			var command = new IorgCommand();
			Assert.Throws<ArgumentOutOfRangeException>(() => command.BuildOrganizeArguments(
				new IorgOrganizeRequest(root, root, convention, NamingConvention: 1)));
		}

		[Fact]
		public void IorgCommand_RejectsPathLikeItemIdBeforeExecution()
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
		public async Task PitsCommand_SameTargetTypedCallsSerializeAcrossWrapperInstances()
		{
			if (OperatingSystem.IsWindows()) return;
			var lockPath = new RaiPath(root / "same-target-lock").FullPath.TrimEnd('/', '\\');
			var overlap = new RaiFile(root, "same-target-overlap", "txt");
			var script = CreateOutputScript("same-target-gate",
				$"#!/bin/sh\nif ! mkdir '{lockPath}' 2>/dev/null; then printf overlap > '{overlap.FullName}'; fi\nsleep 0.35\nrmdir '{lockPath}' 2>/dev/null\nexit 0\n");
			var first = new PitsCommand(script.ScriptFile.Path, script.ScriptFile.NameWithExtension);
			var second = new PitsCommand(script.ScriptFile.Path, script.ScriptFile.NameWithExtension);
			var request = PitsExportRequest.ToJson(PitsTarget.Pit("Activity")) with
			{
				Options = new PitsCommandOptions { PitRoot = root }
			};

			await Task.WhenAll(
				first.ExportAsync(request, TestContext.Current.CancellationToken),
				second.ExportAsync(request, TestContext.Current.CancellationToken));

			Assert.False(overlap.Exists());
		}

		[Fact]
		public async Task PitsCommand_DifferentTargetsMayRunConcurrently()
		{
			if (OperatingSystem.IsWindows()) return;
			var lockPath = new RaiPath(root / "different-target-lock").FullPath.TrimEnd('/', '\\');
			var overlap = new RaiFile(root, "different-target-overlap", "txt");
			var script = CreateOutputScript("different-target-gate",
				$"#!/bin/sh\nif ! mkdir '{lockPath}' 2>/dev/null; then printf overlap > '{overlap.FullName}'; fi\nsleep 0.5\nrmdir '{lockPath}' 2>/dev/null\nexit 0\n");
			var command = new PitsCommand(script.ScriptFile.Path, script.ScriptFile.NameWithExtension);
			var options = new PitsCommandOptions { PitRoot = root };

			await Task.WhenAll(
				command.ExportAsync(PitsExportRequest.ToJson(PitsTarget.Pit("Activity")) with { Options = options }, TestContext.Current.CancellationToken),
				command.ExportAsync(PitsExportRequest.ToJson(PitsTarget.Pit("Person")) with { Options = options }, TestContext.Current.CancellationToken));

			Assert.True(overlap.Exists());
		}

		[Fact]
		public async Task PitsCommand_CancellationWhileQueued_DoesNotLaunchASecondChild()
		{
			if (OperatingSystem.IsWindows()) return;
			var invocations = new RaiFile(root, "queued-invocations", "txt");
			var script = CreateOutputScript("queued-gate",
				$"#!/bin/sh\nprintf 'run\\n' >> '{invocations.FullName}'\nsleep 0.5\nexit 0\n");
			var command = new PitsCommand(script.ScriptFile.Path, script.ScriptFile.NameWithExtension);
			var request = new PitsMaintainRequest(PitsTarget.Pit("Activity"))
			{
				Options = new PitsCommandOptions { PitRoot = root }
			};
			var first = command.MaintainAsync(request, TestContext.Current.CancellationToken);
			await Task.Delay(80, TestContext.Current.CancellationToken);
			using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
				command.MaintainAsync(request, cancellation.Token));
			await first;

			Assert.Single(new TextFile(invocations.FullName).Read());
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
