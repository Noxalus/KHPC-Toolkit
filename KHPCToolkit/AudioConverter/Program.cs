using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace AudioConverter
{
    class Program
    {
        private static readonly string RESOURCES_PATH = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), "resources");
        private static readonly List<string> SUPPORTED_EXTENSIONS = new List<string>() { ".vsb", ".vset", ".mdls", ".dat" };
        private static readonly List<string> EXCLUDED_FILES = new List<string>() { "voice001.vset", "demo.dat", "end.dat", "end2.dat", "opn.dat" };
        
        // Used to store the mapping between stream names and track index and make sure the output SCD has the track in the proper order
        private static Dictionary<string, Dictionary<int, int>> _streamsMapping = new Dictionary<string, Dictionary<int, int>>();


        static void Main(string[] args)
        {
            if (args.Length == 3)
            {
                // Parse JSON files in the resources folder to get streams index mapping
                foreach (var file in Directory.GetFiles(RESOURCES_PATH, "*.json"))
                {
                    var content = File.ReadAllText(file);
                    var data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<int, int>>>(content);

                    foreach (var key in data.Keys)
                    {
                        _streamsMapping[key] = data[key];
                    }
                }

                var ps2ExtractionFolder = args[0];
                var outputFolder = args[1];
                var pcExtractionFolder = args[2];

                FileAttributes attr = File.GetAttributes(ps2ExtractionFolder);

                if (attr.HasFlag(FileAttributes.Directory))
                {
                    var directory = new DirectoryInfo(ps2ExtractionFolder);
                    var allPS2files = Directory.GetFiles(ps2ExtractionFolder, "*.*", SearchOption.AllDirectories);
                    var allPCfiles = Directory.GetFiles(pcExtractionFolder, "*.*", SearchOption.AllDirectories);

                    foreach (var file in allPS2files)
                    {
                        var filename = Path.GetFileName(file);
                        var extension = Path.GetExtension(file);

                        if (SUPPORTED_EXTENSIONS.Contains(extension) && !EXCLUDED_FILES.Contains(filename))
                        {
                            Dictionary<int, int> mapping = null;

                            if (extension == ".vsb")
                            {
                                if (_streamsMapping.ContainsKey(filename))
                                {
                                    mapping = _streamsMapping[filename];
                                }
                                else
                                {
                                    Console.WriteLine($"Warning: no mapping found for file {filename}");
                                }
                            }

                            var originalSCDFolder = FindPCEquivalent(filename, allPCfiles);

                            if (string.IsNullOrEmpty(originalSCDFolder))
                            {
                                Console.WriteLine($"No equivalent file on PC for {filename}");
                                continue;
                            }

                            // Make sure to preserve hierarchy
                            var originalSCDRelativePath = originalSCDFolder.Replace($"{pcExtractionFolder}\\", "");
                            var scdOutputFolder = Path.Combine(outputFolder, originalSCDRelativePath);

                            if (SCDEncoder.SCDTools.ConvertFile(file, scdOutputFolder, originalSCDFolder, mapping))
                            {
                                // Copy original file in the "original" output folder
                                var originalFilePath = Path.Combine(outputFolder, originalSCDRelativePath.Replace("remastered", "original"));
                                var originalFileFolder = Directory.GetParent(originalFilePath).FullName;

                                if (!Directory.Exists(originalFileFolder))
                                    Directory.CreateDirectory(originalFileFolder);

                                File.Copy(file, originalFilePath, true);

                                //Console.WriteLine($"Converted {Path.GetFileName(file)}");
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"{ps2ExtractionFolder} is not a folder, make sure the first argument is a valid folder!");
                }
            }
            else
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("SCDEncoder <file/dir> [<output dir>] [<original scd file>]");
            }
        }

        private static string FindPCEquivalent(string ps2Filename, string[] pcFiles)
        {
            for (int i = 0; i < pcFiles.Length; i++)
            {
                var filename = pcFiles[i];
                var parentFolder = Directory.GetParent(filename);

                if (filename.Contains("remastered") && parentFolder.Name == ps2Filename)
                {
                    return parentFolder.FullName;
                }
            }

            return null;
        }
    }
}
