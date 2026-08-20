using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using RfidManagementSystem.Services;
using System;
using System.Windows;

namespace RfidManagementSystem
{
    public partial class App : Application
    {
        private WebApplication? _webApplication;

        protected override async void OnStartup(
            StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var builder = WebApplication.CreateBuilder();

                // ==========================================
                // ADD CONTROLLERS
                // ==========================================

                builder.Services.AddControllers();

                // ==========================================
                // REGISTER SERVICES
                // ==========================================

                builder.Services.AddSingleton<
                    EmployeeRegistrationService>();

                builder.Services.AddSingleton<RfidService>();

                // ==========================================
                // BUILD WEB APPLICATION
                // ==========================================

                _webApplication = builder.Build();

                // ==========================================
                // MAP API CONTROLLERS
                // THIS IS IMPORTANT
                // ==========================================

                _webApplication.MapControllers();

                // ==========================================
                // START WEB API
                // ==========================================

                await _webApplication.StartAsync();

                // ==========================================
                // GET SAME RFID SERVICE INSTANCE
                // ==========================================

                var rfidService =
                    _webApplication.Services
                        .GetRequiredService<RfidService>();

                // ==========================================
                // START ALL RFID READERS
                // ==========================================

                await rfidService.StartAsync();

                // ==========================================
                // START WPF WINDOW
                // ==========================================

                var mainWindow =
                    new MainWindow(rfidService);

                MainWindow = mainWindow;

                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "RFID Service Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                Shutdown();
            }
        }

        protected override async void OnExit(
            ExitEventArgs e)
        {
            try
            {
                if (_webApplication != null)
                {
                    await _webApplication.StopAsync();

                    await _webApplication.DisposeAsync();
                }
            }
            catch
            {
                // Ignore shutdown errors
            }

            base.OnExit(e);
        }
    }
}