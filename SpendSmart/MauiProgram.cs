using Microsoft.Extensions.Logging;
using SpendSmart.Services;
using SpendSmart.ViewModels;
using SpendSmart.Views;

namespace SpendSmart
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // --- Services ---
            builder.Services.AddSingleton<SpendSmart.Services.DatabaseService>();
            builder.Services.AddSingleton<SpendSmart.Services.AIReceiptScannerService>();
            builder.Services.AddSingleton<SpendSmart.Services.AIFinancialAdvisorService>();
            builder.Services.AddSingleton<SpendSmart.Services.AIVoiceScannerService>();
            // --- ViewModels ---
            builder.Services.AddTransient<SpendSmart.ViewModels.MainViewModel>();
            builder.Services.AddTransient<SpendSmart.ViewModels.AddTransactionViewModel>();
            builder.Services.AddTransient<SpendSmart.ViewModels.ManagePocketsViewModel>();
            builder.Services.AddTransient<SpendSmart.ViewModels.HistoryViewModel>();
            builder.Services.AddTransient<SpendSmart.ViewModels.LoginViewModel>();
            builder.Services.AddTransient<SpendSmart.ViewModels.DashboardViewModel>();
            builder.Services.AddSingleton<SpendSmart.ViewModels.ChatViewModel>();
            builder.Services.AddTransient<BackupViewModel>();

            // --- Views (Pages) ---
            builder.Services.AddTransient<SpendSmart.Views.MainPage>();
            builder.Services.AddTransient<SpendSmart.Views.AddTransactionPage>();
            builder.Services.AddTransient<SpendSmart.Views.ManagePocketsPage>();
            builder.Services.AddTransient<SpendSmart.Views.HistoryPage>();
            builder.Services.AddTransient<SpendSmart.Views.LoginPage>();
            builder.Services.AddTransient<SpendSmart.Views.DashboardPage>();
            builder.Services.AddSingleton<SpendSmart.Views.ChatPage>();
            builder.Services.AddTransient<BackupPage>();

            builder.Services.AddSingleton(Plugin.Maui.Audio.AudioManager.Current);
            return builder.Build();
        }
    }
}