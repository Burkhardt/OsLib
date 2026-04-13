using System;
using System.IO;
using OsLib;

namespace OsLib.Tests;

public class RaiTildeResolutionTests
{
	// works for unix: HOME - used to be able to test without writing the actual user here, e.g. /Users/RSB/
	static string userHomeDir = Environment.GetEnvironmentVariable("HOME") + Os.DIR;    // convention here: directories are terminated by a /
	[Fact]
	public void RaiFile_And_RaiPath_Parse_ConfigPath_From_RealHomeDirectory()
	{
		var expectedPath = $"{userHomeDir}.config{Path.DirectorySeparatorChar}"; // i.e. /Users/RSB/.config/

		var file = new RaiFile("~/.config/RAIkeep.json5");
		Assert.Equal(expectedPath, file.Path.ToString());
		Assert.Equal("RAIkeep", file.Name);
		Assert.Equal("json5", file.Ext);

		(var path, _) = RaiPath.SplitRaiPathAndName("~/.config/RAIkeep.json5");
		Assert.Equal(expectedPath, path.FullPath);
	}
	[Fact]
	public void RaiFile_And_RaiPath_Parse()
	{
		var expected = $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}samples{Path.DirectorySeparatorChar}otw.software.json"; // i.e. /Users/RSB/test/

		(var path, _) = RaiPath.SplitRaiPathAndName("samples/otw.software.json");
		var file1 = new RaiFile(path, "otw.software.json");
		var file2 = new RaiFile("samples/otw.software.json");
		var file3 = new RaiFile(path, "otw.software", "json");
		Assert.StartsWith(Os.DIR, path.FullPath);
		Assert.EndsWith($"samples{Os.DIR}", path.FullPath);
		Assert.Equal("otw.software", file1.Name);
		Assert.Equal("otw.software", file2.Name);
		Assert.Equal("otw.software", file3.Name);
		Assert.Equal("json", file1.Ext);
		Assert.Equal("json", file2.Ext);
		Assert.Equal("json", file3.Ext);
		Assert.Equal(expected, file1.FullName);
		Assert.Equal(expected, file2.FullName);
		Assert.Equal(expected, file3.FullName);
	}
	[Fact]
	public void RaiFile_And_RaiPath_Expand_Directory_Shorthand_Consistently()
	{
		var parent = string.Join(Os.DIR, Directory.GetCurrentDirectory().Split(Path.DirectorySeparatorChar)[..^1]) + Os.DIR; // cuts /Users/RSB to /Users/
		var current = Directory.GetCurrentDirectory() + Os.DIR;
		var home = userHomeDir;
		var osconfig = $"{home}.config{Path.DirectorySeparatorChar}RAIkeep{Path.DirectorySeparatorChar}osconfig.json5";
		var osconfigDir = $"{home}.config{Path.DirectorySeparatorChar}RAIkeep{Path.DirectorySeparatorChar}";
		var otwSoftwareConfig = $"{current}samples{Path.DirectorySeparatorChar}otw.software.json";
		var otwSoftwareConfigDir = $"{current}samples{Path.DirectorySeparatorChar}";

		var path1 = new RaiPath(".").ToString();		//-
		var path2 = new RaiPath("./").ToString();       //+
		var path3 = new RaiPath("~").ToString();   // +
		var path4 = new RaiPath("~/").ToString();  // +
		var path5 = new RaiPath("../").ToString();   // +
		var file1 = new RaiFile("~/");   // +
		var file2 = new RaiFile("../");   //  +
		var file3 = new RaiFile("~/.config/RAIkeep/osconfig.json5");   //
		var file4 = new RaiFile("./samples/otw.software.json");   //

		Assert.Equal(current, path1);
		Assert.Equal(current, path2);
		Assert.Equal(home, path3);
		Assert.Equal(home, path4);
		Assert.Equal(parent, path5);
		Assert.Equal(home, file1.Path.ToString());
		Assert.Equal(parent, file2.Path.ToString());
		Assert.Equal(osconfigDir, file3.Path.ToString());
		Assert.Equal(osconfig, file3.FullName);
		Assert.Equal(otwSoftwareConfigDir, file4.Path.ToString());
		Assert.Equal(otwSoftwareConfig, file4.FullName);
	}
	[Fact]
	public void RaiPath_AlwaysTakeParameterAsPath_Split_FullName_First()
	{
		var expectedPath = $"{Directory.GetCurrentDirectory()}{Os.DIR}otw.software{Os.DIR}"; // i.e. /Users/RSB/test/samples/
		var expectedFile = $"{expectedPath}sample.json"; // i.e. /Users/RSB/test/samples/otw.software/sample.json
		var expectedButNotIntendedPath = $"{expectedPath}sample.json{Os.DIR}";
		var expectedButNotIntendedFile = $"{expectedButNotIntendedPath}sample.json";
		var path1 = new RaiPath("otw.software/sample.json");	// everything after / is now also considered part of the path, not part of the filename
		var path2 = new RaiPath("otw.software/sample.json/");   // everything after / is now also considered part of the path, not part of the filename
		Assert.Equal(expectedButNotIntendedPath, path1.FullPath);
		Assert.Equal(expectedButNotIntendedPath, path2.FullPath);
		var (path3, nameWithExt) = RaiPath.SplitRaiPathAndName("otw.software/sample.json");
		var file1 = new RaiFile(path3, nameWithExt);
		Assert.Equal(expectedFile, file1.FullName);
		var file2 = new RaiFile(path2, "sample.json");
		Assert.Equal(expectedButNotIntendedFile, file2.FullName);
	}
}