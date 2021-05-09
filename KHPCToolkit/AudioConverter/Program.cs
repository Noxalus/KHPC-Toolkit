using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static List<string>  _voiceFiles = new List<string>();

        private const string REMASTERED_FOLDER = "remastered";
        private const string ORIGINAL_FOLDER = "original";

        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("SCDEncoder <file/dir> [<output dir>] [<original scd file>]");
                return;
            }

            // Get voice files
            foreach (var file in Directory.GetFiles(RESOURCES_PATH, "*_voice_files.json"))
            {
                var content = File.ReadAllText(file);
                var data = JsonConvert.DeserializeObject<List<string>>(content);

                _voiceFiles.AddRange(data);
            }

            // Parse JSON files in the resources folder to get streams index mapping
            foreach (var file in Directory.GetFiles(RESOURCES_PATH, "*_mapping.json"))
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
                var allPS2files = Directory.GetFiles(ps2ExtractionFolder, "*.*", SearchOption.AllDirectories);
                var allPCfiles = Directory.GetFiles(pcExtractionFolder, "*.*", SearchOption.AllDirectories);

                var performanceWatch = new Stopwatch();
                performanceWatch.Start();

                var convertedFilesCount = 0;

                var validFiles = new List<string>();

                foreach (var file in allPS2files)
                {
                    var filename = Path.GetFileName(file);

                    //if (!_voiceFiles.Contains(filename))
                    //{
                    //    continue;
                    //}

                    var extension = Path.GetExtension(file);

                    if (SUPPORTED_EXTENSIONS.Contains(extension) && !EXCLUDED_FILES.Contains(filename))
                    {
                        Dictionary<int, int> mapping = null;

                        // VSB files contain multiple tracks and must combine them in a single SCD with a specific order
                        // The streams mapping file is used to make sure the order correspond to what we have on PC
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

                        var scdConvertionPerformanceWatch = new Stopwatch();
                        scdConvertionPerformanceWatch.Start();

                        Console.WriteLine($"File {filename}");

                        if (SCDEncoder.SCDTools.ConvertFile(file, scdOutputFolder, originalSCDFolder, mapping))
                        {
                            if (originalSCDFolder.Contains(REMASTERED_FOLDER))
                            {
                                // Copy original file in the "original" output folder
                                var originalFilePath = Path.Combine(outputFolder, originalSCDRelativePath.Replace(REMASTERED_FOLDER, ORIGINAL_FOLDER));
                                var originalFileFolder = Directory.GetParent(originalFilePath).FullName;

                                if (!Directory.Exists(originalFileFolder))
                                    Directory.CreateDirectory(originalFileFolder);

                                File.Copy(file, originalFilePath, true);
                            }

                            scdConvertionPerformanceWatch.Stop();
                            Console.WriteLine($"Done in {scdConvertionPerformanceWatch.ElapsedMilliseconds}ms");

                            convertedFilesCount++;

                            validFiles.Add(filename);
                        }
                    }
                }

                var validFilesPath = Path.Combine(outputFolder, "validFiles.txt");
                File.WriteAllText(validFilesPath, String.Join(Environment.NewLine, validFiles)) ;

                performanceWatch.Stop();
                Console.WriteLine($"Converted {convertedFilesCount} files in {performanceWatch.ElapsedMilliseconds}ms");
            }
            else
            {
                Console.WriteLine($"{ps2ExtractionFolder} is not a folder, make sure the first argument is a valid folder!");
            }
        }

        private static string FindPCEquivalent(string ps2Filename, string[] pcFiles)
        {
            for (int i = 0; i < pcFiles.Length; i++)
            {
                var filename = pcFiles[i];

                if (filename.Contains(REMASTERED_FOLDER))
                {
                    var parentFolder = Directory.GetParent(filename);
                    
                    if (parentFolder.Name == ps2Filename)
                    {
                        return parentFolder.FullName;
                    }
                }
                else if (filename.Contains(ORIGINAL_FOLDER))
                {
                    var extension = Path.GetExtension(filename);

                    if (extension == ".scd")
                    {
                        if (Path.GetFileNameWithoutExtension(filename) == $"{Path.GetFileNameWithoutExtension(ps2Filename)}.win32")
                        {
                            return filename;
                        }
                    }
                }
            }

            return null;
        }
    }
}
