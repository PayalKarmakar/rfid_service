using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using RfidManagementSystem.Hardware.Cctv;
//using RfidManagementSystem.Hardware.Rfid;
using RfidManagementSystem.Services;

namespace RfidManagementSystem
{
    public partial class MainWindow : System.Windows.Window
    {
        //private readonly RfidTcpServer _rfidServer;
        private readonly RfidService _rfidService;
        private readonly CameraSnapshotService _cameraSnapshotService;
        private readonly CctvCamera _cctvCamera;

        //        public MainWindow()
        //        {
        //            InitializeComponent();

        //            // Create RFID TCP Server
        //            _rfidServer = new RfidTcpServer();
        //            _cctvCamera = new CctvCamera(
        //    "rtsp://tapoadmin:tapoadmin@192.168.1.19:554/stream1"
        //);

        //            // Listen for server status changes
        //            _rfidServer.StatusChanged += OnStatusChanged;

        //            // Listen for RFID/TCP data
        //            _rfidServer.DataReceived += OnDataReceived;
        //       //     _cameraSnapshotService =
        //       //new CameraSnapshotService();

        //            // Start TCP Server on port 5000
        //            StartRfidServer();
        //            CaptureSnapshot();
        //        }

        public MainWindow()
        {
            InitializeComponent();
            _rfidService = new RfidService();
            _rfidService.ServerStarted += OnServerStarted;


            _rfidService.ReaderConnected += OnReaderConnected;

            StartRfidService();

            // CCTV
            _cctvCamera = new CctvCamera(
                "rtsp://tapoadmin:tapoadmin@192.168.1.19:554/stream1"
            );
    //        MessageBox.Show(
    //    "RFID Management System started successfully.",
    //    "Application Started",
    //    MessageBoxButton.OK,
    //    MessageBoxImage.Information
    //);


        }

        private async void StartRfidService()
        {
            try
            {
                await _rfidService.StartAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "RFID Service Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void OnServerStarted(string message)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    message,
                    "RFID TCP Server",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            });
        }

