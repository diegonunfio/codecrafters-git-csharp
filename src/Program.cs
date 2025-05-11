using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace codecrafters_git
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 3 && args[0] == "hash-object" && args[1] == "-w")
            {
                string fileName = args[2];
                string content = File.ReadAllText(fileName); // Asumimos contenido de texto plano
                byte[] contentBytes = Encoding.UTF8.GetBytes(content);
                string header = $"blob {contentBytes.Length}\0";
                byte[] headerBytes = Encoding.UTF8.GetBytes(header);

                // Concatenar los datos del encabezado y el contenido
                byte[] combinedBytes = new byte[headerBytes.Length + contentBytes.Length];
                Buffer.BlockCopy(headerBytes, 0, combinedBytes, 0, headerBytes.Length);
                Buffer.BlockCopy(contentBytes, 0, combinedBytes, headerBytes.Length, contentBytes.Length);

                // Calcular el SHA-1 sobre el contenido completo (encabezado + contenido)
                string hash = BitConverter.ToString(SHA1.Create().ComputeHash(combinedBytes)).Replace("-", "").ToLower();
                Console.WriteLine(hash); // Imprimir el hash en consola

                // Ruta donde se almacenará el objeto
                string objectPath = $".git/objects/{hash.Substring(0, 2)}";
                Directory.CreateDirectory(objectPath); // Crear el directorio si no existe

                string fullPath = Path.Combine(objectPath, hash.Substring(2));

                // Comprimir los datos con Zlib (añadir el encabezado Zlib)
                byte[] compressedData;
                using (var deflateStream = new MemoryStream())
                {
                    // Encabezado Zlib: 0x78 0x9C
                    deflateStream.WriteByte(0x78);
                    deflateStream.WriteByte(0x9C);

                    // Usar DeflateStream para comprimir los datos
                    using (var compressor = new DeflateStream(deflateStream, CompressionLevel.Optimal, leaveOpen: true))
                    {
                        compressor.Write(combinedBytes, 0, combinedBytes.Length);
                    }

                    compressedData = deflateStream.ToArray(); // Obtener los datos comprimidos
                }

                // Guardar los datos comprimidos en el archivo de objetos Git
                File.WriteAllBytes(fullPath, compressedData);
            }
            else
            {
                Console.Error.WriteLine("Comando no reconocido.");
                Environment.Exit(1);
            }
        }
    }
}
