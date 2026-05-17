/*
=========================================================================================
[สรุปภาพรวมการทำงานของคลาส ChatPage]
ไฟล์นี้คือ "โค้ดเบื้องหลังหน้าจอ UI (Code-Behind)" สำหรับหน้าจอสนทนาอัจฉริยะ (AI Chat Screen)
ตามสถาปัตยกรรม MVVM หน้าจอนี้ทำหน้าที่สำคัญ 3 อย่างคือ:

1. รับระบบแชทเข้ามาทำงาน (Dependency Injection): รับคลาส ChatViewModel เข้ามาทาง Constructor
2. ผูกสายข้อมูลแชท (Data Binding): กำหนด BindingContext เพื่อเชื่อมโยงกล่องข้อความ ปุ่มส่งแชท และรายการกล่องแชทโต้ตอบใน XAML
3. เตรียมข้อมูลหลังบ้านให้ AI (Lifecycle Trigger): ดักจับจังหวะ OnAppearing เพื่อสั่งให้โหลดบริบทการเงินไปรอไว้
=========================================================================================
*/

using SpendSmart.ViewModels; // นำเข้าพื้นที่ทำงานของ ViewModels เพื่อดึง ChatViewModel มาใช้งาน

namespace SpendSmart.Views
{
    // กำหนดเป็น partial class เพื่อรวมร่างเข้ากับส่วนหน้าตา UI ที่ดีไซน์ไว้ในไฟล์ ChatPage.xaml ตอนคอมไพล์แอป
    public partial class ChatPage : ContentPage
    {
        // สร้างตัวแปรภายในคลาสสำหรับเก็บตัวสมองกลควบคุมระบบแชท (ChatViewModel)
        private readonly ChatViewModel _viewModel;

        /// <summary>
        /// Constructor: ฟังก์ชันเริ่มต้นทำงานเมื่อมีการเปิดเข้าสู่หน้าจอแชท AI
        /// มีการใช้ระบบ Dependency Injection (DI) ส่งคลาส ChatViewModel เข้ามาให้ใช้งานอัตโนมัติ
        /// </summary>
        public ChatPage(ChatViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;

            // 🌟 จุดสลักสำคัญ: ผูก BindingContext ของหน้าจอนี้เข้ากับสมองกล ViewModel
            // บรรทัดนี้จะทำให้องค์ประกอบในหน้า XAML (เช่น รายการฟองสบู่ข้อความแชทโต้ตอบซ้าย-ขวา) 
            // สามารถดึงข้อมูลข้อความมาวาดแสดงผลบนหน้าจอมือถือได้อย่างถูกต้องเรียลไทม์
            BindingContext = _viewModel;
        }

        /// <summary>
        /// ฟังก์ชันวงจรชีวิตหน้าจอ (Lifecycle Method): 
        /// จะถูกเรียกทำงานโดยอัตโนมัติ "ทุกครั้ง" ที่หน้าแชทนี้กำลังจะโผล่ขึ้นมาโชว์บนหน้าจอมือถือของผู้ใช้
        /// </summary>
        protected override async void OnAppearing()
        {
            base.OnAppearing(); // สั่งให้ลอจิกตั้งต้นของ ContentPage ทำงานตามขั้นตอนปกติของระบบก่อน

            // 🚀 จังหวะที่หน้าแชทเปิดขึ้นมาปุ๊บ สั่งงานแบบ Asynchronous ทันที (ไม่หน่วงหน้าจอ)
            // ให้ ViewModel วิ่งไปโหลดบริบทข้อมูลการเงิน (ยอดเงินคงเหลือล่าสุด, สรุปรายจ่ายแต่ละหมวดในเดือนนี้) 
            // มารอเตรียมพร้อมไว้ในระบบหลังบ้าน เพื่อให้ AI รู้สถานะการเงินจริงของเราตั้งแต่เริ่มบทสนทนาครับ
            await _viewModel.LoadContextAsync();
        }
    }
}