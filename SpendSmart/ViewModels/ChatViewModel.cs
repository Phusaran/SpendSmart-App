/*
=========================================================================================
[สรุปภาพรวมการทำงานของไฟล์ ChatViewModel.cs]
ไฟล์นี้เปรียบเสมือน "สมองกลฝั่งหน้าจอ (ViewModel)" ของระบบแชท AI ภายในแอปพลิเคชัน
มีหน้าที่หลัก 3 ส่วนสำคัญดังนี้:

1. จัดการข้อมูลก่อนคุย (Context Loading): ก่อนที่ผู้ใช้จะคุยกับ AI ไฟล์นี้จะแอบไปกวาดข้อมูล 
   ยอดเงินในกระเป๋าและประวัติรายจ่ายเดือนปัจจุบัน เพื่อแพ็กเป็น "บริบท (Context)" ส่งไปให้ AI รู้จักเราก่อน
2. ตัวกลางสื่อสาร (Chat Handler): รับข้อความที่เราพิมพ์ ส่งไปให้ไฟล์ Service ยิง API ไปหา Python 
   และรอรับคำตอบที่เป็นโครงสร้าง JSON กลับมา
3. ระบบป๊อปอัพอัจฉริยะ (Smart Pop-up UI): ทำหน้าที่แกะกล่อง JSON ที่ AI ส่งมา 
   - ถ้าเป็นข้อความคุยเล่น (success) -> โชว์ในกล่องแชทปกติ
   - ถ้าเป็นการทำธุรกรรม (propose_transaction) -> สั่งเปิดหน้าต่าง Pop-up กลางจอ 
     พร้อมดึงตัวเลข หมวดหมู่ และโน้ต มากรอกรอไว้ให้ผู้ใช้กด "ยืนยัน" เพื่อบันทึกลงฐานข้อมูลจริง
=========================================================================================
*/

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // เครื่องมือสำหรับส่งสัญญาณวิทยุข้ามหน้าจอ (เช่น สั่งให้หน้าอื่นรีเฟรช)
using SpendSmart.Messages;             // ไฟล์เก็บรูปแบบสัญญาณ (TransactionChangedMessage)
using SpendSmart.Models;               // โครงสร้างข้อมูล เช่น TransactionRecord, Pocket
using SpendSmart.Services;             // บริการติดต่อฐานข้อมูลและ AI
using System.Collections.ObjectModel;
using System.Text.Json;               // เครื่องมือสำหรับแกะกล่องและทำความเข้าใจข้อมูล JSON จากเซิร์ฟเวอร์ Python

namespace SpendSmart.ViewModels
{
    // สืบทอดจาก ObservableObject เพื่อให้ตัวแปรต่างๆ สามารถแจ้งเตือนหน้าจอ (UI) ให้เปลี่ยนตามได้ทันที
    public partial class ChatViewModel : ObservableObject
    {
        // บริการสำหรับเรียกใช้ฐานข้อมูล SQLite และยิง API ไปหา AI
        private readonly DatabaseService _databaseService;
        private readonly AIFinancialAdvisorService _aiAdvisor;

        // ลิสต์เก็บประวัติข้อความแชท (ผูกติดกับหน้าจอ UI ถ้าข้อมูลเพิ่ม หน้าจอจะอัปเดตฟองสบู่แชททันที)
        public ObservableCollection<ChatMessage> Messages { get; set; } = new();

        // ตัวแปรผูกกับช่องพิมพ์ข้อความด้านล่างของจอ
        [ObservableProperty] private string _inputText;

        // ตัวแปรเปิด/ปิด เอฟเฟกต์ตัวหนังสือ "AI กำลังคิด..."
        [ObservableProperty] private bool _isTyping;

        // ==========================================================================
        // 🌟 กลุ่มตัวแปรควบคุม "หน้าต่างป๊อปอัพยืนยันรายการ" (Pop-up Overlay)
        // ==========================================================================

        // สวิตช์ควบคุมการโชว์/ซ่อน หน้าต่างป๊อปอัพ
        [ObservableProperty] private bool _isAiPopupVisible = false;

