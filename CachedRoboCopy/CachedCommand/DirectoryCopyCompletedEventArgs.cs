//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace RFBCodeWorks.RoboSharpExtensions
//{
//    /// <summary>
//    /// Args to occur when a DirectoryCopier completes its task
//    /// </summary>
//    public class DirectoryCopyCompletedEventArgs
//    {
//        /// <summary>
//        /// 
//        /// </summary>
//        /// <param name="source"></param>
//        /// <param name="dest"></param>
//        /// <param name="CopiedSuccessfully"></param>
//        /// <param name="e"></param>
//        public DirectoryCopyCompletedEventArgs(DirectoryInfo source, DirectoryInfo dest, bool CopiedSuccessfully, Exception e = null)
//        {
//            Source = source;
//            Destination = dest;
//            AllFilesCopiedSuccessfully = CopiedSuccessfully;
//            Exception = e;
//        }

//        /// <summary>
//        /// 
//        /// </summary>
//        /// <param name="copier"></param>
//        /// <param name="CopiedSuccessfully"></param>
//        /// <param name="e"></param>
//        public DirectoryCopyCompletedEventArgs(DirectoryCopier copier, bool CopiedSuccessfully, Exception e = null)
//        {
//            Source = copier.Source;
//            Destination = copier.Destination;
//            AllFilesCopiedSuccessfully = CopiedSuccessfully;
//            Exception = e;
//        }

//        /// <summary>
//        /// The source directory
//        /// </summary>
//        public DirectoryInfo Source { get; }

//        /// <summary>
//        /// The destination directory
//        /// </summary>
//        public DirectoryInfo Destination { get; }

//        /// <summary>
//        /// Result
//        /// </summary>
//        public bool AllFilesCopiedSuccessfully { get; }

//        /// <summary>
//        /// The exception that occurred, if any
//        /// </summary>
//        public Exception Exception { get; }

//    }
//}
