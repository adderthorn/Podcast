using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.SyndicationFeed;
using Microsoft.SyndicationFeed.Rss;

namespace Monosoftware.Podcast
{
    [DataContract]
    public class Podcast
    {
        #region Class Constructors
        /// <summary>
        /// Default constructor.
        /// </summary>
        public Podcast()
        {
            Episodes = new List<Episode>();
        }

        /// <summary>
        /// Creates an instance fo the Podcast class with a FeedUri specified.
        /// </summary>
        /// <param name="FeedUri">RSS URI for the podcast.</param>
        public Podcast(Uri FeedUri)
        {
            this.FeedUri = FeedUri;
            Episodes = new List<Episode>();
        }
        #endregion Class Constructors

        #region Private Variables
        private const int DEFAULT_MAX_EPISODES = 20;
        private DateTime _LastBuildDate = DateTime.MinValue;
        private DateTime _LastRefreshDate = DateTime.MinValue;
        private int _MaxEpisodes = DEFAULT_MAX_EPISODES;
        private string _Title;
        #endregion

        #region Public Properties
        /// <summary>
        /// Uri to the RSS feed; this value can only be set when creating a new
        /// instance of the Podcast class.
        /// </summary>
        
        [DataMember]
        public Uri FeedUri { get; private set; }

        /// <summary>
        /// The title of the podcast.
        /// </summary>
        [DataMember]
        public string Title { get => _Title; set => _Title = value.Trim(); }

        /// <summary>
        /// Link to the podcast's homepage.
        /// </summary>
        [DataMember]
        public Uri Link { get; set; }

        /// <summary>
        /// Podcast author.
        /// </summary>
        [DataMember]
        public string Author { get; set; }

        /// <summary>
        /// Generator of the podcast feed.
        /// </summary>
        [DataMember]
        public string Generator { get; set; }

        /// <summary>
        /// Language of the podcast.
        /// </summary>
        [DataMember]
        public string Language { get; set; }

        /// <summary>
        /// Copyright information about the podcast.
        /// </summary>
        [DataMember]
        public string Copyright { get; set; }

        /// <summary>
        /// Editor of the podcast.
        /// </summary>
        [DataMember]
        public string Editor { get; set; }

        /// <summary>
        /// Webmaster of the podcast.
        /// </summary>
        [DataMember]
        public string Webmaster { get; set; }

        /// <summary>
        /// Podcast TTL (Time to live).
        /// </summary>
        [DataMember]
        public int Ttl { get; set; }

        /// <summary>
        /// Max number of episodes of the podcast to be stored.
        /// </summary>
        [DataMember]
        public int MaxEpisodes
        {
            get => _MaxEpisodes;
            set => _MaxEpisodes = value;
        }

        /// <summary>
        /// Last build date of the podcast from the RSS feed.
        /// </summary>
        [DataMember]
        public DateTime LastBuildDate
        {
            get => _LastBuildDate;
            set => _LastRefreshDate = value;
        }

        /// <summary>
        /// Last refreshed date of the podcast (to be set by the application using this library).
        /// </summary>
        [DataMember]
        public DateTime LastRefreshDate
        {
            get => _LastRefreshDate;
            set => _LastRefreshDate = value;
        }

        /// <summary>
        /// Podcast description.
        /// </summary>
        [DataMember]
        public string Description { get; set; }

        /// <summary>
        /// Podcast artwork information.
        /// </summary>
        [DataMember]
        public ArtworkInfo Artwork { get; set; }

        /// <summary>
        /// All the episodes that belong to this podcast.
        /// </summary>
        [DataMember]
        public List<Episode> Episodes { get; set; }

        /// <summary>
        /// A count of the number of episodes currently saved with this podcast.
        /// </summary>
        public int EpisodeCount { get => Episodes.Count; }
        #endregion

        #region Public Methods
        public Episode GetMostCurrentEpisodes()
        {
            Episode returnEpisode = (from e in Episodes
                                     select e).FirstOrDefault();
            return returnEpisode;
        }

        public List<Episode> GetMostCurrentEpisodes(int NumberOfEpisodes)
        {
            List<Episode> returnEpisodes = Episodes.Take(NumberOfEpisodes).ToList();
            return returnEpisodes;
        }

        public async Task RefreshPodcastAsync(bool UseEpisodeArtwork, bool AppendToEnd)
        {
            int AppendIndex = AppendToEnd ? -1 : 0;
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add(CastHelpers.USER_AGENT_TEXT, CastHelpers.UserAgent);
            var request = new HttpRequestMessage(HttpMethod.Get, FeedUri);
            var responseMessage = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            RssFeedReader feed;
            int episodeCount = 0;
            using (Stream responseStream = await responseMessage.Content.ReadAsStreamAsync())
            {
                using (var reader = XmlReader.Create(responseStream, new XmlReaderSettings() { Async = true }))
                {
                    feed = new RssFeedReader(reader);
                    while (await feed.Read())
                    {
                        if (feed.ElementType == SyndicationElementType.Item)
                        {
                            if (episodeCount++ >= MaxEpisodes) break;
                            AddEpisodeFromSyndicationContent(await feed.ReadContent(), UseEpisodeArtwork, AppendIndex);
                        }
                    }
                }
            }
        }

