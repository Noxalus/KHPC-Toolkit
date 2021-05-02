using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCDEncoder
{
    class Helpers
    {
        public static int Align(int offset, int alignment)
        {
            var misalignment = offset % alignment;
            return misalignment != 0 ? alignment - misalignment : 0;
        }

        public static long Align(long offset, int alignment)
        {
            var misalignment = offset % alignment;
            return misalignment != 0 ? alignment - misalignment : 0;
        }

        public static void Align(ref byte[] data, int alignment)
        {
            var padding = Align(data.Length, alignment);
            Array.Resize(ref data, data.Length + padding);
        }

        public static bool IsPadding(byte[] padding)
        {
            return padding.All(singleByte => singleByte == 0);
        }
    }
}
