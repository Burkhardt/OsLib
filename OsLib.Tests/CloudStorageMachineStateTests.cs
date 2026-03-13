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
		public void MachineCloudState_PrintsDiscoveryInputs_AndProviderStatus_WithoutFailingForMissingRoots()
		{
			Console.WriteLine("Cloud discovery environment variables:");
			foreach (var envVar in new[]
			{
				"HOME",
				"USERPROFILE",
				"APPDATA",
				"LOCALAPPDATA"
			})
			{
				var value = Environment.GetEnvironmentVariable(envVar);
				Console.WriteLine($"- {envVar}: {(string.IsNullOrWhiteSpace(value) ? "<unset>" : value)}");
			}
			Console.WriteLine($"- active config path: {Os.GetDefaultConfigPath()}");

			Console.WriteLine("Cloud discovery config candidates:");
			foreach (var candidate in GetConfigCandidates())
				Console.WriteLine($"- {candidate}: {(File.Exists(candidate) ? "found" : "missing")}");

			Os.ResetCloudStorageCache();
			var report = Os.GetCloudDiscoveryReport(refresh: true);
			Console.WriteLine(report);

			foreach (var provider in Enum.GetValues<CloudStorageType>())
			{
				var root = Os.GetCloudStorageRoot(provider);
				var status = string.IsNullOrWhiteSpace(root)
					? "not found"
					: Directory.Exists(root) ? "directory exists" : "configured but directory missing";
				Console.WriteLine($"Provider {provider}: {status}{(string.IsNullOrWhiteSpace(root) ? string.Empty : $" -> {root}")}");
			}

			Assert.Contains("Dropbox", report);
			Assert.Contains("OneDrive", report);
			Assert.Contains("GoogleDrive", report);
			Assert.Contains("ICloud", report);
		}

		private static IEnumerable<string> GetConfigCandidates()
		{
			yield return Os.GetDefaultConfigPath();
		}
	}
}