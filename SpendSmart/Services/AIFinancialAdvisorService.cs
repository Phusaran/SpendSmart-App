/*
=========================================================================================
[สรุปภาพรวมการทำงานของคลาส AIFinancialAdvisorService]
ไฟล์นี้ทำหน้าที่เป็น "สะพานเชื่อมอินเทอร์เน็ต (API Service)" ระหว่างแอปพลิเคชันมือถือ (.NET MAUI) 
กับเซิร์ฟเวอร์สมองกล (Python Flask Backend) ที่เราเขียนไว้ โดยรับผิดชอบหน้าที่หลัก 2 อย่าง:

1. ส่งแชทหา AI (SendChatMessageAsync): รับข้อความที่เราพิมพ์คู่กับบริบทกระเป๋าเงิน (Context) 
   แปลงเป็น JSON แล้วยิง POST ไปที่ Endpoint "chat" จากนั้นรับค่า JSON ดิบส่งกลับไปให้ ChatViewModel
2. ขอคำแนะนำการเงิน (GetFinancialAdviceAsync): แพ็กยอดรวมรายจ่ายและข้อมูลหมวดหมู่ย่อย 
   ยิง POST ไปที่ Endpoint "analyze-spending" เพื่อให้ AI ตัวจริงวิเคราะห์ และทำหน้าที่แกะกล่องเอาคำแนะนำออกมาแสดงผล
=========================================================================================
*/

using System.Text;
using System.Text.Json; // คลังเครื่องมือสำหรับจัดรูปข้อมูลให้เป็นรหัส JSON (Serialization/Deserialization)

namespace SpendSmart.Services
{
    public class AIFinancialAdvisorService
    {
        // ตัวแปรส่งผ่านข้อมูลทางอินเทอร์เน็ตประจำคลาส
        private readonly HttpClient _httpClient;

