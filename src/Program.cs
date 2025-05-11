using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

class Program
{
    static int Main(string[] args)
    {
        try
        {
            // Etapa 1: git init
            if (args.Length == 1 && args[0] == "init")
            {
                Directory.CreateDirectory(".git/objects");
                Directory.CreateDirectory(".git/refs");
                return 0;
            }


            // Etapa 2: git hash-object -w <file>
            if (args.Length == 3 && args[0] == "hash-object" && args[1] == "-w")
            {
                string filePath = args[2];
                if (!File.Exists(filePath))
                {
                    Console.Error.WriteLine($"Error: File not found: {filePath}");
                    return 1;
                }

                string content = File.ReadAllText(filePath);
                byte[] contentBytes = Encoding.UTF8.GetBytes(content);
                string header = $"blob {contentBytes.Length}\0";
                byte[] headerBytes = Encoding.UTF8.GetBytes(header);

                byte[] fullData = new byte[headerBytes.Length + contentBytes.Length];
                Buffer.BlockCopy(headerBytes, 0, fullData, 0, headerBytes.Length);
                Buffer.BlockCopy(contentBytes, 0, fullData, headerBytes.Length, contentBytes.Length);

                using var sha1 = SHA1.Create();
                byte[] hashBytes = sha1.ComputeHash(fullData);
                string hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                string dir = $".git/objects/{hashHex[..2]}";
                string file = hashHex[2..];
                Directory.CreateDirectory(dir);

                using var fs = new FileStream($"{dir}/{file}", FileMode.Create);
                using var zlib = new ZLibStream(fs, CompressionLevel.Optimal);
                zlib.Write(fullData, 0, fullData.Length);

                Console.WriteLine(hashHex);
                return 0;
            }

            // Etapa 3: git cat-file -p <hash>
            if (args.Length == 3 && args[0] == "cat-file" && args[1] == "-p")
            {
                string hash = args[2];
                string dir = $".git/objects/{hash[..2]}";
                string file = hash[2..];
                string path = Path.Combine(dir, file);

                if (!File.Exists(path))
                {
                    Console.Error.WriteLine("Error: object not found.");
                    return 1;
                }

                byte[] compressed = File.ReadAllBytes(path);

                using var inputStream = new MemoryStream(compressed);
                using var zlib = new ZLibStream(inputStream, CompressionMode.Decompress);
                using var output = new MemoryStream();
                zlib.CopyTo(output);

                byte[] decompressed = output.ToArray();

                int nullIndex = Array.IndexOf(decompressed, (byte)0);
                if (nullIndex < 0)
                {
                    Console.Error.WriteLine("Invalid blob format.");
                    return 1;
                }

                byte[] contentBytes = decompressed[(nullIndex + 1)..];
                string content = Encoding.UTF8.GetString(contentBytes);
                Console.Write(content); // No usar WriteLine
                return 0;
            }

            // Si el comando no es válido
            Console.Error.WriteLine("Usage: hash-object -w <file>");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            return 1;
        }
    }
}