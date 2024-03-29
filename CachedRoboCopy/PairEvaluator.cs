﻿using RoboSharp;
using RoboSharp.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RoboSharp.Extensions;

namespace RFBCodeWorks.RoboSharpExtensions
{
    /// <summary>
    /// Class that can be instantiated to cache the various values that get checked against when deciding to copy a file or folder.
    /// <br/> Custom Implementations can instantiate this class and use it to assist with evaluating if they need to copy a file, generate the ProcessedFileInfo objects, etc.
    /// </summary>
    public class PairEvaluator
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        public PairEvaluator(IRoboCommand command)
        { 
            AssociatedCommand = command;
            FileAttributesToApplyField = SelectionOptions.ConvertFileAttrStringToEnum(AssociatedCommand.CopyOptions.AddAttributes);
            FileAttributesToRemoveField = SelectionOptions.ConvertFileAttrStringToEnum(AssociatedCommand.CopyOptions.RemoveAttributes);
        }

        #region < Properties >

        /// <summary>
        /// The IRoboCommand object this evaluator is tied to
        /// </summary>
        public IRoboCommand AssociatedCommand { get; }

        /// <summary>
        /// Regex objects generated by <see cref="CopyOptionsExtensions.ShouldIncludeFileName(CopyOptions, IFilePair, ref Regex[])"/>
        /// </summary>
        public Regex[] IncludeFileNameRegex => IncludeFileNameRegexField;
        private Regex[] IncludeFileNameRegexField;

        /// <summary>
        /// Regex objects generated by <see cref="SelectionOptionsExtensions.ShouldExcludeFileName(SelectionOptions, IFilePair, ref Regex[])"/>
        /// </summary>
        public Regex[] ExcludeFileNameRegex => ExcludeFileNameRegexField;
        private Regex[] ExcludeFileNameRegexField;

        private Tuple<bool, Regex>[] DirectoryNameRegexExclusions;

        /// <summary>
        /// File Attributes to add - Gathered from <see cref="CopyOptions.AddAttributes"/> when instantiated.
        /// </summary>
        public FileAttributes? FileAttributesToApply => FileAttributesToApplyField;
        private FileAttributes? FileAttributesToApplyField;

        /// <summary>
        /// File Attributes to remove - Gathered from <see cref="CopyOptions.RemoveAttributes"/> when instantiated.
        /// </summary>
        public FileAttributes? FileAttributesToRemove => FileAttributesToRemoveField;
        private FileAttributes? FileAttributesToRemoveField;

        #endregion

        #region < ShouldCopyDir >

        /// <summary>
        /// Compare the Source/Destination directories, and decide if the directory should be copied down.
        /// </summary>
        /// <param name="pair">the pair to evaluate</param>
        /// <param name="info">the generated ProcessedFileInfo</param>
        /// <param name="dirClass">The dirClass applied to the <paramref name="info"/></param>
        /// <param name="ExcludeDirectoryName">Result of <see cref="ShouldExcludeDirectoryName(IDirectoryPair)"/></param>
        /// <param name="ExcludeJunctionDirectory">Result of <see cref="ShouldExcludeJunctionDirectory(IDirectoryPair)"/></param>
        /// <returns>TRUE if the directory would be excluded based on the current IROboCommand settings, otherwise false</returns>
        public virtual bool ShouldCopyDir(IDirectoryPair pair, out ProcessedFileInfo info, out DirectoryClasses dirClass, out bool ExcludeJunctionDirectory, out bool ExcludeDirectoryName)
        {
            ExcludeDirectoryName = ShouldExcludeDirectoryName(pair);
            ExcludeJunctionDirectory = ShouldExcludeJunctionDirectory(pair);
            
            bool shouldExclude = ExcludeJunctionDirectory | ExcludeDirectoryName;
            if (!shouldExclude && pair.Source.Exists && pair.Destination.Exists)
            {
                dirClass = DirectoryClasses.ExistingDir;
                info = new ProcessedFileInfo(pair.Source, AssociatedCommand.Configuration, DirectoryClasses.ExistingDir);
            }
            else if (!shouldExclude && pair.Source.Exists && !AssociatedCommand.SelectionOptions.ExcludeLonely)
            {
                dirClass = DirectoryClasses.NewDir;
                info = new ProcessedFileInfo(pair.Source, AssociatedCommand.Configuration, DirectoryClasses.NewDir);
            }
            else if (pair.Destination.Exists && !AssociatedCommand.SelectionOptions.ExcludeExtra)
            {
                dirClass = DirectoryClasses.ExtraDir;
                info = new ProcessedFileInfo(pair.Destination, AssociatedCommand.Configuration, DirectoryClasses.ExtraDir);
                return false;
            }
            else
            {
                dirClass = DirectoryClasses.Exclusion;
                info = new ProcessedFileInfo(pair.Source, AssociatedCommand.Configuration, DirectoryClasses.Exclusion);
            }

            return !shouldExclude;
        }


