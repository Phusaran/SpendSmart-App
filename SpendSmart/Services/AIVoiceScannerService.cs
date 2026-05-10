using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace SpendSmart.Services
{
    // คลาสสำหรับรับค่าที่ AI วิเคราะห์เสียงส่งกลับมา
    public class VoiceResult
    {
        public decimal Amount { get; set; }
        public string Type { get; set; }
        public string SubCategory { get; set; }
        public string Note { get; set; }
    }

    public class AIVoiceScannerService
    {
        private readonly HttpClient _httpClient;

        public AIVoiceScannerService()
        {
            _httpClient = new HttpClient();
            // ⚠️ ใช้ IP เดียวกับระบบอื่นๆ (10.0.2.2 สำหรับ Emulator)
            _httpClient.BaseAddress = new Uri("http://10.0.2.2:5000/");
        }

        public async Task<VoiceResult> AnalyzeVoiceAsync(string audioPath)
        {
            try
            {
                using var form = new MultipartFormDataContent();
                using var fileStream = File.OpenRead(audioPath);
                using var streamContent = new StreamContent(fileStream);

                streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                form.Add(streamContent, "audio", "voice_note.wav");

                var response = await _httpClient.PostAsync("voice-to-expense", form);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var jsonDoc = JsonDocument.Parse(jsonString);
                    var data = jsonDoc.RootElement.GetProperty("data");

                    return new VoiceResult
                    {
                        Amount = data.GetProperty("amount").GetDecimal(),
                        Type = data.GetProperty("type").GetString(),
                        SubCategory = data.GetProperty("subCategory").GetString(),
                        Note = data.GetProperty("note").GetString()
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Voice Error: {ex.Message}");
            }
            return null;
        }
    }
}