using OpenCvSharp;
using System;
using System.IO;

namespace RfidManagementSystem.Hardware.Cctv
{
    public class CameraSnapshotService
    {
        public string TakeSnapshot(
            string rtspUrl,
            string cameraName,
            string? rfidCardNumber = null)
        {
            try
            {
                using var capture = new VideoCapture();

                // Open RTSP stream
                capture.Open(rtspUrl);

                if (!capture.IsOpened())
                {
                    throw new Exception(
                        $"Unable to connect to camera: {cameraName}"
                    );
                }

                // Give camera a moment to initialize
                System.Threading.Thread.Sleep(1000);

                using var frame = new Mat();

                // Read frame
                if (!capture.Read(frame) || frame.Empty())
                {
                    throw new Exception(
                        "Unable to capture image from camera."
                    );
                }

                // Create Evidence folder
                string evidenceFolder = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Evidence"
                );

                if (!Directory.Exists(evidenceFolder))
                {
                    Directory.CreateDirectory(evidenceFolder);
                }

                // Safe timestamp
                string timestamp = DateTime.Now
                    .ToString("yyyyMMdd_HHmmss");

                // Create filename
                string fileName;

                if (!string.IsNullOrWhiteSpace(rfidCardNumber))
                {
                    fileName =
                        $"{cameraName}_{rfidCardNumber}_{timestamp}.jpg";
                }
                else
                {
                    fileName =
                        $"{cameraName}_{timestamp}.jpg";
                }

                // Replace invalid filename characters
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    fileName = fileName.Replace(c, '_');
                }

                string filePath = Path.Combine(
                    evidenceFolder,
                    fileName
                );

                // Save image
                Cv2.ImWrite(filePath, frame);

                return filePath;
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Snapshot failed: {ex.Message}",
                    ex
                );
            }
        }
    }
}