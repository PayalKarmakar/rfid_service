using System;
using System.Threading.Tasks;
using RfidManagementSystem.Hardware.Rfid;

namespace RfidManagementSystem.Services;

public class RfidService
{
    private readonly RfidTcpServer _entryRfidServer;
    private readonly RfidTcpServer _exitRfidServer;
    private readonly ConfigurationService _configurationService;
    private readonly SystemLogService _systemLogService;
    private readonly RfidTransactionService _rfidTransactionService;

    public event Action<string, string>? ReaderConnected;
    public event Action<string>? ServerStarted;

    public event Action<string, string, int, byte[]>? CardDataReceived;

    public RfidService()
    {
        _entryRfidServer = new RfidTcpServer();
        _exitRfidServer = new RfidTcpServer();

        _entryRfidServer.StatusChanged +=
       status => HandleReaderStatus("ENTRY", status);

        // EXIT reader status
        _exitRfidServer.StatusChanged +=
            status => HandleReaderStatus("EXIT", status);

        // ENTRY reader data
        _entryRfidServer.DataReceived += (readerIp, serverPort, data) =>
                HandleCardData("ENTRY", readerIp, serverPort, data);

        // EXIT reader data
        _exitRfidServer.DataReceived += (readerIp, serverPort, data) =>
                HandleCardData("EXIT", readerIp, serverPort, data);

        _configurationService = new ConfigurationService();

        _systemLogService = new SystemLogService( _configurationService);

        _rfidTransactionService = new RfidTransactionService(_configurationService,
    _systemLogService);
    }
    public async Task StartAsync()
    {
        int entryPort =
            _configurationService.GetEntryPort();

        int exitPort =
            _configurationService.GetExitPort();


        // ENTRY SERVER START
        ServerStarted?.Invoke(
            $"Starting ENTRY RFID TCP Server on Port {entryPort}..."
        );

        await _systemLogService.LogAsync(
            "RFID_SERVICE",
            "INFO",
            "TCP_SERVER_STARTING",
            $"Starting ENTRY RFID TCP Server on Port {entryPort}...",
            null,
            entryPort
        );


        // EXIT SERVER START
        ServerStarted?.Invoke(
            $"Starting EXIT RFID TCP Server on Port {exitPort}..."
        );

        await _systemLogService.LogAsync(
            "RFID_SERVICE",
            "INFO",
            "TCP_SERVER_STARTING",
            $"Starting EXIT RFID TCP Server on Port {exitPort}...",
            null,
            exitPort
        );


        // START BOTH TCP SERVERS
        await Task.WhenAll(
            _entryRfidServer.StartAsync(entryPort),
            _exitRfidServer.StartAsync(exitPort)
        );
    }
    private async void HandleReaderStatus(string readerType,string status)
    {
        try
        {
            // Temporary: show/log every status
            Console.WriteLine(
                $"[{readerType}] RFID Status: {status}"
            );

            if (status.StartsWith("RFID_READER_CONNECTED"))
            {
                ReaderConnected?.Invoke(
                    readerType,
                    status
                );

                string? sourceIp = null;
                int? sourcePort = null;

                string[] parts = status.Split('|');

                foreach (string part in parts)
                {
                    if (part.StartsWith("IP="))
                    {
                        sourceIp = part.Replace("IP=", "");
                    }

                    if (part.StartsWith("SERVER_PORT="))
                    {
                        if (int.TryParse(
                            part.Replace("SERVER_PORT=", ""),
                            out int port))
                        {
                            sourcePort = port;
                        }
                    }
                }

                await _systemLogService.LogAsync(
                    "RFID_SERVICE",
                    "INFO",
                    "RFID_READER_CONNECTED",
                    $"{readerType} RFID Reader connected.",
                    sourceIp,
                    sourcePort
                );
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"RFID status handling error: {ex.Message}"
            );
        }
    }


    //private async void HandleReaderStatus(
    //    string readerType,
    //    string status)
    //{
    //    try
    //    {
    //        if (status.StartsWith("RFID_READER_CONNECTED"))
    //        {
    //            ReaderConnected?.Invoke(
    //                readerType,
    //                status
    //            );

    //            string? sourceIp = null;
    //            int? sourcePort = null;

    //            string[] parts = status.Split('|');

    //            foreach (string part in parts)
    //            {
    //                if (part.StartsWith("IP="))
    //                {
    //                    sourceIp =
    //                        part.Replace("IP=", "");
    //                }

    //                if (part.StartsWith("SERVER_PORT="))
    //                {
    //                    if (int.TryParse(
    //                        part.Replace("SERVER_PORT=", ""),
    //                        out int port))
    //                    {
    //                        sourcePort = port;
    //                    }
    //                }
    //            }

    //            await _systemLogService.LogAsync(
    //                "RFID_SERVICE",
    //                "INFO",
    //                "RFID_READER_CONNECTED",
    //                $"{readerType} RFID Reader connected.",
    //                sourceIp,
    //                sourcePort
    //            );
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine(
    //            $"RFID status handling error: {ex.Message}"
    //        );
    //    }
    //}


    private async void HandleCardData(
    string direction,
    string readerIp,
    int serverPort,
    byte[] data)
    {
        try
        {
            // Send data to UI
            CardDataReceived?.Invoke(
                direction,
                readerIp,
                serverPort,
                data
            );

            string hexData =
    BitConverter.ToString(data)
        .Replace("-", "");

            // Extract Card UID from RFID packet
            string cardUid;

            if (hexData.Length >= 16)
            {
                cardUid = hexData.Substring(12, 8);
            }
            else
            {
                cardUid = hexData;
            }

            // Save raw card detection log
            await _systemLogService.LogAsync(
                "RFID_SERVICE",
                "INFO",
                "RFID_CARD_DETECTED",
                $"{direction} RFID card detected. Card UID: {cardUid}",
                readerIp,
                serverPort
            );

            // Process employee validation and transaction
            await _rfidTransactionService.ProcessCardAsync(
                direction,
                cardUid,
                readerIp,
                serverPort,
                hexData
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"RFID card handling error: {ex.Message}"
            );
        }
    }


    public void Stop()
    {
        _entryRfidServer.Stop();

        _exitRfidServer.Stop();
    }
}