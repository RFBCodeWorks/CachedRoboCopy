using RoboSharp;
using RoboSharp.Extensions;
using RoboSharp.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RFBCodeWorks.RoboSharpExtensions.FileCopier;

namespace RFBCodeWorks.RoboSharpExtensions
{
    /// <summary>
    /// Interface for objects that allow copying/moving with events for success or failure.
    /// </summary>
    public interface IFileCopier : IFilePair
    {

        #region < Events >

        /// <summary>
        /// Occurs when the copy progress is updated
        /// </summary>
        public event CopyProgressUpdatedEventHandler CopyProgressUpdated;

        /// <summary>
        /// Occurs when a file copy/move is successfully completed
        /// </summary>
        public event CopyCompletedEventHandler CopyCompleted;

        /// <summary>
        /// Occurs when a file copy/move operation fails. May contain exception information.
        /// </summary>
        public event CopyFailedEventHandler CopyFailed;

        #endregion

        #region < Properties >

        /// <inheritdoc cref="FileCopier.IsCopying"/>
        public bool IsCopying { get; }

        /// <inheritdoc cref="FileCopier.WasCancelled"/>
        public bool WasCancelled { get; }

        /// <inheritdoc cref="FileCopier.ShouldCopy"/>
        public bool ShouldCopy { get; set; }

        /// <inheritdoc cref="FileCopier.RoboSharpFileInfo"/>
        public ProcessedFileInfo RoboSharpFileInfo { get; set; }

        /// <inheritdoc cref="FileCopier.RoboSharpDirectoryInfo"/>
        public ProcessedFileInfo RoboSharpDirectoryInfo { get; set; }

        /// <inheritdoc cref="FileCopier.RetryOptions"/>
        public RetryOptions RetryOptions { get; set; }

        #endregion

        #region < Methods >

        /// <summary>
        /// Copy the File(s) to their destination
        /// </summary>
        /// <param name="overWrite">OverWrite the files in the destination if they already exist. Set false if overwriting existing files should not occur.</param>
        /// <returns>
        /// Task that completes when the operation is either successfull, or fails. <br/>
        /// A TRUE result means copy operation performed successfully, FALSE means file was not copied.
        /// </returns>
        public Task<bool> Copy(bool overWrite = false);

        /// <inheritdoc cref="FileCopier.Copy(bool)"/>
        public Task<bool> Copy(bool overWrite, Action<FileInfo> SetAttributes = null);

        /// <summary>
        /// Move the File(s) to their destination
        /// </summary>
        /// <param name="overWrite"><inheritdoc cref="Copy(bool)" path="/param[@name='overWrite']" /></param>
        /// <returns>
        /// Task that completes when the operation is either successfull, or fails. <br/>
        /// A TRUE result means move operation performed successfully, FALSE means file was not moved.
        /// </returns>
        public Task<bool> Move(bool overWrite = false);

        /// <inheritdoc cref="FileCopier.Move(bool, Action{FileInfo})"/>
        public Task<bool> Move(bool overWrite, Action<FileInfo> SetAttributes = null);

        /// <summary>
        /// Cancel the Copy/Move Operation
        /// </summary>
        public void Cancel();

        #endregion

    }
}
