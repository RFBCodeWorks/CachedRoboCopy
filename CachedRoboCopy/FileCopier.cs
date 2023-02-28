using RoboSharp;
using RoboSharp.Interfaces;
using RoboSharp.Results;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using RFBCodeWorks.CachedRoboCopy.CopyFileEx;
using RoboSharp.Extensions;

namespace RFBCodeWorks.CachedRoboCopy
{
    /// <summary>
    /// An optimized File-Copier that reports file copy progress, and optimizes the 'Move' functionality if the file exists on the same drive.
    /// </summary>
    public class FileCopier : IFileCopier, INotifyPropertyChanged, IDisposable, IFilePair
    {

        #region < Constructors >

        /// <summary>
        /// Create a new RoboMoverItem by supplied file paths
        /// </summary>
        /// <param name="source">Fully Qualified Source File Path</param>
        /// <param name="destination">Fully Qualified Destination File Path</param>
        public FileCopier(string source, string destination)
        {
            //Source
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("No Destination Path Specified", nameof(source));
            if (!Path.IsPathRooted(source)) throw new ArgumentException("Path is not rooted", nameof(source));
            if (string.IsNullOrEmpty(Path.GetFileName(source))) throw new ArgumentException("No FileName Provided", nameof(source));

            //Destination
            if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("No Destination Path Specified", nameof(destination));
            if (!Path.IsPathRooted(destination)) throw new ArgumentException("Path is not rooted", nameof(destination));
            if (string.IsNullOrEmpty(Path.GetFileName(destination))) throw new ArgumentException("No FileName Provided", nameof(destination));

            Source = new FileInfo(source);
            Destination = new FileInfo(destination);
        }


        /// <summary>
        /// Create a new RoboMoverItem by supplied file paths
        /// </summary>
        /// <param name="source">FileInfo object that represents the source file</param>
        /// <param name="destination">FileInfo object that represents the destination file</param>
        public FileCopier(FileInfo source, FileInfo destination)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (destination is null) throw new ArgumentNullException(nameof(destination));

            Source = source;
            Destination = destination;
        }

        /// <inheritdoc cref="FileCopier.FileCopier(FileInfo, FileInfo)"/>
        public static FileCopier CreateNew(FileInfo source, FileInfo destination) => new FileCopier(source, destination);

