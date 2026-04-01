using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using OsLib;

namespace OsLib.Tests
{
	public class OsLibTests
	{
		private static void ResetOsCaches()
		{
			var osType = typeof(Os);
			osType.GetField("userHomeDir", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
			osType.GetField("appRootDir", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
			osType.GetField("tempDir", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
			osType.GetField("localBackupDir", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
			osType.GetField("type", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
			osType.GetField("dIRSEPERATOR", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
			osType.GetField("config", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
			osType.GetField("configPathOverride", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
			osType.GetField("cloudRootsCache", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
			osType.GetField("isDiscoveringCloudRoots", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, false);
			osType.GetField("isInitializingConfig", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, false);
			Os.ResetDiagnosticsForTesting();
		}

		private static RaiPath CreateTempDir([CallerMemberName] string testName = "")
		{
			var root = Os.TempDir / "RAIkeep" / "oslib-tests" / "core" / SanitizeSegment(testName);
			Cleanup(root);
			root.mkdir();
			return root;
		}

		private static void Cleanup(RaiPath root)
		{
			try
			{
				if (Directory.Exists(root.Path))
					new RaiFile(root.Path).rmdir(depth: 10, deleteFiles: true);
			}
			catch
			{
			}
		}

		private static string SanitizeSegment(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return "test";

			var invalid = Path.GetInvalidFileNameChars();
			var cleaned = new string(value
				.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch)
				.ToArray())
				.Trim('-');

			return string.IsNullOrWhiteSpace(cleaned) ? "test" : cleaned;
		}

		private static string CreateExecutableScript(RaiPath root, string scriptName, string content)
		{
			return RaiSystem.CreateScript(root, scriptName, content).FullName;
		}

		[Fact]
		public void Os_Type_UsesRuntimePlatformDetection()
		{
			ResetOsCaches();
			var expected = OperatingSystem.IsWindows()
				? OsType.Windows
				: OperatingSystem.IsMacOS()
					? OsType.MacOS
					: OsType.Ubuntu;

			Assert.Equal(expected, Os.Type);
		}

		[Fact]
		public void Os_IsUnixLike_IsTrueForNonWindowsPlatforms()
		{
			ResetOsCaches();

			Assert.Equal(!OperatingSystem.IsWindows(), Os.IsUnixLike);
		}

		[Fact]
		public void Os_DirSeparator_MatchesSystemDirectorySeparatorChar()
		{
			ResetOsCaches();
			Assert.Equal(Path.DirectorySeparatorChar.ToString(), Os.DIRSEPERATOR);
		}

		[Fact]
		public void Os_UserHomeDir_UsesWindowsVariables_OnWindows()
		{
			if (Os.Type != OsType.Windows)
				return;

			var oldUserProfile = Environment.GetEnvironmentVariable("USERPROFILE");
			var oldHomeDrive = Environment.GetEnvironmentVariable("HOMEDRIVE");
			var oldHomePath = Environment.GetEnvironmentVariable("HOMEPATH");
			try
			{
				Environment.SetEnvironmentVariable("USERPROFILE", "C:\\Users\\UnitTestUser");
				Environment.SetEnvironmentVariable("HOMEDRIVE", "C:");
				Environment.SetEnvironmentVariable("HOMEPATH", "\\Users\\FallbackUser");
				ResetOsCaches();

				Assert.Equal("C:\\Users\\UnitTestUser", Os.UserHomeDir.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			}
			finally
			{
				Environment.SetEnvironmentVariable("USERPROFILE", oldUserProfile);
				Environment.SetEnvironmentVariable("HOMEDRIVE", oldHomeDrive);
				Environment.SetEnvironmentVariable("HOMEPATH", oldHomePath);
				ResetOsCaches();
			}
		}

		[Fact]
		public void Os_UserHomeDir_UsesHomeVariable_OnUnix()
		{
			if (!Os.IsUnixLike)
				return;

			var oldHome = Environment.GetEnvironmentVariable("HOME");
			try
			{
				Environment.SetEnvironmentVariable("HOME", "/tmp/oslib-home-unittest");
				var fakeConfigDir = new RaiPath("/tmp/oslib-home-unittest/.config/RAIkeep");
				Cleanup(fakeConfigDir);
				ResetOsCaches();

				Assert.Equal("/tmp/oslib-home-unittest", Os.UserHomeDir.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			}
			finally
			{
				Environment.SetEnvironmentVariable("HOME", oldHome);
				ResetOsCaches();
			}
		}

		[Fact]
		public void RaiPath_AppendsDirectorySeparator()
		{
			var path = new RaiPath("/tmp/railpath");
			Assert.EndsWith(Os.DIRSEPERATOR, path.Path);
		}

		[Fact]
		public void RaiPath_OperatorAddsSubDirectory()
		{
			var basePath = new RaiPath("/tmp/base");
			var subPath = basePath / "child";
			Assert.EndsWith("child" + Os.DIRSEPERATOR, subPath.Path);
		}

		[Fact]
		public void RaiFile_ParsesNameAndExtension()
		{
			var root = CreateTempDir();
			try
			{
				var rf = new RaiFile(root, "file.test", "txt");
				Assert.Equal("file.test", rf.Name);
				Assert.Equal("txt", rf.Ext);
				Assert.EndsWith(Os.DIRSEPERATOR, rf.Path);
			}
			finally
			{
				root.rmdir();
			}
		}

		[Fact]
		public void RaiFile_CopyMoveRemove_WorksOnDisk()
		{
			var root = CreateTempDir();
			try
			{
				var source = new TextFile(root, "source.txt", "data");
				var copy = new TextFile(root, "copy.txt");
				copy.cp(source);
				Assert.True(copy.Exists());
				Assert.Equal("data", copy.Lines[0]);

				var moved = new TextFile(root, "moved.txt");
				moved.mv(copy);
				Assert.True(moved.Exists());
				Assert.False(copy.Exists());
				Assert.Equal("data", moved.Lines[0]);

				moved.rm();
				Assert.False(moved.Exists());
			}
			finally
			{
				root.rmdir(depth: 9, deleteFiles: true);
			}
		}

		[Fact]
		public void RaiFile_Move_ReplaceFalse_ThrowsWhenDestinationExists()
		{
			var root = CreateTempDir();
			try
			{
				var source = new TextFile(root, "source.txt", "src");
				var dest = new TextFile(root, "dest.txt", "dest");

				Assert.Throws<IOException>(() => dest.mv(source, replace: false, keepBackup: false));
			}
			finally
			{
				root.rmdir(depth: 9, deleteFiles: true);
			}
		}

		[Fact]
		public void RaiFile_Move_KeepBackup_CreatesBakFile()
		{
			var root = CreateTempDir();
			try
			{
				var source = new TextFile(root, "source.txt", "src");
				var dest = new TextFile(root, "dest.txt", "dest");

				dest.mv(source, replace: true, keepBackup: true);
				Assert.True(File.Exists(dest.FullName));
				Assert.Equal("src", File.ReadAllText(dest.FullName).TrimEnd('\r', '\n'));

				var bak = new RaiFile(dest.FullName);
				bak.Ext = "bak";
				Assert.True(File.Exists(bak.FullName));
				Assert.Equal("dest", File.ReadAllText(bak.FullName).TrimEnd('\r', '\n'));
			}
			finally
			{
				root.rmdir(depth: 9, deleteFiles: true);
			}
		}

		[Fact]
		public void RaiFile_Move_NoBackup_RemovesBakFile()
		{
			var root = CreateTempDir();
			try
			{
				var source = new TextFile(root, "source.txt", "src");
				var dest = new TextFile(root, "dest.txt", "dest");

				dest.mv(source, replace: true, keepBackup: false);
				Assert.True(File.Exists(dest.FullName));
				Assert.Equal("src", File.ReadAllText(dest.FullName).TrimEnd('\r', '\n'));

				var bak = new RaiFile(dest.FullName);
				bak.Ext = "bak";
				Assert.False(File.Exists(bak.FullName));
			}
			finally
			{
				root.rmdir(depth: 1, deleteFiles: true);
			}
		}

		[Fact]
		public void RaiFile_rmdir_ThrowsWhenDirectoryNotEmpty()
		{
			var root = CreateTempDir();
			try
			{
				var dirPath = root / "rmdir-throws";
				dirPath.mkdir();
				new TextFile(dirPath, "a", "x").Save();
				Assert.Throws<IOException>(() => dirPath.rmdir(depth: 2, deleteFiles: false));
			}
			finally
			{
				root.rmdir(depth: 2, deleteFiles: true);
			}
		}

		[Fact]
		public void RaiFile_rmdir_DeletesTree_WhenDeleteFilesTrue()
		{
			var root = CreateTempDir();
			try
			{
				var dirPath = root / "rmdir-ok";
				dirPath.mkdir();
				new TextFile(dirPath, "a.txt", "x");
				(dirPath / "child").mkdir();

				var rf = new RaiFile(dirPath.Path);
				rf.rmdir(depth: 1, deleteFiles: true);
				Assert.False(Directory.Exists(dirPath.Path));
			}
			finally
			{
				root.rmdir(depth: 2, deleteFiles: true);
			}
		}

		[Fact]
		public void Script_Save_CreatesExecutableFile()
		{
			var root = CreateTempDir();
			try
			{
				var name = OperatingSystem.IsWindows() ? "echo.cmd" : "echo.sh";
				var content = OperatingSystem.IsWindows()
					? "@echo off\r\necho hello\r\n"
					: "#!/bin/sh\necho hello\n";
				var script = RaiSystem.CreateScript(root, name, content);

				Assert.True(File.Exists(script.FullName));
				Assert.Contains("hello", string.Join("\n", script.ScriptFile.Read()));

				if (OperatingSystem.IsWindows())
					return;

				Assert.True(File.GetUnixFileMode(script.FullName).HasFlag(UnixFileMode.UserExecute));
			}
			finally
			{
				root.rmdir(depth: 2, deleteFiles: true);
			}
		}

		[Fact]
		public void RaiFileExtensions_Singularize_CollapsesRepeatedChars()
		{
			var result = "a   b".Singularize(' ');
			Assert.Equal("a b", result);
		}

		[Fact]
		public void RaiFileExtensions_MakePolicyCompliant_RemovesEmptyLines()
		{
			var input = new List<string> { "a  b           \n\t\t\te", "", "c    d" };
			var result = input.MakePolicyCompliant(tabbed: false);
			Assert.Equal(3, result.Count);
			Assert.Equal("a b", result[0]);
			Assert.Equal("			e", result[1]);	// "\t\t\te"
			Assert.Equal("c d", result[2]);
		}

		[Fact]
		public void RaiFileExtensions_CreateDictionariesFromCsvLines_ParsesTabSeparated()
		{
			var lines = "a\tb\n1\t2\n3\t4";
			var result = lines.CreateDictionariesFromCsvLines(tabbed: true);
			Assert.Equal(2, result.Count);
			Assert.Equal("1", result[0]["a"]);
			Assert.Equal("2", result[0]["b"]);
			Assert.Equal("3", result[1]["a"]);
			Assert.Equal("4", result[1]["b"]);
		}

		[Fact]
		public void RaiSystem_Exec_CapturesOutput()
		{
			string command;
			string args;
			if (Os.IsUnixLike)
			{
				command = "/bin/echo";
				args = "hello";
			}
			else
			{
				command = "cmd.exe";
				args = "/c echo hello";
			}

			var rs = new RaiSystem(command, args);
			var exitCode = rs.Exec(out var msg);
			Assert.Equal(0, exitCode);
			Assert.Contains("hello", msg);
		}

		[Fact]
		public void RaiSystem_CommandAndArgsConstructor_SupportsExecutablePathsWithSpaces()
		{
			var root = CreateTempDir();
			try
			{
				string scriptPath;
				string args;
				if (OperatingSystem.IsWindows())
				{
					scriptPath = CreateExecutableScript(root, "echo args.cmd", "@echo off\r\necho %1\r\n");
					args = "hello";
				}
				else
				{
					scriptPath = CreateExecutableScript(root, "echo args.sh", "#!/bin/sh\necho \"$1\"\n");
					args = "hello";
				}

				var rs = new RaiSystem(scriptPath, args);
				var exitCode = rs.Exec(out var msg);
				Assert.Equal(0, exitCode);
				Assert.Contains("hello", msg);
			}
			finally
			{
				root.rmdir(depth: 2, deleteFiles: true);
			}
		}

		[Fact]
		public void RaiSystem_CommandLineConstructor_SupportsQuotedExecutablePaths()
		{
			var root = CreateTempDir();
			try
			{
				string scriptPath;
				string commandLine;
				if (OperatingSystem.IsWindows())
				{
					scriptPath = CreateExecutableScript(root, "echo args.cmd", "@echo off\r\necho %1\r\n");
					commandLine = $"\"{scriptPath}\" hello";
				}
				else
				{
					scriptPath = CreateExecutableScript(root, "echo args.sh", "#!/bin/sh\necho \"$1\"\n");
					commandLine = $"\"{scriptPath}\" hello";
				}

				var rs = new RaiSystem(commandLine);
				var exitCode = rs.Exec(out var msg);
				Assert.Equal(0, exitCode);
				Assert.Contains("hello", msg);
			}
			finally
			{
				root.rmdir(depth: 2, deleteFiles: true);
			}
		}

		[Fact]
		public void FileInfo_AcceptsForwardSlashPath_OnWindows()
		{
			if (!OperatingSystem.IsWindows())
				return;

			var root = CreateTempDir();
			try
			{
				var nested = root / "nested";
				nested.mkdir();
				var sample = new TextFile(nested, "sample.txt", "data");
				var filePath = sample.FullName;

				var forwardSlashPath = filePath.Replace('\\', '/');
				var info = new FileInfo(forwardSlashPath);

				Assert.True(info.Exists);
				Assert.Equal("sample.txt", info.Name);
			}
			finally
			{
				root.rmdir(depth: 2, deleteFiles: true);
			}
		}

		[Fact]
		public void ShellHelper_Bash_RunsCommand_OnUnix()
		{
			if (!Os.IsUnixLike)
				return;
			var result = "echo hello".Bash();
			Assert.Contains("hello", result);
		}

		private static bool IsUbuntuRuntime()
		{
			try
			{
				const string osRelease = "/etc/os-release";
				if (!File.Exists(osRelease))
					return false;

				foreach (var line in File.ReadAllLines(osRelease))
				{
					if (!line.StartsWith("ID=", StringComparison.OrdinalIgnoreCase) &&
						!line.StartsWith("ID_LIKE=", StringComparison.OrdinalIgnoreCase))
						continue;

					var value = line.Substring(line.IndexOf('=') + 1).Trim().Trim('"');
					if (value.Contains("ubuntu", StringComparison.OrdinalIgnoreCase))
						return true;
				}
			}
			catch
			{
			}

			return false;
		}
	}
}
