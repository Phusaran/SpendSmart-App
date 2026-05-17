/*
=========================================================================================
[สรุปภาพรวมการทำงานของคลาส AppShell]
ไฟล์นี้คือ "โครงสร้างแผนผังนำทางหลัก (Navigation & Shell Structure)" ของแอปพลิเคชัน SpendSmart
ทำหน้าที่เป็นกระดูกสันหลังคุมเส้นทางการเปลี่ยนหน้าจอ (Routing) และแท็บบาร์ด้านล่าง (Tab Bar) 
โดยมีความสามารถพิเศษที่ถูกออกแบบมาอย่างแยบคาย 3 อย่างคือ:

1. ลงทะเบียนเส้นทางเดินรถ (Route Registration): บอกแอปพลิเคชันให้รู้จักชื่อคีย์เวิร์ดของหน้าต่างย่อยต่างๆ 
   (เช่น AddTransactionPage, ChatPage) เพื่อให้ทุก ViewModel สามารถใช้คำสั่ง Shell.Current.GoToAsync นำทางได้จากทั่วทุกจุด
2. ระบบดักจับกล่องของขวัญเรียงตามเลเวล (Dynamic Reward Notification): ดึงคะแนนสะสมแบบด่วนจาก Preferences 
   มาเช็กเงื่อนไขขั้นบันได (30 EXP สำหรับเปิดระบบแชท AI / 50 EXP สำหรับระบบวิเคราะห์รายเดือน) 
   หากมีสิทธิ์แต่ยังไม่กดรับ แอปจะเปลี่ยนหน้าตาแท็บวิเคราะห์จากรูปกราฟ 📊 ให้กลายเป็นกล่องของขวัญ 🎁 สีแดงทันทีเพื่อดึงดูดสายตาผู้ใช้
3. ระบบจัดระเบียบหน้าต่างไม่ให้รกเครื่อง (Smart Stack Cleanup): ดักจับทุกครั้งที่มีการเปลี่ยนแท็บเมนูหลัก 
   ระบบจะสั่งล้างไส้ในหน้าต่างย่อยที่เคยเปิดค้างไว้ให้ถอยกลับมาที่หน้าแรกสุด (PopToRootAsync) เสมอ 
   ช่วยป้องกันอาการหน่วยความจำบวม (Memory Leak) และป้องกันผู้ใช้สับสนหน้าจอ
=========================================================================================
*/

using SpendSmart.Views;

namespace SpendSmart
{
    // สืบทอดคุณสมบัติมาจากคลาส Shell ซึ่งเป็นโครงสร้างนำทางมาตรฐานยุคใหม่ของ .NET MAUI
    public partial class AppShell : Shell
    {
        /// <summary>
        /// Constructor: ฟังก์ชันเริ่มต้นระบบผังโครงสร้างแอป ทำงานตอนเปิดแอปพลิเคชัน
        /// </summary>
        public AppShell()
        {
            InitializeComponent(); // สั่งให้วาดโครงสร้างแท็บบาร์และหน้าตาเมนูหลักจากไฟล์ AppShell.xaml

            // 🌟 1. การลงทะเบียนรหัสเส้นทาง (Register Routes) 
            // เป็นการผูกคีย์เวิร์ดข้อความ เข้ากับคลาสหน้า View จริงๆ เพื่อให้ระบบ GoToAsync รู้จักตำแหน่งนำทาง
            Routing.RegisterRoute("AddTransactionPage", typeof(AddTransactionPage));
            Routing.RegisterRoute("ManagePocketsPage", typeof(ManagePocketsPage));
            Routing.RegisterRoute("ChatPage", typeof(ChatPage));

            // ใช้คำสั่ง nameof(BackupPage) แทนการพิมพ์ข้อความตรงๆ เพื่อความปลอดภัย (ป้องกันพิมพ์ชื่อคลาสผิดพลาด)
            Routing.RegisterRoute(nameof(BackupPage), typeof(BackupPage));

            // 🌟 2. ลงทะเบียนดักฟังอีเวนต์เมื่อผู้ใช้เคลื่อนย้ายเปลี่ยนหน้าจอ (Navigation Event Subscription)
            // เมื่อไหร่ก็ตามที่มีการเปลี่ยนหน้าหรือกดสลับแท็บ ให้เรียกฟังก์ชัน OnShellNavigated ทำงานควบคู่ไปด้วย
            Navigated += OnShellNavigated;

            // 🌟 3. สั่งคำนวณตรวจสอบสถานะรางวัลครั้งแรกสุดทันทีที่เปิดแอป
            RefreshAnalysisRewardState();
        }

