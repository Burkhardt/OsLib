using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Xunit;

namespace OsLib.Tests;

/// <summary>
/// CR003 / recovery-concept scenarios 21–23 — OsLib's generic create-once event files
/// and stateless, schema-agnostic event aggregation on a real configured cloud root.
/// </summary>
public sealed class EventFileTests : IDisposable
{
	private readonly List<RaiPath> cleanup = new();

	public void Dispose()
	{
		foreach (var path in cleanup)
		{
			try { path.rmdir(depth: 5, deleteFiles: true); }
			catch { }
		}
	}

	private RaiPath NewCloudRoot(string label)
	{
		var root = ConfiguredCloud.RequireRoot("events", $"{label}-{Guid.NewGuid():N}");
		root.mkdir();
		cleanup.Add(root);
		return root;
	}

	[Fact]
	public void CanonicalJson_OrdersPropertiesOrdinally_PreservesArrayOrder_EmitsCompactInvariantJson()
	{
		var a = JObject.Parse(@"{ ""b"": 1, ""a"": { ""z"": true, ""y"": [3, 1, 2] } }");
		var b = JObject.Parse(@"{ ""a"": { ""y"": [3, 1, 2], ""z"": true }, ""b"": 1 }");
		Assert.Equal(CanonicalJson.Canonicalize(a), CanonicalJson.Canonicalize(b));
		Assert.Equal(@"{""a"":{""y"":[3,1,2],""z"":true},""b"":1}", CanonicalJson.Canonicalize(a));
		// Array order is significant: a reordered array is different content.
		var c = JObject.Parse(@"{ ""a"": { ""y"": [1, 2, 3], ""z"": true }, ""b"": 1 }");
		Assert.NotEqual(CanonicalJson.Sha256Hex(CanonicalJson.Canonicalize(a)),
			CanonicalJson.Sha256Hex(CanonicalJson.Canonicalize(c)));
		// A changed value produces a different hash.
		var d = JObject.Parse(@"{ ""a"": { ""y"": [3, 1, 2], ""z"": false }, ""b"": 1 }");
		Assert.NotEqual(CanonicalJson.Sha256Hex(CanonicalJson.Canonicalize(a)),
			CanonicalJson.Sha256Hex(CanonicalJson.Canonicalize(d)));
	}

	[Fact]
	public void EventFile_ConstructorWritesImmediately_CreatesEventsChild_HashNamedSingleObject()
	{
		var root = NewCloudRoot("ctor-write");
		var content = new JObject { ["Stage"] = "Completed", ["Message"] = "hello" };
		var eventFile = new EventFile(root, "1000_TestMachine-app-1_Completed", content);

		Assert.True(eventFile.Exists(), "The constructor performs the create-once write.");
		Assert.StartsWith((root / EventDirectory.Name).FullPath, eventFile.FullName);
		Assert.EndsWith($"_{eventFile.ContentSha256}.event", eventFile.NameWithExtension);

		var bytes = File.ReadAllText(eventFile.FullName, new UTF8Encoding(false));
		Assert.StartsWith("{", bytes);
		Assert.EndsWith("}", bytes);
		Assert.Equal(eventFile.CanonicalContent, bytes); // exact bytes, no trailing terminator
		Assert.Equal(CanonicalJson.Sha256Hex(bytes), eventFile.ContentSha256);
	}

	[Fact]
	public void EventFile_SameStemAndContent_IsIdempotent_SameStemDifferentContent_GetsDifferentHashPath()
	{
		var root = NewCloudRoot("idempotent");
		var stem = "2000_TestMachine-app-1_RoleDetermined";
		var content = new JObject { ["Role"] = "Master" };

		var first = new EventFile(root, stem, content);
		var firstWriteTime = File.GetLastWriteTimeUtc(first.FullName);
		var second = new EventFile(root, stem, (JObject)content.DeepClone());

		Assert.Equal(first.FullName, second.FullName); // same path, idempotent success
		Assert.Equal(firstWriteTime, File.GetLastWriteTimeUtc(second.FullName)); // not rewritten
		Assert.Single(Directory.GetFiles((root / EventDirectory.Name).Path, "*.event"));

		var different = new EventFile(root, stem, new JObject { ["Role"] = "Loser" });
		Assert.NotEqual(first.FullName, different.FullName); // different content → different hash path
		Assert.Equal(2, Directory.GetFiles((root / EventDirectory.Name).Path, "*.event").Length);
	}

