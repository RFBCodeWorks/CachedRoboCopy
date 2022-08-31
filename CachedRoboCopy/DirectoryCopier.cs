using RoboSharp;
using RoboSharp.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RFBCodeWorks.CachedRoboCopy
{
    /// <summary>
    /// Class designed to facilitate the Copy/Move functionality for a directory
    /// </summary>
    internal class DirectoryCopier : INotifyPropertyChanged, IDirectorySourceDestinationPair
    {

        #region < Constructors >

        public DirectoryCopier(string sourceDir, string destinationDir)
        {
            if (string.IsNullOrWhiteSpace(sourceDir)) throw new ArgumentException("parameter is null or empty", nameof(sourceDir));
            if (string.IsNullOrWhiteSpace(destinationDir)) throw new ArgumentException("parameter is null or empty", nameof(destinationDir));

            if (!Path.IsPathRooted(sourceDir)) throw new ArgumentException("Path is not rooted", nameof(sourceDir));
            if (!Path.IsPathRooted(destinationDir)) throw new ArgumentException("Path is not rooted", nameof(destinationDir));

            var source = new DirectoryInfo(sourceDir);
            var dest= new DirectoryInfo(destinationDir);

            this.Source = source;
            this.Destination = dest;
            Refresh();
        }

        public DirectoryCopier(DirectoryInfo sourceDir, DirectoryInfo destinationDir)
        {
            this.Source = sourceDir ?? throw new ArgumentNullException(nameof(sourceDir));
            this.Destination = destinationDir ?? throw new ArgumentNullException(nameof(destinationDir));
            Refresh();
        }

        public DirectoryCopier(IDirectorySourceDestinationPair directoryPair)
        {
            if (directoryPair is null) throw new ArgumentNullException(nameof(directoryPair));
            this.Source = directoryPair?.Source ?? throw new ArgumentNullException("Source");
            this.Destination = directoryPair?.Destination ?? throw new ArgumentNullException("Destination");
            Refresh();
        }


        #endregion

        #region < Static Properties & Constants >

        #endregion

        #region < Events >

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


        //#region < CopyCompleted >

        ///// <summary>
        ///// Delegate for the CopyCompleted event
        ///// </summary>
        //public delegate void CopyCompletedHandler(object sender, DirectoryCopyCompletedEventArgs e);

        ///// <summary>
        ///// Occurs when the copy operation has finished
        ///// </summary>
        //public event CopyCompletedHandler CopyCompleted;

        ///// <summary> Raises the CopyCompleted event </summary>
        //protected virtual void OnCopyCompleted(bool success, Exception e = null)
        //{
        //    CopyCompleted?.Invoke(this, new DirectoryCopyCompletedEventArgs(this, success, e));
        //}

        //#endregion


        ///// <summary>
        ///// 
        ///// </summary>
        //public event FileCopier.FileCopyProgressUpdatedHandler FileCopyProgressUpdated;

        ///// <summary> Raises the CopyProgressUpdated event </summary>
        //protected virtual void OnFileCopyProgressUpdated(FileCopyProgressUpdatedEventArgs e)
        //{
        //    FileCopyProgressUpdated?.Invoke(this, e);
        //}

        ///// <summary>
        ///// 
        ///// </summary>
        //public event FileCopier.fi FileCopyFailed;

        ///// <summary> Raises the CopyFailed event </summary>
        //protected virtual void OnFileCopyFailed(FileCopyFailedEventArgs e)
        //{
        //    FileCopyFailed?.Invoke(this, e);
        //}


        ///// <summary>
        ///// 
        ///// </summary>
        //public event FileCopier.CopyCompletedHandler FileCopyCompleted;

        ///// <summary> Raises the CopyComplete event </summary>
        //protected virtual void OnFileCopyComplete(FileCopyCompletedEventArgs e)
        //{
        //    FileCopyCompleted?.Invoke(this, e);
        //}

        #endregion

        #region < Properties >

        //private CancellationTokenSource CancellationSource;

        /// <summary>
        /// The Source Directory
        /// </summary>
        public DirectoryInfo Source { get; }

        /// <summary>
        /// The Destination Directory
        /// </summary>
        public DirectoryInfo Destination { get; }

        /// <summary>
        /// The <see cref="ProcessedFileInfo"/> object pertaining to this copier
        /// </summary>
        public ProcessedFileInfo RoboSharpInfo { get; set; }

        ///// <summary>
        ///// 
        ///// </summary>
        //public bool IsCopying
        //{
        //    get { return IsCopyingField; }
        //    set { SetProperty(ref IsCopyingField, value, nameof(IsCopying)); }
        //}
        //private bool IsCopyingField;

        /// <summary>
        /// The combined list of directory pairs that reside within the Source and Destination
        /// </summary>
        public CachedEnumerable<DirectoryCopier> SubDirectories { get; private set; }

        /// <summary>
        /// The combines list of file pairs that reside within the Source and Destination
        /// </summary>
        public CachedEnumerable<FileCopier> Files { get; private set; }

        public bool HasFiles => Files.Any();

        public bool HasSubdirectories => SubDirectories.Any();

        public bool IsEmpty => !HasFiles & !HasSubdirectories;

        public bool SourceHasSubDirectories { get; private set; }
        public bool SourceHasFiles { get; private set; }

        public bool IsSourceEmpty => !SourceHasFiles && !SourceHasSubDirectories;

        /// <inheritdoc cref="SelectionOptionsExtensions.IsLonely(IDirectorySourceDestinationPair)"/>
        public bool IsLonely => ((IDirectorySourceDestinationPair)this).IsLonely();

        /// <inheritdoc cref="SelectionOptionsExtensions.IsExtra(IDirectorySourceDestinationPair)"/>
        public bool IsExtra => ((IDirectorySourceDestinationPair)this).IsExtra();

        //DirectoryInfo IDirectorySourceDestinationPair.Source => Source.DirectoryInfo;

        //DirectoryInfo IDirectorySourceDestinationPair.Destination => Destination.DirectoryInfo;



        #endregion

        #region < Object Methods >

        ///// <param name="verbose">
        ///// If Source / Destination are on same drive and the destination doesn't exist, default functionality will use Directory.Move() to move the folder in its entirety in one fast command. <br/>
        ///// That has the downside of not logging the filenames, but is much quicker. If explicit logging is requested, turn this on to do normal functionality.
        ///// </param>
        ///// <inheritdoc cref="FileCopier.Copy(bool)"/>
        ///// <param name="directoryInclusionPattern"/><param name="fileInclusionPattern"/><param name="ignoredDirs"/><param name="overwrite"/>
        //public async Task<bool> Move(bool overwrite = true, bool verbose = false, string fileInclusionPattern = "*", string directoryInclusionPattern = "*", params string[] ignoredDirs)
        //{
        //    if (!CanCopy()) return false;
        //    bool result = false;
        //    if (!verbose && Path.GetPathRoot(Source.FullName).Equals(Path.GetPathRoot(Destination.FullName), StringComparison.InvariantCultureIgnoreCase))
        //    {
        //        if (!Directory.Exists(Destination.FullName))
        //        {
        //            try
        //            {
        //                Directory.Move(Source.FullName, Destination.FullName);
        //                result = true;
        //            }
        //            catch (Exception e)
        //            {
        //                OnFileCopyFailed(new FileCopyFailedEventArgs(null, e.Message));
        //                OnCopyCompleted(false, e);
        //            }
        //            if (result) OnCopyCompleted(result);
        //            return result;
        //        }
        //    }

        //    CancellationSource = new CancellationTokenSource();
        //    result = await Dig(this, true, overwrite, fileInclusionPattern, ignoredDirs, directoryInclusionPattern);
        //    OnCopyCompleted(result);
        //    return result;
        //}

        //private Task<bool> Dig(DirectoryCopier copier, bool isMoveOperation, bool overwrite,
        //    string fileInclusionPattern, string[] ignoredDirs, string directoryIncludsionPattern = "*")
        //{
        //    return RunCopyOp(
        //        copier.GetFileCopiers(fileInclusionPattern),
        //        copier.GetDirectoryCopiers(directoryIncludsionPattern, ignoredDirs),
        //        isMoveOperation, overwrite, fileInclusionPattern, ignoredDirs);
        //}

        //private async Task<bool> RunCopyOp(FileCopier[] fileCopiers, DirectoryCopier[] directoryCopiers, bool isMoveOperation, bool overwrite, string fileInclusionPattern, string[] ignoredDirs)
        //{
        //    bool result = true;
        //    foreach( var f in fileCopiers)
        //    {
        //        if (CancellationSource.IsCancellationRequested) throw new TaskCanceledException();
        //        f.CopyProgressUpdated += FileCopyProgressUpdated;
        //        f.CopyCompleted += FileCopyCompleted;
        //        f.CopyFailed += FileCopyFailed;
        //        var copyTask = (isMoveOperation ? f.Move(overwrite) : f.Copy(overwrite));
        //        await Task.Run(() => Task.WaitAll(new Task[] { copyTask}, CancellationSource.Token));
        //        if (CancellationSource.IsCancellationRequested) f.Cancel();
        //        result &= await copyTask;
        //        f.CopyProgressUpdated -= FileCopyProgressUpdated;
        //        f.CopyCompleted -= FileCopyCompleted;
        //        f.CopyFailed -= FileCopyFailed;
        //    }

        //    foreach (var d in directoryCopiers)
        //    {
        //        if (CancellationSource.IsCancellationRequested) throw new TaskCanceledException();
        //        d.FileCopyProgressUpdated += FileCopyProgressUpdated;
        //        d.FileCopyCompleted += FileCopyCompleted;
        //        d.FileCopyFailed += FileCopyFailed;
        //        var digTask = Dig(d, isMoveOperation, overwrite, fileInclusionPattern, ignoredDirs);
        //        await Task.Run(() => Task.WaitAll(new Task[] { digTask }, CancellationSource.Token));
        //        result &= digTask.Result;
        //        d.FileCopyProgressUpdated -= FileCopyProgressUpdated;
        //        d.FileCopyCompleted -= FileCopyCompleted;
        //        d.FileCopyFailed -= FileCopyFailed;
        //    }
        //    return result;
        //}

        ///// <summary>
        ///// Check if the object can copy or not
        ///// </summary>
        ///// <returns></returns>
        //public bool CanCopy() => !IsCopying;

        ///// <summary>
        ///// Check if the object can cancel or not
        ///// </summary>
        ///// <returns></returns>
        //public bool CanCancel() => IsCopying && !(CancellationSource?.IsCancellationRequested ?? true);

        ///// <summary>
        ///// Cancel the copy operations - does not have any effect on successfully copied/moved files
        ///// </summary>
        //public void Cancel()
        //{
        //    if (CanCancel())
        //        CancellationSource.Cancel();
        //}

        /// <summary>
        /// Refresh the entire object, including the cached enumerables
        /// </summary>
        public void Refresh()
        {
            Source.Refresh();
            Destination.Refresh();
            SourceHasFiles = Source.HasFiles();
            SourceHasSubDirectories = Source.HasSubDirectories();
            Files = GetFileCopiersEnumerable();
            SubDirectories = GetDirectoryCopiersEnumerable();
        }

        public void Purge(RetryOptions options)
        {
            int attempts = 1;
            Source.Refresh();
            Destination.Refresh();
        tryDelete:
            try {

                if (IsExtra)
                { Destination.Delete(true);
                    Destination.Refresh();
                }
            }
            catch(Exception e)
            {
                if (attempts < options.RetryCount)
                    goto tryDelete;

            }
        }

        /// <summary>
        /// Generate the FileCopiers
        /// </summary>
        /// <returns></returns>
        private FileCopier[] GetFileCopiers()
        {
            return ISourceDestinationPairExtensions.GetFilePairs(this, (i1, i2) => new FileCopier(i1, i2));
        }

        /// <summary>
        /// Generate the FileCopiers
        /// </summary>
        /// <returns></returns>
        private CachedEnumerable<FileCopier> GetFileCopiersEnumerable()
        {
            return ISourceDestinationPairExtensions.GetFilePairsEnumerable(this, (i1, i2) => new FileCopier(i1, i2));
        }


        /// <summary>
        /// Generate the DirectoryCopiers
        /// </summary>
        /// <returns></returns>
        private DirectoryCopier[] GetDirectoryCopiers()
        {
            return ISourceDestinationPairExtensions.GetDirectoryPairs(this, (i, i2) => new DirectoryCopier(i, i2));
        }

        /// <summary>
        /// Generate the DirectoryCopiers
        /// </summary>
        /// <returns></returns>
        private CachedEnumerable<DirectoryCopier> GetDirectoryCopiersEnumerable()
        {
            return ISourceDestinationPairExtensions.GetDirectoryPairsEnumerable(this, (i, i2) => new DirectoryCopier(i, i2));
        }

        #endregion

    }
}