        private void OnReaderConnected(
           string readerType,
           string status)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    $"{readerType} RFID Reader Connected!\n\n{status}",
                    "RFID Reader Detected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            });
        }

        //private async void StartRfidServer()
        //{
        //    try
        //    {
        //        // PC will listen on port 5000
        //        await _rfidServer.StartAsync(5000);
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(
        //            ex.Message,
        //            "RFID Server Error",
        //            MessageBoxButton.OK,
        //            MessageBoxImage.Error
        //        );
        //    }
        //}




        //private async void StartRfidServers()
        //{
        //    try
        //    {
        //        await Task.WhenAll(
        //            _entryRfidServer.StartAsync(5000),
        //            _exitRfidServer.StartAsync(5001)
        //        );
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(
        //            ex.Message,
        //            "RFID Server Error",
        //            MessageBoxButton.OK,
        //            MessageBoxImage.Error
        //        );
        //    }
        //}

        //private void OnStatusChanged(string status)
        //{
        //    Dispatcher.Invoke(() =>
        //    {
        //        Title = $"RFID - {status}";

        //        MessageBox.Show(
        //            status,
        //            "TCP Server Status"
        //        );
        //    });
        //}

        private void OnRfidDataReceived(
      string direction,
      string readerIp,
      byte[] data)
        {
            try
            {
                string hexData = BitConverter
                    .ToString(data)
                    .Replace("-", " ");

                string cardUid = ParseCardUid(data);

                Dispatcher.Invoke(() =>
                {
                    txtReceivedData.Text =
                        $"Direction: {direction}\n\n" +
                        $"Reader IP: {readerIp}\n\n" +
                        $"Card UID: {cardUid}\n\n" +
                        $"Raw Data: {hexData}";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        ex.Message,
                        "RFID Error"
                    );
                });
            }
        }

        //private void OnStatusChanged(string status)
        //{
        //    Dispatcher.Invoke(() =>
        //    {
        //        Title = $"RFID - {status}";
        //    });
        //}

        //private async void OnDataReceived(byte[] data)
        //{
        //    string receivedText =
        //        System.Text.Encoding.UTF8.GetString(data);

        //    Dispatcher.Invoke(() =>
        //    {
        //        MessageBox.Show(
        //            $"RFID Data Received: {receivedText}",
        //            "RFID Data"
        //        );
        //    });

        //    await _rfidServer.SendAsync(
        //        $"ACK: {receivedText} received"
        //    );
        //}

        //private void OnDataReceived(byte[] data)
        //{
        //    try
        //    {
        //        string hexData = BitConverter
        //            .ToString(data)
        //            .Replace("-", " ");

        //        string cardUid = ParseCardUid(data);

        //        Dispatcher.Invoke(() =>
        //        {
        //            txtReceivedData.Text =
        //                $"HEX: {hexData}\n\n" +
        //                $"CARD UID: {cardUid}";
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        Dispatcher.Invoke(() =>
        //        {
        //            txtReceivedData.Text =
        //                $"Error parsing RFID data: {ex.Message}";
        //        });
        //    }
        //}

        private async void OnRfidDataReceived(
    string direction,
    byte[] data)
        {
            try
            {
                string hexData = BitConverter
                    .ToString(data)
                    .Replace("-", " ");

                string cardUid = ParseCardUid(data);

                Dispatcher.Invoke(() =>
                {
                    txtReceivedData.Text =
                        $"Direction: {direction}\n\n" +
                        $"Card UID: {cardUid}\n\n" +
                        $"Raw Data: {hexData}";
                });

                // Later we will save to PostgreSQL here

                // await SaveRfidTransactionAsync(
                //     direction,
                //     cardUid,
                //     hexData
                // );

                // Later capture CCTV evidence automatically
                // CaptureSnapshot();
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        ex.Message,
                        "RFID Error"
                    );
                });
            }
        }       


        // ADD THIS FUNCTION HERE
        private string ParseCardUid(byte[] data)
        {
            if (data == null || data.Length < 8)
                return "Invalid RFID Data";

            // Check command = 2F 01
            if (data[1] != 0x2F || data[2] != 0x01)
                return "Unknown RFID Command";

            // Check RFID error status
            if (data[3] != 0x00 || data[4] != 0x00)
                return "RFID Reader Error";

            // UID length
            int uidLength = data[5];

            // Validate packet length
            if (data.Length < 6 + uidLength)
                return "Incomplete UID Data";

            byte[] uid = new byte[uidLength];

            Array.Copy(
                data,
                6,
                uid,
                0,
                uidLength
            );

            return BitConverter
                .ToString(uid)
                .Replace("-", "");
        }


        



        private async void SendToClient_Click(
    object sender,
    RoutedEventArgs e)
        {
            string message = txtMessage.Text.Trim();

            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("Please enter a message.");
                return;
            }

            try
            {
                //await _rfidServer.SendToClientAsync(message);

                txtMessage.Clear();

                MessageBox.Show("Message sent successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Send Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void TakeSnapshot_Click(object sender, RoutedEventArgs e)
        {
            CaptureSnapshot();
        }



        private async void CaptureSnapshot()
        {
            try
            {
                Mat? snapshot = await Task.Run(() =>
                {
                    return _cctvCamera.TakeSnapshot();
                });

                if (snapshot == null || snapshot.Empty())
                {
                    MessageBox.Show(
                        "Camera connected, but no image frame was captured.",
                        "CCTV Error"
                    );

                    return;
                }

                string folderPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "CCTV_Evidence"
                );

                Directory.CreateDirectory(folderPath);

                string filePath = Path.Combine(
                    folderPath,
                    $"Snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.jpg"
                );

                // SAVE DIRECTLY USING OPENCV
                Cv2.ImWrite(filePath, snapshot);

                // Convert saved image for WPF display
                var bitmap = new BitmapImage();

                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                bitmap.Freeze();

                imgCctvCapture.Source = bitmap;

                txtNoImage.Visibility = Visibility.Collapsed;

                snapshot.Dispose();

                MessageBox.Show(
                    $"Snapshot saved successfully!\n\n{filePath}",
                    "Success"
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Snapshot Error:\n\n{ex}",
                    "CCTV Error"
                );
            }
        }

        //protected override void OnClosed(EventArgs e)
        //{
        //    _rfidServer.Stop();

        //    base.OnClosed(e);
        //}

        protected override void OnClosed(EventArgs e)
        {
            _rfidService.Stop();

            base.OnClosed(e);
        }



    }
}