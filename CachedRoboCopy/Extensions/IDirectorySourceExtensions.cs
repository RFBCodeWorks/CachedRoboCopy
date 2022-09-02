using RoboSharp.Extensions;
using RoboSharp.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RFBCodeWorks.CachedRoboCopy.Extensions
{
    /// <summary>
    /// Extension Methods for an IDirectorySource object
    /// </summary>
    public static class IDirectorySourceExtensions
    {
        private static T CreateFile<T>(this IDirectorySource directory, string fileName, Func<string, T> ctor) where T : IFileSource
        {
            return ctor(Path.Combine(directory.SourceDirectoryPath, Path.GetFileName(fileName)));
        }

        private static T CreateDirectory<T>(this IDirectorySource directory, string fileName, Func<string, T> ctor) where T : IDirectorySource
        {
            return ctor(Path.Combine(directory.SourceDirectoryPath, Path.GetFileName(fileName)));
        }

        /// <inheritdoc cref="Directory.Exists(string)"/>
        public static bool Exists(IDirectorySource source) => Directory.Exists(source.SourceDirectoryPath);


        #region < Enumerate Files >

        /// <summary>
        /// Enumerate the files within the directory as new <see cref="IFileSource"/> objects
        /// </summary>
        /// <typeparam name="T">the generated type</typeparam>
        /// <param name="source">the source directory</param>
        /// <param name="ctor">the constructor to use</param>
        /// <returns>new <see cref="CachedEnumerable{T}"/> of <see cref="IFileSource"/> objects</returns>
        public static CachedEnumerable<T> EnumerateFileSources<T>(this IDirectorySource source, Func<string, T> ctor) where T : IFileSource
        {
            return Directory.EnumerateFiles(source.SourceDirectoryPath).Select(f => CreateFile(source, f, ctor)).AsCachedEnumerable();
        }

        /// <summary>
        /// Enumerates the File Copiers from this source and destination
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">the source folder</param>
        /// <param name="destination">the destination folder</param>
        /// <param name="ctor">the constructor to generate the FileCopier</param>
        /// <returns></returns>
        public static CachedEnumerable<T> EnumerateFileCopiers<T>(this IDirectorySource source, string destination, Func<FileInfo, FileInfo, T> ctor) where T : RoboSharp.Extensions.IFilePair
        {
            var dirPair = new RoboSharp.Extensions.DirectoryPair(new DirectoryInfo(source.SourceDirectoryPath), new DirectoryInfo(destination));
            return dirPair.Source.EnumerateFiles()
                .Select(fn => CreateT(fn))
                .AsCachedEnumerable();

            T CreateT(FileInfo file)
            {
                string dn = file.FullName.Replace(dirPair.Source.FullName, dirPair.Destination.FullName);
                return ctor(file, new(dn));
            }
        }

        /// <inheritdoc cref="EnumerateFileCopiers{T}(IDirectorySource, string, Func{FileInfo, FileInfo, T})"/>
        public static CachedEnumerable<FileCopier> EnumerateFileCopiers(this IDirectorySource source, string destination)
            => EnumerateFileCopiers<FileCopier>(source, destination, (o, e) => new FileCopier(o, e));

        #endregion

        #region < Enumerate Directories >

        /// <summary>
        /// Enumerate the directories within the directory as new <see cref="IDirectorySource"/> objects
        /// </summary>
        /// <typeparam name="T">the generated type</typeparam>
        /// <param name="source">the source directory</param>
        /// <param name="ctor">the constructor to use</param>
        /// <returns>new <see cref="CachedEnumerable{T}"/> of <see cref="IDirectorySource"/> objects.</returns>
        public static CachedEnumerable<T> EnumerateDirectorySources<T>(this IDirectorySource source, Func<string, T> ctor) where T : IDirectorySource
        {
            return Directory.EnumerateFiles(source.SourceDirectoryPath).Select(f => CreateDirectory(source, f, ctor)).AsCachedEnumerable();
        }

        #endregion


        /// <summary>
        /// Create a new IRoboCommand by specifying the destination directory
        /// </summary>
        /// <typeparam name="T">Type of IRoboCommand</typeparam>
        /// <param name="source">Source Directory</param>
        /// <param name="destination">Destination Directory Path</param>
        /// <param name="ctor">Constructor for the IRoboCommand</param>
        /// <returns></returns>
        public static T CreateRoboCommand<T>(this IDirectorySource source, string destination, Func<T> ctor) where T : IRoboCommand
        {
            var rc = ctor();
            rc.CopyOptions.Source = source.SourceDirectoryPath;
            rc.CopyOptions.Destination = destination;
            return rc;
        }

        /// <inheritdoc cref="CreateRoboCommand{T}(IDirectorySource, string, Func{T})"/>
        public static T CreateRoboCommand<T>(this IDirectorySource source, DirectoryInfo destination, Func<T> ctor) where T : IRoboCommand
            => CreateRoboCommand(source, destination.FullName, ctor);
    }
}
