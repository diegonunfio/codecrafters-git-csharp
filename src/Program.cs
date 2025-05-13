using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;


class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Please provide a command.");
            return;
        }

        string command = args[0];
        if (command == "init")
        {
            Directory.CreateDirectory(".git");
            Directory.CreateDirectory(".git/objects");
            Directory.CreateDirectory(".git/refs");
            File.WriteAllText(".git/HEAD", "ref: refs/heads/main\n");
            Console.WriteLine("Initialized git directory");
        }
        else if (command == "cat-file")
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine(
                    "cat-file command requires 3 arguments: git cat-file <argument> <sha1-hash>");
                return;
            }

            string argument = args[1];
            string hash = args[2];
            if (argument != "-p")
            {
                Console.Error.WriteLine(
                    "only the -p argument is supported for the cat-file command");
                return;
            }

            string objectSubDir = hash[..2];
            string objectFile = hash[2..];
            if (!Directory.Exists($".git/objects/{objectSubDir}") ||
                !File.Exists($".git/objects/{objectSubDir}/{objectFile}"))
            {
                Console.Error.WriteLine("invalid sha1 hash");
                return;
            }

            byte[] compressedFileContents =
                File.ReadAllBytes($".git/objects/{objectSubDir}/{objectFile}");
            byte[] decompressedData = compressedFileContents.DecompressZlibData();
            string fileContents = Encoding.UTF8.GetString(decompressedData);
            string[] nullSplitContents = fileContents.Split("\0");
            if (nullSplitContents.Length != 2)
            {
                Console.Error.WriteLine(
                    "corrupted git repository - improper object file format");
                return;
            }

            string blobContents = nullSplitContents[1];
            Console.Write(blobContents);
        }
        else if (command == "hash-object")
        {
            if (args.Length < 2 || args.Length > 3)
            {
                Console.Error.WriteLine("usage: git hash-object <args> <filename>");
            }

            bool shouldWriteToFile = false;
            string filename = args[1];
            if (args[1] == "-w")
            {
                shouldWriteToFile = true;
                filename = args[2];
            }

            byte[] fileContent = File.ReadAllBytes(filename);
            byte[] header = Encoding.UTF8.GetBytes($"blob {fileContent.Length}\0");
            byte[] output = [..header.Concat(fileContent)];
            string fileHash = output.GetSha1Hash();
            Console.WriteLine(fileHash);
            if (shouldWriteToFile)
            {
                string objectSubDir = fileHash[..2];
                string objectFileName = fileHash[2..];
                Directory.CreateDirectory($".git/objects/{objectSubDir}");
                FileStream fs =
                    File.Create($".git/objects/{objectSubDir}/{objectFileName}");
                output.CompressZlibData(fs);
            }
        }
        else if (command == "ls-tree")
        {
            string shaHash = args[2];
            string objectSubDir = shaHash[..2];
            string objectFileName = shaHash[2..];
            byte[] compressedFileContent =
                File.ReadAllBytes($".git/objects/{objectSubDir}/{objectFileName}");
            string stringFileContent =
                Encoding.UTF8.GetString(compressedFileContent.DecompressZlibData());
            string[] nullSplitFileContent = stringFileContent.Split("\0");
            IEnumerable<string> filenames =
                nullSplitFileContent.Skip(1).Select(s => s.Split(" ").Last()).SkipLast(1);
            foreach (string? filename in filenames)
            {
                Console.WriteLine(filename);
            }
        }
        else if (command == "write-tree")
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            (string hash, byte[] _) = WriteTreeObject(currentDirectory);
            Console.WriteLine(hash);

            (string hash, byte[] hashWithoutHex) WriteTreeObject(string directory)
            {
                IEnumerable<string> directories = Directory.GetDirectories(directory)
                    .Where(dir => !dir.EndsWith(Path.DirectorySeparatorChar + ".git"));
                string[] files = Directory.GetFiles(directory);
                IOrderedEnumerable<string> entries = directories.Concat(files).Order();
                using MemoryStream treeObjectBody = new();
                foreach (string? entry in entries)
                {
                    byte[] rowBytes;
                    if (Directory.Exists(entry))
                    {
                        (string _, byte[] treeHashRow) = WriteTreeObject(entry);
                        string treeObjectRow = $"40000 {Path.GetFileName(entry)}\0";
                        byte[] treeObjectRowBytes = Encoding.ASCII.GetBytes(treeObjectRow);
                        rowBytes = [..treeObjectRowBytes, ..treeHashRow];
                    }
                    else
                    {
                        (string _, byte[] blobHashRow) = WriteBlobObject(entry);
                        string blobObjectRow = $"100644 {Path.GetFileName(entry)}\0";
                        byte[] blobObjectRowBytes = Encoding.ASCII.GetBytes(blobObjectRow);
                        rowBytes = [..blobObjectRowBytes, ..blobHashRow];
                    }

                    treeObjectBody.Write(rowBytes, 0, rowBytes.Length);
                }

                string treeObjectHeader = $"tree {treeObjectBody.Length}\0";
                byte[] treeObjectHeaderBytes =
                    Encoding.ASCII.GetBytes(treeObjectHeader);
                byte[] treeObjectBytes =
                    [..treeObjectHeaderBytes, ..treeObjectBody.ToArray()];
                string treeHash = treeObjectBytes.GetSha1Hash();
                string treeObjectPath =
                    Path.Combine(".git", "objects", treeHash[..2], treeHash[2..]);
                Directory.CreateDirectory(Path.GetDirectoryName(treeObjectPath)!);
                using Stream fs = File.Create(treeObjectPath);
                treeObjectBytes.CompressZlibData(fs);
                return (treeHash, SHA1.HashData(treeObjectBytes));
            }

            (string hash, byte[] hashWithoutHex) WriteBlobObject(string filePath)
            {
                byte[] fileContent = File.ReadAllBytes(filePath);
                string blobHeader = $"blob {fileContent.Length}\0";
                byte[] blobHeaderBytes = Encoding.ASCII.GetBytes(blobHeader);
                byte[] blobObjectBytes = [..blobHeaderBytes, ..fileContent];
                string blobHash = blobObjectBytes.GetSha1Hash();
                string blobObjectPath =
                    Path.Combine(".git", "objects", blobHash[..2], blobHash[2..]);
                Directory.CreateDirectory(Path.GetDirectoryName(blobObjectPath)!);
                using FileStream fs = File.Create(blobObjectPath);
                blobObjectBytes.CompressZlibData(fs);
                return (blobHash, SHA1.HashData(blobObjectBytes));
            }
        }
        else
        {
            throw new ArgumentException($"Unknown command {command}");
        }
        
    }
    
}