using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace Monosoftware.Podcast
{
    /// <summary>
    /// A custom implementaiton of the HttpClient library that supports progress reporting.
    /// </summary>
    public class HttpClientProgress : IDisposable
    {
        #region Private Variables
        private const int BUFFER_SIZE = 4096;
        private Stream _downloadStream;
        private readonly TimeSpan TIMEOUT = TimeSpan.FromHours(2);
        private HttpClient _httpClient;
        private CancellationToken _CancellationToken = new CancellationToken();
        #endregion

        #region Public Properties
        /// <summary>
        /// To URL of the item to be downloaded.
        /// </summary>
        public string DownloadUrl { get; set; }

        /// <summary>
        /// Cancellatin token to be used for the download.
        /// </summary>
        public CancellationToken CancellationToken
        {
            get => _CancellationToken;
            set => _CancellationToken = value;
        }
        #endregion

        #region Handlers
        /// <summary>
        /// Deleagte for the progress changed event.
        /// </summary>
        /// <param name="TotalFileSizeBytes">Total file size (in bytes) of the download.</param>
        /// <param name="TotalBytesDownloaded">Current number of bytes downloaded.</param>
        /// <param name="ProgressPercentage">Current percentage of the download (requires total file size).</param>
        public delegate void ProgressChangedHandler(long? TotalFileSizeBytes, long TotalBytesDownloaded, double? ProgressPercentage);

        /// <summary>
        /// Handler for the progress changed event.
        /// </summary>
        public event ProgressChangedHandler ProgressChanged;
        #endregion

        #region Class Constructors
        /// <summary>
        /// Initializes new a new instance with the download Url as a string.
        /// </summary>
        /// <param name="DownloadUrl">Url of the file to download.</param>
        public HttpClientProgress(string DownloadUrl) => this.DownloadUrl = DownloadUrl;

        /// <summary>
        /// Initializes a new instance with the download Url as a Uri class.
        /// </summary>
        /// <param name="DownloadUrl">Url of the file to download.</param>
        public HttpClientProgress(Uri DownloadUrl) => this.DownloadUrl = DownloadUrl.AbsoluteUri;

        /// <summary>
        /// Initializes a new instance with the download Url as a string with a cancellation token.
        /// </summary>
        /// <param name="DownloadUrl">Url of the file to download.</param>
        /// <param name="CancellationToken">Cancellation token for the download.</param>
        public HttpClientProgress(Uri DownloadUrl, CancellationTokenSource CancellationToken)
        {
            this.DownloadUrl = DownloadUrl.AbsoluteUri;
            _CancellationToken = CancellationToken.Token;
        }

        /// <summary>
        /// Initializes a new instance with the download Url as a Uri class with a cancellation token.
        /// </summary>
        /// <param name="DownloadUrl">Url of the file to download.</param>
        /// <param name="CancellationToken">Cancellation token for the download.</param>
        public HttpClientProgress(string DownloadUrl, CancellationTokenSource CancellationToken)
        {
            this.DownloadUrl = DownloadUrl;
            _CancellationToken = CancellationToken.Token;
        }
        #endregion Class Constructors

        #region Public Methods
        /// <summary>
        /// Starts the download of the file specificed in DownloadUrl.
        /// </summary>
        /// <returns>Awaitable Stream object of the downloaded content.</returns>
        public async Task<Stream> StartDownloadAsync()
        {
            _httpClient = new HttpClient() { Timeout = TIMEOUT };
            using (var response = await _httpClient.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, _CancellationToken))
            {
                await downloadFileFromHttpResponseMessage(response);
            }
            _downloadStream.Seek(0, SeekOrigin.Begin);
            return _downloadStream;
        }

        public void Dispose()
        {
            ProgressChanged = null;
            _httpClient?.Dispose();
            _downloadStream?.Dispose();
        }
        #endregion

        #region Private Methods
        private async Task downloadFileFromHttpResponseMessage(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            long? totalBytes = response.Content.Headers.ContentLength;
            using (var contentStream = await response.Content.ReadAsStreamAsync())
            {
                await processContentStream(totalBytes, contentStream);
            }
        }

        private async Task processContentStream(long? totalBytes, Stream contentStream)
        {
            long totalBytesRead = 0L;
            long readCount = 0L;
            byte[] buffer = new byte[BUFFER_SIZE];
            bool moreToRead = true;

            _downloadStream = new MemoryStream();
            do
            {
                if (_CancellationToken.IsCancellationRequested) throw new TaskCanceledException();
                int bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    moreToRead = false;
                    triggerProgressChanged(totalBytes, totalBytesRead);
                    continue;
                }
                await _downloadStream.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
                readCount++;

                if (readCount % 100 == 0) triggerProgressChanged(totalBytes, totalBytesRead);

            } while (moreToRead);
        }

        private void triggerProgressChanged(long? totalBytes, long totalBytesRead)
        {
            if (ProgressChanged == null) return;
            double? progressPercentage = null;
            if (totalBytes.HasValue)
            {
                progressPercentage = Math.Round((double)totalBytesRead / totalBytes.Value * 100, 2);
            }
            ProgressChanged(totalBytes, totalBytesRead, progressPercentage);
        }
        #endregion
    }
}
