namespace SpendSmart
{
    public partial class App : Application
    {
        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();

            // กำหนดให้เปิดหน้า LoginPage เป็นหน้าแรกเมื่อเข้าแอป
            MainPage = serviceProvider.GetService<Views.LoginPage>();
        }
    }
}