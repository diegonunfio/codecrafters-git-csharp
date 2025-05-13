using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;


class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
            return;

        if (args.Length == 1 && args[0] == "init")
        {
            Directory.CreateDirectory(".git/objects");
            Directory.CreateDirectory(".git/refs/heads");
            File.WriteAllText(".git/HEAD", "ref: refs/heads/master\n");
        }
        if (args.Length == 3 && args[0] == "ls-tree" && args[1] == "--name-only")
        {
            string treeSha = args[2];
            string objectPath = $".git/objects/{treeSha.Substring(0, 2)}/{treeSha.Substring(2)}";

            if (!File.Exists(objectPath))
            {
                Console.Error.WriteLine("Object not found");
                return;
            }

            byte[] compressedData = File.ReadAllBytes(objectPath);
            byte[] decompressedData;

            using (MemoryStream input = new MemoryStream(compressedData))
            using (DeflateStream zlibStream = new DeflateStream(input, CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream())
            {
                zlibStream.CopyTo(output);
                decompressedData = output.ToArray();
            }

            int headerEnd = Array.IndexOf(decompressedData, (byte)0); 
            int index = headerEnd + 1;

            while (index < decompressedData.Length)
            {
                int spaceIndex = Array.IndexOf(decompressedData, (byte)' ', index);
                string mode = Encoding.ASCII.GetString(decompressedData, index, spaceIndex - index);
                index = spaceIndex + 1;
                
                int nullIndex = Array.IndexOf(decompressedData, (byte)0, index);
                string name = Encoding.ASCII.GetString(decompressedData, index, nullIndex - index);
                index = nullIndex + 1;

                index += 20;

                Console.WriteLine(name);
            }
        }
    }
}