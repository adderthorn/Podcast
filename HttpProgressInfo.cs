namespace Monosoftware.Podcast
{
    /// <summary>
    /// Struct representing progress info for the HttpClientProgress class.
    /// </summary>
    public struct HttpProgressInfo
    {
        /// <summary>
        /// Current number of bytes received.
        /// </summary>
        public long BytesReceived { get; set; }

        /// <summary>
        /// Total number of bytes expected to be received.
        /// </summary>
        public long? TotalBytesToReceive { get; set; }

        /// <summary>
        /// Current percentage of download completed.
        /// </summary>
        public double? ProgressPercentage { get; set; }
    }
}
