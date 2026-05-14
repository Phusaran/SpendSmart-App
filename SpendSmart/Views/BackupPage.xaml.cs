using SpendSmart.ViewModels;
using Microsoft.Maui.Controls;

namespace SpendSmart.Views
{
    public partial class BackupPage : ContentPage
    {
        public BackupPage(BackupViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}