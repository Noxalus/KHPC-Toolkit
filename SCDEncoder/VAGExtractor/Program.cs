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
                var keepName = args.Length > 2;

                if (!Directory.Exists(outputFolder))
                    Directory.CreateDirectory(outputFolder);

                var inputData = File.ReadAllBytes(inputFile);
                var inputStream = File.OpenRead(inputFile);
                var foundOffsets = SearchPatterns(inputData, "VAGp");

                for (int i = 0; i < foundOffsets.Count; i++)
                {
                    int offset = foundOffsets[i];
                    inputStream.Seek(offset, SeekOrigin.Begin);

                    var vag = new VAG(inputStream);

                    Console.WriteLine($"{i} => {vag.Name}");
                    
                    vag.Export(outputFolder, keepName ? null : i.ToString());
                }
            }
            else
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("VAGExtractor <file/dir> [<output dir>]");
            }
        }
        
        private static List<int> SearchPatterns(byte[] data, string stringPattern)
        {
            var foundOffsets = new List<int>();

            var pattern = Encoding.ASCII.GetBytes(stringPattern);
            int patternLength = pattern.Length;
            int totalLength = data.Length;
            byte firstMatchByte = pattern[0];
            for (int i = 0; i < totalLength; i++)
            {
                if (firstMatchByte == data[i] && totalLength - i >= patternLength)
                {
                    byte[] match = new byte[patternLength];
                    Array.Copy(data, i, match, 0, patternLength);
                    if (match.SequenceEqual<byte>(pattern))
                    {
                        foundOffsets.Add(i);
                    }
                }
            }

            return foundOffsets;
        }
    }
}
