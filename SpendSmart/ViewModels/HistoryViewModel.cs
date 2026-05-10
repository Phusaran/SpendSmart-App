using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpendSmart.Models;
using SpendSmart.Services;
using System.Collections.ObjectModel;

namespace SpendSmart.ViewModels
{
    public partial class HistoryViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        // ลิสต์เก็บประวัติการทำรายการทั้งหมด
        public ObservableCollection<TransactionRecord> Transactions { get; set; } = new();

        public HistoryViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        [RelayCommand]
        public async Task LoadHistoryAsync()
        {
            var data = await _databaseService.GetTransactionsAsync();
            Transactions.Clear();

            foreach (var item in data)
            {
                Transactions.Add(item);
            }
        }
    }
}