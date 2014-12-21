#region

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Com.Wodzu.EightTracksGrabber.Core;
using Com.Wodzu.WebAutomation.Helpers;
using Newtonsoft.Json.Linq;
using NLog;

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
		///     The the song's title.
		/// </summary>
		string Title { get; }

		/// <summary>
		///     The the artist of the song.
		/// </summary>
		string Artist { get; }

		/// <summary>
		///     The User, that created this playlist.
		/// </summary>
		string User { get; }

		/// <summary>
		///     The donwload URL of the song.
		/// </summary>
		string DownloadUrl { get; }

		/// <summary>
		///     The filename of the song.
		/// </summary>
		string FileName { get; }

		/// <summary>
		///     The album this song belongs to.
		/// </summary>
		string Album { get; }

		/// <summary>
		///     The the year this tack has been published.
		/// </summary>
		int Year { get; }

		/// <summary>
		///     The the URL to the track.
		/// </summary>
		string TrackUrl { get; }

		/// <summary>
		///     The the URL to the image used for this track.
		/// </summary>
		string ImageUrl { get; }

		/// <summary>
		///     Downloads the song, from the DownloadUrl of this response.
		/// </summary>
		/// <param name="targetBaseDir">The directory were all downloaded playlist are to be placed.</param>
		/// <param name="generateTags">Wether to generate ID3 tags from the response's properties.</param>
		/// <param name="overwrite">Wether to overwrite already existing files.</param>
		void DownloadSong(string targetBaseDir, bool generateTags = true, bool overwrite = false);
	}

	/// <summary>
	///     Represents a response from 8Tracks.com.
	/// </summary>
	public class SongResponse : ISongResponse
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public SongResponse(JObject jObject, string referer)
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

			FileName = string.Format("{0} - {1}", Artist, Title);

			var splitted = referer.Split('/');
			PlayList = (splitted.Length >= 2) ? splitted[1] : "unknown playlist";
			User = (splitted.Length >= 3) ? splitted[2] : "unknown user";
		}

		public string PlayList { get; private set; }
		public string Title { get; private set; }
		public string Artist { get; private set; }
		public string User { get; private set; }
		public string DownloadUrl { get; private set; }
		public string FileName { get; private set; }
		public string Album { get; private set; }
		public int Year { get; private set; }
		public string TrackUrl { get; private set; }
		public string ImageUrl { get; private set; }

		public void DownloadSong(string targetBaseDir, bool generateTags = true, bool overwrite = false)
			// TODO: make configurable
		{
			Task.Factory.StartNew(() =>
			{
				// TODO: download playlist pic 
				// TODO: use it for album art
				var target = GetDownloadTarget(targetBaseDir);
				if (!overwrite && FileNameExists(target)) // if audiofile already exists, don't create a .download file
				{
					Logger.Info("An audio file with name \"{0}\" already exists, aborting download", FileName);
					return;
				}
				FileGrabber.Download(DownloadUrl, target, true);
				FinishDownload(target, generateTags);
			});
		}

		#region private methods

		private bool FileNameExists(FileInfo target)
		{
			return target.Directory != null &&
			       (target.Directory.Exists &&
			        target.Directory.GetFiles()
				        .Any(
					        file => Path.GetFileNameWithoutExtension(file.FullName).Equals(FileName, StringComparison.InvariantCulture)));
		}

		private void FinishDownload(FileInfo target, bool generateTags)
		{
			var container = AudioContainer.GetAudioContainer(target.FullName);
			if (string.IsNullOrWhiteSpace(container))
			{
				Logger.Warn("Unknown container format, cannot set file extension.");
				return;
			}
			var newFile = Path.ChangeExtension(target.FullName, container);
			File.Move(target.FullName, newFile);
			if (generateTags) AddId3Tags(new FileInfo(newFile));
		}

		private void AddId3Tags(FileInfo target)
		{
			using (var file = TagLib.File.Create(target.FullName))
			{
				file.Tag.Title = Title;
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
				new FileInfo(String.Format("{0}{1}{2}{3}{4}{5}{6}.download", targetBaseDir, Path.DirectorySeparatorChar, PlayList,
					Path.DirectorySeparatorChar, User, Path.DirectorySeparatorChar, normalizedFileName));
		}

		#endregion private methods
	}
}