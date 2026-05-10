using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpendSmart.Models;
using SpendSmart.Services;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System;
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

        // 🌟 1. เพิ่มตัวแปรสำหรับ Dropdown เลือกประเภท
        public List<string> PocketTypes { get; } = new() { "Saving (เงินเก็บ)", "Spending (ใช้จ่าย)" };
        [ObservableProperty] private string _selectedPocketType = "Saving (เงินเก็บ)";

        // 🌟 2. ตัวแปรควบคุมการโชว์ช่องเป้าหมายและวันที่
        [ObservableProperty] private bool _isSavingPocket = true;

        public ObservableCollection<Pocket> Pockets { get; set; } = new();

        public ManagePocketsViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        // 🌟 3. เมื่อเปลี่ยนประเภทกระเป๋า ให้ซ่อน/โชว์ ช่องเป้าหมายอัตโนมัติ
        partial void OnSelectedPocketTypeChanged(string value)
        {
            IsSavingPocket = value.Contains("Saving");
            UpdateCalculation();
        }

        partial void OnNewPocketTargetChanged(decimal value) => UpdateCalculation();
        partial void OnTargetDateChanged(DateTime value) => UpdateCalculation();

        private void UpdateCalculation()
        {
            if (!IsSavingPocket) return; // ถ้าเป็นกระเป๋าใช้จ่าย ไม่ต้องคำนวณเดือน

            int months = ((TargetDate.Year - DateTime.Now.Year) * 12) + TargetDate.Month - DateTime.Now.Month;
            if (months <= 0) months = 1;
            CalculatedMonthly = NewPocketTarget / months;
        }

        [RelayCommand]
        public async Task LoadPocketsAsync()
        {
            var pockets = await _databaseService.GetPocketsAsync();
            Pockets.Clear();
            foreach (var p in pockets) Pockets.Add(p);
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

            // 🌟 4. บันทึกข้อมูลแยกตามประเภท
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

            // เคลียร์ช่อง
            NewPocketName = string.Empty;
            NewPocketTarget = 0;
            TargetDate = DateTime.Now.AddMonths(6);
            CalculatedMonthly = 0;
            SelectedPocketType = "Saving (เงินเก็บ)"; // รีเซ็ตกลับเป็นค่าเดิม

            await Shell.Current.DisplayAlert("สำเร็จ", "สร้าง Cloud Pocket เรียบร้อย", "ตกลง");
        }

        [RelayCommand]
        public async Task DeletePocketAsync(Pocket pocket)
        {
            // 1. ป้องกันการลบกระเป๋าที่เป็นค่าเริ่มต้น (ถ้ามีระบบ Default)
            if (pocket.IsDefault)
            {
                await Shell.Current.DisplayAlert("แจ้งเตือน", "ไม่สามารถลบกระเป๋าหลักได้", "ตกลง");
                return;
            }

            // 2. ถามเพื่อความแน่ใจก่อนลบจริง
            bool confirm = await Shell.Current.DisplayAlert(
                "ยืนยันการลบ",
                $"คุณต้องการลบกระเป๋า '{pocket.Name}' ใช่หรือไม่?\n(ข้อมูลยอดเงินในกระเป๋านี้จะหายไป)",
                "ลบเลย", "ยกเลิก");

            if (confirm)
            {
                // 3. ลบข้อมูลจากฐานข้อมูล SQLite
                await _databaseService.DeletePocketAsync(pocket);

                // 4. อัปเดตรายการบนหน้าจอทันที
                await LoadPocketsAsync();

                // (เพิ่มเติม) ถ้าแอปมีการคำนวณยอดรวมในหน้าอื่น อย่าลืมส่งสัญญาณแจ้งเตือนด้วยครับ
                await Shell.Current.DisplayAlert("สำเร็จ", "ลบกระเป๋าเรียบร้อยแล้ว", "ตกลง");
            }
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
            // 1. ถ้าไม่ได้ลาก หรือลากไปใส่กระเป๋าเดิม ให้ยกเลิก
            if (_draggedPocket == null || _draggedPocket.Id == destinationPocket.Id)
            {
                _draggedPocket = null;
                return;
            }

            // 2. เด้ง Popup ถามจำนวนเงินที่ต้องการโอน (ย้ายเงิน)
            string amountStr = await Shell.Current.DisplayPromptAsync(
                "ย้ายเงิน (Drag & Drop)",
                $"โอนจาก '{_draggedPocket.Name}'\nไปยัง '{destinationPocket.Name}'\n\nระบุจำนวนเงิน:",
                keyboard: Keyboard.Numeric);

            // 3. ตรวจสอบว่ากรอกตัวเลขถูกต้องและมากกว่า 0 ไหม
            if (decimal.TryParse(amountStr, out decimal amount) && amount > 0)
            {
                // เช็กว่าเงินในกระเป๋าต้นทางมีพอให้โอนไหม
                if (_draggedPocket.CurrentBalance < amount)
                {
                    await Shell.Current.DisplayAlert("ไม่สำเร็จ", "ยอดเงินในกระเป๋าต้นทางไม่เพียงพอ", "ตกลง");
                    _draggedPocket = null;
                    return;
                }

                // หักเงินต้นทาง และเพิ่มเงินปลายทาง
                _draggedPocket.CurrentBalance -= amount;
                destinationPocket.CurrentBalance += amount;

                // บันทึกลง Database
                await _databaseService.SavePocketAsync(_draggedPocket);
                await _databaseService.SavePocketAsync(destinationPocket);

                // รีเฟรชหน้าจอใหม่
                await LoadPocketsAsync();
            }

            // ล้างค่าหลังจากจบกระบวนการ
            _draggedPocket = null;
        }
    }
}