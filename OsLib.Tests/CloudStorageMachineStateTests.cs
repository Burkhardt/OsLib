using System;
using System.Collections.Generic;
using System.IO;
using OsLib;
using Xunit;

namespace OsLib.Tests
{
	[Collection("CloudStorageEnvironment")]
	public class CloudStorageMachineStateTests
	{
		[Fact]
		public void MachineCloudState_PrintsConfiguredProviderStatus_WithoutFailingForMissingRoots()
		{
			using var configuredCloud = CloudStorageRealTestEnvironment.BeginConfiguredCloudResolution();

			Console.WriteLine("Cloud configuration state:");
			Console.WriteLine($"- active config path: {Os.ConfigFileFullName}");
			Console.WriteLine($"- config file exists: {File.Exists(Os.ConfigFileFullName)}");

			Console.WriteLine("Cloud discovery config candidates:");
			foreach (var candidate in GetConfigCandidates())
				Console.WriteLine($"- {candidate}: {(File.Exists(candidate) ? "found" : "missing")}");

			var report = Os.GetCloudConfigurationDiagnosticReport();
			Console.WriteLine(report);

			foreach (var provider in Enum.GetValues<Cloud>())
			{
				var configuredRoot = string.Empty;
				try
				{
					configuredRoot = Os.GetCloudStorageRoot(provider)?.Path ?? string.Empty;
				}
				catch
				{
					configuredRoot = string.Empty;
				}

				var root = configuredRoot;
				var status = string.IsNullOrWhiteSpace(root)
					? "not configured"
					: Directory.Exists(root) ? "directory exists" : "configured but directory missing";
				Console.WriteLine($"Provider {provider}: {status}{(string.IsNullOrWhiteSpace(root) ? string.Empty : $" -> {root}")}");
			}

			Assert.Contains("Dropbox", report);
			Assert.Contains("OneDrive", report);
			Assert.Contains("GoogleDrive", report);
		}

		private static IEnumerable<string> GetConfigCandidates()
		{
			yield return Os.ConfigFileFullName;
		}
	}
}