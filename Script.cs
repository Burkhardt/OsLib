using System;
using System.IO;

namespace OsLib
{
	public sealed class Script : RaiSystem
	{
		private const UnixFileMode ExecutableMode =
			UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
			UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
			UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

		public TextFile File { get; }

		public string FullName => File.FullName;

		public Script(RaiPath path, string name, string content)
			: base(new RaiFile(path, name).FullName)
		{
			File = new TextFile(path, name, content);
			EnsureExecutable();
		}

		public Script Append(string line)
		{
			File.Append(line);
			return this;
		}

		public Script Save(bool backup = false)
		{
			File.Save(backup);
			EnsureExecutable();
			return this;
		}

		public Script EnsureExecutable()
		{
			if (!OperatingSystem.IsWindows() && System.IO.File.Exists(FullName))
				System.IO.File.SetUnixFileMode(FullName, ExecutableMode);
			return this;
		}
	}
}