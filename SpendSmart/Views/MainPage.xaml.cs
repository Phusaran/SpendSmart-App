using SpendSmart.ViewModels;

namespace SpendSmart.Views 
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel _viewModel;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // สั่งให้โหลดข้อมูลทุกครั้งที่เปิดหน้านี้ขึ้นมา
            await _viewModel.LoadDataAsync();
        }
    }
}