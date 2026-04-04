using System;
using System.IO;
using OsLib;

namespace OsLib.Tests;

public class RaiTildeResolutionTests
{
	// works for unix: HOME - used to be able to test without writing the actual user here, e.g. /Users/RSB/
	static string userHomeDir = Environment.GetEnvironmentVariable("HOME") + Os.DIR;	// convention here: directories are terminated by a /
	[Fact]
	public void RaiFile_And_RaiPath_Parse_ConfigPath_From_RealHomeDirectory()
	{
		var expectedPath = $"{userHomeDir}.config{Path.DirectorySeparatorChar}RAIkeep{Path.DirectorySeparatorChar}"; // i.e. /Users/RSB/.config/RAIkeep/

		var file = new RaiFile("~/.config/RAIkeep/osconfig.json5");
		Assert.Equal(expectedPath, file.Path.ToString());
		Assert.Equal("osconfig", file.Name);
		Assert.Equal("json5", file.Ext);

		var path = new RaiPath("~/.config/RAIkeep/osconfig.json5");
		Assert.Equal(expectedPath, path.Path.ToString());
	}

	[Fact]
	public void RaiFile_And_RaiPath_Expand_Directory_Shorthand_Consistently()
	{
		var parent = new RaiPath(Directory.GetCurrentDirectory()).Path; // cuts /Users/RSB to /Users/
		var current = Directory.GetCurrentDirectory() + Os.DIR;
		var home = userHomeDir;

		Assert.Equal(current, new RaiPath(".").Path.ToString());
		Assert.Equal(current, new RaiFile(".").Path.ToString());
		Assert.Equal(current, new RaiPath("./").Path.ToString());
		Assert.Equal(current, new RaiFile("./").Path.ToString());
		Assert.Equal(home, new RaiPath("~").Path.ToString());
		Assert.Equal(home, new RaiFile("~").Path.ToString());
		Assert.Equal(home, new RaiPath("~/").Path.ToString());
		Assert.Equal(home, new RaiFile("~/").Path.ToString());
		Assert.Equal(parent, new RaiPath("../").Path.ToString());
		Assert.Equal(parent, new RaiFile("../").Path.ToString());
	}
}