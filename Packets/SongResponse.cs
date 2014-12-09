#region

using System;
using System.IO;
using System.Threading.Tasks;
using Com.Wodzu.WebAutomation.Core;
using Com.Wodzu.WebAutomatization.Impl.Helpers;
using Newtonsoft.Json.Linq;
using NLog;
using File = TagLib.File;

#endregion

namespace Com.Wodzu.EightTracksGrabber.Packets
{
    /// <summary>
    ///     Offers common properties typically available in JSON responses from 8Tracks.com
    /// </summary>
    public interface ISongResponse
    {
        /// <summary>
        ///     The name of the playlist this response belongs to.
        /// </summary>
        string PlayList { get; }

        /// <summary>
        ///     Gets the song's title.
        /// </summary>
        string Title { get; }

        /// <summary>
        ///     Gets the artist of the song.
        /// </summary>
        string Artist { get; }

        /// <summary>
        ///     Gets the donwload URL of the song.
        /// </summary>
        string DownloadUrl { get; }

        /// <summary>
        ///     Gets filename of the song.
        /// </summary>
        string FileName { get; }

        /// <summary>
        ///     Gets album name of the song.
        /// </summary>
        string Album { get; }

        /// <summary>
        ///     Gets the year this tack has been publish.
        /// </summary>
        int Year { get; }

        /// <summary>
        ///     Gets the URL to the track.
        /// </summary>
        string TrackUrl { get; }

        /// <summary>
        ///     Gets the URL to the image used for this track.
        /// </summary>
        string ImageUrl { get; }

        /// <summary>
        ///     Downloads the song, from the DownloadUrl of this response.
        /// </summary>
        /// <param name="targetBaseDir">The directory were all downloaded playlist are to be placed.</param>
        /// <param name="overwrite">Wether to overwrite already existing files.</param>
        void DownloadSong(string targetBaseDir, bool overwrite = false);
    }

    /// <summary>
    ///     Represents a response from 8Tracks.com.
    /// </summary>
    public class SongResponse : ISongResponse
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public SongResponse(JObject jObject, string playList)
        {
            var set = jObject["set"];
            if (set == null)
            {
                Logger.Warn("No set information available.");
                return;
            }
            var track = set.Get("track");
            if (track == null) return;

            Title = track.Get("name").GetStringValue();
            Artist = track.Get("performer").GetStringValue();
            Album = track.Get("release_name").GetStringValue();
            Year = track.Get("year").GetIntValue();

            ImageUrl = track.Get("image_url").GetStringValue();
            TrackUrl = track.Get("url").GetStringValue();
            DownloadUrl = track.Get("track_file_stream_url").GetStringValue();

            FileName = string.Format("{0} - {1}.mp3", Artist, Title);
            PlayList = playList;
        }

        public string PlayList { get; private set; }
        public string Title { get; private set; }
        public string Artist { get; private set; }
        public string DownloadUrl { get; private set; }
        public string FileName { get; private set; }
        public string Album { get; private set; }
        public int Year { get; private set; }
        public string TrackUrl { get; private set; }
        public string ImageUrl { get; private set; }

        public void DownloadSong(string targetBaseDir, bool overwrite = false)
        {
            Task.Factory.StartNew(() =>
            {
                // TODO: download playlist pic 
                // TODO: use it for album art
                var target = GetDownloadTarget(targetBaseDir);
                FileGrabber.Download(DownloadUrl, target, overwrite);
            //   AddTags(target); // TODO: uncomment
            });
        }

        #region private methods

        private void AddTags(FileInfo target)
        {
            using (var file = File.Create(target.FullName))
            {
                file.Tag.Performers = new[] {Artist};
                file.Tag.Album = Album;
                file.Tag.Year = (uint) Year;
                // TODO: add album art
                /*
                 * IPicture newArt = new Picture(tmpImg);
                    tagFile.Tag.Pictures = new IPicture[1] {newArt};
                 * http://stackoverflow.com/questions/10247216/c-sharp-mp3-id-tags-with-taglib-album-art
                 */
                file.Save();
            }
        }

        private FileInfo GetDownloadTarget(string targetBaseDir)
        {
            var normalizedFileName = FileGrabber.NormalizeFileName(FileName);
            return
                new FileInfo(String.Format("{0}\\{1}\\{2}", targetBaseDir, PlayList,
                    normalizedFileName));
        }

        #endregion private methods
    }
}