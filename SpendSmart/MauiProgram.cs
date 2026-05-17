/*
=========================================================================================
[สรุปภาพรวมการทำงานของไฟล์ MauiProgram.cs]
ไฟล์นี้คือ "หัวใจการเริ่มต้นระบบ (App Bootstrapper & DI Container)" ของแอปพลิเคชัน SpendSmart
ทำหน้าที่เป็นด่านแรกสุดที่ระบบปฏิบัติการ (Android/iOS/Windows) จะวิ่งเข้ามาเรียกใช้งานเมื่อเปิดแอป

หน้าที่สำคัญที่สุดของไฟล์นี้คือระบบ "Inversion of Control (IoC)" หรือ "Dependency Injection (DI)"
ซึ่งเป็นการลงทะเบียนจับคู่โครงสร้าง 3 ชั้นหลัก (Services, ViewModels, Views) เอาไว้ในระบบส่วนกลาง
เพื่อควบคุม "วงจรชีวิต (Lifespan)" ของออบเจกต์ในแอป โดยแบ่งการบริหารจัดการหน่วยความจำออกเป็น 2 รูปแบบหลัก:

1. AddSingleton (สร้างครั้งเดียวใช้ร่วมกัน): เหมาะกับ Services แกนหลักที่ต้องเปิดท่อค้างไว้ตลอดเวลา 
   และกรณีพิเศษอย่างหน้าจอแชท (Chat) เพื่อป้องกันข้อความสนทนาหายเวลากดสลับแท็บเมนูไปมา
2. AddTransient (สร้างใหม่ทุกครั้งที่เรียก): เหมาะกับหน้าจอทั่วไป (เช่น หน้าเพิ่มรายการ, หน้าประวัติ)
   ที่จะสร้างขึ้นมาเฉพาะตอนเปิดดู และทำลายทิ้งเพื่อคืนแรม (RAM) ทันทีเมื่อผู้ใช้ปิดหน้านั้นไป
=========================================================================================
*/

using Microsoft.Extensions.Logging;
using SpendSmart.ViewModels;
using SpendSmart.Views;