        // ตัวแปรเก็บข้อมูลที่ AI วิเคราะห์และส่งกลับมา เพื่อนำมาโชว์ในป๊อปอัพ
        [ObservableProperty] private decimal _aiAmount;
        [ObservableProperty] private string _aiType; // "Income" (รายรับ) หรือ "Expense" (รายจ่าย)
        [ObservableProperty] private string _aiSubCategory;
        [ObservableProperty] private string _aiNote;

        // ตัวแปรเก็บกระเป๋าเงินที่ผู้ใช้เลือกในป๊อปอัพ (ว่าจะให้หักเงินหรือเพิ่มเงินที่กระเป๋าไหน)
        [ObservableProperty] private Pocket _selectedPocket;

        // ลิสต์รายชื่อกระเป๋าเงินทั้งหมด เพื่อเอาไปทำเป็นตัวเลือก (Dropdown/Picker) ในหน้าป๊อปอัพ
        public ObservableCollection<Pocket> Pockets { get; set; } = new();

        // ตัวแปรซ่อนสำหรับเก็บ "ข้อมูลสรุปการเงินยาวๆ" ที่จะแอบส่งไปให้ AI อ่านเป็นแบ็กกราวด์
        private string _financialContext = "";

        // Constructor: ฟังก์ชันเริ่มต้นทำงานตอนเปิดหน้าแชท
        public ChatViewModel(DatabaseService databaseService, AIFinancialAdvisorService aiAdvisor)
        {
            _databaseService = databaseService;
            _aiAdvisor = aiAdvisor;

            // พิมพ์ข้อความต้อนรับอัตโนมัติจากฝั่ง AI
            Messages.Add(new ChatMessage { Text = "สวัสดีครับ! มีอะไรให้ SpendSmart AI ช่วยเรื่องการเงินหรือวิเคราะห์ค่าใช้จ่ายไหมครับ? 😊", IsUser = false });
        }

        /// <summary>
        /// [ฟังก์ชันเตรียมข้อมูลบริบท (Context)]
        /// ทำหน้าที่กวาดข้อมูลจาก Database เพื่อสรุปเป็นข้อความให้ AI รู้จักสถานะทางการเงินของเรา
        /// </summary>
        public async Task LoadContextAsync()
        {
            // 1. ระบุวันที่ปัจจุบันให้ AI รู้ (เพื่อให้ประเมินได้ว่านี่ต้นเดือนหรือปลายเดือน)
            string fullContext = $"[📅 ข้อมูลวันที่ปัจจุบัน: {DateTime.Now:dd MMMM yyyy}]\n\n";

            // 2. ดึงข้อมูล "กระเป๋าเงิน" ทั้งหมดมาสรุป
            var pockets = await _databaseService.GetPocketsAsync();
            fullContext += "[💰 สถานะกระเป๋าเงินและเป้าหมายออมเงิน]\n";

            // เคลียร์รายชื่อกระเป๋าเงินในคลังของ Pop-up ให้ว่างก่อน
            Pockets.Clear();

            if (pockets.Count > 0)
            {
                foreach (var p in pockets)
                {
                    // นำกระเป๋าเงินแอดใส่ List เพื่อเอาไปโชว์ให้เลือกในหน้า Pop-up
                    Pockets.Add(p);

                    // แยกแยะและเขียนสรุปข้อมูลกระเป๋าแบบเงินเก็บ (Saving) และแบบใช้จ่าย (Spending)
                    if (p.PocketType == "Saving")
                    {
                        string targetMonthInfo = p.TargetDate.HasValue
                            ? $"เป้าหมายเดือน: {p.TargetDate.Value:MMMM yyyy}" : "ไม่ระบุเดือนเป้าหมาย";
                        fullContext += $"- {p.Name}: เหลือ {p.CurrentBalance:N0} บาท (เป้า: {p.GoalAmount:N0} บาท, {targetMonthInfo})\n";
                    }
                    else
                    {
                        fullContext += $"- {p.Name}: มีเงินเหลือ {p.CurrentBalance:N0} บาท (กระเป๋าใช้จ่ายทั่วไป)\n";
                    }
                }
            }
            else fullContext += "- ยังไม่ได้สร้างกระเป๋าเงิน\n";

            fullContext += "\n";

            // 3. ดึงข้อมูล "รายจ่าย" ของเดือนปัจจุบันเพื่อรวมยอด
            var transactions = await _databaseService.GetTransactionsAsync();
            var currentMonthExpenses = transactions
                .Where(t => t.Type == "Expense" && t.Date.Month == DateTime.Now.Month && t.Date.Year == DateTime.Now.Year)
                .ToList();

            var totalExpense = currentMonthExpenses.Sum(t => t.Amount);
            fullContext += $"[💸 สรุปรายจ่ายเดือนนี้ - รวม {totalExpense:N0} บาท]\n";

            // จัดกลุ่มรายจ่ายตามหมวดหมู่ (เช่น อาหารกี่บาท, เดินทางกี่บาท)
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
            else fullContext += "- ยังไม่มีข้อมูลการใช้จ่ายในเดือนนี้\n";

            // บันทึกข้อความยาวๆ ทั้งหมดเก็บไว้ในตัวแปร เตรียมส่งให้ AI
            _financialContext = fullContext;
        }

