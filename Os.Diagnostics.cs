using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
namespace OsLib
{
	internal sealed class OsDiagnosticsLogScope
	{
	}
	public interface IStartupDiagnosticSink
	{
		void WriteError(string message);
	}
	public sealed class ConsoleErrorStartupDiagnosticSink : IStartupDiagnosticSink
	{
		public void WriteError(string message)
		{
			if (string.IsNullOrWhiteSpace(message))
				return;
			Console.Error.WriteLine(message);
		}
	}
	public static partial class Os
	{
		private static ILoggerFactory loggerFactory = NullLoggerFactory.Instance;
		private static IStartupDiagnosticSink startupDiagnosticSink = new ConsoleErrorStartupDiagnosticSink();
		private static readonly HashSet<string> emittedDiagnostics = new(StringComparer.OrdinalIgnoreCase);
		public static void ConfigureDiagnostics(ILoggerFactory loggerFactory, IStartupDiagnosticSink startupDiagnosticSink = null)
		{
			Os.loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
			Os.startupDiagnosticSink = startupDiagnosticSink ?? new ConsoleErrorStartupDiagnosticSink();
			resetDiagnosticState();
		}
		internal static ILogger<TCategory> GetLogger<TCategory>()
		{
			return loggerFactory.CreateLogger<TCategory>();
		}
		internal static void LogDebug<TCategory>(string message, params object[] args)
		{
			GetLogger<TCategory>().LogDebug(message, args);
		}
		internal static void LogInformation<TCategory>(string message, params object[] args)
		{
			GetLogger<TCategory>().LogInformation(message, args);
		}
		internal static void LogWarning<TCategory>(string message, params object[] args)
		{
			GetLogger<TCategory>().LogWarning(message, args);
		}
		internal static void LogWarningOnce<TCategory>(string key, string message, params object[] args)
		{
			if (!tryRegisterDiagnostic(key))
				return;
			GetLogger<TCategory>().LogWarning(message, args);
		}
		internal static void LogError<TCategory>(Exception ex, string message, params object[] args)
		{
			GetLogger<TCategory>().LogError(ex, message, args);
		}
		internal static void resetDiagnosticsForTesting()
		{
			loggerFactory = NullLoggerFactory.Instance;
			startupDiagnosticSink = new ConsoleErrorStartupDiagnosticSink();
			resetDiagnosticState();
		}
		private static bool tryRegisterDiagnostic(string key)
		{
			if (string.IsNullOrWhiteSpace(key))
				return true;
			lock (emittedDiagnostics)
				return emittedDiagnostics.Add(key);
		}
		private static void resetDiagnosticState()
		{
			lock (emittedDiagnostics)
				emittedDiagnostics.Clear();
		}
	}
}