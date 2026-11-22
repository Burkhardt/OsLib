using System.Collections.Generic;
using System.IO;

namespace OsLib;

/// <summary>
/// Exact host paths for build tools that inspect sources before any RAIkeep configuration exists.
/// Operations preserve literal names and do not run the configured temporary-directory probe.
/// </summary>
public static class LocalFileSystem
{
	public static string AbsoluteName(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
	public static string ParentName(string path) => Path.GetDirectoryName(path);
	public static string LeafName(string path) => Path.GetFileName(path);
	public static string Combine(params string[] segments) => Path.Combine(segments);
	public static string Extension(string path) => Path.GetExtension(path);
	public static char SearchPathSeparator => Path.PathSeparator;
	public static string[] Segments(string path) => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	public static bool FileExists(string path) => File.Exists(path);
	public static FileAttributes Attributes(string path) => File.GetAttributes(path);
	public static IEnumerable<string> Entries(string directory) => Directory.EnumerateFileSystemEntries(directory);
	public static IEnumerable<string> Files(string directory, string pattern) => Directory.EnumerateFiles(directory, pattern);
	public static Stream OpenSharedRead(string path) => File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
	public static string ReadText(string path) => File.ReadAllText(path);
	public static void WriteText(string path, string content) => File.WriteAllText(path, content);
}
