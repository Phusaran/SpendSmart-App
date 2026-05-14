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

            UserLevel = profile.CurrentLevel;
            ExpProgress = profile.LevelProgress;
            ExpText = profile.ExpText;

            if (UserLevel < 5)
            {
                RankName = "Iron";
                RankColor = "#7F8C8D";
            }
            else if (UserLevel < 10)
            {
                RankName = "Bronze";
                RankColor = "#CD7F32";
            }
            else if (UserLevel < 20)
            {
                RankName = "Silver";
                RankColor = "#BDC3C7";
            }
            else if (UserLevel < 30)
            {
                RankName = "Gold";
                RankColor = "#F1C40F";
            }
            else if (UserLevel < 40)
            {
                RankName = "Platinum";
                RankColor = "#1ABC9C";
            }
            else if (UserLevel < 50)
            {
                RankName = "Diamond";
                RankColor = "#9B59B6";
            }
            else if (UserLevel < 70)
            {
                RankName = "Ascendant";
                RankColor = "#2ECC71";
            }
            else if (UserLevel < 100)
            {
                RankName = "Immortal";
                RankColor = "#E74C3C";
            }
            else
            {
                RankName = "Radiant";
                RankColor = "#F39C12";
            }
        }

        [RelayCommand]
        public async Task GoToChatAsync()
        {
            await Shell.Current.GoToAsync("ChatPage");
        }

        [RelayCommand]
        public async Task AskAiForAdviceAsync()
        {
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
            // สั่งให้แอปเปลี่ยนหน้าไปยัง BackupPage
            await Shell.Current.GoToAsync(nameof(SpendSmart.Views.BackupPage));
        }
    }
}