using System;

namespace OsLib.Tests;

[Collection("CloudStorageEnvironment")]
public class RemoteSshTests
{
	[Fact]
	public void Mzansi_Ssh_Readiness_Probe_Works()
	{
		using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();
		Console.WriteLine(Os.GetCloudConfigurationDiagnosticReport(refresh: true));
		Console.WriteLine(Os.GetRemoteTestConfigurationDiagnosticReport(refresh: true));

		var observer = Os.LoadRemoteTestConfig(refresh: true).GetObserver("mzansi");
		if (observer == null || string.IsNullOrWhiteSpace(observer.SshTarget))
			Assert.Skip($"Configure observer 'mzansi' in {Os.GetDefaultRemoteTestConfigPath()}.{Environment.NewLine}{Os.GetRemoteTestConfigurationDiagnosticReport()}");

		var probe = new SshFileProbe(observer.SshTarget);
		var result = probe.ExecuteScript("printf ready");

		Assert.Equal(0, result.ExitCode);
		Assert.Equal("ready", result.StandardOutput.Trim());
	}

	[Fact]
	public void Mzansi_GoogleDrive_Root_Is_Readable_From_Remote_OsConfig()
	{
		using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();
		Console.WriteLine(Os.GetCloudConfigurationDiagnosticReport(refresh: true));
		Console.WriteLine(Os.GetRemoteTestConfigurationDiagnosticReport(refresh: true));

		if (!RemoteCloudSyncProbe.TryCreate(CloudStorageType.GoogleDrive, "mzansi", out var probe, out var reason))
			Assert.Skip(reason + Environment.NewLine + Os.GetCloudConfigurationDiagnosticReport() + Environment.NewLine + Os.GetRemoteTestConfigurationDiagnosticReport());

		Assert.True(probe.Observer.DirectoryExists(probe.RemoteCloudRoot.Path), probe.LastFailure);
	}
}