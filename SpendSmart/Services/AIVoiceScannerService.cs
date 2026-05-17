/*
=========================================================================================
[สรุปภาพรวมการทำงานของคลาส AIVoiceScannerService]
ไฟล์นี้ทำหน้าที่เป็น "สะพานเชื่อมต่ออินเทอร์เน็ต (API Service)" สำหรับฟีเจอร์ "สั่งการด้วยเสียง (Voice-to-Expense)" 
ทำหน้าที่รับไฟล์เสียงที่เราอัดจากไมโครโฟนบนมือถือ แล้วส่งไปให้เซิร์ฟเวอร์ Python หลังบ้านแปลงเป็นรายการบัญชี 
โดยแบ่งกระบวนการทำงานออกเป็น 3 ขั้นตอนหลัก:

1. แพ็กไฟล์เสียง (Multipart FormData): รับที่อยู่ไฟล์เสียง (.wav) จากเครื่องมือถือ นำมาเปิดอ่านเป็น Stream ข้อมูลดิบ 
   แล้วจัดรูปให้อยู่ในรูปแบบกล่องฟอร์ม Multipart Content (เหมือนการแนบไฟล์เสียงส่งไปในระบบฟอร์ม)
2. ยิง POST API ข้ามระบบ: ส่งไฟล์เสียงนี้ผ่านอินเทอร์เน็ตไปยังเซิร์ฟเวอร์ Python Flask ที่ Endpoint "voice-to-expense" 
   เพื่อให้ Gemini ฟังและถอดรหัสเสียงพูดออกมา
3. สกัดข้อมูลผลลัพธ์ (Data Mapping): รับข้อมูล JSON คำตอบกลับมาจากเซิร์ฟเวอร์ แกะกล่องข้อมูลย่อย (data) 
   แล้วนำค่า จำนวนเงิน (Amount), ประเภท (Type), หมวดหมู่ (SubCategory) และ โน้ต (Note) 
   มาจัดใส่คลาส VoiceResult เพื่อส่งต่อให้หน้าจอแสดงผลกรอกฟอร์มให้อัตโนมัติ
=========================================================================================
*/


using System.Net.Http.Headers; // เครื่องมือสำหรับตั้งค่า Header บอกประเภทไฟล์ (Mime Type)
using System.Text.Json;        // คลังเครื่องมือสำหรับแกะกล่องอ่านข้อความรหัส JSON ที่ AI หลังบ้านส่งกลับมา

namespace SpendSmart.Services
{
    /// <summary>
    /// คลาสโครงสร้างข้อมูล (DTO) สำหรับใช้อุ้มผลลัพธ์ที่ AI สกัดได้จากเสียงพูด เพื่อส่งกลับไปกรอกบนฟอร์มหน้าจอ
    /// </summary>
    public class VoiceResult
    {
        public decimal Amount { get; set; }      // จำนวนเงินตัวเลขทศนิยมที่ AI ฟังได้
        public string Type { get; set; }         // ประเภทธุรกรรม ("Expense" รายจ่าย หรือ "Income" รายรับ)
        public string SubCategory { get; set; }  // หมวดหมู่ย่อยที่ AI วิเคราะห์ให้ (เช่น อาหาร, เดินทาง)
        public string Note { get; set; }         // รายละเอียดเนื้อหาบันทึกย่อ (เช่น ค่าข้าวมันไก่ผสมไข่ดาว)
    }

    public class AIVoiceScannerService
    {
        // ตัวแปรสำหรับใช้ยิงข้อมูลผ่านอินเทอร์เน็ตประจำคลาส
        private readonly HttpClient _httpClient;

