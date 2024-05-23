using System.IO;
using System.Runtime.InteropServices;

namespace SRO.Utility
{
    public static class FileStreamHelpers
    {
        /// <summary>
        /// Reads and convert a sequence from stream into a structure type.
        /// </summary>
        public static T Read<T>(this FileStream stream)
        {
            byte[] bytes = new byte[Marshal.SizeOf(typeof(T))];
            stream.Read(bytes, 0, bytes.Length);
            return ByteArrayHelpers.FromByteArray<T>(bytes);
        }
        /// <summary>
        /// Convert the structure and writes the sequence on stream.
        /// </summary>
        public static void Write<T>(this FileStream stream, T value)
        {
            byte[] bytes = ByteArrayHelpers.ToByteArray(value);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
