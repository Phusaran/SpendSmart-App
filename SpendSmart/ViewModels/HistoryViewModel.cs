using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SpendSmart.Messages;
using SpendSmart.Models;
using SpendSmart.Services;
using System.Collections.ObjectModel;

namespace SpendSmart.ViewModels
{
    public partial class HistoryViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

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

        [RelayCommand]
        public async Task DeleteTransactionAsync(TransactionRecord transaction)
        {
            if (transaction == null)
                return;

            bool confirm = await Shell.Current.DisplayAlert(
                "ยืนยันการลบ",
                "ต้องการลบรายการนี้และคืนยอดเงินในกระเป๋าใช่หรือไม่?",
                "ลบ",
                "ยกเลิก");

            if (!confirm)
                return;

            await _databaseService.DeleteTransactionAndUndoPocketAsync(transaction);

            Transactions.Remove(transaction);

            WeakReferenceMessenger.Default.Send(new TransactionChangedMessage());
        }
    }
}