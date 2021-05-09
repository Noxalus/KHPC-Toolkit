using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VAGExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                var inputFile = args[0];
                var outputFolder = args.Length > 1 ? args[1] : "output";
                var keepName = args.Length > 2 ? int.Parse(args[2]) : 0;

                VAGTools.ExtractVAGFiles(inputFile, outputFolder, keepName);
            }
            else
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("VAGExtractor <file/dir> [<output dir>]");
            }
        }
    }
}
