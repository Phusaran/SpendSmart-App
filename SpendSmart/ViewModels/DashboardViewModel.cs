using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpendSmart.Models;
using SpendSmart.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace SpendSmart.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        // 🌟 เปลี่ยนมาเรียกใช้ Service ตัวใหม่
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

        // 🌟 Inject Service ตัวใหม่เข้ามาใน Constructor
        public DashboardViewModel(DatabaseService databaseService, AIFinancialAdvisorService aiAdvisor)
        {
            _databaseService = databaseService;
            _aiAdvisor = aiAdvisor;
        }

        public async Task LoadDashboardDataAsync()
        {
            var transactions = await _databaseService.GetTransactionsAsync();

            var currentMonthExpenses = transactions
                .Where(t => t.Type == "Expense" && t.Date.Month == DateTime.Now.Month && t.Date.Year == DateTime.Now.Year)
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
            // 🌟 อัปเดตข้อมูลแรงก์
            var profile = await _databaseService.GetUserProfileAsync();
            UserLevel = profile.CurrentLevel;
            ExpProgress = profile.LevelProgress;
            ExpText = profile.ExpText;

            // คลาสสิฟาย Rank ตามเลเวล
            if (UserLevel < 5) { RankName = "Iron"; RankColor = "#7F8C8D"; } // เทา
            else if (UserLevel < 10) { RankName = "Bronze"; RankColor = "#CD7F32"; } // ทองแดง
            else if (UserLevel < 20) { RankName = "Silver"; RankColor = "#BDC3C7"; } // เงิน
            else if (UserLevel < 30) { RankName = "Gold"; RankColor = "#F1C40F"; } // ทอง
            else if (UserLevel < 40) { RankName = "Platinum"; RankColor = "#1ABC9C"; } // เขียวอมฟ้า
            else if (UserLevel < 50) { RankName = "Diamond"; RankColor = "#9B59B6"; } // ม่วง
            else if (UserLevel < 70) { RankName = "Ascendant"; RankColor = "#2ECC71"; } // เขียวมรกต
            else if (UserLevel < 100) { RankName = "Immortal"; RankColor = "#E74C3C"; } // แดง
            else { RankName = "Radiant"; RankColor = "#F39C12"; } // ทองคำเปล่งประกาย
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

            // 🌟 ส่งข้อมูลให้ Service ตัวใหม่ทำงาน
            AiAdvice = await _aiAdvisor.GetFinancialAdviceAsync(TotalExpense, CategoryData);

            IsAiThinking = false;
        }
    }
}