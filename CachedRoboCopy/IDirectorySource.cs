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
}
