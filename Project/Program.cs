#region

using System;
using Com.Wodzu.EightTracksGrabber.Core;
using SharpPcap;

#endregion

namespace Com.Wodzu.EightTracksGrabber
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            var devices = CaptureDeviceList.Instance;
            for (var i = 0; i < devices.Count; i++)
                Console.WriteLine("Device ID: {0}\r\n{1}\n", i, devices[i]);

            Console.Write("Choose device: ");
            var k = 0;
            var input = Console.ReadLine();
            int.TryParse(input, out k);
            var captureDevice = devices[k];

            Console.WriteLine("Starting Grabber...");
            var songGrabber = new SongGrabber(captureDevice, true);
            songGrabber.Start();
        }
    }
}