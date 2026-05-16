using SpendSmart.Views;
using Microsoft.Maui.Storage;

namespace SpendSmart
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute("AddTransactionPage", typeof(AddTransactionPage));
            Routing.RegisterRoute("ManagePocketsPage", typeof(ManagePocketsPage));
            Routing.RegisterRoute("ChatPage", typeof(ChatPage));
            Routing.RegisterRoute(nameof(BackupPage), typeof(BackupPage));

            Navigated += OnShellNavigated;

            RefreshAnalysisRewardState();
        }

        public void RefreshAnalysisRewardState()
        {
            int totalExp = Preferences.Get("TotalExpMirror", 0);

            bool hasClaimedAiUnlock = Preferences.Get("HasClaimedAiUnlock", false);
            bool hasClaimedMonthlyUnlock = Preferences.Get("HasClaimedMonthlyAnalysisUnlock", false);

            bool hasUnclaimedReward =
                (totalExp >= 30 && !hasClaimedAiUnlock) ||
                (totalExp >= 50 && !hasClaimedMonthlyUnlock);

            if (hasUnclaimedReward)
            {
                AnalysisTab.Title = "🎁 วิเคราะห์";
                AnalysisIcon.Glyph = "🎁";
                AnalysisIcon.Color = Color.FromArgb("#E74C3C");
            }
            else
            {
                AnalysisTab.Title = "วิเคราะห์";
                AnalysisIcon.Glyph = "📊";
                AnalysisIcon.Color = Color.FromArgb("#D4AF37");
            }
        }

        private async void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
        {
            RefreshAnalysisRewardState();

            if (e.Source == ShellNavigationSource.ShellItemChanged ||
                e.Source == ShellNavigationSource.ShellSectionChanged)
            {
                if (Navigation.NavigationStack.Count > 1)
                {
                    await Navigation.PopToRootAsync(false);
                }
            }
        }
    }
}