using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RunProcessAsTask; // https://github.com/jamesmanning/RunProcessAsTask

//using Hangfire; // https://www.hangfire.io/overview.html
/*
*	based on RsbSystem (C++ version from 1991, C# version 2005, dotnet core 2019)
*/

namespace OsLib     // aka OsLibCore
{
	/// <summary>
	/// Helpers for running shell commands.
	/// </summary>
	public static class ShellHelper
	{
		/// <summary>
		/// Run a command via /bin/bash and return standard output.
		/// </summary>
		public static string Bash(this string cmd)
		{
			var escapedArgs = cmd.Replace("\"", "\\\"");

			var process = new Process()
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "/bin/bash",
					Arguments = $"-c \"{escapedArgs}\"",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				}
			};
			process.Start();
			string result = process.StandardOutput.ReadToEnd();
			process.WaitForExit();
			return result;
		}
	}
	/// <summary>
	/// Run external processes with optional output capture.
	/// </summary>
	public sealed class RaiSystemResult
	{
		public string Command { get; init; } = string.Empty;
		public string Arguments { get; init; } = string.Empty;
		public string CommandLine { get; init; } = string.Empty;
		public string StandardOutput { get; init; } = string.Empty;
		public string StandardError { get; init; } = string.Empty;
		public string Output { get; init; } = string.Empty;
		public int ExitCode { get; init; }
		public bool TimedOut { get; init; }
		public int WorkerThreadId { get; init; }
	}

	public class RaiSystem
	{
		string command = null;
		string param = null;
		string commandLine = null;
		readonly List<string> argumentList = new();
		public static string IndirectShellExecFile = new RaiFile("~/bin/start").FullName;
		public int ExitCode = 0;
		public string this[string environmentVariable]
		{
			get
			{
				var p = new Process();
				return p.StartInfo.EnvironmentVariables[environmentVariable];
			}
		}
		/// <summary>
		/// Execute a command and capture standard output and error.
		/// </summary>
		/// <param name="msg">returns output of called program</param>
		/// <returns>0 if ok</returns>
		/// <remarks>RsbSystem instance keeps the result in member ExitCode</remarks>
		public int Exec(out string msg)
		{
			var result = ExecResult();
			msg = result.Output.TrimEnd();
			return result.ExitCode;
		}
		public RaiSystemResult ExecResult(int timeoutMilliseconds = 120000)
		{
			using var p = new Process();
			p.StartInfo = CreateStartInfo(redirectStandardOutput: true, redirectStandardError: true);
			p.EnableRaisingEvents = true;
			p.Start();

			var standardOutput = p.StandardOutput.ReadToEnd();
			var standardError = p.StandardError.ReadToEnd();
			var timedOut = timeoutMilliseconds > 0 && !p.WaitForExit(timeoutMilliseconds);
			if (timedOut)
			{
				try
				{
					p.Kill(entireProcessTree: true);
				}
				catch
				{
				}
			}
			else if (timeoutMilliseconds <= 0)
			{
				p.WaitForExit();
			}

			ExitCode = timedOut ? -1 : p.ExitCode;
			return new RaiSystemResult
			{
				Command = command ?? string.Empty,
				Arguments = param ?? string.Empty,
				CommandLine = commandLine ?? string.Empty,
				StandardOutput = standardOutput,
				StandardError = standardError,
				Output = standardOutput + standardError,
				ExitCode = ExitCode,
				TimedOut = timedOut,
				WorkerThreadId = Environment.CurrentManagedThreadId
			};
		}
		public Task<RaiSystemResult> ExecAsync(CancellationToken cancellationToken = default)
		{
			return Task.Factory.StartNew(() =>
			{
				cancellationToken.ThrowIfCancellationRequested();
				return ExecResult();
			}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
		}
		/// <summary>
		/// Execute a command, optionally waiting for it to exit.
		/// </summary>
		/// <param name="wait">waits for the process to exit</param>
		/// <returns>null or process</returns>
		/// <remarks>RsbSystem instance keeps the result in member ExitCode if wait==true</remarks>
		public Process Exec(bool wait = true)
		{
			var p = new Process();
			p.StartInfo = CreateStartInfo(redirectStandardOutput: true, redirectStandardError: false);
			var result = p.Start();
			if (wait)
			{
				if (!p.StandardOutput.EndOfStream)
				{
					_ = p.StandardOutput.ReadToEnd();
				}
				p.WaitForExit(120000);  // don't wait more than 2 min
				ExitCode = p.ExitCode;
			}
			return p;
		}
		/// <summary>
		/// Execute a command asynchronously.
		/// </summary>
		public async Task<ProcessResults> Start()
		{
			var results = await ProcessEx.RunAsync(command, param);
			return results;
		}
		public string FireAndForget()
		{
			// var jobId = BackgroundJob.Enqueue(
			// 	() => Console.WriteLine("Fire-and-forget!"));
			// return jobId;
			return "not implemented";
		}
		public static Script CreateScript(RaiPath path, string name, string ext, string content = null)
		{
			if (ext.Length > 5)
				throw new ArgumentException($"Extension suspiciously long: {ext} - are you trying to pass-in the content in the ext parameter?", nameof(ext));
			return new Script(path, name, ext: ext, content: content ?? string.Empty);
		}
		public RaiSystem(string cmdLine)
		{
			commandLine = cmdLine ?? "";
			(command, param) = SplitCommandLine(commandLine);
		}
		public RaiSystem(string cmd, string p)
		{
			command = cmd ?? "";
			param = p ?? "";
			commandLine = string.IsNullOrWhiteSpace(param) ? command : command + " " + param;
		}
		public RaiSystem(string cmd, IEnumerable<string> args)
		{
			command = cmd ?? "";
			if (args != null)
				argumentList.AddRange(args);
			param = string.Join(" ", argumentList);
			commandLine = string.IsNullOrWhiteSpace(param) ? command : command + " " + param;
		}

		private ProcessStartInfo CreateStartInfo(bool redirectStandardOutput, bool redirectStandardError)
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = command,
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardOutput = redirectStandardOutput,
				RedirectStandardError = redirectStandardError
			};

			if (argumentList.Count > 0)
			{
				foreach (var arg in argumentList)
					startInfo.ArgumentList.Add(arg ?? string.Empty);
			}
			else startInfo.Arguments = param;

			return startInfo;
		}

		private static (string command, string param) SplitCommandLine(string cmdLine)
		{
			if (string.IsNullOrWhiteSpace(cmdLine))
				return ("", "");

			var trimmed = cmdLine.Trim();
			if (trimmed[0] == '"')
			{
				var closingQuote = FindClosingQuote(trimmed);
				if (closingQuote < 0)
					return (trimmed.Trim('"'), "");

				var parsedCommand = trimmed.Substring(1, closingQuote - 1);
				var parsedParam = closingQuote + 1 < trimmed.Length
					? trimmed.Substring(closingQuote + 1).TrimStart()
					: "";
				return (parsedCommand, parsedParam);
			}

			var splitPos = trimmed.IndexOf(' ');
			if (splitPos < 0)
				return (trimmed, "");

			return (trimmed.Substring(0, splitPos), trimmed.Substring(splitPos + 1).TrimStart());
		}

		private static int FindClosingQuote(string value)
		{
			for (int i = 1; i < value.Length; i++)
			{
				if (value[i] == '"' && value[i - 1] != '\\')
					return i;
			}
			return -1;
		}
	}

	/// <summary>
	/// Windows network drive mount helper.
	/// </summary>
	public class RaiNetDrive : RaiSystem
	{
		/// <summary></summary>
		/// <param name="drive">todo: describe drive parameter on Mount</param>
		/// <param name="path">todo: describe path parameter on Mount</param>
		/// <param name="user">todo: describe user parameter on Mount</param>
		/// <param name="pwd">todo: describe pwd parameter on Mount</param>
		/// <param name="msg">todo: describe msg parameter on Mount</param>
		public int Mount(string drive, string path, string user, string pwd, ref string msg)
		{
			if (System.IO.Directory.Exists(drive + ":\\"))
			{
				var devnul = new string(' ', 80);
				Unmount(drive, ref devnul);
			}
			using (var p = new Process())
			{
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.RedirectStandardError = true;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.FileName = "net.exe";
				p.StartInfo.Arguments = " use " + drive + ": " + path + " /user:" + user + " " + pwd;
				p.Start();
				p.WaitForExit();
				ExitCode = p.ExitCode;
				msg = p.StandardOutput.ReadToEnd();
				msg += p.StandardError.ReadToEnd();
			}
			return ExitCode;
		}
		/// <summary>
		/// Unmount a network drive
		/// </summary>
		/// <param name="drive"></param>
		/// <param name="msg">todo: describe msg parameter on Unmount</param>
		/// <returns>0 if successful</returns>
		/// <remarks>replaces addDrive</remarks>
		public int Unmount(string drive, ref string msg)
		{
			using (var p = new System.Diagnostics.Process())
			{
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.RedirectStandardError = true;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.FileName = "net.exe";
				p.StartInfo.Arguments = " use " + drive + ": /DELETE";
				p.Start();
				p.WaitForExit();
				ExitCode = p.ExitCode;
				msg = p.StandardOutput.ReadToEnd();
				msg += p.StandardError.ReadToEnd();
			}
			return ExitCode;
		}
		public RaiNetDrive()
			 : base("")
		{
		}
	}
}
