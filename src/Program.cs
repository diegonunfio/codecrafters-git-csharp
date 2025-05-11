using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        if (args[0] == "hash-object" && args[1] == "-w")
        {
            string filePath = args[2];
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

            string dir = $".git/objects/{hashHex.Substring(0, 2)}";
            string file = hashHex.Substring(2);
            Directory.CreateDirectory(dir);

            using var fs = new FileStream($"{dir}/{file}", FileMode.Create);
            using var deflate = new ZLibStream(fs, CompressionLevel.Optimal);
            deflate.Write(fullData, 0, fullData.Length);

            Console.WriteLine(hashHex);
        }
    }
}