        /// <summary>
        /// Create a new RoboMoverItem by supplied file paths
        /// </summary>
        /// <param name="source">FileInfo object that represents the source file</param>
        /// <param name="destinationDirectory">The Directory that the file will be copied into</param>
        public FileCopier(FileInfo source, DirectoryInfo destinationDirectory)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (destinationDirectory is null) throw new ArgumentNullException(nameof(destinationDirectory));
            Source = source;
            Destination = new FileInfo(Path.Combine(destinationDirectory.FullName, source.Name));
        }

        /// <summary>
        /// Used for synchronizing the items network to local
        /// </summary>
        public FileCopier(IFilePair FilePair)
        {
            if (FilePair is null) throw new ArgumentNullException(nameof(FilePair));
            Source = FilePair?.Source ?? throw new ArgumentNullException("Source");
            Destination = FilePair?.Destination ?? throw new ArgumentNullException("Destination");
        }

        #endregion

        #region < Events >

        #region < CopyProgressUpdated >

        /// <summary>
        /// Delegate for the FileCopyProgressUpdated event
        /// </summary>
        public delegate void CopyProgressUpdatedEventHandler(object sender, FileCopyProgressUpdatedEventArgs e);

        /// <inheritdoc cref="IFileCopier.CopyProgressUpdated"/>
        public event CopyProgressUpdatedEventHandler CopyProgressUpdated;

        /// <summary> Raises the FileCopyProgressUpdated event </summary>
        protected virtual void OnFileCopyProgressUpdated(double progress)
        {
            Progress = progress;
            CopyProgressUpdated?.Invoke(this, new FileCopyProgressUpdatedEventArgs(progress, Source, Destination, RoboSharpFileInfo, RoboSharpDirectoryInfo));
        }

        #endregion

        #region < FileCopyCompleted >

        /// <summary>
        /// Delegate for the FileCopyCompleted event
        /// </summary>
        public delegate void CopyCompletedEventHandler(object sender, FileCopyCompletedEventArgs e);

        /// <inheritdoc cref="IFileCopier.CopyCompleted"/>
        public event CopyCompletedEventHandler CopyCompleted;

        /// <summary> Raises the FileCopyCompleted event </summary>
        protected virtual void OnFileCopyCompleted()
        {
            CopyCompleted?.Invoke(this, new FileCopyCompletedEventArgs(Source, Destination, StartDate, EndDate, RoboSharpFileInfo, RoboSharpDirectoryInfo));
        }

        #endregion

        #region < FileCopyFailed >

        /// <summary>
        /// Delegate for the FileCopyFailed event
        /// </summary>
        public delegate void CopyFailedEventHandler(object sender, FileCopyFailedEventArgs e);

        /// <inheritdoc cref="IFileCopier.CopyFailed"/>
        public event CopyFailedEventHandler CopyFailed;

        /// <summary> Raises the FileCopyFailed event </summary>
        protected virtual void OnFileCopyFailed(string error = "", Exception e = null, bool cancelled = false, bool failed = false)
        {
            CopyFailed?.Invoke(this, new FileCopyFailedEventArgs(this, error, e, failed, cancelled));
        }

        #endregion

        #region < PropertyChanged >

        /// <summary>
        /// 
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary> Raises the PropertyChanged event </summary>
        protected virtual void OnPropertyChanged(string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// Set the property and raise PropertyChanged
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <param name="propertyName"/>
        protected void SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (!field.Equals(value))
            {
                field = value;
                OnPropertyChanged(propertyName);
            }
        }

        #endregion

        #endregion

        #region < Properties >

        private CancellationTokenSource CancellationSource;

        #region < File Info >

        /// <summary>
        /// The name of the file to be copied
        /// </summary>
        public string Name => Destination.Name;

        /// <summary>
        /// Information about the Source Path
        /// </summary>
        public FileInfo Source { get; }

        /// <summary>
        /// Information about the Destination Path
        /// </summary>
        public FileInfo Destination { get; }

        /// <summary>
        /// This object's FileInfo
        /// </summary>
        public RoboSharp.ProcessedFileInfo RoboSharpFileInfo { get; set; }
        
        /// <summary>
        /// The Parent's info
        /// </summary>
        public RoboSharp.ProcessedFileInfo RoboSharpDirectoryInfo { get; set; }

        /// <summary>
        /// Flag that can be set by consumers commands to log if the file should be copied or skipped
        /// </summary>
        public bool ShouldCopy { get; set; } = true;

        /// <summary>
        /// 
        /// </summary>
        public long Bytes => Source.Exists ? Source.Length : Destination.Length;

        #endregion

        #region < Progress Reporting >

        /// <summary>
        /// Copied Status -> True if the copy action has been performed.
        /// </summary>
        public bool IsCopied
        {
            get { return IsCopiedField; }
            set { SetProperty(ref IsCopiedField, value, nameof(IsCopied)); }
        }
        private bool IsCopiedField;

        /// <summary>
        /// TRUE if the task that performs the copy is currently running
        /// </summary>
        public bool IsCopying
        {
            get { return IsCopyingField; }
            set { SetProperty(ref IsCopyingField, value, nameof(IsCopying)); }
        }
        private bool IsCopyingField;


        /// <summary>
        /// TRUE if the copy operation was cancelled
        /// </summary>
        public bool WasCancelled
        {
            get { return WasCancelledField; }
            set { SetProperty(ref WasCancelledField, value, nameof(WasCancelled)); }
        }
        private bool WasCancelledField;


        /// <summary>
        /// 
        /// </summary>
        public double Progress
        {
            get { return ProgressField; }
            set { SetProperty(ref ProgressField, value, nameof(Progress)); }
        }
        private double ProgressField = 0;

        /// <summary>
        /// 
        /// </summary>
        public DateTime StartDate
        {
            get { return StartDateField; }
            private set { SetProperty(ref StartDateField, value, nameof(StartDate)); }
        }
        private DateTime StartDateField;


        /// <summary>
        /// 
        /// </summary>
        public DateTime EndDate
        {
            get { return EndDateField; }
            private set
            {
                SetProperty(ref EndDateField, value, nameof(EndDate));
                OnPropertyChanged(nameof(TimeToCompletion));
            }
        }
        private DateTime EndDateField;
        private bool disposedValue;

        /// <summary>
        /// 
        /// </summary>
        public TimeSpan TimeToCompletion
        {
            get
            {
                if (EndDate > StartDate) return EndDate - StartDate;
                return new TimeSpan();
            }
        }


        #endregion

        #endregion

        #region < Run the Copy Operation >

        /// <summary>
        /// Run the Copy Operation
        /// </summary>
        /// <param name="overWrite"></param>
        /// <param name="RaiseFailedEvent">TRUE to raise the Failed event after an exception from copying</param>
        /// <returns>True if the file was copied successfully, otherwise false</returns>
        private Task<bool> RunCopyOperation(bool overWrite, bool RaiseFailedEvent)
        {
            if (!overWrite && File.Exists(Destination.FullName))
            {
                OnFileCopyFailed("Destination file already exists", cancelled: true);
                return Task.FromResult(false);
            }
            
            if (IsCopying) throw new Exception("Copy Operation Already in progress!");

            CancellationSource = new CancellationTokenSource();
            StartDate = DateTime.Now;
            IsCopied = false;
            IsCopying = true;

            Task<bool> copyTask = Task.Run(() =>
           {
               Directory.CreateDirectory(Destination.DirectoryName);
               Destination.Delete();

               bool pbCancel = false;
               return FileCopyEx.CopyFile(Source.FullName, Destination.FullName, CopyProgressHandler, ref pbCancel, CopyFileFlags.COPY_FILE_RESTARTABLE);
               //return DoubleBufferCopy();
               //return SingleBufferCopy();
           
           }, CancellationSource.Token);

            Task<bool> continuation = copyTask.ContinueWith((result) =>
           {
               bool completed = result.IsCompleted && !result.IsFaulted && result.Result;
               if (!completed)
               {
                   Destination.Delete(); //Delete incomplete files!
               }
               IsCopying = false;
               IsCopied = completed;
               WasCancelled = CancellationSource.IsCancellationRequested;
               CancellationSource.Dispose();
               CancellationSource = null;
               EndDate = DateTime.Now;
               Destination.Refresh();
               if (completed)
                   OnFileCopyCompleted();
               else
               {
                   if (WasCancelled)
                       OnFileCopyFailed("Copy Operation Cancelled", cancelled: true);
                   else if (RaiseFailedEvent)
                   {
                       string copyFailedMessage = result.Exception == null ? "Copy Operation Failed" : result.Exception.Message;
                       OnFileCopyFailed(copyFailedMessage, result.Exception, failed: true);
                   }
               }
               return completed;
           }, TaskContinuationOptions.LongRunning);

            //copyTask.Start();
            return continuation;
        }

        /// <summary> Custom written copy operation that handles the read/write </summary>
        /// <returns> TRUE if successful, false is not </returns>
        /// <remarks> <see href="https://stackoverflow.com/questions/6044629/file-copy-with-progress-bar"/></remarks>
        /// Was providing approx 3-5 MegaBytes per minute in my testing
        private bool SingleBufferCopy()
        {
            byte[] buffer = new byte[1024 * 1024]; // 1MB buffer

            using (FileStream source = Source.OpenRead())
            {
                long fileLength = source.Length;
                if (Destination.Exists) Destination.Delete();
                using (FileStream dest = new FileStream(Destination.FullName, FileMode.CreateNew, FileAccess.Write))
                {
                    long totalBytes = 0;
                    int currentBlockSize = 0;

                    while ((currentBlockSize = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        totalBytes += currentBlockSize;
                        double percentage = (double)totalBytes * 100.0 / fileLength;

                        dest.Write(buffer, 0, currentBlockSize);

                        OnFileCopyProgressUpdated(percentage);
                        if (CancellationSource.IsCancellationRequested)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary> Custom written copy operation that handles the read/write </summary>
        /// <returns> TRUE if successful, false is not </returns>
        /// <remarks> <see href="https://stackoverflow.com/questions/6044629/file-copy-with-progress-bar"/></remarks>
        /// Was providing approx 3-5 MegaBytes per minute in my testing
        private bool DoubleBufferCopy()
        {
            const int bufferSize = 1024 * 1024;  //1MB
            byte[] buffer = new byte[bufferSize], buffer2 = new byte[bufferSize];
            bool swap = false;
            double reportedProgress = 0, progress;
            int read = 0;
            long len = Source.Length;    
            float flen = len;
            Task writer = null;

            Destination.Delete();
            using (var source = Source.OpenRead())
            using (var dest = Destination.OpenWrite())
            {
                dest.SetLength(source.Length);
                for (long size = 0; size < len; size += read)
                {
                    Destination.Directory.Create();
                    if (Source.Length == 0)
                    {
                        File.WriteAllBytes(Destination.FullName, Array.Empty<byte>());
                        OnFileCopyProgressUpdated(100);
                    }
                    else
                    {
                        long sizeWritten = 0, SourceLength = Source.Length;
                        bool flushed = false;
                        using (var dest = Destination.OpenWrite())
                        {

                        TryWrite:
                            try
                            {
                                while (sizeWritten < SourceLength && exceptionData is null)
                                {
                                    if (CancellationSource.IsCancellationRequested) break;
                                    if (IsPaused) Thread.Sleep(100);
                                    if (!BytesReadQueue.IsEmpty && BytesReadQueue.TryDequeue(out var tuple))
                                    {
                                        dest.Write(tuple.Item1, 0, tuple.Item2);
                                        sizeWritten += tuple.Item2;
                                        OnFileCopyProgressUpdated((double)sizeWritten / SourceLength * 100);
                                    }
                                    else
                                    {
                                        //wait for an item to hit the queue
                                        Thread.Sleep(25);
                                    }
                                }
                                if (!flushed)
                                {
                                    dest.Flush(); flushed = true;
                                    dest.Close();
                                }
                                if (Progress == 100 && SetAttributes != null)
                                {
                                    SetAttributes(Destination);
                                }

                            }
                            catch (Exception e)
                            {
                                if (tries < RetryOptions.RetryCount)
                                {
                                    Thread.Sleep(RetryOptions.GetWaitTime());
                                    tries++;
                                    goto TryWrite;
                                }
                                exceptionData = e;
                            }
                            finally
                            {
                                try { dest.Close(); } catch { }
                            }
                        }
                    }
                }
                writer?.Wait();  //Fixed - Thanks @sam-hocevar
                OnFileCopyProgressUpdated(100);
            }
            return true;
        }

        /// <summary>
        /// Process the callback from CopyFileEx
        /// </summary>
        /// <inheritdoc cref="FileCopyEx.CopyProgressRoutine"/>
        private CopyProgressResult CopyProgressHandler(long TotalFileSize, long TotalBytesTransferred, long StreamSize, long StreamBytesTransferred, uint dwStreamNumber, CopyProgressCallbackReason dwCallbackReason, IntPtr hSourceFile, IntPtr hDestinationFile, IntPtr lpData)
        {
            //when a chunk is finished call the progress changed.
            if (dwCallbackReason == CopyProgressCallbackReason.CALLBACK_CHUNK_FINISHED)
            {
                OnFileCopyProgressUpdated(TotalBytesTransferred / (double)TotalFileSize * 100.0);
            }
            return CancellationSource.IsCancellationRequested ? CopyProgressResult.PROGRESS_CANCEL : CopyProgressResult.PROGRESS_CONTINUE;
        }

        #endregion

        #region < Cancel >

        /// <summary>
        /// Determine if the copy operation can currently be cancelled
        /// </summary>
        /// <returns>TRUE if the operation is running and has not yet been cancelled. Otherwise false.</returns>
        public bool CanCancel() => !disposedValue && IsCopying && !(CancellationSource?.IsCancellationRequested ?? true);

        /// <summary>
        /// Determine if the copy operation can current be started
        /// </summary>
        /// <returns>TRUE if the operation can be started, FALSE is the object is disposed / currently copying</returns>
        public bool CanStart() => !disposedValue && !IsCopying;

        /// <summary>
        /// Request Cancellation immediately.
        /// </summary>
        public void Cancel()
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(FileCopier));
            if (CanCancel())
            {
                CancellationSource.Cancel();
            }
        }

        /// <summary>
        /// Request Cancellation after a number of <paramref name="milliseconds"/>
        /// </summary>
        public async void CancelAfter(int milliseconds)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(FileCopier));
            if (CanCancel())
            {
                await Task.Delay(milliseconds);
                Cancel();
            }
        }

        #endregion

        #region < Copy >

        /// <summary>
        /// Create a task that copies the file to the destination
        /// </summary>
        /// <inheritdoc cref="RunCopyOperation"/>
        public Task<bool> Copy(bool overwrite = true)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(FileCopier));
            return RunCopyOperation(overwrite, true);
        }

        /// <summary>
        /// Copies the file, trying multiple times per the <paramref name="options"/>
        /// </summary>
        /// <inheritdoc cref="Copy(bool)"/>
        /// <param name="options">retry options</param>
        /// <param name="SetAttributes">
        /// Action that will set the file attributes to to the file after it has been copied/moved
        /// <br/> For example: <see cref="PairEvaluator.ApplyAttributes(FileInfo)"/>
        /// </param>
        public async Task<bool> Copy(RetryOptions options, Action<FileInfo> SetAttributes = null)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(FileCopier));
            int tries = 0;
        TryAgain:
            try
            {
                tries++;
                bool copied = await RunCopyOperation(true, false);
                if (copied && SetAttributes != null)
                    SetAttributes(Destination);
                return copied;
            }
            catch when (!WasCancelled && tries < options.RetryCount)
            {
                await Task.Delay(options.GetWaitTime());
                goto TryAgain;
            }
            catch (Exception e)
            {
                OnFileCopyFailed(e.Message, e, failed: true);
                return false;
            }
        }

        #endregion

        #region < Move >

        /// <summary>
        /// Moves the file, trying multiple times per the <paramref name="options"/>
        /// </summary>
        /// <inheritdoc cref="Move(bool)"/>
        /// <inheritdoc cref="Copy(RetryOptions, Action{FileInfo})"/>
        public async Task<bool> Move(RetryOptions options, Action<FileInfo> SetAttributes = null)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(FileCopier));
            int tries = 0;
            bool moved = false;
        TryAgain:
            try
            {
                tries++;
                if (!moved)
                    moved = await Move(true, false);
        
                if (moved && SetAttributes != null)
                    SetAttributes(Destination);
                return moved;
            }
            catch when (!WasCancelled && tries < options.RetryCount)
            {
                await Task.Delay(options.GetWaitTime());
                goto TryAgain; 
            }
            catch(Exception e) when (!WasCancelled)
            {
                OnFileCopyFailed(e.Message, e, failed: true);
                return false;
            }
        }

        /// <summary>
        /// Move the file
        /// </summary>
        /// <inheritdoc cref="RunCopyOperation"/>
        public Task<bool> Move(bool overWrite = true)
        {
            return Move(overWrite, true);
        }

        
        private async Task<bool> Move(bool overWrite, bool RaiseFailedEvent)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(FileCopier));
            if (File.Exists(Destination.FullName) && !overWrite)
            {
                if (RaiseFailedEvent) OnFileCopyFailed("Destination file already exists", failed: true);
                return false;
            }
            if (!File.Exists(Source.FullName))
            {
                if (RaiseFailedEvent) OnFileCopyFailed("Source does not exist", failed: true);
                return false;
            }

            //Check if Source & Destination are on same physical drive
            string sourceRoot = Path.GetPathRoot(Source.FullName);
            string destRoot = Path.GetPathRoot(Destination.FullName);
            if (sourceRoot.Equals(destRoot, comparisonType: StringComparison.InvariantCultureIgnoreCase))
            {
                //Delete the file at the destination, then move
                StartDate = DateTime.Now;
                IsCopied = false;
                IsCopying = true;
                bool copied = false;
                try
                {
                    if (File.Exists(Destination.FullName)) Destination.Delete();
                    File.Move(Source.FullName, Destination.FullName);
                    copied = true;
                }
                catch (Exception e) when (RaiseFailedEvent)
                {
                    EndDate = DateTime.Now;
                    OnFileCopyFailed(e.Message, e, failed: true);
                }
                finally
                {
                    IsCopying = false;
                    IsCopied = copied;
                    if (copied)
                    {
                        EndDate = DateTime.Now;
                        Source.Refresh();
                        Destination.Refresh();
                        OnFileCopyProgressUpdated(100);
                        OnFileCopyCompleted();
                    }
                }
                return copied;
            }

            //Source/Dest on different drives : Copy with progress
            var copyTask = RunCopyOperation(overWrite, RaiseFailedEvent);
            bool wasSuccess = await copyTask;
            if (WasCancelled) return wasSuccess;
            if (wasSuccess && IsCopied && !copyTask.IsFaulted)
            {
                try
                {
                    Source.Delete();
                }
                catch (Exception e) when (RaiseFailedEvent)
                {
                    OnFileCopyFailed($"Deletion of Source File Failed -- {Source.FullName}{Environment.NewLine}{e.Message}");
                }
                Source.Refresh();
            }
            return wasSuccess;
        }

        #endregion

        #region < Dispose >

        /// <inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)   
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                Cancel();
                CancellationSource?.Dispose();
                CancellationSource = null;
                
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        /// <summary>
        /// 
        /// </summary>
        ~FileCopier()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}