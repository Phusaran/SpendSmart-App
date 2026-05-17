/*
=========================================================================================
[สรุปภาพรวมการทำงานของไฟล์ AddTransactionViewModel.cs]
ไฟล์นี้คือ "หัวใจหลักของการบันทึกรายรับ-รายจ่าย" (Add Transaction Screen) ทำหน้าที่จัดการระบบ
ที่ผู้ใช้จะนำข้อมูลเข้าสู่แอปพลิเคชัน โดยมีความสามารถสุดล้ำ 4 อย่างรวมอยู่ในไฟล์เดียว:

1. ระบบกรอกข้อมูลปกติ (Manual): ดึงรายชื่อกระเป๋าเงินมาให้เลือก และรอรับค่าตัวเลข/หมวดหมู่จากผู้ใช้
2. ระบบสั่งด้วยเสียง (Voice-to-Expense): บันทึกเสียงพูด ส่งไปให้ AI วิเคราะห์ แล้วดึงค่ามากรอกให้อัตโนมัติ
3. ระบบสแกนสลิป (AI OCR): เปิดคลังภาพ เลือกรูปสลิป ส่งให้ AI อ่านยอดเงินและวันที่มากรอกให้
4. ระบบสะสมแต้ม (Gamification): เมื่อบันทึกรายการสำเร็จ ระบบจะแจกค่าประสบการณ์ (EXP) ให้ผู้ใช้ 
   เพื่อนำไปปลดล็อกฟีเจอร์อื่นๆ ภายในแอป
=========================================================================================
*/

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Plugin.Maui.Audio;               
using SpendSmart.Messages;
using SpendSmart.Models;
using SpendSmart.Services;
using System.Collections.ObjectModel;

namespace SpendSmart.ViewModels
{
    // สืบทอด ObservableObject เพื่อให้ตัวแปรสื่อสารกับหน้าจอ UI ได้ (ค่าเปลี่ยน หน้าจอเปลี่ยนตาม)
    public partial class AddTransactionViewModel : ObservableObject
    {
        // 🌟 กลุ่ม Services ที่จำเป็นต้องใช้
        private readonly DatabaseService _databaseService;       // ติดต่อฐานข้อมูล
        private readonly AIReceiptScannerService _aiScanner;     // บริการอ่านสลิป
        private readonly AIVoiceScannerService _voiceScanner;    // บริการฟังเสียง
        private readonly IAudioManager _audioManager;            // ตัวควบคุมฮาร์ดแวร์ไมโครโฟน
        private IAudioRecorder _audioRecorder;                   // ตัวอัดเสียง

        // ==========================================================================
        // 🌟 กลุ่มตัวแปรผูกกับหน้าจอ UI (Properties)
        // ==========================================================================
        [ObservableProperty] private decimal _amount;            // ยอดเงิน
        [ObservableProperty] private string _selectedType = "Expense"; // ประเภท (ค่าเริ่มต้นคือ รายจ่าย)
        [ObservableProperty] private Pocket _selectedPocket;     // กระเป๋าเงินที่เลือกจะหัก/เพิ่มเงิน
        [ObservableProperty] private string _subCategory;        // หมวดหมู่
        [ObservableProperty] private DateTime _date = DateTime.Now; // วันที่ (ค่าเริ่มต้นคือ วันนี้)
        [ObservableProperty] private string _note;               // บันทึกช่วยจำ
        [ObservableProperty] private string _receiptImagePath;   // ที่อยู่ไฟล์รูปสลิป

        // กลุ่มตัวแปรควบคุมระบบบันทึกเสียง
        [ObservableProperty] private bool _isRecording;          // สถานะว่ากำลังอัดเสียงอยู่หรือไม่
        [ObservableProperty] private string _voiceButtonText = "🎙️ กดเพื่อพูดจดรายจ่าย"; // ข้อความบนปุ่ม
        [ObservableProperty] private Color _voiceButtonColor = Color.FromArgb("#3498DB"); // สีของปุ่ม

