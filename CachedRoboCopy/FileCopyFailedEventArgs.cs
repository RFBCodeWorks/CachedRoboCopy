using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RFBCodeWorks.CachedRoboCopy
{
    /// <summary>
    /// Args thrown when a copy operation fails
    /// </summary>
    public class FileCopyFailedEventArgs : EventArgs
    {
        /// <summary>
        /// 
        /// </summary>
        public FileCopyFailedEventArgs(FileCopier copier, string error, bool failed = true, bool cancelled = false)
        {
            Source = copier.Source;
            Destination = copier.Destination;
            Error = error;
            //WasSkipped = skipped;
            WasFailed = failed;
            WasCancelled = cancelled;
        }

        /// <summary>
        /// Source File
        /// </summary>
        public FileInfo Source { get; }

        /// <summary>
        /// Destination File
        /// </summary>
        public FileInfo Destination { get; }

        /// <summary>
        /// File copy failed
        /// </summary>
        public bool WasFailed { get; }


        /// <summary>
        /// File copy was cancelled
        /// </summary>
        public bool WasCancelled { get; }


        /// <summary>
        /// Error Text
        /// </summary>
        public string Error { get; }

    }
}
