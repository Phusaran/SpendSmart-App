using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace SpendSmart.ViewModels
{
    public partial class BackupViewModel : ObservableObject
    {
        private readonly string _dbFileName = "SpendSmart.db";

        [ObservableProperty] private string _statusMessage = "พร้อมสำหรับการจัดการข้อมูล";
        [ObservableProperty] private bool _isBusy;

        [RelayCommand]
        public async Task BackupDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusMessage = "กำลังแพ็กฐานข้อมูลและรูปสลิป...";

            try
            {
                string backupZipPath = Path.Combine(FileSystem.CacheDirectory, "SpendSmart_Backup.zip");
                if (File.Exists(backupZipPath)) File.Delete(backupZipPath);

                using (var archive = ZipFile.Open(backupZipPath, ZipArchiveMode.Create))
                {
                    // 🌟 1. ดึง DB เข้า Zip แบบปลอดภัย (ทะลุการล็อคไฟล์)
                    string dbPath = Path.Combine(FileSystem.AppDataDirectory, _dbFileName);
                    if (File.Exists(dbPath))
                    {
                        var dbEntry = archive.CreateEntry(_dbFileName);
                        using (var entryStream = dbEntry.Open())
                        using (var fileStream = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            await fileStream.CopyToAsync(entryStream);
                        }
                    }

                    // 🌟 2. ดึงรูปภาพเข้า Zip แบบปลอดภัย (ทะลุการล็อคไฟล์)
                    var allFiles = Directory.GetFiles(FileSystem.AppDataDirectory);
                    foreach (var file in allFiles)
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                        {
                            var imgEntry = archive.CreateEntry(Path.GetFileName(file));
                            using (var entryStream = imgEntry.Open())
                            using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                await fileStream.CopyToAsync(entryStream);
                            }
                        }
                    }
                }

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "สำรองข้อมูล SpendSmart (รวมสลิป)",
                    File = new ShareFile(backupZipPath)
                });

                StatusMessage = "ส่งออกไฟล์ Zip เรียบร้อยแล้ว";
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"ไม่สามารถสำรองข้อมูลได้: {ex.Message}", "ตกลง");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task RestoreDataAsync()
        {
            if (IsBusy) return;

            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "เลือกไฟล์ Backup (.zip)"
                });

                if (result == null) return;

                bool confirm = await Shell.Current.DisplayAlert("คำเตือน", "ข้อมูลปัจจุบันและสลิปเดิมจะถูกเขียนทับด้วยไฟล์ Backup คุณแน่ใจหรือไม่?", "ยืนยัน", "ยกเลิก");
                if (!confirm) return;

                IsBusy = true;
                StatusMessage = "กำลังแตกไฟล์และกู้คืนข้อมูล...";

                string extractPath = Path.Combine(FileSystem.CacheDirectory, "ExtractedBackup");
                if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
                Directory.CreateDirectory(extractPath);

                string tempZipPath = Path.Combine(FileSystem.CacheDirectory, "temp_restore.zip");
                using (var stream = await result.OpenReadAsync())
                using (var newFile = File.Create(tempZipPath))
                {
                    await stream.CopyToAsync(newFile);
                }

                ZipFile.ExtractToDirectory(tempZipPath, extractPath, overwriteFiles: true);

                // 🌟 3. นำไฟล์กลับเข้าแอปแบบปลอดภัย (เขียนทับได้แม้ไฟล์เปิดอยู่)
                var extractedFiles = Directory.GetFiles(extractPath);
                foreach (var file in extractedFiles)
                {
                    string targetDest = Path.Combine(FileSystem.AppDataDirectory, Path.GetFileName(file));

                    using (var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var destStream = new FileStream(targetDest, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                    {
                        await sourceStream.CopyToAsync(destStream);
                    }
                }

                // ล้างไฟล์ขยะ
                Directory.Delete(extractPath, true);
                File.Delete(tempZipPath);

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