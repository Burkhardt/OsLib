#nullable enable
using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace OsLib;

internal static class CloudRoles
{
	internal static string Live(JObject config) => (string?)config["LiveCloud"]
		?? (string?)(config["DefaultCloudOrder"] as JArray)?.FirstOrDefault()
		?? throw new InvalidOperationException("LiveCloud is unset and DefaultCloudOrder has no first provider.");
	internal static string? Test(JObject config) => (string?)config["TestCloud"];
	internal static string Root(JObject config, string provider)
	{
		var cloud = config["Cloud"] as JObject;
		var value = cloud?.Properties().FirstOrDefault(p => p.Name.Equals(provider, StringComparison.OrdinalIgnoreCase))?.Value;
		if (value?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string?)value)) return value.Value<string>()!;
		throw new InvalidOperationException($"Cloud.{provider} is not configured. Configured Cloud entries: {Names(config)}");
	}
	internal static void Validate(JObject config)
	{
		foreach (var key in new[] { "LiveCloud", "TestCloud" })
		{
			if (!config.TryGetValue(key, out var value)) continue;
			var root = value.Type == JTokenType.String ? (config["Cloud"] as JObject)?[value.Value<string>()!] : null;
			if (root?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string?)root))
				throw new InvalidOperationException($"{key} must name a configured Cloud entry; got '{value}'. Configured Cloud entries: {Names(config)}");
		}
	}
	private static string Names(JObject config) => string.Join(", ", (config["Cloud"] as JObject)?.Properties().Select(p => p.Name) ?? Array.Empty<string>());
}
