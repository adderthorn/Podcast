using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Monosoftware.Podcast
{
    /// <summary>
    /// Interface reprecenting a downloadable object.
    /// </summary>
    public interface IDownloadable
    {
        /// <summary>
        /// Uri of the file to be downloaded.
        /// </summary>
        Uri MediaSource { get; set; }

        /// <summary>
        /// Task to download the file Asynchronously.
        /// </summary>
        /// <param name="ProgressCallback">Callback function for progress.</param>
        /// <param name="CancellationToken">Cancellation token for the download.</param>
        /// <returns></returns>
        Task DownloadFileAsync(IProgress<HttpProgressInfo> ProgressCallback = null, CancellationTokenSource CancellationToken = null);

        /// <summary>
        /// Gets a stream of the downloaded file.
        /// </summary>
        /// <returns>Stream object of the downloaded file.</returns>
        Stream GetStream();

        /// <summary>
        /// Sets the stream of the file.
        /// </summary>
        /// <param name="stream">Steam object of the file.</param>
        void SetStream(Stream stream);

        /// <summary>
        /// Represents whether or not the file has been downloaded.
        /// </summary>
        /// <returns>True/False of download status.</returns>
        bool IsDownloaded();
    }
}
