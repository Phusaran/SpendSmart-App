/*
=========================================================================================
[สรุปภาพรวมการทำงานของไฟล์ DashboardViewModel.cs]
ไฟล์นี้คือ "ผู้จัดการหน้าจอวิเคราะห์และแดชบอร์ด" ทำหน้าที่เป็นหน้าต่างหลักที่แสดงภาพรวมของการเงิน
และระบบสะสมแต้ม (Gamification) ภายในแอปพลิเคชัน มีความสามารถหลักๆ 4 ส่วนคือ:

1. สรุปรายจ่ายรายเดือน: ดึงข้อมูลการใช้จ่ายเฉพาะเดือนปัจจุบัน มาคำนวณยอดรวมและจัดกลุ่มตามหมวดหมู่
2. ระบบบัญชีผู้ใช้ (Gamification): ดึงข้อมูลค่าประสบการณ์ (EXP) คำนวณเลเวล และจัดระดับแรงก์ (Iron, Bronze, Silver)
3. ระบบปลดล็อกฟีเจอร์ (Unlock System): ตรวจสอบว่า EXP ถึงเกณฑ์หรือไม่ เพื่อเปิดให้ผู้ใช้กด "รับรางวัล" 
   เพื่อปลดล็อกฟีเจอร์ AI Chat (ใช้ 30 EXP) และ ฟีเจอร์ AI วิเคราะห์รายเดือน (ใช้ 50 EXP)
4. ขอคำแนะนำ AI (Ask AI): รวบรวมข้อมูลยอดเงินและหมวดหมู่ของเดือนนี้ ส่งไปให้ AI วิเคราะห์เพื่อขอคำแนะนำ
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
    // สืบทอด ObservableObject เพื่อให้ตัวแปรสื่อสารและอัปเดตหน้าจอ UI ได้แบบเรียลไทม์
    public partial class DashboardViewModel : ObservableObject
    {
        // 🌟 ตัวแปรเรียกใช้บริการหลังบ้าน (Services)
        private readonly DatabaseService _databaseService; // ติดต่อ SQLite
        private readonly AIFinancialAdvisorService _aiAdvisor; // ติดต่อระบบ AI

        // ==========================================================================
        // 🌟 กลุ่มตัวแปรผูกกับหน้าจอ UI (Properties)
        // ==========================================================================
        [ObservableProperty] private decimal _totalExpense;      // ยอดรวมรายจ่ายเดือนนี้
        [ObservableProperty] private string _aiAdvice;           // ข้อความคำแนะนำจาก AI
        [ObservableProperty] private bool _isAiThinking;         // สถานะตอน AI กำลังคิด (เปิด/ปิด โหลดดิ้ง)

        // กลุ่มตัวแปรระบบเลเวล
        [ObservableProperty] private int _userLevel;             // เลเวลปัจจุบัน
        [ObservableProperty] private double _expProgress;        // หลอดความคืบหน้า EXP (0.0 - 1.0)
        [ObservableProperty] private string _expText;            // ข้อความโชว์ EXP (เช่น "15 / 30")
        [ObservableProperty] private string _rankName;           // ชื่อระดับ (Iron, Bronze, Silver)
        [ObservableProperty] private string _rankColor;          // สีของระดับ (นำไปเปลี่ยนสีข้อความหรือกรอบ)

        // กลุ่มตัวแปรระบบปลดล็อกของรางวัล
        [ObservableProperty] private bool _hasPendingAiReward;       // มีรางวัล AI Chat รอให้กดรับหรือไม่?
        [ObservableProperty] private bool _hasPendingMonthlyReward;  // มีรางวัล Monthly Analysis รอให้กดรับหรือไม่?
        [ObservableProperty] private bool _canUseAiChat;             // ผู้ใช้มีสิทธิ์เข้าหน้าแชท AI หรือยัง?
        [ObservableProperty] private bool _canUseMonthlyAnalysis;    // ผู้ใช้มีสิทธิ์กดปุ่มวิเคราะห์รายเดือนหรือยัง?
        [ObservableProperty] private bool _isAiChatLocked;           // สถานะล็อกปุ่มแชท (เอาไว้โชว์ไอคอนแม่กุญแจ)
        [ObservableProperty] private bool _isMonthlyAnalysisLocked;  // สถานะล็อกปุ่มวิเคราะห์

        // ลิสต์เก็บข้อมูลสรุปแต่ละหมวดหมู่ (เช่น อาหาร 500 บาท, เดินทาง 200 บาท) เพื่อเอาไปโชว์เป็นกราฟหรือ List
        public ObservableCollection<CategorySummary> CategoryData { get; set; } = new();

        // Constructor: ฟังก์ชันเริ่มต้น
        public DashboardViewModel(DatabaseService databaseService, AIFinancialAdvisorService aiAdvisor)
        {
            _databaseService = databaseService;
            _aiAdvisor = aiAdvisor;

            // 🌟 ลงทะเบียนรับสัญญาณ (Messenger): ถ้าระบบได้รับสัญญาณ TransactionChangedMessage (มีการเพิ่ม/ลบ ข้อมูล)
            // ให้ทำการเรียกฟังก์ชัน LoadDashboardDataAsync() เพื่อรีเฟรชข้อมูลบนหน้าจอนี้ใหม่ทันที
            WeakReferenceMessenger.Default.Register<TransactionChangedMessage>(this, async (recipient, message) =>
            {
                await LoadDashboardDataAsync();
            });
        }

        /// <summary>
        /// [ฟังก์ชันหลัก: โหลดข้อมูลทั้งหมดของหน้าแดชบอร์ด]
        /// </summary>
        public async Task LoadDashboardDataAsync()
        {
            // 1. ดึงข้อมูลรายการธุรกรรมทั้งหมดจากฐานข้อมูล
            var transactions = await _databaseService.GetTransactionsAsync();

            // 2. กรองข้อมูล (Filter) เอาเฉพาะ "รายจ่าย" (Expense) และต้องเป็นของ "เดือนนี้" และ "ปีนี้" เท่านั้น
            var currentMonthExpenses = transactions
                .Where(t => t.Type == "Expense" &&
                            t.Date.Month == DateTime.Now.Month &&
                            t.Date.Year == DateTime.Now.Year)
                .ToList();

            // 3. คำนวณหาผลรวมของรายจ่ายทั้งหมดในเดือนนี้
            TotalExpense = currentMonthExpenses.Sum(x => x.Amount);

            // 4. จัดกลุ่มข้อมูล (Group By) ตามหมวดหมู่ เพื่อหักยอดรวมแต่ละหมวด
            var grouped = currentMonthExpenses
                .GroupBy(t => string.IsNullOrWhiteSpace(t.SubCategory) ? "ทั่วไป" : t.SubCategory)
                .Select(g => new CategorySummary
                {
                    CategoryName = g.Key,                                // ชื่อหมวดหมู่
                    Amount = g.Sum(x => x.Amount),                       // ยอดรวมเงินในหมวดหมู่นี้
                    // คำนวณเปอร์เซ็นต์ (ถ้ายอดรวม>0 ให้นำ ยอดหมวดหารยอดรวม)
                    Percentage = TotalExpense > 0 ? (double)(g.Sum(x => x.Amount) / TotalExpense) : 0
                })
                .OrderByDescending(x => x.Amount) // เรียงลำดับจากจ่ายมากสุด ไปหาน้อยสุด
                .ToList();

            // อัปเดตข้อมูลใส่ List เพื่อนำไปโชว์บนหน้าจอ
            CategoryData.Clear();
            foreach (var item in grouped)
            {
                CategoryData.Add(item);
            }

            // ==========================================
            // 🌟 5. โหลดข้อมูลผู้ใช้ และอัปเดตระบบแรงก์/เลเวล
            // ==========================================
            var profile = await _databaseService.GetUserProfileAsync();

            // เซฟ EXP ไว้ใน Preferences (เก็บค่าลงเครื่อง) เพื่อให้หน้าอื่นดึงไปใช้โชว์ได้ง่ายๆ โดยไม่ต้องเรียก Database ตลอด
            Preferences.Set("TotalExpMirror", profile.TotalExp);

            UserLevel = profile.CurrentLevel;
            ExpProgress = profile.LevelProgress;
            ExpText = profile.ExpText;

            // ตรวจสอบและกำหนดระดับแรงก์ (Rank) ตามช่วง EXP
            if (profile.TotalExp < 30)
            {
                RankName = "Iron";
                RankColor = "#7F8C8D"; // สีเทาเหล็ก
            }
            else if (profile.TotalExp < 50)
            {
                RankName = "Bronze";
                RankColor = "#CD7F32"; // สีทองแดง
            }
            else
            {
                RankName = "Silver";
                RankColor = "#BDC3C7"; // สีเงิน
            }

            // ==========================================
            // 🌟 6. ระบบตรวจสอบและปลดล็อกของรางวัล
            // ==========================================
            // เช็กในระบบมือถือว่าผู้ใช้ เคยกดรับรางวัล ไปแล้วหรือยัง? (ถ้าไม่เคย ค่าเริ่มต้นคือ false)
            bool hasClaimedAiUnlock = Preferences.Get("HasClaimedAiUnlock", false);
            bool hasClaimedMonthlyUnlock = Preferences.Get("HasClaimedMonthlyAnalysisUnlock", false);

            // คำนวณว่า "มีรางวัลรอให้กดรับอยู่ไหม?" (EXP ถึงเกณฑ์แล้ว + แต่ยังไม่เคยกดรับ)
            HasPendingAiReward = profile.TotalExp >= 30 && !hasClaimedAiUnlock;
            HasPendingMonthlyReward = profile.TotalExp >= 50 && !hasClaimedMonthlyUnlock;

            // กำหนดสิทธิ์ว่า "อนุญาตให้เข้าใช้งานฟีเจอร์นี้หรือไม่?" (EXP ต้องถึงเกณฑ์ + และต้องกดรับรางวัลแล้วเท่านั้น)
            CanUseAiChat = profile.TotalExp >= 30 && hasClaimedAiUnlock;
            CanUseMonthlyAnalysis = profile.TotalExp >= 50 && hasClaimedMonthlyUnlock;

            // ตัวแปรสำหรับเอาไปโชว์แม่กุญแจบนหน้าจอ (สลับค่าจาก CanUse...)
            IsAiChatLocked = !CanUseAiChat;
            IsMonthlyAnalysisLocked = !CanUseMonthlyAnalysis;

            // สั่งให้ตัวครอบแอปหลัก (AppShell) อัปเดตไอคอนแจ้งเตือน (จุดแดง) ที่แถบเมนูด้านล่าง
            if (Shell.Current is AppShell appShell)
            {
                appShell.RefreshAnalysisRewardState();
            }
        }

        /// <summary>
        /// [ปุ่มกดรับรางวัล: ปลดล็อกฟีเจอร์ AI Chat] (ต้องใช้ 30 EXP)
        /// </summary>
        [RelayCommand]
        public async Task ClaimAiRewardAsync()
        {
            // บันทึกสถานะลงเครื่องว่า "กดรับรางวัลนี้แล้วนะ"
            Preferences.Set("HasClaimedAiUnlock", true);

            await Shell.Current.DisplayAlert(
                "รับรางวัลสำเร็จ",
                "🎁 ปลดล็อก เริ่มต้นการสนทนา AI แล้ว",
                "ตกลง");

            // รีโหลดหน้าจอเพื่ออัปเดตปุ่มและสถานะแม่กุญแจ
            await LoadDashboardDataAsync();
        }

        /// <summary>
        /// [ปุ่มกดรับรางวัล: ปลดล็อกฟีเจอร์ AI วิเคราะห์รายเดือน] (ต้องใช้ 50 EXP)
        /// </summary>
        [RelayCommand]
        public async Task ClaimMonthlyRewardAsync()
        {
            // บันทึกสถานะลงเครื่องว่า "กดรับรางวัลนี้แล้วนะ"
            Preferences.Set("HasClaimedMonthlyAnalysisUnlock", true);

            await Shell.Current.DisplayAlert(
                "รับรางวัลสำเร็จ",
                "🎁 ปลดล็อก วิเคราะห์ข้อมูลรายเดือน แล้ว",
                "ตกลง");

            // รีโหลดหน้าจอ
            await LoadDashboardDataAsync();
        }

        /// <summary>
        /// [คำสั่งนำทางไปยังหน้า AI Chat]
        /// </summary>
        [RelayCommand]
        public async Task GoToChatAsync()
        {
            // ถ้ายังไม่มีสิทธิ์ (ยังไม่ปลดล็อก) ให้เด้งป๊อปอัพเตือนและบล็อกไม่ให้เข้า
            if (!CanUseAiChat)
            {
                await Shell.Current.DisplayAlert(
                    "ยังไม่ปลดล็อก",
                    "ต้องมี 30 EXP ก่อน แล้วกดรับรางวัล 🎁 ที่หน้า วิเคราะห์",
                    "ตกลง");
                return;
            }

            // ถ้ามีสิทธิ์แล้ว ให้เปิดหน้า ChatPage
            await Shell.Current.GoToAsync("ChatPage");
        }

        /// <summary>
        /// [คำสั่งขอคำปรึกษาจาก AI]
        /// </summary>
        [RelayCommand]
        public async Task AskAiForAdviceAsync()
        {
            // บล็อกการเข้าถึงถ้ายังไม่ปลดล็อกฟีเจอร์
            if (!CanUseMonthlyAnalysis)
            {
                await Shell.Current.DisplayAlert(
                    "ยังไม่ปลดล็อก",
                    "ต้องมี 50 EXP ก่อน แล้วกดรับรางวัล 🎁 ที่หน้า วิเคราะห์",
                    "ตกลง");
                return;
            }

            // ถ้าในเดือนนี้ยังไม่มีรายการใช้จ่ายเลย ให้บอกผู้ใช้ก่อน ไม่ต้องยิงไปหา AI ให้เปลืองโควต้า
            if (CategoryData.Count == 0)
            {
                await App.Current.MainPage.DisplayAlert("แจ้งเตือน", "ยังไม่มีข้อมูลรายจ่ายให้วิเคราะห์ครับ", "ตกลง");
                return;
            }

            // 1. เปิดสถานะ "AI กำลังคิด" เพื่อโชว์หลอดโหลดบนหน้าจอ
            IsAiThinking = true;
            AiAdvice = "AI กำลังวิเคราะห์พฤติกรรมการใช้จ่ายของคุณ...";

            // 2. เรียกบริการยิง API ส่ง "ยอดเงินรวม" และ "หมวดหมู่ทั้งหมด" ไปหา Python AI
            AiAdvice = await _aiAdvisor.GetFinancialAdviceAsync(TotalExpense, CategoryData);

            // 3. ปิดสถานะกำลังคิด (นำคำตอบที่ได้มาแสดงบนหน้าจอ)
            IsAiThinking = false;
        }

        /// <summary>
        /// [คำสั่งนำทางไปยังหน้าสำรอง/กู้คืนข้อมูล (Backup)]
        /// </summary>
        [RelayCommand]
        public async Task GoToBackupAsync()
        {
            await Shell.Current.GoToAsync(nameof(SpendSmart.Views.BackupPage));
        }
    }
}