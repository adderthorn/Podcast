using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Monosoftware.Podcast
{
    /// <summary>
    /// Class that represents information about the artwork; at podcast or at episode level.
    /// </summary>
    [DataContract]
    public class ArtworkInfo : IDisposable, IDownloadable
    {
        #region Properties
        private Uri _MediaSource;
        private Stream _MediaStream;
        private HttpClientProgress httpClient;
        private bool isDisposed = false;

        /// <summary>
        /// Local path to the artwork on the file system.
        /// </summary>
        [DataMember]
        public string LocalArtworkPath { get; set; }
        /// <summary>
        /// The URI that represents the artwork.
        /// </summary>
        [DataMember]
        public Uri MediaSource { get => _MediaSource; set => _MediaSource = value; }
        /// <summary>
        /// URI to the thumbnail or smaller version of the artwork.
        /// </summary>
        [DataMember]
        public Uri ThumbnailUri { get; set; }
        /// <summary>
        /// Byte array of the artwork image.
        /// </summary>
        public byte[] MediaBytes { get; set; }
        #endregion Properties

        #region Class Constructors
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ArtworkInfo() { }
        /// <summary>
        /// Constructor to set artwork with URI to image file.
        /// </summary>
        /// <param name="MediaSource">URI to the image file.</param>
        public ArtworkInfo(Uri MediaSource) => _MediaSource = MediaSource;
        /// <summary>
        /// Constructor to set artwork with URI to image file.
        /// </summary>
        /// <param name="MediaSource">URI to the image file.</param>
        public ArtworkInfo(string MediaSource) => _MediaSource = new Uri(MediaSource);
        /// <summary>
        /// Constructor to set the artwork file from a byte array.
        /// </summary>
        /// <param name="MediaBytes">Byte array that represents the artwork.</param>
        public ArtworkInfo(byte[] MediaBytes)
        {
            this.MediaBytes = MediaBytes;
        }

        #endregion Class Constructors

        #region Interface Implementations
        public void Dispose()
        {
            MediaBytes = null;
            _MediaStream?.Dispose();
            httpClient?.Dispose();
            isDisposed = true;
        }

        /// <summary>
        /// Represents the same value as ArtworkReady, to show if the item is downloaded.
        /// </summary>
        /// <returns>True/False</returns>
        public bool IsDownloaded() => ArtworkReady();
        #endregion Interface Implementations

        #region Public Methods
        /// <summary>
        /// Checks to see if we have a valid byte array for the artwork and
        /// that the object isn't disposed already.
        /// </summary>
        /// <returns>True/False</returns>
        public bool ArtworkReady()
        {
            bool result = false;
            if (MediaBytes != null && !isDisposed && MediaBytes.Length > 0)
                result = true;
            return result;
        }

        /// <summary>
        /// Downloads the artwork file from the MediaSource and stores it into the object's MediaBytes.
        /// </summary>
        /// <param name="ProgressCallback">Callback function to get progress information.</param>
        /// <param name="CancellationToken">Cancellation token source to cancel a download-in-progress.</param>
        /// <returns>Task</returns>
        public async Task DownloadFileAsync(IProgress<HttpProgressInfo> ProgressCallback = null, CancellationTokenSource CancellationToken = null)
        {
            if (_MediaStream == null)
            {
                if (httpClient == null)
                    httpClient = new HttpClientProgress(MediaSource);
                if (ProgressCallback != null)
                {
                    httpClient.ProgressChanged += (totalFileSize, bytesDownloaded, percentage) =>
                    {
                        var info = new HttpProgressInfo()
                        {
                            TotalBytesToReceive = totalFileSize,
                            BytesReceived = bytesDownloaded,
                            ProgressPercentage = percentage
                        };
                        ProgressCallback.Report(info);
                    };
                }
                _MediaStream = await httpClient.StartDownloadAsync();
            }
            CopyStreamToBytes();
        }

        /// <summary>
        /// Gets a stream object from MediaBytes.
        /// </summary>
        /// <returns>Stream object</returns>
        public Stream GetStream()
        {
            if (isDisposed)
                throw new ObjectDisposedException("MediaStream", "The object is currently NULL or disposed. Check IsDownloaded before accessing the stream.");
            MemoryStream tempStream = new MemoryStream(MediaBytes);
            tempStream.Seek(0, SeekOrigin.Begin);
            return tempStream;
        }

        /// <summary>
        /// Sets MediaButes from the given stream object.
        /// </summary>
        /// <param name="stream">Stream object to be copies to MediaBytes.</param>
        public void SetStream(Stream stream)
        {
            if (!stream.CanRead)
                throw new ObjectDisposedException(nameof(stream), "Cannot access object, currently disposed!");
            _MediaStream = stream;
            if (MediaBytes != null) MediaBytes = null;
            CopyStreamToBytes();
        }
        #endregion Public Methods

        #region Private Methods
        /// <summary>
        /// Copies _MediaStream to MediaBytes
        /// </summary>
        private void CopyStreamToBytes()
        {
            using (var tempStream = new MemoryStream())
            {
                _MediaStream.Seek(0, SeekOrigin.Begin);
                _MediaStream.CopyTo(tempStream);
                MediaBytes = tempStream.ToArray();
            }
        }
        #endregion Private Methods
    }
}
