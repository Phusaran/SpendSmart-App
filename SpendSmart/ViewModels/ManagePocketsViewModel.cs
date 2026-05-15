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
            if (pocket == null) return;

            // 🌟 1. ตรวจสอบ: ต้องเหลืออย่างน้อย 1 กระเป๋าเสมอ
            if (Pockets.Count <= 1)
            {
                await Shell.Current.DisplayAlert("แจ้งเตือน", "ไม่สามารถลบได้ คุณต้องมีกระเป๋าเงินอย่างน้อย 1 ใบในระบบ", "ตกลง");
                return;
            }

            // 🌟 2. ตรวจสอบ: ห้ามลบกระเป๋าหลัก (ถ้ามีระบบ IsDefault)
            if (pocket.IsDefault)
            {
                await Shell.Current.DisplayAlert("แจ้งเตือน", "ไม่สามารถลบกระเป๋าหลักได้", "ตกลง");
                return;
            }

            // 🌟 3. ตรวจสอบ (Hard Block): ถ้าเงินไม่เป็น 0 ห้ามลบเด็ดขาด
            if (pocket.CurrentBalance != 0)
            {
                await Shell.Current.DisplayAlert(
                    "ไม่สามารถลบได้",
                    $"กระเป๋านี้ยังมีเงินค้างอยู่ {pocket.CurrentBalance:N2} ฿\n\nกรุณาโยกเงินออกจากกระเป๋านี้ให้หมดจนเหลือ 0 ฿ ก่อนทำการลบครับ",
                    "ตกลง");
                return; // ตัดจบการทำงานทันที
            }

            // 4. ยืนยันการลบ (กรณีเงินเป็น 0 แล้ว)
            bool confirm = await Shell.Current.DisplayAlert(
                "ยืนยันการลบ",
                $"ต้องการลบกระเป๋า '{pocket.Name}' ใช่หรือไม่?\n(ประวัติรายการทั้งหมดของกระเป๋านี้จะถูกลบออกด้วย)",
                "ลบ",
                "ยกเลิก");

            if (!confirm) return;

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

                // 🌟 บันทึก Log พร้อมเก็บ ID ต้นทางและปลายทาง เพื่อใช้ Undo ในหน้า History
                var transferLog = new TransactionRecord
                {
                    Type = "โยกเงิน",
                    SubCategory = "🔄 Transfer",
                    Amount = amount,
                    Date = DateTime.Now,
                    Note = $"โยกเงินจาก [{_draggedPocket.Name}] ➔ [{destinationPocket.Name}]",
                    PocketId = _draggedPocket.Id,      // ต้นทาง
                    TargetPocketId = destinationPocket.Id // ปลายทาง
                };

                await _databaseService.SaveTransactionAsync(transferLog);

                await LoadPocketsAsync();

                WeakReferenceMessenger.Default.Send(new TransactionChangedMessage());
            }

            _draggedPocket = null;
        }
    }
}