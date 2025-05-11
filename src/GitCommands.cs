using System.IO.Compression;
using System.Text;

namespace codecrafters_git;

public class GitCommands
{
    public static void CatFile(string hash)
    {
        string gitObjectsPath = Path.Combine(".git", "objects", hash.Substring(0, 2), hash.Substring(2));
        
        if (!File.Exists(gitObjectsPath))
        {
            Console.Error.WriteLine("Error: objeto no encontrado");
            Environment.Exit(1);
        }

        byte[] compressed = File.ReadAllBytes(gitObjectsPath);

        using (var compressedStream = new MemoryStream(compressed))
        using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
        using (var resultStream = new MemoryStream())
        {
            deflateStream.CopyTo(resultStream);
            byte[] decompressed = resultStream.ToArray();

            // Buscar el byte 0 que separa el header del contenido
            int nullIndex = Array.IndexOf(decompressed, (byte)0);

            // Extraer solo el contenido del blob (después del byte nulo)
            byte[] contentBytes = new byte[decompressed.Length - nullIndex - 1];
            Array.Copy(decompressed, nullIndex + 1, contentBytes, 0, contentBytes.Length);

            // Imprimir sin salto de línea final
            Console.Write(Encoding.UTF8.GetString(contentBytes));
        }
    }
}