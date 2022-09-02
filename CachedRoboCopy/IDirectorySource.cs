using RoboSharp.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using RoboSharp.Interfaces;

namespace RFBCodeWorks.CachedRoboCopy
{
    /// <summary>
    /// An object that represents a directory that can be copied from
    /// </summary>
    public interface IDirectorySource
    {
        /// <summary>
        /// The full path to the Source Directory to copy files from
        /// </summary>
        public string SourceDirectoryPath { get; }
    }

    /// <summary>
    /// Extension Methods for an IDirectorySource object
    /// </summary>
    public static class IDirectorySourceExtensions
    {
        private static T CreateFile<T>(this IDirectorySource directory, string fileName, Func<string, T> ctor) where T: IFileSource
        {
            return ctor(Path.Combine(directory.SourceDirectoryPath, Path.GetFileName(fileName)));
        }

        private static T CreateDirectory<T>(this IDirectorySource directory, string fileName, Func<string, T> ctor) where T : IDirectorySource
        {
            return ctor(Path.Combine(directory.SourceDirectoryPath, Path.GetFileName(fileName)));
        }

        public static CachedEnumerable<T> EnumerateFileSources<T>(this IDirectorySource source, Func<string, T> ctor) where T : IFileSource
        {
            return Directory.EnumerateFiles(source.SourceDirectoryPath).Select(f => CreateFile(source, f, ctor)).AsCachedEnumerable();
        }

        public static CachedEnumerable<T> EnumerateDirectorySources<T>(this IDirectorySource source, Func<string, T> ctor) where T : IDirectorySource
        {
            return Directory.EnumerateFiles(source.SourceDirectoryPath).Select(f => CreateDirectory(source, f, ctor)).AsCachedEnumerable();
        }

        /// <inheritdoc cref="RoboSharp.Extensions.DirectoryPairExtensions.EnumerateSourceFilePairs{T}(RoboSharp.Extensions.IDirectoryPair, Func{FileInfo, FileInfo, T})"/>
        public static CachedEnumerable<T> EnumerateFileCopiers<T>(this IDirectorySource source, string destination, Func<FileInfo, FileInfo, T> ctor) where T : RoboSharp.Extensions.IFilePair
        {
            var dirPair = new RoboSharp.Extensions.DirectoryPair(new DirectoryInfo(source.SourceDirectoryPath), new DirectoryInfo(destination));
            return dirPair.EnumerateSourceFilePairs(ctor);
        }

        public static T CreateRoboCommand<T>(this IDirectorySource source, string destination, Func<T> ctor) where T : IRoboCommand
        {
            var rc = ctor();
            rc.CopyOptions.Source = source.SourceDirectoryPath;
            rc.CopyOptions.Destination = destination;
            return rc;
        }

        public static T CreateRoboCommand<T>(this IDirectorySource source, DirectoryInfo destination, Func<T> ctor) where T : IRoboCommand
            => CreateRoboCommand(source, destination.FullName, ctor);
    }
}
