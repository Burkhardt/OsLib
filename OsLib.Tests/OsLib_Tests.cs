using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using OsLib;

namespace OsLib.Tests
{
	public class OsLibTests
	{
		private static void ResetOsCaches()
		{
			var osType = typeof(Os);
			osType.GetField("homeDir", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
			osType.GetField("type", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
			osType.GetField("dIRSEPERATOR", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
		}

		private static string CreateTempDir()
		{
			var root = Path.Combine(Path.GetTempPath(), "OsLibTests", Guid.NewGuid().ToString("N")) + Path.DirectorySeparatorChar;
			Directory.CreateDirectory(root);
			return root;
		}

		private static string CreateExecutableScript(string root, string scriptName, string content)
		{
			var scriptPath = Path.Combine(root, scriptName);
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
		public void Os_Type_UsesRuntimePlatformDetection()
		{
			ResetOsCaches();
			var expected = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
				? OsType.Windows
				: OsType.UNIX;

			Assert.Equal(expected, Os.Type);
		}

		[Fact]
		public void Os_DirSeparator_MatchesSystemDirectorySeparatorChar()
		{
			ResetOsCaches();
			Assert.Equal(Path.DirectorySeparatorChar.ToString(), Os.DIRSEPERATOR);
		}

		[Fact]
		public void Os_HomeDir_UsesWindowsVariables_OnWindows()
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

				Assert.Equal("C:\\Users\\UnitTestUser", Os.HomeDir);
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
		public void Os_HomeDir_UsesHomeVariable_OnUnix()
		{
			if (Os.Type != OsType.UNIX)
				return;

			var oldHome = Environment.GetEnvironmentVariable("HOME");
			try
			{
				Environment.SetEnvironmentVariable("HOME", "/tmp/oslib-home-unittest");
				ResetOsCaches();

				Assert.Equal("/tmp/oslib-home-unittest", Os.HomeDir);
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
				var rf = new RaiFile(Path.Combine(root, "file.test.txt"));
				Assert.Equal("file.test", rf.Name);
				Assert.Equal("txt", rf.Ext);
				Assert.EndsWith(Os.DIRSEPERATOR, rf.Path);
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void RaiFile_CopyMoveRemove_WorksOnDisk()
		{
			var root = CreateTempDir();
			try
			{
				var source = new RaiFile(Path.Combine(root, "source.txt"));
				File.WriteAllText(source.FullName, "data");
				var copy = new RaiFile(Path.Combine(root, "copy.txt"));
				copy.cp(source);
				Assert.True(File.Exists(copy.FullName));
				Assert.Equal("data", File.ReadAllText(copy.FullName));

				var moved = new RaiFile(Path.Combine(root, "moved.txt"));
				moved.mv(copy);
				Assert.True(File.Exists(moved.FullName));
				Assert.False(File.Exists(copy.FullName));

				moved.rm();
				Assert.False(File.Exists(moved.FullName));
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void RaiFile_Move_ReplaceFalse_ThrowsWhenDestinationExists()
		{
			var root = CreateTempDir();
			try
			{
				var source = new RaiFile(Path.Combine(root, "source.txt"));
				File.WriteAllText(source.FullName, "src");
				var dest = new RaiFile(Path.Combine(root, "dest.txt"));
				File.WriteAllText(dest.FullName, "dest");

				Assert.Throws<IOException>(() => dest.mv(source, replace: false, keepBackup: false));
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void RaiFile_Move_KeepBackup_CreatesBakFile()
		{
			var root = CreateTempDir();
			try
			{
				var source = new RaiFile(Path.Combine(root, "source.txt"));
				File.WriteAllText(source.FullName, "src");
				var dest = new RaiFile(Path.Combine(root, "dest.txt"));
				File.WriteAllText(dest.FullName, "dest");

				dest.mv(source, replace: true, keepBackup: true);
				Assert.True(File.Exists(dest.FullName));
				Assert.Equal("src", File.ReadAllText(dest.FullName));

				var bak = new RaiFile(dest.FullName);
				bak.Ext = "bak";
				Assert.True(File.Exists(bak.FullName));
				Assert.Equal("dest", File.ReadAllText(bak.FullName));
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void RaiFile_Move_NoBackup_RemovesBakFile()
		{
			var root = CreateTempDir();
			try
			{
				var source = new RaiFile(Path.Combine(root, "source.txt"));
				File.WriteAllText(source.FullName, "src");
				var dest = new RaiFile(Path.Combine(root, "dest.txt"));
				File.WriteAllText(dest.FullName, "dest");

				dest.mv(source, replace: true, keepBackup: false);
				Assert.True(File.Exists(dest.FullName));
				Assert.Equal("src", File.ReadAllText(dest.FullName));

				var bak = new RaiFile(dest.FullName);
				bak.Ext = "bak";
				Assert.False(File.Exists(bak.FullName));
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void RaiFile_rmdir_ThrowsWhenDirectoryNotEmpty()
		{
			var root = CreateTempDir();
			try
			{
				var dirPath = new RaiPath(Path.Combine(root, "rmdir-throws")).Path;
				Directory.CreateDirectory(dirPath);
				File.WriteAllText(Path.Combine(dirPath, "a.txt"), "x");
				var rf = new RaiFile(dirPath);

				Assert.Throws<IOException>(() => rf.rmdir());
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void RaiFile_rmdir_DeletesTree_WhenDeleteFilesTrue()
		{
			var root = CreateTempDir();
			try
			{
				var dirPath = new RaiPath(Path.Combine(root, "rmdir-ok")).Path;
				Directory.CreateDirectory(dirPath);
				File.WriteAllText(Path.Combine(dirPath, "a.txt"), "x");
				Directory.CreateDirectory(Path.Combine(dirPath, "child"));

				var rf = new RaiFile(dirPath);
				rf.rmdir(depth: 1, deleteFiles: true);
				Assert.False(Directory.Exists(dirPath));
			}
			finally
			{
				Directory.Delete(root, recursive: true);
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
			if (Os.Type == OsType.UNIX)
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
				Directory.Delete(root, recursive: true);
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
				Directory.Delete(root, recursive: true);
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
				var filePath = Path.Combine(root, "nested", "sample.txt");
				var directoryPath = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException("Expected a parent directory.");
				Directory.CreateDirectory(directoryPath);
				File.WriteAllText(filePath, "data");

				var forwardSlashPath = filePath.Replace('\\', '/');
				var info = new FileInfo(forwardSlashPath);

				Assert.True(info.Exists);
				Assert.Equal("sample.txt", info.Name);
			}
			finally
			{
				Directory.Delete(root, recursive: true);
			}
		}

		[Fact]
		public void ShellHelper_Bash_RunsCommand_OnUnix()
		{
			if (Os.Type != OsType.UNIX)
				return;
			var result = "echo hello".Bash();
			Assert.Contains("hello", result);
		}
	}
}
