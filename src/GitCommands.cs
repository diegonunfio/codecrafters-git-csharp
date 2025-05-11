using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

public static class GitCommands
{
    public static void Init()
    {
        Directory.CreateDirectory(".git");
        Directory.CreateDirectory(".git/objects");
        Directory.CreateDirectory(".git/refs");
        File.WriteAllText(".git/HEAD", "ref: refs/heads/main\n");
    }

    public static void HashObject(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"El archivo '{filePath}' no existe.");
            Environment.Exit(1);
        }

        byte[] content = File.ReadAllBytes(filePath);
        string header = $"blob {content.Length}\0";

        byte[] headerBytes = Encoding.UTF8.GetBytes(header);
        byte[] blobData = new byte[headerBytes.Length + content.Length];
        Buffer.BlockCopy(headerBytes, 0, blobData, 0, headerBytes.Length);
        Buffer.BlockCopy(content, 0, blobData, headerBytes.Length, content.Length);

        byte[] hashBytes = SHA1.Create().ComputeHash(blobData);
        string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

        byte[] compressedData;
        using (var outStream = new MemoryStream())
        {
            using (var deflate = new DeflateStream(outStream, CompressionLevel.Optimal, true))
            {
                deflate.Write(blobData, 0, blobData.Length);
            }

            compressedData = outStream.ToArray();
        }

        string dir = Path.Combine(".git", "objects", hash.Substring(0, 2));
        string fileName = hash.Substring(2);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, fileName), compressedData);

        Console.WriteLine(hash);
    }

    public static void CatFile(string hash)
    {
        string dir = Path.Combine(".git", "objects", hash.Substring(0, 2));
        string fileName = hash.Substring(2);
        string path = Path.Combine(dir, fileName);

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"El objeto '{hash}' no existe.");
            Environment.Exit(1);
        }

        using (var fs = File.OpenRead(path))
        using (var deflate = new DeflateStream(fs, CompressionMode.Decompress))
        using (var ms = new MemoryStream())
        {
            deflate.CopyTo(ms);
            byte[] decompressed = ms.ToArray();

            // Buscar el primer byte nulo (\0) y extraer solo el contenido
            int nullIndex = Array.IndexOf(decompressed, (byte)0);
            byte[] contentOnly = decompressed[(nullIndex + 1)..];

            Console.Write(Encoding.UTF8.GetString(contentOnly));
        }
    }
}