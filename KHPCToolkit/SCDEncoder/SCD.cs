using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xe.BinaryMapper;

namespace SCDEncoder
{
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
        public class SCDTableHeader
        {
            [Data] public ushort Table0ElementCount { get; set; }
            [Data] public ushort Table1ElementCount { get; set; }
            [Data] public ushort Table2ElementCount { get; set; }
            [Data] public ushort Table3ElementCount { get; set; }
            [Data] public uint Table1Offset { get; set; }
            [Data] public uint Table2Offset { get; set; }
            [Data] public uint Table3Offset { get; set; }
            [Data] public uint Unk14 { get; set; } // Always null?
            [Data] public uint Table4Offset { get; set; }
            [Data] public uint Padding { get; set; }
        }

        public class StreamName
        {
            [Data] public ushort Unknown01_1 { get; set; }
            [Data] public ushort Unknown01_2 { get; set; }
            [Data] public ushort Unknown02_1 { get; set; }
            [Data] public ushort Unknown02_2 { get; set; }
            [Data] public ushort Unknown03_1 { get; set; }
            [Data] public ushort Unknown03_2 { get; set; }
            [Data] public uint Index { get; set; }
            [Data] public uint Unknown05 { get; set; }
            [Data] public uint Unknown06 { get; set; }
            [Data] public uint Unknown07 { get; set; }
            [Data] public uint Unknown08 { get; set; }
            [Data] public ushort Unknown09_1 { get; set; }
            [Data] public ushort Unknown09_2 { get; set; }
            [Data] public uint Unknown10 { get; set; }
            [Data] public uint Unknown11 { get; set; }
            [Data] public uint Unknown12 { get; set; }
            [Data(Count = 20)] public string Name { get; set; }
        }

        public class Table1Data
        {
            [Data(Count = 88)] public byte[] Unknown { get; set; }
        }

        public class Table3Data
        {
            [Data(Count = 128)] public byte[] Unknown { get; set; }
        }

        public class Table4Data
        {
            [Data(Count = 124)] public byte[] Unknown { get; set; }
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

        private SCDHeader _header = new SCDHeader();
        private SCDTableHeader _tablesHeader = new SCDTableHeader();
        private List<StreamName> _streamsNames = new List<StreamName>();
        private List<StreamData> _streamsData = new List<StreamData>();
        private byte[] _data;

        // Table 0 => stream names
        private List<uint> _table0Offsets = new List<uint>();
        private List<uint> _table1Offsets = new List<uint>();
        // Table 2 => stream data
        private List<uint> _table2Offsets = new List<uint>();
        private List<uint> _table3Offsets = new List<uint>();
        private List<uint> _table4Offsets = new List<uint>();

        private List<Table1Data> _table1Data = new List<Table1Data>();
        private List<Table3Data> _table3Data = new List<Table3Data>();
        private List<Table4Data> _table4Data = new List<Table4Data>();

        public SCDHeader Header => _header;
        public SCDTableHeader TablesHeader => _tablesHeader;
        public List<StreamName> StreamsNames => _streamsNames;
        public List<StreamData> StreamsData => _streamsData;
        public byte[] Data => _data;

        public SCD(Stream stream)
        {
            // Read base SCD header
            _header = BinaryMapping.ReadObject<SCDHeader>(stream);

            if (_header.MagicCode != MAGIC_CODE)
            {
                throw new Exception("Magic code not found, invalid SCD file.");
            }

            if (_header.SSCFVersion != SSCF_VERSION)
            {
                throw new Exception("Wrong SSCF version, invalid SCD file.");
            }

            // Read tables offsets
            _tablesHeader = BinaryMapping.ReadObject<SCDTableHeader>(stream);

            if (_tablesHeader.Padding != 0)
            {
                throw new Exception("Padding is not null!");
            }

            // Table 0 offsets
            for (int i = 0; i < _tablesHeader.Table0ElementCount; i++)
            {
                _table0Offsets.Add(stream.ReadUInt32());
            }

            stream.AlignPosition(0x10);

            if (stream.Position != _tablesHeader.Table1Offset)
            {
                throw new Exception("Wrong stream position!");
            }

            // Table 1 offsets
            for (int i = 0; i < _tablesHeader.Table1ElementCount; i++)
            {
                _table1Offsets.Add(stream.ReadUInt32());
            }

            stream.AlignPosition(0x10);

            if (stream.Position != _tablesHeader.Table2Offset)
            {
                throw new Exception("Wrong stream position!");
            }

            // Table 2 offsets
            for (int i = 0; i < _tablesHeader.Table2ElementCount; i++)
            {
                _table2Offsets.Add(stream.ReadUInt32());
            }

            stream.AlignPosition(0x10);

            if (stream.Position != _tablesHeader.Table3Offset)
            {
                throw new Exception("Wrong stream position!");
            }

            // Table 3 offsets
            // TODO: Understand what are the 3 last offsets
            for (int i = 0; i < (_tablesHeader.Table3ElementCount / 2) - 3; i++)
            {
                _table3Offsets.Add(stream.ReadUInt32());
            }

            stream.AlignPosition(0x10);

            var table4ElementCount = (_tablesHeader.Table3ElementCount / 2) - _table3Offsets.Count - 1;

            // Table 4 offsets
            for (int i = 0; i < table4ElementCount; i++)
            {
                _table4Offsets.Add(stream.ReadUInt32());
            }

            stream.AlignPosition(0x10);

            // Table 3 data => ???

            for (int i = 0; i < _table3Offsets.Count; i++)
            {
                if (stream.Position != _table3Offsets[i])
                {
                    Console.WriteLine($"Seek to the proper offset for index {i} of Table 3");
                    stream.Seek(_table3Offsets[i], SeekOrigin.Begin);
                    //throw new Exception("Wrong stream position!");
                }

                var data = BinaryMapping.ReadObject<Table3Data>(stream);

                _table3Data.Add(data);
            }

            // Table 0 data => Stream names
            for (int i = 0; i < _table0Offsets.Count; i++)
            {
                if (stream.Position != _table0Offsets[i])
                {
                    // TODO: Understand why when the stream name is null, the size is 4 bytes larger :O
                    stream.Seek(_table0Offsets[i], SeekOrigin.Begin);
                }

                var streamName = BinaryMapping.ReadObject<StreamName>(stream);
                _streamsNames.Add(streamName);
            }

            // Table 1 data => ???
            for (int i = 0; i < _table1Offsets.Count; i++)
            {
                if (stream.Position != _table1Offsets[i])
                {
                    Console.WriteLine($"Seek to the proper offset for index {i} of Table 1");
                    stream.Seek(_table1Offsets[i], SeekOrigin.Begin);
                    //throw new Exception("Wrong stream position!");
                }

                var data = BinaryMapping.ReadObject<Table1Data>(stream);
                _table1Data.Add(data);
            }

            for (int i = 0; i < _table4Offsets.Count; i++)
            {
                if (stream.Position != _table4Offsets[i])
                {
                    Console.WriteLine($"Seek to the proper offset for index {i} of Table 4");
                    stream.Seek(_table4Offsets[i], SeekOrigin.Begin);
                    //throw new Exception("Wrong stream position!");
                }

                var data = BinaryMapping.ReadObject<Table4Data>(stream);
                _table4Data.Add(data);
            }

            stream.AlignPosition(0x10);

            // Stream Data
            for (int i = 0; i < _table2Offsets.Count; i++)
            {
                uint streamOffset = _table2Offsets[i];

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

                var nextOffset = i == _table2Offsets.Count - 1 ? stream.Length : _table2Offsets[i + 1];
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
    }
}
