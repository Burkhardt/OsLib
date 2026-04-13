using System;
using System.IO;
namespace OsLib.Tests;
[Collection("CloudStorageEnvironment")]
public class CloudStorageAgreementMechanicsTests
{
	[Fact]
	public void RaiPath_SlashString_AppendsRelativeDirectorySegments()
	{
		var root = OsTestEnvironment.NewTestRoot("backup", testName: "slash-string");
		var appended = root / "src" / "logs";

		Assert.StartsWith(root.Path, appended.Path);
		Assert.EndsWith($"src{Os.DIR}logs{Os.DIR}", appended.Path);
	}

}
