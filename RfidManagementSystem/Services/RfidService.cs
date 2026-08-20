using RfidManagementSystem.Hardware.Rfid;
using RfidManagementSystem.Models;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RfidManagementSystem.Services;

public class RfidService
{
    private readonly ConfigurationService _configurationService;
    private readonly RfidReaderConfigurationService _readerConfigurationService;
    private readonly SystemLogService _systemLogService;
    private readonly RfidTransactionService _rfidTransactionService;
    private readonly EmployeeRegistrationService _employeeRegistrationService;

    // ============================
    // DYNAMIC RFID READERS
    // ============================

    // Key = reader_id from master_rfid_readers
    // Value = TCP server for that reader
    private readonly Dictionary<long, RfidTcpServer> _rfidServers = new();

    // Stores IDs of currently connected readers
    private readonly HashSet<long> _connectedReaderIds = new();

    // Stores IDs of currently disconnected connected readers
    private readonly HashSet<long> _disconnectedReaderIds = new();

    // Active readers loaded from database
    private List<MasterRfidReader> _activeReaders = new();

    // Prevents ALL_READERS_CONNECTED
    // log from being inserted multiple times
    private bool _allReadersStatusLogged = false;

   

    // ============================
    // EVENTS
    // ============================
    public event Action<MasterRfidReader, string>? ReaderConnected;
    //public event Action<MasterRfidReader, string>? ServerStarted;

    public event Action<MasterRfidReader, string,int,byte[]>? CardDataReceived;
    public event Action<MasterRfidReader, string, int, RfidScanResult>? ScanProcessed;

    public event Action? ReaderConnectionStatusChanged;

    // ==========================================
    // EMPLOYEE RFID REGISTRATION STATE
    // ==========================================

    private bool _employeeRegistrationActive = false;

    private RfidScanResult? _employeeRegistrationResult;

    private readonly object _employeeRegistrationLock = new();

    public RfidService(EmployeeRegistrationService employeeRegistrationService)
    {
        _configurationService = new ConfigurationService();

        _systemLogService = new SystemLogService(_configurationService);
     
        _rfidTransactionService = new RfidTransactionService(_configurationService,_systemLogService);

        _readerConfigurationService =new RfidReaderConfigurationService(_configurationService);

        _employeeRegistrationService = employeeRegistrationService;
    }

    public async Task StartAsync()
    {
        try
        {
            // Load all active RFID readers dynamically from database
            _activeReaders = await _readerConfigurationService.GetActiveReadersAsync();

            if (_activeReaders.Count == 0)
            {
                await _systemLogService.LogAsync("RFID_SERVICE","WARNING","NO_ACTIVE_READERS", "No active RFID readers are configured.", null,null);
                return;
            }

            // Start TCP server for every active reader
            foreach (var reader in _activeReaders)
            {
                await StartReaderAsync(reader);
            }

            // Monitor physical RFID reader connections
            // and retry/check based on appsettings configuration
            _ = MonitorReaderConnectionsAsync();
        }
        catch (Exception ex)
        {
            await _systemLogService.LogAsync(
                "RFID_SERVICE",
                "ERROR",
                "RFID_SERVICE_START_ERROR",
                $"Error starting RFID service: {ex.Message}",
                null,
                null
            );
        }
    }

    private async Task StartReaderAsync(MasterRfidReader reader)
    {
        if (_rfidServers.ContainsKey(reader.ReaderId))
        {
            return;
        }

        var server = new RfidTcpServer();

        _rfidServers.Add(
            reader.ReaderId,
            server
        );

        server.StatusChanged += status =>
        {
            HandleReaderStatus(
                reader,
                status
            );
        };

        server.DataReceived +=
            (readerIp, serverPort, data) =>
            {
                HandleCardData(
                    reader,
                    readerIp,
                    serverPort,
                    data
                );
            };

        await _systemLogService.LogAsync(
            "RFID_SERVICE",
            "INFO",
            "TCP_SERVER_STARTING",
            $"Starting RFID TCP Server for '{reader.ReaderName}' " +
            $"({reader.ReaderPurpose}). " +
            $"Expected IP: {reader.IpAddress}, " +
            $"Port: {reader.Port}.",
            reader.IpAddress,
            reader.Port
        );

        _ = server.StartAsync(reader.Port);
    }

