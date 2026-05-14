using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SpendSmart.Messages;
using SpendSmart.Models;
using SpendSmart.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SpendSmart.ViewModels
{
    public partial class ManagePocketsViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty] private string _newPocketName;
        [ObservableProperty] private decimal _newPocketTarget;
        [ObservableProperty] private string _newPocketColor = "#00E5FF";
        [ObservableProperty] private DateTime _targetDate = DateTime.Now.AddMonths(6);
        [ObservableProperty] private decimal _calculatedMonthly;

        public List<string> PocketTypes { get; } = new() { "Saving (เงินเก็บ)", "Spending (ใช้จ่าย)" };

        [ObservableProperty] private string _selectedPocketType = "Saving (เงินเก็บ)";
        [ObservableProperty] private bool _isSavingPocket = true;

        public ObservableCollection<Pocket> Pockets { get; set; } = new();

        public ManagePocketsViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;

            WeakReferenceMessenger.Default.Register<TransactionChangedMessage>(this, async (recipient, message) =>
            {
                await LoadPocketsAsync();
            });
        }

        partial void OnSelectedPocketTypeChanged(string value)
        {
            IsSavingPocket = value.Contains("Saving");
            UpdateCalculation();
        }

        partial void OnNewPocketTargetChanged(decimal value) => UpdateCalculation();

        partial void OnTargetDateChanged(DateTime value) => UpdateCalculation();

        private void UpdateCalculation()
        {
            if (!IsSavingPocket)
                return;

            int months = ((TargetDate.Year - DateTime.Now.Year) * 12) + TargetDate.Month - DateTime.Now.Month;

            if (months <= 0)
                months = 1;

            CalculatedMonthly = NewPocketTarget / months;
        }

        [RelayCommand]
        public async Task LoadPocketsAsync()
        {
            var pockets = await _databaseService.GetPocketsAsync();

            Pockets.Clear();

            foreach (var p in pockets)
            {
                Pockets.Add(p);
            }
        }

        [RelayCommand]
        public async Task AddCloudPocketAsync()
        {
            if (string.IsNullOrWhiteSpace(NewPocketName))
            {
                await Shell.Current.DisplayAlert("แจ้งเตือน", "กรุณากรอกชื่อกระเป๋า", "ตกลง");
                return;
            }

            if (IsSavingPocket && NewPocketTarget <= 0)
            {
                await Shell.Current.DisplayAlert("แจ้งเตือน", "กรุณาตั้งยอดเป้าหมายสำหรับการออม", "ตกลง");
                return;
            }

            var pocket = new Pocket
            {
                Name = NewPocketName,
                PocketType = IsSavingPocket ? "Saving" : "Spending",
                GoalAmount = IsSavingPocket ? NewPocketTarget : 0,
                TargetBudget = IsSavingPocket ? NewPocketTarget : 0,
                CurrentBalance = 0,
                TargetDate = IsSavingPocket ? TargetDate : null,
                MonthlySavingAim = IsSavingPocket ? CalculatedMonthly : 0,
                ThemeColor = NewPocketColor
            };

            await _databaseService.SavePocketAsync(pocket);
            await LoadPocketsAsync();

            NewPocketName = string.Empty;
            NewPocketTarget = 0;
            TargetDate = DateTime.Now.AddMonths(6);
            CalculatedMonthly = 0;
            SelectedPocketType = "Saving (เงินเก็บ)";

            WeakReferenceMessenger.Default.Send(new TransactionChangedMessage());

            await Shell.Current.DisplayAlert("สำเร็จ", "สร้าง Cloud Pocket เรียบร้อย", "ตกลง");
        }

        [RelayCommand]
        public async Task DeletePocketAsync(Pocket pocket)
        {
            if (pocket == null)
                return;

            if (pocket.IsDefault)
            {
                await Shell.Current.DisplayAlert("แจ้งเตือน", "ไม่สามารถลบกระเป๋าหลักได้", "ตกลง");
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert(
                "ยืนยันการลบ",
                $"ต้องการลบกระเป๋า '{pocket.Name}' ใช่หรือไม่?\n\nรายการประวัติทั้งหมดของกระเป๋านี้จะถูกลบด้วย",
                "ลบ",
                "ยกเลิก");

            if (!confirm)
                return;

            await _databaseService.DeletePocketAndTransactionsAsync(pocket);
            await LoadPocketsAsync();

            WeakReferenceMessenger.Default.Send(new TransactionChangedMessage());

            await Shell.Current.DisplayAlert("สำเร็จ", "ลบกระเป๋าเรียบร้อยแล้ว", "ตกลง");
        }

        private Pocket _draggedPocket;

        [RelayCommand]
        public void DragStarting(Pocket pocket)
        {
            _draggedPocket = pocket;
        }

        [RelayCommand]
        public async Task DropPocketAsync(Pocket destinationPocket)
        {
            if (_draggedPocket == null || destinationPocket == null || _draggedPocket.Id == destinationPocket.Id)
            {
                _draggedPocket = null;
                return;
            }

            string amountStr = await Shell.Current.DisplayPromptAsync(
                "ย้ายเงิน (Drag & Drop)",
                $"โอนจาก '{_draggedPocket.Name}'\nไปยัง '{destinationPocket.Name}'\n\nระบุจำนวนเงิน:",
                keyboard: Keyboard.Numeric);

            if (decimal.TryParse(amountStr, out decimal amount) && amount > 0)
            {
                if (_draggedPocket.CurrentBalance < amount)
                {
                    await Shell.Current.DisplayAlert("ไม่สำเร็จ", "ยอดเงินในกระเป๋าต้นทางไม่เพียงพอ", "ตกลง");
                    _draggedPocket = null;
                    return;
                }

                _draggedPocket.CurrentBalance -= amount;
                destinationPocket.CurrentBalance += amount;

                await _databaseService.SavePocketAsync(_draggedPocket);
                await _databaseService.SavePocketAsync(destinationPocket);

                await LoadPocketsAsync();

                WeakReferenceMessenger.Default.Send(new TransactionChangedMessage());
            }

            _draggedPocket = null;
        }
    }
}