        public void ShrinkEpisodesToCount(int count)
        {
            if (EpisodeCount <= count) return;
            Episodes = (from ep in Episodes
                        orderby ep.PublishDate descending
                        select ep).Take(count).ToList();
        }

        public void SetFeedUri(Uri FeedUri)
        {
            this.FeedUri = FeedUri;
        }

        public void SetFeedUri(string FeedUri)
        {
            this.FeedUri = new Uri(FeedUri);
        }

        public void AddEpisodeFromSyndicationContent(ISyndicationContent syndicationContent, bool UseEpisodeArtwork, int InsertIndex)
        {
            ISyndicationItem item = new RssParser().CreateItem(syndicationContent);
            if (this.Episodes.Count(e => String.Equals(e.Title, item.Title.Trim(), StringComparison.OrdinalIgnoreCase) && e.PublishDate == item.Published) > 0) return;
            Episode episode = new Episode()
            {
                PodcastTitle = this.Title,
                Title = item.Title,
                Description = item.Description,
                PublishDate = item.Published,
                Duration = TimeSpan.MinValue
            };
            ISyndicationContent content = syndicationContent.Fields.FirstOrDefault(c => String.Equals(c.Name, RssElementNamesExt.Encoded, StringComparison.OrdinalIgnoreCase)
                && String.Equals(c.Namespace, XmlNamespaces.Content, StringComparison.OrdinalIgnoreCase));
            if (content != null) episode.Description = content.Value;

            ISyndicationLink mediaUri = item.Links.FirstOrDefault(l => String.Equals(l.RelationshipType, RssLinkTypes.Enclosure, StringComparison.OrdinalIgnoreCase));
            if (mediaUri == null)
                mediaUri = item.Links.FirstOrDefault(l => String.Equals(l.MediaType, RssElementNamesExt.MP3, StringComparison.OrdinalIgnoreCase));
            if (mediaUri == null)
                mediaUri = new SyndicationLink(new Uri(CastHelpers.DEFAULT_MP3));
            episode.MediaSource = mediaUri.Uri;
            episode.DurationLong = mediaUri.Length;
            if (UseEpisodeArtwork)
            {
                Uri artworkUri = CastHelpers.CheckForUri(syndicationContent.Fields.FirstOrDefault(c => String.Equals(c.Name, RssElementNames.Image, StringComparison.OrdinalIgnoreCase)
                    && String.Equals(c.Namespace, XmlNamespaces.iTunes, StringComparison.OrdinalIgnoreCase)));
                if (artworkUri == null)
                    artworkUri = CastHelpers.CheckForUri(syndicationContent.Fields.FirstOrDefault(c => String.Equals(c.Name, RssElementNamesExt.Artwork)));
                if (artworkUri == null && this.Artwork != null)
                    artworkUri = this.Artwork.MediaSource;
                else
                    episode.HasUniqueArtwork = true;

                if (artworkUri != null)
                {
                    episode.Artwork = new ArtworkInfo(artworkUri);
                }
                else
                {
                    episode.Artwork = this.Artwork;
                }
            }
            else
            {
                episode.Artwork = new ArtworkInfo();
            }
            episode.GenerateGUID();
            if (InsertIndex < 0 || InsertIndex >= this.EpisodeCount)
            {
                this.Episodes.Add(episode);
            }
            else
            {
                this.Episodes.Insert(InsertIndex, episode);
            }
        }
        #endregion

        #region Static Methods
        /// <summary>
        /// Gets a new instance of a podcast asynchronously from a feed Uri of the Uri class.
        /// </summary>
        /// <param name="FeedUri">Uri of the podcast's RSS feed.</param>
        /// <returns>An awaitable podcast.</returns>
        public static async Task<Podcast> GetPodcastAsync(Uri FeedUri) => await GetPodcastAsync(FeedUri, DEFAULT_MAX_EPISODES);

        /// <summary>
        /// Gets a new instance of a podcast asynchronously from a feed Uri of the string class.
        /// </summary>
        /// <param name="FeedUri">Uri of the podcast's RSS feed.</param>
        /// <returns>An awaitable podcast.</returns>
        public static async Task<Podcast> GetPodcastAsync(string FeedUri)
        {
            Uri newUri = new Uri(FeedUri);
            return await GetPodcastAsync(newUri, DEFAULT_MAX_EPISODES);
        }

