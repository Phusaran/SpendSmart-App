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

        [ObservableProperty]
        private decimal _totalBalance;

        public ObservableCollection<Pocket> Pockets { get; set; } = new();
        public ObservableCollection<TransactionRecord> RecentTransactions { get; set; } = new();

        public MainViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            var pockets = await _databaseService.GetPocketsAsync();
            Pockets.Clear();

            decimal total = 0;

            foreach (var pocket in pockets)
            {
                Pockets.Add(pocket);
                total += pocket.CurrentBalance;
            }

            TotalBalance = total;

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
            var pockets = await _databaseService.GetPocketsAsync();

            if (pockets.Count == 0)
            {
                bool goCreatePocket = await Shell.Current.DisplayAlert(
                    "ยินดีต้อนรับสู่ SpendSmart",
                    "มาเริ่มต้นปั้นความฝันของคุณให้เป็นจริงกัน\n" +
"โดยแยกเงินออมตามใจชอบเหมือนมีกระปุกหลายใบ\n\n" +
"เริ่มสร้างกระเป๋าใบแรกของคุณเลย\n" +
"(เช่น ออมซื้อมือถือ/กระเป๋าจ่ายค่าอาหาร)",
                    "ไปสร้างกระเป๋า",
                    "ยกเลิก");

                if (goCreatePocket)
                {
                    await Shell.Current.GoToAsync("ManagePocketsPage");
                }

                return;
            }

            await Shell.Current.GoToAsync("AddTransactionPage");
        }
    }
}