        // ตัวแปรควบคุมหน้าจอโหลดดิ้ง (เวลา AI กำลังทำงาน)
        [ObservableProperty] private bool _isScanning;

        // ลิสต์รายชื่อกระเป๋าเงินสำหรับโชว์ในช่อง Dropdown
        public ObservableCollection<Pocket> Pockets { get; set; } = new();

        // ลิสต์ตัวเลือกประเภทรายการ 
        public List<string> TransactionTypes { get; } = new List<string> { "Expense", "Income" };

        // Constructor: ฉีด Services (Dependency Injection) เข้ามาใช้งาน
        public AddTransactionViewModel(
            DatabaseService databaseService,
            AIReceiptScannerService aiScanner,
            AIVoiceScannerService voiceScanner,
            IAudioManager audioManager)
        {
            _databaseService = databaseService;
            _aiScanner = aiScanner;
            _voiceScanner = voiceScanner;
            _audioManager = audioManager;
        }

        /// <summary>
        /// ดึงรายชื่อกระเป๋าเงินจากฐานข้อมูลมาเตรียมไว้ให้ผู้ใช้เลือก
        /// </summary>
        public async Task LoadPocketsAsync()
        {
            var pockets = await _databaseService.GetPocketsAsync();
            Pockets.Clear();

            foreach (var p in pockets)
            {
                Pockets.Add(p);

                // พยายามหากระเป๋าที่ชื่อมีคำว่า "main" หรือตั้งค่าเป็น IsDefault เพื่อเลือกไว้ให้ก่อนอัตโนมัติ
                if (p.IsDefault || p.Name.ToLower().Contains("main"))
                {
                    SelectedPocket = p;
                }
            }

            // ถ้าหาบัญชีหลักไม่เจอ ก็ให้เลือกบัญชีอันแรกสุดแทน
            if (SelectedPocket == null && Pockets.Count > 0)
            {
                SelectedPocket = Pockets[0];
            }
        }

        /// <summary>
        /// [ฟังก์ชันควบคุมการสั่งงานด้วยเสียง]
        /// กดครั้งแรก = ขอสิทธิ์และเริ่มอัดเสียง | กดครั้งที่สอง = หยุดอัดและส่งให้ AI
        /// </summary>
        [RelayCommand]
        public async Task ToggleVoiceRecordAsync()
        {
            // 1. ตรวจสอบว่าแอปได้รับอนุญาตให้ใช้ไมโครโฟนหรือยัง
            var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();

            if (status != PermissionStatus.Granted)
            {
                // ถ้ายังไม่ได้รับอนุญาต ให้เด้งป๊อปอัพขอสิทธิ์จากระบบปฏิบัติการมือถือ
                status = await Permissions.RequestAsync<Permissions.Microphone>();

                if (status != PermissionStatus.Granted)
                {
                    await Shell.Current.DisplayAlert("แจ้งเตือน", "กรุณาอนุญาตให้แอปใช้ไมโครโฟนก่อนครับ", "ตกลง");
                    return;
                }
            }

            // 2. ถ้าไม่ได้อัดเสียงอยู่ -> ให้เริ่มอัดเสียง
            if (!IsRecording)
            {
                _audioRecorder = _audioManager.CreateRecorder(); // สร้างตัวอัด
                await _audioRecorder.StartAsync();               // เริ่มอัด

                IsRecording = true;
                VoiceButtonText = "🛑 กำลังฟัง... (กดเพื่อหยุด)";
                VoiceButtonColor = Color.FromArgb("#E74C3C"); // เปลี่ยนปุ่มเป็นสีแดงเพื่อเตือน
            }
            // 3. ถ้ากำลังอัดเสียงอยู่ -> ให้หยุดอัดและส่งข้อมูลไปหา AI
            else
            {
                var recordedAudio = await _audioRecorder.StopAsync(); // หยุดและรับไฟล์เสียงมา

                // รีเซ็ตสถานะหน้าจอ
                IsRecording = false;
                VoiceButtonText = "🎙️ กดเพื่อพูดจดรายจ่าย";
                VoiceButtonColor = Color.FromArgb("#3498DB");
                IsScanning = true; // เปิดโชว์ตัวโหลดดิ้ง

                var audioStream = recordedAudio.GetAudioStream();
                var filePath = Path.Combine(FileSystem.CacheDirectory, "voice_expense.wav");

                // เซฟสตรีมเสียงเป็นไฟล์ .wav ชั่วคราวลงในโฟลเดอร์ Cache
                using (var fileStream = File.Create(filePath))
                {
                    await audioStream.CopyToAsync(fileStream);
                }

                // 🌟 ส่งไฟล์เสียงไปหา Python AI
                var result = await _voiceScanner.AnalyzeVoiceAsync(filePath);

                // ถ้า AI วิเคราะห์สำเร็จ จะนำค่ามากรอกใส่ตัวแปรหน้าจอ UI ให้อัตโนมัติ
                if (result != null)
                {
                    Amount = result.Amount;
                    SelectedType = result.Type;
                    SubCategory = result.SubCategory;
                    Note = result.Note;

                    await Shell.Current.DisplayAlert(
                        "AI ทำงานสำเร็จ",
                        $"จับใจความได้ว่า:\nหมวด: {SubCategory}\nยอด: ฿{Amount}\nบันทึก: {Note}",
                        "เยี่ยม");
                }
                else
                {
                    await Shell.Current.DisplayAlert("ข้อผิดพลาด", "AI ไม่สามารถวิเคราะห์เสียงนี้ได้ครับ", "ตกลง");
                }

                IsScanning = false; // ปิดโหลดดิ้ง
            }
        }

