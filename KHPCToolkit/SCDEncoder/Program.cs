using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xe.BinaryMapper;
using Newtonsoft.Json;
using VAGExtractor;

namespace SCDEncoder
{
    class Program
    {
        private static void Main(string[] args)
        {
            if (!SCDTools.CheckTools())
            {
                return;
            }

            if (args.Length == 3)
            {
                var input = args[0];
                var outputFolder = args[1];
                var originalScdFile = args[2];

                FileAttributes attr = File.GetAttributes(args[0]);
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    var directory = new DirectoryInfo(args[0]);
                    outputFolder = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), outputFolder, directory.Name);

                    string[] allfiles = Directory.GetFiles(input, "*.*", SearchOption.AllDirectories);

                    foreach (var file in allfiles)
                    {
                        SCDTools.ConvertFile(file, outputFolder, originalScdFile);
                    }
                }
                else
                {
                    SCDTools.ConvertFile(input, outputFolder, originalScdFile);
                }
            }
            else
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("SCDEncoder <file/dir> [<output dir>] [<original scd file>]");
            }
        }
    }
}
