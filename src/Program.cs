using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

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
            Directory.CreateDirectory(".git");
            Directory.CreateDirectory(".git/objects");
            Directory.CreateDirectory(".git/refs");
            File.WriteAllText(".git/HEAD", "ref: refs/heads/main\n");
            Console.WriteLine("Initialized git directory");
        }
        else if (command == "cat-file")
        {
            var sha = args[2];
            var dir = sha[..2];
            var fileName = sha[2..];
            var bytes = File.ReadAllBytes(Path.Combine(".git", "objects", dir, fileName));
            using var memoryStream = new MemoryStream(bytes, false);
            memoryStream.Seek(2, SeekOrigin.Begin);
            using var deflateStream =
                new DeflateStream(memoryStream, CompressionMode.Decompress);
            using var reader = new StreamReader(deflateStream);
            var content = reader.ReadToEnd();
            var parts = content.Split('\x00');
            Console.Write(parts[1]);
        }
        else if (command == "hash-object")
        {
            var fileName = args[2];
            var content = File.ReadAllText(fileName);
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var header = $"blob {contentBytes.Length}\0";
            var headerBytes = Encoding.UTF8.GetBytes(header);
            byte[] bytes = [..headerBytes, ..contentBytes];
            var hash =
                Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(bytes))
                    .ToLowerInvariant();
            Console.Write(hash);
            var objectPath = Path.Combine(".git", "objects", hash[..2]);
            Directory.CreateDirectory(objectPath);
            var fullPath = Path.Combine(objectPath, hash[2..]);
            using var stream = new ZLibStream(
                new FileStream(fullPath, FileMode.Create, FileAccess.Write),
                CompressionMode.Compress);
            stream.Write(bytes);
        }
        else if (command == "ls-tree")
        {
            var hash = args[2];
            var treePath = Path.Combine(".git", "objects", hash[..2], hash[2..]);
            var contentBytes = File.ReadAllBytes(treePath);
            using var memoryStream = new MemoryStream(contentBytes);
            using var zStream = new ZLibStream(memoryStream, CompressionMode.Decompress);
            using var reader = new StreamReader(zStream);
            var treeObject = reader.ReadToEnd();
            var splittedContent = treeObject.Split("\0");
            var fileNames =
                splittedContent.Skip(1).Select(s => s.Split(" ").Last()).SkipLast(1);
            foreach (var fileName in fileNames)
            {
                Console.WriteLine(fileName);
            }
        }
        else
        {
            throw new ArgumentException($"Unknown command {command}");
        }
    }
}