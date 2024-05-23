using System.IO;

namespace SRO.PK2
{
    public class Pk2File
    {
        #region Private Members & Public Properties
        private FileStream mFileStream;
        /// <summary>
        /// File name.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The folder which contains this file.
        /// </summary>
        public Pk2Folder Parent { get; }
        /// <summary>
        /// Offset from file data on stream.
        /// </summary>
        public long Offset { get; }
        /// <summary>
        /// File data size.
        /// </summary>
        public uint Size { get; }
        #endregion

        #region Constructor
        public Pk2File(string name, Pk2Folder parent, FileStream filestream, long offset, uint size)
        {
            Name = name;
            Parent = parent;
            mFileStream = filestream;
            Offset = offset;
            Size = size;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Get the full path to this file.
        /// </summary>
        public string GetFullPath()
        {
            if (Parent != null)
                return Path.Combine(Parent.GetFullPath(), Name.ToLowerInvariant());
            return Name.ToLowerInvariant();
        }
        /// <summary>
        /// Gets the content in bytes from the file.
        /// </summary>
        public byte[] GetContent()
        {
            mFileStream.Seek(Offset, SeekOrigin.Begin);
            var bytes = new byte[Size];
            mFileStream.Read(bytes, 0, bytes.Length);
            return bytes;
        }
        #endregion
    }
}
