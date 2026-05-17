/*
=========================================================================================
[สรุปภาพรวมการทำงานของคลาส AIReceiptScannerService]
ไฟล์นี้ทำหน้าที่เป็น "สะพานเชื่อมเชื่อมต่ออินเทอร์เน็ต (API Service)" สำหรับฟีเจอร์สแกนสลิปอัจฉริยะ 
รับหน้าที่ส่งไฟล์ภาพจากฝั่งมือถือ ไปให้ฝั่งสมองกลเพื่อสกัดข้อความออกมา โดยมีขั้นตอนหลัก 3 สเตป:

1. ส่งไฟล์รูปแบบฟอร์ม (MultipartFormData): โหลดไฟล์รูปภาพสลิปจากเครื่องผู้ใช้ แพ็กใส่ Multipart Form 
   (เหมือนการแนบไฟล์บนหน้าเว็บ) แล้วยิง POST API ไปที่ Endpoint "scan-slip" บนเซิร์ฟเวอร์ Python Flask
2. รับและทำความสะอาดข้อความ (Data Parsing): เมื่อเซิร์ฟเวอร์ส่งข้อความยอดเงินและวันที่กลับมาในรูปแบบ JSON 
   ไฟล์นี้จะทำการแกะรหัส และทำความสะอาดอักขระส่วนเกิน (เช่น ตัดเครื่องหมาย ฿ หรือลูกศรออก)
3. แปลงวันที่ไทยเป็นสากล (Thai Date Parsing): มีลอจิกสุดฉลาดในการแยกชิ้นส่วนวันที่ย่อภาษาไทย 
   (เช่น "4 พ.ค. 69") แล้วแปลงปี พ.ศ. ย่อ ให้กลายเป็น ปี ค.ศ. สากล (DateTime) ที่ระบบฐานข้อมูลต้องการได้แม่นยำ
=========================================================================================
*/

using System.Net.Http.Headers; // เครื่องมือสำหรับตั้งค่า Header พิเศษให้ไฟล์รูปภาพ
using System.Text.Json;        // คลังเครื่องมือสำหรับแกะกล่องอ่านข้อความรหัส JSON จาก Python


namespace SpendSmart.Services
{
    /// <summary>
    /// คลาสโครงสร้าง (DTO) สำหรับใช้อุ้มผลลัพธ์ข้อมูลที่สแกนเสร็จแล้ว ส่งต่อไปให้หน้าจอเพิ่มรายการ
    /// </summary>
    public class ScanResult
    {
        public decimal Amount { get; set; }        // ยอดเงินที่สแกนได้
        public DateTime Date { get; set; }         // วันที่โอนสลิปที่แปลงเป็น DateTime แล้ว
        public string SuggestedNote { get; set; }   // ข้อความโน้ตแนะนำที่ระบบจัดเซตไว้ให้
    }

    public class AIReceiptScannerService
    {
        // ตัวแปรส่งผ่านข้อมูลทางอินเทอร์เน็ตประจำคลาส
        private readonly HttpClient _httpClient;