        /// <summary>
        /// [ฟังก์ชันสแกนสลิปโอนเงิน (OCR)]
        /// เปิดคลังภาพ เลือกรูป แล้วส่งให้ AI ตรวจจับตัวเลขและวันที่
        /// </summary>
        [RelayCommand]
        public async Task AutoScanReceiptAsync()
        {
            try
            {
                // เปิดหน้าต่างระบบมือถือให้ผู้ใช้เลือกรูป
                var photo = await MediaPicker.Default.PickPhotoAsync();

                if (photo != null)
                {
                    IsScanning = true; // เปิดโหลดดิ้ง

                    // ตั้งชื่อไฟล์รูปใหม่ด้วย Guid เพื่อไม่ให้ชื่อซ้ำกัน แล้วก๊อปปี้มาเซฟในพื้นที่แอป
                    string uniqueFileName = $"{Guid.NewGuid()}_{photo.FileName}";
                    string localFilePath = Path.Combine(FileSystem.AppDataDirectory, uniqueFileName);

                    using Stream sourceStream = await photo.OpenReadAsync();
                    using FileStream localFileStream = File.Create(localFilePath);

                    await sourceStream.CopyToAsync(localFileStream);
                    localFileStream.Close();

                    ReceiptImagePath = localFilePath; // จำที่อยู่รูปไว้เผื่อผู้ใช้ต้องการดูย้อนหลัง

                    // 🌟 ส่งที่อยู่รูปภาพไปให้ Python AI ทำการสแกนหาข้อความ
                    var result = await _aiScanner.ScanReceiptAsync(ReceiptImagePath);

                    // นำข้อมูลที่สกัดได้มากรอกลงตัวแปรหน้าจอ
                    Amount = result.Amount;
                    Date = result.Date;
                    Note = result.SuggestedNote;

                    IsScanning = false; // ปิดโหลดดิ้ง

                    await Shell.Current.DisplayAlert("AI Scanner", result.SuggestedNote, "เยี่ยมเลย");
                }
            }
            catch (Exception ex)
            {
                IsScanning = false;
                await Shell.Current.DisplayAlert("ข้อผิดพลาด", $"สแกนไม่สำเร็จ: {ex.Message}", "ตกลง");
            }
        }