namespace SpendSmart
{
    // กำหนดเป็นคลาสสแตติก (static) เพื่อให้ระบบเรียกใช้ฟังก์ชัน CreateMauiApp ได้ทันทีโดยไม่ต้องสั่ง new
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // 🚀 1. เริ่มต้นสร้างตัวประกอบร่างแอปพลิเคชัน (.NET MAUI App Builder)
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>() // ระบุคลาส App.xaml ให้เป็นโครงร่างหลักของแอป
                .ConfigureFonts(fonts =>
                {
                    // ลงทะเบียนฟอนต์ส่วนกลางสำหรับดึงไปใช้แต่งหน้าจอ UI
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // 🛠️ 2. ลอจิกเปิดระบบบันทึกข้อผิดพลาด (Conditional Compilation)
            // คำสั่ง #if DEBUG จะทำงานเฉพาะตอนที่เรากำลังต่อสายรันระบบทดสอบระบบบนเครื่องคอมพิวเตอร์เท่านั้น
            // ข้อดี: ระบบจะแอดเครื่องมือดีบั๊กยิง Log บั๊กให้ แต่เมื่อไหร่ที่บีบอัดไฟล์ส่งออกใช้งานจริง (Release) 
            // โค้ดแถวนี้จะถูกตัดทิ้งอัตโนมัติ เพื่อความปลอดภัยและรีดความเร็วเครื่องสูงสุด
#if DEBUG
            builder.Logging.AddDebug();
#endif

            // ==========================================================================
            // 🌟 3. กลุ่มลงทะเบียนระบบบริการหลังบ้าน [Services]
            // ลงทะเบียนแบบ "AddSingleton" เพื่อให้สร้างตัวแปรนี้แค่ตัวเดียว (Instance) ตลอดอายุแอป
            // ไม่ว่าจะเรียกจากหน้าไหน จะได้ต่อท่อใช้งานผ่านดาต้าและไอพีเซิร์ฟเวอร์เดียวกันทั้งหมด
            // ==========================================================================
            builder.Services.AddSingleton<SpendSmart.Services.DatabaseService>();          // ระบบควบคุมฐานข้อมูล SQLite
            builder.Services.AddSingleton<SpendSmart.Services.AIReceiptScannerService>();   // ระบบ API สแกนสลิป OCR
            builder.Services.AddSingleton<SpendSmart.Services.AIFinancialAdvisorService>(); // ระบบ API แชทและวิเคราะห์เงิน
            builder.Services.AddSingleton<SpendSmart.Services.AIVoiceScannerService>();      // ระบบ API ถอดรหัสเสียงพูด

            // ==========================================================================
            // 🌟 4. กลุ่มลงทะเบียนระบบสมองกลคำนวณหน้าจอ [ViewModels]
            // ==========================================================================

            // ใช้ AddTransient: สร้างใหม่แกะกล่องทุกครั้งที่จิ้มเปิดหน้านั้น ยอดเงินและสเตตัสจะได้สดใหม่เสมอ
            builder.Services.AddTransient<SpendSmart.ViewModels.MainViewModel>();
            builder.Services.AddTransient<SpendSmart.ViewModels.AddTransactionViewModel>();
            builder.Services.AddTransient<SpendSmart.ViewModels.ManagePocketsViewModel>();
            builder.Services.AddTransient<SpendSmart.ViewModels.HistoryViewModel>();
            builder.Services.AddTransient<SpendSmart.ViewModels.LoginViewModel>();
            builder.Services.AddTransient<SpendSmart.ViewModels.DashboardViewModel>();
            builder.Services.AddTransient<BackupViewModel>();

            // ⚠️ จุดสังเกตพิเศษ: ChatViewModel ต้องลงทะเบียนเป็น "AddSingleton" !!
            // ลอจิก: บังคับให้ระบบจำคลาสแชทนี้ไว้ถาวรตลอดการเปิดแอป เพื่อให้ลิสต์ข้อความที่คุยกับ AI 
            // ไม่ถูกลบทิ้งหรือรีเซ็ตเป็นค่าว่างเปล่า เมื่อผู้ใช้กดสลับแท็บบาร์ด้านล่างหนีไปหน้าอื่น
            builder.Services.AddSingleton<SpendSmart.ViewModels.ChatViewModel>();

            // ==========================================================================
            // 🌟 5. กลุ่มลงทะเบียนหน้าจอแสดงผล [Views / Pages]
            // โครงสร้างการลงทะเบียน Lifecycle (วงจรชีวิต) ต้องล้อลื่นไหลตามฝั่ง ViewModel ด้านบนเป๊ะๆ
            // ==========================================================================
            builder.Services.AddTransient<SpendSmart.Views.MainPage>();
            builder.Services.AddTransient<SpendSmart.Views.AddTransactionPage>();
            builder.Services.AddTransient<SpendSmart.Views.ManagePocketsPage>();
            builder.Services.AddTransient<SpendSmart.Views.HistoryPage>();
            builder.Services.AddTransient<SpendSmart.Views.LoginPage>();
            builder.Services.AddTransient<SpendSmart.Views.DashboardPage>();
            builder.Services.AddTransient<BackupPage>();

            // หน้าจอแชทลงทะเบียนเป็น "AddSingleton" เพื่อให้สอดคล้องกับ ChatViewModel และเก็บบันทึกสถานะหน้าจอเดิมไว้
            builder.Services.AddSingleton<SpendSmart.Views.ChatPage>();

            // ==========================================================================
            // 🌟 6. ระบบลงทะเบียนปลั๊กอินฮาร์ดแวร์ภายนอก (External Audio Native Plugin)
            // สั่งลงทะเบียนระบบจัดการเสียง AudioManager.Current ของเครื่องสมาร์ทโฟนไว้ที่กล่องส่วนกลาง
            // เพื่อเปิดทางให้ฝั่งหน้าจอและ Service แชทเสียง สามารถดึงระบบไมโครโฟนอัดเสียงพูดไปใช้งานได้ทันที
            // ==========================================================================
            builder.Services.AddSingleton(Plugin.Maui.Audio.AudioManager.Current);

            // ประกอบร่างชิ้นส่วนสถาปัตยกรรมทั้งหมดเสร็จสิ้น พร้อมปล่อยแอปพลิเคชันทำงาน
            return builder.Build();
        }
    }
}