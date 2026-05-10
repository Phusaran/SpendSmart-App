using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpendSmart.Models;
using SpendSmart.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace SpendSmart.ViewModels
{
    public partial class ChatViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly AIFinancialAdvisorService _aiAdvisor;

        public ObservableCollection<ChatMessage> Messages { get; set; } = new();

        [ObservableProperty] private string _inputText;
        [ObservableProperty] private bool _isTyping;

        private string _financialContext = "";

        public ChatViewModel(DatabaseService databaseService, AIFinancialAdvisorService aiAdvisor)
        {
            _databaseService = databaseService;
            _aiAdvisor = aiAdvisor;

            // ข้อความต้อนรับ
            Messages.Add(new ChatMessage { Text = "สวัสดีครับ! มีอะไรให้ SpendSmart AI ช่วยเรื่องการเงินหรือวิเคราะห์ค่าใช้จ่ายไหมครับ? 😊", IsUser = false });
        }

        // ดึงข้อมูลสถานะการเงินแบบละเอียด (รวมวันที่ปัจจุบันและเป้าหมาย) ส่งให้ AI
        public async Task LoadContextAsync()
        {
            // 🌟 1. ระบุวันที่ปัจจุบันให้ AI รู้ (เพื่อให้มันรู้ว่าตอนนี้อยู่ช่วงต้นเดือนหรือปลายเดือน)
            string fullContext = $"[📅 ข้อมูลวันที่ปัจจุบัน: {DateTime.Now:dd MMMM yyyy}]\n\n";

            // ----------------------------------------------------
            // 2. ดึงข้อมูล "กระเป๋าเงิน (Pockets)" พร้อมเดือนเป้าหมาย
            // ----------------------------------------------------
            var pockets = await _databaseService.GetPocketsAsync();
            fullContext += "[💰 สถานะกระเป๋าเงินและเป้าหมายออมเงิน]\n";

            if (pockets.Count > 0)
            {
                foreach (var p in pockets)
                {
                    if (p.PocketType == "Saving")
                    {
                        // 🌟 คำนวณเดือนเป้าหมาย (ถ้ามี)
                        string targetMonthInfo = p.TargetDate.HasValue
                            ? $"เป้าหมายเดือน: {p.TargetDate.Value:MMMM yyyy}"
                            : "ไม่ระบุเดือนเป้าหมาย";

                        fullContext += $"- {p.Name}: เหลือ {p.CurrentBalance:N0} บาท (เป้า: {p.GoalAmount:N0} บาท, {targetMonthInfo})\n";
                    }
                    else
                    {
                        fullContext += $"- {p.Name}: มีเงินเหลือ {p.CurrentBalance:N0} บาท (กระเป๋าใช้จ่ายทั่วไป)\n";
                    }
                }
            }
            else
            {
                fullContext += "- ยังไม่ได้สร้างกระเป๋าเงิน\n";
            }

            fullContext += "\n";

            // ----------------------------------------------------
            // 3. ดึงข้อมูล "รายจ่าย" ในเดือนปัจจุบัน
            // ----------------------------------------------------
            var transactions = await _databaseService.GetTransactionsAsync();
            var currentMonthExpenses = transactions
                .Where(t => t.Type == "Expense" && t.Date.Month == DateTime.Now.Month && t.Date.Year == DateTime.Now.Year)
                .ToList();

            var totalExpense = currentMonthExpenses.Sum(t => t.Amount);
            fullContext += $"[💸 สรุปรายจ่ายเดือนนี้ - รวม {totalExpense:N0} บาท]\n";

            var groupedCategories = currentMonthExpenses
                .GroupBy(t => string.IsNullOrWhiteSpace(t.SubCategory) ? "ทั่วไป" : t.SubCategory)
                .Select(g => new { CategoryName = g.Key, Amount = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.Amount)
                .ToList();

            if (groupedCategories.Count > 0)
            {
                foreach (var item in groupedCategories)
                {
                    fullContext += $"- หมวด {item.CategoryName}: {item.Amount:N0} บาท\n";
                }
            }
            else
            {
                fullContext += "- ยังไม่มีข้อมูลการใช้จ่ายในเดือนนี้\n";
            }

            // 🌟 4. เก็บข้อมูลทั้งหมดไว้ส่งให้ AI
            _financialContext = fullContext;
        }

        [RelayCommand]
        public async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(InputText)) return;

            // 1. โชว์ข้อความที่เราพิมพ์
            var userMsg = InputText;
            Messages.Add(new ChatMessage { Text = userMsg, IsUser = true });
            InputText = string.Empty;

            // 2. ขึ้นสถานะว่า AI กำลังพิมพ์
            IsTyping = true;

            // 3. ส่งไปให้ AI ประมวลผล
            string reply = await _aiAdvisor.SendChatMessageAsync(userMsg, _financialContext);

            // 4. โชว์ข้อความที่ AI ตอบกลับ
            IsTyping = false;
            Messages.Add(new ChatMessage { Text = reply, IsUser = false });
        }
    }
}