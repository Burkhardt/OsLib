using System;
using System.IO;
using Xunit;

namespace OsLib.Tests;

public sealed class TextFileMaterializationTests
{
	// Model a mount on which the write succeeds but subsequent lookups cannot see it.
	private sealed class HiddenAfterWrite(RaiPath directory) : TextFile(directory, "visible", "txt")
	{
		public override string FullName => new RaiFile(base.FullName).Exists() ? base.FullName + ".missing" : base.FullName;
	}

	[Fact]
	public void TimeoutIsObservableAndLeavesTheEditDirty()
	{
		var directory = Os.TempDir / Guid.NewGuid().ToString("N");
		try
		{
			var file = new HiddenAfterWrite(directory) { Lines = ["payload"], Changed = true };
			var error = Assert.Throws<FileNotFoundException>(() => file.Save());
			Assert.Equal(file.FullName, error.FileName);
			Assert.Contains("materialize", error.Message);
			Assert.True(file.Changed);
		}
		finally { directory.rmdir(depth: 2, deleteFiles: true); }
	}
}
