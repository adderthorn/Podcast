using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.IO;
namespace Monosoftware.Podcast
{
    /// <summary>
    /// Represents a single episode of a podcast
    /// </summary>
    [DataContract]
    public class Episode : IDisposable, IDownloadable
    {
        #region Private Variables
        private HttpClientProgress httpClient;
        private Stream _MediaStream;
        private bool isDisposed = false;
        private string _Title;
        #endregion

        #region Public Properties
        /// <summary>
        /// The title of the podcast the episode belongs to; used to
        /// relate the episode to the parent podcast.
        /// </summary>
        [DataMember]
        public string PodcastTitle { get; set; }
        /// <summary>
        /// The title of the episode.
        /// </summary>
        [DataMember]
        public string Title { get => _Title; set => _Title = value.Trim(); }
        /// <summary>
        /// The podcast description; this is usually HTML formatted text.
        /// </summary>
        [DataMember]
        public string Description { get; set; }
        /// <summary>
        /// A boolean that represents whether the episode has its own artwork.
        /// </summary>
        [DataMember]
        public bool HasUniqueArtwork { get; set; }
        /// <summary>
        /// The epsiode's artwork; if this is not unique, it should return the
        /// podcast's artwork.
        /// </summary>
        [DataMember]
        public ArtworkInfo Artwork { get; set; }
        /// <summary>
        /// The published date of the episode.
        /// </summary>
        [DataMember]
        public DateTimeOffset PublishDate { get; set; }
        /// <summary>
        /// The URI that represents the MP3 or audio file for the episode.
        /// </summary>
        [DataMember]
        public Uri MediaSource { get; set; }
        /// <summary>
        /// Represents the system path to the locally downloaded episode file.
        /// </summary>
        [DataMember]
        public string LocalFilePath { get; set; }
        /// <summary>
        /// Represents a unique token to the local file; this can be used in platforms
        /// like Microsoft's UWP where access control comes from the API rather than
        /// direct filesystem access.
        /// </summary>
        [DataMember]
        public string LocalFileToken { get; set; }
        /// <summary>
        /// Represents if the episode has been downloaded locally.
        /// </summary>
        [DataMember]
        public bool Downloaded { get; set; }
        /// <summary>
        /// Flag that can be used to queue up a group of episodes to be downloaded.
        /// </summary>
        public bool PendingDownload { get; set; }
        /// <summary>
        /// Flag that can be used to flag an episode as active for use in players, etc.
        /// </summary>
        public bool ActiveEpisode { get; set; }
        /// <summary>
        /// The total duration of the episode; this is set from the feed, not the MP3.
        /// </summary>
        public TimeSpan Duration { get; set; }
        /// <summary>
        /// The current playback position of the episode, so users can resume from a
        /// certain point in the episode.
        /// </summary>
        public TimeSpan PlaybackPosition { get; set; }
        /// <summary>
        /// long integer of playback position to be stored in the XML.
        /// </summary>
        [DataMember]
        public long PlaybackPositionLong
        {
            get => PlaybackPosition.Ticks;
            set => PlaybackPosition = new TimeSpan(value);
        }
        /// <summary>
        /// Long integer of duration to be stored in the XML.
        /// </summary>
        [DataMember]
        public long DurationLong
        {
            get => Duration.Ticks;
            set => Duration = new TimeSpan(value);
        }
        /// <summary>
        /// A unique identifier that represents the episode, can be used to find an exact episode where
        /// a title may be used twice (not globally unique).
        /// </summary>
        [DataMember]
        public string GUID { get; set; }
        /// <summary>
        /// Gets or sets if the episode has been played to completion; setting this will cause the playpack
        /// position to be equal to the total duration of the episode.
        /// </summary>
        public bool IsPlayed
        {
            get
            {
                if (Duration == PlaybackPosition) return true;
                return false;
            }
            set
            {
                if (value)
                {
                    PlaybackPosition = Duration;
                }
                else
                {
                    if (PlaybackPosition == Duration)
                    {
                        PlaybackPosition = TimeSpan.MinValue;
                    }
                }
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Downloads the episode file to the object's internal media stream, the downloaded file can be retreived using the GetStream() method.
        /// </summary>
        /// <param name="progressCallback">Optional callback function to be used for progress.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel a download-in-progress.</param>
        /// <returns>An awaitable task.</returns>
        public async Task DownloadFileAsync(IProgress<HttpProgressInfo> progressCallback = null, CancellationTokenSource cancellationToken = null)
        {
            if (_MediaStream == null)
            {
                if (httpClient == null)
                    httpClient = new HttpClientProgress(MediaSource, cancellationToken);
                httpClient.ProgressChanged += (totalFileSize, bytesDownloaded, percentage) =>
                {
                    var info = new HttpProgressInfo()
                    {
                        TotalBytesToReceive = totalFileSize,
                        BytesReceived = bytesDownloaded,
                        ProgressPercentage = percentage
                    };
                    progressCallback?.Report(info);
                };
                _MediaStream = await httpClient.StartDownloadAsync();
            }
        }

        /// <summary>
        /// Gets a new stream object from the downloaded file; note that the file must be downloaded first,
        /// you can check IsDownloaded before using this method to prevent an ObjectDisposedException.
        /// </summary>
        /// <returns>Stream object of the episode.</returns>
        public Stream GetStream()
        {
            if (_MediaStream == null || isDisposed)
                throw new ObjectDisposedException("MediaStream", "The object is currently NULL or disposed.Check IsDownloaded before accessing the stream.");
            _MediaStream.Seek(0, SeekOrigin.Begin);
            return _MediaStream;
        }

        /// <summary>
        /// Sets the episode stream to a stream object.
        /// </summary>
        /// <param name="stream">Stream object of the episode.</param>
        public void SetStream(Stream stream)
        {
            if (!stream.CanRead || !stream.CanSeek || isDisposed)
                throw new ObjectDisposedException(nameof(stream), "Object is disposed and cannot be read!");
            stream.Seek(0, SeekOrigin.Begin);
            _MediaStream = stream;
        }

        /// <summary>
        /// Disposes of the episode and its stream.
        /// </summary>
        public void Dispose()
        {
            Artwork.Dispose();
            _MediaStream?.Dispose();
            httpClient?.Dispose();
            isDisposed = true;
        }

        /// <summary>
        /// Generates a new GUID and sets the GUID property of the episode.
        /// </summary>
        public void GenerateGUID() => GUID = Guid.NewGuid().ToString();

        /// <summary>
        /// Returns a value to show if the episode has been downloaded.
        /// </summary>
        /// <returns>True/False status of downloaded.</returns>
        public bool IsDownloaded() => Downloaded;
        #endregion
    }
}
