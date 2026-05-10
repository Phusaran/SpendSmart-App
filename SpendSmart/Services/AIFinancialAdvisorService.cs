using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SpendSmart.Services
{
    public class AIFinancialAdvisorService
    {
        private readonly HttpClient _httpClient;

        public AIFinancialAdvisorService()
        {
            _httpClient = new HttpClient();
            // ⚠️ ใช้ IP เดียวกับระบบสแกนสลิป (10.0.2.2 สำหรับ Emulator หรือ IP เครื่อง)
            _httpClient.BaseAddress = new Uri("http://10.0.2.2:5000/");
        }
        public async Task<string> SendChatMessageAsync(string message, string context)
        {
            try
            {
                var requestData = new { message = message, context = context };
                string jsonContent = JsonSerializer.Serialize(requestData);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("chat", httpContent);
                var jsonString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var jsonDoc = JsonDocument.Parse(jsonString);
                    return jsonDoc.RootElement.GetProperty("reply").GetString();
                }
                return "AI กำลังงงๆ รบกวนลองใหม่อีกครั้งครับ 😅";
            }
            catch (Exception ex)
            {
                return $"เชื่อมต่อ AI ไม่ได้: {ex.Message}";
            }
        }
        public async Task<string> GetFinancialAdviceAsync(decimal totalExpense, object categoryData)
        {
            try
            {
                var requestData = new
                {
                    total = totalExpense,
                    categories = categoryData
                };

                string jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // ส่งไปที่ Endpoint ใหม่ของ Python
                var response = await _httpClient.PostAsync("analyze-spending", content);
                var jsonString = await response.Content.ReadAsStringAsync(); // อ่านข้อความที่ Python ส่งมาก่อน

                if (response.IsSuccessStatusCode)
                {
                    var jsonDoc = JsonDocument.Parse(jsonString);
                    return jsonDoc.RootElement.GetProperty("advice").GetString();
                }
                else
                {
                    // 🌟 ถ้าพัง (500) ให้ดึงสาเหตุจริงๆ ออกมาโชว์ที่หน้าจอเลย
                    try
                    {
                        var jsonDoc = JsonDocument.Parse(jsonString);
                        string realError = jsonDoc.RootElement.GetProperty("message").GetString();
                        return $"[แจ้งเตือนจากเซิร์ฟเวอร์]\n{realError}";
                    }
                    catch
                    {
                        return $"[ข้อผิดพลาด]\n{jsonString}";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"เกิดข้อผิดพลาดในการเชื่อมต่อ AI: {ex.Message}";
            }
        }
    }
}