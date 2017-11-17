﻿using DokanNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace nlcfs
{
  public static class Headers
  {
    public const byte HEADER_CALL = 0x01;
    public const byte HEADER_RETURN = 0x02;
    public const byte HEADER_HANDSHAKE = 0x03;
    public const byte HEADER_MOVE = 0x04;
    public const byte HEADER_ERROR = 0x05;
  }

  internal class Client
  {
    private Socket cS = null;
    private Encryption eCls = null;

    #region Variables

    /// <summary>
    /// Port for the client to connect, changing this variable while the client is connected will have no effect.
    /// </summary>
    public int iPort { get; set; }

    /// <summary>
    /// IP for the client to connect, changing this variable while the client is connected will have no effect.
    /// </summary>
    public string sIP { get; set; }

    /// <summary>
    /// If true, debugging information will be output to the console.
    /// </summary>
    public static bool bDebugLog { get; set; } = false;

    #endregion Variables

    #region Prototypes

    public FileInfo[] FindFilesHelper(string fileName, string searchPattern)
      => RemoteCall<FileInfo[]>("FindFilesHelper", fileName, searchPattern);

    public bool Exists(string path, bool isDirectory)
      => RemoteCall<bool>("Exists", path, isDirectory);

    public long GetFileSize(string path)
      => RemoteCall<long>("GetFileSize", path);
    
    public FileAttributes GetAttributes(string path)
      => RemoteCall<FileAttributes>("GetAttributes", path);

    public bool Create(string path, bool isDirectory)
      => RemoteCall<bool>("Create", path, isDirectory);

    public byte[] Read(string path, long offset, int length)
      => RemoteCall<byte[]>("Read", path, offset, length);

    public int Write(string path, long offset, byte[] buffer)
      => RemoteCall<int>("Write", path, offset, buffer);

    public FileInfo GetFileInfo(string filename)
      => RemoteCall<FileInfo>("GetFileInfo", filename);

    public DirectoryInfo GetDirectoryInfo(string directoryname)
      => RemoteCall<DirectoryInfo>("GetDirectoryInfo", directoryname);

    public NtStatus SetPathAttributes(string path, FileAttributes attributes)
      => RemoteCall<NtStatus>("SetPathAttributes", path, attributes);

    public NtStatus SetPathTime(string path, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime)
      => RemoteCall<NtStatus>("SetPathTime", path, creationTime, lastAccessTime, lastWriteTime);

    public NtStatus DeleteFile(string path)
      => RemoteCall<NtStatus>("DeleteFile", path);

    public NtStatus DeleteDirectory(string path)
      => RemoteCall<NtStatus>("DeleteDirectory", path);

    public NtStatus MoveFile(string oldName, string newName, bool replace, bool isDirectory)
      => RemoteCall<NtStatus>("MoveFile", oldName, newName, replace, isDirectory);
    #endregion Prototypes

    /// <summary>
    /// Initializes the NLC Client. Credits to Killpot :^)
    /// </summary>
    /// <param name="sIP">IP of server to connect to.</param>
    /// <param name="iPort">Port of server to connect to.</param>
    public Client(string sIP = "localhost", int iPort = 1337)
    {
      this.sIP = sIP;
      this.iPort = iPort;
      cS = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }

    /// <summary>
    /// Connect to the Server and commence the OnConnect routine.
    /// </summary>
    public void Start()
    {
      cS.Connect(sIP, iPort);
      Log("Successfully connected to server!", ConsoleColor.Green);
      OnConnect();
    }

    /// <summary>
    /// Close connection to the Server.
    /// </summary>
    public void Stop()
    {
      cS.Close();
    }

    private void OnConnect()
    {
      BeginEncrypting(); // Starts the encryption process, you can also add whatever else you want to happen on connect here (Ex. Send HWID or Key)
    }

    private void BeginEncrypting()
    //Our handshake routine
    {
      byte[] bKeyTemp;

      CngKey cCngKey = CngKey.Create(CngAlgorithm.ECDiffieHellmanP521);
      byte[] cPublic = cCngKey.Export(CngKeyBlobFormat.EccPublicBlob);

      object[] oRecv = BlockingReceive();
      if (!oRecv[0].Equals(Headers.HEADER_HANDSHAKE)) // Sanity check
        throw new Exception("Unexpected error");

      byte[] sBuf = oRecv[1] as byte[];

      using (var cAlgo = new ECDiffieHellmanCng(cCngKey))
      using (CngKey sPubKey = CngKey.Import(sBuf, CngKeyBlobFormat.EccPublicBlob))
        bKeyTemp = cAlgo.DeriveKeyMaterial(sPubKey);

      BlockingSend(Headers.HEADER_HANDSHAKE, cPublic);

      Log(String.Format("Handshake complete, key length: {0}", bKeyTemp.Length), ConsoleColor.Green);

      eCls = new Encryption(bKeyTemp, HASH_STRENGTH.MINIMAL);
    }

    private T RemoteCall<T>(string identifier, params object[] param)
    {
      object[] payload = new object[param.Length + 2]; // +2 for header & method name
      payload[0] = Headers.HEADER_MOVE;
      payload[1] = identifier;
      Array.Copy(param, 0, payload, 2, param.Length);
      Log(String.Format("Calling remote method: {0}", identifier), ConsoleColor.Cyan);
      BlockingSend(payload);
      object[] oRecv = BlockingReceive();
      if (oRecv[0].Equals(Headers.HEADER_ERROR))
        throw new Exception("An exception was caused on the server!");
      else if (!oRecv[0].Equals(Headers.HEADER_RETURN))
        throw new Exception("Unexpected error");

      return (T)oRecv[1];
    }

    private void RemoteCall(string identifier, params object[] param)
    {
      object[] payload = new object[param.Length + 2]; // +2 for header & method name
      payload[0] = Headers.HEADER_CALL;
      payload[1] = identifier;
      Array.Copy(param, 0, payload, 2, param.Length);
      Log(String.Format("Calling remote method: {0}", identifier), ConsoleColor.Cyan);
      BlockingSend(payload);
      object[] oRecv = BlockingReceive();

      if (oRecv[0].Equals(Headers.HEADER_ERROR))
        throw new Exception("An exception was caused on the server!");
      else if (!oRecv[0].Equals(Headers.HEADER_RETURN))
        throw new Exception("Unexpected error");
    }

    private object[] BlockingReceive()
    {
      byte[] bSize = new byte[4];
      cS.Receive(bSize);

      byte[] sBuf = new byte[BitConverter.ToInt32(bSize, 0)];

      int iReceived = 0;

      while (iReceived < sBuf.Length)
        iReceived += cS.Receive(sBuf, iReceived, sBuf.Length - iReceived, SocketFlags.None);

      if (sBuf.Length <= 0)
        throw new Exception("Invalid data length, did the server force disconnect you?");

      Log(String.Format("Receiving {0} bytes...", sBuf.Length), ConsoleColor.Cyan);

      if (eCls != null)
        sBuf = eCls.AES_Decrypt(sBuf);
      else
        sBuf = Encryption.Decompress(sBuf);

      return Encryption.BinaryFormatterSerializer.Deserialize(sBuf);
    }

    private void BlockingSend(params object[] param)
    {
      byte[] bSend = Encryption.BinaryFormatterSerializer.Serialize(param);

      if (eCls != null)
        bSend = eCls.AES_Encrypt(bSend);
      else
        bSend = Encryption.Compress(bSend);

      Log(String.Format("Sending {0} bytes...", bSend.Length), ConsoleColor.Cyan);
      cS.Send(BitConverter.GetBytes(bSend.Length)); // Send expected payload length, gets bytes of int representing size, will always be 4 bytes for Int32
      cS.Send(bSend);
    }

    public static void Log(string message, ConsoleColor color = ConsoleColor.Gray)
    {

      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.Write("[{0}] ", DateTime.Now.ToLongTimeString());
      Console.ForegroundColor = color;
      Console.Write("{0}{1}", message, Environment.NewLine);
      Console.ResetColor();
    }
  }
}