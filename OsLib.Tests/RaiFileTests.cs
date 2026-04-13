using Xunit;
using OsLib;
namespace OsLib.Tests
{
	public class RaiFileTest
	{
		[Fact]
		public void RaiFile_NameAndExtension_PropertiesMutateCorrectly()
		{
			// Arrange: Start with a clean file object
			var file = new RaiFile("/tmp/workspace/dummy.txt");

			// Act & Assert 1: Implicit Split
			file.NameAndExt = ("archive.tar", "");
			Assert.Equal("archive.tar", file.Name);
			Assert.Equal("txt", file.Ext);
			Assert.Equal("archive.tar.txt", file.NameWithExtension);
			Assert.Equal(("archive.tar", "txt"), file.NameAndExt);

			// Act & Assert 2: Explicit Override (Forces the extension)
			file.NameAndExt = ("archive.tar", "zip");
			Assert.Equal("archive.tar", file.Name);
			Assert.Equal("zip", file.Ext);
			Assert.Equal("archive.tar.zip", file.NameWithExtension);
			Assert.Equal(("archive.tar", "zip"), file.NameAndExt);

			// Act & Assert 3: Setting 'Name' while 'Ext' exists
			// Because Ext is "zip", setting Name should NOT split the dot in "otw.software"
			file.Name = "otw.software";
			Assert.Equal("otw.software", file.Name);
			Assert.Equal("zip", file.Ext); // Preserved!
			Assert.Equal("otw.software.zip", file.NameWithExtension);
			Assert.Equal(("otw.software", "zip"), file.NameAndExt);

			// Act & Assert 4: Explicitly replacing both parts via Tuple
			file.NameAndExt = ("otw.software", "json");
			Assert.Equal("otw.software", file.Name);
			Assert.Equal("json", file.Ext);
			Assert.Equal("otw.software.json", file.NameWithExtension);
			Assert.Equal(("otw.software", "json"), file.NameAndExt);

			// Act & Assert 5: Stripping path during Name assignment
			// Simulates a user accidentally passing a full path into the Name property
			file.Name = "/sneaky/dir/config.test";
			Assert.Equal("config.test", file.Name);
			Assert.Equal("json", file.Ext);
			Assert.Equal("config.test.json", file.NameWithExtension);
		}
	}
}