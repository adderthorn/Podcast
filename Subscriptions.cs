using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml.Linq;
using System.Diagnostics;
using System.ComponentModel;

namespace Monosoftware.Podcast
{
    /// <summary>
    /// Class that stores subscriptions to podcasts.
    /// </summary>
    [DataContract]
    public class Subscriptions : INotifyPropertyChanged
    {
        #region Constants
        /// <summary>
        /// Span of 10-seconds, used to indicate a major time of listenting has occured; can be used to 
        /// determine if the playback position of the podcast has changed and therefore should be saved.
        /// </summary>
        public static readonly TimeSpan MajorTimespanChange = new TimeSpan(0, 0, 10); // 10 Seconds
        #endregion Constants

        #region Private Variables
        private ObservableCollection<Podcast> _Podcasts = new ObservableCollection<Podcast>();
        #endregion Private Variables

        #region Public Properties
        /// <summary>
        /// Event handler for when a property of the subscriptions has changed.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the last modified date of the subscriptions.
        /// </summary>
        [DataMember(Order = 0)]
        public DateTime LastModifiedDate { get; set; }

        /// <summary>
        /// Gets or sets the collection of all of the subscribed podcasts.
        /// </summary>
        [DataMember(Order = 1)]
        public ObservableCollection<Podcast> Podcasts
        {
            get => _Podcasts;
            set => _Podcasts = value;
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes an empty subscriptions class.
        /// </summary>
        public Subscriptions() { }
        #endregion Constructors

        #region Public Methods
        /// <summary>
        /// Adds a podcast to the current subscrition.
        /// </summary>
        /// <param name="podcast">Podcast to add.</param>
        public void AddPodcast(Podcast podcast)
        {
            Podcasts.Add(podcast);
            NotifyPropertyChanged(nameof(Podcasts));
        }

        /// <summary>
        /// Removes a podcast from the current subscription.
        /// </summary>
        /// <param name="podcast">Podcast to be removed.</param>
        public void RemovePodcast(Podcast podcast)
        {
            Podcasts.Remove(podcast);
            NotifyPropertyChanged(nameof(Podcasts));
        }

        /// <summary>
        /// Gets all of the subscribes podcasts where the LocalArtworkPath is empty.
        /// </summary>
        /// <returns>Podcasts with no artwork.</returns>
        public ObservableCollection<Podcast> GetPodcastsWithNoLocalArtworkUri()
        {
            var resultList = new ObservableCollection<Podcast>();
            Podcasts.Where(p => String.IsNullOrEmpty(p.Artwork.LocalArtworkPath)).ToList().ForEach(a => resultList.Add(a));
            return resultList;
        }

        /// <summary>
        /// Refreshes the podcast feeds and adds any new episodes that have been added since the last refresh.
        /// </summary>
        /// <param name="UseEpisodeArtwork">If true, the episode artwork will be downloaded and used if available.</param>
        /// <param name="TotalEpisodesToKeep">Number of episodes to keep for each podcast.</param>
        /// <param name="AppendToEnd">If true, new podcast episodes will be added to the end of the list rather than at the beginning.</param>
        /// <returns></returns>
        public async Task RefreshFeedsAsync(bool UseEpisodeArtwork = false, int TotalEpisodesToKeep = 0, bool AppendToEnd = true)
        {
            int errorCount = 0;
            int notResolvedCount = 0;
            foreach (var podcast in Podcasts)
            {
                try
                {
                    if (TotalEpisodesToKeep > 0) podcast.ShrinkEpisodesToCount(TotalEpisodesToKeep);
                    await podcast.RefreshPodcastAsync(UseEpisodeArtwork, AppendToEnd);
                }
                catch (NullReferenceException ex)
                {
                    if (ex.Message.ToLower().Contains("uri not resolved"))
                        notResolvedCount++;
                    Debug.WriteLine("NullReferenceException! Got error '{0}' on podcast {1}.", ex.Message, podcast.Title);
                    errorCount++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("General Exception! Got error '{0}' on podcast {1}.", ex.Message, podcast.Title);
                    errorCount++;
                }
                if (errorCount > 1) break;
            }
            if (notResolvedCount > 0)
                throw new Exception("Offline!");
            else if (errorCount > 0 && notResolvedCount == 0)
                throw new Exception("Error!");
            NotifyPropertyChanged(nameof(Podcasts));
        }

        /// <summary>
        /// Gets the episodes that are currently pending download and have not already been downloaded.
        /// </summary>
        /// <param name="MaxCount">Maximum number of episodes to download for each podcast.</param>
        /// <returns>Collection of episodes to be downloaded.</returns>
        public IEnumerable<Episode> GetPendingDownloadEpisodes(int MaxCount = 0)
        {
            var episodeList = new List<Episode>();
            episodeList = (from ep in Podcasts.SelectMany(p => p.Episodes)
                           where ep.PendingDownload == true
                           && ep.Downloaded == false
                           select ep).ToList();
            if (MaxCount > 0)
            {
                episodeList = episodeList.OrderBy(ep => ep.PublishDate).Reverse().Take(MaxCount).ToList();
            }
            return episodeList;
        }

        /// <summary>
        /// Gets the first active episode.
        /// </summary>
        /// <returns>The active episode.</returns>
        public Episode GetActiveEpisode()
        {
            var episodeList = (from p in Podcasts
                               where p.Episodes.Any(e => e.ActiveEpisode)
                               select p.Episodes.FirstOrDefault(e => e.ActiveEpisode)).FirstOrDefault();
            return episodeList;
        }

        /// <summary>
        /// Generates a new GUID for each podcast episode.
        /// </summary>
        /// <param name="OverwriteCurrent">If true, any episodes with exiting GUIDs will be overwritten, 
        /// otherwise existing GUIDs will be kept.</param>
        public void GenerateEpisodeGuids(bool OverwriteCurrent = false)
        {
            foreach (var p in Podcasts)
            {
                if (OverwriteCurrent)
                {
                    p.Episodes.ForEach(e => e.GUID = new Guid().ToString());
                }
                else
                {
                    p.Episodes.Where(e => String.IsNullOrWhiteSpace(e.GUID)).ToList().ForEach(e => e.GUID = new Guid().ToString());
                }
            }
        }

        /// <summary>
        /// Returns the episode with the specified GUID; this will search through all podcasts.
        /// </summary>
        /// <param name="GUID">The GUID of the podcast to find.</param>
        /// <returns>The episode.</returns>
        public Episode GetEpisodeFromGuid(string GUID)
        {
            Episode episode = null;
            episode = Podcasts.SelectMany(p => p.Episodes).Where(e => e.GUID == GUID).FirstOrDefault();
            return episode;
        }

        /// <summary>
        /// Gets an enumeration of all episodes that have been downloaded and played to completion;
        /// useful to determine which episodes should be deleted from local storage.
        /// </summary>
        /// <param name="Count">Maximum number of episodes to be deleted.</param>
        /// <returns>List of episodes.</returns>
        public IEnumerable<Episode> GetEpisodesToRemove(int Count)
        {
            var totalEpisodeList = new List<Episode>();
            foreach (var p in Podcasts)
            {
                var episodeList = (from e in p.Episodes
                                   where e.Downloaded && (e.PlaybackPosition == e.Duration
                                   || e.PlaybackPosition == TimeSpan.MinValue)
                                   && !String.IsNullOrEmpty(e.LocalFileToken)
                                   orderby e.PublishDate ascending
                                   select e).ToList();
                if (episodeList.Count > Count)
                {
                    totalEpisodeList.AddRange(episodeList);
                }
            }
            return totalEpisodeList;
        }

        /// <summary>
        /// Finds a podcast in the subscription list by the specified name.
        /// </summary>
        /// <param name="PodcastName">The name of the podcast to find.</param>
        /// <returns>The podcast.</returns>
        public Podcast GetPodcastByName(string PodcastName)
        {
            Podcast podcast = null;
            podcast = (from p in Podcasts
                       where p.Title.Equals(PodcastName, StringComparison.CurrentCultureIgnoreCase)
                       orderby p.Title ascending
                       select p).FirstOrDefault();
            return podcast;
        }

        /// <summary>
        /// From the specified OPML stream, add each podcast that doesn't already exist to the current subscription list.
        /// </summary>
        /// <param name="OpmlStream">Stream of the OPML file.</param>
        /// <param name="progress">IProgress to present the current podcast being added while running.</param>
        /// <returns>Count of podcasts not added due to errors.</returns>
        public async Task<int> AddPodcastsFromOpmlAsync(Stream OpmlStream, IProgress<string> progress)
        {
            const string xmlUrl = "xmlUrl";
            XDocument doc = XDocument.Load(OpmlStream);
            IEnumerable<XElement> opmlElements = from d in doc.Descendants("outline")
                                                 where d.Attributes().Any(a => a.Name == xmlUrl)
                                                 select d;
            int errorCount = 0;
            foreach (XElement element in opmlElements)
            {
                string url = element.Attribute(xmlUrl).Value;
                string title = element.Attribute("title").Value;
                if (String.IsNullOrWhiteSpace(title)) title = url;
                progress?.Report(title);
                if (!await CastHelpers.CheckUriValidAsync(url))
                {
                    errorCount++;
                    continue;
                }
                this.AddPodcast(await Podcast.GetPodcastAsync(url));
            }
            return errorCount;
        }
        #endregion

        #region Private Methods
        private void NotifyPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion

        #region Static Methods
        /// <summary>
        /// From a given enumeration of podcast Uris, create a subscription class.
        /// </summary>
        /// <param name="PodcastUris">Podcast RSS feed Uris to subscribe to.</param>
        /// <returns>An awaitable subscriptions.</returns>
        public static async Task<Subscriptions> GetSubscriptionsAsync(IEnumerable<Uri> PodcastUris)
        {
            Subscriptions subscriptions = new Subscriptions();
            foreach (Uri uri in PodcastUris)
            {
                Podcast podcast = await Podcast.GetPodcastAsync(uri);
                subscriptions.AddPodcast(podcast);
            }
            subscriptions.GenerateEpisodeGuids(false);
            return subscriptions;
        }

        /// <summary>
        /// From a given enumeration of podcast Uris, create a subscription class.
        /// </summary>
        /// <param name="PodcastUris">Podcast RSS feed Uris to subscribe to.</param>
        /// <returns>An awaitable subscriptions.</returns>
        public static async Task<Subscriptions> GetSubscriptionsAsync(IEnumerable<string> PodcastUris)
        {
            Subscriptions subscriptions = new Subscriptions();
            foreach (string uri in PodcastUris)
            {
                Podcast podcast = await Podcast.GetPodcastAsync(uri);
            }
            subscriptions.GenerateEpisodeGuids(false);
            return subscriptions;
        }
        #endregion
    }
}
