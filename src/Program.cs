using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;


class Program
{
  static void Main(string[] args)
  {
    if (args.Length < 1)
    {
      Console.WriteLine("Please provide a command.");
      return;
    }

    // You can use print statements as follows for debugging, they'll be visible
    // when running tests.
    Console.Error.WriteLine("Logs from your program will appear here!");
    string command = args[0];
    if (command == "init")
    {
      // Uncomment this block to pass the first stage
      //
      Directory.CreateDirectory(".git");
      Directory.CreateDirectory(".git/objects");
      Directory.CreateDirectory(".git/refs");
      File.WriteAllText(".git/HEAD", "ref: refs/heads/main\n");
      Console.WriteLine("Initialized git directory");
    }
    else if (command == "cat-file")
    {
      if (args.Length < 3)
      {
        Console.WriteLine("Usage: git cat-file <type> <object>");
        return;
      }

      string type = args[1];
      if (type != "-p")
      {
        Console.WriteLine($"Unknown type {type}");
        return;
      }

      string objectName = args[2];
      string objectPath =
        Path.Combine(".git", "objects", objectName[..2], objectName[2..]);
      if (!File.Exists(objectPath))
      {
        Console.WriteLine($"Object {objectName} not found");
        return;
      }

      using var stream = File.OpenRead(objectPath);
      using ZLibStream decompressionStream =
        new(stream, CompressionMode.Decompress);
      MemoryStream memoryStream = new();
      decompressionStream.CopyTo(memoryStream);
      memoryStream.Position = 0;
      using StreamReader reader = new(memoryStream);
      string content = reader.ReadToEnd();
      string objectType = content[..4];
      if (objectType != "blob")
      {
        Console.WriteLine($"Unknown object type {objectType}");
        return;
      }

      int zeroByteIndex = content[5..].IndexOf('\0');
      if (zeroByteIndex == -1)
      {
        Console.WriteLine($"Invalid object {objectName}");
        return;
      }

      string sizeStringWithoutZero = content.Substring(5, zeroByteIndex);
      if (!int.TryParse(sizeStringWithoutZero, out int objectSize))
      {
        Console.WriteLine($"Invalid object size {sizeStringWithoutZero}");
        return;
      }

      Console.Write(content[(5 + zeroByteIndex + 1)..]);
    }
    else if (command == "hash-object")
    {
      if (args.Length < 2)
      {
        Console.WriteLine("Usage: git hash-object <file>");
        return;
      }

      string filePath = args[1];
      bool writeObject = args[1] == "-w";
      if (writeObject)
      {
        if (args.Length < 3)
        {
          Console.WriteLine("Usage: git hash-object -w <file>");
          return;
        }

        filePath = args[2];
      }

      if (!File.Exists(filePath))
      {
        Console.WriteLine($"File {filePath} not found");
        return;
      }

      using var stream = File.OpenRead(filePath);
      using MemoryStream memoryStream = new();
      stream.CopyTo(memoryStream);
      memoryStream.Position = 0;
      string objectType = "blob";
      string header = $"{objectType} {memoryStream.Length}\0";
      byte[] headerBytes = System.Text.Encoding.UTF8.GetBytes(header);
      using MemoryStream headerStream = new();
      headerStream.Write(headerBytes, 0, headerBytes.Length);
      memoryStream.CopyTo(headerStream);
      // byte[] sha1Hash =
      // System.Security.Cryptography.SHA1.Create().ComputeHash(headerStream.ToArray());
      byte[] sha1Hash =
        System.Security.Cryptography.SHA1.HashData(headerStream.ToArray());
      string sha1HashString =
        BitConverter.ToString(sha1Hash).Replace("-", "").ToLowerInvariant();
      if (writeObject)
      {
        string objectDirectory =
          Path.Combine(".git", "objects", sha1HashString[..2]);
        Directory.CreateDirectory(objectDirectory);
        string objectPath = Path.Combine(objectDirectory, sha1HashString[2..]);
        using FileStream fileStream = File.Create(objectPath);
        using ZLibStream compressionStream =
          new(fileStream, CompressionMode.Compress);
        headerStream.Position = 0;
        headerStream.CopyTo(compressionStream);
      }

      Console.WriteLine(sha1HashString);
    }
    else if (command == "ls-tree")
    {
      if (args.Length < 2)
      {
        Console.WriteLine("Usage: git ls-tree <object>");
        return;
      }

      bool nameOnly = false;
      string objectName = args[1];
      if (objectName == "--name-only")
      {
        if (args.Length < 3)
        {
          Console.WriteLine("Usage: git ls-tree --name-only <object>");
          return;
        }

        objectName = args[2];
        nameOnly = true;
      }

      string objectPath =
        Path.Combine(".git", "objects", objectName[..2], objectName[2..]);
      if (!File.Exists(objectPath))
      {
        Console.WriteLine($"Object {objectName} not found");
        return;
      }

      using var stream = File.OpenRead(objectPath);
      using var decompressionStream =
        new ZLibStream(stream, CompressionMode.Decompress);
      using var memoryStream = new MemoryStream();
      decompressionStream.CopyTo(memoryStream);
      memoryStream.Position = 0;
      using var reader = new BinaryReader(memoryStream);
      string header = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(4));
      if (header != "tree")
      {
        Console.WriteLine($"Invalid object type: {header}");
        return;
      }

      string sizeString = "";
      char c;
      while ((c = (char)reader.ReadByte()) != '\0')
      {
        sizeString += c;
      }

      // Console.WriteLine($"size: {sizeString}");
      while (memoryStream.Position < memoryStream.Length)
      {
        // Read mode (e.g., 100644 or 40000)
        string mode = "";
        while ((c = (char)reader.ReadByte()) != ' ')
        {
          mode += c;
        }

        // Read file name
        string name = "";
        while ((c = (char)reader.ReadByte()) != '\0')
        {
          name += c;
        }

        // Read SHA1 hash (20 bytes)
        byte[] sha1Bytes = reader.ReadBytes(20);
        string sha1 =
          BitConverter.ToString(sha1Bytes).Replace("-", "").ToLowerInvariant();
        string type = mode == "40000" ? "tree" : "blob";
        if (nameOnly)
        {
          Console.WriteLine(name);
        }
        else
        {
          Console.WriteLine($"{mode.PadLeft(6, '0')} {type} {sha1}\t{name}");
        }
      }
    }
    else if (command == "write-tree")
    {
      var currentPath = Directory.GetCurrentDirectory();
      var currentFilePathHash = GenerateTreeObjectFileHash(currentPath);
      if (currentFilePathHash is null)
      {
        Console.WriteLine("No files to hash");
        return;
      }

      var hashString = Convert.ToHexString(currentFilePathHash).ToLower();
      Console.Write(hashString);
    }
    else
    {
      throw new ArgumentException($"Unknown command {command}");
    }

