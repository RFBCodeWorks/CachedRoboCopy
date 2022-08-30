using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RFBCodeWorks.CachedRoboCopy.CopyFileEx
{

    #region < Enums >

    /// <summary>
    /// Flags to pass into CopyFileEx to determine how it should run
    /// </summary>
    [Flags]
    public enum CopyFileFlags : uint
    {
        /// <summary>
        /// Fail to copy if the destination file exists
        /// </summary>
        COPY_FILE_FAIL_IF_EXISTS = 0x00000001,
        /// <summary>
        /// Required to report progress
        /// </summary>
        COPY_FILE_RESTARTABLE = 0x00000002,
        /// <summary>
        /// 
        /// </summary>
        COPY_FILE_OPEN_SOURCE_FOR_WRITE = 0x00000004,
        /// <summary>
        /// 
        /// </summary>
        COPY_FILE_ALLOW_DECRYPTED_DESTINATION = 0x00000008
    }

    /// <summary>
    /// Event description from CopyFileEx (why its performing the callback)
    /// </summary>
    public enum CopyProgressCallbackReason : uint
    {
        /// <summary>
        /// Copy Progress Updated!
        /// </summary>
        CALLBACK_CHUNK_FINISHED = 0x00000000,
        /// <summary>
        /// ?
        /// </summary>
        CALLBACK_STREAM_SWITCH = 0x00000001
    }

    /// <summary>
    /// The result of the callback to be evaluated by CopyFileEx
    /// </summary>
    public enum CopyProgressResult : uint
    {
        /// <summary>
        /// Allow CopyFileEx to continue
        /// </summary>
        PROGRESS_CONTINUE = 0,
        /// <summary>
        /// Request Cancellation
        /// </summary>
        PROGRESS_CANCEL = 1,
        /// <summary>
        /// Stop reporting progress?
        /// </summary>
        PROGRESS_STOP = 2,
        /// <summary>
        /// Stop reporting progress? 
        /// </summary>
        PROGRESS_QUIET = 3
    }

    ///// <summary>
    ///// 
    ///// </summary>
    //[Flags]
    //public enum MoveFileFlags : uint
    //{
    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    MOVE_FILE_REPLACE_EXISTSING = 0x00000001,
    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    MOVE_FILE_COPY_ALLOWED = 0x00000002,
    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    MOVE_FILE_DELAY_UNTIL_REBOOT = 0x00000004,
    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    MOVE_FILE_WRITE_THROUGH = 0x00000008,
    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    MOVE_FILE_CREATE_HARDLINK = 0x00000010,
    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    MOVE_FILE_FAIL_IF_NOT_TRACKABLE = 0x00000020
    //}

    /// <summary>
    /// 
    /// </summary>
    public enum SymbolicLinkFlags : uint
    {
        /// <summary>
        /// 
        /// </summary>
        SYMBLOC_LINK_FLAG_FILE = 0x0,
        /// <summary>
        /// 
        /// </summary>
        SYMBLOC_LINK_FLAG_DIRECTORY = 0x1
    }

    #endregion

    /// <summary>
    /// CopyFileEx if a file copy engine that reports copy progress - class doesn't match name due to naming convention errors
    /// </summary>
    public static class FileCopyEx
    {
        #region < Setup CopyFileEx >

        /// <summary>
        /// CopyFileEx is a copy utility that shows progress
        /// </summary>
        /// <param name="lpExistingFileName">Source FilePath </param>
        /// <param name="lpNewFileName">Destination File Path</param>
        /// <param name="lpProgressRoutine">Progress Reporter Call-Back</param>
        /// <param name="lpData">IntPtr.Zero</param>
        /// <param name="pbCancel">Boolean to trigger cancellation</param>
        /// <param name="dwCopyFlags">Copy Flags</param>
        /// <returns>TRUE if the copy operation completed</returns>
        [DllImport("kernel32", SetLastError = true)]
        private static extern bool CopyFileEx(string lpExistingFileName, string lpNewFileName, CopyProgressRoutine lpProgressRoutine, IntPtr lpData, ref bool pbCancel, int dwCopyFlags);

        /// <summary>
        /// Default Handler just always states to continue copying
        /// </summary>
        private static CopyProgressResult DefaultHandler(long total, long transferred, long streamSize, long streamByteTrans, uint dwStreamNumber,
                                                CopyProgressCallbackReason reason, IntPtr hSourceFile, IntPtr hDestinationFile, IntPtr lpData) => CopyProgressResult.PROGRESS_CONTINUE;

        #endregion

        #region < Delegate >

        /// <summary>
        /// Handle the CallBack requested by CopyFileEx
        /// </summary>
        /// <param name="TotalFileSize">Total File Size to be copied (bytes)</param>
        /// <param name="TotalBytesTransferred">Total number of bytes transfered</param>
        /// <param name="StreamSize"></param>
        /// <param name="StreamBytesTransferred"></param>
        /// <param name="dwStreamNumber"></param>
        /// <param name="dwCallbackReason"><inheritdoc cref="CopyProgressCallbackReason"/></param>
        /// <param name="hSourceFile">pointer to the source file - DO Nothing With!</param>
        /// <param name="hDestinationFile">pointer to the destination file - DO Nothing With!</param>
        /// <param name="lpData">pointer - DO Nothing With!</param>
        /// <returns><inheritdoc cref="CopyProgressResult"/></returns>
        public delegate CopyProgressResult CopyProgressRoutine(long TotalFileSize, long TotalBytesTransferred, long StreamSize, long StreamBytesTransferred, uint dwStreamNumber, CopyProgressCallbackReason dwCallbackReason,
                                                IntPtr hSourceFile, IntPtr hDestinationFile, IntPtr lpData);

        #endregion

        #region < Public Methods >

        /// <inheritdoc cref="CopyFileEx"/>
        public static bool CopyFile(string sourceFile, string destFile, CopyProgressRoutine CopyProgressHandler, ref bool pbCancel, CopyFileFlags flags = CopyFileFlags.COPY_FILE_RESTARTABLE)
        {
            return CopyFileEx(sourceFile, destFile, CopyProgressHandler ?? DefaultHandler, IntPtr.Zero, ref pbCancel, (int)flags);
        }

        /// <inheritdoc cref="CopyFileEx"/>
        public static bool CopyFileIfMissing(string sourceFile, string destFile, CopyProgressRoutine CopyProgressHandler, ref bool pbCancel, CopyFileFlags flags = CopyFileFlags.COPY_FILE_RESTARTABLE | CopyFileFlags.COPY_FILE_FAIL_IF_EXISTS)
        {
            return CopyFileEx(sourceFile, destFile, CopyProgressHandler ?? DefaultHandler, IntPtr.Zero, ref pbCancel, (int)flags);
        }

        #endregion
    }
}