        /// <inheritdoc cref="SelectionOptionsExtensions.ShouldExcludeJunctionDirectory(SelectionOptions, IDirectoryPair)"/>
        public bool ShouldExcludeJunctionDirectory(IDirectoryPair pair) => AssociatedCommand.SelectionOptions.ShouldExcludeJunctionDirectory(pair.Source);

        /// <inheritdoc cref="SelectionOptionsExtensions.ShouldExcludeDirectoryName(SelectionOptions, IDirectoryPair, ref Tuple{bool, Regex}[])"/>
        public bool ShouldExcludeDirectoryName(IDirectoryPair pair) => AssociatedCommand.SelectionOptions.ShouldExcludeDirectoryName(pair.Source.FullName, ref DirectoryNameRegexExclusions);

        #endregion

        #region < ShouldCopyFile >

        /// <summary>
        /// Evaluate RoboCopy Options of the command, the source, and destination and compute a ProcessedFileInfo object <br/>
        /// Ignores <see cref="LoggingOptions.ListOnly"/>
        /// </summary>
        /// <param name="info">a ProcessedFileInfo object generated that reflects the output of this method</param>
        /// <param name="pair">the pair of Source/Destination to compare</param>
        /// <returns>TRUE if the file should be copied/moved, FALSE if the file should be skiped</returns>
        /// <remarks>
        /// Note: Does not evaluate the FileName inclusions from CopyOptions, since RoboCopy appears to use those to filter prior to performing these evaluations. <br/>
        /// Use <see cref="ShouldIncludeFileName(IFilePair)"/> as a pre-filter for this.
        /// </remarks>
        public virtual bool ShouldCopyFile(IFilePair pair, out ProcessedFileInfo info)
        {
            bool SourceExists = pair.Source.Exists;
            bool DestExists = pair.Destination.Exists;
            string Name = AssociatedCommand.LoggingOptions.IncludeFullPathNames ?
                (DestExists & !SourceExists ? pair.Destination.FullName : pair.Source.FullName) :
                (DestExists & !SourceExists ? pair.Destination.Name : pair.Source.Name);
            info = new ProcessedFileInfo()
            {
                FileClassType = FileClassType.File,
                Name = Name,
                Size = SourceExists ? pair.Source.Length : DestExists? pair.Destination.Length : 0,
            };
            var SO = AssociatedCommand.SelectionOptions;

            // Order of the following checks was done to allow what are likely the fastest checks to go first. More complex checks (such as DateTime parsing) are towards the bottom.

            //EXTRA
            if (pair.IsExtra())// SO.ShouldExcludeExtra(pair))
            {
                info.SetFileClass(FileClasses.ExtraFile, AssociatedCommand.Configuration);
                //info.Name = AssociatedCommand.LoggingOptions.IncludeFullPathNames ? pair.Destination.FullName : pair.Destination.Name; //Already Handled in ctor
                //info.Size = pair.Destination.Length;  //Already handled in ctor
                return false;
            }
            //Lonely
            else if (SO.ShouldExcludeLonely(pair))
            {
                info.SetFileClass(FileClasses.ExtraFile, AssociatedCommand.Configuration); // TO-DO: Does RoboCopy identify Lonely seperately? If so, we need a token for it!
            }
            //Exclude Newer
            else if (SO.ShouldExcludeNewer(pair))
            {
                info.SetFileClass(FileClasses.NewerFile, AssociatedCommand.Configuration);
            }
            //Exclude Older
            else if (SO.ShouldExcludeOlder(pair))
            {
                info.SetFileClass(FileClasses.OlderFile, AssociatedCommand.Configuration);
            }
            //MaxFileSize
            else if (SO.ShouldExcludeMaxFileSize(pair.Source.Length))
            {
                info.SetFileClass(FileClasses.MaxFileSizeExclusion, AssociatedCommand.Configuration);
            }
            //MinFileSize
            else if (SO.ShouldExcludeMinFileSize(pair.Source.Length))
            {
                info.SetFileClass(FileClasses.MinFileSizeExclusion, AssociatedCommand.Configuration);
            }
            //FileAttributes
            else if (!SO.ShouldIncludeAttributes(pair) || SO.ShouldExcludeFileAttributes(pair))
            {
                info.SetFileClass(FileClasses.AttribExclusion, AssociatedCommand.Configuration);
            }
            //Max File Age
            else if (SO.ShouldExcludeMaxFileAge(pair))
            {
                info.SetFileClass(FileClasses.MaxAgeSizeExclusion, AssociatedCommand.Configuration);
            }
            //Min File Age
            else if (SO.ShouldExcludeMinFileAge(pair))
            {
                info.SetFileClass(FileClasses.MinAgeSizeExclusion, AssociatedCommand.Configuration);
            }
            //Max Last Access Date
            else if (SO.ShouldExcludeMaxLastAccessDate(pair))
            {
                info.SetFileClass(FileClasses.MaxAgeSizeExclusion, AssociatedCommand.Configuration);
            }
            //Min Last Access Date
            else if (SO.ShouldExcludeMinLastAccessDate(pair))
            {
                info.SetFileClass(FileClasses.MinAgeSizeExclusion, AssociatedCommand.Configuration); // TO-DO: Does RoboCopy iddentify Last Access Date exclusions seperately? If so, we need a token for it!
            }
            // Name Filters - These are last check since Regex will likely take the longest to evaluate
            if (SO.ShouldExcludeFileName(pair.Source.Name, ref this.ExcludeFileNameRegexField))
            {
                info.SetFileClass(FileClasses.FileExclusion, AssociatedCommand.Configuration);
            }
            else if (pair.IsExtra())
            {
                info.SetFileClass(FileClasses.ExtraFile, AssociatedCommand.Configuration);
                return false; // Source doesn't exist
            }
            else
            {
                // Check for symbolic links
                bool xjf = SO.ExcludeSymbolicFile(pair.Source); // TO-DO: Likely needs its own 'FileClass' set up for proper evaluation by ProgressEstimator

                // File passed all checks - It should be copied!
                if (pair.IsLonely())
                {
                    info.SetFileClass(FileClasses.NewFile, AssociatedCommand.Configuration);
                    return !xjf && !AssociatedCommand.SelectionOptions.ExcludeLonely;
                }
                else if (pair.IsSourceNewer())
                {
                    info.SetFileClass(FileClasses.NewerFile, AssociatedCommand.Configuration);
                    return !xjf && !AssociatedCommand.SelectionOptions.ExcludeNewer;
                }
                else if (pair.IsDestinationNewer())
                {
                    info.SetFileClass(FileClasses.OlderFile, AssociatedCommand.Configuration);
                    return !xjf && !AssociatedCommand.SelectionOptions.ExcludeOlder;
                }
                else
                {
                    info.SetFileClass(FileClasses.SameFile, AssociatedCommand.Configuration);
                    return !xjf && AssociatedCommand.SelectionOptions.IncludeSame;
                }
            }

            return false; // File failed one of the checks, do not copy.

        }





