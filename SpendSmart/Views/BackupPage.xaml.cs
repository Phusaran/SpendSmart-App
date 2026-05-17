/*
=========================================================================================
[สรุปภาพรวมการทำงานของคลาส BackupPage]
ไฟล์นี้คือ "โค้ดเบื้องหลังหน้าจอ UI (Code-Behind)" สำหรับหน้าสำรองและกู้คืนข้อมูล (Backup & Restore Screen)
ทำหน้าที่เป็นตัวกลางในการเชื่อมต่อโครงสร้างหน้าจอแสดงผลที่เขียนด้วย XAML (BackupPage.xaml) 
เข้ากับระบบประมวลผลหลังบ้านที่อยู่ใน BackupViewModel โดยมีหน้าที่หลัก 2 อย่างคือ:

1. รับระบบจัดการเข้าทำงาน (Dependency Injection): รับคลาส BackupViewModel เข้ามาทาง Constructor เมื่อหน้าจอนี้ถูกเรียกสร้าง
2. ผูกสายข้อมูล (Data Binding): กำหนด BindingContext ให้ชี้ไปที่ตัว ViewModel เพื่อให้ปุ่มกดต่างๆ ในหน้า XAML 
   (เช่น ปุ่มกดสำรองข้อมูล หรือปุ่มกู้คืนข้อมูล) สามารถส่งคำสั่งยิงไปทำงานที่ฟังก์ชันใน ViewModel ได้โดยตรง
=========================================================================================
*/

using SpendSmart.ViewModels; // นำเข้าพื้นที่ทำงานของ ViewModels เพื่อเรียกใช้งาน BackupViewModel

namespace SpendSmart.Views
{
    // กำหนดให้เป็น partial class เพื่อให้ระบบนำโค้ดส่วนนี้ไปประกอบรวมร่างกับหน้าจอแสดงผล XAML ตอนคอมไพล์แอป
    public partial class BackupPage : ContentPage
    {
        /// <summary>
        /// Constructor: ฟังก์ชันเริ่มต้นทำงานของหน้าจอสำรองข้อมูล
        /// มีการใช้ระบบ Dependency Injection (DI) ในการส่ง BackupViewModel (สมองกลของหน้านี้) เข้ามาให้โดยอัตโนมัติ
        /// </summary>
        public BackupPage(BackupViewModel viewModel)
        {
            InitializeComponent(); // คำสั่งบังคับของ .NET MAUI เพื่อสั่งให้ระบบทำการวาดชิ้นส่วน UI ทั้งหมดจากไฟล์ XAML

            // 🌟 จุดสำคัญ: ทำการผูกสายข้อมูล BindingContext เข้ากับ ViewModel ที่ส่งเข้ามา
            // บรรทัดนี้จะทำให้พวกปุ่มกด (Buttons) หรือข้อความแจ้งสถานะ (Labels) ในหน้า XAML 
            // รู้จักและสามารถเชื่อมโยงค่าดึงข้อมูลจาก BackupViewModel มาแสดงผลหรือสั่งงานได้อย่างถูกต้อง
            BindingContext = viewModel;
        }
    }
}