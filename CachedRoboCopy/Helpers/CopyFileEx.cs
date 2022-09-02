using System;
using System.Collections.Generic;
using System.IO;
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
    ///  Move File with progress: https://docs.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefilewithprogressa
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
            return RunCopyFile(sourceFile, destFile, CopyProgressHandler, ref pbCancel, flags);
        }

        /// <inheritdoc cref="CopyFileEx"/>
        public static bool CopyFileIfMissing(string sourceFile, string destFile, CopyProgressRoutine CopyProgressHandler, ref bool pbCancel, CopyFileFlags flags = CopyFileFlags.COPY_FILE_RESTARTABLE | CopyFileFlags.COPY_FILE_FAIL_IF_EXISTS)
        {
            return RunCopyFile(sourceFile, destFile, CopyProgressHandler, ref pbCancel, flags);
        }

        /// <summary>
        /// Runs the copy task, marshals the error, then returns the result
        /// </summary>
        private static bool RunCopyFile(string sourceFile, string destFile, CopyProgressRoutine CopyProgressHandler, ref bool pbCancel, CopyFileFlags flags)
        {
            //Check for locked file prior to starting the write process
            using (var stream = File.OpenWrite(destFile))
                stream.Close();

            bool returnVal = CopyFileEx(sourceFile, destFile, CopyProgressHandler ?? DefaultHandler, IntPtr.Zero, ref pbCancel, (int)flags);

            //https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.dllimportattribute.setlasterror?view=net-6.0
            int errorCode = 0;
#if Net6OrGreater   // VS2019 doesn't support Net6, but this will be required for future compatibility
            errorCode = Marshal.GetLastPInvokeError();
#else
            // Get the last error and display it.
            errorCode = Marshal.GetLastWin32Error();
#endif
            ThrowWin32Error(errorCode, sourceFile, destFile);
            return returnVal;
        }

        #endregion

        ///<summary>
        /// Look up the Win32 error code and throw the appropriate exception. If not defined in the statement, throw a generic exception with the fault code.
        /// </summary>
        /// <remarks>
        /// <see href="https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-erref/18d8fbe8-a967-4f1c-ae50-99ca8e491d2d"/>
        /// </remarks>
        public static void ThrowWin32Error(int errorCode, string sourceFile, string destFile)
        {
            switch(errorCode)
            {
                case 0: return;
                case 1: throw new InvalidOperationException("Invalid Operation");
                case 2: throw new FileNotFoundException(message: "Unable to locate the file", fileName: sourceFile);
                case 3: throw new DirectoryNotFoundException("Unable to locate the directory: " + Path.GetDirectoryName(destFile));
                case 4: throw new FileNotFoundException("Unable to open the file: ", sourceFile);
                case 8: throw new InsufficientMemoryException();
                case 0x0000000E: throw new InsufficientMemoryException("Not enough storage is available to complete this operation.");
                case 0x0000000F: throw new DriveNotFoundException("The system cannot find the drive specified.");
                case 0x00000013: throw new UnauthorizedAccessException("The media is write-protected.");
                case 0x00000014: throw new DriveNotFoundException("The system cannot find the device specified.");
                case 0x00000015: throw new IOException("The device is not ready.");
                case 0x00000027: throw new IOException("The destination disk is full: " + Path.GetPathRoot(destFile));
                case 0x00000032: throw new IOException(); // Occurs when the file is locked
                default: throw new IOException(@$"CopyFileEx Error Code: {errorCode}{Environment.NewLine} See https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-erref/18d8fbe8-a967-4f1c-ae50-99ca8e491d2d for details");
            };
    }
    }
}