        /// <summary>
        /// [ฟังก์ชันส่งแชทคุยกับ AI]
        /// สั่งงานเมื่อผู้ใช้กดปุ่ม "ส่ง" บนหน้าจอ
        /// </summary>
        [RelayCommand]
        public async Task SendMessageAsync()
        {
            // ถ้าไม่ได้พิมพ์อะไรมาเลย ให้เมินคำสั่งนี้ไป
            if (string.IsNullOrWhiteSpace(InputText)) return;

            // 1. นำข้อความที่พิมพ์ไปโชว์บนหน้าจอเป็นฝั่งผู้ใช้ (IsUser = true)
            var userMsg = InputText;
            Messages.Add(new ChatMessage { Text = userMsg, IsUser = true });
            InputText = string.Empty; // ล้างช่องพิมพ์ข้อความ

            IsTyping = true; // เปิดโชว์ข้อความว่า "AI กำลังคิด..."

            // 2. โยนข้อความและบริบท (Context) ส่งไปให้ Python ประมวลผล
            string rawResponse = await _aiAdvisor.SendChatMessageAsync(userMsg, _financialContext);
            IsTyping = false; // ปิดสถานะกำลังคิด

            // 3. เริ่มกระบวนการ "แกะกล่องรหัส JSON" ที่ได้รับมาจาก Python
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(rawResponse))
                {
                    JsonElement root = doc.RootElement;

                    // เช็กดูว่า JSON ที่ได้มา มีตัวแปรที่ชื่อว่า "status" หรือไม่
                    if (root.TryGetProperty("status", out JsonElement statusProp))
                    {
                        string status = statusProp.GetString();

                        {
                            // ดึงประโยคน่ารักๆ ที่ AI ทักทายมาโชว์ในช่องแชท
                            if (root.TryGetProperty("reply", out JsonElement replyProp))
                            {
                                Messages.Add(new ChatMessage { Text = replyProp.GetString(), IsUser = false });
                            }

                            // ดึงก้อนข้อมูลบัญชี (data) ที่ AI จัดทรงมาให้
                            if (root.TryGetProperty("data", out JsonElement dataProp))
                            {
                                // กระจายค่าไปเก็บในตัวแปรหน้าจอ (Property) ตัวเลข หมวดหมู่ ฯลฯ
                                AiAmount = dataProp.GetProperty("amount").GetDecimal();
                                AiType = dataProp.GetProperty("type").GetString();
                                AiSubCategory = dataProp.GetProperty("subCategory").GetString();
                                AiNote = dataProp.GetProperty("note").GetString();

                                // โหลดรายชื่อกระเป๋าเงินล่าสุดกันเหนียว
                                var currentPockets = await _databaseService.GetPocketsAsync();
                                Pockets.Clear();
                                foreach (var p in currentPockets) Pockets.Add(p);

                                // ตั้งค่าให้เลือกกระเป๋าใบหลัก (Default) อัตโนมัติเป็นค่าเริ่มต้น
                                SelectedPocket = Pockets.FirstOrDefault(p => p.IsDefault) ?? Pockets.FirstOrDefault();

                                // 🚀 สั่งเปิดหน้าต่าง Pop-up กลางจอทันทีเพื่อให้ผู้ใช้กดยืนยัน!
                                IsAiPopupVisible = true;
                            }
                            return; // จบการทำงานฟังก์ชันนี้ ไม่ทำบรรทัดล่างต่อ
                        }

                        // 🌟 เคส B: AI ตัดสินใจว่าเป็นแค่การคุยเล่นทั่วไป (success)
                        else if (status == "success")
                        {
                            // ดึงเฉพาะข้อความพูดคุยจาก AI มาแสดงในฟองสบู่กล่องแชท
                            if (root.TryGetProperty("reply", out JsonElement replyProp))
                            {
                                Messages.Add(new ChatMessage { Text = replyProp.GetString(), IsUser = false });
                                return;
                            }
                        }
                    }
                }
            }
            catch
            {
                // ถ้าโค้ด JSON พัง หรือแกะกล่องไม่ได้ ให้เงียบไว้แล้วปล่อยให้ไหลไปทำงานแบบ Fallback
            }

