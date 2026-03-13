using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OsLib;
using Xunit;

namespace OsLib.Tests
{
	public class CliCommandTests
	{
		private static string CreateTempRoot()
		{
			var root = new RaiPath(Os.TempDir) / "RAIkeep" / "oslib-tests" / "cli" / Guid.NewGuid().ToString("N");
			root.mkdir();
			return root.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		}

		private static void Cleanup(string root)
		{
			try
			{
				if (Directory.Exists(root))
					Directory.Delete(root, recursive: true);
			}
			catch
			{
			}
		}

		private static string CreateExecutableScript(string root, string scriptName, string content)
		{
			var scriptPath = new RaiFile(scriptName) { Path = root }.FullName;
			File.WriteAllText(scriptPath, content);
			if (!OperatingSystem.IsWindows())
			{
				File.SetUnixFileMode(scriptPath,
					UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
					UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
					UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
			}
			return scriptPath;
		}

		[Fact]
		public async Task CliCommand_RunAsync_ExecutesOnWorkerThread_AndCapturesOutput()
		{
			var root = CreateTempRoot();
			try
			{
				var script = OperatingSystem.IsWindows()
					? CreateExecutableScript(root, "echo.cmd", "@echo off\r\necho %1\r\n")
					: CreateExecutableScript(root, "echo.sh", "#!/bin/sh\necho \"$1\"\n");
				var sut = new TestCliCommand(script);
				var callingThreadId = Environment.CurrentManagedThreadId;

				var result = await sut.RunAsync("hello", TestContext.Current.CancellationToken);

				Assert.Equal(0, result.ExitCode);
				Assert.Contains("hello", result.Output);
				Assert.NotEqual(callingThreadId, result.WorkerThreadId);
			}
			finally
			{
				Cleanup(root);
			}
		}

		[Fact]
		public void SevenZipCommand_ResolvesAlternateExecutableCandidates()
		{
			var root = CreateTempRoot();
			try
			{
				var sevenZa = OperatingSystem.IsWindows()
					? CreateExecutableScript(root, "7za.cmd", "@echo off\r\n")
					: CreateExecutableScript(root, "7za", "#!/bin/sh\nexit 0\n");
				var sut = new MultiCandidateCliCommand(sevenZa, "7z", "7zz", "7za");

				Assert.True(sut.TryResolveExecutable(out var resolved));
				Assert.Equal(sevenZa, resolved);
			}
			finally
			{
				Cleanup(root);
			}
		}

		[Fact]
		public void CommandInstallAndUpdateCommands_AreAvailable_ForKnownCommands()
		{
			Assert.False(string.IsNullOrWhiteSpace(new CurlCommand().GetInstallCommand()));
			Assert.False(string.IsNullOrWhiteSpace(new RCloneCommand().GetInstallCommand()));
			Assert.False(string.IsNullOrWhiteSpace(new ZipCommand().GetUpdateCommand()));
			Assert.False(string.IsNullOrWhiteSpace(new SevenZipCommand().GetInstallCommand()));
		}

		private sealed class TestCliCommand : CliCommand
		{
			public TestCliCommand(string executable) : base(executable)
			{
			}
		}

		private sealed class MultiCandidateCliCommand : CliCommand
		{
			private readonly string[] candidates;

			public MultiCandidateCliCommand(params string[] candidates) : base(candidates[0])
			{
				this.candidates = candidates;
			}

			public override IEnumerable<string> CandidateExecutables
			{
				get
				{
					foreach (var candidate in candidates)
						yield return candidate;
				}
			}
		}
	}
}