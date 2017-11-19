using DokanNet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using static nlcfs.Program;
using FileAccess = DokanNet.FileAccess;

namespace nlcfs
{
  internal class RemoteDirectory : IDokanOperations
  {

    private const FileAccess DataAccess = FileAccess.ReadData | FileAccess.WriteData | FileAccess.AppendData |
                                          FileAccess.Execute |
                                          FileAccess.GenericExecute | FileAccess.GenericWrite |
                                          FileAccess.GenericRead;

    private const FileAccess DataWriteAccess = FileAccess.WriteData | FileAccess.AppendData |
                                               FileAccess.Delete |
                                               FileAccess.GenericWrite;


    public void Mount(string mountPath)
    {
      client.RegisterClientRoot(mountPath);
      this.Mount(mountPath, DokanOptions.DebugMode, 1);
    }

    private T Trace<T>(string name, string file, T result, ConsoleColor color)
    {
      Client.Log($"{name} - {file}: {result}", color);
      return result;
    }

    #region Implementation of IDokanOperations

    public NtStatus CreateFile(string fileName, FileAccess access, FileShare share, FileMode mode,
        FileOptions options, FileAttributes attributes, DokanFileInfo info)
    {
      NtStatus success;
      return Trace("CreateFile" + (info.IsDirectory ? " Dir" : " File"), fileName, success = client.CreateFile(fileName, mode, info.IsDirectory), success == NtStatus.Success ? ConsoleColor.Green : ConsoleColor.Yellow);      
    }

    public void Cleanup(string fileName, DokanFileInfo info)
    {
    }

    public void CloseFile(string fileName, DokanFileInfo info)
    {
    }

    public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, DokanFileInfo info)
    {
      bytesRead = 0;

      if (info.IsDirectory)
        return Trace("ReadFile", fileName, NtStatus.Error, ConsoleColor.Yellow);

      if (!client.Exists(fileName, false))
        return Trace("ReadFile", fileName, NtStatus.ObjectNameNotFound, ConsoleColor.Yellow);

      Client.Log($"Read {buffer.Length} bytes from {fileName}");

      var tBuf = client.Read(fileName, offset, buffer.Length);

      bytesRead = tBuf.Length;
      buffer = tBuf;

      return Trace("ReadFile", fileName, NtStatus.Success, ConsoleColor.Green);
    }

