using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using SpendSmart.ViewModels;

namespace SpendSmart.Views
{
    public partial class DashboardPage : ContentPage
    {
        private readonly DashboardViewModel _viewModel;

        public DashboardPage(DashboardViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel; // เชื่อมหน้าจอเข้ากับข้อมูล
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // สั่งให้โหลดข้อมูลและเรียก AI วิเคราะห์ทุกครั้งที่สลับมาหน้านี้
            await _viewModel.LoadDashboardDataAsync();
        }
    }
}