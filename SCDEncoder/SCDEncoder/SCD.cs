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
            [Data] public uint Unk18 { get; set; } // Offset of the end of the table offset header?
            [Data] public uint Padding { get; set; }
        }

        public class StreamName
        {
            [Data(Count = 88)] public byte[] Unknown { get; set; }
        }

        public class StreamUnknown
        {
            [Data(Count = 128)] public byte[] Unknown { get; set; }
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

            var streamNames = new List<StreamName>();

            for (int i = 0; i < namesOffsets.Count; i++)
            {
                uint nameOffset = namesOffsets[i];
                stream.Seek(nameOffset, SeekOrigin.Begin);

                var streamName = BinaryMapping.ReadObject<StreamName>(stream);

                streamNames.Add(streamName);
            }

            var streamUnknowns = new List<StreamUnknown>();

            for (int i = 0; i < unknownOffsets.Count; i++)
            {
                uint unknownOffset = unknownOffsets[i];
                stream.Seek(unknownOffset, SeekOrigin.Begin);

                var streamUnknown = BinaryMapping.ReadObject<StreamUnknown>(stream);

                streamUnknowns.Add(streamUnknown);
            }

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

            var totalStreamSize = _streamsData.Sum(streamData => streamData.Data.Length);

            _data = stream.ReadAllBytes();

            stream.Close();
        }
    }
}
