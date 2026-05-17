/*
=========================================================================================
[สรุปภาพรวมการทำงานของคลาส AddTransactionPage]
ไฟล์นี้คือ "โค้ดเบื้องหลังหน้าจอ UI (Code-Behind)" สำหรับหน้าเพิ่มรายการธุรกรรม (Add Transaction Screen)
ตามสถาปัตยกรรม MVVM หน้าจอนี้จะไม่คำนวณลอจิกการเงินเอง แต่ทำหน้าที่สำคัญ 3 อย่างคือ:

1. รับสมองกลเข้ามาทำงาน (Dependency Injection): รับวัตถุ AddTransactionViewModel เข้ามาผ่าน Constructor
2. ผูกสายไฟข้อมูล (Data Binding): ตั้งค่า BindingContext เพื่อเชื่อมโยงปุ่มและช่องกรอกข้อมูลใน XAML 
   ให้ทำงานตรงกับตัวแปรใน ViewModel
3. ดักฟังวงจรชีวิตหน้าจอ (Lifecycle Trigger): ดักจับจังหวะที่หน้าจอนี้ "กำลังจะเปิดขึ้นมาโชว์บนจอมือถือ" (OnAppearing) 
   เพื่อสั่งให้ ViewModel รีบไปโหลดรายชื่อกระเป๋าเงินล่าสุดมาเตรียมพร้อมให้ผู้ใช้เลือกทันที
=========================================================================================
*/

using SpendSmart.ViewModels; // นำเข้าพื้นที่ทำงานของ ViewModels เพื่อดึง AddTransactionViewModel มาใช้งาน

namespace SpendSmart.Views
{
    // กำหนดให้เป็น partial class เพราะโค้ดส่วนนี้จะถูกนำไปรวมร่างกับไฟล์หน้าตา UI (AddTransactionPage.xaml) ตอนรันแอป
    public partial class AddTransactionPage : ContentPage
    {
        // สร้างตัวแปรภายในแบบอ่านได้อย่างเดียว (readonly) เพื่อเก็บตัว ViewModel (สมองกลของหน้านี้)
        private readonly AddTransactionViewModel _viewModel;

        /// <summary>
        /// Constructor: ฟังก์ชันเริ่มต้นทำงานเมื่อแอปพลิเคชันกำลังจะเปิดหน้าจอนี้
        /// มีการใช้ระบบ Dependency Injection (DI) ส่งตัว AddTransactionViewModel เข้ามาให้โดยอัตโนมัติ
        /// </summary>
        public AddTransactionPage(AddTransactionViewModel viewModel)
        {
            InitializeComponent(); // คำสั่งบังคับของระบบเพื่อวาดและเตรียมชิ้นส่วน UI (ปุ่ม, ช่องกรอก) จากไฟล์ XAML

            _viewModel = viewModel; // นำ ViewModel ที่ระบบส่งมาให้ เก็บไว้ในตัวแปรประจำคลาสเพื่อเรียกใช้งานยาวๆ

            // 🌟 จุดสลักสำคัญ: ทำการผูก BindingContext เข้ากับ ViewModel
            // บรรทัดนี้เปรียบเสมือนการเสียบสายไฟเชื่อมระหว่างปุ่มกดใน XAML ให้วิ่งมาสั่งงานฟังก์ชันในภาษา C# ได้
            BindingContext = _viewModel;
        }

        /// <summary>
        /// ฟังก์ชันวงจรชีวิตหน้าจอ (Lifecycle Method): 
        /// จะถูกเรียกทำงานโดยอัตโนมัติ "ทุกครั้ง" ที่หน้าจอนี้กำลังจะโผล่ขึ้นมาแสดงผลบนหน้าจอมือถือผู้ใช้
        /// </summary>
        protected override async void OnAppearing()
        {
            base.OnAppearing(); // สั่งให้โค้ดเริ่มต้นของระบบ ContentPage ทำงานตามปกติก่อน

            // จังหวะที่หน้าเปิดปุ๊บ สั่งงานแบบ Asynchronous (ไม่หน่วง UI) 
            // ให้ ViewModel วิ่งไปโหลดรายชื่อกระเป๋าเงินล่าสุดมาจาก SQLite ทันที ยอดเงินจะได้อัปเดตตรงเป๊ะตลอดเวลา
            await _viewModel.LoadPocketsAsync();
        }
    }
}