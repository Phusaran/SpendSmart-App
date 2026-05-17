/*
=========================================================================================
[สรุปภาพรวมการทำงานของไฟล์ HistoryViewModel.cs]
ไฟล์นี้คือ "ผู้จัดการหน้าจอประวัติธุรกรรม" (History Screen) ทำหน้าที่คอยเชื่อมต่อข้อมูลระหว่าง
หน้าจอแสดงผล(UI) กับฐานข้อมูล SQLite โดยมีฟังก์ชันหลัก 2 อย่างคือ:
1. โหลดประวัติ (LoadHistoryAsync): ดึงรายการธุรกรรม (รายรับ-รายจ่าย) ทั้งหมดจากฐานข้อมูล 
มาแสดงผลบนตารางหรือรายการในหน้าแอปพลิเคชัน
2. ลบประวัติและคืนเงิน (DeleteTransactionAsync): เมื่อผู้ใช้สั่งลบรายการใดรายการหนึ่ง 
ระบบจะคำนวณเงินย้อนกลับ(Undo) เพื่อนำเงินไปคืนหรือหักออกจากกระเป๋าใบที่เคยใช้ให้ถูกต้อง 
ก่อนจะลบประวัติรายการนั้นทิ้งไปอย่างปลอดภัย
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
    // คลาสนี้สืบทอดมาจาก ObservableObject เพื่อให้หน้าจอ UI คอยจับตาดูการเปลี่ยนแปลงของข้อมูลได้
    public partial class HistoryViewModel : ObservableObject
    {
        // ตัวแปรสำหรับเรียกใช้งาน Service จัดการฐานข้อมูล SQLite
        private readonly DatabaseService _databaseService;

        // ObservableCollection คือลิสต์พิเศษที่เมื่อมีข้อมูลเพิ่มหรือลด หน้าจอ UI จะอัปเดตแสดงผลทันทีโดยอัตโนมัติ
        public ObservableCollection<TransactionRecord> Transactions { get; set; } = new();

        // Constructor: ตัวเริ่มต้นระบบ จะรับ DatabaseService เข้ามาใช้งานผ่าน Dependency Injection
        public HistoryViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// [คำสั่งโหลดประวัติการเงิน]
        /// ทำหน้าที่กวาดข้อมูลธุรกรรมทั้งหมดจากฐานข้อมูล SQLite มายัดใส่ลิสต์เพื่อโชว์บนหน้าจอ
        /// </summary>

        [RelayCommand]
        public async Task LoadHistoryAsync()
        {
            // ดึงข้อมูลรายการทั้งหมดจากฐานข้อมูลในรูปแบบ asynchronous (ไม่ทำให้แอปค้างขณะโหลด)
            var data = await _databaseService.GetTransactionsAsync();

            // ล้างข้อมูลเก่าที่ค้างอยู่ในลิสต์หน้าจอออกก่อนป้องกันข้อมูลแสดงผลซ้ำ
            Transactions.Clear();

            // นำข้อมูลใหม่ที่ได้จากฐานข้อมูล วนลูปแอดเพิ่มเข้าไปในลิสต์หน้าจอทีละรายการ
            foreach (var item in data)
            {
                Transactions.Add(item);
            }
        }

        /// <summary>
        /// [คำสั่งลบรายการธุรกรรม]
        /// ทำหน้าที่ลบรายการที่เราไม่ต้องการ พร้อมลอจิกพิเศษ "คืนยอดเงิน" กลับเข้ากระเป๋าเงินใบเดิมด้วย
        /// </summary>
        [RelayCommand]
        public async Task DeleteTransactionAsync(TransactionRecord transaction)
        {
            // เซฟตี้ดักจับ: ถ้าข้อมูลส่งมาเป็นค่าว่าง (Null) ให้ยกเลิกการทำงานทันทีป้องกันแอปแครช
            if (transaction == null)
                return;

            // แสดงกล่องข้อความป๊อปอัพแจ้งเตือนถามผู้ใช้เพื่อความแน่ใจก่อนจะลบข้อมูลจริง
            bool confirm = await Shell.Current.DisplayAlert(
                "ยืนยันการลบ",
                "ต้องการลบรายการนี้และคืนยอดเงินในกระเป๋าใช่หรือไม่?",
                "ลบ",
                "ยกเลิก");

            // ถ้าผู้ใช้กด "ยกเลิก" ให้จบการทำงานทันที ไม่ลบข้อมูล
            if (!confirm)
                return;

            // 🌟 จุดสำคัญ: เรียกใช้ Service หลังบ้าน เพื่อไปลบตัวประวัติในฐานข้อมูล 
            // พร้อมทั้งทำลอจิกคืนเงิน (Undo) ยอดเงินในกระเป๋าใบที่เกี่ยวข้องให้อัตโนมัติ
            await _databaseService.DeleteTransactionAndUndoPocketAsync(transaction);

            // ลบรายการธุรกรรมชิ้นนี้ออกจากลิสต์ที่แสดงอยู่บนหน้าจอแอปปัจจุบันทันที
            Transactions.Remove(transaction);

            // 🌟 ส่งสัญญาณกระจายข่าวไปบอกหน้าอื่นๆ (เช่น หน้าแดชบอร์ด, หน้ากระเป๋าเงิน) 
            // ว่าตอนนี้ข้อมูลเงินเปลี่ยนไปแล้วนะ ให้หน้าพวกนั้นทำการโหลดเลขยอดเงินใหม่ให้เป็นปัจจุบันด้วย
            WeakReferenceMessenger.Default.Send(new TransactionChangedMessage());
        }
    }
}