    static byte[]? GenerateTreeObjectFileHash(string currentPath)
    {
      if (currentPath.Contains(".git"))
      {
        return null;
      }

      var files = Directory.GetFiles(currentPath);
      var directories = Directory.GetDirectories(currentPath);
      var treeEntries = new List<TreeEntry>();
      foreach (var file in files)
      {
        string fileName = Path.GetFileName(file);
        var fileContentInBytes = File.ReadAllBytes(file);
        var fileHash = GenerateHashByte("blob", fileContentInBytes);
        var fileEntry = new TreeEntry("100644", fileName, fileHash);
        treeEntries.Add(fileEntry);
      }

      for (var i = 0; i < directories.Length; i++)
      {
        var directoryName = Path.GetFileName(directories[i]);
        var directoryHash = GenerateTreeObjectFileHash(directories[i]);
        if (directoryHash is not null)
        {
          var directoryEntry = new TreeEntry("40000", directoryName, directoryHash);
          treeEntries.Add(directoryEntry);
        }
      }

      var treeObject = CreateTreeObject(treeEntries);
      return GenerateHashByte("tree", treeObject);
    }

    static byte[] GenerateHashByte(string gitObjectType, byte[] input)
    {
      var objectHeader = CreateObjectHeaderInBytes(gitObjectType, input);
      var gitObject = objectHeader.Concat(input).ToArray();
      var hash = System.Security.Cryptography.SHA1.HashData(gitObject);
      using MemoryStream memoryStream = new();
      using (ZLibStream zlibStream = new(memoryStream, CompressionLevel.Optimal))
      {
        zlibStream.Write(gitObject, 0, gitObject.Length);
      }

      var compressedObject = memoryStream.ToArray();
      var hashString = Convert.ToHexString(hash).ToLower();
      Directory.CreateDirectory($".git/objects/{hashString[..2]}");
      File.WriteAllBytes($".git/objects/{hashString[..2]}/{hashString[2..]}",
        compressedObject);
      return hash;
    }

    static byte[] CreateObjectHeaderInBytes(string gitObjectType, byte[] input)
    {
      var header = $"{gitObjectType} {input.Length}\x00";
      return System.Text.Encoding.UTF8.GetBytes(header);
    }

    static byte[] CreateTreeObject(List<TreeEntry> treeEntries)
    {
      using var memoryStream = new MemoryStream();
      using var streamWriter =
        new StreamWriter(memoryStream, new System.Text.UTF8Encoding(false));
      foreach (var entry in treeEntries.OrderBy(x => x.FileName))
      {
        var line = $"{entry.Mode} {entry.FileName}\x00";
        streamWriter.Write(line);
        streamWriter.Flush();
        memoryStream.Write(entry.Hash, 0, entry.Hash.Length);
      }

      streamWriter.Flush();
      return memoryStream.ToArray();
    }
  }
}

public record TreeEntry(string Mode, string FileName, byte[] Hash);