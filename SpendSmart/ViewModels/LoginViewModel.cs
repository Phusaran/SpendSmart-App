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
                    var isAvailable = await CrossFingerprint.Current.IsAvailableAsync(true);

            if (!isAvailable)
            {
                // เปลี่ยนจากการใช้ Shell.Current เป็น Application.Current.MainPage เพราะเรายังไม่ได้เข้าหน้าจอหลัก
                await Application.Current.MainPage.DisplayAlert("จำลองเข้าสู่ระบบ", "เครื่องนี้ไม่มีสแกนลายนิ้วมือ (อนุญาตให้เข้าแอปชั่วคราว)", "ตกลง");
                Application.Current.MainPage = new AppShell();
                return;
            }

            var request = new AuthenticationRequestConfiguration("ปลดล็อก SpendSmart", "กรุณาสแกนลายนิ้วมือหรือใบหน้าเพื่อเข้าใช้งาน");
            var result = await CrossFingerprint.Current.AuthenticateAsync(request);

            if (result.Authenticated)
            {
                Application.Current.MainPage = new AppShell();
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("ไม่สำเร็จ", "การยืนยันตัวตนล้มเหลว", "ตกลง");
            }
        }
    }
}