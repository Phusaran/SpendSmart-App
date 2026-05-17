/*
=========================================================================================
[สรุปภาพรวมการทำงานของไฟล์ BackupViewModel.cs]
ไฟล์นี้คือ "ผู้จัดการระบบสำรองและกู้คืนข้อมูล" (Backup & Restore System) ซึ่งใช้เทคนิคขั้นสูง 
ในการจัดการไฟล์บนระบบปฏิบัติการมือถือ(Android/iOS) มีหน้าที่สำคัญ 2 อย่างคือ:

1. สำรองข้อมูล (BackupDataAsync): ควบรวมไฟล์ฐานข้อมูล SQLite (.db) และกวาดรูปภาพสลิปทั้งหมด 
ที่ผู้ใช้เคยบันทึกไว้ในแอปมาบีบอัดรวมกันเป็นไฟล์ `SpendSmart_Backup.zip` จากนั้นจะเปิดหน้าต่าง 
Share ของระบบมือถือ เพื่อให้ผู้ใช้ส่งไฟล์ไปเก็บไว้ใน Line, Google Drive หรือส่งเมลได้
2. กู้คืนข้อมูล (RestoreDataAsync): เปิดหน้าต่างให้ผู้ใช้เลือกไฟล์ .zip ที่เคยสำรองไว้ 
จากนั้นจะทำการแตกไฟล์ออก และเขียนทับฐานข้อมูลรวมถึงรูปสลิปปัจจุบัน เพื่อดึงข้อมูลเก่าทั้งหมดกลับมา
=========================================================================================
*/

