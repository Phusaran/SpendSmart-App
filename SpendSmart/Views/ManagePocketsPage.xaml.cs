/*
=========================================================================================
[สรุปภาพรวมการทำงานของคลาส ManagePocketsPage]
ไฟล์นี้คือ "โค้ดเบื้องหลังหน้าจอ UI (Code-Behind)" สำหรับหน้าจอจัดการกระเป๋าเงิน (Manage Pockets Screen)
ตามสถาปัตยกรรม MVVM หน้าจอนี้ทำหน้าที่เป็นสะพานเชื่อมต่อหน้าตา XAML เข้ากับ ManagePocketsViewModel 
โดยมีความรับผิดชอบหลัก 3 อย่างคือ:

1. รับระบบจัดการกระเป๋าเข้าทำงาน (Dependency Injection): รับคลาส ManagePocketsViewModel เข้ามาทาง Constructor
2. ผูกสายข้อมูล (Data Binding): กำหนด BindingContext เพื่อให้หน้ารายการ การ์ดกระเป๋า และฟอร์มสร้างกระเป๋าใหม่ 
   ในหน้า XAML ทำงานร่วมกับ ViewModel ได้อย่างสมบูรณ์
3. 🛠️ แก้บั๊กข้ามแพลตฟอร์ม (Platform Workaround): มีฟังก์ชัน OnDragStarting ที่เขียนลอจิกพิเศษแก้บั๊กของระบบ Android 
   เพื่อให้ฟีเจอร์ลากการ์ดกระเป๋าเงินไปปล่อยใส่อีกใบเพื่อ "โยกเงิน" (Drag & Drop) สามารถทำงานได้จริงบนมือถือ Android
4. อัปเดตข้อมูล (Lifecycle Trigger): ดักจับจังหวะ OnAppearing เพื่อสั่งให้รีเฟรชรายชื่อกระเป๋าเงินล่าสุดจาก SQLite ทุกครั้งที่เปิดหน้านี้
=========================================================================================
*/

using SpendSmart.ViewModels; // นำเข้าพื้นที่ทำงานของ ViewModels เพื่อดึง ManagePocketsViewModel มาใช้งาน

namespace SpendSmart.Views
{
    // กำหนดให้เป็น partial class เพื่อให้ระบบนำโค้ดส่วนนี้ไปประกอบรวมร่างกับหน้าจอแสดงผล XAML ตอนคอมไพล์แอป
    public partial class ManagePocketsPage : ContentPage
    {
        // สร้างตัวแปรภายในคลาสเพื่อเก็บสมองกลควบคุมระบบกระเป๋าเงิน (ManagePocketsViewModel)
        private readonly ManagePocketsViewModel _viewModel;

        /// <summary>
        /// Constructor: ฟังก์ชันเริ่มต้นทำงานเมื่อแอปพลิเคชันเปิดเข้าสู่หน้าจอจัดการกระเป๋าเงิน
        /// มีการใช้ระบบ Dependency Injection (DI) ในการส่ง ManagePocketsViewModel เข้ามาให้ใช้งานอัตโนมัติ
        /// </summary>
        public ManagePocketsPage(ManagePocketsViewModel viewModel)
        {
            InitializeComponent(); // คำสั่งบังคับของระบบเพื่อวาดและจัดเตรียมชิ้นส่วน UI ทั้งหมด (เช่น ฟอร์มกรอกข้อมูล, รายการกระเป๋า) จาก XAML

            _viewModel = viewModel; // นำระบบจัดการกระเป๋าที่ได้มา เก็บไว้ในตัวแปรประจำคลาสเพื่อเปิดใช้คำสั่งภายใน

            // 🌟 จุดสลักสำคัญ: ผูก BindingContext ของหน้าจอนี้เข้ากับสมองกล ViewModel
            // บรรทัดนี้จะทำให้องค์ประกอบในหน้า XAML (เช่น รายชื่อกระเป๋าเงิน หรือช่องกรอกเป้าหมายรายเดือน) สามารถเชื่อมโยงข้อมูลกันได้ตรงๆ
            BindingContext = _viewModel;
        }

        /// <summary>
        /// 🛠️ [ฟังก์ชันพิเศษ: แก้บั๊กระบบ Drag & Drop บน Android]
        /// จะทำงานทันทีเมื่อผู้ใช้เริ่มเอานิ้วจิ้มที่การ์ดกระเป๋าเงินแล้วขยับเพื่อเตรียมลาก
        /// </summary>
        private void OnDragStarting(object sender, DragStartingEventArgs e)
        {
            // 🐛 อธิบายบั๊ก: ในระบบ .NET MAUI เวอร์ชันปัจจุบัน ฟีเจอร์ DragGestureRecognizer บน Android 
            // จะไม่ยอมทำงานหรือส่งข้อมูลข้ามคอนโทรลเลย หากในแพ็กเกจข้อมูล (Data Package) ไม่มี Property อะไรฝังอยู่เลย
            // 
            // 💡 วิธีแก้: เราจึงต้องแอบยัดข้อมูลสมมติ (Dummy Data) เข้าไปใน Properties เป็นคีย์-ค่าสั้นๆ 1 ชิ้น
            // เพื่อหลอกให้ระบบ Android ยอมเปิดใจเปิดทางให้เกิดสถานะการลาก (Drag) และยอมให้ปล่อยคำสั่ง Drop โยกเงินได้สำเร็จครับ
            e.Data.Properties.Add("FixAndroidBug", "Yes");
        }

        /// <summary>
        /// ฟังก์ชันวงจรชีวิตหน้าจอ (Lifecycle Method): 
        /// จะถูกเรียกทำงานโดยอัตโนมัติ "ทุกครั้ง" ที่หน้าจัดการกระเป๋านี้กำลังจะโผล่ขึ้นมาแสดงผลบนจอมือถือผู้ใช้
        /// </summary>
        protected override async void OnAppearing()
        {
            base.OnAppearing(); // สั่งให้โค้ดเริ่มต้นของระบบ ContentPage ทำงานตามปกติก่อน

            // 🚀 จังหวะที่หน้าเปิดปุ๊บ สั่งงานแบบ Asynchronous ทันที (ไม่หน่วงหน้าจอ UI)
            // ให้ ViewModel วิ่งไปโหลดรายชื่อกระเป๋าเงินทั้งหมดรวมถึงยอดเงินคงเหลือล่าสุดจากฐานข้อมูล SQLite มารายงานบนหน้าจอใหม่
            // ป้องกันเคสที่ผู้ใช้เพิ่งไปกดแชทคุยกับ AI เพื่อบันทึกรายจ่าย หรือเพิ่งไปกดลบรายการในหน้าประวัติมา ยอดเงินจะได้อัปเดตตรงเป๊ะครับ
            await _viewModel.LoadPocketsAsync();
        }
    }
}