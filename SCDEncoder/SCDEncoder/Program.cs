using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xe.BinaryMapper;

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

                if (!File.Exists(@$"{TOOLS_PATH}/sox/sox.exe"))
                {
                    Console.WriteLine($"Please put sox.exe in the tools folder: {TOOLS_PATH}/sox");
                    Console.WriteLine("You can find it here: https://sourceforge.net/projects/sox/files/sox/");
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
            var vagOutputFolder = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), VAG_OUT_FOLDER_NAME);

            if (!Directory.Exists(tmpFolder))
                Directory.CreateDirectory(tmpFolder);
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            if (Directory.Exists(vagOutputFolder))
                Directory.Delete(vagOutputFolder, true);

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

                vagFiles.AddRange(Directory.GetFiles(vagOutputFolder, "*.vag"));
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
                    var currentWavPCM48Path = Path.Combine(tmpFolder, $"{Path.GetFileNameWithoutExtension(vagFile)}-pcm-48.wav");

                    p.StartInfo.FileName = $@"{TOOLS_PATH}/vgmstream/test.exe";
                    p.StartInfo.Arguments = $"-o \"{currentWavPCMPath}\" \"{vagFile}\"";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardInput = false;
                    p.Start();
                    p.WaitForExit();

                    // Convert WAV PCM (any sample rate) to WAV PCM with a sample rate of 48kHz
                    p.StartInfo.FileName = $@"{TOOLS_PATH}/sox/sox.exe";
                    p.StartInfo.Arguments = $"\"{currentWavPCMPath}\" --rate 48000 \"{currentWavPCM48Path}\"";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardInput = false;
                    p.Start();
                    p.WaitForExit();

                    wavPCMFiles.Add(currentWavPCM48Path);
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
            // Out folder is created by the tool that extract VAG from VSB files
            Directory.Delete(vagOutputFolder, true);
#endif
        }

        private static void CreateSCD(List<string> wavFiles, string outputFile, string originalScd = null)
        {
            if (!string.IsNullOrEmpty(originalScd))
            {
                var scd = new SCD(File.OpenRead(originalScd));

                if (scd.StreamsData.Count != wavFiles.Count)
                {
                    throw new Exception(
                        "The streams count in the original SCD and the the WAV count doesn't match, " +
                        "please make sure the original SCD you specified correspond to the VSB/WAVs you specified."
                    );
                }

                var wavesContent = new List<byte[]>();

                foreach (var wavFile in wavFiles)
                {
                    var wavContent = Helpers.StripWavHeader(File.ReadAllBytes(wavFile));
                    Helpers.Align(ref wavContent, 0x10);

                    wavesContent.Add(wavContent);
                }

                using (var writer = new MemoryStream())
                {
                    // Write SCD Header
                    var scdHeader = new SCD.SCDHeader()
                    {
                        FileVersion = scd.Header.FileVersion,
                        BigEndianFlag = scd.Header.BigEndianFlag,
                        MagicCode = scd.Header.MagicCode,
                        SSCFVersion = scd.Header.SSCFVersion,
                        Padding = scd.Header.Padding,
                        HeaderSize = scd.Header.HeaderSize,
                        // TODO: Fix this, it should be new total file size - table 0 offset position (which correspond to the header size?)
                        TotalFileSize = (uint)wavesContent.Sum(content => content.Length)
                    };

                    BinaryMapping.WriteObject(writer, scdHeader);

                    // Write Table offsets header
                    var scdTableOffsetsHeader = new SCD.SCDTableOffsetHeader()
                    {
                        Table0ElementCount = scd.TableOffsetHeader.Table0ElementCount,
                        Table0Offset = scd.TableOffsetHeader.Table0Offset,
                        Table1ElementCount = scd.TableOffsetHeader.Table1ElementCount,
                        Table1Offset = scd.TableOffsetHeader.Table1Offset,
                        Table2ElementCount = scd.TableOffsetHeader.Table2ElementCount,
                        Table2Offset = scd.TableOffsetHeader.Table2Offset,
                        Unk06 = scd.TableOffsetHeader.Unk06,
                        Unk14 = scd.TableOffsetHeader.Unk14,
                        Unk18 = scd.TableOffsetHeader.Unk18,
                        Padding = scd.TableOffsetHeader.Padding,
                    };

                    BinaryMapping.WriteObject(writer, scdTableOffsetsHeader);

                    // Write original data from current position to the table 1 offset (before to write all streams offets)
                    var data = scd.Data.SubArray((int)writer.Position, (int)(scdTableOffsetsHeader.Table1Offset - writer.Position));
                    writer.Write(data);

                    // Write stream entries offset
                    var streamOffset = (uint)scd.StreamsData[0].Offset;
                    var streamHeaderSize = 32;
                    var streamsOffsets = new List<uint>();

                    for (int i = 0; i < wavesContent.Count; i++)
                    {
                        var wavContent = wavesContent[i];
                        writer.Write(BitConverter.GetBytes(streamOffset));

                        streamsOffsets.Add(streamOffset);

                        streamOffset += (uint)(wavContent.Length + (streamHeaderSize + scd.StreamsData[i].ExtraData.Length));
                    }

                    // Write the original data from current stream position to the start of the first stream header
                    data = scd.Data.SubArray((int)writer.Position, (int)(streamsOffsets[0] - writer.Position));
                    writer.Write(data);

                    // Write data for each stream entry
                    for (int i = 0; i < scd.StreamsData.Count; i++)
                    {
                        var streamData = scd.StreamsData[i];
                        var wavFile = wavFiles[i];
                        var wavContent = wavesContent[i];

                        var waveFileInfo = new WaveFileReader(wavFile);

                        var newStreamHeader = new SCD.StreamHeader
                        {
                            AuxChunkCount = streamData.Header.AuxChunkCount,
                            ChannelCount = (uint)waveFileInfo.WaveFormat.Channels,
                            Codec = streamData.Header.Codec,
                            ExtraDataSize = streamData.Header.ExtraDataSize,
                            LoopStart = streamData.Header.LoopStart,
                            LoopEnd = streamData.Header.LoopEnd,
                            SampleRate = (uint)waveFileInfo.WaveFormat.SampleRate,
                            StreamSize = (uint)wavContent.Length
                        };

                        // Write stream header
                        BinaryMapping.WriteObject(writer, newStreamHeader);
                        // Write stream extra data
                        writer.Write(streamData.ExtraData);
                        // Write stream audio data
                        writer.Write(wavContent);

                        waveFileInfo.Close();
                    }

                    File.WriteAllBytes(outputFile, writer.ReadAllBytes());

                    // Check the new SCD is correct
                    var newScd = new SCD(File.OpenRead(outputFile));
                }
            }
            else
            {
                if (wavFiles.Count > 1)
                {
                    throw new Exception("You need to pass an original SCD file to create a new SCD from multiple WAV files.");
                }

                var scdHeader = File.ReadAllBytes(DUMMY_SCD_HEADER_FILE);
            }
        }
    }
}
