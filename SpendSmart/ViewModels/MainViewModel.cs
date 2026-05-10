using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpendSmart.Models;
using SpendSmart.Services;
using System.Collections.ObjectModel;

namespace SpendSmart.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        // ตัวแปรเก็บยอดเงินรวม (ใช้ [ObservableProperty] เพื่อให้ UI อัปเดตอัตโนมัติเมื่อค่าเปลี่ยน)
        [ObservableProperty]
        private decimal _totalBalance;

        // ลิสต์เก็บกระเป๋าเงินและรายการล่าสุด
        public ObservableCollection<Pocket> Pockets { get; set; } = new();
        public ObservableCollection<TransactionRecord> RecentTransactions { get; set; } = new();

        public MainViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            // 1. โหลดข้อมูลกระเป๋าเงินและคำนวณยอดรวม
            var pockets = await _databaseService.GetPocketsAsync();
            Pockets.Clear();
            decimal total = 0;

            foreach (var pocket in pockets)
            {
                Pockets.Add(pocket);
                total += pocket.CurrentBalance;
            }
            TotalBalance = total;

            // 2. โหลดรายการเคลื่อนไหวล่าสุด (สมมติเอามาแค่ 5 รายการแรก)
            var transactions = await _databaseService.GetTransactionsAsync();
            RecentTransactions.Clear();

            foreach (var transaction in transactions.Take(5))
            {
                RecentTransactions.Add(transaction);
            }
        }
        [RelayCommand]
        public async Task GoToAddTransactionAsync()
        {
            // เปลี่ยนมาเรียกใช้ชื่อ Route ที่ลงทะเบียนไว้แบบตรงๆ
            await Shell.Current.GoToAsync("AddTransactionPage");
        }
    }
}