using System.IO.Compression; 
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace SpendSmart.ViewModels
{
    public partial class BackupViewModel : ObservableObject
    {
        // กำหนดชื่อไฟล์ฐานข้อมูลหลักของแอป
        private readonly string _dbFileName = "SpendSmart.db";

        // ข้อความโต้ตอบบนหน้าจอเพื่อบอกสถานะผู้ใช้ เช่น "กำลังแพ็กข้อมูล..."
        [ObservableProperty] private string _statusMessage = "พร้อมสำหรับการจัดการข้อมูล";

        // สถานะบอกระบบว่าแอปกำลังทำงานหนักอยู่หรือไม่ (เอาไว้เปิด/ปิด วงกลมโหลดหมุนๆ บน UI)
        [ObservableProperty] private bool _isBusy;

        /// <summary>
        /// [ฟังก์ชันสั่งสำรองข้อมูล]
        /// แพ็กฐานข้อมูล + รูปสลิปทั้งหมด -> บีบอัดเข้าไฟล์ Zip -> ส่งเข้าปุ่มแชร์ข้ามแอป
        /// </summary>
        [RelayCommand]
        public async Task BackupDataAsync()
        {
            // ดักจับ: ถ้าแอปกำลังทำงานนี้อยู่แล้ว ยูสเซอร์กดซ้ำเข้ามา ให้เมินคำสั่งทันทีป้องกันงานซ้อน
            if (IsBusy) return;
            IsBusy = true; // เปิดสถานะว่ากำลังทำงานอยู่
            StatusMessage = "กำลังแพ็กฐานข้อมูลและรูปสลิป...";

            try
            {
                // กำหนดตำแหน่งวางไฟล์ Zip ชั่วคราวในโฟลเดอร์ CacheDirectory ของแอป
                string backupZipPath = Path.Combine(FileSystem.CacheDirectory, "SpendSmart_Backup.zip");

                // ถ้าระบบเจอว่าเคยมีไฟล์ Backup เก่าค้างอยู่ใน Cache ให้ลบทิ้งก่อนเพื่อเตรียมทำอันใหม่
                if (File.Exists(backupZipPath)) File.Delete(backupZipPath);

                // เริ่มต้นกระบวนการสร้างไฟล์ Zip บีบอัดข้อมูล
                using (var archive = ZipFile.Open(backupZipPath, ZipArchiveMode.Create))
                {
                    // 🌟 สเตปที่ 1: ดึงไฟล์ตัวฐานข้อมูล SQLite (.db) เข้าสู่ไฟล์ Zip
                    string dbPath = Path.Combine(FileSystem.AppDataDirectory, _dbFileName);
                    if (File.Exists(dbPath))
                    {
                        // สร้างที่อยู่ภายในไฟล์ Zip สำหรับเก็บฐานข้อมูล
                        var dbEntry = archive.CreateEntry(_dbFileName);

                        using (var entryStream = dbEntry.Open())
                        // ใช้ FileShare.ReadWrite เพื่อทะลุการล็อคไฟล์ ช่วยให้ดึงข้อมูลคัดลอกได้แม้แอปกำลังเปิดใช้งานฐานข้อมูลอยู่
                        using (var fileStream = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            // คัดลอกเนื้อหาทั้งหมดจากไฟล์ฐานข้อมูล เข้าไปในไฟล์ Zip
                            await fileStream.CopyToAsync(entryStream);
                        }
                    }

                    // 🌟 สเตปที่ 2: กวาดรูปภาพสลิปทั้งหมด (.png, .jpg, .jpeg) เข้าสู่ไฟล์ Zip
                    var allFiles = Directory.GetFiles(FileSystem.AppDataDirectory);
                    foreach (var file in allFiles)
                    {
                        string ext = Path.GetExtension(file).ToLower(); // ดึงนามสกุลไฟล์มาเช็ก
                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                        {
                            // นำรูปภาพแต่ละรูปแอดเข้าไปรวมร่างในไฟล์ Zip
                            var imgEntry = archive.CreateEntry(Path.GetFileName(file));
                            using (var entryStream = imgEntry.Open())
                            using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                await fileStream.CopyToAsync(entryStream);
                            }
                        }
                    }
                }

                // 🌟 สเตปที่ 3: เรียกฟังก์ชันของระบบมือถือเปิดหน้าต่าง Share ทันทีเพื่อให้ผู้ใช้เลือกส่งไฟล์
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "สำรองข้อมูล SpendSmart (รวมสลิป)",
                    File = new ShareFile(backupZipPath)
                });

                StatusMessage = "ส่งออกไฟล์ Zip เรียบร้อยแล้ว";
            }
            catch (Exception ex)
            {
                // หากเกิดข้อผิดพลาดระหว่างกระบวนการ ให้ขึ้นป๊อปอัพแจ้งเตือนสาเหตุ
                await Shell.Current.DisplayAlert("Error", $"ไม่สามารถสำรองข้อมูลได้: {ex.Message}", "ตกลง");
            }
            finally
            {
                IsBusy = false; // ปิดสถานะการทำงานหนัก
            }
        }

        /// <summary>
        /// [ฟังก์ชันกู้คืนข้อมูล]
        /// เปิด File Picker ให้ผู้ใช้เลือกไฟล์ Zip -> แตกไฟล์ออก -> เขียนทับไฟล์ปัจจุบันในแอป
        /// </summary>
        [RelayCommand]
        public async Task RestoreDataAsync()
        {
            if (IsBusy) return;

            try
            {
                // 🌟 สเตปที่ 1: เรียกเครื่องมือระบบเปิดหน้าต่างให้ผู้ใช้ไปเลือกไฟล์ .zip จากในเครื่องมา
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "เลือกไฟล์ Backup (.zip)"
                });

                // ถ้าผู้ใช้กดปิดหน้าต่างเลือกไฟล์โดยไม่ได้เลือกอะไรเลย ให้จบฟังก์ชันทันที
                if (result == null) return;

                // ขึ้นเตือนความปลอดภัยเพราะข้อมูลปัจจุบันจะโดนลบหายหมดและแทนที่ด้วยข้อมูลเก่า
                bool confirm = await Shell.Current.DisplayAlert("คำเตือน", "ข้อมูลปัจจุบันและสลิปเดิมจะถูกเขียนทับด้วยไฟล์ Backup คุณแน่ใจหรือไม่?", "ยืนยัน", "ยกเลิก");
                if (!confirm) return;

                IsBusy = true;
                StatusMessage = "กำลังแตกไฟล์และกู้คืนข้อมูล...";

                // กำหนดโฟลเดอร์สำหรับแตกไฟล์ออกชั่วคราวในโฟลเดอร์แคช
                string extractPath = Path.Combine(FileSystem.CacheDirectory, "ExtractedBackup");
                if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true); // ล้างของเก่าออกก่อนถ้ามีค้าง
                Directory.CreateDirectory(extractPath);

                // คัดลอกไฟล์สตรีมจากไฟล์ Zip ที่เลือกเข้ามา ยัดลงไฟล์ชั่วคราว
                string tempZipPath = Path.Combine(FileSystem.CacheDirectory, "temp_restore.zip");
                using (var stream = await result.OpenReadAsync())
                using (var newFile = File.Create(tempZipPath))
                {
                    await stream.CopyToAsync(newFile);
                }

                // สั่งแตกไฟล์ทั้งหมด (Unzip) ออกมาวางที่โฟลเดอร์ชั่วคราว
                ZipFile.ExtractToDirectory(tempZipPath, extractPath, overwriteFiles: true);

                // 🌟 สเตปที่ 2: นำไฟล์ที่แตกออกมา ย้ายกลับเข้าไปเขียนทับที่อยู่จริงในแอปแบบปลอดภัย
                var extractedFiles = Directory.GetFiles(extractPath);
                foreach (var file in extractedFiles)
                {
                    // กำหนดตำแหน่งปลายทางจริงที่จะเอาไฟล์ไปวางทับ (AppDataDirectory)
                    string targetDest = Path.Combine(FileSystem.AppDataDirectory, Path.GetFileName(file));

                    // ใช้ลอจิกเปิด FileStream คู่กับ FileShare.ReadWrite เพื่อให้สามารถเขียนทับฐานข้อมูล
                    // และไฟล์รูปสลิปเก่าได้ทันที แม้แอปจะกำลังเปิดหรือใช้งานไฟล์เหล่านั้นอยู่ก็ตาม
                    using (var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var destStream = new FileStream(targetDest, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                    {
                        await sourceStream.CopyToAsync(destStream);
                    }
                }

                // 🌟 สเตปที่ 3: เคลียร์ทำความสะอาด ลบโฟลเดอร์และไฟล์ขยะชั่วคราวทิ้งทั้งหมดหลังงานเสร็จ
                Directory.Delete(extractPath, true);
                File.Delete(tempZipPath);

                // แจ้งเตือนผู้ใช้ให้ปิดแอปแล้วเปิดใหม่ เพื่อให้ SQLite โหลดโครงสร้างไฟล์ที่มาแทนที่ใหม่ทำงาานได้ถูกต้อง
                await Shell.Current.DisplayAlert("สำเร็จ", "กู้คืนข้อมูลพร้อมรูปสลิปเรียบร้อยแล้ว!\nกรุณาปิดแอปแล้วเปิดใหม่ 1 ครั้งเพื่อให้ข้อมูลทั้งหมดอัปเดตครับ", "ตกลง");
                StatusMessage = "กู้คืนข้อมูลสำเร็จ";
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"เกิดข้อผิดพลาดในการกู้คืน: {ex.Message}", "ตกลง");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}