using DokanNet;
using System;
using System.IO;
using System.Linq;

namespace nlcfs___Server
{
  public class SharedClass : IDisposable
  {
    public static string strEmulatedRoot = string.Empty;
    private string strClientRoot = string.Empty;

    public string TranslatePath(string clientPath)
    {
      return strEmulatedRoot + clientPath;
    }

    [NLCCall("RegisterClientRoot")]
    public void RegisterClientRoot(string root)
    {
      strClientRoot = root;
    }

    [NLCCall("FindFilesHelper")]
    public FileInfo[] FindFilesHelper(string fileName, string searchPattern)
    {
      var files = new DirectoryInfo(TranslatePath(fileName))
          .EnumerateFileSystemInfos()
          .Where(finfo => DokanHelper.DokanIsNameInExpression(searchPattern, finfo.Name, true))
          .Select(finfo => new FileInfo(finfo.FullName))
          .ToArray();

      return files;
    }

    [NLCCall("CreateFile")]
    public NtStatus CreateFile(string path, FileMode mode, bool isDirectory)
    {
      if (GetAttributes(path).HasFlag(FileAttributes.Directory))
      {
        switch (mode)
        {
          case FileMode.Open:
            try
            {
              if (GetAttributes(path).HasFlag(FileAttributes.Directory))
                return NtStatus.Success;
              else
                return NtStatus.ObjectPathNotFound;
            }
            catch
            {
              return NtStatus.ObjectPathNotFound;
            }

          case FileMode.CreateNew:
            try
            {
              if (!Exists(path, true))
                return Create(path, true) ? NtStatus.Success : NtStatus.Error;
              else
                return NtStatus.ObjectNameCollision;
            }
            catch
            {
              return NtStatus.Error;
            }
          default:
            return NtStatus.Error;
        }
      }
      else
      {
        try
        {
          switch (mode)
          {
            case FileMode.Open:
              {
                if (Exists(path, false))
                  return NtStatus.Success;
                else
                  return NtStatus.ObjectNameNotFound;
              }
            case FileMode.CreateNew:
              {
                if (Exists(path, false))
                  return NtStatus.ObjectNameCollision;

                return Create(path, false)? NtStatus.Success : NtStatus.Error;
              }
            case FileMode.Create:
              {
                return Create(path, false) ? NtStatus.Success : NtStatus.Error;
              }
            case FileMode.OpenOrCreate:
              {
                if (!Exists(path, false))
                  return Create(path, false) ? NtStatus.Success : NtStatus.Error;
                else
                  return NtStatus.Success;
              }
            case FileMode.Truncate:
              {
                if (!Exists(path, false))
                  return NtStatus.ObjectNameNotFound;

                return Create(path, false) ? NtStatus.Success : NtStatus.Error;
              }
            case FileMode.Append:
              {
                if (Exists(path, false))
                  return NtStatus.Success;

                return Create(path, false) ? NtStatus.Success : NtStatus.Error;
              }
            default:
              return NtStatus.Error;
          }
        }
        catch
        {
          return NtStatus.Error;
        }
      }
    }

    [NLCCall("GetFileInfo")]
    public FileInfo GetFileInfo(string filename)
    {
      try
      {
        return new FileInfo(TranslatePath(filename));
      }
      catch { return null; }
    }

    [NLCCall("GetDirectoryInfo")]
    public DirectoryInfo GetDirectoryInfo(string directoryname)
    {
      try
      {
        return new DirectoryInfo(TranslatePath(directoryname));
      }
      catch { return null; }
    }

    [NLCCall("Exists")]
    public bool Exists(string path, bool isDirectory)
    {
      return isDirectory ? Directory.Exists(TranslatePath(path)) : File.Exists(TranslatePath(path));
    }

    [NLCCall("GetAttributes")]
    public FileAttributes GetAttributes(string path)
    {
      try
      {
        return File.GetAttributes(TranslatePath(path));
      }
      catch { return FileAttributes.Offline; }
    }

    [NLCCall("Create")]
    public bool Create(string path, bool isDirectory)
    {
      try
      {
        if (isDirectory)
          Directory.CreateDirectory(TranslatePath(path));
        else
          File.Create(TranslatePath(path)).Close();

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
        using (var stream = new FileStream(TranslatePath(path), FileMode.Open))
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
        using (var stream = new FileStream(TranslatePath(path), FileMode.Open))
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
        File.SetAttributes(TranslatePath(path), attributes);
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
          File.SetCreationTime(TranslatePath(path), creationTime.Value);

        if (lastAccessTime.HasValue)
          File.SetLastAccessTime(TranslatePath(path), lastAccessTime.Value);

        if (lastWriteTime.HasValue)
          File.SetLastWriteTime(TranslatePath(path), lastWriteTime.Value);

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
      if (Directory.Exists(TranslatePath(path)))
        return NtStatus.AccessDenied;

      if (!File.Exists(TranslatePath(path)))
        return NtStatus.ObjectNameNotFound;

      if (File.GetAttributes(TranslatePath(path)).HasFlag(FileAttributes.Directory))
        return NtStatus.AccessDenied;

      try
      {
        File.Delete(TranslatePath(path));
        return NtStatus.Success;
      }
      catch { return NtStatus.Error; }
    }

    [NLCCall("DeleteDirectory")]
    public NtStatus DeleteDirectory(string path)
    {
      if (!Directory.Exists(TranslatePath(path)))
        return NtStatus.ObjectPathNotFound;

      if (File.Exists(TranslatePath(path)))
        return NtStatus.AccessDenied;

      try
      {
        Directory.Delete(TranslatePath(path));
        return NtStatus.Success;
      }
      catch { return NtStatus.Error; }
    }

    [NLCCall("MoveFile")]
    public NtStatus MoveFile(string oldName, string newName, bool replace, bool isDirectory)
    {
      var exist = isDirectory ? Directory.Exists(TranslatePath(newName)) : File.Exists(TranslatePath(newName));

      try
      {
        if (!exist)
        {
          if (isDirectory)
            Directory.Move(TranslatePath(oldName), TranslatePath(newName));
          else
            File.Move(TranslatePath(oldName), TranslatePath(newName));
          return NtStatus.Success;
        }
        else if (replace)
        {
          if (isDirectory) //Cannot replace directory destination - See MOVEFILE_REPLACE_EXISTING
            return NtStatus.AccessDenied;

          File.Delete(TranslatePath(newName));
          File.Move(TranslatePath(oldName), TranslatePath(newName));
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