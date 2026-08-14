using System;
using System.Threading.Tasks;
using RfidManagementSystem.Hardware.Rfid;

namespace RfidManagementSystem.Services;

public class RfidService
{
    private readonly RfidTcpServer _entryRfidServer;
    private readonly RfidTcpServer _exitRfidServer;

    public event Action<string, string>? ReaderConnected;
    public event Action<string>? ServerStarted;

    public RfidService()
    {
        _entryRfidServer = new RfidTcpServer();
        _exitRfidServer = new RfidTcpServer();

        // ENTRY reader status
        _entryRfidServer.StatusChanged +=
            status => HandleReaderStatus("ENTRY", status);

        // EXIT reader status
        _exitRfidServer.StatusChanged +=
            status => HandleReaderStatus("EXIT", status);
    }

    public async Task StartAsync()
    {
        ServerStarted?.Invoke(
        "Starting ENTRY RFID TCP Server on Port 5000..."
    );

        ServerStarted?.Invoke(
            "Starting EXIT RFID TCP Server on Port 5001..."
        );
        await Task.WhenAll(
            _entryRfidServer.StartAsync(5000),
            _exitRfidServer.StartAsync(5001)
        );
    }

    private void HandleReaderStatus(
        string readerType,
        string status)
    {
        if (status.StartsWith("RFID_READER_CONNECTED"))
        {
            ReaderConnected?.Invoke(
                readerType,
                status
            );
        }
    }

    public void Stop()
    {
        _entryRfidServer.Stop();
        _exitRfidServer.Stop();
    }
}