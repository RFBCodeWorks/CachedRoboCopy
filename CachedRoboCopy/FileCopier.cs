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
using System.Collections.Concurrent;

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
            if (CopyProgressUpdated != null)
            {
                var args = new FileCopyProgressUpdatedEventArgs(progress, this, RoboSharpFileInfo, RoboSharpDirectoryInfo);
                this.RoboSharpFileInfo ??= args.RoboSharpFileInfo;
                this.RoboSharpDirectoryInfo ??= args.RoboSharpDirInfo;
                CopyProgressUpdated?.Invoke(this, args);
            }
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
            CopyCompleted?.Invoke(this, new FileCopyCompletedEventArgs(this, StartDate, EndDate, RoboSharpFileInfo, RoboSharpDirectoryInfo));
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
        /// Retry Options
        /// </summary>
        public RetryOptions RetryOptions {
            get => RetryOptionsField ??= new RetryOptions();
            set { if (value != null) RetryOptionsField = value; }
        }
        private RetryOptions RetryOptionsField;
       
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

        /// <summary>
        /// TRUE is the copier was paused while it was running, otherwise false.
        /// </summary>
        public bool IsPaused { get; private set; }

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
        /// <param name="IsMoveOperation">TRUE to if the source should be deleted after successfully finishing copying.</param>
        /// <returns>True if the file was copied successfully, otherwise false</returns>
        /// <inheritdoc cref="WriteTask(bool, Action{FileInfo})"/>
        /// <param name="SetAttributes"/>
        private Task RunOperation(bool overWrite, bool IsMoveOperation, Action<FileInfo> SetAttributes = null)
        {
            if (IsCopying) throw new Exception("Copy Operation Already in progress!");

            SetStarted();
            if (!overWrite && File.Exists(Destination.FullName))
            {
                OnFileCopyFailed("Destination file already exists", cancelled: true);
                SetEnded(false, false);
                return Task.FromResult(false);
            }
            
            

            var read = GetReadTask();
            var write = WriteTask(IsMoveOperation, SetAttributes);
            read.Start();
            write.Start();
            return Task.WhenAll(read, write);
        }

        ///// <summary> Custom written copy operation that handles the read/write </summary>
        ///// <returns> TRUE if successful, false is not </returns>
        ///// <remarks> <see href="https://stackoverflow.com/questions/6044629/file-copy-with-progress-bar"/></remarks>
        ///// Was providing approx 3-5 MegaBytes per minute in my testing
        //private bool SingleBufferCopy()
        //{
        //    byte[] buffer = new byte[1024 * 1024]; // 1MB buffer

        //    using (FileStream source = Source.OpenRead())
        //    {
        //        long fileLength = source.Length;
        //        if (Destination.Exists) Destination.Delete();
        //        using (FileStream dest = new FileStream(Destination.FullName, FileMode.CreateNew, FileAccess.Write))
        //        {
        //            long totalBytes = 0;
        //            int currentBlockSize = 0;

        //            while ((currentBlockSize = source.Read(buffer, 0, buffer.Length)) > 0)
        //            {
        //                totalBytes += currentBlockSize;
        //                double percentage = (double)totalBytes * 100.0 / fileLength;

        //                dest.Write(buffer, 0, currentBlockSize);

        //                OnFileCopyProgressUpdated(percentage);
        //                if (CancellationSource.IsCancellationRequested)
        //                {
        //                    return false;
        //                }
        //            }
        //        }
        //    }
        //    return true;
        //}

        ///// <summary> Custom written copy operation that handles the read/write </summary>
        ///// <returns> TRUE if successful, false is not </returns>
        ///// <remarks> <see href="https://stackoverflow.com/questions/6044629/file-copy-with-progress-bar"/></remarks>
        ///// Was providing approx 3-5 MegaBytes per minute in my testing
        //private bool DoubleBufferCopy()
        //{
        //    const int bufferSize = 1024 * 1024;  //1MB
        //    byte[] buffer = new byte[bufferSize], buffer2 = new byte[bufferSize];
        //    bool swap = false;
        //    double reportedProgress = 0, progress;
        //    int read = 0;
        //    long len = Source.Length;    
        //    float flen = len;
        //    Task writer = null;

        //    Destination.Delete();
        //    using (var source = Source.OpenRead())
        //    using (var dest = Destination.OpenWrite())
        //    {
        //        dest.SetLength(source.Length);
        //        for (long size = 0; size < len; size += read)
        //        {
        //            if ((progress = size / flen * 100) != reportedProgress)
        //                OnFileCopyProgressUpdated(reportedProgress = progress);

        //            read = source.Read(swap ? buffer : buffer2, 0, bufferSize);
        //            writer?.Wait();  // if < .NET4 // if (writer != null) writer.Wait(); 
        //            writer = dest.WriteAsync(swap ? buffer : buffer2, 0, read);
        //            swap = !swap;
        //            if (CancellationSource.IsCancellationRequested)
        //                return false;
        //        }
        //        writer?.Wait();  //Fixed - Thanks @sam-hocevar
        //        OnFileCopyProgressUpdated(100);
        //    }
        //    return true;
        //}


        #region < Fields for use by tasks >

        /// <summary>
        /// The buffer size to read for each segment of the file
        /// </summary>
        /// <remarks>
        /// 1MB
        /// </remarks>
        const int bufferSize = 1 * 1024 * 1024; // 1MB
        /// <summary>
        /// The max buffer size to load into memory from a single file (this is to prevent reading a very large file into memory, and filling up memory completely before the file can be written) <br/>
        /// This is the maximum number of items that can be in the <see cref="BytesReadQueue"/> queue
        /// </summary>
        /// <remarks>
        /// 100 MB
        /// </remarks>
        const int bufferMax = (100 * 1024 * 1024 ) / bufferSize;
        /// <summary>
        /// The queue of bytes waiting to be written to disk
        /// </summary>
        private ConcurrentQueue<Tuple<byte[], int>> BytesReadQueue = new();
        /// <summary>
        /// Evaluate if more can be read, or if the reading should be suspended due to the amount of information waiting to be written
        /// </summary>
        private bool CanStillRead => (BytesReadQueue?.Count ?? 0) < bufferMax;
        /// <summary>
        /// Returns true if the <see cref="KillReadTaskField"/> is true, or if cancellation was requested.
        /// </summary>
        /// <remarks>Allows dissaociating the read task from the cancellation source.</remarks>
        private bool KillReadTask { get => KillReadTaskField || (CancellationSource?.IsCancellationRequested ?? false); set => KillReadTaskField = value; }
        private bool KillReadTaskField;
        private ConcurrentQueue<double> ProgressUpdates = new();
        private CancellationTokenSource CancellationSource;
        private Exception exceptionData;

        #endregion

        /// <summary>
        /// Sets up the flags that allow the read/write tasks to run.
        /// </summary>
        internal void SetStarted()
        {
            CancellationSource = new CancellationTokenSource();
            StartDate = DateTime.Now;
            IsCopied = false;
            IsCopying = true;
            KillReadTask = false;
        }

        /// <summary>
        /// Set <see cref="IsCopying"/> to FALSE <br/>
        /// set <see cref="EndDate"/> <br/>
        /// Dospose of cancellation token
        /// </summary>
        /// <param name="wasCancelled">set <see cref="WasCancelled"/></param>
        /// <param name="isCopied">set <see cref="IsCopied"/></param>
        private void SetEnded(bool wasCancelled, bool isCopied)
        {
            KillReadTask = true;
            IsCopying = false;
            WasCancelled = wasCancelled;
            IsCopied = isCopied;
            EndDate = DateTime.Now;
            CancellationSource.Dispose();
            CancellationSource = null;
        }

        /// <summary>
        /// Create a task that reads the file into memory
        /// </summary>
        /// <returns>An unstarted task that completes when the read operation is cancelled, or when the entire file has been read into memory. <br/> TASK MUST BE STARTED!</returns>
        internal Task GetReadTask()
        {
            return new Task(() =>
            {
                byte[] buffer;
                int bytesRead = 0, tries = 1;
                try
                {
                    if (Source.Length == 0) return;
                    using (var source = Source.OpenRead())
                    {
                        for (long size = 0; size < source.Length; size += bytesRead)
                        {
                        TryRead:
                            try
                            {
                                while (IsPaused || !CanStillRead)
                                {
                                    //Check if paused, the WriteTask threw an exception, or if already read the max buffer size
                                    if (KillReadTask) break;
                                    Thread.Sleep(10);
                                }
                                if (KillReadTask) break;
                                bytesRead = source.Read((buffer = new byte[bufferSize]), 0, bufferSize);
                                BytesReadQueue.Enqueue(new Tuple<byte[], int>(buffer, bytesRead));
                            }
                            catch (Exception e)
                            {
                                if (tries < RetryOptions.RetryCount)
                                {
                                    Thread.Sleep(RetryOptions.GetWaitTime());
                                    goto TryRead;
                                }
                                exceptionData = e;
                                break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    exceptionData = e;
                }
            }, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Dequeues all items in the <see cref="BytesReadQueue"/>, then sets the field to null
        /// </summary>
        private void ClearReadQueue()
        {
            if (BytesReadQueue is null) return;
            while (!BytesReadQueue.IsEmpty)
            {
                _ = BytesReadQueue.TryDequeue(out _);
            }
            BytesReadQueue = null;
        }

        /// <summary>
        /// Create a task that writes the file to disk
        /// </summary>
        /// <param name="isMoving">set TRUE to delete the source file after it has been fully written to the destination</param>
        /// <param name="SetAttributes">
        /// Action that will set the file attributes to to the file after it has been copied/moved to the <see cref="Destination"/>
        /// <br/> For example: <see cref="PairEvaluator.ApplyAttributes(FileInfo)"/>
        /// </param>
        /// <returns>A an unstarted task that completes when the write operation is cancelled, or when the entire file has been written. <br/> TASK MUST BE STARTED!</returns>
        private Task WriteTask(bool isMoving, Action<FileInfo> SetAttributes = null)
        {
            Task t = new Task( () =>
            {
                int tries = 1;
            TryOpenWrite:
                try
                {
                    Destination.Directory.Create();
                    if (Source.Length == 0)
                    {
                        File.WriteAllBytes(Destination.FullName, new byte[] { });
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
                                        var progress = sizeWritten / SourceLength * 100;
                                        OnFileCopyProgressUpdated(progress);
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
                catch (Exception e)
                {
                    if (tries < RetryOptions.RetryCount)
                    {
                        Thread.Sleep(RetryOptions.GetWaitTime());
                        tries++;
                        goto TryOpenWrite;
                    }
                    exceptionData = e;
                }
                finally
                {
                    //Set values inditicating that operation has been completed.
                    bool isFaulted = !(exceptionData is null);
                    SetEnded(
                        wasCancelled: CancellationSource?.IsCancellationRequested ?? true,
                        isCopied: !isFaulted && !WasCancelled && Progress == 100
                        );

                    //Raise Events
                    if (IsCopied)
                    {
                        if (isMoving)
                        {
                            try
                            {
                                Source.Delete();
                            }
                            catch (Exception e)
                            {
                                OnFileCopyFailed("Failed to delete source file after Move was requested.", e);
                            }
                            Source.Refresh();
                        }
                        OnFileCopyCompleted();
                    }
                    else
                    {
                        if (File.Exists(Destination.FullName))
                        {
                            try
                            {
                                Destination.Delete(); //Delete incomplete files!
                            }
                            catch { }
                        }
                        if (WasCancelled) OnFileCopyFailed("Copy Operation Cancelled", cancelled: true);
                        else if (isFaulted)
                        {
                            string copyFailedMessage = !isFaulted ? "Copy Operation Failed" : exceptionData.Message;
                            OnFileCopyFailed(copyFailedMessage, exceptionData, failed: true);
                        }
                    }
                    Destination.Refresh();
                }
            }, TaskCreationOptions.LongRunning);
            return t;
        }

        /// <inheritdoc cref="WriteTask(bool, Action{FileInfo})"/>
        internal Task GetWriteTask(Action<FileInfo> SetAttributes) => WriteTask(false, SetAttributes);

        /// <summary>Create a task that writes the file to disk, then deletes the source file if the file copied was successfull.</summary>
        /// <inheritdoc cref="WriteTask(bool, Action{FileInfo})"/>
        internal Task GetMoveTask(Action<FileInfo> SetAttributes) => Move(true, SetAttributes);

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

        #region < Pause / Resume / Cancel >

        /// <summary>
        /// Pause the copy action
        /// </summary>
        public void Pause()
        {
            if (IsCopying)
                IsPaused = true;
        }

        /// <summary>
        /// Resume if paused
        /// </summary>
        public void Resume()
        {
            if (IsCopying && IsPaused)
                IsPaused = false;
        }

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
        /// <inheritdoc cref="RunOperation"/>
        public async Task<bool> Copy(bool overwrite = false)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(FileCopier));
            await RunOperation(overwrite, false, null);
            return IsCopied;
        }

        /// <summary>
        /// Create a task that copies the file to the destination
        /// </summary>
        /// <inheritdoc cref="RunOperation"/>
        public async Task<bool> Copy(bool overwrite, Action<FileInfo> SetAttributes = null)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(FileCopier));
            await RunOperation(overwrite, false, SetAttributes);
            return Destination.Exists;
        }

        #endregion

        #region < Move >

        /// <summary>
        /// Moves the file
        /// </summary>
        /// <inheritdoc cref="Move(bool)"/>
        /// <inheritdoc cref="Copy(bool, Action{FileInfo})"/>
        public Task<bool> Move(bool overWrite = false)
        {
            return Move(overWrite, null);
        }

        /// <summary>
        /// Move the file
        /// </summary>
        /// <inheritdoc cref="RunOperation"/>
        public async Task<bool> Move(bool overWrite, Action<FileInfo> SetAttributes = null)
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(FileCopier));

            if (File.Exists(Destination.FullName) && !overWrite)
            {
                OnFileCopyFailed("Destination file already exists", failed: true);
                return false;
            }
            if (!File.Exists(Source.FullName))
            {
                OnFileCopyFailed("Source does not exist", failed: true);
                return false;
            }

            //Check if Source & Destination are on same physical drive
            string sourceRoot = Path.GetPathRoot(Source.FullName);
            string destRoot = Path.GetPathRoot(Destination.FullName);
            if (sourceRoot.Equals(destRoot, comparisonType: StringComparison.InvariantCultureIgnoreCase))
            {

                //Delete the file at the destination, then move
                SetStarted();
                bool moved = false;
                int tries = 1;
            TryMove:
                try
                {
                    Destination.Directory.Create();
                    if (!moved)
                    {
                        if (File.Exists(Destination.FullName)) Destination.Delete();
                        File.Move(Source.FullName, Destination.FullName);
                        moved = true;
                    }
                    if (moved && SetAttributes != null)
                        SetAttributes(Destination);
                }
                catch (Exception e)
                {
                    if (tries < RetryOptions.RetryCount)
                    {
                        await Task.Delay(RetryOptions.GetWaitTime());
                        goto TryMove;
                    }
                    EndDate = DateTime.Now;
                    OnFileCopyFailed(e.Message, e, failed: true);
                }
                finally
                {
                    SetEnded(
                            wasCancelled: CancellationSource?.IsCancellationRequested ?? false,
                            isCopied: moved
                            ); 
                    if (moved)
                    {
                        
                        Source.Refresh();
                        Destination.Refresh();
                        OnFileCopyProgressUpdated(100);
                        OnFileCopyCompleted();
                    }
                }
                return moved;
            }

            //Source/Dest on different drives : Copy with progress
            await RunOperation(overWrite, true, SetAttributes);
            return Destination.Exists && !Source.Exists;
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