            // Fallback: หากระบบพังหรือไม่เข้าเงื่อนไขใดเลย ให้นำข้อความดิบจากเซิร์ฟเวอร์มาแสดงแทน
            Messages.Add(new ChatMessage { Text = rawResponse, IsUser = false });
        }

        /// <summary>
        /// [คำสั่งปุ่ม "ยกเลิก" บนป๊อปอัพ]
        /// </summary>
        [RelayCommand]
        public void ClosePopup()
        {
            IsAiPopupVisible = false; // ปิดหน้าต่างป๊อปอัพทิ้งไป
        }

        /// <summary>
        /// [คำสั่งปุ่ม "บันทึกเลย" บนป๊อปอัพ]
        /// ทำหน้าที่นำข้อมูลที่ AI หามาให้ บวกกับกระเป๋าที่ผู้ใช้เลือก ไปหักยอดเงินจริงและบันทึกลง SQLite
        /// </summary>
        [RelayCommand]
        public async Task ConfirmTransactionAsync()
        {
            // เซฟตี้: ถ้าผู้ใช้ไม่ได้เลือกกระเป๋าเงิน ให้กดเซฟไม่ผ่าน
            if (SelectedPocket == null)
            {
                await Shell.Current.DisplayAlert("แจ้งเตือน", "กรุณาเลือกกระเป๋าเงินที่จะดำเนินการด้วยครับ", "ตกลง");
                return;
            }

            // 1. ประกอบร่างข้อมูลรายการธุรกรรม (TransactionRecord) ชิ้นใหม่
            var newRecord = new TransactionRecord
            {
                Amount = AiAmount,
                Type = AiType,
                SubCategory = AiSubCategory,
                Note = AiNote,
                Date = DateTime.Now,
                PocketId = SelectedPocket.Id
            };

            // 2. ปรับยอดเงินจริงในกระเป๋าที่ถูกเลือก
            if (AiType == "Income")
            {
                SelectedPocket.CurrentBalance += AiAmount; // ถ้ารายรับ ให้บวกเงินเพิ่ม
            }
            else
            {
                SelectedPocket.CurrentBalance -= AiAmount; // ถ้ารายจ่าย ให้หักเงินออก
                // ดักจับป้องกันไม่ให้เงินในกระเป๋าติดลบจนกลายเป็นหนี้ (ต่ำสุดคือ 0 บาท)
                if (SelectedPocket.CurrentBalance < 0) SelectedPocket.CurrentBalance = 0;
            }

            // 3. สั่งบันทึกข้อมูลการโอนและกระเป๋าที่อัปเดตยอดแล้ว ลงฐานข้อมูล (SQLite)
            await _databaseService.SaveTransactionAsync(newRecord);
            await _databaseService.SavePocketAsync(SelectedPocket);

            // 4. สลายร่าง Pop-up ปิดทิ้งไป และสั่งให้โหลด Context การเงินใหม่เพื่อให้เป็นยอดล่าสุด
            IsAiPopupVisible = false;
            await LoadContextAsync();

            // ส่งสัญญาณวิทยุ (WeakReferenceMessenger) ไปแจ้งหน้าแดชบอร์ดและหน้าอื่นๆ ให้รีเฟรชยอดเงินด่วน!
            WeakReferenceMessenger.Default.Send(new TransactionChangedMessage());

            // แจ้งเตือนผู้ใช้ว่าภารกิจสำเร็จ
            await Shell.Current.DisplayAlert("สำเร็จ", $"บันทึกรายการ '{AiNote}' เรียบร้อยแล้วครับ", "ตกลง");
        }
    }
}