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

        public static string ReadString(this Stream stream, int maxLength, Encoding encoding)
        {
            var data = stream.ReadBytes(maxLength);
            var terminatorIndex = Array.FindIndex(data, x => x == 0);
            return encoding.GetString(data, 0, terminatorIndex < 0 ? maxLength : terminatorIndex);
        }

        #region Helpers

        public static int Align(int offset, int alignment)
        {
            var misalignment = offset % alignment;
            return misalignment > 0 ? alignment - misalignment : offset;
        }

        public static long Align(long offset, int alignment)
        {
            var misalignment = offset % alignment;
            return misalignment > 0 ? offset + alignment - misalignment : offset;
        }

        public static void Align(ref byte[] data, int alignment)
        {
            var padding = Align(data.Length, alignment);
            Array.Resize(ref data, data.Length + padding);
        }

        #endregion
    }
}