        /// <summary>
        /// Filter the filenames according to the filters specified by <see cref="CopyOptions.FileFilter"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <returns></returns>
        public CachedEnumerable<T> FilterFilePairs<T>(IEnumerable<T> collection) where T: IFilePair
        {
            var filters = AssociatedCommand.CopyOptions.FileFilter;
            if (filters.Any() && filters.All(s => s != "*.*" && s != "*"))
                return collection
                .Where(P => ShouldIncludeFileName(P))
                .AsCachedEnumerable();
            else
                return collection is CachedEnumerable<T> enumerable ? enumerable : collection.AsCachedEnumerable();
        }

        /// <inheritdoc cref="CopyOptionsExtensions.ShouldIncludeFileName(CopyOptions, IFilePair, ref Regex[])"/>
        public bool ShouldIncludeFileName(IFilePair pair)
        {
            return AssociatedCommand.CopyOptions.ShouldIncludeFileName(pair, ref IncludeFileNameRegexField);
        }

        /// <inheritdoc cref="CopyOptionsExtensions.ShouldIncludeFileName(CopyOptions, IFilePair, ref Regex[])"/>
        public bool ShouldIncludeFileName(FileInfo file)
        {
            return AssociatedCommand.CopyOptions.ShouldIncludeFileName(file, ref IncludeFileNameRegexField);
        }
        /// <inheritdoc cref="CopyOptionsExtensions.ShouldIncludeFileName(CopyOptions, IFilePair, ref Regex[])"/>
        public bool ShouldIncludeFileName(string file)
        {
            return AssociatedCommand.CopyOptions.ShouldIncludeFileName(file, ref IncludeFileNameRegexField);
        }

        #endregion

        #region < Purge >

        /// <inheritdoc cref="CopyOptionsExtensions.ShouldPurge(IRoboCommand, IFilePair)"/>
        public bool ShouldPurge(IFilePair pair)
            => AssociatedCommand.ShouldPurge(pair);

        #endregion

        #region < Apply Attributes >

        /// <inheritdoc cref="CopyOptionsExtensions.SetFileAttributes(CopyOptions, FileInfo)"/>
        public void ApplyAttributes(FileInfo destination)
        {
            if (FileAttributesToApply.HasValue)
                destination.Attributes &= FileAttributesToApply.Value;
            if (FileAttributesToRemove.HasValue)
                destination.Attributes &= ~FileAttributesToRemove.Value;
        }

        /// <inheritdoc cref="CopyOptionsExtensions.SetFileAttributes(CopyOptions, FileInfo)"/>
        public void ApplyAttributes(IFilePair pair)
            => ApplyAttributes(pair.Destination);

        #endregion

    }
}