        // Constructor: ตัวเริ่มต้นระบบ จะทำงานทันทีเมื่อคลาสนี้ถูกเรียกใช้
        public AIFinancialAdvisorService()
        {
            _httpClient = new HttpClient();

            // ⚠️ ตั้งค่าที่อยู่ (URL) หลักของเซิร์ฟเวอร์ Python หลังบ้าน
            // หมายเหตุ: ใช้ IP "http://10.0.2.2:5000/" เพราะเป็น IP ทะลุพิเศษของ Android Emulator 
            // ที่ใช้ติดต่อกลับมายังเครื่องคอมพิวเตอร์ตัวเอง (Localhost พอร์ต 5000)
            _httpClient.BaseAddress = new Uri("http://10.0.2.2:5000/");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// [ฟังก์ชันส่งข้อความแชทไปหาเซิร์ฟเวอร์ AI]
        /// </summary>
        public async Task<string> SendChatMessageAsync(string message, string context)
        {
            try
            {
                // 1. นำข้อความของผู้ใช้ และบริบทการเงิน มาแพ็กคู่รวมกันเป็นวัตถุ (Object) ชิ้นเดียว
                var requestData = new { message = message, context = context };

                // 2. แปลงวัตถุชิ้นนั้นให้กลายเป็น String ในรูปแบบรหัส JSON
                string jsonContent = JsonSerializer.Serialize(requestData);

                // 3. กำหนดหัวข้อไฟล์ (Header) ว่านี่คือไฟล์ประเภท JSON และเข้ารหัสภาษาด้วย UTF-8
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // 4. สั่งยิงข้อมูลรูปแบบ POST ไปที่เซิร์ฟเวอร์ปลายทางตรงตำแหน่ง "chat" (URL เต็มคือ http://10.0.2.2:5000/chat)
                var response = await _httpClient.PostAsync("chat", httpContent);

                // 5. อ่านข้อความที่เซิร์ฟเวอร์ Python ตอบกลับมาในรูปแบบ String ดิบ
                var jsonString = await response.Content.ReadAsStringAsync();

                // 6. เช็กว่าเซิร์ฟเวอร์ตอบกลับมาสำเร็จหรือไม่ (Status HTTP 200 OK)
                if (response.IsSuccessStatusCode)
                {
                    // 🌟 ส่งคืนข้อความ JSON ดิบทั้งหมดที่ได้รับมาจาก Flask 
                    // เพื่อให้ ChatViewModel นำไปถอดรหัส (Parse) แกะเอาข้อความในคีย์ "reply" ออกมาแสดงผลบนฟองสบู่แชทได้ทันที
                    return jsonString;
                }

                // กรณีเซิร์ฟเวอร์ตอบกลับรหัสข้อผิดพลาดอื่นๆ
                return "AI กำลังงงๆ รบกวนลองใหม่อีกครั้งครับ 😅";
            }
            catch (Exception ex)
            {
                // ดักจับเคสฉุกเฉิน (เช่น ลืมเปิดเซิร์ฟเวอร์ Python หรือเน็ตหลุด) ป้องกันแอปค้างแครช
                return $"เชื่อมต่อ AI ไม่ได้: {ex.Message}";
            }
        }

        /// <summary>
        /// [ฟังก์ชันส่งยอดเงินรวมเพื่อขอคำแนะนำการเงินประจำเดือน]
        /// </summary>
        public async Task<string> GetFinancialAdviceAsync(decimal totalExpense, object categoryData)
        {
            try
            {
                // 1. นำยอดรวมรายจ่าย และ ลิสต์ข้อมูลรายหมวดหมู่ มาแพ็กใส่กล่องวัตถุ
                var requestData = new
                {
                    total = totalExpense,
                    categories = categoryData
                };

                // 2. แปลงวัตถุให้เป็นข้อความรหัส JSON เพื่อส่งผ่านอินเทอร์เน็ต
                string jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // 3. ยิงคำสั่ง POST ไปที่ Endpoint "analyze-spending" ของเซิร์ฟเวอร์หลังบ้าน
                var response = await _httpClient.PostAsync("analyze-spending", content);

                // 4. อ่านคำตอบดึบที่ได้รับกลับมาจากเซิร์ฟเวอร์
                var jsonString = await response.Content.ReadAsStringAsync();

                // 🌟 กรณี A: เซิร์ฟเวอร์ประมวลผลสำเร็จ (HTTP 200 OK)
                if (response.IsSuccessStatusCode)
                {
                    // ทำการแกะกล่อง JSON (Parse) เพื่อเจาะจงดึงเอาข้อความในตัวแปร "advice" ออกมาแสดงผลบนจอตรงๆ
                    var jsonDoc = JsonDocument.Parse(jsonString);
                    string advice = jsonDoc.RootElement.GetProperty("advice").GetString();
                    return advice ?? "ไม่สามารถโหลดคำแนะนำได้ในขณะนี้";
                }
                // 🌟 กรณี B: เซิร์ฟเวอร์เกิดข้อผิดพลาด (เช่น ติดลิมิต Quota หรือ API พัง)
                else
                {
                    try
                    {
                        // พยายามแกะดูคำสั่งแจ้งเตือน Error ที่ Python ส่งมา (ตัวแปร message)
                        var jsonDoc = JsonDocument.Parse(jsonString);
                        string realError = jsonDoc.RootElement.GetProperty("message").GetString();
                        return $"[แจ้งเตือนจากเซิร์ฟเวอร์]\n{realError}";
                    }
                    catch
                    {
                        // หากเซิร์ฟเวอร์พังหนักจนไม่ส่ง JSON กลับมา ให้พ่นข้อความดิบแจ้งเตือนความปลอดภัยล่างสุด
                        return $"[ข้อผิดพลาด]\n{jsonString}";
                    }
                }
            }
            catch (Exception ex)
            {
                // ดักจับข้อผิดพลาดกรณีไม่สามารถต่อท่อเชื่อมอินเทอร์เน็ตไปหาตัว Python เซิร์ฟเวอร์ได้
                return $"เกิดข้อผิดพลาดในการเชื่อมต่อ AI: {ex.Message}";
            }
        }
    }
}