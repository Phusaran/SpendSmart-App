using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace SpendSmart.Services
{
    public class ScanResult
    {
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string SuggestedNote { get; set; }
    }

    public class AIReceiptScannerService
    {
        private readonly HttpClient _httpClient;

        public AIReceiptScannerService()
        {
            _httpClient = new HttpClient();
            // ⚠️ ตรวจสอบ IP ของคอมพิวเตอร์คุณให้ถูกต้อง (หรือใช้ 10.0.2.2 สำหรับ Emulator)
            _httpClient.BaseAddress = new Uri("http://10.0.2.2:5000/");
        }

        public async Task<ScanResult> ScanReceiptAsync(string imagePath)
        {
            try
            {
                using var form = new MultipartFormDataContent();
                using var fileStream = File.OpenRead(imagePath);
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                form.Add(streamContent, "image", Path.GetFileName(imagePath));

                var response = await _httpClient.PostAsync("scan-slip", form);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var jsonDoc = JsonDocument.Parse(jsonString);

                    // 1. ดึงยอดเงิน (Amount)
                    string amountText = jsonDoc.RootElement.GetProperty("amount").GetString();
                    amountText = amountText.Replace(",", "").Replace("฿", "").Trim();
                    decimal.TryParse(amountText, out decimal parsedAmount);

                    // 2. ดึงวันที่ภาษาไทย (Date String)
                    string dateText = jsonDoc.RootElement.GetProperty("date").GetString();

                    // 3. แปลง "4 พ.ค. 69" ให้กลายเป็น DateTime ของจริง
                    DateTime scannedDate = ParseThaiDate(dateText);

                    return new ScanResult
                    {
                        Amount = parsedAmount,
                        Date = scannedDate,
                        // ✨ ปรับ Noti / Note ให้โชว์ทั้งยอดเงินและวันที่
                        SuggestedNote = $"✨ AI สแกน: {parsedAmount}฿ ({dateText})"
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            return new ScanResult { Amount = 0, Date = DateTime.Now, SuggestedNote = "อ่านสลิปไม่สำเร็จ" };
        }

        private DateTime ParseThaiDate(string thaiDateStr)
        {
            try
            {
                if (string.IsNullOrEmpty(thaiDateStr)) return DateTime.Now;

                // แยกส่วนประกอบ "4", "พ.ค.", "69"
                var parts = thaiDateStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) return DateTime.Now;

                int day = int.Parse(parts[0]);
                string monthStr = parts[1];
                int yearShort = int.Parse(parts[2]);

                // หาลำดับเดือน
                int month = GetMonthNumber(monthStr);

                // คำนวณปี: 69 -> 2569 -> 2026
                int christianYear = (2500 + yearShort) - 543;

                return new DateTime(christianYear, month, day);
            }
            catch
            {
                return DateTime.Now;
            }
        }

        private int GetMonthNumber(string monthThai)
        {
            var months = new Dictionary<string, int>
            {
                {"ม.ค.", 1}, {"ก.พ.", 2}, {"มี.ค.", 3}, {"เม.ย.", 4},
                {"พ.ค.", 5}, {"มิ.ย.", 6}, {"ก.ค.", 7}, {"ส.ค.", 8},
                {"ก.ย.", 9}, {"ต.ค.", 10}, {"พ.ย.", 11}, {"ธ.ค.", 12}
            };
            return months.ContainsKey(monthThai) ? months[monthThai] : DateTime.Now.Month;
        }
    }
}