        // Constructor: ฟังก์ชันเริ่มต้นระบบทำงานทันทีเมื่อคลาสนี้ถูกเรียกใช้
        public AIReceiptScannerService()
        {
            _httpClient = new HttpClient();

            // ⚠️ ตั้งค่าที่อยู่ URL หลักไปยังเซิร์ฟเวอร์หลังบ้านภาษา Python
            // (ใช้ IP 10.0.2.2 เพราะเป็น IP ทะลุพิเศษจาก Android Emulator กลับมาเครื่องตัวเอง)
            _httpClient.BaseAddress = new Uri("http://10.0.2.2:5000/");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// [ฟังก์ชันส่งรูปภาพสลิปไปสแกนที่เซิร์ฟเวอร์]
        /// </summary>
        public async Task<ScanResult> ScanReceiptAsync(string imagePath)
        {
            try
            {
                // 1. เตรียมกล่องบรรจุข้อมูลแบบฟอร์ม (Multipart Form) เพื่อใช้สำหรับส่งไฟล์ภาพข้ามเน็ต
                using var form = new MultipartFormDataContent();

                // 2. เปิดอ่านไฟล์รูปภาพตาม Path ที่ส่งเข้ามาในรูปแบบ Stream ข้อมูลดิบ
                using var fileStream = File.OpenRead(imagePath);
                using var streamContent = new StreamContent(fileStream);

                // 3. กำหนด Header บอกเซิร์ฟเวอร์ว่าไฟล์ที่ส่งไปนี้เป็นภาพประเภท JPEG นะ
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

                // 4. แอดรูปภาพชิ้นนี้เข้าสู่ฟอร์ม ตั้งชื่อตัวแปรว่า "image" ให้ตรงกับคีย์ที่ฝั่ง Python Flask รอรับ
                form.Add(streamContent, "image", Path.GetFileName(imagePath));

                // 5. ยิง POST คำสั่งแนบฟอร์มรูปภาพไปที่เซิร์ฟเวอร์ตรงคำว่า "scan-slip"
                var response = await _httpClient.PostAsync("scan-slip", form);

                // 🌟 หากเซิร์ฟเวอร์ประมวลผลผ่านและตอบกลับมาสำเร็จ (HTTP 200 OK)
                if (response.IsSuccessStatusCode)
                {
                    // อ่านข้อความ JSON ที่ Python ส่งกลับมา
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var jsonDoc = JsonDocument.Parse(jsonString);

                    // ----------------------------------------------------
                    // สเตปที่ 1: ดึงและทำความสะอาดข้อมูล ยอดเงิน (Amount)
                    // ----------------------------------------------------
                    string amountText = jsonDoc.RootElement.GetProperty("amount").GetString();

                    // คลีนข้อมูล: ลบเครื่องหมายจุลภาค (,) และสัญลักษณ์เงิน ฿ ออกให้เหลือแต่ตัวเลขล้วนๆ
                    amountText = amountText.Replace(",", "").Replace("฿", "").Trim();

                    // แปลงจากข้อความตัวหนังสือ (String) ให้กลายเป็นตัวเลขทศนิยมการเงิน (decimal)
                    decimal.TryParse(amountText, out decimal parsedAmount);

                    // ----------------------------------------------------
                    // สเตปที่ 2: ดึงข้อมูล วันที่ (Date String)
                    // ----------------------------------------------------
                    // อ่านข้อความวันที่ภาษาไทยที่ Python คลีนมาให้แล้ว (เช่น "18 พ.ค. 69")
                    string dateText = jsonDoc.RootElement.GetProperty("date").GetString();

                    // ----------------------------------------------------
                    // สเตปที่ 3: เรียกลอจิกแปลงข้อความวันที่ไทยให้กลายเป็นระบบ DateTime สากล
                    // ----------------------------------------------------
                    DateTime scannedDate = ParseThaiDate(dateText);

                    // 6. แพ็กผลลัพธ์ทั้งหมดใส่กล่อง ScanResult ส่งคืนกลับไปให้ ViewModel ใช้งานกรอกลงฟอร์ม
                    return new ScanResult
                    {
                        Amount = parsedAmount,
                        Date = scannedDate,
                        // เซตข้อความแนะนำสำหรับเขียนลงโน้ตบันทึก เพื่อแจ้งผู้ใช้ว่ารายการนี้มาจากระบบสแกน
                        SuggestedNote = $"✨ AI สแกน: {parsedAmount}฿ ({dateText})"
                    };
                }
            }
            catch (Exception ex)
            {
                // แสดงข้อความความผิดพลาดลงหน้าต่าง Console หากเกิดปัญหาทางระบบฮาร์ดแวร์หรือเน็ตหลุด
                Console.WriteLine($"Error: {ex.Message}");
            }

            // Fallback: หากระบบพังหรือสแกนพลาด ให้ส่งค่าเงิน 0 และวันที่ปัจจุบันกลับไปเป็นค่าเซฟตี้ ป้องกันแอปแครช
            return new ScanResult { Amount = 0, Date = DateTime.Now, SuggestedNote = "อ่านสลิปไม่สำเร็จ" };
        }

        /// <summary>
        /// [ลอจิกสุดฉลาด: แปลงข้อความวันที่ไทยย่อ เช่น "4 พ.ค. 69" ให้เป็นคริสต์ศักราชระบบ DateTime]
        /// </summary>
        private DateTime ParseThaiDate(string thaiDateStr)
        {
            try
            {
                // เซฟตี้ดักจับ: ถ้าค่าส่งมาเป็นช่องว่าง ให้ตีเป็นเวลาปัจจุบันทันที
                if (string.IsNullOrEmpty(thaiDateStr)) return DateTime.Now;

                // ใช้คำสั่ง Split ตัดช่องว่างเพื่อแยกส่วนประกอบออกเป็นลิสต์อาร์เรย์ทีละชิ้น
                // ตัวอย่าง: "4 พ.ค. 69" จะถูกหั่นออกเป็นชิ้นๆ คือ parts[0]="4", parts[1]="พ.ค.", parts[2]="69"
                var parts = thaiDateStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) return DateTime.Now; // โครงสร้างข้อความไม่ครบวันเดือนปี ให้ส่งเวลากลับ

                int day = int.Parse(parts[0]);       // แปลงข้อความชิ้นแรกเป็นตัวเลข "วัน"
                string monthStr = parts[1];          // ดึงข้อความย่อ "เดือน" ออกมาเก็บไว้
                int yearShort = int.Parse(parts[2]); // แปลงข้อความชิ้นสุดท้ายเป็นตัวเลข "ปี พ.ศ. ย่อ"

                // นำตัวย่อเดือน (เช่น "พ.ค.") เข้าฟังก์ชันจับคู่ไปหาลำดับตัวเลข (ได้เลข 5)
                int month = GetMonthNumber(monthStr);

                // 🌟 สูตรคำนวณปีเด็ด: แปลงปี พ.ศ. 2 หลัก ให้กลายเป็น ปี ค.ศ. 4 หลัก
                // ลอจิก: เอาเลขปี 2 หลัก (69) มารวมกับศตวรรษ (2500) = พ.ศ. 2569 แล้วลบด้วย 543 เพื่อแปลงเป็น ค.ศ. 2026
                int christianYear = (2500 + yearShort) - 543;

                // ประกอบชิ้นส่วนตัวเลข ปี, เดือน, วัน ที่คำนวณเสร็จแล้ว เป็นวัตถุ DateTime สากลอย่างสมบูรณ์
                return new DateTime(christianYear, month, day);
            }
            catch
            {
                // หากผู้ใช้จงใจอัปโหลดรูปภาพที่ไม่ใช่สลิปแล้วลอจิกแตก ให้ส่งวันที่ปัจจุบันกลับไปแบบปลอดภัย
                return DateTime.Now;
            }
        }

