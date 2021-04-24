using NAudio.Wave;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SCDEncoder
{
    class Program
    {
        private static readonly string TOOLS_PATH = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), "tools");
        private static readonly string SCD_HEADER_FILE = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), "scd/header.scd");

        private const string TMP_FOLDER_NAME = "tmp";
        private const string ADPCM_SUFFIX = "-adpcm";

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                var inputFile = args[0];
                var outputFolder = args.Length == 1 ? "output" : args[1];

                // Check for tools
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
                        ConvertFile(file, outputFolder);
                    }
                }
                else
                {
                    ConvertFile(args[0], outputFolder);
                }
            }
            else
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("SCDEncoder <file/dir> [<output dir>]");
            }
        }

        private static void ConvertFile(string inputFile, string outputFolder)
        {
            var tmpFolder = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), TMP_FOLDER_NAME);

            if (!Directory.Exists(tmpFolder))
                Directory.CreateDirectory(tmpFolder);
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var wavPCMPath = Path.Combine(tmpFolder, $"{Path.GetFileNameWithoutExtension(inputFile)}.wav");
            var wavADPCMPath = Path.Combine(tmpFolder, $"{Path.GetFileNameWithoutExtension(inputFile)}{ADPCM_SUFFIX}.wav");
            var scdPath = Path.Combine(tmpFolder, $"{Path.GetFileNameWithoutExtension(inputFile)}.scd");
            var outputFile = Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(inputFile)}.win32.scd");

            var p = new Process();

            if (Path.GetExtension(inputFile) == ".vag")
            {
                // Convert VAG to WAV
                p.StartInfo.FileName = $@"{TOOLS_PATH}/vgmstream/test.exe";
                p.StartInfo.Arguments = $"-o \"{wavPCMPath}\" \"{inputFile}\"";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardInput = false;
                p.Start();
                p.WaitForExit();
            }
            else
            {
                File.Copy(inputFile, wavPCMPath, true);
            }

            // Convert WAV PCM into WAV MS-ADPCM
            p.StartInfo.FileName = $@"{TOOLS_PATH}/adpcmencode/adpcmencode3.exe";
            p.StartInfo.Arguments = $"-b 32 \"{wavPCMPath}\" \"{wavADPCMPath}\"";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardInput = false;
            p.Start();
            p.WaitForExit();

            p.Close();

            CreateSCD(wavADPCMPath, scdPath);

            File.Copy(scdPath, outputFile, true);

            Console.WriteLine($"Converted {Path.GetFileName(inputFile)} into {Path.GetFileName(outputFile)}. (output: {outputFile})");

            Directory.Delete(tmpFolder, true);
        }

        private static void CreateSCD(string wavFilePath, string outputFile)
        {
            var streamName = Path.GetFileNameWithoutExtension(wavFilePath).Replace("-adpcm", "");
            var wavData = StripWavHeader(File.ReadAllBytes(wavFilePath));
            var waveFile = new WaveFileReader(wavFilePath);
            var dummyScd = File.ReadAllBytes(SCD_HEADER_FILE);

            using (var writer = new BinaryWriter(new MemoryStream(dummyScd)))
            {
                var totalFileSize = dummyScd.Length + wavData.Length;

                // Total file size
                writer.BaseStream.Position = 0x10;
                writer.Write(totalFileSize);

                // Stream name
                writer.BaseStream.Position = 0x150;
                writer.Write(Encoding.UTF8.GetBytes(streamName));

                // Audio data size
                writer.BaseStream.Position = 0x250;
                writer.Write(wavData.Length);

                // Channel count
                writer.BaseStream.Position = 0x254;
                writer.Write((uint)waveFile.WaveFormat.Channels);

                // Sample rate
                writer.BaseStream.Position = 0x258;
                writer.Write((uint)waveFile.WaveFormat.SampleRate);

                // Frame size / Block align
                writer.BaseStream.Position = 0x27C;
                writer.Write((short)waveFile.WaveFormat.BlockAlign);
            }

            waveFile.Close();

            var finalScd = new byte[dummyScd.Length + wavData.Length];
            Array.Copy(dummyScd, finalScd, dummyScd.Length);
            Array.Copy(wavData, 0, finalScd, dummyScd.Length, wavData.Length);

            File.WriteAllBytes(outputFile, finalScd);
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
