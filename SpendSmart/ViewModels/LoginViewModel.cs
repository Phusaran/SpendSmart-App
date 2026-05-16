using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;

namespace SpendSmart.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        [RelayCommand]
        public async Task AuthenticateAsync()
        {
            try
            {
                var isAvailable = await CrossFingerprint.Current.IsAvailableAsync(true);

                if (!isAvailable)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "จำลองเข้าสู่ระบบ",
                        "เครื่องนี้ไม่พร้อมใช้งานสแกนนิ้วมือ จึงอนุญาตให้เข้าแอปชั่วคราว",
                        "ตกลง");

                    Application.Current.MainPage = new AppShell();
                    return;
                }

                var request = new AuthenticationRequestConfiguration(
                    "ปลดล็อก SpendSmart",
                    "กรุณาสแกนลายนิ้วมือหรือใบหน้าเพื่อเข้าใช้งาน")
                {
                    AllowAlternativeAuthentication = true
                };

                var result = await CrossFingerprint.Current.AuthenticateAsync(request);

                if (result.Authenticated)
                {
                    Application.Current.MainPage = new AppShell();
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "ไม่สำเร็จ",
                        "การยืนยันตัวตนล้มเหลว หรือถูกยกเลิก",
                        "ตกลง");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "เกิดข้อผิดพลาด",
                    $"ระบบสแกนนิ้วมือมีปัญหา: {ex.Message}",
                    "ตกลง");

                Application.Current.MainPage = new AppShell();
            }
        }
    }
}