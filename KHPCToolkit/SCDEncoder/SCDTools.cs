using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xe.BinaryMapper;

namespace SCDEncoder
{
    public class SCDTools
    {
        private static readonly string TOOLS_PATH = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), "tools");
        private static readonly string TMP_FOLDER = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), "tmp");

        private const bool USE_SOX = true;

        public static bool CheckTools()
        {
            if (!File.Exists(@$"{TOOLS_PATH}/vgmstream/test.exe"))
            {
                Console.WriteLine($"Please put test.exe in the tools folder: {TOOLS_PATH}/vgmstream");
                Console.WriteLine("You can find it here: https://vgmstream.org/downloads");
                return false;
            }

            if (!File.Exists(@$"{TOOLS_PATH}/adpcmencode/adpcmencode3.exe"))
            {
                Console.WriteLine($"Please put adpcmencode3.exe in the tools folder: {TOOLS_PATH}/adpcmencode");
                Console.WriteLine("You can find it in the Windows 10 SDK: https://developer.microsoft.com/fr-fr/windows/downloads/windows-10-sdk/");
                return false;
            }

            if (!File.Exists(@$"{TOOLS_PATH}/sox/sox.exe"))
            {
                Console.WriteLine($"Please put sox.exe in the tools folder: {TOOLS_PATH}/sox");
                Console.WriteLine("You can find it here: https://sourceforge.net/projects/sox/files/sox/");
                return false;
            }

            if (!File.Exists(@$"{TOOLS_PATH}/oggenc/oggenc2.exe"))
            {
                Console.WriteLine($"Please put oggenc2.exe in the tools folder: {TOOLS_PATH}/oggenc");
                Console.WriteLine("You can find it here: https://www.rarewares.org/ogg-oggenc.php");
                return false;
            }

            return true;
        }

        public static bool ConvertFile(string inputFile, string outputFolder, string originalSCDFolder, Dictionary<int, int> mapping = null)
        {
            var filename = Path.GetFileName(inputFile);
            var filenameWithoutExtension = Path.GetFileNameWithoutExtension(inputFile);
            var fileExtension = Path.GetExtension(inputFile);

            if (Directory.Exists(TMP_FOLDER))
                Directory.Delete(TMP_FOLDER, true);

            Directory.CreateDirectory(TMP_FOLDER);

            var vagFiles = VAGExtractor.VAGTools.ExtractVAGFiles(inputFile, TMP_FOLDER, 1, true);

            if (vagFiles.Count == 0)
            {
                //Console.WriteLine("No VAG files found...");
                return false;
            }

            Console.WriteLine($"Convert {filename}");

            foreach (var file in vagFiles)
            {
                Console.WriteLine($"\t{Path.GetFileName(file)}");
            }


            var wavPCMPath = Path.Combine(TMP_FOLDER, $"{filenameWithoutExtension}.wav");

            var p = new Process();

            var wavPCMFiles = new List<string>();

            // Convert VAG to WAV
            if (vagFiles.Count > 0)
            {
                foreach (var vagFile in vagFiles)
                {
                    var currentWavFile = Path.Combine(TMP_FOLDER, $"{Path.GetFileNameWithoutExtension(vagFile)}.wav");

                    p.StartInfo.FileName = $@"{TOOLS_PATH}/vgmstream/test.exe";
                    p.StartInfo.Arguments = $"-o \"{currentWavFile}\" \"{vagFile}\"";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardInput = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.Start();
                    p.WaitForExit();


                    // Convert WAV PCM (any sample rate) to WAV PCM with a sample rate of 48kHz
                    if (USE_SOX)
                    {
                        var increaseVolume = fileExtension == ".vset" ? "vol 10 dB" : "";

                        var currentWav48kHzFile  = Path.Combine(TMP_FOLDER, $"{Path.GetFileNameWithoutExtension(vagFile)}-48.wav");
                        p.StartInfo.FileName = $@"{TOOLS_PATH}/sox/sox.exe";
                        p.StartInfo.Arguments = $" \"{currentWavFile}\" --rate 48000 \"{currentWav48kHzFile}\" {increaseVolume}";
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.RedirectStandardInput = false;
                        p.StartInfo.RedirectStandardOutput = true;
                        p.Start();
                        p.WaitForExit();

                        File.Copy(currentWav48kHzFile, currentWavFile, true);
                    }

                    wavPCMFiles.Add(currentWavFile);
                }
            }
            else
            {
                wavPCMFiles.Add(wavPCMPath);
            }

            p.Close();

            // Output multiple or a single SCD file?
            var outputFile = outputFolder;
            var outputSCDFiles = new List<string>();

            FileAttributes attr = File.GetAttributes(originalSCDFolder);
            bool isFolder = attr.HasFlag(FileAttributes.Directory);

            if (isFolder)
            {
                foreach (var file in Directory.GetFiles(originalSCDFolder, "*.scd", SearchOption.TopDirectoryOnly))
                {
                    outputSCDFiles.Add(file);
                }
            }
            else
            {
                outputSCDFiles.Add(originalSCDFolder);
            }

            if (outputSCDFiles.Count == 1)
            {
                var scdPath = Path.Combine(TMP_FOLDER, $"{Path.GetFileNameWithoutExtension(outputSCDFiles[0])}.scd");
                outputFile = Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(outputSCDFiles[0])}.scd");

                if (CreateSCD(wavPCMFiles, scdPath, outputSCDFiles[0], mapping) == null)
                {
                    return false;
                }

                var scdOutputFolder = Directory.GetParent(outputFile).FullName;

                if (!Directory.Exists(scdOutputFolder))
                    Directory.CreateDirectory(scdOutputFolder);

                File.Copy(scdPath, outputFile, true);
            }
            else
            {
                for (int i = 0; i < outputSCDFiles.Count; i++)
                {
                    var originalSCDFile = outputSCDFiles[i];
                    var wavFile = wavPCMFiles.FirstOrDefault(filename => originalSCDFile.EndsWith($"{Path.GetFileNameWithoutExtension(filename)}.win32.scd"));

                    if (wavFile == null)
                    {
                        //Console.WriteLine($"Warning: Unable to find the WAV file equivalent to {Path.GetFileName(originalSCDFile)}...");
                        continue;
                    }

                    var wavFilenameWithoutExtension = Path.GetFileNameWithoutExtension(wavFile);
                    var scdPath = Path.Combine(TMP_FOLDER, $"{wavFilenameWithoutExtension}.scd");

                    if (CreateSCD(new List<string>() { wavFile }, scdPath, originalSCDFile) == null)
                    {
                        return false;
                    }

                    outputFile = Path.Combine(outputFolder, $"{wavFilenameWithoutExtension}.win32.scd");
                    var scdOutputFolder = Directory.GetParent(outputFile).FullName;

                    if (!Directory.Exists(scdOutputFolder))
                        Directory.CreateDirectory(scdOutputFolder);

                    File.Copy(scdPath, outputFile, true);
                }
            }

            return true;
        }

        public static string CreateSCD(List<string> wavFiles, string outputFile, string originalSCDFile, Dictionary<int, int> mapping = null)
        {
            var scd = new SCD(File.OpenRead(originalSCDFile));

            var orderedWavFiles = new SortedList<int, string>();

            if (mapping != null)
            {
                foreach (var key in mapping.Keys)
                {
                    orderedWavFiles.Add(key, wavFiles[mapping[key]]);
                }
            }
            else
            {
                for (int i = 1; i <= wavFiles.Count; i++)
                {
                    orderedWavFiles.Add(i, wavFiles[i - 1]);
                }
            }

            if (orderedWavFiles.Count != wavFiles.Count)
            {
                Console.WriteLine($"{Path.GetFileName(originalSCDFile)} has {wavFiles.Count} tracks, not {orderedWavFiles.Count}!");
                throw new Exception("Some stream names haven't been found!");
            }

            if (scd.StreamsData.Count != wavFiles.Count)
            {
                Console.WriteLine(
                    "The streams count in the original SCD and the the WAV count doesn't match, " +
                    "please make sure the original SCD you specified correspond to the VSB/WAVs you specified."
                );

                return null;
            }

            var streamsAudioData = new List<byte[]>();

            foreach (var wavFile in orderedWavFiles)
            {
                var index = wavFile.Key;
                var wavFilename = wavFile.Value;
                var finalAudioFilePath = Path.GetFileNameWithoutExtension(wavFilename).Split("@")[0];
                var streamAudioData = new byte[0];

                // Convert wav files according to origin SCD codec
                var originalScdStream = scd.StreamsData[index - 1];

                if (originalScdStream.Header.Codec == (uint)SCD.Codecs.MSADPCM)
                {
                    finalAudioFilePath = Path.Combine(TMP_FOLDER, $"{finalAudioFilePath}.wav");

                    // Convert WAV PCM into WAV MS-ADPCM
                    var p = new Process();
                    p.StartInfo.FileName = $@"{TOOLS_PATH}/adpcmencode/adpcmencode3.exe";
                    p.StartInfo.Arguments = $"-b 32 \"{wavFilename}\" \"{finalAudioFilePath}\"";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardInput = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.Start();
                    p.WaitForExit();

                    p.Close();

                    streamAudioData = Helpers.StripWavHeader(File.ReadAllBytes(finalAudioFilePath));
                }
                else if (originalScdStream.Header.Codec == (uint)SCD.Codecs.OGG)
                {
                    finalAudioFilePath = Path.Combine(TMP_FOLDER, $"{finalAudioFilePath}.ogg");

                    // Convert WAV PCM into WAV MS-ADPCM
                    var p = new Process();
                    p.StartInfo.FileName = $@"{TOOLS_PATH}/oggenc/oggenc2.exe";
                    p.StartInfo.Arguments = $" -s {index} --resample 48000 -o \"{finalAudioFilePath}\" \"{wavFilename}\"";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardInput = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.Start();
                    p.WaitForExit();

                    p.Close();

                    streamAudioData = File.ReadAllBytes(finalAudioFilePath);
                }

                Helpers.Align(ref streamAudioData, 0x10);

                streamsAudioData.Add(streamAudioData);
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
                    TotalFileSize = (uint)streamsAudioData.Sum(content => content.Length)
                };

                BinaryMapping.WriteObject(writer, scdHeader);

                // Write Table offsets header
                var scdTableOffsetsHeader = new SCD.SCDTableHeader()
                {
                    Table0ElementCount = scd.TablesHeader.Table0ElementCount,
                    Table1ElementCount = scd.TablesHeader.Table1ElementCount,
                    Table2ElementCount = scd.TablesHeader.Table2ElementCount,
                    Table3ElementCount = scd.TablesHeader.Table3ElementCount,
                    Table1Offset = scd.TablesHeader.Table1Offset,
                    Table2Offset = scd.TablesHeader.Table2Offset,
                    Table3Offset = scd.TablesHeader.Table3Offset,
                    Table4Offset = scd.TablesHeader.Table4Offset,
                    Unk14 = scd.TablesHeader.Unk14,
                    Padding = scd.TablesHeader.Padding,
                };

                BinaryMapping.WriteObject(writer, scdTableOffsetsHeader);

                // Write original data from current position to the table 1 offset (before to write all streams offets)
                var data = scd.Data.SubArray((int)writer.Position, (int)(scdTableOffsetsHeader.Table2Offset - writer.Position));
                writer.Write(data);

                // Write stream entries offset
                var streamOffset = (uint)scd.StreamsData[0].Offset;
                var streamHeaderSize = 32;
                var streamsOffsets = new List<uint>();

                for (int i = 0; i < streamsAudioData.Count; i++)
                {
                    var audioContent = streamsAudioData[i];
                    writer.Write(BitConverter.GetBytes(streamOffset));

                    streamsOffsets.Add(streamOffset);

                    streamOffset += (uint)(audioContent.Length + (streamHeaderSize + scd.StreamsData[i].ExtraData.Length));
                }

                // Write the original data from current stream position to the start of the first stream header
                data = scd.Data.SubArray((int)writer.Position, (int)(streamsOffsets[0] - writer.Position));
                writer.Write(data);

                // Write data for each stream entry
                for (int i = 0; i < scd.StreamsData.Count; i++)
                {
                    var streamData = scd.StreamsData[i];
                    var wavFile = wavFiles[i];
                    var audioContent = streamsAudioData[i];

                    var newStreamHeader = streamData.Header;

                    newStreamHeader.StreamSize = (uint)audioContent.Length;

                    if (streamData.Header.Codec == (uint)SCD.Codecs.MSADPCM)
                    {
                        var waveFileInfo = new WaveFileReader(wavFile);

                        newStreamHeader.ChannelCount = (uint)waveFileInfo.WaveFormat.Channels;
                        newStreamHeader.SampleRate = (uint)waveFileInfo.WaveFormat.SampleRate;

                        waveFileInfo.Close();
                    }

                    // Write stream header
                    BinaryMapping.WriteObject(writer, newStreamHeader);
                    // Write stream extra data
                    writer.Write(streamData.ExtraData);
                    // Write stream audio data
                    writer.Write(audioContent);
                }

                File.WriteAllBytes(outputFile, writer.ReadAllBytes());

                // Check the new SCD is correct
                var newScd = new SCD(File.OpenRead(outputFile));

                return outputFile;
            }
        }
    }
}
