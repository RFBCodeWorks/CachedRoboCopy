using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using RoboSharp.Extensions;

namespace RFBCodeWorks.RoboSharpExtensions
{
    /// <summary>
    /// Interface that allows creation of IFileCopier objects
    /// </summary>
    public interface IFileCopierFactory
    {
        /// <inheritdoc cref="FileCopier.FileCopier(string, string)"/>
        IFileCopier CreateFileCopier(string source, string destination);

        /// <inheritdoc cref="FileCopier.FileCopier(FileInfo, DirectoryInfo)"/>
        IFileCopier CreateFileCopier(string source, DirectoryInfo destination);

        /// <inheritdoc cref="FileCopier.FileCopier(FileInfo, FileInfo)"/>
        IFileCopier CreateFileCopier(FileInfo source, FileInfo destination);

        /// <inheritdoc cref="FileCopier.FileCopier(FileInfo, DirectoryInfo)"/>
        IFileCopier CreateFileCopier(FileInfo source, DirectoryInfo destination);

        /// <inheritdoc cref="FileCopier.FileCopier(IFilePair)"/>
        IFileCopier CreateFileCopier(IFilePair filePair);
    }

    /// <summary>
    /// Default implentation of the <see cref="IFileCopierFactory"/> interface that produces <see cref="FileCopier"/> objects
    /// </summary>
    public class FileCopierFactory : IFileCopierFactory
    {
        
        /// <inheritdoc/>
        public virtual IFileCopier CreateFileCopier(string source, string destination)
        {
            return new FileCopier(source, destination);
        }

        /// <inheritdoc/>
        public virtual IFileCopier CreateFileCopier(string source, DirectoryInfo destination)
        {
            return new FileCopier(new FileInfo(source), destination);
        }

        /// <inheritdoc/>
        public virtual IFileCopier CreateFileCopier(FileInfo source, FileInfo destination)
        {
            return new FileCopier(source, destination);
        }

        /// <inheritdoc/>
        public virtual IFileCopier CreateFileCopier(FileInfo source, DirectoryInfo destination)
        {
            return new FileCopier(source, destination);
        }

        /// <inheritdoc/>
        public virtual IFileCopier CreateFileCopier(IFilePair filePair)
        {
            return CreateFileCopier(filePair.Source, filePair.Destination);
        }
    }
}
