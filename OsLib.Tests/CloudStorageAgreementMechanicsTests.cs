using System;
using System.IO;
namespace OsLib.Tests;
[Collection("CloudStorageEnvironment")]
public class CloudStorageAgreementMechanicsTests
{
	[Theory]
	[InlineData(Cloud.Dropbox, "DropboxRoot")]
	[InlineData(Cloud.OneDrive, "OneDriveRoot")]
	[InlineData(Cloud.GoogleDrive, "GoogleDriveRoot")]
	public void Backup_StripsConfiguredCloudRoot_FromBackupTargetPath(Cloud provider, string rootName)
	{
		var root = OsTestEnvironment.NewTestRoot("cloud-agreement");
		using var env = new OsTestEnvironment(root);
		var localBackup = root / "local-backup";
		var cloudRoot = root / rootName;
		var projectDir = cloudRoot / "Work" / "Reports";
		switch (provider)
		{
			case Cloud.Dropbox:
				env.WriteConfig(localBackupDir: localBackup.ToString(), dropbox: cloudRoot.ToString());
				break;
			case Cloud.OneDrive:
				env.WriteConfig(localBackupDir: localBackup.ToString(), oneDrive: cloudRoot.ToString());
				break;
			case Cloud.GoogleDrive:
				env.WriteConfig(localBackupDir: localBackup.ToString(), googleDrive: cloudRoot.ToString());
				break;
		}
		Directory.CreateDirectory(localBackup.Path);
		Directory.CreateDirectory(projectDir.Path);
		var source = new TextFile(projectDir, "report.txt", content: "provider backup").Save();
		Assert.Equal($"Work{Os.DIR}Reports{Os.DIR}", RaiFile.BackupRelativePath(source.Path).Path);
		var backup = source.backup(copy: true);
		Assert.Equal((localBackup / "Work" / "Reports").ToString(), backup.Path.ToString());
		Assert.True(File.Exists(backup.FullName));
		Assert.True(File.Exists(source.FullName));
	}
	[Fact]
	public void Backup_MirrorsLocalDirectoryShape_ForNonCloudFiles()
	{
		var root = OsTestEnvironment.NewTestRoot("backup", testName: "local-shape");
		using var env = new OsTestEnvironment(root);
		var localBackup = root / "bak";
		var sourceDir = root / "src" / "logs";
		env.WriteConfig(localBackupDir: localBackup.Path);
		Directory.CreateDirectory(localBackup.Path);
		Directory.CreateDirectory(sourceDir.Path);
		var source = new TextFile(sourceDir, "app.log", content: "local backup").Save();

		Assert.Equal(sourceDir.Path, new RaiPath(source.FullName).Path);
		Assert.Equal(GetMirroredAbsoluteDirectoryTail(sourceDir), RaiFile.BackupRelativePath(source.Path).Path);

		var backup = source.backup(copy: true);
		Assert.Equal(MirrorAbsoluteDirectoryUnder(Os.LocalBackupDir, sourceDir), backup.Path.ToString());
		Assert.True(File.Exists(backup.FullName));
		Assert.True(File.Exists(source.FullName));
	}
	[Fact]
	public void RaiPath_SlashString_AppendsRelativeDirectorySegments()
	{
		var root = OsTestEnvironment.NewTestRoot("backup", testName: "slash-string");
		var appended = root / "src" / "logs";

		Assert.StartsWith(root.Path, appended.Path);
		Assert.EndsWith($"src{Os.DIR}logs{Os.DIR}", appended.Path);
	}

	private static string MirrorAbsoluteDirectoryUnder(RaiPath root, RaiPath absoluteDirectory)
	{
		return new RaiPath(root.Path + GetMirroredAbsoluteDirectoryTail(absoluteDirectory)).Path;
	}

	private static string GetMirroredAbsoluteDirectoryTail(RaiPath absoluteDirectory)
	{
		return absoluteDirectory.Path.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	}
}