        // Constructor: ฟังก์ชันเริ่มต้นระบบ ทำงานทันทีเมื่อแอปพลิเคชันเรียกใช้ Service นี้
        public AIVoiceScannerService()
        {
            _httpClient = new HttpClient();

            // ⚠️ ตั้งค่าที่อยู่ URL ปลายทางหลักไปยังเซิร์ฟเวอร์หลังบ้านภาษา Python ของเรา
            // (ใช้ IP 10.0.2.2 พอร์ต 5000 เพราะเป็น IP ทะลุพิเศษจาก Android Emulator วิ่งตรงเข้าเครื่องคอมตัวเอง)
            _httpClient.BaseAddress = new Uri("http://10.0.2.2:5000/");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// [ฟังก์ชันส่งไฟล์เสียงไปให้ AI วิเคราะห์รายการเงิน]
        /// </summary>
        /// <param name="audioPath">ที่อยู่ตำแหน่งพาธไฟล์เสียงชั่วคราว (.wav) ที่เซฟไว้ในเครื่องมือถือ</param>
        public async Task<VoiceResult> AnalyzeVoiceAsync(string audioPath)
        {
            try
            {
                // 1. เตรียมกล่องข้อมูลรูปแบบฟอร์ม (MultipartFormDataContent) เพื่อส่งไฟล์ดิบข้ามเน็ตคู่กับชื่อตัวแปร
                using var form = new MultipartFormDataContent();

                // 2. เปิดอ่านไฟล์เสียงจากตำแหน่งในเครื่องมือถือในรูปแบบสตรีมข้อมูลดิบ (File.OpenRead)
                using var fileStream = File.OpenRead(audioPath);
                using var streamContent = new StreamContent(fileStream);

                // 3. ตั้งค่า Header เพื่อบอกเซิร์ฟเวอร์หลังบ้านให้รู้ว่าไฟล์นี้เป็นรูปแบบเสียงประเภท Wave (.wav) นะ
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

                // 4. แอดสตรีมข้อมูลเสียงลงในกล่องฟอร์ม ตั้งชื่อคีย์ตัวแปรว่า "audio" ให้ตรงเป๊ะกับที่ฝั่ง Python Flask รอรับ
                form.Add(streamContent, "audio", "voice_note.wav");

                // 5. สั่งยิงข้อมูล POST แนบกล่องฟอร์มไฟล์เสียงนี้ ข้ามอินเทอร์เน็ตไปที่ Endpoint "voice-to-expense"
                var response = await _httpClient.PostAsync("voice-to-expense", form);

                // 🌟 หากเซิร์ฟเวอร์ฝั่ง Python ประมวลผลและแปลงเสียงเสร็จสิ้นสำเร็จ (HTTP 200 OK)
                if (response.IsSuccessStatusCode)
                {
                    // 6. อ่านข้อความรหัส JSON ทั้งหมดที่ส่งกลับมาจากเซิร์ฟเวอร์
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // 7. ใช้คลาส JsonDocument ทำการถอดรหัสแตกกล่องข้อความออกมาอ่านทีละส่วน
                    var jsonDoc = JsonDocument.Parse(jsonString);

                    // เจาะจงดึงเฉพาะข้อมูลก้อนที่อยู่ในตัวแปรคีย์ชื่อว่า "data" 
                    var data = jsonDoc.RootElement.GetProperty("data");

                    // 8. ดึงข้อมูลย่อยข้างในทีละตัวแปร นำมาผูก (Mapping) เข้าสู่โครงสร้างคลาส VoiceResult ตัวใหม่ของเรา
                    return new VoiceResult
                    {
                        // ดึงเลขจำนวนเงิน แปลงรหัสจาก JSON เป็นค่าตัวเลข decimal ของภาษา C#
                        Amount = data.GetProperty("amount").GetDecimal(),

                        // ดึงข้อความประเภท (Expense/Income) แปลงเป็นข้อความ String
                        Type = data.GetProperty("type").GetString(),

                        // ดึงข้อความหมวดหมู่ย่อย และรายละเอียดบันทึกย่อ
                        SubCategory = data.GetProperty("subCategory").GetString(),
                        Note = data.GetProperty("note").GetString()
                    };
                }
            }
            catch (Exception ex)
            {
                // ดักจับระบบเซฟตี้ (Fallback): หากเกิดข้อผิดพลาดทางฮาร์ดแวร์หรืออินเทอร์เน็ตขัดข้อง ให้สั่งปริ้นท์ข้อความบอกในหน้าต่างดีบั๊ก
                Console.WriteLine($"Voice Error: {ex.Message}");
            }

            // หากระบบพังหรือทำงานไม่สำเร็จ ให้ส่งคืนค่าว่าง (null) กลับไป เพื่อให้ ViewModel ไปขึ้นแจ้งเตือนป๊อปอัพผู้ใช้ต่อไป
            return null;
        }
    }
}