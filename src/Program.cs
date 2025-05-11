using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using codecrafters_git;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "init")
        {
            GitCommands.Init();
        }
        else if (args.Length == 3 && args[0] == "cat-file" && args[1] == "-p")
        {
            GitCommands.CatFile(args[2]);
        }
        else
        {
            Console.Error.WriteLine("Comando no reconocido.");
            Environment.Exit(1);
        }
    }
