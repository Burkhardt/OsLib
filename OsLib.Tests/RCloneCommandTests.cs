using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace OsLib.Tests
{
	public class RCloneCommandTests
	{
		private static string CreateTempRoot()
		{
			var root = new RaiPath(Os.TempDir) / "RAIkeep" / "oslib-tests" / "rclone-command" / Guid.NewGuid().ToString("N");
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
		public void RCloneCommand_UsesExecutableInsideConfiguredPath()
		{
			var root = CreateTempRoot();
			try
			{
				var script = OperatingSystem.IsWindows()
					? CreateExecutableScript(root, "rclone.cmd", "@echo off\r\n")
					: CreateExecutableScript(root, "rclone", "#!/bin/sh\nexit 0\n");
				var sut = new RCloneCommand(root + Path.DirectorySeparatorChar, Path.GetFileName(script));

				Assert.True(sut.TryResolveExecutable(out var resolved));
				Assert.Equal(script, resolved);
			}
			finally
			{
				Cleanup(root);
			}
		}

		[Fact]
		public async Task RCloneCommand_RunSubcommandAsync_PrefixesSubcommand_AndUsesWorkerThread()
		{
			var root = CreateTempRoot();
			try
			{
				var log = new RaiFile("rclone.log") { Path = root }.FullName;
				var script = OperatingSystem.IsWindows()
					? CreateExecutableScript(root, "rclone.cmd",
						$"@echo off\r\n> \"{log}\" echo %1\r\n>> \"{log}\" echo %2\r\necho ok\r\n")
					: CreateExecutableScript(root, "rclone",
						$"#!/bin/sh\nprintf '%s\\n' \"$1\" > \"{log}\"\nprintf '%s\\n' \"$2\" >> \"{log}\"\nprintf 'ok'\n");
				var sut = new RCloneCommand(root + Path.DirectorySeparatorChar, Path.GetFileName(script));
				var callingThreadId = Environment.CurrentManagedThreadId;

				var result = await sut.RunSubcommandAsync("listremotes", "--config test.conf", TestContext.Current.CancellationToken);

				Assert.Equal(0, result.ExitCode);
				Assert.Contains("ok", result.Output);
				Assert.NotEqual(callingThreadId, result.WorkerThreadId);
				var lines = File.ReadAllLines(log);
				Assert.Equal("listremotes", lines[0]);
				Assert.Equal("--config", lines[1]);
			}
			finally
			{
				Cleanup(root);
			}
		}

		[Fact]
		public void RCloneCommand_ProvidesInstallAndUpdateCommands()
		{
			var sut = new RCloneCommand();

			Assert.False(string.IsNullOrWhiteSpace(sut.GetInstallCommand()));
			Assert.False(string.IsNullOrWhiteSpace(sut.GetUpdateCommand()));
		}
	}
}