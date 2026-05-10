using SpendSmart.ViewModels;

namespace SpendSmart.Views
{
    public partial class ChatPage : ContentPage
    {
        private readonly ChatViewModel _viewModel;

        public ChatPage(ChatViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadContextAsync(); // โหลดข้อมูลการเงินไปรอไว้
        }
    }
}