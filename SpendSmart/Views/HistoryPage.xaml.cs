/*
=========================================================================================
[สรุปภาพรวมการทำงานของคลาส HistoryPage]
ไฟล์นี้คือ "โค้ดเบื้องหลังหน้าจอ UI (Code-Behind)" สำหรับหน้าจอประวัติธุรกรรม (Transaction History Screen)
ทำหน้าที่เป็นสะพานเชื่อมโยงหน้ารายการบันทึกที่เขียนด้วย XAML (HistoryPage.xaml) 
เข้ากับระบบคัดกรองข้อมูลและการสั่งลบรายการของ HistoryViewModel โดยมีหน้าที่หลัก 3 อย่างคือ:

1. รับระบบจัดการประวัติเข้าทำงาน (Dependency Injection): รับคลาส HistoryViewModel เข้ามาผ่าน Constructor ตอนเปิดหน้า
2. ผูกสายไฟข้อมูล (Data Binding): กำหนด BindingContext เพื่อเชื่อมโยงปุ่มกดลบรายการ (Undo) และลิสต์รายการประวัติ
   ในหน้า XAML ให้ทำงานสัมพันธ์กับข้อมูลฝั่ง ViewModel
3. ดักฟังวงจรชีวิตเพื่อรีเฟรชรายการ (Lifecycle Trigger): ดักจับจังหวะที่หน้าจอประวัติกำลังจะเปิดขึ้นมาโชว์บนหน้าจอมือถือ (OnAppearing)
   เพื่อสั่งให้ ViewModel โหลดข้อมูลประวัติชุดล่าสุดจากฐานข้อมูล SQLite ขึ้นมาแสดงผลใหม่ทันที
=========================================================================================
*/

using SpendSmart.ViewModels; // นำเข้าพื้นที่ทำงานของ ViewModels เพื่อดึง HistoryViewModel มาใช้งาน

namespace SpendSmart.Views
{
    // กำหนดให้เป็น partial class เพื่อให้ระบบนำโค้ดส่วนนี้ไปประกอบรวมร่างกับหน้าจอแสดงผล XAML ตอนคอมไพล์แอป
    public partial class HistoryPage : ContentPage
    {
        // สร้างตัวแปรภายในคลาสเพื่อเก็บสมองกลควบคุมระบบประวัติ (HistoryViewModel)
        private readonly HistoryViewModel _viewModel;

        /// <summary>
        /// Constructor: ฟังก์ชันเริ่มต้นทำงานเมื่อแอปพลิเคชันเปิดเข้าสู่หน้าจอประวัติธุรกรรม
        /// มีการใช้ระบบ Dependency Injection (DI) ในการส่ง HistoryViewModel เข้ามาให้ใช้งานอัตโนมัติ
        /// </summary>
        public HistoryPage(HistoryViewModel viewModel)
        {
            InitializeComponent(); // คำสั่งบังคับของ .NET MAUI เพื่อสั่งให้ระบบวาดชิ้นส่วน UI ทั้งหมด (เช่น CollectionView) จาก XAML

            _viewModel = viewModel; // นำระบบประวัติที่ได้มา เก็บไว้ในตัวแปรประจำคลาสเพื่อเปิดใช้คำสั่งภายใน

            // 🌟 จุดสลักสำคัญ: ผูก BindingContext ของหน้าจอนี้เข้ากับสมองกล ViewModel
            // บรรทัดนี้จะทำให้องค์ประกอบในหน้า XAML (เช่น รายการประวัติ หรือปุ่มสั่งลบรายการ) สามารถดึงข้อมูลมาแสดงผลได้ตรงๆ
            BindingContext = _viewModel;
        }

        /// <summary>
        /// ฟังก์ชันวงจรชีวิตหน้าจอ (Lifecycle Method): 
        /// จะถูกเรียกทำงานโดยอัตโนมัติ "ทุกครั้ง" ที่หน้าประวัตินี้กำลังจะโผล่ขึ้นมาแสดงผลบนหน้าจอมือถือผู้ใช้
        /// </summary>
        protected override async void OnAppearing()
        {
            base.OnAppearing(); // สั่งให้โค้ดเริ่มต้นของระบบ ContentPage ทำงานตามปกติก่อน

            //  จังหวะที่หน้าเปิด สั่งงานแบบ Asynchronous ทันที (ไม่หน่วงหน้าจอ UI)
            // ให้ ViewModel วิ่งไปโหลดประวัติการเงินทั้งหมดจากฐานข้อมูล SQLite ขึ้นมาแสดงผลใหม่
            // ยอดเงินหรือรายการที่เพิ่งถูกเพิ่มเข้ามาใหม่จากหน้าอื่น หรือเคสที่เพิ่งกดลบ Undo ไป จะได้รีเฟรชแสดงผลตรงเป๊ะเรียลไทม์ครับ
            await _viewModel.LoadHistoryAsync();
        }
    }
}