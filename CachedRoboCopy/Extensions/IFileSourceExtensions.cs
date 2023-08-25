using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using RFBCodeWorks.RoboSharpExtensions;
using System.IO;

namespace RFBCodeWorks.RoboSharpExtensions.Extensions
{

    /// <summary>
    /// Extension Methods for an IFileSource object
    /// </summary>
    public static class IFIleSourceExtensions
    {
        /// <inheritdoc cref="File.Exists(string)"/>
        public static bool Exists(this IFileSource file) => File.Exists(file.SourceFilePath);

        /// <summary>
        /// Create a new IFileCopier by specifying a IFileSource's new destination folder
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">the source file path</param>
        /// <param name="destinationFolder">the folder path to copy the file into</param>
        /// <param name="ctor">
        /// Constructor that has the accepts the Source and the Destination as a parameters 
        /// <br/> - Source should be first parameter
        /// <br/> - Destination should be second parameter
        /// </param>
        /// <returns>new <see cref="IFileCopier"/></returns>
        public static T CreateFileCopier<T>(this IFileSource source, string destinationFolder, Func<FileInfo, FileInfo, T> ctor) where T : IFileCopier
        {
            var sourceFile = new FileInfo(source.SourceFilePath);
            var destFile = new FileInfo(Path.Combine(destinationFolder, Path.GetFileName(source.SourceFilePath)));
            return ctor(sourceFile, destFile);
        }

        /// <inheritdoc cref="CreateFileCopier{T}(IFileSource, string, Func{FileInfo, FileInfo, T})"/>
        public static T CreateFileCopier<T>(this IFileSource source, DirectoryInfo destinationFolder, Func<FileInfo, FileInfo, T> ctor) where T : IFileCopier
            => CreateFileCopier(source, destinationFolder.FullName, ctor);

        /// <summary>
        /// Take a collection of <paramref name="fileCopiers"/> and add them to a new <see cref="FileCopierCommand"/>
        /// </summary>
        /// <param name="fileCopiers">the collection of copiers</param>
        /// <returns>a new <see cref="FileCopierCommand"/></returns>
        public static FileCopierCommand CreateFileCopierCommand(this IEnumerable<IFileCopier> fileCopiers)
        {
            return new FileCopierCommand(fileCopiers);
        }
    }
}
