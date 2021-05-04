using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCDEncoder
{
    public static class StreamExtensions
    {
        public static T SetPosition<T>(this T stream, long position) where T : Stream
        {
            stream.Seek(position, SeekOrigin.Begin);
            return stream;
        }

        public static byte[] ReadBytes(this Stream stream) => stream.ReadBytes((int)(stream.Length - stream.Position));

        public static byte[] ReadBytes(this Stream stream, int length)
        {
            var data = new byte[length];
            stream.Read(data, 0, length);
            return data;
        }

        public static byte[] ReadBytes(this Stream stream, long length)
        {
            var data = new byte[length];
            stream.Read(data, 0, (int)length);
            return data;
        }

        public static byte[] ReadAllBytes(this Stream stream)
        {
            var data = stream.SetPosition(0).ReadBytes();
            stream.Position = 0;
            return data;
        }

        unsafe public static uint ReadUInt32(this Stream stream)
        {
            var buffer = new byte[4];
            stream.Read(buffer, 0, 4);
            fixed (byte* ptr = buffer)
                return *(uint*)ptr;
        }

        unsafe public static ushort ReadUShort(this Stream stream)
        {
            var buffer = new byte[2];
            stream.Read(buffer, 0, 2);
            fixed (byte* ptr = buffer)
                return *(ushort*)ptr;
        }

        public static string ReadString(this Stream stream, int maxLength, Encoding encoding)
        {
            var data = stream.ReadBytes(maxLength);
            var terminatorIndex = Array.FindIndex(data, x => x == 0);
            return encoding.GetString(data, 0, terminatorIndex < 0 ? maxLength : terminatorIndex);
        }

        public static T AlignPosition<T>(this T stream, int alignValue) where T : Stream => stream.SetPosition(stream.Position + Helpers.Align(stream.Position, alignValue));
    }
}
