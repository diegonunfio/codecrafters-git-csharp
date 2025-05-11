using System.IO.Compression;
using System.Text;

namespace codecrafters_git;

public class GitCommands
{
    public static void CatFile(string hash)
    {
        string path = Path.Combine(".git", "objects", hash.Substring(0, 2), hash.Substring(2));
        if (!File.Exists(path))
        {
            Console.Error.WriteLine("Objeto no encontrado");
            Environment.Exit(1);
        }

        byte[] compressed = File.ReadAllBytes(path);

        using (var compressedStream = new MemoryStream(compressed))
        using (var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress)) // ⚠️ Aquí el cambio
        using (var resultStream = new MemoryStream())
        {
            zlibStream.CopyTo(resultStream);
            byte[] decompressed = resultStream.ToArray();

            int nullIndex = Array.IndexOf(decompressed, (byte)0);
            byte[] contentBytes = new byte[decompressed.Length - nullIndex - 1];
            Array.Copy(decompressed, nullIndex + 1, contentBytes, 0, contentBytes.Length);

            Console.Write(Encoding.UTF8.GetString(contentBytes));
        }
    }
    public static void Init()
    {
        string gitDir = ".git";
        string objectsDir = Path.Combine(gitDir, "objects");
        string refsDir = Path.Combine(gitDir, "refs");

        Directory.CreateDirectory(gitDir);
        Directory.CreateDirectory(objectsDir);
        Directory.CreateDirectory(refsDir);

        // También se suele crear el archivo HEAD apuntando a la rama principal
        File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/main\n");
    }

}