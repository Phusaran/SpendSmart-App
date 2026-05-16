using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SpendSmart.Messages;
using SpendSmart.Models;
using SpendSmart.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace SpendSmart.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly AIFinancialAdvisorService _aiAdvisor;

        [ObservableProperty] private decimal _totalExpense;
        [ObservableProperty] private string _aiAdvice;
        [ObservableProperty] private bool _isAiThinking;
        [ObservableProperty] private int _userLevel;
        [ObservableProperty] private double _expProgress;
        [ObservableProperty] private string _expText;
        [ObservableProperty] private string _rankName;
        [ObservableProperty] private string _rankColor;

        [ObservableProperty] private bool _hasPendingAiReward;
        [ObservableProperty] private bool _hasPendingMonthlyReward;
        [ObservableProperty] private bool _canUseAiChat;
        [ObservableProperty] private bool _canUseMonthlyAnalysis;
        [ObservableProperty] private bool _isAiChatLocked;
        [ObservableProperty] private bool _isMonthlyAnalysisLocked;

        public ObservableCollection<CategorySummary> CategoryData { get; set; } = new();

        public DashboardViewModel(DatabaseService databaseService, AIFinancialAdvisorService aiAdvisor)
        {
            _databaseService = databaseService;
            _aiAdvisor = aiAdvisor;

            WeakReferenceMessenger.Default.Register<TransactionChangedMessage>(this, async (recipient, message) =>
            {
                await LoadDashboardDataAsync();
            });
        }

        public async Task LoadDashboardDataAsync()
        {
            var transactions = await _databaseService.GetTransactionsAsync();

            var currentMonthExpenses = transactions
                .Where(t => t.Type == "Expense" &&
                            t.Date.Month == DateTime.Now.Month &&
                            t.Date.Year == DateTime.Now.Year)
                .ToList();

            TotalExpense = currentMonthExpenses.Sum(x => x.Amount);

            var grouped = currentMonthExpenses
                .GroupBy(t => string.IsNullOrWhiteSpace(t.SubCategory) ? "ทั่วไป" : t.SubCategory)
                .Select(g => new CategorySummary
                {
                    CategoryName = g.Key,
                    Amount = g.Sum(x => x.Amount),
                    Percentage = TotalExpense > 0 ? (double)(g.Sum(x => x.Amount) / TotalExpense) : 0
                })
                .OrderByDescending(x => x.Amount)
                .ToList();

            CategoryData.Clear();

            foreach (var item in grouped)
            {
                CategoryData.Add(item);
            }

            var profile = await _databaseService.GetUserProfileAsync();

            Preferences.Set("TotalExpMirror", profile.TotalExp);

            UserLevel = profile.CurrentLevel;
            ExpProgress = profile.LevelProgress;
            ExpText = profile.ExpText;

            if (profile.TotalExp < 30)
            {
                RankName = "Iron";
                RankColor = "#7F8C8D";
            }
            else if (profile.TotalExp < 50)
            {
                RankName = "Bronze";
                RankColor = "#CD7F32";
            }
            else
            {
                RankName = "Silver";
                RankColor = "#BDC3C7";
            }

            bool hasClaimedAiUnlock = Preferences.Get("HasClaimedAiUnlock", false);
            bool hasClaimedMonthlyUnlock = Preferences.Get("HasClaimedMonthlyAnalysisUnlock", false);

            HasPendingAiReward = profile.TotalExp >= 30 && !hasClaimedAiUnlock;
            HasPendingMonthlyReward = profile.TotalExp >= 50 && !hasClaimedMonthlyUnlock;

            CanUseAiChat = profile.TotalExp >= 30 && hasClaimedAiUnlock;
            CanUseMonthlyAnalysis = profile.TotalExp >= 50 && hasClaimedMonthlyUnlock;

            IsAiChatLocked = !CanUseAiChat;
            IsMonthlyAnalysisLocked = !CanUseMonthlyAnalysis;

            if (Shell.Current is AppShell appShell)
            {
                appShell.RefreshAnalysisRewardState();
            }
        }

        [RelayCommand]
        public async Task ClaimAiRewardAsync()
        {
            Preferences.Set("HasClaimedAiUnlock", true);

            await Shell.Current.DisplayAlert(
                "รับรางวัลสำเร็จ",
                "🎁 ปลดล็อก เริ่มต้นการสนทนา AI แล้ว",
                "ตกลง");

            await LoadDashboardDataAsync();
        }

        [RelayCommand]
        public async Task ClaimMonthlyRewardAsync()
        {
            Preferences.Set("HasClaimedMonthlyAnalysisUnlock", true);

            await Shell.Current.DisplayAlert(
                "รับรางวัลสำเร็จ",
                "🎁 ปลดล็อก วิเคราะห์ข้อมูลรายเดือน แล้ว",
                "ตกลง");

            await LoadDashboardDataAsync();
        }

        [RelayCommand]
        public async Task GoToChatAsync()
        {
            if (!CanUseAiChat)
            {
                await Shell.Current.DisplayAlert(
                    "ยังไม่ปลดล็อก",
                    "ต้องมี 30 EXP ก่อน แล้วกดรับรางวัล 🎁 ที่หน้า วิเคราะห์",
                    "ตกลง");

                return;
            }

            await Shell.Current.GoToAsync("ChatPage");
        }

        [RelayCommand]
        public async Task AskAiForAdviceAsync()
        {
            if (!CanUseMonthlyAnalysis)
            {
                await Shell.Current.DisplayAlert(
                    "ยังไม่ปลดล็อก",
                    "ต้องมี 50 EXP ก่อน แล้วกดรับรางวัล 🎁 ที่หน้า วิเคราะห์",
                    "ตกลง");

                return;
            }

            if (CategoryData.Count == 0)
            {
                await App.Current.MainPage.DisplayAlert("แจ้งเตือน", "ยังไม่มีข้อมูลรายจ่ายให้วิเคราะห์ครับ", "ตกลง");
                return;
            }

            IsAiThinking = true;
            AiAdvice = "AI กำลังวิเคราะห์พฤติกรรมการใช้จ่ายของคุณ...";

            AiAdvice = await _aiAdvisor.GetFinancialAdviceAsync(TotalExpense, CategoryData);

            IsAiThinking = false;
        }

        [RelayCommand]
        public async Task GoToBackupAsync()
        {
            await Shell.Current.GoToAsync(nameof(SpendSmart.Views.BackupPage));
        }
    }
}