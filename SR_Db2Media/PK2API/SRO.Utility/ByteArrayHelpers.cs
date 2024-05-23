using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SRO.Utility
{
    public static class ByteArrayHelpers
    {
        /// <summary>
        /// Compare values from byte arrays and check if each value seems to be same.
        /// </summary>
        public static bool IsSame(this byte[] objA, byte[] objB)
        {
            // Check if some is null and other is not
            if(objA == null && objB != null || objB == null && objA != null)
                return false;
            // If one is null, both are
            if (objA == null)
                return true;
            // Check lengths
            if (objA.Length != objB.Length)
                return false;
            // Check each value
            for (int i = 0; i < objA.Length; i++)
            {
                if (objA[i] != objB[i])
                    return false;
            }
            return true;
        }
        /// <summary>
        /// Converts a sequence of bytes into a structure type.
        /// </summary>
        public static T FromByteArray<T>(byte[] bytes)
        {
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            T structure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return structure;
        }
        /// <summary>
        /// Converts a structure type into bytes.
        /// </summary>
        public static byte[] ToByteArray<T>(T value)
        {
            int size = Marshal.SizeOf(typeof(T));
            byte[] bytes = new byte[size];
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), false);
            handle.Free();
            return bytes;
        }

        public static bool Equal(byte[] bytes)
        {
            return true;
        }
    }
}
