using Microsoft.Extensions.Configuration;

namespace RfidManagementSystem.Services;

public class ConfigurationService
{
    private readonly IConfiguration _configuration;

    public ConfigurationService()
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(
                "appsettings.json",
                optional: false,
                reloadOnChange: true
            )
            .Build();
    }

    public string GetConnectionString()
    {
        return _configuration.GetConnectionString(
            "PostgreSqlConnection"
        )
        ?? throw new Exception(
            "PostgreSQL connection string is missing."
        );
    }

    public int GetEntryPort()
    {
        return int.Parse(
            _configuration["RfidSettings:EntryPort"]
            ?? throw new Exception("EntryPort is missing.")
        );
    }

    public int GetExitPort()
    {
        return int.Parse(
            _configuration["RfidSettings:ExitPort"]
            ?? throw new Exception("ExitPort is missing.")
        );
    }
}