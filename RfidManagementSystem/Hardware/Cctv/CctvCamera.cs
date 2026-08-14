using OpenCvSharp;
using System.Threading;

namespace RfidManagementSystem.Hardware.Cctv
{
    public class CctvCamera
    {
        private readonly string _rtspUrl;

        public CctvCamera(string rtspUrl)
        {
            _rtspUrl = rtspUrl;
        }

        public Mat? TakeSnapshot()
        {
            using var capture = new VideoCapture();

            // Explicitly use FFmpeg for RTSP
            capture.Open(
                _rtspUrl,
                VideoCaptureAPIs.FFMPEG
            );

            if (!capture.IsOpened())
            {
                return null;
            }

            using var frame = new Mat();

            // RTSP stream may need time to start
            Thread.Sleep(3000);

            // Read multiple frames
            for (int i = 0; i < 30; i++)
            {
                bool success = capture.Read(frame);

                if (success && !frame.Empty())
                {
                    return frame.Clone();
                }

                Thread.Sleep(100);
            }

            return null;
        }
    }
}