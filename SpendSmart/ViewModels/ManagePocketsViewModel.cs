/*
=========================================================================================
[สรุปภาพรวมการทำงานของไฟล์ ManagePocketsViewModel.cs]
ไฟล์นี้คือ "ผู้จัดการกระเป๋าเงิน" ทำหน้าที่ดูแลระบบ "บัญชีแยกประเภท" ของผู้ใช้ (Pockets)
เปรียบเสมือนการแบ่งเงินใส่ซองหรือกระปุกออมสิน โดยมีความสามารถเด่นๆ 4 อย่างคือ:

1. สร้างกระเป๋าใหม่: รองรับทั้งแบบ "กระเป๋าใช้จ่าย" (Spending) และ "กระเป๋าเงินเก็บ" (Saving)
2. คำนวณเป้าหมายอัตโนมัติ: ถ้าสร้างกระเป๋าเงินเก็บ ระบบจะคำนวณให้ทันทีว่า "ต้องเก็บเงินเดือนละเท่าไหร่" ถึงจะถึงเป้า
3. ระบบป้องกันการลบ (Safe Delete): ดักลอจิกห้ามลบกระเป๋าใบสุดท้าย, ห้ามลบกระเป๋าหลัก และที่สำคัญคือ
   "ห้ามลบกระเป๋าที่ยังมีเงินค้างอยู่" เพื่อป้องกันเงินผู้ใช้สูญหาย
4. โยกเงินแบบลากวาง (Drag & Drop): ให้ผู้ใช้ลากกระเป๋าใบหนึ่งไปวางใส่อีกใบ เพื่อโอนเงินหากัน 
   พร้อมบันทึกประวัติการโอน (Log) เก็บไว้ให้ดูย้อนหลัง
=========================================================================================
*/

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; 
using SpendSmart.Messages;
using SpendSmart.Models;
using SpendSmart.Services;
using System.Collections.ObjectModel;

namespace SpendSmart.ViewModels
{
    // สืบทอด ObservableObject เพื่อให้ตัวแปรสื่อสารกับหน้าจอ UI ได้แบบเรียลไทม์
    public partial class ManagePocketsViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        // ==========================================================================
        // 🌟 กลุ่มตัวแปรสำหรับ "ฟอร์มสร้างกระเป๋าใบใหม่"
        // ==========================================================================
        [ObservableProperty] private string _newPocketName;           // ชื่อกระเป๋า
        [ObservableProperty] private decimal _newPocketTarget;        // ยอดเป้าหมาย (ถ้าเป็นกระเป๋าออมเงิน)
        [ObservableProperty] private string _newPocketColor = "#00E5FF"; // สีธีมของกระเป๋า (ค่าเริ่มต้นเป็นสีฟ้า)
        [ObservableProperty] private DateTime _targetDate = DateTime.Now.AddMonths(6); // วันที่เป้าหมาย (เริ่มต้นคือ 6 เดือนข้างหน้า)
        [ObservableProperty] private decimal _calculatedMonthly;      // ตัวเลขที่ระบบคำนวณให้ว่าต้องเก็บเดือนละเท่าไหร่

        // ตัวเลือกประเภทกระเป๋าสำหรับ Dropdown (Picker)
        public List<string> PocketTypes { get; } = new() { "Saving (เงินเก็บ)", "Spending (ใช้จ่าย)" };

        [ObservableProperty] private string _selectedPocketType = "Saving (เงินเก็บ)"; // ประเภทที่ผู้ใช้เลือก
        [ObservableProperty] private bool _isSavingPocket = true; // สวิตช์บอกว่าเป็นกระเป๋าเงินเก็บหรือไม่ (เพื่อเอาไปเปิด/ปิดช่องกรอกเป้าหมาย)

        // ลิสต์รายการกระเป๋าเงินทั้งหมด เพื่อนำไปโชว์เป็น Card บนหน้าจอ
        public ObservableCollection<Pocket> Pockets { get; set; } = new();

        // Constructor: ฟังก์ชันเริ่มต้น
        public ManagePocketsViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;

