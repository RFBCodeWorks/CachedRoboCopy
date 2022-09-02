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
    internal class DirectoryCopier : INotifyPropertyChanged, IDirectoryPair
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

        public DirectoryCopier(IDirectoryPair directoryPair)
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
        /// The directory name
        /// </summary>
        public string Name => Source.Name;

        /// <summary>
        /// The <see cref="ProcessedFileInfo"/> object pertaining to this copier
        /// </summary>
        public ProcessedFileInfo RoboSharpInfo { get; set; }

        public DirectoryClasses DirectoryClass { get; set; }

        public bool ShouldExclude_JunctionDirectory { get; set; }
        public bool ShouldExclude_NamedDirectory { get; set; }

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

        /// <inheritdoc cref="DirectoryPairExtensions.IsLonely(IDirectoryPair)"/>
        public bool IsLonely => ((IDirectoryPair)this).IsLonely();

        /// <inheritdoc cref="DirectoryPairExtensions.IsExtra(IDirectoryPair)"/>
        public bool IsExtra => ((IDirectoryPair)this).IsExtra();

        //DirectoryInfo IDirectorySourceDestinationPair.Source => Source.DirectoryInfo;

        //DirectoryInfo IDirectorySourceDestinationPair.Destination => Destination.DirectoryInfo;



        #endregion

        #region < Object Methods >

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

        /// <summary>
        /// Recursively delete the entire destination folder and all its contents
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public async Task Purge(RetryOptions options)
        {
            int attempts = 1;
            Source.Refresh();
            Destination.Refresh();
        tryDelete:
            try
            {
                if (IsExtra)
                {
                    Destination.Delete(true);
                    Destination.Refresh();
                }
            }
            catch when (attempts < options.RetryCount)
            {

                await Task.Delay(new TimeSpan(hours: 0, minutes: 0, seconds: options.RetryWaitTime));
                goto tryDelete;
            }
        }

        /// <summary>
        /// Generate the FileCopiers
        /// </summary>
        /// <returns></returns>
        private FileCopier[] GetFileCopiers()
        {
            return DirectoryPairExtensions.GetFilePairs(this, (i1, i2) => new FileCopier(i1, i2));
        }

        /// <summary>
        /// Generate the FileCopiers
        /// </summary>
        /// <returns></returns>
        private CachedEnumerable<FileCopier> GetFileCopiersEnumerable()
        {
            return DirectoryPairExtensions.EnumerateFilePairs(this, (i1, i2) => new FileCopier(i1, i2));
        }


        /// <summary>
        /// Generate the DirectoryCopiers
        /// </summary>
        /// <returns></returns>
        private DirectoryCopier[] GetDirectoryCopiers()
        {
            return DirectoryPairExtensions.GetDirectoryPairs(this, (i, i2) => new DirectoryCopier(i, i2));
        }

        /// <summary>
        /// Generate the DirectoryCopiers
        /// </summary>
        /// <returns></returns>
        private CachedEnumerable<DirectoryCopier> GetDirectoryCopiersEnumerable()
        {
            return DirectoryPairExtensions.EnumerateDirectoryPairs(this, (i, i2) => new DirectoryCopier(i, i2));
        }

        #endregion

    }
}
