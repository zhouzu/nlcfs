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
  internal class Mirror : IDokanOperations
  {
    private readonly string strMountPath;

    private const FileAccess DataAccess = FileAccess.ReadData | FileAccess.WriteData | FileAccess.AppendData |
                                          FileAccess.Execute |
                                          FileAccess.GenericExecute | FileAccess.GenericWrite |
                                          FileAccess.GenericRead;

    private const FileAccess DataWriteAccess = FileAccess.WriteData | FileAccess.AppendData |
                                               FileAccess.Delete |
                                               FileAccess.GenericWrite;

    public Mirror(string path)
    {
      if (!Directory.Exists(path))
        throw new ArgumentException(nameof(path));
      strMountPath = path;
    }

    private string GetRPath(string fileName)
    {
      return fileName.Substring(strMountPath.Length -1);
    }

    #region Implementation of IDokanOperations

    public NtStatus CreateFile(string fileName, FileAccess access, FileShare share, FileMode mode,
        FileOptions options, FileAttributes attributes, DokanFileInfo info)
    {
      if (info.IsDirectory)
      {
        switch (mode)
        {
          case FileMode.Open:
            Client.Log($"OpenDirectory {fileName}");
            try
            {
              if (client.GetAttributes(GetRPath(fileName)).HasFlag(FileAttributes.Directory))
              {
                return NtStatus.Success;
              }
              else
              {
                return NtStatus.ObjectPathNotFound;
              }
            }
            catch
            {
              return NtStatus.ObjectPathNotFound;
            }

          case FileMode.CreateNew:
            Client.Log($"CreateDirectory {fileName}");
            try
            {
              if (!client.Exists(GetRPath(fileName), true))
              {
                return client.Create(GetRPath(fileName), true) ? NtStatus.Success : NtStatus.Error;
              }
              else
              {
                return NtStatus.ObjectNameCollision;
              }
            }
            catch (Exception e)
            {
              return NtStatus.Error;
            }
          default:
            Client.Log($"Error FileMode invalid for directory {mode}", ConsoleColor.Yellow);
            return NtStatus.Error;
        }
      }
      else
      {
        Client.Log($"CreateFile {fileName}");
        try
        {
          switch (mode)
          {
            case FileMode.Open:
              {
                Client.Log("Open");
                if (client.Exists(GetRPath(fileName), false))
                  return NtStatus.Success;
                else
                  return NtStatus.ObjectNameNotFound;
              }
            case FileMode.CreateNew:
              {
                Client.Log("CreateNew");
                if (client.Exists(GetRPath(fileName), false))
                  return NtStatus.ObjectNameCollision;

                return client.Create(GetRPath(fileName), false) ? NtStatus.Success : NtStatus.Error;
              }
            case FileMode.Create:
              {
                Client.Log("Create");

                return client.Create(GetRPath(fileName), false) ? NtStatus.Success : NtStatus.Error;
              }
            case FileMode.OpenOrCreate:
              {
                Client.Log("OpenOrCreate");

                if (!client.Exists(GetRPath(fileName), false))
                  return client.Create(GetRPath(fileName), false) ? NtStatus.Success : NtStatus.Error;
                else
                  return NtStatus.Success;
              }
            case FileMode.Truncate:
              {
                Client.Log("Truncate");

                if (!client.Exists(GetRPath(fileName), false))
                  return NtStatus.ObjectNameNotFound;

                return client.Create(GetRPath(fileName), false) ? NtStatus.Success : NtStatus.Error;
              }
            case FileMode.Append:
              {
                Client.Log("Append");

                if (client.Exists(GetRPath(fileName), false))
                  return NtStatus.Success;

                return client.Create(GetRPath(fileName), false) ? NtStatus.Success : NtStatus.Error;
              }
            default:
              Client.Log($"Error unknown FileMode {mode}");
              return NtStatus.Error;
          }
        }
        catch (Exception e)
        {
          return NtStatus.ObjectNameNotFound;
        }
      }
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
        return NtStatus.Error;

      if (!client.Exists(GetRPath(fileName), false))
        return NtStatus.ObjectNameNotFound;

      Client.Log($"Read {buffer.Length} bytes from {fileName}");

      var tBuf = client.Read(GetRPath(fileName), offset, buffer.Length);

      bytesRead = tBuf.Length;
      buffer = tBuf;

      return NtStatus.Success;
    }

    public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, DokanFileInfo info)
    {
      bytesWritten = 0;

      if (info.IsDirectory)
        return NtStatus.Error;

      if (!client.Exists(GetRPath(fileName), false))
        return NtStatus.ObjectNameNotFound;

      Client.Log($"Write {buffer.Length} bytes to {fileName}");

      var size = client.Write(GetRPath(fileName), offset, buffer);

      if (size == -1)
        return NtStatus.Error;

      bytesWritten = size;

      return NtStatus.Success;
    }

    public NtStatus FlushFileBuffers(string fileName, DokanFileInfo info)
    {
      return NtStatus.Success;
    }

    public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, DokanFileInfo info)
    {
      FileSystemInfo finfo = client.GetFileInfo(GetRPath(fileName));
      if (finfo == null)
        finfo = client.GetDirectoryInfo(GetRPath(fileName));

      fileInfo = new FileInformation
      {
        FileName = fileName,
        Attributes = finfo.Attributes,
        CreationTime = finfo.CreationTime,
        LastAccessTime = finfo.LastAccessTime,
        LastWriteTime = finfo.LastWriteTime,
        Length = /*(finfo as FileInfo)?.Length ??*/ 10,
      };
      return DokanResult.Success;
    }

    public NtStatus FindFiles(string fileName, out IList<FileInformation> files, DokanFileInfo info)
    {
      files = client.FindFilesHelper(GetRPath(fileName), "*").Select(finfo => new FileInformation
      {
        FileName = finfo.Name,
        Attributes = finfo.Attributes,
        CreationTime = finfo.CreationTime,
        LastAccessTime = finfo.LastAccessTime,
        LastWriteTime = finfo.LastWriteTime,
        Length = (finfo as FileInfo)?.Length ?? 0,
      }).ToArray();

      return DokanResult.Success;
    }

    public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, DokanFileInfo info)
    {
      files = client.FindFilesHelper(GetRPath(fileName), searchPattern).Select(finfo => new FileInformation
      {
        FileName = finfo.Name,
        Attributes = finfo.Attributes,
        CreationTime = finfo.CreationTime,
        LastAccessTime = finfo.LastAccessTime,
        LastWriteTime = finfo.LastWriteTime,
        Length = (finfo as FileInfo)?.Length ?? 10,
      }).ToArray();

      return DokanResult.Success;
    }

    public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, DokanFileInfo info)
    {
      return client.SetPathAttributes(GetRPath(fileName), attributes);
    }

    public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime,
        DateTime? lastWriteTime, DokanFileInfo info)
    {
      return client.SetPathTime(GetRPath(fileName), creationTime, lastAccessTime, lastWriteTime);
    }

    public NtStatus DeleteFile(string fileName, DokanFileInfo info)
    {
      return client.DeleteFile(GetRPath(fileName));
    }

    public NtStatus DeleteDirectory(string fileName, DokanFileInfo info)
    {
      return client.DeleteDirectory(GetRPath(fileName));
    }

    public NtStatus MoveFile(string oldName, string newName, bool replace, DokanFileInfo info)
    {
      return client.MoveFile(oldName, newName, replace, info.IsDirectory);
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
      return NtStatus.Success;
    }

    public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features,
        out string fileSystemName, DokanFileInfo info)
    {
      volumeLabel = "nlcfs";
      fileSystemName = "NTFS";

      features = FileSystemFeatures.CasePreservedNames | FileSystemFeatures.CaseSensitiveSearch |
                 FileSystemFeatures.PersistentAcls | FileSystemFeatures.SupportsRemoteStorage |
                 FileSystemFeatures.UnicodeOnDisk;

      return NtStatus.Success;
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