        /// <summary>
        /// [ฟังก์ชันอัปเดตหน้าตาแท็บวิเคราะห์ตามสถานะแต้ม EXP]
        /// ทำหน้าที่เปลี่ยนไอคอนและชื่อเมนูเป็นกล่องของขวัญ เพื่อเตือนความจำให้ผู้ใช้กดรับฟีเจอร์ใหม่
        /// </summary>
        public void RefreshAnalysisRewardState()
        {
            // ดึงค่าแต้ม EXP ล่าสุดที่แอปทำกระจกเงาสะท้อน (Mirror) เก็บไว้ในคลัง Preferences (อ่านเขียนไวกว่าดึงจาก SQLite ตรงๆ)
            int totalExp = Preferences.Get("TotalExpMirror", 0);

            // ดึงสถานะว่าผู้ใช้เคยกดปุ่มเคลมปลดล็อกฟีเจอร์ AI Chat และระบบวิเคราะห์รายเดือนไปแล้วหรือยัง
            bool hasClaimedAiUnlock = Preferences.Get("HasClaimedAiUnlock", false);
            bool hasClaimedMonthlyUnlock = Preferences.Get("HasClaimedMonthlyAnalysisUnlock", false);

            // 💡 สมการลอจิกเช็กสิทธิ์รางวัลค้างรับ (Unclaimed Reward Checking):
            // สิทธิ์จะเกิดก็ต่อเมื่อ: (แต้มถึง 30 และยังไม่ได้เคลมแชท) หรือ (แต้มถึง 50 และยังไม่ได้เคลมระบบวิเคราะห์)
            bool hasUnclaimedReward =
                (totalExp >= 30 && !hasClaimedAiUnlock) ||
                (totalExp >= 50 && !hasClaimedMonthlyUnlock);

            // 🎁 เงื่อนไขที่ 1: ตรวจพบว่ามีสิทธิ์ของรางวัลที่ยังไม่ได้กดรับ
            if (hasUnclaimedReward)
            {
                // เปลี่ยนข้อความบนแท็บ และแปลงไอคอนเป็นกล่องของขวัญสีแดงกระแทกตา เพื่อกระตุ้นให้กดเข้ามาดู
                AnalysisTab.Title = "🎁 วิเคราะห์";
                AnalysisIcon.Glyph = "🎁";
                AnalysisIcon.Color = Color.FromArgb("#E74C3C"); // สีแดงแจ้งเตือน
            }
            // 📊 เงื่อนไขที่ 2: ไม่มีรางวัลค้างคา (ผู้ใช้กดรับไปหมดแล้ว หรือแต้มเลเวลยังไม่ถึงเกณฑ์)
            else
            {
                // เปลี่ยนรูปแบบเมนูกลับคืนสู่โหมดปกติ เป็นรูปกราฟแท่งสีกรมท่า/ทองหรูหรา
                AnalysisTab.Title = "วิเคราะห์";
                AnalysisIcon.Glyph = "📊";
                AnalysisIcon.Color = Color.FromArgb("#D4AF37"); // สีทองประจำแอป
            }
        }

        /// <summary>
        /// [ฟังก์ชันดักจับทุกจังหวะที่มีการเคลื่อนไหวเปลี่ยนหน้าจอ]
        /// </summary>
        private async void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
        {
            // ทุกครั้งที่ผู้ใช้ก้าวเท้าสลับหน้าจอ ให้สั่งรีเฟรชเช็กกล่องของขวัญใหม่เสมอ (ตัวเลขจะได้อัปเดตเรียลไทม์)
            RefreshAnalysisRewardState();

            // 🐛 ลอจิกเคลียร์ขยะหน้าจอซ้ำซ้อน:
            // ตรวจสอบว่าสาเหตุการขยับหน้าจอ เกิดจากการที่ผู้ใช้ "จิ้มเปลี่ยนแท็บเมนูหลักด้านล่าง" (ShellItemChanged / ShellSectionChanged) ใช่หรือไม่
            if (e.Source == ShellNavigationSource.ShellItemChanged ||
                e.Source == ShellNavigationSource.ShellSectionChanged)
            {
                // เช็กต่อว่าในหน้าต่างแท็บเดิมที่ผู้ใช้เพิ่งเดินจากมา มีการกดเปิดหน้าจอย่อยๆ ซ้อนลึกเข้าไปเกิน 1 ชั้นไหม
                if (Navigation.NavigationStack.Count > 1)
                {
                    // สั่งการล้างไส้ใน (Pop to Root): ถล่มหน้าจอย่อยๆ ที่เปิดซ้อนค้างทิ้งไปให้หมด 
                    // เพื่อให้แท็บนั้นถอยกลับมาสแตนด์บายรอที่หน้าหลักของตัวเองอย่างสะอาดสะอ้าน
                    // (ใส่พารามิเตอร์ false เพื่อปิดอนิเมชั่นว่อนหน้าจอ ให้เคลียร์เบื้องหลังเงียบๆ)
                    await Navigation.PopToRootAsync(false);
                }
            }
        }
    }
}