        /// <summary>
        /// [ฟังก์ชันบันทึกรายการลงระบบ]
        /// เซฟข้อมูลลง SQLite, หักเงินจากกระเป๋า, และระบบเพิ่มแต้ม (EXP) เพื่อปลดล็อกของรางวัล
        /// </summary>
        [RelayCommand]
        public async Task SaveTransactionAsync()
        {
            // ตรวจสอบความถูกต้องของข้อมูลเบื้องต้น
            if (Amount <= 0 || SelectedPocket == null)
            {
                await Shell.Current.DisplayAlert("แจ้งเตือน", "กรุณากรอกจำนวนเงินและเลือกกระเป๋า", "ตกลง");
                return;
            }

            // 1. สร้าง Record ใหม่
            var transaction = new TransactionRecord
            {
                Amount = Amount,
                Type = SelectedType,
                PocketId = SelectedPocket.Id,
                SubCategory = SubCategory,
                Date = Date,
                Note = Note,
                ReceiptImagePath = ReceiptImagePath
            };

            // 2. ปรับตัวเลขยอดเงินในกระเป๋าที่ถูกเลือก
            if (SelectedType == "Expense")
                SelectedPocket.CurrentBalance -= Amount;
            else
                SelectedPocket.CurrentBalance += Amount;

            // 3. เซฟลงฐานข้อมูล
            await _databaseService.SavePocketAsync(SelectedPocket);
            await _databaseService.SaveTransactionAsync(transaction);

            // ==========================================
            // 🌟 ระบบ Gamification: แจกค่าประสบการณ์ (EXP)
            // ==========================================
            int gainedExp = 10; // ได้รับ 10 EXP ต่อ 1 รายการ

            var profile = await _databaseService.GetUserProfileAsync();
            int oldExp = profile.TotalExp; // จำ EXP เดิมไว้เพื่อใช้เช็กการเลเวลอัป

            profile.TotalExp += gainedExp; // บวก EXP เพิ่ม
            await _databaseService.SaveUserProfileAsync(profile); // เซฟ Profile ใหม่

            // เซฟค่าใส่ Preferences เพื่อให้หน้าอื่นๆ ดึงไปโชว์ได้ง่ายๆ
            Preferences.Set("TotalExpMirror", profile.TotalExp);

            // ตรวจสอบเงื่อนไขว่าเลเวลอัปและปลดล็อกฟีเจอร์ใหม่หรือไม่
            bool unlockedAiNow = oldExp < 30 && profile.TotalExp >= 30;
            bool unlockedMonthlyNow = oldExp < 50 && profile.TotalExp >= 50;

            // ถ้ามีการปลดล็อก ให้รีเฟรชหน้า AppShell เพื่ออัปเดตเมนูบาร์ด่วน
            if (Shell.Current is AppShell appShell)
            {
                appShell.RefreshAnalysisRewardState();
            }

            // เตรียมข้อความแจ้งเตือนความสำเร็จ
            string alertMsg = $"บันทึกรายการเรียบร้อย\n✨ ได้รับ +{gainedExp} EXP!";

            if (unlockedAiNow)
            {
                alertMsg += "\n🎁 มีรางวัลใหม่ที่หน้า วิเคราะห์";
            }

            if (unlockedMonthlyNow)
            {
                alertMsg += "\n🎁 ปลดล็อกของรางวัลใหม่ที่หน้า วิเคราะห์";
            }

            // แจ้งเตือน และสั่งให้แอปเด้งกลับไปหน้าก่อนหน้า (GoBack)
            await Shell.Current.DisplayAlert("สำเร็จ", alertMsg, "ตกลง");
            await Shell.Current.GoToAsync("..");
        }
    }
}