using RoboSharp.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RFBCodeWorks.CachedRoboCopy.FileCopier;

namespace RFBCodeWorks.CachedRoboCopy
{
    interface IFileCopier
    {
        /// <summary>
        /// Occurs when the copy progress is updated
        /// </summary>
        public event FileCopyProgressUpdatedHandler FileCopyProgressUpdated;

        /// <summary>
        /// Occurs when a file copy is successfully completed
        /// </summary>
        public event FileCopyCompletedHandler FileCopyCompleted;

        /// <summary>
        /// Occurs when a file copy Fails, or is skipped
        /// </summary>
        public event FileCopyFailedHandler FileCopyFailed;

        /// <summary>
        /// Copy the File(s) to their destination
        /// </summary>
        /// <param name="overWrite">OverWrite the files in the destination if they already exist. Set false if overwriting existing files should not occur.</param>
        /// <returns>
        /// Task that completes when the operation is either successfull, or fails. <br/>
        /// A TRUE result means copy operation performed successfully, FALSE means file was not copied.
        /// </returns>
        public Task<bool> Copy(bool overWrite = true);

        /// <summary>
        /// Move the File(s) to their destination
        /// </summary>
        /// <param name="overWrite"><inheritdoc cref="Copy(bool)" path="/param[@name='overWrite']" /></param>
        /// <returns>
        /// Task that completes when the operation is either successfull, or fails. <br/>
        /// A TRUE result means move operation performed successfully, FALSE means file was not moved.
        /// </returns>
        public Task<bool> Move(bool overWrite = true);

        /// <summary>
        /// Cancel the Copy Operation
        /// </summary>
        public void Cancel();
    }
}
