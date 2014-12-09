#region

using System;
using Com.Wodzu.EightTracksGrabber.Helper;
using Com.Wodzu.EightTracksGrabber.Packets;
using Com.Wodzu.WebAutomation.Packet.Http;
using Newtonsoft.Json.Linq;
using NLog;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using SharpPcap.WinPcap;

#endregion

namespace Com.Wodzu.EightTracksGrabber.Core
{
    // TODO modes: active(use webclient and for fetching json response) / passive(listen and wait for matching json)

    /// <summary>
    ///     Grabs tracks from 8Tracks.com by sniffing the network traffic between client and service for API requests and
    ///     extracting the resource URLs to the desired song.
    /// </summary>
    public sealed class SongGrabber : FileGrabber
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly bool _activeMode;

        // keeps track of the current playlist, since its name is only transmitted on the first request
        private string _currentPlaylist;

        /// <summary>
        ///     Initializes a new instance of a SongGrabber.
        /// </summary>
        /// <param name="captureDevice">The device, which is used for.</param>
        /// <param name="activeMode">
        ///     Wether to grab files actively or passively. Active mode filters song requests and resend them
        ///     in order to retreive the file location directly from the server response. Passive mode only filters the responses
        ///     for the request and extracts the file location.
        /// </param>
        public SongGrabber(ICaptureDevice captureDevice, bool activeMode = false)
            : base(captureDevice, GetFilterString(captureDevice))
        {
            _activeMode = activeMode;
        }

        protected override void ProcessPacket(Packet packet)
        {
            try
            {
                var tcp = (TcpPacket) packet.Extract(typeof (TcpPacket));
                if (tcp.PayloadData.Length == 0) return;
                IRawHttpPacket rawHttpPacket = new RawHttpPacket(tcp.PayloadData);

                ISongResponse songResponse = null;
                if (_activeMode)
                {
                    var songRequest = rawHttpPacket.FilterSongRequest();
                    if (songRequest == null) return;
                    Logger.Debug("Captured request: {0}", songRequest.RequestUrl);
                    SetCurrentPlayList(songRequest);
                    if (songRequest.GotBoobs()) return; // filter requests, that have been sent by the grabber itself

                    var response = songRequest.GetResponse();
                    Logger.Debug("Captured response: {0}", response);
                    songResponse = new SongResponse(JObject.Parse(response), _currentPlaylist);
                }
                else
                {
                    // TODO: reassemble packets
                    // TODO: filter response
                }
                if (songResponse == null) return;
                songResponse.DownloadSong(DownloadDirectory.FullName);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to process packet.");
                Logger.Error(ex);
            }
        }

        #region private methods

        private static string GetFilterString(ICaptureDevice captureDevice)
        {
            return String.Format("((tcp dst port 80) and (src net {0})) or ((dst net {0}) and (tcp src port 80))",
                ((LibPcapLiveDevice) captureDevice).Addresses[1].Addr);
        }

        private void SetCurrentPlayList(IHttpRequest request)
        {
            if (!String.IsNullOrWhiteSpace(request.Referer)) _currentPlaylist = request.Referer.Replace(String.Format("http://{0}", request.Host), "");
            Logger.Debug("Current playlist is: \"{0}\"", _currentPlaylist);
        }

        #endregion private methods
    }
}