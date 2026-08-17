using System;
using System.Windows;
using RfidManagementSystem.Services;

namespace RfidManagementSystem
{
    public partial class MainWindow : System.Windows.Window
    {
      
        private readonly RfidService _rfidService;
        private readonly ConfigurationService _configurationService;
        public MainWindow()
        {
            InitializeComponent();

            _configurationService = new ConfigurationService();

            int entryPort = _configurationService.GetEntryPort();
            int exitPort = _configurationService.GetExitPort();

            txtEntryPort.Text = $"TCP Port: {entryPort}";
            txtExitPort.Text = $"TCP Port: {exitPort}";

            _rfidService = new RfidService();
          

            // RFID server started
            //_rfidService.ServerStarted += OnServerStarted;// testing purpose

            // RFID reader connected
            //_rfidService.ReaderConnected += OnReaderConnected;//testing purpose

            // RFID card data received
            _rfidService.CardDataReceived += OnCardDataReceived;

            // Start RFID service
            StartRfidService();
            
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

        // Testing Function
        //private async void StartRfidService()
        //{
        //    try
        //    {
        //        await _rfidService.StartAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(
        //            ex.Message,
        //            "RFID Service Error",
        //            MessageBoxButton.OK,
        //            MessageBoxImage.Error
        //        );
        //    }
        //}

        //private void OnServerStarted(string message)
        //{
        //    Dispatcher.Invoke(() =>
        //    {
        //        MessageBox.Show(
        //            message,
        //            "RFID TCP Server",
        //            MessageBoxButton.OK,
        //            MessageBoxImage.Information
        //        );
        //    });
        //}
        private void OnReaderConnected(string readerType,string status)
        {
            Dispatcher.Invoke(() =>
            {
                string cleanStatus = status
                    .Replace("RFID_READER_CONNECTED|", "")
                    .Replace("|", "\n");

                MessageBox.Show(
                    $"{readerType} RFID Reader Connected!\n\n" +
                    cleanStatus,
                    "RFID Reader Detected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            });
        }
        private void OnCardDataReceived(string direction,string readerIp,int serverPort, byte[] data)
        {
            Dispatcher.Invoke(() =>
            {
                string hexData = BitConverter
                    .ToString(data)
                    .Replace("-", " ");

                string cardUid = ParseCardUid(data);

                txtReceivedData.Text =
                    $"Direction: {direction}\n\n" +
                    $"Reader IP: {readerIp}\n" +
                    $"Server Port: {serverPort}\n\n" +
                    $"Card UID: {cardUid}\n\n" +
                    $"HEX: {hexData}";
            });
        }       
        private string ParseCardUid(byte[] data)
        {
            // Minimum expected:
            // Frame + Command + Error + UID Length + UID + CRC
            if (data == null || data.Length < 8)
                return "Invalid RFID Data";

            // Check command = 2F01 (Inventory)
            if (data[1] != 0x2F || data[2] != 0x01)
                return "Unknown RFID Command";

            // Check error code
            if (data[3] != 0x00 || data[4] != 0x00)
                return "RFID Reader Error";

            // UID length
            int uidLength = data[5];

            // Make sure complete UID is available
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
        protected override void OnClosed(EventArgs e)
        {
            _rfidService.Stop();

            base.OnClosed(e);
        }



    }
}