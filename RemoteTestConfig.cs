using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace OsLib
{
	public sealed class RemoteObserverModel
	{
		[JsonProperty("sshTarget")]
		public string SshTarget { get; internal set; } = string.Empty;

		[JsonProperty("cloudRoots")]
		public Dictionary<CloudStorageType, string> CloudRoots { get; internal set; } = new();

		internal void Normalize()
		{
			SshTarget = SshTarget?.Trim() ?? string.Empty;
			CloudRoots = (CloudRoots ?? new Dictionary<CloudStorageType, string>())
				.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
				.ToDictionary(kvp => kvp.Key, kvp => new RaiPath(kvp.Value).Path);
		}

		internal string GetCloudRoot(CloudStorageType provider)
		{
			return CloudRoots != null && CloudRoots.TryGetValue(provider, out var root)
				? root
				: string.Empty;
		}
	}

	public sealed class RemoteApiModel
	{
		[JsonProperty("baseUrl")]
		public string BaseUrl { get; internal set; } = string.Empty;

		[JsonProperty("timeoutSeconds")]
		public int TimeoutSeconds { get; internal set; } = 180;

		internal void Normalize()
		{
			BaseUrl = BaseUrl?.Trim() ?? string.Empty;
			if (TimeoutSeconds <= 0)
				TimeoutSeconds = 180;
		}
	}

	public sealed class RemoteScenarioModel
	{
		[JsonProperty("provider")]
		public CloudStorageType Provider { get; internal set; } = CloudStorageType.GoogleDrive;

		[JsonProperty("observer")]
		public string Observer { get; internal set; } = string.Empty;

		[JsonProperty("api")]
		public string Api { get; internal set; } = string.Empty;

		[JsonProperty("diskTimeoutSeconds")]
		public int DiskTimeoutSeconds { get; internal set; } = 120;

		[JsonProperty("apiTimeoutSeconds")]
		public int ApiTimeoutSeconds { get; internal set; } = 180;

		[JsonProperty("pollIntervalMilliseconds")]
		public int PollIntervalMilliseconds { get; internal set; } = 1000;

		internal void Normalize()
		{
			Observer = Observer?.Trim() ?? string.Empty;
			Api = Api?.Trim() ?? string.Empty;
			if (DiskTimeoutSeconds <= 0)
				DiskTimeoutSeconds = 120;
			if (ApiTimeoutSeconds <= 0)
				ApiTimeoutSeconds = 180;
			if (PollIntervalMilliseconds <= 0)
				PollIntervalMilliseconds = 1000;
		}
	}

	public sealed class RemoteTestConfigModel
	{
		[JsonProperty("observers")]
		public Dictionary<string, RemoteObserverModel> Observers { get; internal set; } = new(StringComparer.OrdinalIgnoreCase);

		[JsonProperty("apis")]
		public Dictionary<string, RemoteApiModel> Apis { get; internal set; } = new(StringComparer.OrdinalIgnoreCase);

		[JsonProperty("scenarios")]
		public Dictionary<string, RemoteScenarioModel> Scenarios { get; internal set; } = new(StringComparer.OrdinalIgnoreCase);

		internal void Normalize()
		{
			Observers = NormalizeDictionary(Observers, model => model.Normalize());
			Apis = NormalizeDictionary(Apis, model => model.Normalize());
			Scenarios = NormalizeDictionary(Scenarios, model => model.Normalize());
		}

		internal RemoteObserverModel GetObserver(string name)
		{
			if (string.IsNullOrWhiteSpace(name) || Observers == null)
				return null;

			Observers.TryGetValue(name.Trim(), out var observer);
			return observer;
		}

		private static Dictionary<string, TModel> NormalizeDictionary<TModel>(Dictionary<string, TModel> source, Action<TModel> normalize)
			where TModel : class, new()
		{
			var normalized = new Dictionary<string, TModel>(StringComparer.OrdinalIgnoreCase);
			foreach (var kvp in source ?? new Dictionary<string, TModel>())
			{
				var key = kvp.Key?.Trim();
				if (string.IsNullOrWhiteSpace(key))
					continue;

				var model = kvp.Value ?? new TModel();
				normalize(model);
				normalized[key] = model;
			}

			return normalized;
		}
	}

	public sealed class RemoteTestConfigFile : ConfigFile<RemoteTestConfigModel>
	{
		public RemoteTestConfigFile(string fullName) : base(fullName, autoLoad: true)
		{
		}

		protected override RemoteTestConfigModel CreateDefaultData()
		{
			return new RemoteTestConfigModel();
		}

		protected override RemoteTestConfigModel NormalizeData(RemoteTestConfigModel data)
		{
			data ??= CreateDefaultData();
			data.Normalize();
			return data;
		}
	}

	public static partial class Os
	{
		private static RemoteTestConfigFile remoteTestConfig;
		private const string defaultRemoteTestConfigFileName = "remote-test-config.json";

		public static RemoteTestConfigFile RemoteTestConfig
		{
			get
			{
				var configPath = GetDefaultRemoteTestConfigPath();
				if (remoteTestConfig == null)
					remoteTestConfig = new RemoteTestConfigFile(configPath);
				else if (remoteTestConfig.SetFullName(configPath))
					remoteTestConfig.Load();
				return remoteTestConfig;
			}
		}

		public static RemoteTestConfigModel LoadRemoteTestConfig(bool refresh = false)
		{
			if (refresh)
				RemoteTestConfig.Load();

			return RemoteTestConfig.Data;
		}

		public static string GetDefaultRemoteTestConfigPath()
		{
			var configDir = new RaiPath(new RaiFile(defaultConfigFileLocation).Path);
			return new RaiFile(configDir, defaultRemoteTestConfigFileName).FullName;
		}

		public static string GetRemoteTestConfigurationDiagnosticReport(bool refresh = false)
		{
			var config = LoadRemoteTestConfig(refresh);
			var sb = new System.Text.StringBuilder();
			sb.AppendLine("Remote test configuration diagnostics:");
			sb.AppendLine($"- active remote-test config path: {GetDefaultRemoteTestConfigPath()}");
			sb.AppendLine($"- observers: {config.Observers.Count}");
			foreach (var kvp in config.Observers.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
				sb.AppendLine($"  - {kvp.Key}: sshTarget={(string.IsNullOrWhiteSpace(kvp.Value?.SshTarget) ? "<empty>" : kvp.Value.SshTarget)} cloudRoots={kvp.Value?.CloudRoots?.Count ?? 0}");
			sb.AppendLine($"- apis: {config.Apis.Count}");
			foreach (var kvp in config.Apis.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
				sb.AppendLine($"  - {kvp.Key}: baseUrl={(string.IsNullOrWhiteSpace(kvp.Value?.BaseUrl) ? "<empty>" : kvp.Value.BaseUrl)} timeoutSeconds={kvp.Value?.TimeoutSeconds ?? 0}");
			sb.AppendLine($"- scenarios: {config.Scenarios.Count}");
			foreach (var kvp in config.Scenarios.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
				sb.AppendLine($"  - {kvp.Key}: provider={kvp.Value?.Provider} observer={kvp.Value?.Observer ?? string.Empty} api={kvp.Value?.Api ?? string.Empty}");
			return sb.ToString().TrimEnd();
		}
	}
}