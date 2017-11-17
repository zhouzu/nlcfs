using DokanNet;
using System;
using System.IO;
using System.Linq;

namespace nlcfs___Server
{
  public class SharedClass : IDisposable
  {
    private string strClientRoot = "C:\\Test\\";

    [NLCCall("FindFilesHelper")]
    public FileInfo[] FindFilesHelper(string fileName, string searchPattern)
    {
      var files = new DirectoryInfo(strClientRoot + fileName)
          .EnumerateFileSystemInfos()
          .Where(finfo => DokanHelper.DokanIsNameInExpression(searchPattern, finfo.Name, true))
          .Select(finfo => new FileInfo(finfo.FullName))
          .ToArray();

      return files;
    }

    [NLCCall("GetFileInfo")]
    public FileInfo GetFileInfo(string filename)
    {
      try
      {
        Console.WriteLine(strClientRoot + filename);
        return new FileInfo(strClientRoot + filename);
      }
      catch { return null; }
    }

    [NLCCall("GetDirectoryInfo")]
    public DirectoryInfo GetDirectoryInfo(string directoryname)
    {
      try
      {
        return new DirectoryInfo(strClientRoot + directoryname);
      }
      catch { return null; }
    }

    [NLCCall("Exists")]
    public bool Exists(string path, bool isDirectory)
    {
      return isDirectory ? Directory.Exists(strClientRoot + path) : File.Exists(strClientRoot + path);
    }

    [NLCCall("GetAttributes")]
    public FileAttributes GetAttributes(string path)
    {
      try
      {
        return File.GetAttributes(strClientRoot + path);
      }
      catch { return FileAttributes.Offline; }
    }

    [NLCCall("Create")]
    public bool Create(string path, bool isDirectory)
    {
      try
      {
        if (isDirectory)
          Directory.CreateDirectory(strClientRoot + path);
        else
          File.Create(strClientRoot + path).Close();

        return true;
      }
      catch { return false; }
    }

    [NLCCall("Read")]
    public byte[] Read(string path, long offset, int length)
    {
      var vArray = new byte[length];

      try
      {
        using (var stream = new FileStream(strClientRoot + path, FileMode.Open))
        {
          stream.Position = offset;
          var size = stream.Read(vArray, 0, length);
          Array.Resize(ref vArray, size);
        }
        return vArray;
      }
      catch { return null; }
    }

    [NLCCall("Write")]
    public int Write(string path, long offset, byte[] buffer)
    {
      try
      {
        using (var stream = new FileStream(strClientRoot + path, FileMode.Open))
        {
          stream.Position = offset;
          stream.Write(buffer, 0, buffer.Length);
          return buffer.Length;
        }
      }
      catch { return -1; }
    }

    [NLCCall("SetPathAttributes")]
    public NtStatus SetPathAttributes(string path, FileAttributes attributes)
    {
      try
      {
        File.SetAttributes(strClientRoot + path, attributes);
        return NtStatus.Success;
      }
      catch (UnauthorizedAccessException)
      {
        return NtStatus.AccessDenied;
      }
      catch (FileNotFoundException)
      {
        return NtStatus.ObjectNameNotFound;
      }
      catch (DirectoryNotFoundException)
      {
        return NtStatus.ObjectPathNotFound;
      }
    }

    [NLCCall("SetPathTime")]
    public NtStatus SetPathTime(string path, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime)
    {
      try
      {
        if (creationTime.HasValue)
          File.SetCreationTime(strClientRoot + path, creationTime.Value);

        if (lastAccessTime.HasValue)
          File.SetLastAccessTime(strClientRoot + path, lastAccessTime.Value);

        if (lastWriteTime.HasValue)
          File.SetLastWriteTime(strClientRoot + path, lastWriteTime.Value);

        return NtStatus.Success;
      }
      catch (UnauthorizedAccessException)
      {
        return NtStatus.AccessDenied;
      }
      catch (FileNotFoundException)
      {
        return NtStatus.ObjectNameNotFound;
      }
    }

    [NLCCall("DeleteFile")]
    public NtStatus DeleteFile(string path)
    {
      if (Directory.Exists(strClientRoot + path))
        return NtStatus.AccessDenied;

      if (!File.Exists(strClientRoot + path))
        return NtStatus.ObjectNameNotFound;

      if (File.GetAttributes(strClientRoot + path).HasFlag(FileAttributes.Directory))
        return NtStatus.AccessDenied;

      try
      {
        File.Delete(path);
        return NtStatus.Success;
      }
      catch { return NtStatus.Error; }
    }

    [NLCCall("DeleteDirectory")]
    public NtStatus DeleteDirectory(string path)
    {
      if (!Directory.Exists(strClientRoot + path))
        return NtStatus.ObjectPathNotFound;

      if (File.Exists(strClientRoot + path))
        return NtStatus.AccessDenied;

      try
      {
        Directory.Delete(path);
        return NtStatus.Success;
      }
      catch { return NtStatus.Error; }
    }

    [NLCCall("MoveFile")]
    public NtStatus MoveFile(string oldName, string newName, bool replace, bool isDirectory)
    {
      var exist = isDirectory ? Directory.Exists(strClientRoot + newName) : File.Exists(strClientRoot + newName);

      try
      {
        if (!exist)
        {
          if (isDirectory)
            Directory.Move(strClientRoot + oldName, strClientRoot + newName);
          else
            File.Move(strClientRoot + oldName, strClientRoot + newName);
          return NtStatus.Success;
        }
        else if (replace)
        {
          if (isDirectory) //Cannot replace directory destination - See MOVEFILE_REPLACE_EXISTING
            return NtStatus.AccessDenied;

          File.Delete(strClientRoot + newName);
          File.Move(strClientRoot + oldName, strClientRoot + newName);
          return NtStatus.Success;
        }
      }
      catch { return NtStatus.Error; }

      return NtStatus.ObjectNameExists;
    }

    public void Dispose()
    {
    }
  }
}