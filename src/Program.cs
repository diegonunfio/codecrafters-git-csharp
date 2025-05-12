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

    public static void LsTreeNameOnly(string sha)
    {
        string dir = $".git/objects/{sha.Substring(0, 2)}";
        string file = sha.Substring(2);
        string path = Path.Combine(dir, file);

        if (!File.Exists(path))
        {
            Console.Error.WriteLine("Tree object not found: " + path);
            return;
        }

        byte[] compressed = File.ReadAllBytes(path);

        // Descomprimir objeto zlib
        using var ms = new MemoryStream(compressed);
        using var zlib = new DeflateStream(ms, CompressionMode.Decompress);
        using var decompressedStream = new MemoryStream();
        zlib.CopyTo(decompressedStream);
        byte[] decompressed = decompressedStream.ToArray();

        // Saltar encabezado "tree <size>\0"
        int index = Array.IndexOf(decompressed, (byte)0);
        int pos = index + 1;

        while (pos < decompressed.Length)
        {
            // Leer modo
            int spaceIndex = Array.IndexOf(decompressed, (byte)' ', pos);
            string mode = Encoding.ASCII.GetString(decompressed, pos, spaceIndex - pos);
            pos = spaceIndex + 1;

            // Leer nombre
            int nullIndex = Array.IndexOf(decompressed, (byte)0, pos);
            string name = Encoding.ASCII.GetString(decompressed, pos, nullIndex - pos);
            pos = nullIndex + 1;

            // Saltar 20 bytes del SHA binario
            pos += 20;

            Console.WriteLine(name);
        }
    }

    public static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "init")
        {
            Init();
        }
        else if (args.Length == 3 && args[0] == "ls-tree" && args[1] == "--name-only")
        {
            LsTreeNameOnly(args[2]);
        }
        else
        {
            Console.WriteLine("Uso: git.sh ls-tree --name-only <sha>");
        }
    }
}