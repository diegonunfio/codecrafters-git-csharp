using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

class Program
{
    public static void Init()
    {
        Directory.CreateDirectory(".git/objects/info");
        Directory.CreateDirectory(".git/objects/pack");
        Directory.CreateDirectory(".git/refs/heads");
        File.WriteAllText(".git/HEAD", "ref: refs/heads/master\n");
        Console.WriteLine("Initialized git repository");
    }

    public static byte[] DecompressGitObject(byte[] compressed)
    {
        using var input = new MemoryStream(compressed, 2, compressed.Length - 2);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
    }

    public static void LsTreeNameOnly(string sha)
    {
        string path = Path.Combine(".git/objects", sha.Substring(0, 2), sha.Substring(2));
        if (!File.Exists(path))
        {
            Console.Error.WriteLine("Tree object not found: " + path);
            Environment.Exit(1);
        }

        byte[] compressed = File.ReadAllBytes(path);
        byte[] decompressed = DecompressGitObject(compressed);

        int index = Array.IndexOf(decompressed, (byte)0);
        int pos = index + 1;

        while (pos < decompressed.Length)
        {
            int spaceIndex = Array.IndexOf(decompressed, (byte)' ', pos);
            string mode = Encoding.ASCII.GetString(decompressed, pos, spaceIndex - pos);
            pos = spaceIndex + 1;

            int nullIndex = Array.IndexOf(decompressed, (byte)0, pos);
            string name = Encoding.ASCII.GetString(decompressed, pos, nullIndex - pos);
            pos = nullIndex + 1;

            pos += 20;
            Console.WriteLine(name);
        }
    }

    public static void HashObjectWrite(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine("File not found: " + filePath);
            Environment.Exit(1);
        }

        byte[] content = File.ReadAllBytes(filePath);
        string header = $"blob {content.Length}\0";
        byte[] headerBytes = Encoding.ASCII.GetBytes(header);

        byte[] fullContent = new byte[headerBytes.Length + content.Length];
        Buffer.BlockCopy(headerBytes, 0, fullContent, 0, headerBytes.Length);
        Buffer.BlockCopy(content, 0, fullContent, headerBytes.Length, content.Length);

        using var sha1 = SHA1.Create();
        byte[] shaBytes = sha1.ComputeHash(fullContent);
        string shaHex = BitConverter.ToString(shaBytes).Replace("-", "").ToLower();

        string dir = Path.Combine(".git/objects", shaHex.Substring(0, 2));
        string file = shaHex.Substring(2);
        Directory.CreateDirectory(dir);
        string objectPath = Path.Combine(dir, file);

        if (!File.Exists(objectPath))
        {
            using var output = new MemoryStream();
            using (var deflate = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflate.Write(fullContent, 0, fullContent.Length);
            }
            File.WriteAllBytes(objectPath, output.ToArray());
        }

        Console.WriteLine(shaHex);
    }


    public static void Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "init")
            {
                Init();
            }
            else if (args.Length == 3 && args[0] == "ls-tree" && args[1] == "--name-only")
            {
                LsTreeNameOnly(args[2]);
            }
            else if (args.Length == 3 && args[0] == "hash-object" && args[1] == "-w")
            {
                HashObjectWrite(args[2]);
            }
            else
            {
                Console.WriteLine("Uso: git.sh ls-tree --name-only <sha>");
                Environment.Exit(1);
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            Environment.Exit(1);
        }
    }
}