        /// <summary>
        /// Gets a new instance of a podcast asynchronously from a feed Uri of the string class
        /// with a specified number of maximum episodes.
        /// </summary>
        /// <param name="FeedUri">Uri of the podcast's RSS feed.</param>
        /// <param name="MaxEpisodes">Maximum number of episodes of the podcast to be stored.</param>
        /// <returns>An awaitable podcast.</returns>
        public static async Task<Podcast> GetPodcastAsync(string FeedUri, int MaxEpisodes)
        {
            Uri newUri = new Uri(FeedUri);
            return await GetPodcastAsync(newUri, MaxEpisodes);
        }

        /// <summary>
        /// Gets a new instance of a podcast asynchronously from a feed Uri of the Uri class
        /// with a specified number of maximum episodes.
        /// </summary>
        /// <param name="FeedUri">Uri of the podcast's RSS feed.</param>
        /// <param name="MaxEpisodes">Maximum number of episodes of the podcast to be stored.</param>
        /// <returns>An awaitable podcast.</returns>
        public static async Task<Podcast> GetPodcastAsync(Uri FeedUri, int MaxEpisodes)
        {
            RssFeedReader feedReader;
            Podcast podcast = new Podcast(FeedUri)
            {
                LastBuildDate = DateTime.Now,
                LastRefreshDate = DateTime.Now
            };
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add(CastHelpers.USER_AGENT_TEXT, CastHelpers.UserAgent);
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, FeedUri);
            var responseMessage = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead);
            using (Stream responseStream = await responseMessage.Content.ReadAsStreamAsync())
            {
                using (var reader = XmlReader.Create(responseStream, new XmlReaderSettings() { Async = true }))
                {
                    feedReader = new RssFeedReader(reader);
                    bool foundHighResImage = false;
                    bool foundBuildDate = false;
                    bool foundLink = false;
                    int episodeCount = 0;
                    while (await feedReader.Read())
                    {
                        switch (feedReader.ElementName)
                        {
                            case RssElementNames.Title:
                                podcast.Title = await feedReader.ReadValue<string>();
                                continue;
                            case RssElementNamesExt.Subtitle:
                                podcast.Description = await feedReader.ReadValue<string>();
                                continue;
                            case RssElementNamesExt.Artwork:
                            case RssElementNames.Image:
                                Uri artworkUri = CastHelpers.CheckForUri(await feedReader.ReadContent());
                                if (artworkUri != null)
                                {
                                    Debug.WriteLine(artworkUri);
                                    foundHighResImage = true;
                                    podcast.Artwork = new ArtworkInfo(artworkUri);
                                    await podcast.Artwork.DownloadFileAsync();
                                }
                                continue;
                            case RssElementNames.Copyright:
                                podcast.Copyright = await feedReader.ReadValue<string>();
                                continue;
                            case RssElementNames.Language:
                                podcast.Language = await feedReader.ReadValue<string>();
                                continue;
                            case RssElementNames.LastBuildDate:
                            case RssElementNames.PubDate:
                                if (!foundBuildDate)
                                {
                                    DateTime buildDate;
                                    try
                                    {
                                        buildDate = await feedReader.ReadValue<DateTime>();
                                        foundBuildDate = true;
                                    }
                                    catch
                                    {
                                        buildDate = DateTime.MinValue;
                                    }
                                    podcast.LastBuildDate = buildDate;
                                }
                                continue;
                            case RssElementNames.Author:
                                podcast.Author = await feedReader.ReadValue<string>();
                                continue;
                            case RssElementNames.TimeToLive:
                                podcast.Ttl = await feedReader.ReadValue<int>();
                                continue;
                            case RssElementNames.Generator:
                                podcast.Generator = await feedReader.ReadValue<string>();
                                continue;
                            default:
                                break;
                        }
                        switch (feedReader.ElementType)
                        {
                            case SyndicationElementType.Image:
                                if (!foundHighResImage)
                                {
                                    ISyndicationImage artworkImage = await feedReader.ReadImage();
                                    podcast.Artwork.MediaSource = artworkImage.Url;
                                    await podcast.Artwork.DownloadFileAsync();
                                }
                                continue;
                            case SyndicationElementType.Item:
                                if (episodeCount == 0 || episodeCount < MaxEpisodes)
                                {
                                    podcast.AddEpisodeFromSyndicationContent(await feedReader.ReadContent(), false, -1);
                                    episodeCount++;
                                }
                                continue;
                            case SyndicationElementType.Link:
                                if (!foundLink)
                                {
                                    var link = await feedReader.ReadLink();
                                    podcast.Link = link.Uri;
                                    foundLink = true;
                                }
                                continue;
                            default:
                                break;
                        }
                    }
                }
            }
            return podcast;
        }
        #endregion
    }
}
