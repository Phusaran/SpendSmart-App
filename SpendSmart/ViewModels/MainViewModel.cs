/*
=========================================================================================
[สรุปภาพรวมการทำงานของไฟล์ MainViewModel.cs]
ไฟล์นี้คือ "ผู้จัดการหน้าจอหลัก (Home/Main Screen)" ซึ่งเป็นหน้าแรกที่ผู้ใช้จะเห็นเมื่อเข้าแอปพลิเคชัน
ทำหน้าที่เป็นศูนย์กลางการแสดงผลข้อมูลที่สำคัญที่สุด โดยมีความสามารถหลัก 3 ส่วนคือ:

1. สรุปยอดเงินรวม (Total Balance): ดึงข้อมูลกระเป๋าเงินทุกใบมาบวกรวมกัน เพื่อให้ผู้ใช้เห็นความมั่งคั่งทั้งหมด
2. แสดงภาพรวมย่อ (Overview): โชว์รายการกระเป๋าเงินทั้งหมด (Pockets) และดึงประวัติการใช้จ่ายล่าสุด 5 รายการมาแสดง
3. ระบบนำทางอัจฉริยะ (Smart Navigation): ควบคุมปุ่ม "เพิ่มรายการ" โดยมีลอจิกดักจับว่า 
   ถ้าผู้ใช้เพิ่งโหลดแอปมาใหม่และ "ยังไม่มีกระเป๋าเงินเลยสักใบ" ระบบจะเด้งป๊อปอัพชวนให้ไปสร้างกระเป๋าใบแรกก่อน 
   เพื่อป้องกันแอปแครชเวลาจะบันทึกรายจ่าย
=========================================================================================
*/

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpendSmart.Models;
using SpendSmart.Services;
using System.Collections.ObjectModel;

namespace SpendSmart.ViewModels
{
    // สืบทอด ObservableObject เพื่อให้ตัวแปรต่างๆ สามารถแจ้งเตือนให้หน้าจอ (UI) อัปเดตตามได้ทันที
    public partial class MainViewModel : ObservableObject
    {
        // ตัวแปรสำหรับเรียกใช้บริการฐานข้อมูล SQLite
        private readonly DatabaseService _databaseService;

        // ==========================================================================
        // 🌟 กลุ่มตัวแปรผูกกับหน้าจอ UI (Properties)
        // ==========================================================================

        // ตัวแปรเก็บยอดเงินรวมทั้งหมดของทุกกระเป๋า
        [ObservableProperty]
        private decimal _totalBalance;

        // ลิสต์สำหรับเก็บรายการกระเป๋าเงิน (เพื่อให้หน้าจอนำไปสร้างเป็น Card โชว์เรียงกัน)
        public ObservableCollection<Pocket> Pockets { get; set; } = new();

        // ลิสต์สำหรับเก็บประวัติธุรกรรมล่าสุด 
        public ObservableCollection<TransactionRecord> RecentTransactions { get; set; } = new();

        // Constructor: ฟังก์ชันเริ่มต้น จะรับ DatabaseService เข้ามาใช้งาน
        public MainViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// [ฟังก์ชันโหลดข้อมูลหน้าหลัก]
        /// จะถูกเรียกใช้ทุกครั้งที่หน้าจอหลักปรากฏขึ้น เพื่อรีเฟรชยอดเงินให้เป็นปัจจุบันเสมอ
        /// </summary>
        [RelayCommand]
        public async Task LoadDataAsync()
        {
            // 🌟 1. ดึงข้อมูลกระเป๋าเงินและคำนวณยอดเงินรวม
            var pockets = await _databaseService.GetPocketsAsync();

            // ล้าง List เก่าออกก่อนป้องกันการโชว์ข้อมูลซ้ำเบิ้ล
            Pockets.Clear();

            decimal total = 0; // ตัวแปรทดเลขสำหรับคำนวณยอดรวม

            foreach (var pocket in pockets)
            {
                // นำกระเป๋าแต่ละใบแอดใส่ List เพื่อให้ UI นำไปโชว์
                Pockets.Add(pocket);

                // นำเงินในกระเป๋าใบนั้นมาบวกทบเข้าไปในตัวแปร total
                total += pocket.CurrentBalance;
            }

            // นำยอดรวมที่คำนวณเสร็จแล้ว อัปเดตขึ้นแสดงบนหน้าจอ
            TotalBalance = total;

            // 🌟 2. ดึงข้อมูลประวัติการทำธุรกรรม
            var transactions = await _databaseService.GetTransactionsAsync();
            RecentTransactions.Clear();

            // ใช้คำสั่ง .Take(5) เพื่อดึงมาโชว์แค่ 5 รายการล่าสุดเท่านั้น (หน้าจอจะได้ไม่รกเกินไป)
            foreach (var transaction in transactions.Take(5))
            {
                RecentTransactions.Add(transaction);
            }
        }

        /// <summary>
        /// [ฟังก์ชันกดปุ่มไปหน้าเพิ่มรายการ (Add Transaction)]
        /// มีลอจิกช่วยป้องกันข้อผิดพลาดกรณีที่ผู้ใช้ยังไม่มีกระเป๋าเงิน
        /// </summary>
        [RelayCommand]
        public async Task GoToAddTransactionAsync()
        {
            // เช็กก่อนว่าตอนนี้ในระบบมีกระเป๋าเงินบ้างหรือยัง
            var pockets = await _databaseService.GetPocketsAsync();

            // 🌟 กรณีที่ 1: ตรวจพบว่ายังไม่มีกระเป๋าเงินเลย (ผู้ใช้ใหม่)
            if (pockets.Count == 0)
            {
                // โชว์ป๊อปอัพแจ้งเตือนด้วยข้อความเชิญชวนอย่างเป็นมิตร
                bool goCreatePocket = await Shell.Current.DisplayAlert(
                    "ยินดีต้อนรับสู่ SpendSmart",
                    "มาเริ่มต้นปั้นความฝันของคุณให้เป็นจริงกัน\n" +
                    "โดยแยกเงินออมตามใจชอบเหมือนมีกระปุกหลายใบ\n\n" +
                    "เริ่มสร้างกระเป๋าใบแรกของคุณเลย\n" +
                    "(เช่น ออมซื้อมือถือ/กระเป๋าจ่ายค่าอาหาร)",
                    "ไปสร้างกระเป๋า",
                    "ยกเลิก");

                // ถ้าผู้ใช้กดปุ่ม "ไปสร้างกระเป๋า" ระบบจะพานำทางไปยังหน้าจัดการกระเป๋าเงินทันที
                if (goCreatePocket)
                {
                    await Shell.Current.GoToAsync("ManagePocketsPage");
                }

                // สั่งยกเลิกการทำงานฟังก์ชันนี้ ไม่ให้พาไปหน้าเพิ่มรายการ เพราะยังไงก็บันทึกไม่ได้ (ไม่มีกระเป๋าให้หักเงิน)
                return;
            }

            // 🌟 กรณีที่ 2: มีกระเป๋าเงินอยู่แล้ว ก็พานำทางไปยังหน้า "บันทึกรายรับ-รายจ่าย" ได้เลย
            await Shell.Current.GoToAsync("AddTransactionPage");
        }
    }
}