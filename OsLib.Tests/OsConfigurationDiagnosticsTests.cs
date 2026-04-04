using System.IO;
using Microsoft.Extensions.Logging;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class OsConfigurationDiagnosticsTests
{
	[Fact]
	public void LoadConfig_MissingConfig_LogsError_AndWritesStartupDiagnostic()
	{
		var root = OsTestEnvironment.NewTestRoot("os-diagnostics");
		using var env = new OsTestEnvironment(root);
		env.DeleteConfig();

		var loggerFactory = new TestLoggerFactory();
		var startupSink = new TestStartupDiagnosticSink();
		Os.ConfigureDiagnostics(loggerFactory, startupSink);

		_ = Os.LoadConfig();

		Assert.Contains(loggerFactory.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("missing", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(startupSink.Messages, message => message.Contains("degraded mode", StringComparison.OrdinalIgnoreCase));
		Assert.False(File.Exists(env.ConfigPath));
	}

	[Fact]
	public void LoadConfig_MalformedConfig_LogsError_AndWritesStartupDiagnostic()
	{
		var root = OsTestEnvironment.NewTestRoot("os-diagnostics");
		using var env = new OsTestEnvironment(root);

		var invalidConfig = new TextFile(env.ConfigPath);
		invalidConfig.Append("{ invalid json");
		invalidConfig.Save();
		OsTestEnvironment.ResetOsCaches();

		var loggerFactory = new TestLoggerFactory();
		var startupSink = new TestStartupDiagnosticSink();
		Os.ConfigureDiagnostics(loggerFactory, startupSink);

		_ = Os.LoadConfig();

		Assert.Contains(loggerFactory.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("malformed", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(startupSink.Messages, message => message.Contains("malformed", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void TempDir_FallsBackWithLogWarning_ButNoConsole_WhenConfigIsValid()
	{
		var root = OsTestEnvironment.NewTestRoot("os-diagnostics");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig();

		var loggerFactory = new TestLoggerFactory();
		var startupSink = new TestStartupDiagnosticSink();
		OsTestEnvironment.ResetOsCaches();
		Os.ConfigureDiagnostics(loggerFactory, startupSink);

		var tempDir = Os.TempDir;

		Assert.False(string.IsNullOrWhiteSpace(tempDir.Path));
		Assert.Contains(loggerFactory.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("TempDir", StringComparison.OrdinalIgnoreCase));
		Assert.Empty(startupSink.Messages);
	}

	[Fact]
	public void LocalBackupDir_FallsBackWithLogWarning_ButNoConsole_WhenConfigIsValid()
	{
		var root = OsTestEnvironment.NewTestRoot("os-diagnostics");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig();

		var loggerFactory = new TestLoggerFactory();
		var startupSink = new TestStartupDiagnosticSink();
		OsTestEnvironment.ResetOsCaches();
		Os.ConfigureDiagnostics(loggerFactory, startupSink);

		var localBackupDir = Os.LocalBackupDir;

		Assert.False(new RaiFile(localBackupDir.Path).Cloud);
		Assert.Contains(loggerFactory.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("LocalBackupDir", StringComparison.OrdinalIgnoreCase));
		Assert.Empty(startupSink.Messages);
	}

	[Fact]
	public void CloudStorageRootDir_WhenUnavailable_LogsError_AndWritesStartupDiagnostic()
	{
		var root = OsTestEnvironment.NewTestRoot("os-diagnostics");
		using var env = new OsTestEnvironment(root);
		env.WriteConfig();

		var loggerFactory = new TestLoggerFactory();
		var startupSink = new TestStartupDiagnosticSink();
		OsTestEnvironment.ResetOsCaches();
		Os.ConfigureDiagnostics(loggerFactory, startupSink);

		var ex = Assert.Throws<DirectoryNotFoundException>(() => _ = Os.CloudStorageRootDir);

		Assert.Contains("No cloud storage root could be discovered", ex.Message);
		Assert.Contains(loggerFactory.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("No cloud storage root", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(startupSink.Messages, message => message.Contains("cloud storage root", StringComparison.OrdinalIgnoreCase));
	}
}