    private async Task MonitorReaderConnectionsAsync()
    {
        int retryIntervalSeconds =_configurationService.GetReaderConnectionRetryIntervalSeconds();

        int retryDurationMinutes = _configurationService.GetReaderConnectionRetryDurationMinutes();

        TimeSpan maximumRetryDuration = TimeSpan.FromMinutes(retryDurationMinutes);

        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < maximumRetryDuration)
        {
            List<MasterRfidReader> disconnectedReaders;

            lock (_connectedReaderIds)
            {
                disconnectedReaders = _activeReaders.Where(reader => !_connectedReaderIds.Contains(reader.ReaderId)).ToList();
            }

            if (disconnectedReaders.Count == 0)
            {
                await LogAllReadersConnectedAsync();
                return;
            }

            foreach (var reader in disconnectedReaders)
            {
                await HandleReaderNotConnectedAsync(reader);
            }

            await Task.Delay(
                TimeSpan.FromSeconds(retryIntervalSeconds)
            );
        }

        await LogFinalDisconnectedReadersAsync();
    }

    private async Task HandleReaderNotConnectedAsync(MasterRfidReader reader)
    {
        // Prevent the same disconnected reader
        // from being logged repeatedly on every retry
        bool newlyDisconnected;

        lock (_disconnectedReaderIds)
        {
            newlyDisconnected =
                _disconnectedReaderIds.Add(reader.ReaderId);
        }

        if (!newlyDisconnected)
        {
            return;
        }

        await _systemLogService.LogAsync(
            "RFID_SERVICE",
            "WARNING",
            "RFID_READER_NOT_CONNECTED",
            $"RFID Reader '{reader.ReaderName}' is not connected. " +
            $"IP: {reader.IpAddress}, Port: {reader.Port}, " +
            $"Purpose: {reader.ReaderPurpose}.",
            reader.IpAddress,
            reader.Port
        );
    }

    private async Task LogAllReadersConnectedAsync()
    {
        // Prevent duplicate ALL_READERS_CONNECTED logs
        if (_allReadersStatusLogged)
        {
            return;
        }

        _allReadersStatusLogged = true;

        string readerDetails = string.Join(
            ", ",
            _activeReaders.Select(reader =>
                $"{reader.ReaderName} " +
                $"(IP: {reader.IpAddress}, Port: {reader.Port}, " +
                $"Purpose: {reader.ReaderPurpose})"
            )
        );

        await _systemLogService.LogAsync(
            "RFID_SERVICE",
            "INFO",
            "ALL_RFID_READERS_CONNECTED",
            $"All {_activeReaders.Count} active RFID readers are connected. " +
            $"Readers: {readerDetails}",
            null,
            null
        );
    }

    private async Task LogFinalDisconnectedReadersAsync()
    {
        List<MasterRfidReader> disconnectedReaders;

        lock (_connectedReaderIds)
        {
            disconnectedReaders = _activeReaders
                .Where(reader =>
                    !_connectedReaderIds.Contains(reader.ReaderId)
                )
                .ToList();
        }

        // Safety check
        if (disconnectedReaders.Count == 0)
        {
            await LogAllReadersConnectedAsync();
            return;
        }

        foreach (var reader in disconnectedReaders)
        {
            await _systemLogService.LogAsync(
                "RFID_SERVICE",
                "ERROR",
                "RFID_READER_CONNECTION_FAILED",
                $"RFID Reader '{reader.ReaderName}' could not be connected " +
                $"within the configured retry duration. " +
                $"IP: {reader.IpAddress}, " +
                $"Port: {reader.Port}, " +
                $"Purpose: {reader.ReaderPurpose}.",
                reader.IpAddress,
                reader.Port
            );
        }
    }

    private async void HandleReaderStatus(MasterRfidReader reader,string status)
    {
        try
        {
            Console.WriteLine(
                $"[{reader.ReaderName}] RFID Status: {status}"
            );

            if (status.StartsWith("RFID_READER_CONNECTED"))
            {
                lock (_connectedReaderIds)
                {
                    _connectedReaderIds.Add(reader.ReaderId);
                }

                lock (_disconnectedReaderIds)
                {
                    _disconnectedReaderIds.Remove(reader.ReaderId);
                }

                ReaderConnected?.Invoke(
                    reader,
                    status
                );

                ReaderConnectionStatusChanged?.Invoke();

                await _systemLogService.LogAsync(
                    "RFID_SERVICE",
                    "INFO",
                    "RFID_READER_CONNECTED",
                    $"RFID Reader '{reader.ReaderName}' connected successfully. " +
                    $"IP: {reader.IpAddress}, " +
                    $"Port: {reader.Port}, " +
                    $"Purpose: {reader.ReaderPurpose}.",
                    reader.IpAddress,
                    reader.Port
                );
            }
            // ==========================================
            // READER DISCONNECTED
            // ==========================================

            else if (status.StartsWith("RFID_READER_DISCONNECTED"))
            {
                lock (_connectedReaderIds)
                {
                    _connectedReaderIds.Remove(reader.ReaderId);
                }

                lock (_disconnectedReaderIds)
                {
                    _disconnectedReaderIds.Add(reader.ReaderId);
                }

                ReaderConnectionStatusChanged?.Invoke();

                await _systemLogService.LogAsync(
                    "RFID_SERVICE",
                    "WARNING",
                    "RFID_READER_DISCONNECTED",
                    $"RFID Reader '{reader.ReaderName}' disconnected. " +
                    $"IP: {reader.IpAddress}, " +
                    $"Port: {reader.Port}, " +
                    $"Purpose: {reader.ReaderPurpose}.",
                    reader.IpAddress,
                    reader.Port
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

    private async void HandleCardData(MasterRfidReader reader, string readerIp,int readerPort, byte[] data)
    {
        try
        {
            // ==========================================
            // CONVERT COMPLETE RFID PACKET TO HEX
            // ==========================================

            string rawHexData =BitConverter.ToString(data).Replace("-", "");

            // ==========================================
            // EXTRACT ACTUAL CARD UID
            // Tested logic from your previous code
            // ==========================================

            string cardUid;

            if (rawHexData.Length >= 16)
            {
                cardUid =rawHexData.Substring(12, 8);
            }
            else
            {
                cardUid = rawHexData;
            }

            //Console.WriteLine(
            //    $"RFID Card Detected. " +
            //    $"Reader: {reader.ReaderName}, " +
            //    $"Purpose: {reader.ReaderPurpose}, " +
            //    $"Card UID: {cardUid}"
            //);

            // ==========================================
            // LOG CARD DETECTION
            // ==========================================

            await _systemLogService.LogAsync(
                "RFID_SERVICE",
                "INFO",
                "RFID_CARD_DETECTED",
                $"RFID card detected from reader " +
                $"'{reader.ReaderName}'. " +
                $"Card UID: {cardUid}. " +
                $"Purpose: {reader.ReaderPurpose}.",
                readerIp,
                readerPort
            );

            // ==========================================
            // EMPLOYEE REGISTRATION READER
            //
            // Do NOT process through RfidTransactionService.
            //
            // RFID Service only reads the card UID.
            // Dashboard Service will:
            // - validate the card
            // - check if already assigned
            // - show error if required
            // - save the employee and card UID
            // ==========================================


            if (reader.ReaderPurpose == RfidReaderPurpose.EMPLOYEE_REGISTRATION)
            {
                _employeeRegistrationService.SubmitCardUid(cardUid);

                var registrationResult = new RfidScanResult
                {
                    ResultType = RfidScanResultType.EMPLOYEE_REGISTRATION,
                    Message = "RFID card scanned successfully.",
                    CardUid = cardUid,
                    ShouldAlertDashboard = true,
                    ShouldAnnounce = false
                };                

                ScanProcessed?.Invoke(
                    reader,
                    readerIp,
                    readerPort,
                    registrationResult
                );

                await HandleScanResultAsync(
                    reader,
                    readerIp,
                    readerPort,
                    registrationResult
                );

                return;
            }

     
           

            // ==========================================
            // NORMAL RFID READERS
            //
            // ENTRY
            // EXIT
            // ENTRY_EXIT
            //
            // Send these to transaction service
            // ==========================================

            RfidScanResult result = await _rfidTransactionService.ProcessCardAsync(reader,cardUid,readerIp, readerPort,rawHexData);

            // ==========================================
            // SEND RAW DATA EVENT IF NEEDED
            // ==========================================

            CardDataReceived?.Invoke(reader, readerIp,readerPort,data);

            // ==========================================
            // SEND FINAL RESULT
            //
            // Dashboard can later use this for:
            // - alerts
            // - announcements
            // - unauthorized card
            // - cooldown scan
            // - access denied
            // ==========================================

            ScanProcessed?.Invoke(reader,readerIp,readerPort,result);

            // ==========================================
            // HANDLE DASHBOARD / ANNOUNCEMENT
            // ==========================================

            await HandleScanResultAsync(reader,readerIp,readerPort,result);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"RFID card handling error: {ex.Message}"
            );

            // ==========================================
            // CREATE ERROR RESULT
            // ==========================================         
            await _systemLogService.LogAsync(
                "RFID_SERVICE",
                "ERROR",
                "RFID_CARD_PROCESSING_ERROR",
                $"Error processing RFID card: {ex.Message}",
                readerIp,
                readerPort
            );

            var errorResult = new RfidScanResult
            {
                ResultType =
                 RfidScanResultType.ERROR,

                Message =
                 $"Error processing RFID card: {ex.Message}",

                CardUid = string.Empty,

                ShouldAlertDashboard = true,

                ShouldAnnounce = false
            };


            // Notify listeners about the error
            ScanProcessed?.Invoke(
                reader,
                readerIp,
                readerPort,
                errorResult
            );
        }
    }

    private async Task HandleScanResultAsync(MasterRfidReader reader, string readerIp,int readerPort, RfidScanResult result)
    {
        try
        {
            // ==========================================
            // LOG RESULT IN CONSOLE FOR NOW
            // ==========================================

            Console.WriteLine($"RFID Scan Result: {result.ResultType} | " +
                $"Card UID: {result.CardUid} | " +
                $"Message: {result.Message}"
            );

            // ==========================================
            // DASHBOARD ALERT
            // ==========================================

            if (result.ShouldAlertDashboard)
            {
                Console.WriteLine(
                    $"DASHBOARD ALERT REQUIRED: {result.Message}"
                );

                // Dashboard Service integration
                // will be added here later
            }

            // ==========================================
            // AUDIO ANNOUNCEMENT
            // ==========================================

            if (result.ShouldAnnounce)
            {
                Console.WriteLine(
                    $"ANNOUNCEMENT REQUIRED: {result.Message}"
                );

                // Audio announcement integration
                // will be added here later
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Error handling scan result: {ex.Message}"
            );

            await _systemLogService.LogAsync(
                "RFID_SERVICE",
                "ERROR",
                "RFID_SCAN_RESULT_HANDLING_ERROR",
                $"Error handling scan result for card " +
                $"{result.CardUid}: {ex.Message}",
                readerIp,
                readerPort
            );
        }
    }

    // ==========================================
    // START EMPLOYEE REGISTRATION SCAN
    // ==========================================

    public void StartEmployeeRegistrationScan()
    {
        lock (_employeeRegistrationLock)
        {
            _employeeRegistrationActive = true;

            // Clear previous result
            _employeeRegistrationResult = null;
        }
    }


    // ==========================================
    // GET EMPLOYEE REGISTRATION RESULT
    // ==========================================

    public RfidScanResult? GetEmployeeRegistrationResult()
    {
        lock (_employeeRegistrationLock)
        {
            return _employeeRegistrationResult;
        }
    }


    // ==========================================
    // CANCEL EMPLOYEE REGISTRATION SCAN
    // ==========================================

    public void CancelEmployeeRegistrationScan()
    {
        lock (_employeeRegistrationLock)
        {
            _employeeRegistrationActive = false;

            _employeeRegistrationResult = null;
        }
    }

    public List<MasterRfidReader> GetConnectedReaders()
    {
        lock (_connectedReaderIds)
        {
            return _activeReaders
                .Where(reader =>
                    _connectedReaderIds.Contains(reader.ReaderId))
                .ToList();
        }
    }

    public List<MasterRfidReader> GetDisconnectedReaders()
    {
        lock (_connectedReaderIds)
        {
            return _activeReaders
                .Where(reader =>
                    !_connectedReaderIds.Contains(reader.ReaderId))
                .ToList();
        }
    }

    public void Stop()
    {
        foreach (var server in _rfidServers.Values)
        {
            server.Stop();
        }

        _rfidServers.Clear();

        lock (_connectedReaderIds)
        {
            _connectedReaderIds.Clear();
        }

        lock (_disconnectedReaderIds)
        {
            _disconnectedReaderIds.Clear();
        }

        _allReadersStatusLogged = false;
    }
}