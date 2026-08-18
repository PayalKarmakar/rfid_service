using Npgsql;

namespace RfidManagementSystem.Services;

public class RfidTransactionService
{
    private readonly ConfigurationService _configurationService;
    private readonly SystemLogService _systemLogService;

    public RfidTransactionService(
        ConfigurationService configurationService,
        SystemLogService systemLogService)
    {
        _configurationService = configurationService;
        _systemLogService = systemLogService;
    }

    public async Task ProcessCardAsync(
        string direction,
        string cardUid,
        string readerIp,
        int readerPort,
        string rawHexData)
    {
        try
        {
            string connectionString =
                _configurationService.GetConnectionString();

            await using var connection =
                new NpgsqlConnection(connectionString);

            await connection.OpenAsync();

            // Check whether this RFID card is mapped
            // to an active employee
            const string employeeSql = @"
                SELECT
                    emp_id,
                    employee_name,
                    employee_code,
                    chamber_id
                FROM public.master_employees
                WHERE card_uid = @cardUid
                AND is_active = TRUE
                LIMIT 1;
            ";

            await using var command =
                new NpgsqlCommand(employeeSql, connection);

            command.Parameters.AddWithValue(
                "cardUid",
                cardUid
            );

            await using var reader =
                await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                await reader.CloseAsync();

                await _systemLogService.LogAsync(
                    "RFID_SERVICE",
                    "WARNING",
                    "ACCESS_DENIED",
                    $"Access denied. RFID Card UID {cardUid} is not mapped to an active employee.",
                    readerIp,
                    readerPort
                );

                return;
            }

            long employeeId = reader.GetInt64(0);

            string employeeName =
                reader.GetString(1);

            string employeeCode =
                reader.GetString(2);

            int? chamberId =
    reader.IsDBNull(3)
        ? null
        : reader.GetInt32(3);

            if (!chamberId.HasValue)
            {
                await reader.CloseAsync();

                await _systemLogService.LogAsync(
                    "RFID_SERVICE",
                    "WARNING",
                    "CHAMBER_NOT_ASSIGNED",
                    $"Access denied. Employee {employeeName} ({employeeCode}) does not have a chamber assigned.",
                    readerIp,
                    readerPort
                );

                return;
            }

            await reader.CloseAsync();

            // Employee is valid
            if (direction == "ENTRY")
            {
                await ProcessEntryAsync(
                    connection,
                    employeeId,
                    chamberId.Value,
                    employeeName,
                    employeeCode,
                    cardUid,
                    readerIp,
                    readerPort,
                    rawHexData
                );
            }
            else if (direction == "EXIT")
            {
                await ProcessExitAsync(
                    connection,
                    employeeId,
                    employeeName,
                    cardUid,
                    readerIp,
                    readerPort,
                    rawHexData
                );
            }
        }
        catch (Exception ex)
        {
            await _systemLogService.LogAsync(
                "RFID_SERVICE",
                "ERROR",
                "RFID_TRANSACTION_ERROR",
                $"Error processing card {cardUid}: {ex.Message}",
                readerIp,
                readerPort
            );
        }
    }

    private async Task ProcessEntryAsync(
    NpgsqlConnection connection,
    long employeeId,
    int chamberId,
    string employeeName,
    string employeeCode,
    string cardUid,
    string readerIp,
    int readerPort,
    string rawHexData)
    { 
        const string checkOpenTransactionSql = @"
            SELECT id
            FROM public.rfid_transactions
            WHERE employee_id = @employeeId
            AND exit_time IS NULL
            AND status <> 'INCOMPLETE'
            ORDER BY entry_time DESC
            LIMIT 1;
        ";

        await using var checkCommand =
            new NpgsqlCommand(
                checkOpenTransactionSql,
                connection
            );

        checkCommand.Parameters.AddWithValue(
            "employeeId",
            employeeId
        );

        object? openTransaction =
            await checkCommand.ExecuteScalarAsync();

        if (openTransaction != null)
        {
            await _systemLogService.LogAsync(
                "RFID_SERVICE",
                "WARNING",
                "ALREADY_INSIDE",
                $"ENTRY denied. Employee {employeeName} already has an open transaction.",
                readerIp,
                readerPort
            );

            return;
        }

        const string insertSql = @"
            INSERT INTO public.rfid_transactions
            (
                employee_id,
                chamber_id,
                employee_name,
                card_uid,
                entry_time,
                entry_reader_ip,
                entry_reader_port,
                entry_raw_hex_data,
                status
            )
            VALUES
            (
                @employeeId,
                @chamberId,
                @employeeName,
                @cardUid,
                NOW(),
                @readerIp,
                @readerPort,
                @rawHexData,
                'OPEN'
            );
        ";

        await using var insertCommand =
            new NpgsqlCommand(
                insertSql,
                connection
            );

        insertCommand.Parameters.AddWithValue(
            "employeeId",
            employeeId
        );

        insertCommand.Parameters.AddWithValue(
            "employeeName",
            employeeName
        );

        insertCommand.Parameters.AddWithValue(
            "chamberId",
            chamberId
        );

        insertCommand.Parameters.AddWithValue(
            "cardUid",
            cardUid
        );

        insertCommand.Parameters.AddWithValue(
            "readerIp",
            readerIp
        );

        insertCommand.Parameters.AddWithValue(
            "readerPort",
            readerPort
        );

        insertCommand.Parameters.AddWithValue(
            "rawHexData",
            rawHexData
        );

        await insertCommand.ExecuteNonQueryAsync();

        await _systemLogService.LogAsync(
            "RFID_SERVICE",
            "INFO",
            "ENTRY_RECORDED",
            $"Entry recorded for {employeeName} ({employeeCode}).",
            readerIp,
            readerPort
        );
    }

    private async Task ProcessExitAsync(
        NpgsqlConnection connection,
        long employeeId,
        string employeeName,
        string cardUid,
        string readerIp,
        int readerPort,
        string rawHexData)
    {
        const string updateSql = @"
            UPDATE public.rfid_transactions
            SET
                exit_time = NOW(),
                exit_reader_ip = @readerIp,
                exit_reader_port = @readerPort,
                exit_raw_hex_data = @rawHexData,
                status = 'COMPLETED',
                updated_at = NOW()
            WHERE id =
            (
                SELECT id
                FROM public.rfid_transactions
                WHERE employee_id = @employeeId
                AND status = 'OPEN'
                ORDER BY entry_time DESC
                LIMIT 1
            );
        ";

        await using var updateCommand =
            new NpgsqlCommand(
                updateSql,
                connection
            );

        updateCommand.Parameters.AddWithValue(
            "employeeId",
            employeeId
        );

        updateCommand.Parameters.AddWithValue(
            "readerIp",
            readerIp
        );

        updateCommand.Parameters.AddWithValue(
            "readerPort",
            readerPort
        );

        updateCommand.Parameters.AddWithValue(
            "rawHexData",
            rawHexData
        );

        int rowsAffected =
            await updateCommand.ExecuteNonQueryAsync();

        if (rowsAffected == 0)
        {
            await _systemLogService.LogAsync(
                "RFID_SERVICE",
                "WARNING",
                "EXIT_WITHOUT_ENTRY",
                $"Exit detected for {employeeName}, but no open ENTRY transaction was found.",
                readerIp,
                readerPort
            );

            return;
        }

        await _systemLogService.LogAsync(
            "RFID_SERVICE",
            "INFO",
            "EXIT_RECORDED",
            $"Exit recorded for {employeeName}.",
            readerIp,
            readerPort
        );
    }
}