using System;
using System.Collections.Generic;
using System.Text;

namespace RFBCodeWorks.RoboSharpExtensions
{
    /// <summary>
    /// An object that represents a File that can be copied elsewhere
    /// </summary>
    public interface IFileSource
    {
        /// <summary>
        /// The full path to the Source Directory to copy files from
        /// </summary>
        public string SourceFilePath { get; }
    }

}
