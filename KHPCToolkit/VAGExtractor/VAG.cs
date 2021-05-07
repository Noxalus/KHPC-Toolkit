using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xe.BinaryMapper;

namespace VAGExtractor
{
    class VAG
    {
        private const string MAGIC_CODE = "VAGp";

        public class VAGHeader
        {
            [Data(Count = 4)] public string Magic { get; set; }
            [Data] public UInt32 Version { get; set; }
            [Data] public UInt32 ReservedArea1 { get; set; }
            [Data] public UInt32 DataSize { get; set; } // In bytes
            [Data] public UInt32 SampleRate { get; set; }
            [Data(Count = 10)] public byte[] ReservedArea2 { get; set; }
            [Data] public byte Channels { get; set; }
            [Data] public byte ReservedArea3 { get; set; }
            [Data(Count = 16)] public string Name { get; set; }
        }

        private VAGHeader _header;
        private byte[] _audioData;

        private IBinaryMapping _mapper;

        public string Name => _header.Name;

        public VAG(Stream stream)
        {
            // VAG files are in Big Endian...
            _mapper = MappingConfiguration
                .DefaultConfiguration(Encoding.UTF8, true)
                .Build();

            // Read base SCD header
            _header = _mapper.ReadObject<VAGHeader>(stream);

            _audioData = new byte[_header.DataSize];
             stream.Read(_audioData);

            if (!_header.Magic.Equals(MAGIC_CODE))
            {
                throw new Exception("Magic code not found, invalid SCD file.");
            }
        }

        public string Export(string outputPath, string newFilename = null, bool removeSuffix = true)
        {
            var filename = $"{(newFilename != null ? newFilename : Name)}.vag";

            if (removeSuffix)
                filename = filename.Replace("_f", "");

            var filePath = Path.Combine(outputPath, filename);
            var fileStream = File.Create(filePath);

            _mapper.WriteObject<VAGHeader>(fileStream, _header);
            fileStream.Write(_audioData);

            fileStream.Close();

            return filePath;
        }
    }
}
