using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace OsLib.Tests;

internal sealed record TestLogEntry(LogLevel Level, string Category, string Message, Exception? Exception);

internal sealed class TestLoggerFactory : ILoggerFactory
{
	private readonly ConcurrentQueue<TestLogEntry> entries = new();

	internal IReadOnlyCollection<TestLogEntry> Entries => entries.ToArray();

	public void AddProvider(ILoggerProvider provider)
	{
	}

	public ILogger CreateLogger(string categoryName)
	{
		return new TestLogger(categoryName, entries);
	}

	public void Dispose()
	{
	}

	private sealed class TestLogger : ILogger
	{
		private readonly string categoryName;
		private readonly ConcurrentQueue<TestLogEntry> entries;

		public TestLogger(string categoryName, ConcurrentQueue<TestLogEntry> entries)
		{
			this.categoryName = categoryName;
			this.entries = entries;
		}

		public IDisposable BeginScope<TState>(TState state) where TState : notnull
		{
			return NullScope.Instance;
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return true;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			entries.Enqueue(new TestLogEntry(logLevel, categoryName, formatter(state, exception), exception));
		}

		private sealed class NullScope : IDisposable
		{
			public static readonly NullScope Instance = new();

			public void Dispose()
			{
			}
		}
	}
}

internal sealed class TestStartupDiagnosticSink : IStartupDiagnosticSink
{
	private readonly List<string> messages = new();

	internal IReadOnlyList<string> Messages => messages;

	public void WriteError(string message)
	{
		messages.Add(message);
	}
}