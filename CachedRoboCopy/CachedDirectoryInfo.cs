//using RoboSharp.Extensions;
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;

//namespace RFBCodeWorks.CachedRoboCopy
//{
//    /// <summary>
//    /// Caches the Directory Info and enumerables for easy evaluation
//    /// </summary>
//    internal class CachedDirectoryInfo
//    {
//        public CachedDirectoryInfo(string directoryPath)
//        {
//            if (String.IsNullOrWhiteSpace(directoryPath)) throw new ArgumentException("Path is empty", nameof(directoryPath));
//            DirectoryInfo = new DirectoryInfo(directoryPath);
//            Refresh();
//        }

//        public event EventHandler Refreshed;

//        protected void OnRefreshed() => Refreshed?.Invoke(this, new EventArgs());

//        public CachedDirectoryInfo(DirectoryInfo directory)
//        {
//            DirectoryInfo = directory ?? throw new ArgumentNullException(nameof(directory));
//            Refresh();
//        }

//        /// <summary>
//        /// The underlying DirectoryInfo object
//        /// </summary>
//        public DirectoryInfo DirectoryInfo { get; }

//        public CachedEnumerable<CachedDirectoryInfo> SubDirectories { get; private set; }

//        public CachedEnumerable<FileInfo> Files { get; private set; }

//        public static implicit operator DirectoryInfo(CachedDirectoryInfo info) => info.DirectoryInfo;

//        public static implicit operator CachedDirectoryInfo(DirectoryInfo dir) => new CachedDirectoryInfo(dir);

//        /// <summary>
//        /// True if the <see cref="Files"/> enumerable has no objects, otherwise false
//        /// </summary>
//        public bool HasFiles => Files.Any();

//        /// <summary>
//        /// True if the <see cref="SubDirectories"/> enumerable has no objects, otherwise false
//        /// </summary>
//        public bool HasSubdirectories => SubDirectories.Any();

//        /// <summary>
//        /// True if has no files and no directories
//        /// </summary>
//        public bool IsEmpty => !HasFiles & !HasSubdirectories;

//        /// <summary>
//        /// Refresh the DirectoryInfo object and the reset the CachedEnumerable objects
//        /// </summary>
//        public void Refresh()
//        {
//            DirectoryInfo.Refresh();
//            SubDirectories = DirectoryInfo.EnumerateDirectories().Select(d => new CachedDirectoryInfo(d)).AsCachedEnumerable();
//            Files = DirectoryInfo.EnumerateFiles().AsCachedEnumerable();
//            OnRefreshed();
//        }
//    }
//}
