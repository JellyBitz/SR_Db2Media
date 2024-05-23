using System.Collections.Generic;
using System.IO;

namespace SRO.PK2
{
    public class Pk2Folder
    {
        #region Private Members & Public Properties
        /// <summary>
        /// The folder which contains this folder.
        /// </summary>
        public Pk2Folder Parent;
        /// <summary>
        /// Folder name.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// Offset from the <see cref="PackFileBlock"/> chain.
        /// </summary>
        public long Offset { get; }
        /// <summary>
        /// All files this folder contains.
        /// </summary>
        public Dictionary<string, Pk2File> Files { get; } = new Dictionary<string, Pk2File>();
        /// <summary>
        /// All subfolders this folder contains.
        /// </summary>
        public Dictionary<string, Pk2Folder> Folders { get; } = new Dictionary<string, Pk2Folder>();
        #endregion

        #region Constructor
        public Pk2Folder(string name, Pk2Folder parent, long offset)
        {
            Name = name;
            Parent = parent;
            Offset = offset;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Get the full path to this folder.
        /// </summary>
        public string GetFullPath()
        {
            if (Parent != null)
                return Path.Combine(Parent.GetFullPath(), Name.ToLowerInvariant());
            return Name.ToLowerInvariant();
        }
        #endregion
    }
}