            // ลงทะเบียนรับสัญญาณ: ถ้าระบบมีการอัปเดตเงิน (TransactionChangedMessage) ให้โหลดกระเป๋ามาโชว์ใหม่
            WeakReferenceMessenger.Default.Register<TransactionChangedMessage>(this, async (recipient, message) =>
            {
                await LoadPocketsAsync();
            });
        }

        // ==========================================================================
        // 🌟 กลุ่มฟังก์ชัน "คำนวณเป้าหมายอัตโนมัติ" (ทำงานทันทีที่ผู้ใช้พิมพ์แก้ข้อมูล)
        // ==========================================================================

        // เมื่อผู้ใช้เปลี่ยนประเภทกระเป๋า
        partial void OnSelectedPocketTypeChanged(string value)
        {
            IsSavingPocket = value.Contains("Saving"); // เช็กว่าเลือก Saving ใช่หรือไม่
            UpdateCalculation(); // สั่งคำนวณยอดเงินใหม่
        }

        // เมื่อผู้ใช้พิมพ์ตัวเลขเป้าหมายเปลี่ยนไป -> สั่งคำนวณใหม่
        partial void OnNewPocketTargetChanged(decimal value) => UpdateCalculation();

        // เมื่อผู้ใช้เปลี่ยนวันที่เป้าหมายเปลี่ยนไป -> สั่งคำนวณใหม่
        partial void OnTargetDateChanged(DateTime value) => UpdateCalculation();

        /// <summary>
        /// ลอจิกคำนวณว่าต้องเก็บเงินเดือนละเท่าไหร่
        /// </summary>
        private void UpdateCalculation()
        {
            // ถ้าเป็นกระเป๋าใช้จ่ายธรรมดา ไม่ต้องคำนวณอะไร
            if (!IsSavingPocket) return;

            // คำนวณหาระยะห่าง "จำนวนเดือน" ระหว่างวันนี้ ถึง วันที่เป้าหมาย
            int months = ((TargetDate.Year - DateTime.Now.Year) * 12) + TargetDate.Month - DateTime.Now.Month;

            // ดักความผิดพลาด: ถ้าเลือกเดือนชนกันหรือเดือนในอดีต ให้ตีเป็น 1 เดือนไว้ก่อน (ป้องกันตัวหารเป็นศูนย์)
            if (months <= 0) months = 1;

            // เอาเป้าหมายทั้งหมด หารด้วย จำนวนเดือน = ยอดที่ต้องเก็บต่อเดือน
            CalculatedMonthly = NewPocketTarget / months;
        }

        // ==========================================================================
        // 🌟 กลุ่มฟังก์ชันจัดการกระเป๋า (โหลด, สร้าง, ลบ)
        // ==========================================================================

        /// <summary>
        /// โหลดรายชื่อกระเป๋าเงินทั้งหมดจาก SQLite มาโชว์บนหน้าจอ
        /// </summary>
        [RelayCommand]
        public async Task LoadPocketsAsync()
        {
            var pockets = await _databaseService.GetPocketsAsync();
            Pockets.Clear();
            foreach (var p in pockets) Pockets.Add(p);
        }

        /// <summary>
        /// บันทึกกระเป๋าใบใหม่ลงระบบ
        /// </summary>
        [RelayCommand]
        public async Task AddCloudPocketAsync()
        {
            // เช็กความถูกต้อง: ห้ามเว้นว่างชื่อ
            if (string.IsNullOrWhiteSpace(NewPocketName))
            {
                await Shell.Current.DisplayAlert("แจ้งเตือน", "กรุณากรอกชื่อกระเป๋า", "ตกลง");
                return;
            }

            // เช็กความถูกต้อง: ถ้าสร้างกระเป๋าออมเงิน ต้องตั้งเป้าหมายมากกว่า 0 บาท
            if (IsSavingPocket && NewPocketTarget <= 0)
            {
                await Shell.Current.DisplayAlert("แจ้งเตือน", "กรุณาตั้งยอดเป้าหมายสำหรับการออม", "ตกลง");
                return;
            }

            // ประกอบร่าง Object กระเป๋าใบใหม่
            var pocket = new Pocket
            {
                Name = NewPocketName,
                PocketType = IsSavingPocket ? "Saving" : "Spending",
                GoalAmount = IsSavingPocket ? NewPocketTarget : 0,         // ยอดเป้าหมายสูงสุด
                TargetBudget = IsSavingPocket ? NewPocketTarget : 0,       // งบเป้าหมาย
                CurrentBalance = 0,                                        // เริ่มต้นเงินเป็น 0 เสมอ
                TargetDate = IsSavingPocket ? TargetDate : null,           // กำหนดวันเป้าหมาย
                MonthlySavingAim = IsSavingPocket ? CalculatedMonthly : 0, // ยอดออมต่อเดือนที่ระบบคิดให้
                ThemeColor = NewPocketColor
            };

            // เซฟลงฐานข้อมูล และสั่งโหลดขึ้นมาโชว์ใหม่
            await _databaseService.SavePocketAsync(pocket);
            await LoadPocketsAsync();

            // เคลียร์ฟอร์มหน้าจอให้กลับเป็นค่าว่าง พร้อมให้สร้างใบถัดไป
            NewPocketName = string.Empty;
            NewPocketTarget = 0;
            TargetDate = DateTime.Now.AddMonths(6);
            CalculatedMonthly = 0;
            SelectedPocketType = "Saving (เงินเก็บ)";

            // ส่งสัญญาณแจ้งหน้าอื่นว่ามีกระเป๋าใหม่เพิ่มมาแล้วนะ
            WeakReferenceMessenger.Default.Send(new TransactionChangedMessage());

            await Shell.Current.DisplayAlert("สำเร็จ", "สร้างกระเป๋าเรียบร้อย", "ตกลง");
        }

        /// <summary>
        /// ลบกระเป๋าเงินทิ้ง (พร้อมระบบป้องกันความผิดพลาด)
        /// </summary>
        [RelayCommand]
        public async Task DeletePocketAsync(Pocket pocket)
        {
            if (pocket == null) return;

            // 🌟 1. ตรวจสอบ: ต้องเหลืออย่างน้อย 1 กระเป๋าเสมอ (ไม่งั้นแอปพังเวลาพยายามจะเซฟรายจ่าย)
            if (Pockets.Count <= 1)
            {
                await Shell.Current.DisplayAlert("แจ้งเตือน", "ไม่สามารถลบได้ คุณต้องมีกระเป๋าเงินอย่างน้อย 1 ใบในระบบ", "ตกลง");
                return;
            }

            // 🌟 2. ตรวจสอบ: ห้ามลบกระเป๋าหลัก (ถ้ามีการตั้งค่า IsDefault ไว้)
            if (pocket.IsDefault)
            {
                await Shell.Current.DisplayAlert("แจ้งเตือน", "ไม่สามารถลบกระเป๋าหลักได้", "ตกลง");
                return;
            }

            // 🌟 3. ตรวจสอบ (Hard Block): ถ้าเงินไม่เป็น 0 ห้ามลบเด็ดขาด ป้องกันเงินหาย!
            if (pocket.CurrentBalance != 0)
            {
                await Shell.Current.DisplayAlert(
                    "ไม่สามารถลบได้",
                    $"กระเป๋านี้ยังมีเงินค้างอยู่ {pocket.CurrentBalance:N2} ฿\n\nกรุณาโยกเงินออกจากกระเป๋านี้ให้หมดจนเหลือ 0 ฿ ก่อนทำการลบครับ",
                    "ตกลง");
                return; // ตัดจบการทำงานทันที
            }

            // 4. ถามยืนยันเพื่อความชัวร์อีกรอบ (กรณีเงินเป็น 0 แล้ว)
            bool confirm = await Shell.Current.DisplayAlert(
                "ยืนยันการลบ",
                $"ต้องการลบกระเป๋า '{pocket.Name}' ใช่หรือไม่?\n(ประวัติรายการทั้งหมดของกระเป๋านี้จะถูกลบออกด้วย)",
                "ลบ",
                "ยกเลิก");

            if (!confirm) return;

            // ลบของจริงและรีเฟรชหน้าจอ
            await _databaseService.DeletePocketAndTransactionsAsync(pocket);
            await LoadPocketsAsync();

            WeakReferenceMessenger.Default.Send(new TransactionChangedMessage());
            await Shell.Current.DisplayAlert("สำเร็จ", "ลบกระเป๋าเรียบร้อยแล้ว", "ตกลง");
        }

        // ==========================================================================
        // 🌟 กลุ่มฟังก์ชัน "ย้ายเงินแบบลากวาง" (Drag & Drop)
        // ==========================================================================

        // ตัวแปรจำเอาไว้ว่าตอนเริ่มกดลาก ผู้ใช้กำลังจิ้มกระเป๋าใบไหนอยู่ (กระเป๋าต้นทาง)
        private Pocket _draggedPocket;

        /// <summary>
        /// เมื่อผู้ใช้เริ่มเอานิ้วจิ้มและลากการ์ดกระเป๋า
        /// </summary>
        [RelayCommand]
        public void DragStarting(Pocket pocket)
        {
            _draggedPocket = pocket; // จำกระเป๋าใบต้นทางเอาไว้
        }

        /// <summary>
        /// เมื่อผู้ใช้ปล่อยนิ้ววางการ์ดกระเป๋าลงบนกระเป๋าอีกใบ (destinationPocket = กระเป๋าปลายทาง)
        /// </summary>
        [RelayCommand]
        public async Task DropPocketAsync(Pocket destinationPocket)
        {
            // ดักความผิดพลาด: ถ้ายกเลิกลากกลางคัน หรือลากใส่ตัวเอง ให้เมินคำสั่งนี้ไปเลย
            if (_draggedPocket == null || destinationPocket == null || _draggedPocket.Id == destinationPocket.Id)
            {
                _draggedPocket = null;
                return;
            }

            // เด้งป๊อปอัพขึ้นมาถามว่า "จะโยกเงินเท่าไหร่?" พร้อมดึงแป้นพิมพ์ตัวเลข (Keyboard.Numeric) ขึ้นมาให้
            string amountStr = await Shell.Current.DisplayPromptAsync(
                "ย้ายเงิน (Drag & Drop)",
                $"โอนจาก '{_draggedPocket.Name}'\nไปยัง '{destinationPocket.Name}'\n\nระบุจำนวนเงิน:",
                keyboard: Keyboard.Numeric);

            // ถ้าผู้ใช้พิมพ์ตัวเลขที่ถูกต้องและยอดมากกว่า 0
            if (decimal.TryParse(amountStr, out decimal amount) && amount > 0)
            {
                // เช็กก่อนว่ากระเป๋าต้นทางมีเงินพอให้โยกไหม?
                if (_draggedPocket.CurrentBalance < amount)
                {
                    await Shell.Current.DisplayAlert("ไม่สำเร็จ", "ยอดเงินในกระเป๋าต้นทางไม่เพียงพอ", "ตกลง");
                    _draggedPocket = null;
                    return;
                }

                // หักเงินต้นทาง และ บวกเงินให้ปลายทาง
                _draggedPocket.CurrentBalance -= amount;
                destinationPocket.CurrentBalance += amount;

                // สั่งเซฟการเปลี่ยนแปลงของกระเป๋าทั้งคู่ลงฐานข้อมูล
                await _databaseService.SavePocketAsync(_draggedPocket);
                await _databaseService.SavePocketAsync(destinationPocket);

                // 🌟 บันทึกประวัติ (Log) ของการโยกเงิน เพื่อให้ไปแสดงผลในหน้า History
                // มีการเก็บทั้ง PocketId(ต้นทาง) และ TargetPocketId(ปลายทาง) เพื่อลอจิกตอนลบ Undo เงินคืนให้ถูกที่
                var transferLog = new TransactionRecord
                {
                    Type = "โยกเงิน",
                    SubCategory = "🔄 Transfer",
                    Amount = amount,
                    Date = DateTime.Now,
                    Note = $"โยกเงินจาก [{_draggedPocket.Name}] ➔ [{destinationPocket.Name}]",
                    PocketId = _draggedPocket.Id,      // กระเป๋าต้นทาง (โดนหักเงิน)
                    TargetPocketId = destinationPocket.Id // กระเป๋าปลายทาง (ได้รับเงิน)
                };

                await _databaseService.SaveTransactionAsync(transferLog);

                // โหลดหน้าจอใหม่เพื่อให้ตัวเลขเงินปรับทันที
                await LoadPocketsAsync();
                WeakReferenceMessenger.Default.Send(new TransactionChangedMessage());
            }

            // เสร็จงานแล้ว เคลียร์ค่าตัวแปรจำทิ้งไปเพื่อรอลากครั้งถัดไป
            _draggedPocket = null;
        }
    }
}