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

        private static void CreateSCD(List<string> wavFiles, string outputFile, string originalScd = null)
        {
            if (!string.IsNullOrEmpty(originalScd))
            {
                var scd = new SCD(File.OpenRead(originalScd));

                //var scdHeaderData = scd.ExtractHeader();
                //var streamTotalSize = scd.StreamsData.Sum(streamData => streamData.Data.Length);
                //var streamHeadersSize = 32 * scd.StreamsData.Count;
                //var scdHeaderSize = scd.Data.Length - streamTotalSize - streamHeadersSize;

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
                    var wavContent = StripWavHeader(File.ReadAllBytes(wavFile));
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
                        TotalFileSize = scd.Header.TotalFileSize  //(uint)(scd.Header.HeaderSize + wavesContent.Sum(content => content.Length))
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

                    // TODO: Remove this
                    //var data = scd.Data.SubArray((int)writer.Position, (int)(scd.StreamsData[0].Offset - writer.Position));
                    //writer.Write(data);

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

                        var newStreamHeader = new SCD.StreamHeader();
                        newStreamHeader.AuxChunkCount = streamData.Header.AuxChunkCount;
                        newStreamHeader.ChannelCount = (uint)waveFileInfo.WaveFormat.Channels;
                        newStreamHeader.Codec = streamData.Header.Codec;
                        newStreamHeader.ExtraDataSize = streamData.Header.ExtraDataSize;
                        newStreamHeader.LoopStart = streamData.Header.LoopStart;
                        newStreamHeader.LoopEnd = streamData.Header.LoopEnd;
                        newStreamHeader.SampleRate = (uint)waveFileInfo.WaveFormat.SampleRate;
                        newStreamHeader.StreamSize = (uint)wavContent.Length;

                        BinaryMapping.WriteObject(writer, newStreamHeader);
                        writer.Write(streamData.ExtraData);
                        writer.Write(wavContent);

                        // TODO: Remove this
                        //if (i == scd.StreamsData.Count - 1)
                        //{
                        //    BinaryMapping.WriteObject(writer, newStreamHeader);
                        //    writer.Write(wavContent);
                        //    //writer.Write(wavContent.SubArray(0, (int)streamData.Data.Length));
                        //}
                        //else
                        //{
                        //    BinaryMapping.WriteObject(writer, streamData.Header);
                        //    writer.Write(streamData.Data);

                        //    //writer.Write(wavContent);
                        //    //writer.Write(new byte[streamData.Data.Length - wavContent.Length]);
                        //}

                        //byte[] padding;

                        //if (i < scd.StreamsData.Count - 1)
                        //{
                        //    var size = (int)(scd.StreamsData[i + 1].Offset - writer.Position);
                        //    var originalData = scd.Data.SubArray((int)writer.Position, size);
                        //    Console.WriteLine($"Compare {size} with {scd.StreamsData[i].Data.Length % 64}");

                        //    padding = new byte[size];
                        //}
                        //else
                        //{
                        //    var size = (int)(scd.Data.Length - writer.Position);
                        //    padding = new byte[size];
                        //}

                        //writer.Write(padding);

                        waveFileInfo.Close();
                    }

                    //data = scd.Data.SubArray((int)writer.Position, (int)(scd.Data.Length - writer.Position));
                    //writer.Write(data);

                    File.WriteAllBytes(outputFile, writer.ReadAllBytes());

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

    public class SCD
    {
        private const UInt64 MAGIC_CODE = 0x4643535342444553;
        private const byte SSCF_VERSION = 0x4;

        public class SCDHeader
        {
            [Data] public UInt64 MagicCode { get; set; }
            [Data] public uint FileVersion { get; set; }
            [Data] public byte BigEndianFlag { get; set; }
            [Data] public byte SSCFVersion { get; set; }
            [Data] public ushort HeaderSize { get; set; }
            [Data] public uint TotalFileSize { get; set; }
            [Data(Count = 7)] public uint[] Padding { get; set; }
        }

        // Table0 has the offsets of the name entries
        // Table1 has the offsets to the sound entries
        // Table2 has ???
        public class SCDTableOffsetHeader
        {
            [Data] public ushort Table0ElementCount { get; set; }
            [Data] public ushort Table1ElementCount { get; set; }
            [Data] public ushort Table2ElementCount { get; set; }
            [Data] public ushort Unk06 { get; set; }
            [Data] public uint Table0Offset { get; set; }
            [Data] public uint Table1Offset { get; set; }
            [Data] public uint Table2Offset { get; set; }
            [Data] public uint Unk14 { get; set; }
            [Data] public uint Unk18 { get; set; }
            [Data] public uint Padding { get; set; }
        }

        public class StreamHeader
        {
            [Data] public uint StreamSize { get; set; }
            [Data] public uint ChannelCount { get; set; } // 1: Mono, 2: Stereo
            [Data] public uint SampleRate { get; set; }
            [Data] public uint Codec { get; set; } // 0x0C: MS-ADPCM, 0x06: OGG
            [Data] public uint LoopStart { get; set; }
            [Data] public uint LoopEnd { get; set; }
            [Data] public uint ExtraDataSize { get; set; } // Also known as "first frame position". Add to after header for first frame.
            [Data] public ushort AuxChunkCount { get; set; }
            [Data] public ushort Unknown { get; set; }
        }

        public struct StreamData
        {
            public StreamHeader Header;
            public byte[] Data;
            public byte[] ExtraData;
            public long Offset;
        }

        public class StreamPadding
        {
            [Data] public ushort Unknown1 { get; set; }
            [Data] public ushort Unknown2 { get; set; }
            [Data] public ushort Unknown3 { get; set; }
            [Data] public byte Unknown4 { get; set; }
            [Data] public byte Unknown5 { get; set; }
        }

        private SCDHeader _header = new SCDHeader();
        private SCDTableOffsetHeader _tableOffsetHeader = new SCDTableOffsetHeader();
        private List<StreamData> _streamsData = new List<StreamData>();
        private byte[] _data;

        public SCDHeader Header => _header;
        public SCDTableOffsetHeader TableOffsetHeader => _tableOffsetHeader;
        public List<StreamData> StreamsData => _streamsData;
        public byte[] Data => _data;

        public SCD(Stream stream)
        {
            _header = BinaryMapping.ReadObject<SCDHeader>(stream);

            if (_header.MagicCode != MAGIC_CODE)
            {
                throw new Exception("Magic code not found, invalid SCD file.");
            }

            if (_header.SSCFVersion != SSCF_VERSION)
            {
                throw new Exception("Wrong SSCF version, invalid SCD file.");
            }

            _tableOffsetHeader = BinaryMapping.ReadObject<SCDTableOffsetHeader>(stream);

            if (_tableOffsetHeader.Padding != 0)
            {
                throw new Exception("Padding is not null!");
            }

            // size = 16 when 1 element, 192 with 36 elements, what is it?
            var unknown = stream.ReadBytes(_tableOffsetHeader.Table0Offset - stream.Position);

            stream.Seek(_tableOffsetHeader.Table0Offset, SeekOrigin.Begin);

            // Warning: Table0ElementCount has the wrong number of elements
            var namesOffsets = new List<uint>();
            for (int i = 0; i < _tableOffsetHeader.Table1ElementCount; i++)
            {
                namesOffsets.Add(stream.ReadUInt32());
            }

            stream.AlignPosition(0x10);
            //stream.Seek(_tableOffsetHeader.Table1Offset, SeekOrigin.Begin);

            var streamOffsets = new List<uint>();
            for (int i = 0; i < _tableOffsetHeader.Table1ElementCount; i++)
            {
                streamOffsets.Add(stream.ReadUInt32());
            }

            stream.AlignPosition(0x10);
            //stream.Seek(_tableOffsetHeader.Table2Offset, SeekOrigin.Begin);

            var unknownOffsets = new List<uint>();
            for (int i = 0; i < _tableOffsetHeader.Table2ElementCount; i++)
            {
                unknownOffsets.Add(stream.ReadUInt32());
            }

            stream.AlignPosition(0x10);

            for (int i = 0; i < streamOffsets.Count; i++)
            {
                uint streamOffset = streamOffsets[i];

                stream.Seek(streamOffset, SeekOrigin.Begin);

                var header = BinaryMapping.ReadObject<StreamHeader>(stream);

                // Skip any aux chunks
                int chunkStartPosition = (int)stream.Position;
                int chunkEndPos = chunkStartPosition;
                for (int j = 0; j < header.AuxChunkCount; j++)
                {
                    stream.ReadUInt32();
                    chunkEndPos += (int)stream.ReadUInt32();
                    stream.Seek(chunkEndPos, SeekOrigin.Begin);
                }

                var extraData = stream.ReadBytes((int)header.ExtraDataSize);
                var data = stream.ReadBytes((int)header.StreamSize);

                var nextOffset = i == streamOffsets.Count - 1 ? stream.Length : streamOffsets[i + 1];
                var padding = stream.ReadBytes((int)(nextOffset - stream.Position));

                // Check there is no data in the padding
                if (!Helpers.IsPadding(padding))
                {
                    throw new Exception("Padding is not null!");
                }

                var streamData = new StreamData
                {
                    Header = header,
                    Data = data,
                    ExtraData = extraData,
                    Offset = streamOffset
                };

                _streamsData.Add(streamData); 
            }

            _data = stream.ReadAllBytes();

            stream.Close();
        }

        public byte[] ExtractHeader()
        {
            for (int i = 1; i < _streamsData.Count; i++)
            {
                var diff = (_streamsData[i].Offset - _streamsData[i - 1].Data.Length) - _streamsData[i - 1].Offset;
                Console.WriteLine(diff);
            }

            var size = (int)_header.TotalFileSize - _streamsData.Sum(stream => stream.Data.Length);
            return _data.SubArray(0, (int)_streamsData[0].Offset);
        }
    }

    public static class Extensions
    {
        public static T[] SubArray<T>(this T[] array, int offset, int length)
        {
            T[] result = new T[length];
            Array.Copy(array, offset, result, 0, length);
            return result;
        }
    }
}