        /// <summary>
        /// ฟังก์ชันพจนานุกรม (Dictionary) สำหรับจับคู่ผูกชื่อเดือนย่อไทย เข้ากับหลักตัวเลขสากล (1-12)
        /// </summary>
        private int GetMonthNumber(string monthThai)
        {
            var months = new Dictionary<string, int>
            {
                {"ม.ค.", 1}, {"ก.พ.", 2}, {"มี.ค.", 3}, {"เม.ย.", 4},
                {"พ.ค.", 5}, {"มิ.ย.", 6}, {"ก.ค.", 7}, {"ส.ค.", 8},
                {"ก.ย.", 9}, {"ต.ค.", 10}, {"พ.ย.", 11}, {"ธ.ค.", 12}
            };

            // เช็กว่าตัวอักษรเดือนที่ส่งมา มีอยู่ในพจนานุกรมไหม ถ้ามีให้ส่งเลขลำดับเดือนนั้นกลับไป 
            // แต่ถ้าอ่านพลาดจนไม่มีในลิสต์ ให้ใช้เลขเดือนของเวลาปัจจุบันตัวเครื่องส่งกลับไปแทนกันบั๊ก
            return months.ContainsKey(monthThai) ? months[monthThai] : DateTime.Now.Month;
        }
    }
}