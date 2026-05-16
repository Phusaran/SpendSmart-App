using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.Audio;
using SpendSmart.Models;
using SpendSmart.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.Media;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace SpendSmart.ViewModels
{
    public partial class AddTransactionViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly AIReceiptScannerService _aiScanner;
        private readonly AIVoiceScannerService _voiceScanner;
        private readonly IAudioManager _audioManager;
        private IAudioRecorder _audioRecorder;

        [ObservableProperty] private decimal _amount;
        [ObservableProperty] private string _selectedType = "Expense";
        [ObservableProperty] private Pocket _selectedPocket;
        [ObservableProperty] private string _subCategory;
        [ObservableProperty] private DateTime _date = DateTime.Now;
        [ObservableProperty] private string _note;
        [ObservableProperty] private string _receiptImagePath;
        [ObservableProperty] private bool _isRecording;
        [ObservableProperty] private string _voiceButtonText = "🎙️ กดเพื่อพูดจดรายจ่าย";
        [ObservableProperty] private Color _voiceButtonColor = Color.FromArgb("#3498DB");
        [ObservableProperty] private bool _isScanning;

        public ObservableCollection<Pocket> Pockets { get; set; } = new();
        public List<string> TransactionTypes { get; } = new List<string> { "Expense", "Income" };

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

        public async Task LoadPocketsAsync()
        {
            var pockets = await _databaseService.GetPocketsAsync();

            Pockets.Clear();

            foreach (var p in pockets)
            {
                Pockets.Add(p);

                if (p.IsDefault || p.Name.ToLower().Contains("main"))
                {
                    SelectedPocket = p;
                }
            }

            if (SelectedPocket == null && Pockets.Count > 0)
            {
                SelectedPocket = Pockets[0];
            }
        }

        [RelayCommand]
        public async Task ToggleVoiceRecordAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();

            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Microphone>();

                if (status != PermissionStatus.Granted)
                {
                    await Shell.Current.DisplayAlert("แจ้งเตือน", "กรุณาอนุญาตให้แอปใช้ไมโครโฟนก่อนครับ", "ตกลง");
                    return;
                }
            }

            if (!IsRecording)
            {
                _audioRecorder = _audioManager.CreateRecorder();
                await _audioRecorder.StartAsync();

                IsRecording = true;
                VoiceButtonText = "🛑 กำลังฟัง... (กดเพื่อหยุด)";
                VoiceButtonColor = Color.FromArgb("#E74C3C");
            }
            else
            {
                var recordedAudio = await _audioRecorder.StopAsync();

                IsRecording = false;
                VoiceButtonText = "🎙️ กดเพื่อพูดจดรายจ่าย";
                VoiceButtonColor = Color.FromArgb("#3498DB");

                IsScanning = true;

                var audioStream = recordedAudio.GetAudioStream();

                var filePath = Path.Combine(FileSystem.CacheDirectory, "voice_expense.wav");

                using (var fileStream = File.Create(filePath))
                {
                    await audioStream.CopyToAsync(fileStream);
                }

                var result = await _voiceScanner.AnalyzeVoiceAsync(filePath);

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

                IsScanning = false;
            }
        }

        [RelayCommand]
        public async Task AutoScanReceiptAsync()
        {
            try
            {
                var photo = await MediaPicker.Default.PickPhotoAsync();

                if (photo != null)
                {
                    IsScanning = true;

                    string uniqueFileName = $"{Guid.NewGuid()}_{photo.FileName}";
                    string localFilePath = Path.Combine(FileSystem.AppDataDirectory, uniqueFileName);

                    using Stream sourceStream = await photo.OpenReadAsync();
                    using FileStream localFileStream = File.Create(localFilePath);

                    await sourceStream.CopyToAsync(localFileStream);
                    localFileStream.Close();

                    ReceiptImagePath = localFilePath;

                    var result = await _aiScanner.ScanReceiptAsync(ReceiptImagePath);

                    Amount = result.Amount;
                    Date = result.Date;
                    Note = result.SuggestedNote;

                    IsScanning = false;

                    await Shell.Current.DisplayAlert("AI Scanner", result.SuggestedNote, "เยี่ยมเลย");
                }
            }
            catch (Exception ex)
            {
                IsScanning = false;
                await Shell.Current.DisplayAlert("ข้อผิดพลาด", $"สแกนไม่สำเร็จ: {ex.Message}", "ตกลง");
            }
        }

        [RelayCommand]
        public async Task SaveTransactionAsync()
        {
            if (Amount <= 0 || SelectedPocket == null)
            {
                await Shell.Current.DisplayAlert("แจ้งเตือน", "กรุณากรอกจำนวนเงินและเลือกกระเป๋า", "ตกลง");
                return;
            }

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

            if (SelectedType == "Expense")
                SelectedPocket.CurrentBalance -= Amount;
            else
                SelectedPocket.CurrentBalance += Amount;

            await _databaseService.SavePocketAsync(SelectedPocket);
            await _databaseService.SaveTransactionAsync(transaction);

            int gainedExp = 10;

            var profile = await _databaseService.GetUserProfileAsync();
            int oldExp = profile.TotalExp;

            profile.TotalExp += gainedExp;

            await _databaseService.SaveUserProfileAsync(profile);

            Preferences.Set("TotalExpMirror", profile.TotalExp);

            bool unlockedAiNow = oldExp < 30 && profile.TotalExp >= 30;
            bool unlockedMonthlyNow = oldExp < 50 && profile.TotalExp >= 50;

            if (Shell.Current is AppShell appShell)
            {
                appShell.RefreshAnalysisRewardState();
            }

            string alertMsg = $"บันทึกรายการเรียบร้อย\n✨ ได้รับ +{gainedExp} EXP!";

            if (unlockedAiNow)
            {
                alertMsg += "\n🎁 มีรางวัลใหม่ที่หน้า วิเคราะห์";
            }

            if (unlockedMonthlyNow)
            {
                alertMsg += "\n🎁 ปลดล็อกของรางวัลใหม่ที่หน้า วิเคราะห์";
            }

            await Shell.Current.DisplayAlert("สำเร็จ", alertMsg, "ตกลง");
            await Shell.Current.GoToAsync("..");
        }
    }
}