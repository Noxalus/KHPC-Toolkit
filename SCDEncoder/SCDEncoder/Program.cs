using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SCDEncoder
{
    class Program
    {
        private static readonly string TOOLS_PATH = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), "tools");
        private static readonly string DUMMY_SCD_HEADER_FILE = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), "scd/header.scd");

        private const string TMP_FOLDER_NAME = "tmp";
        private const string VAG_OUT_FOLDER_NAME = "out";
        private const string ADPCM_SUFFIX = "-adpcm";

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                var inputFile = args[0];
                var outputFolder = args.Length > 1 ? args[1] : "output";
                var originalScdFile = args.Length > 2 ? args[2] : null;

                // Check for tools

                if (!File.Exists(@$"{TOOLS_PATH}/iopvoiceext/IOPVOICEExt.exe"))
                {
                    Console.WriteLine($"Please put IOPVOICEExt.exe in the tools folder: {TOOLS_PATH}/iopvoiceext");
                    return;
                }

                if (!File.Exists(@$"{TOOLS_PATH}/vgmstream/test.exe"))
                {
                    Console.WriteLine($"Please put test.exe in the tools folder: {TOOLS_PATH}/vgmstream");
                    Console.WriteLine("You can find it here: https://vgmstream.org/downloads");
                    return;
                }

                if (!File.Exists(@$"{TOOLS_PATH}/adpcmencode/adpcmencode3.exe"))
                {
                    Console.WriteLine($"Please put adpcmencode3.exe in the tools folder: {TOOLS_PATH}/adpcmencode");
                    Console.WriteLine("You can find it in the Windows 10 SDK: https://developer.microsoft.com/fr-fr/windows/downloads/windows-10-sdk/");
                    return;
                }

                FileAttributes attr = File.GetAttributes(args[0]);
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    var directory = new DirectoryInfo(args[0]);
                    outputFolder = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), outputFolder, directory.Name);
                    foreach (var file in Directory.GetFiles(args[0], "*"))
                    {
                        ConvertFile(file, outputFolder, originalScdFile);
                    }
                }
                else
                {
                    ConvertFile(args[0], outputFolder, originalScdFile);
                }
            }
            else
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("SCDEncoder <file/dir> [<output dir>]");
            }
        }

        private static void ConvertFile(string inputFile, string outputFolder, string originalScd = null)
        {
            var tmpFolder = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), TMP_FOLDER_NAME);
            var vagOutFolder = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), VAG_OUT_FOLDER_NAME);

            if (!Directory.Exists(tmpFolder))
                Directory.CreateDirectory(tmpFolder);
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            if (Directory.Exists(vagOutFolder))
                Directory.Delete(vagOutFolder, true);

            var wavPCMPath = Path.Combine(tmpFolder, $"{Path.GetFileNameWithoutExtension(inputFile)}.wav");
            var wavADPCMPath = Path.Combine(tmpFolder, $"{Path.GetFileNameWithoutExtension(inputFile)}{ADPCM_SUFFIX}.wav");
            var scdPath = Path.Combine(tmpFolder, $"{Path.GetFileNameWithoutExtension(inputFile)}.scd");
            var outputFile = Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(inputFile)}.win32.scd");

            var p = new Process();

            var vagFiles = new List<string>();
            var wavPCMFiles = new List<string>();
            var wavADPCMFiles = new List<string>();

            if (Path.GetExtension(inputFile) == ".vsb")
            {
                // Convert VSB into VAG files
                p.StartInfo.FileName = $@"{TOOLS_PATH}/iopvoiceext/IOPVOICEExt.exe";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardInput = true;
                p.Start();

                p.StandardInput.WriteLine(inputFile);
                p.StandardInput.WriteLine(" ");

                p.WaitForExit();

                vagFiles.AddRange(Directory.GetFiles(vagOutFolder, "*.vag"));
            }
            else if (Path.GetExtension(inputFile) == ".vag")
            {
                vagFiles.Add(inputFile);
            }
            else
            {
                File.Copy(inputFile, wavPCMPath, true);
            }

            // Convert VAG to WAV
            if (vagFiles.Count > 0)
            {
                foreach (var vagFile in vagFiles)
                {
                    var currentWavPCMPath = Path.Combine(tmpFolder, $"{Path.GetFileNameWithoutExtension(vagFile)}-pcm.wav");

                    p.StartInfo.FileName = $@"{TOOLS_PATH}/vgmstream/test.exe";
                    p.StartInfo.Arguments = $"-o \"{currentWavPCMPath}\" \"{vagFile}\"";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardInput = false;
                    p.Start();
                    p.WaitForExit();

                    wavPCMFiles.Add(currentWavPCMPath);
                }
            }
            else
            {
                wavPCMFiles.Add(wavPCMPath);
            }

            foreach (var wavPCMFile in wavPCMFiles)
            {
                var currentWavADPCMPath = Path.Combine(tmpFolder, $"{Path.GetFileNameWithoutExtension(wavPCMFile)}{ADPCM_SUFFIX}.wav");

                // Convert WAV PCM into WAV MS-ADPCM
                p.StartInfo.FileName = $@"{TOOLS_PATH}/adpcmencode/adpcmencode3.exe";
                p.StartInfo.Arguments = $"-b 32 \"{wavPCMFile}\" \"{currentWavADPCMPath}\"";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardInput = false;
                p.Start();
                p.WaitForExit();

                wavADPCMFiles.Add(currentWavADPCMPath);
            }

            p.Close();

            CreateSCD(wavADPCMFiles, scdPath, originalScd);

            File.Copy(scdPath, outputFile, true);

            Console.WriteLine($"Converted {Path.GetFileName(inputFile)} into {Path.GetFileName(outputFile)}. (output: {outputFile})");