	[Fact]
	public void EventFile_HashPathCollisionWithDifferentBytes_PreservesExisting_WritesNonceSibling()
	{
		var root = NewCloudRoot("collision");
		var stem = "3000_TestMachine-app-1_Failed";
		var content = new JObject { ["Message"] = "original" };
		var (canonical, sha) = CanonicalJson.CanonicalizeWithHash(content);

		// Pre-create different bytes at the exact hash-derived path.
		var eventsDir = (root / EventDirectory.Name).mkdir();
		var collidingPath = new RaiFile(eventsDir, $"{stem}_{sha}", "event").FullName;
		File.WriteAllText(collidingPath, "{\"foreign\":true}", new UTF8Encoding(false));

		var eventFile = new EventFile(root, stem, content);

		// Existing artifact preserved; new event on a nonce-suffixed sibling path.
		Assert.Equal("{\"foreign\":true}", File.ReadAllText(collidingPath, new UTF8Encoding(false)));
		Assert.NotEqual(collidingPath, eventFile.FullName);
		Assert.True(eventFile.Exists());
		Assert.Equal(canonical, File.ReadAllText(eventFile.FullName, new UTF8Encoding(false)));
	}

	[Fact]
	public void EventFile_AcceptsDynamicObject_SerializingToOneTopLevelObject()
	{
		var root = NewCloudRoot("dynamic");
		var eventFile = new EventFile(root, "4000_TestMachine-app-1_Info", new { Stage = "Completed", Count = 3 });
		Assert.True(eventFile.Exists());
		var parsed = JObject.Parse(File.ReadAllText(eventFile.FullName));
		Assert.Equal("Completed", (string?)parsed["Stage"]);
		Assert.Equal(3, (int)parsed["Count"]!);
	}

	[Fact]
	public void EventDirectory_MissingEventsChild_ReturnsEmpty_WithoutCreatingIt()
	{
		var root = NewCloudRoot("missing-dir");
		var events = EventDirectory.Events(root);
		Assert.Empty(events);
		Assert.False(Directory.Exists((root / EventDirectory.Name).Path), "Reading must never create the Events child.");
	}

	[Fact]
	public void EventDirectory_FreshEnumerationEveryCall_ReturnsCompleteFilenameKeys_AndUnchangedObjects()
	{
		var root = NewCloudRoot("fresh");
		var first = new EventFile(root, "5000_MachineA-app-1_Completed", new JObject { ["N"] = 1 });

		var call1 = EventDirectory.Events(root);
		Assert.Single(call1);
		Assert.Equal(first.NameWithExtension, call1.Keys.Single());
		Assert.Equal(1, (int)call1.Values.Single()["N"]!);

		// A later call reflects newly materialized files without any refresh mechanism.
		var second = new EventFile(root, "5001_MachineB-app-2_Completed", new JObject { ["N"] = 2 });
		var call2 = EventDirectory.Events(root);
		Assert.Equal(2, call2.Count);
		Assert.Contains(second.NameWithExtension, call2.Keys);

		// Events from multiple machines coexist without overwrite (scenario 21).
		Assert.Contains(first.NameWithExtension, call2.Keys);

		// OsLib does not require or interpret JsonPit fields (schema-agnostic).
		var arbitrary = new EventFile(root, "5002_whatever_arbitrary", new JObject { ["AnythingAtAll"] = "yes" });
		var call3 = EventDirectory.Events(root);
		Assert.Equal("yes", (string?)call3[arbitrary.NameWithExtension]["AnythingAtAll"]);
	}

	[Fact]
	public void EventDirectory_OmitsIncompleteUnparseableOrHashInvalidFilesIndividually_AndReconsidersLater()
	{
		var root = NewCloudRoot("bad-events");
		var good = new EventFile(root, "6000_MachineA-app-1_Completed", new JObject { ["Ok"] = true });
		var eventsDir = root / EventDirectory.Name;

		// Hash-invalid: correct-looking name, wrong content bytes.
		var wrongHashName = $"6001_MachineA-app-1_Failed_{new string('a', 64)}";
		File.WriteAllText(new RaiFile(eventsDir, wrongHashName, "event").FullName, "{\"Ok\":false}", new UTF8Encoding(false));

		// Unparseable: hash matches the truncated bytes, but JSON is incomplete.
		var truncated = "{\"Ok\":fal";
		var truncatedSha = CanonicalJson.Sha256Hex(truncated);
		var truncatedFile = new RaiFile(eventsDir, $"6002_MachineA-app-1_Failed_{truncatedSha}", "event");
		File.WriteAllText(truncatedFile.FullName, truncated, new UTF8Encoding(false));

		var events = EventDirectory.Events(root);
		Assert.Single(events); // the two bad files are omitted individually, not the whole read
		Assert.Contains(good.NameWithExtension, events.Keys);

		// Once the file materializes correctly, the next fresh call includes it (scenario 23).
		var completed = "{\"Ok\":false}";
		var completedSha = CanonicalJson.Sha256Hex(completed);
		var completedFile = new RaiFile(eventsDir, $"6002_MachineA-app-1_Failed_{completedSha}", "event");
		File.WriteAllText(completedFile.FullName, completed, new UTF8Encoding(false));
		var later = EventDirectory.Events(root);
		Assert.Contains(completedFile.NameWithExtension, later.Keys);
	}
}
