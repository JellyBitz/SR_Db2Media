using SRO.Security;
using SRO.Utility;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Text;

namespace SRO.PK2
{
    public class Pk2Stream : IDisposable
    {
        #region Private Members & Public Properties
        private Blowfish mBlowfish = new Blowfish();
        private FileStream mFileStream;
        private PackFileHeader mHeader;
        private Dictionary<string, Pk2Folder> mFolders = new Dictionary<string, Pk2Folder>();
        private Dictionary<string, Pk2File> mFiles = new Dictionary<string, Pk2File>();
        private SortedDictionary<long, long> mDiskAllocations = new SortedDictionary<long, long>();
        #endregion

        #region Constructor
        /// <summary>
        /// Initialize a PK2 file stream which handles all the data required to work with it.
        /// </summary>
        /// <param name="path">Path to the PK2 file.</param>
        /// <param name="key">Blowfish key used for encryption.</param>
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="AuthenticationException"/>
        /// <exception cref="IOException"/>
        public Pk2Stream(string path, string key, FileMode mode = FileMode.Open)
        {
            // Set up blowfish
            mBlowfish.Initialize(key);

            // Check file existence first
            var fileExists = File.Exists(path);
            // Check file setup
            mFileStream = new FileStream(path, mode, FileAccess.ReadWrite, FileShare.ReadWrite);
            switch (mode)
            {
                case FileMode.Create:
                case FileMode.CreateNew:
                    CreateBaseStream();
                    break;
                case FileMode.Append:
                case FileMode.OpenOrCreate:
                    if (!fileExists)
                        CreateBaseStream();
                    break;
                case FileMode.Truncate:
                    throw new NotSupportedException("Truncate mode is not supported");
            }
            // Prepare to read the file
            mFileStream.Seek(0, SeekOrigin.Begin);
            mHeader = mFileStream.Read<PackFileHeader>();
            // Track header allocation
            mDiskAllocations[0] = Marshal.SizeOf(typeof(PackFileHeader));

            // Check if the key provided is the right one by comparing the checksum
            var checksum = mBlowfish.Encode(Encoding.UTF8.GetBytes("Joymax Pack File"));
            var comparer = new byte[mHeader.Checksum.Length];
            Array.Copy(checksum, comparer, 3); // Compare first three values generated from encoding
            if (!mHeader.Checksum.IsSame(comparer))
                throw new AuthenticationException("Error on key authentication access to stream");

            // Set root folder
            var root = new Pk2Folder("", null, mFileStream.Position);
            mFolders.Add(root.Name, root);
            // Try to read all the blocks
            try
            {
                // Track default block
                mDiskAllocations[root.Offset] = Marshal.SizeOf(typeof(PackFileBlock));
                InitializeStreamBlock(root.Offset, root);
            }
            catch (Exception ex)
            {
                // The only reason to fail is a wrong blowfish key or differents Pk2 structures
                throw new IOException("Error reading PK2 file!", ex);
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Get folder by path.
        /// Returns null if folder is not found.
        /// </summary>
        public Pk2Folder GetFolder(string path)
        {
            if (path == null)
                throw new ArgumentException();
            // Normalize path
            path = path.ToLowerInvariant().Replace("/", Path.DirectorySeparatorChar.ToString());
            // Try to get folder
            if (mFolders.TryGetValue(path, out var folder))
                return folder;
            return null;
        }
        /// <summary>
        /// Get file by path.
        /// Returns null if file is not found.
        /// </summary>
        public Pk2File GetFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException();
            // Normalize path
            path = path.ToLowerInvariant().Replace("/", Path.DirectorySeparatorChar.ToString());
            // Try to get file
            if (mFiles.TryGetValue(path, out var file))
                return file;
            return null;
        }
        /// <summary>
        /// Add a folder path.
        /// Returns false if cannot be created or the folder already exists.
        /// </summary>
        public bool AddFolder(string path)
        {
            // Check if folder already exists
            if (GetFolder(path) != null)
                return false;
            // Make sure the same path doesn't exists as a file
            if (GetFile(path) != null)
                return false;
            // Normalize path
            path = path.ToLowerInvariant().Replace("/", Path.DirectorySeparatorChar.ToString());

            // Proceed to create the new folder
            // Try to find the closer existing parent folder
            int i;
            Pk2Folder parent = null;
            var paths = path.Split(Path.DirectorySeparatorChar).ToList();
            for (i = 0; i < paths.Count; i++)
            {
                var nearPath = string.Join(Path.DirectorySeparatorChar.ToString(), paths.Take(paths.Count - i));
                if (mFolders.TryGetValue(nearPath, out parent))
                    break;
            }
            // Set root if an existing parent folder is not found
            if (parent == null)
                parent = mFolders[""];

            // Check paths required to fulfill
            var newPaths = paths.Skip(paths.Count - i).ToList();
            CreateFolderBlock(parent.Offset, parent, newPaths);
            return true;
        }
        /// <summary>
        /// Add a file on specified path. If the path does not exists, it is created.
        /// Return success.
        /// </summary>
        /// <exception cref="ArgumentException"/>
        public bool AddFile(string path, byte[] bytes)
        {
            // Make sure the same path doesn't exists as a folder
            if (GetFolder(path) != null)
                return false;
            // Normalize path
            path = path.ToLowerInvariant().Replace("/", Path.DirectorySeparatorChar.ToString());

            // Check if the file already exists to update/overwrite info with the block entry
            var file = GetFile(path);
            if (file != null)
            {
                long fileOffset;
                // Find and update size allocation
                if (bytes.Length <= file.Size)
                {
                    fileOffset = file.Offset;
                }
                else
                {
                    mDiskAllocations.Remove(file.Offset);
                    fileOffset = AllocateSpace(bytes.Length);
                }
                // Find and update block entry
                long blockOffset = file.Parent.Offset;
                while (blockOffset != 0)
                {
                    var block = LoadPackFileBlock(blockOffset);
                    for (int i = 0; i < block.Entries.Length; i++)
                    {
                        if (block.Entries[i].Type == PackFileEntryType.File
                            && block.Entries[i].Offset == file.Offset) // Easier find offset than normalize names
                        {
                            var newFile = new Pk2File(file.Name, file.Parent, mFileStream, fileOffset, (uint)bytes.Length);
                            // Write/Overwrite file data & track it
                            mFileStream.Seek(newFile.Offset, SeekOrigin.Begin);
                            mFileStream.Write(bytes, 0, bytes.Length);
                            mFileStream.Flush();
                            mDiskAllocations[newFile.Offset] = newFile.Size;
                            // Update entry
                            var timeNow = DateTime.Now;
                            block.Entries[i].ModificationTime = timeNow;
                            block.Entries[i].Size = newFile.Size;
                            block.Entries[i].Offset = newFile.Offset;
                            UpdatePackFileBlock(blockOffset, block);
                            // File added success
                            file.Parent.Files[newFile.Name] = newFile;
                            mFiles[path] = newFile;
                            return true;
                        }
                    }
                    // Continue reading chain until find it
                    blockOffset = block.Entries.Last().NextBlock;
                }
                // Entry was not able to be found
                return false;
            }
            else
            {
                // Create folders required to allocate the file
                var folderName = Path.GetDirectoryName(path);
                if (folderName != "")
                    AddFolder(folderName);
                var folder = GetFolder(folderName);
                // Load and update block with the new file
                long blockOffset = folder.Offset;
                while (blockOffset != 0)
                {
                    var block = LoadPackFileBlock(blockOffset);
                    for (int i = 0; i < block.Entries.Length; i++)
                    {
                        if (block.Entries[i].Type == PackFileEntryType.Empty)
                        {
                            var newFile = new Pk2File(Path.GetFileName(path), folder, mFileStream, (int)AllocateSpace(bytes.Length), (uint)bytes.Length);
                            // Write file data & track it
                            mFileStream.Seek(newFile.Offset, SeekOrigin.Begin);
                            mFileStream.Write(bytes, 0, bytes.Length);
                            mFileStream.Flush();
                            mDiskAllocations[newFile.Offset] = newFile.Size;
                            // Update entry
                            block.Entries[i].Type = PackFileEntryType.File;
                            block.Entries[i].Name = newFile.Name;
                            var timeNow = DateTime.Now;
                            block.Entries[i].CreationTime = timeNow;
                            block.Entries[i].ModificationTime = timeNow;
                            block.Entries[i].Size = newFile.Size;
                            block.Entries[i].Offset = newFile.Offset;
                            UpdatePackFileBlock(blockOffset, block);
                            // File added success
                            folder.Files[newFile.Name] = newFile;
                            mFiles[path] = newFile;
                            return true;
                        }
                    }
                    // Continue reading next block or expand it
                    var nextBlock = block.Entries.Last().NextBlock;
                    // Expand chain if necessary
                    blockOffset = nextBlock == 0 ? ExpandPackFileBlock(blockOffset, block) : nextBlock;
                }
                // Entry was not able to be found
                return false;
            }
        }
        /// <summary>
        /// Removes a folder.
        /// Returns success.
        /// </summary>
        /// <exception cref="ArgumentException"/>
        public bool RemoveFolder(string path)
        {
            if (path == "")
                throw new ArgumentException("Root folder cannot be removed!");
            // Folder has to exists
            var folder = GetFolder(path);
            if (folder == null)
                return false;
            // Remove all links to the folders and files
            RemoveFolderLinks(folder);
            // Remove entry from parent block only so it breaks all the sub blocks
            var blockOffset = folder.Parent.Offset;
            while (blockOffset != 0)
            {
                var block = LoadPackFileBlock(blockOffset);
                for (int i = 0; i < block.Entries.Length; i++)
                {
                    if (block.Entries[i].Type == PackFileEntryType.Folder
                        && block.Entries[i].Offset == folder.Offset) // Easier find offset than normalize names
                    {
                        // Reset entry
                        block.Entries[i] = PackFileEntry.GetDefault();
                        // Update block
                        UpdatePackFileBlock(blockOffset, block);
                        // Remove from parent
                        folder.Parent.Folders.Remove(folder.Name);
                        return true;
                    }
                }
                // Continue reading chain until find it
                blockOffset = block.Entries.Last().NextBlock;
            }
            // Entry was not able to be found
            return false;
        }
        /// <summary>
        /// Removes a file.
        /// Returns success.
        /// </summary
        public bool RemoveFile(string path)
        {
            // File has to exists
            var file = GetFile(path);
            if (file == null)
                return false;
            // Normalize path
            path = path.ToLowerInvariant().Replace("/", Path.DirectorySeparatorChar.ToString());

            // Load and update block which contains the entry to this file
            var blockOffset = file.Parent.Offset;
            while (blockOffset != 0)
            {
                var block = LoadPackFileBlock(blockOffset);
                for (int i = 0; i < block.Entries.Length; i++)
                {
                    if (block.Entries[i].Type == PackFileEntryType.File
                        && block.Entries[i].Offset == file.Offset) // Easier find offset than normalize names
                    {
                        // Reset entry
                        block.Entries[i] = PackFileEntry.GetDefault();
                        // Update block
                        UpdatePackFileBlock(blockOffset, block);
                        // Remove from parent folder
                        file.Parent.Files.Remove(file.Name);
                        // Remove from dictionary
                        mFiles.Remove(path);
                        return true;
                    }
                }
                // Continue reading chain until find it
                blockOffset = block.Entries.Last().NextBlock;
            }
            // Entry was not able to be found
            return false;
        }
        /// <summary>
        /// Close the stream and free the resources.
        /// </summary>
        public void Dispose() => mFileStream?.Dispose();
        #endregion

        #region Private Helpers
        /// <summary>
        /// Creates the base from Pk2 file to work.
        /// </summary>
        private void CreateBaseStream()
        {
            // Header
            var header = PackFileHeader.GetDefault();
            var checksum = mBlowfish.Encode(Encoding.UTF8.GetBytes("Joymax Pack File"));
            Array.Copy(checksum, header.Checksum, 3);
            mFileStream.Write(header);
            mFileStream.Flush();
            // Setup block chain from root folder
            var offset = Marshal.SizeOf(typeof(PackFileHeader));
            PackFileBlock block = new PackFileBlock { Entries = new PackFileEntry[20] };
            for (int i = 0; i < block.Entries.Length; i++)
            {
                block.Entries[i] = PackFileEntry.GetDefault();
                if (i == 0)
                {
                    // Initialize root pointer
                    block.Entries[i].Type = PackFileEntryType.Folder;
                    block.Entries[i].Name = ".";
                    var timeNow = DateTime.Now;
                    block.Entries[i].CreationTime = timeNow;
                    block.Entries[i].ModificationTime = timeNow;
                    block.Entries[i].Offset = offset;
                }
            }
            UpdatePackFileBlock(offset, block);
            // Fill left bytes to match chunk size (4096)
            var bytes = new byte[4096 - mFileStream.Length];
            mFileStream.Write(bytes, 0, bytes.Length);
            mFileStream.Flush();
        }
        /// <summary>
        /// Initialize the block chains reading the minimal information to use the stream.
        /// </summary>
        private void InitializeStreamBlock(long offset, Pk2Folder parent)
        {
            var block = LoadPackFileBlock(offset);
            // Check all entries
            foreach (var entry in block.Entries)
            {
                switch (entry.Type)
                {
                    case PackFileEntryType.Empty:
                        break;
                    case PackFileEntryType.Folder:
                        // Ignore reading pointers to the block itself or parent block
                        if (entry.Name == "." || entry.Name == "..")
                            break;
                        // Add new folder to parent
                        var newFolder = new Pk2Folder(entry.Name, parent, entry.Offset);
                        parent.Folders.Add(entry.Name.ToLowerInvariant(), newFolder);
                        // Keep full path for a quick search
                        mFolders.Add(newFolder.GetFullPath(), newFolder);
                        // Track folder allocation
                        mDiskAllocations[entry.Offset] = Marshal.SizeOf(typeof(PackFileBlock));
                        // Continue reading folder
                        InitializeStreamBlock(entry.Offset, newFolder);
                        break;
                    case PackFileEntryType.File:
                        // Add new file to parent
                        var newFile = new Pk2File(entry.Name, parent, mFileStream, entry.Offset, entry.Size);
                        parent.Files.Add(entry.Name.ToLowerInvariant(), newFile);
                        // Keep full path for a quick search
                        mFiles.Add(newFile.GetFullPath(), newFile);
                        // Track file allocation
                        mDiskAllocations[entry.Offset] = entry.Size;
                        break;
                }
            }
            // Read next block from chain
            var nextBlock = block.Entries.Last().NextBlock;
            if (nextBlock != 0)
            {
                InitializeStreamBlock(nextBlock, parent);
                // Track folder chain allocation
                mDiskAllocations[nextBlock] = Marshal.SizeOf(typeof(PackFileBlock));
            }
        }
        /// <summary>
        /// Load and decode the PackFileBlock from offset
        /// </summary>
        private PackFileBlock LoadPackFileBlock(long offset)
        {
            // Set cursor position
            mFileStream.Seek(offset, SeekOrigin.Begin);
            // Read and decode block
            var bytes = new byte[Marshal.SizeOf(typeof(PackFileBlock))];
            mFileStream.Read(bytes, 0, bytes.Length);
            return ByteArrayHelpers.FromByteArray<PackFileBlock>(mBlowfish.Decode(bytes, 0, bytes.Length));
        }
        /// <summary>
        /// Encode and write a PackFileBlock at offset
        /// </summary>
        private void UpdatePackFileBlock(long offset, PackFileBlock block)
        {
            var bytes = ByteArrayHelpers.ToByteArray(block);
            bytes = mBlowfish.Encode(bytes);
            mFileStream.Seek(offset, SeekOrigin.Begin);
            mFileStream.Write(bytes, 0, bytes.Length);
            mFileStream.Flush();
        }
        /// <summary>
        /// Creates a new block on the block chain given.
        /// Returns the offset to the new block.
        /// </summary>
        private long ExpandPackFileBlock(long offset, PackFileBlock block)
        {
            var newBlockOffset = AllocateSpace(Marshal.SizeOf(typeof(PackFileBlock)));
            // Set up new block on chain
            PackFileBlock newBlock = new PackFileBlock { Entries = new PackFileEntry[block.Entries.Length] };
            for (int j = 0; j < newBlock.Entries.Length; j++)
                newBlock.Entries[j] = PackFileEntry.GetDefault();
            UpdatePackFileBlock(newBlockOffset, newBlock);
            // Track it
            mDiskAllocations[newBlockOffset] = Marshal.SizeOf(typeof(PackFileBlock));
            // Join the new block to the block chain
            block.Entries[block.Entries.Length - 1].NextBlock = newBlockOffset;
            UpdatePackFileBlock(offset, block);
            return newBlockOffset;
        }
        /// <summary>
        /// Search all the data available to allocate space in between otherwise create space at EOF.
        /// Returns the offset which contains the space.
        /// </summary>
        private long AllocateSpace(long Size)
        {
            // Sort all offsets
            var offsets = mDiskAllocations.Keys.ToList();
            for (int i = 0; i < offsets.Count; i++)
            {
                var allocationSize = mDiskAllocations[offsets[i]];
                var nextAllocation = offsets[i] + allocationSize;
                if (!mDiskAllocations.TryGetValue(nextAllocation, out _))
                {
                    // Check space available between this allocation and the next one
                    long availableSize = i + 1 == offsets.Count ? mFileStream.Length - nextAllocation : offsets[i + 1] - nextAllocation;
                    if (availableSize >= Size)
                        return nextAllocation;
                }
            }

            // Creates space at the end if no available space is found
            long allignedDiskSpace = (long)Math.Ceiling(Size / 4096f) * 4096L;
            byte[] bytes = new byte[allignedDiskSpace];
            mFileStream.Seek(0, SeekOrigin.End);
            var pos = mFileStream.Length;
            mFileStream.Write(bytes, 0, bytes.Length);
            mFileStream.Flush();
            return pos;
        }
        private void CreateFolderBlock(long offset, Pk2Folder parentFolder, List<string> paths)
        {
            var block = LoadPackFileBlock(offset);
            // Find an entry available on this block
            for (int i = 0; i < block.Entries.Length; i++)
            {
                if (block.Entries[i].Type == PackFileEntryType.Empty)
                {
                    var newFolder = new Pk2Folder(paths[0], parentFolder, AllocateSpace(Marshal.SizeOf(typeof(PackFileBlock))));
                    // Setup new block from folder
                    var timeNow = DateTime.Now;
                    PackFileBlock newBlock = new PackFileBlock { Entries = new PackFileEntry[block.Entries.Length] };
                    for (int j = 0; j < newBlock.Entries.Length; j++)
                    {
                        newBlock.Entries[j] = PackFileEntry.GetDefault();
                        if (j < 2)
                        {
                            // Initialize the pointers to itself and parent
                            newBlock.Entries[j].Type = PackFileEntryType.Folder;
                            newBlock.Entries[j].Name = j == 0 ? "." : "..";
                            newBlock.Entries[j].CreationTime = timeNow;
                            newBlock.Entries[j].ModificationTime = timeNow;
                            newBlock.Entries[j].Offset = j == 0 ? newFolder.Offset : parentFolder.Offset;
                        }
                    }
                    // Write block data & track it
                    UpdatePackFileBlock(newFolder.Offset, newBlock);
                    mDiskAllocations[newFolder.Offset] = Marshal.SizeOf(typeof(PackFileBlock));
                    // Update entry
                    block.Entries[i].Type = PackFileEntryType.Folder;
                    block.Entries[i].Name = newFolder.Name;
                    block.Entries[i].CreationTime = timeNow;
                    block.Entries[i].ModificationTime = timeNow;
                    block.Entries[i].Offset = newFolder.Offset;
                    UpdatePackFileBlock(offset, block);
                    // Folder added
                    parentFolder.Folders.Add(newFolder.Name, newFolder);
                    mFolders.Add(newFolder.GetFullPath(), newFolder);

                    // Continue creating folder if necessary
                    paths.RemoveAt(0);
                    if (paths.Count > 0)
                        CreateFolderBlock(newFolder.Offset, newFolder, paths);
                    return;
                }
            }

            // Continue reading next block or expand it
            var nextBlock = block.Entries.Last().NextBlock;
            // Expand chain if necessary
            offset = nextBlock == 0 ? ExpandPackFileBlock(offset, block) : nextBlock;
            // Continue reading next block
            CreateFolderBlock(offset, parentFolder, paths);
        }
        /// <summary>
        /// Removes a folder and subfolders with all their links to the stream.
        /// </summary>
        private void RemoveFolderLinks(Pk2Folder folder)
        {
            // Remove subfolders
            foreach (var kv in folder.Folders)
            {
                // Repeat cicle for sub folders
                RemoveFolderLinks(kv.Value);
            }
            // Remove all files
            foreach (var kv in folder.Files)
            {
                // Remove from dictionary
                mFiles.Remove(kv.Value.GetFullPath());
                // Remove space allocation
                mDiskAllocations.Remove(kv.Value.Offset);
            }
            // Remove from dictionary
            mFolders.Remove(folder.GetFullPath());
            // Remove the block space allocation
            mDiskAllocations.Remove(folder.Offset);
        }
        #endregion
    }
}
