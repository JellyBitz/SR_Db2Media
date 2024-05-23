using SRO.Security;
using System;
using System.Linq;
using System.Runtime.InteropServices;

// https://github.com/DummkopfOfHachtenduden/SilkroadDoc/wiki/JMXPACK
namespace SRO.PK2
{
    [StructLayout(LayoutKind.Sequential, Size = 256)]
    public struct PackFileHeader
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 30)]
        public string Signature; //JoyMax File Manager!\n
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        private byte[] Version; //2.0.0.1
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        private byte[] _Encrypted;
        /// <summary>
        /// Indicates if the file is encrypted.
        /// </summary>
        public bool IsEncrypted
        {
            get => BitConverter.ToBoolean(_Encrypted, 0);
            set => _Encrypted = BitConverter.GetBytes(value);
        }
        /// <summary>
        /// Used to test the blowfish key.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Checksum;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 205)]
        public byte[] Reserved;
        /// <summary>
        /// Creates a header with default values.
        /// </summary>
        public static PackFileHeader GetDefault()
        {
            var result = new PackFileHeader()
            {
                Signature = "JoyMax File Manager!\n",
                Version = new byte[4] { 2, 0, 0, 1 },
                IsEncrypted = true,
                Checksum = new byte[16],
                Reserved = new byte[205],
            };
            return result;
        }
    }
    [StructLayout(LayoutKind.Sequential, Size = 2560)]
    public struct PackFileBlock
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public PackFileEntry[] Entries;
    }
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct PackFileEntry
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        private byte[] _Type;
        public PackFileEntryType Type
        {
            get => (PackFileEntryType)_Type[0];
            set => _Type = new byte[] { (byte)value };
        }
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 89)]
        public string Name;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        private byte[] _CreationTime;
        public DateTime CreationTime
        {
            get => DateTime.FromFileTime(BitConverter.ToInt64(_CreationTime.ToArray(), 0));
            set => _CreationTime = BitConverter.GetBytes(value.ToFileTime());
        }
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        private byte[] _ModificationTime;
        public DateTime ModificationTime
        {
            get => DateTime.FromFileTime(BitConverter.ToInt64(_ModificationTime.ToArray(), 0));
            set => _ModificationTime = BitConverter.GetBytes(value.ToFileTime());
        }
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        private byte[] _Offset;
        /// <summary>
        /// Depends on <see cref="Type"/>.
        /// <para>If <see cref="PackFileEntryType.Folder"/> : Offset to the directory block.</para>
        /// <para>If <see cref="PackFileEntryType.File"/> : Offset to the file data.</para>
        /// </summary>
        public long Offset
        {
            get => BitConverter.ToInt64(_Offset.ToArray(), 0);
            set => _Offset = BitConverter.GetBytes(value);
        }
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        private byte[] _Size;
        /// <summary>
        /// File size.
        /// </summary>
        public uint Size
        {
            get => BitConverter.ToUInt32(_Size.ToArray(), 0);
            set => _Size = BitConverter.GetBytes(value);
        }
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        private byte[] _NextBlock;
        /// <summary>
        /// Offset pointing to the next block from chain.
        /// </summary>
        public long NextBlock
        {
            get => BitConverter.ToInt64(_NextBlock.ToArray(), 0);
            set => _NextBlock = BitConverter.GetBytes(value).ToArray();
        }
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] Padding;
        /// <summary>
        /// Creates an entry with default values.
        /// </summary>
        public static PackFileEntry GetDefault()
        {
            return new PackFileEntry()
            {
                Type = PackFileEntryType.Empty,
                Name = string.Empty,
                CreationTime = DateTime.FromFileTime(0),
                ModificationTime = DateTime.FromFileTime(0),
                Offset = 0,
                Size = 0,
                NextBlock = 0,
                Padding = new byte[2]
            };
        }
    }
    public enum PackFileEntryType : byte
    {
        Empty = 0,
        Folder,
        File,
    }
}