    public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, DokanFileInfo info)
    {
      bytesWritten = 0;

      if (info.IsDirectory)
        return Trace("ReadFile", fileName, NtStatus.Error, ConsoleColor.Yellow);

      if (!client.Exists(fileName, false))
        return Trace("ReadFile", fileName, NtStatus.ObjectNameNotFound, ConsoleColor.Yellow);

      Client.Log($"Write {buffer.Length} bytes to {fileName}");

      var size = client.Write(fileName, offset, buffer);

      if (size == -1)
        return Trace("ReadFile", fileName, NtStatus.Error, ConsoleColor.Yellow);

      bytesWritten = size;

      return Trace("ReadFile", fileName, NtStatus.Success, ConsoleColor.Green);
    }

    public NtStatus FlushFileBuffers(string fileName, DokanFileInfo info)
    {
      return Trace("FlushFileBuffers", fileName, NtStatus.Success, ConsoleColor.Green);
    }

    public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, DokanFileInfo info)
    {
      FileSystemInfo finfo = client.GetFileInfo(fileName);
      if (finfo == null)
        finfo = client.GetDirectoryInfo(fileName);

      fileInfo = new FileInformation
      {
        FileName = fileName,
        Attributes = finfo.Attributes,
        CreationTime = finfo.CreationTime,
        LastAccessTime = finfo.LastAccessTime,
        LastWriteTime = finfo.LastWriteTime,
        Length = /*(finfo as FileInfo)?.Length ??*/ 10,
      };
      return Trace("GetFileInformation", fileName, DokanResult.Success, ConsoleColor.Green);
    }

    public NtStatus FindFiles(string fileName, out IList<FileInformation> files, DokanFileInfo info)
    {
      files = client.FindFilesHelper(fileName, "*").Select(finfo => new FileInformation
      {
        FileName = finfo.Name,
        Attributes = finfo.Attributes,
        CreationTime = finfo.CreationTime,
        LastAccessTime = finfo.LastAccessTime,
        LastWriteTime = finfo.LastWriteTime,
        Length = (finfo as FileInfo)?.Length ?? 0,
      }).ToArray();

      return Trace("FindFiles", fileName, DokanResult.Success, ConsoleColor.Green);
    }

    public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, DokanFileInfo info)
    {
      files = client.FindFilesHelper(fileName, searchPattern).Select(finfo => new FileInformation
      {
        FileName = finfo.Name,
        Attributes = finfo.Attributes,
        CreationTime = finfo.CreationTime,
        LastAccessTime = finfo.LastAccessTime,
        LastWriteTime = finfo.LastWriteTime,
        Length = (finfo as FileInfo)?.Length ?? 10,
      }).ToArray();

      return Trace("FindFilesWithPattern", fileName, DokanResult.Success, ConsoleColor.Green);
    }

    public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, DokanFileInfo info)
    {
      NtStatus success;
      return Trace("SetFileAttributes", fileName, success = client.SetPathAttributes(fileName, attributes), success == NtStatus.Success ? ConsoleColor.Green : ConsoleColor.Yellow);
    }

    public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime,
        DateTime? lastWriteTime, DokanFileInfo info)
    {
      NtStatus success;
      return Trace("SetFileTime", fileName, success = client.SetPathTime(fileName, creationTime, lastAccessTime, lastWriteTime), success == NtStatus.Success ? ConsoleColor.Green : ConsoleColor.Yellow);
    }

    public NtStatus DeleteFile(string fileName, DokanFileInfo info)
    {
      NtStatus success;
      return Trace("DeleteFile", fileName, success = client.DeleteFile(fileName), success == NtStatus.Success ? ConsoleColor.Green : ConsoleColor.Yellow);
    }

    public NtStatus DeleteDirectory(string fileName, DokanFileInfo info)
    {
      NtStatus success;
      return Trace("DeleteDirectory", fileName, success = client.DeleteDirectory(fileName), success == NtStatus.Success ? ConsoleColor.Green : ConsoleColor.Yellow);
    }

    public NtStatus MoveFile(string oldName, string newName, bool replace, DokanFileInfo info)
    {
      NtStatus success;
      return Trace("MoveFile", oldName, success = client.MoveFile(oldName, newName, replace, info.IsDirectory), success == NtStatus.Success ? ConsoleColor.Green : ConsoleColor.Yellow);
    }

    public NtStatus SetEndOfFile(string fileName, long length, DokanFileInfo info)
    {
      return NtStatus.Success;
    }

    public NtStatus SetAllocationSize(string fileName, long length, DokanFileInfo info)
    {
      return NtStatus.Success;
    }

    public NtStatus LockFile(string fileName, long offset, long length, DokanFileInfo info)
    {
      return NtStatus.Success;
    }

    public NtStatus UnlockFile(string fileName, long offset, long length, DokanFileInfo info)
    {
      return NtStatus.Success;
    }

    public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, DokanFileInfo info)
    {
      freeBytesAvailable = 1024L * 1024 * 1024 * 10;
      totalNumberOfBytes = 1024L * 1024 * 1024 * 20;
      totalNumberOfFreeBytes = 1024L * 1024 * 1024 * 10;
      return Trace("GetDiskFreeSpace", string.Empty, NtStatus.Success, ConsoleColor.Green);
    }

    public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features,
        out string fileSystemName, DokanFileInfo info)
    {
      volumeLabel = "nlcfs";
      fileSystemName = "NTFS";

      features = FileSystemFeatures.CasePreservedNames | FileSystemFeatures.CaseSensitiveSearch |
                 FileSystemFeatures.PersistentAcls | FileSystemFeatures.SupportsRemoteStorage |
                 FileSystemFeatures.UnicodeOnDisk;

      return Trace("GetVolumeInformation", volumeLabel, NtStatus.Success, ConsoleColor.Green);
    }

    public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, DokanFileInfo info)
    {
      security = null;
      return NtStatus.Error;
    }

    public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections,
        DokanFileInfo info)
    {
      return NtStatus.Error;
    }

    public NtStatus Mounted(DokanFileInfo info)
    {
      return NtStatus.Success;
    }

    public NtStatus Unmounted(DokanFileInfo info)
    {
      return NtStatus.Success;
    }

    public NtStatus FindStreams(string fileName, IntPtr enumContext, out string streamName, out long streamSize,
        DokanFileInfo info)
    {
      streamName = string.Empty;
      streamSize = 0;
      return NtStatus.NotImplemented;
    }

    public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, DokanFileInfo info)
    {
      streams = new List<FileInformation>();
      return NtStatus.NotImplemented;
    }

    #endregion Implementation of IDokanOperations
  }
}