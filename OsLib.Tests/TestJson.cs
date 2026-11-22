using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OsLib.Tests;

internal static class TestJson
{
	internal static JObject ParseObject(string json) =>
		JsonConvert.DeserializeObject<JObject>(json, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None })
		?? throw new JsonReaderException("Expected a JSON object.");
}
