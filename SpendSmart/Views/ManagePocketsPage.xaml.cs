using SpendSmart.ViewModels;

namespace SpendSmart.Views
{
    public partial class ManagePocketsPage : ContentPage
    {
        private readonly ManagePocketsViewModel _viewModel;

        public ManagePocketsPage(ManagePocketsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }
        private void OnDragStarting(object sender, DragStartingEventArgs e)
        {
            // แอบยัดข้อมูลสมมติลงไป 1 ชิ้น เพื่อให้ Android ยอมรับการ Drop
            e.Data.Properties.Add("FixAndroidBug", "Yes");
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadPocketsAsync();
        }
    }
}