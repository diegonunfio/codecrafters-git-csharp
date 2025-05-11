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
                return 0; // Exit OK
            }
            else
            {
                Console.Error.WriteLine("Usage: hash-object -w <file>");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            return 1;
        }
    }
}