#if RELEASE
            Directory.Delete(tmpFolder, true);
#endif
        }

        private struct Wav
        {
            public int SampleRate;
            public int BlockAlign;
            public int Channels;
            public string Name;
            public byte[] Data;
        }

        private static void CreateSCD(List<string> wavFiles, string outputFile, string originalScd = null)
        {
            var wavData = new List<Wav>();

            foreach (var wavFile in wavFiles)
            {
                var wavContent = StripWavHeader(File.ReadAllBytes(wavFile));
                var waveFileInfo = new WaveFileReader(wavFile);

                var wavDataEntry = new Wav()
                {
                    SampleRate = waveFileInfo.WaveFormat.SampleRate,
                    BlockAlign = waveFileInfo.BlockAlign,
                    Channels = waveFileInfo.WaveFormat.Channels,
                    Name = Path.GetFileNameWithoutExtension(wavFile).Replace(ADPCM_SUFFIX, ""),
                    Data = wavContent,
                };

                waveFileInfo.Close();

                wavData.Add(wavDataEntry);
            }

            var scdHeader = string.IsNullOrEmpty(originalScd) ? File.ReadAllBytes(DUMMY_SCD_HEADER_FILE) : ExtractScdHeader(originalScd);
            var totalWavDataLenght = wavData.Sum(wav => wav.Data.Length);
            var finalScd = new byte[scdHeader.Length + totalWavDataLenght];
            Array.Copy(scdHeader, finalScd, scdHeader.Length);

            using (var writer = new BinaryWriter(new MemoryStream(scdHeader)))
            {
                foreach (var wav in wavData)
                {
                    var totalFileSize = scdHeader.Length + wav.Data.Length;

                    // Total file size
                    writer.BaseStream.Position = 0x10;
                    writer.Write(totalFileSize);

                    // Stream name
                    //writer.BaseStream.Position = 0x150;
                    //writer.Write(Encoding.UTF8.GetBytes(streamName));

                    // Audio data size
                    writer.BaseStream.Position = 0x250;
                    writer.Write(wav.Data.Length);

                    // Channel count
                    writer.BaseStream.Position = 0x254;
                    writer.Write((uint)wav.Channels);

                    // Sample rate
                    writer.BaseStream.Position = 0x258;
                    writer.Write((uint)wav.SampleRate);

                    // Frame size / Block align
                    writer.BaseStream.Position = 0x27C;
                    writer.Write((short)wav.BlockAlign);

                    // TODO: Update table offsets!

                    // TODO: This is not correct
                    Array.Copy(wav.Data, 0, finalScd, scdHeader.Length, totalWavDataLenght);
                }
            }

            File.WriteAllBytes(outputFile, finalScd);
        }

        private static byte[] ExtractScdHeader(string originalScd)
        {
            // TODO: Implement this
            return new byte[0];
        }

        private static byte[] StripWavHeader(byte[] wavData)
        {
            // Find wave header size
            var pattern = Encoding.ASCII.GetBytes("data");
            var wavHeaderOffset = SearchBytePattern(wavData, pattern) + pattern.Length;

            // Add int32 for the size
            wavHeaderOffset += 0x04;

            // Strip wav header
            wavData = wavData.Skip(wavHeaderOffset).ToArray();

            return wavData;
        }

        private static int SearchBytePattern(byte[] data, byte[] pattern)
        {
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
                        return i;
                    }
                }
            }

            return -1;
        }
    }
}
