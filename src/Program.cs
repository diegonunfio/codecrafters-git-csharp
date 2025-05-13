using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;


class Program
{
    public static void Main(string[] args) {
      if (args.Length < 1) {
        Console.WriteLine("Please provide a command.");
        return;
      }
      Console.Error.WriteLine("Logs from your program will appear here!");
      string command = args[0];
      string[]? flags = args.Where(x => x.StartsWith('-')).ToArray();
      string? file =
          args.FirstOrDefault(x => !flags.Contains(x) && x != command);
      string[]? hashes =
          args.Where(x => !flags.Contains(x) && x != command).ToArray();
      if (command == "init") {
        InitRepo();
      } else if (command == "cat-file" && args[1] == "-p") {
        CatFile(args[2]);
      } else if (command == "hash-object" && args[1] == "-w") {
        HashObjectW(args[2]);
      } else if (command == "ls-tree" && args[1] == "--name-only") {
        if (args.Length < 3 || args[1] != "--name-only") {
          Console.WriteLine("Usage: ls-tree --name-only <tree-sha>");
          return;
        }
        LsTree(args[2]);
      } else if (command == "write-tree") {
        var currentPath = Directory.GetCurrentDirectory();
        var currentFilePathHash = GenerateTreeObjectFileHash(currentPath);
        var hashString = Convert.ToHexString(currentFilePathHash).ToLower();
        Console.Write(hashString);
      } else if (command == "commit-tree") {
        if (hashes is null || hashes.Length != 3) {
          throw new ArgumentException("Wrong amount of args");
        }
        if (!flags.Contains("-m") || !flags.Contains("-p")) {
          throw new ArgumentException("No message or parent flag");
        }
        string treeHash = hashes[0];
        string parentHash = hashes[1];
        string message = hashes[2];
        string commit = CreateCommit(treeHash, parentHash, message);
        Console.WriteLine(commit);
      } else if (command == "clone") {
        if (args.Length < 2) {
          Console.WriteLine(
              "Usage: clone https://github.com/blah/blah <some_dir>");
          return;
        }
        CloneRepo(args[1], args[2]);
      } else {
        throw new ArgumentException($"Unknown command {command}");
      }
    }
    private static void LsTree(string treeSha) {
      string objectDir =
          Path.Combine(".git", "objects", treeSha.Substring(0, 2));
      string objectPath = Path.Combine(objectDir, treeSha.Substring(2));
      if (!File.Exists(objectPath)) {
        Console.WriteLine($"Fatal: Not a valid object name {treeSha}");
        return;
      }
      byte[] compressedData = File.ReadAllBytes(objectPath);
      byte[] decompressedData;
      using (var memoryStream = new MemoryStream(compressedData)) {
        // Skip the zlib header (2 bytes)
        memoryStream.Seek(2, SeekOrigin.Begin);
        using (var deflateStream = new DeflateStream(
                   memoryStream,
                   CompressionMode.Decompress)) using (var outputStream =
                                                           new MemoryStream()) {
          deflateStream.CopyTo(outputStream);
          decompressedData = outputStream.ToArray();
        }
      }
      int nullIndex = Array.IndexOf(decompressedData, (byte)0);
      int contentStart = nullIndex + 1;
      List<string> entries = new List<string>();
      while (contentStart < decompressedData.Length) {
        int spaceIndex =
            Array.IndexOf(decompressedData, (byte)' ', contentStart);
        int nullTerminator =
            Array.IndexOf(decompressedData, (byte)0, spaceIndex + 1);
        string name = Encoding.UTF8.GetString(decompressedData, spaceIndex + 1,
                                              nullTerminator - spaceIndex - 1);
        entries.Add(name);
        contentStart = nullTerminator + 21;
      }
      foreach (string entry in entries.OrderBy(e => e)) {
        Console.WriteLine(entry);
      }
    }
    private static void HashObjectW(string newfile) {
      byte nullByte = (byte)0;
      string startPath = "./.git/objects/";
      using FileStream fs = File.OpenRead(newfile);
      MemoryStream ms = new();
      fs.CopyTo(ms);
      Memory<byte> buffer = new Memory<byte>(ms.GetBuffer())[..(int)ms.Length];
      string obj = Encoding.UTF8.GetString(
          buffer.Span); 
      byte[] blobContent = Encoding.UTF8.GetBytes(obj);
      int blobLen = blobContent.Length;
      string s = "blob " + Convert.ToString(blobLen);
      StringBuilder sb = new StringBuilder();
      sb.Append(s);
      sb.Append(char.MinValue);
      sb.Append(obj);
      byte[] bytes = Encoding.UTF8.GetBytes(s);
      byte[] blobStart = Combine(bytes, [nullByte]);
      byte[] blob = Combine(blobStart, blobContent);
      // create a SHA hash from the content of file
      string hash = ShaHash(
          sb.ToString()); 
      hash = hash.ToLower();
      Console.WriteLine(hash);
      string firstDir = hash.Substring(0, 2);
      string secondFile = hash.Substring(2);
      string full = startPath + firstDir + "/" + secondFile;
      string dirPath = startPath + firstDir;
      if (!Directory.Exists(dirPath)) {
        Directory.CreateDirectory(dirPath);
      }
      using FileStream fw = File.OpenWrite(full);
      using ZLibStream zs = new(fw, CompressionMode.Compress);
      zs.Write(blob);
    }
    private static void InitRepo() {
      Directory.CreateDirectory(".git");
      Directory.CreateDirectory(".git/objects");
      Directory.CreateDirectory(".git/refs");
      File.WriteAllText(".git/HEAD", "ref: refs/heads/main\n");
      Console.WriteLine("Initialized git directory");
    }
    private static void CatFile(string fileNameAndDir) {
      string middleDir = fileNameAndDir.Substring(0, 2);
      string newFile = fileNameAndDir.Substring(2);
      string fullPath = "./.git/objects/" + middleDir + "/" + newFile;
      using FileStream fs = File.OpenRead(fullPath);
      using ZLibStream zs = new(fs, CompressionMode.Decompress);
      MemoryStream ms = new();
      zs.CopyTo(ms);
      Memory<byte> buffer = new Memory<byte>(ms.GetBuffer())[..(int)ms.Length];
      string obj = Encoding.UTF8.GetString(buffer.Span[..4]);
      int nullByte = buffer[5..].Span.IndexOf((byte)0);
      Memory<byte> blobStr = buffer[5..][(nullByte + 1)..];
      if (int.TryParse(Encoding.UTF8.GetString(buffer[5..].Span[..nullByte]),
                       out int blobLength) &&
          blobLength != blobStr.Length) {
        Console.WriteLine("Bad Blob Length");
        return;
      } else {
        Console.Write(Encoding.UTF8.GetString(blobStr.Span));
      }
    }
    private static void CloneRepo(string url, string dir) {
      string directory = dir;
      Uri uri;
      try {
        uri = new Uri(url);
      } catch {
        Console.WriteLine($"Could not load repo {url}");
        return;
      }
      string lastSegment = uri.Segments.Last().TrimEnd('/');
      if (lastSegment.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) {
        lastSegment = lastSegment.Substring(0, lastSegment.Length - 4);
      }
    directory = lastSegment;
     try {
      Repository.clone(url, dir);
       Console.WriteLine($"Cloned repo from {url} into {directory}");
     } catch (Exception ex) {
        Console.WriteLine($"Error cloning repo: {ex.Message}");
      }
    }
    private static string CreateCommit(string treeHash, string parentHash,
                                       string message) {
      string author = "My Name <my.name@gmail.com";
      string committer = author;
      long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
      string timeZone = "-0600"; // example
      StringBuilder sb = new StringBuilder();
      sb.AppendLine($"tree {treeHash}");
      sb.AppendLine($"parent {parentHash}");
      sb.AppendLine($"author {author} {timestamp} {timeZone}");
      sb.AppendLine($"committer {committer} {timestamp} {timeZone}");
      sb.AppendLine();
      sb.AppendLine(message);
      byte[] content = Encoding.UTF8.GetBytes(sb.ToString());
      return HashObject(content, "commit");
    }
    private static string HashObject(byte[] content, string type) {
      string header = $"{type} {content.Length}\0";
      byte[] headerBytes = Encoding.UTF8.GetBytes(header);
      using (var sha1 = SHA1.Create()) {
        sha1.TransformBlock(headerBytes, 0, headerBytes.Length, null, 0);
        sha1.TransformFinalBlock(content, 0, content.Length);
        string hashHex = Convert.ToHexStringLower(sha1.Hash);
        string objDir =
            Path.Combine(".git", "objects", hashHex.Substring(0, 2));
        string objPath = Path.Combine(objDir, hashHex.Substring(2));
        Directory.CreateDirectory(objDir);
        using (var fileStream = File.Create(objPath)) {
          fileStream.WriteByte(0x78);
          fileStream.WriteByte(0x01);
          using (var deflateStream = new DeflateStream(
                     fileStream, CompressionMode.Compress, true)) {
            deflateStream.Write(headerBytes, 0, headerBytes.Length);
            deflateStream.Write(content, 0, content.Length);
          }
          uint adler32 = ComputeAdler32(headerBytes.Concat(content).ToArray());
          fileStream.WriteByte((byte)(adler32 >> 24));
          fileStream.WriteByte((byte)(adler32 >> 16));
          fileStream.WriteByte((byte)(adler32 >> 8));
          fileStream.WriteByte((byte)adler32);
        }
        return hashHex;
      }
    }
    static uint ComputeAdler32(byte[] data) {
      const uint MOD_ADLER = 65521;
      uint a = 1, b = 0;
      foreach (byte bt in data) {
        a = (a + bt) % MOD_ADLER;
        b = (b + a) % MOD_ADLER;
      }
      return (b << 16) | a;
    }
    public static string ShaHash(string input) {
      return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(input)));
    }
    public static byte[] Combine(byte[] first, byte[] second) {
      byte[] bytes = new byte[first.Length + second.Length];
      Buffer.BlockCopy(first, 0, bytes, 0, first.Length);
      Buffer.BlockCopy(second, 0, bytes, first.Length, second.Length);
      return bytes;
    }
    static byte[]? GenerateTreeObjectFileHash(string currentPath) {
      if (currentPath.Contains(".git")) {
        return null;
      }
      var files = Directory.GetFiles(currentPath);
      var directories = Directory.GetDirectories(currentPath);
      var treeEntries = new List<TreeEntry>();
      foreach (var file in files) {
        string fileName = Path.GetFileName(file);
        var fileContentInBytes = File.ReadAllBytes(file);
        var fileHash = GenerateHashByte("blob", fileContentInBytes);
        var fileEntry = new TreeEntry("100644", fileName, fileHash);
        treeEntries.Add(fileEntry);
      }
      for (var i = 0; i < directories.Length; i++) {
        var directoryName = Path.GetFileName(directories[i]);
        var directoryHash = GenerateTreeObjectFileHash(directories[i]);
        if (directoryHash is not null) {
          var directoryEntry =
              new TreeEntry("40000", directoryName, directoryHash);
          treeEntries.Add(directoryEntry);
        }
      }
      var treeObject = CreateTreeObject(treeEntries);
      return GenerateHashByte("tree", treeObject);
    }
    static byte[] GenerateHashByte(string gitObjectType, byte[] input) {
      var objectHeader = CreateObjectHeaderInBytes(gitObjectType, input);
      var gitObject = objectHeader.Concat(input).ToArray();
      var hash = SHA1.HashData(gitObject);
      using MemoryStream memoryStream = new MemoryStream();
      using (ZLibStream zlibStream =
                 new ZLibStream(memoryStream, CompressionLevel.Optimal)) {
        zlibStream.Write(gitObject, 0, gitObject.Length);
      }
      var compressedObject = memoryStream.ToArray();
      var hashString = Convert.ToHexString(hash).ToLower();
      Directory.CreateDirectory($".git/objects/{hashString[..2]}");
      File.WriteAllBytes($".git/objects/{hashString[..2]}/{hashString[2..]}",
                         compressedObject);
      return hash;
    }
    static byte[] CreateObjectHeaderInBytes(string gitObjectType,
                                            byte[] input) {
      var header = $"{gitObjectType} {input.Length}\x00";
      return Encoding.UTF8.GetBytes(header);
    }
    static byte[] CreateTreeObject(List<TreeEntry> treeEntries) {
      using var memoryStream = new MemoryStream();
      using var streamWriter =
          new StreamWriter(memoryStream, new UTF8Encoding(false));
      foreach (var entry in treeEntries.OrderBy(x => x.FileName)) {
        var line = $"{entry.Mode} {entry.FileName}\x00";
        streamWriter.Write(line);
        streamWriter.Flush();
        memoryStream.Write(entry.Hash, 0, entry.Hash.Length);
      }
      streamWriter.Flush();
      return memoryStream.ToArray();
    }
    public record TreeEntry(string Mode, string FileName, byte[] Hash);
  }

internal class Repository
{
}