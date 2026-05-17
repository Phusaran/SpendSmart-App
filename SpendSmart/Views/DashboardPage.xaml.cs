/*
=========================================================================================
[สรุปภาพรวมการทำงานของคลาส DashboardPage]
ไฟล์นี้คือ "โค้ดเบื้องหลังหน้าจอ UI (Code-Behind)" สำหรับหน้าจอวิเคราะห์และแดชบอร์ด (Dashboard Screen)
ทำหน้าที่เป็นสะพานเชื่อมต่อส่วนการแสดงผลกราฟและเลเวลผู้ใช้ที่เขียนด้วย XAML (DashboardPage.xaml)
เข้ากับระบบประมวลผลและการสะสมแต้มที่ควบคุมโดย DashboardViewModel โดยมีหน้าที่หลักคือ:

1. รับระบบแดชบอร์ดเข้ามาทำงาน (Dependency Injection): รับคลาส DashboardViewModel เข้ามาทาง Constructor ตอนเริ่มสร้างหน้าจอ
2. ผูกสายไฟข้อมูล (Data Binding): กำหนด BindingContext เพื่อให้ปุ่มและหลอดพลัง EXP ใน XAML ทำงานร่วมกับ ViewModel ได้
3. ดักฟังวงจรชีวิตหน้าจอเพื่ออัปเดตข้อมูล (Lifecycle Trigger): ดักจับจังหวะที่ผู้ใช้สลับหน้าจอมาที่หน้านี้ (OnAppearing)
   เพื่อสั่งให้ ViewModel โหลดข้อมูลการเงินล่าสุดของเดือนนี้และรีเฟรชระดับเลเวลแรงก์ใหม่ทันที ตัวเลขและกราฟบนหน้าจอจึงสดใหม่เสมอ
=========================================================================================
*/

using SpendSmart.ViewModels; // นำเข้าพื้นที่ทำงานของ ViewModels เพื่อดึง DashboardViewModel มาใช้งาน

namespace SpendSmart.Views
{
    // กำหนดให้เป็น partial class เพื่อให้ระบบนำโค้ดส่วนนี้ไปประกอบรวมร่างกับหน้าหน้าจอแสดงผล XAML (DashboardPage.xaml) ตอนรันแอป
    public partial class DashboardPage : ContentPage
    {
        // สร้างตัวแปรภายในสำหรับเก็บตัว DashboardViewModel (สมองกลของหน้าวิเคราะห์นี้)
        private readonly DashboardViewModel _viewModel;

        /// <summary>
        /// Constructor: ฟังก์ชันเริ่มต้นทำงานเมื่อแอปพลิเคชันกำลังจะเปิดเข้าสู่หน้าแดชบอร์ด
        /// มีการใช้ระบบ Dependency Injection (DI) ส่งตัว DashboardViewModel เข้ามาให้ใช้งานอัตโนมัติ
        /// </summary>
        public DashboardPage(DashboardViewModel viewModel)
        {
            InitializeComponent(); // คำสั่งบังคับเพื่อวาดและจัดเตรียมองค์ประกอบหน้าจอทั้งหมด (เช่น กราฟวงกลม, ปุ่มรับรางวัล) จาก XAML

            _viewModel = viewModel; // นำระบบแดชบอร์ดที่ได้มา เก็บไว้ในตัวแปรประจำคลาสเพื่อเปิดใช้คำสั่งภายใน

            // 🌟 จุดสลักสำคัญ: ผูก BindingContext ของหน้าจอนี้เข้ากับสมองกล ViewModel
            // บรรทัดนี้จะทำให้องค์ประกอบในหน้า XAML สามารถดึงยอดเงินรวม, ตัวเลขเลเวล และคำแนะนำจาก AI มาโชว์ได้ตรงๆ
            BindingContext = _viewModel; // เชื่อมหน้าจอเข้ากับข้อมูล
        }

        /// <summary>
        /// ฟังก์ชันวงจรชีวิตหน้าจอ (Lifecycle Method): 
        /// จะถูกเรียกทำงานโดยอัตโนมัติ "ทุกครั้ง" ที่ผู้ใช้กดแท็บสลับกลับมาที่หน้าแดชบอร์ด/วิเคราะห์นี้
        /// </summary>
        protected override async void OnAppearing()
        {
            base.OnAppearing(); // สั่งให้โค้ดเริ่มต้นของระบบ ContentPage ทำงานตามปกติก่อน

            // 🚀 จังหวะที่ผู้ใช้เปิดหน้านี้ขึ้นมา สั่งงานแบบ Asynchronous (ไม่หน่วง UI)
            // ให้ ViewModel วิ่งไปกวาดข้อมูลรายจ่ายเดือนปัจจุบันมาคำนวณใหม่ และอัปเดตหลอดแต้ม EXP ล่าสุดทันที
            // ช่วยให้กราฟวงกลมและสถานะการปลดล็อกปุ่ม AI เป็นเลขที่ถูกต้องเรียลไทม์เสมอครับ
            await _viewModel.LoadDashboardDataAsync();
        }
    }
}