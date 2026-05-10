namespace SpendSmart
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute("AddTransactionPage", typeof(Views.AddTransactionPage));
            Routing.RegisterRoute("ManagePocketsPage", typeof(Views.ManagePocketsPage));
            Routing.RegisterRoute("ChatPage", typeof(Views.ChatPage));
        }
    }
}
