namespace SpendSmart.Models
{
    public class ChatMessage
    {
        public string Text { get; set; }
        public bool IsUser { get; set; }

        // 🌟 ตั้งค่า UI อัตโนมัติตามคนส่ง
        public string SenderName => IsUser ? "คุณ" : "SpendSmart AI";
        public Color BackgroundColor => IsUser ? Color.FromArgb("#D4AF37") : Color.FromArgb("#FFFFFF");
        public Color TextColor => IsUser ? Colors.White : Color.FromArgb("#2C3E50");
        public LayoutOptions Alignment => IsUser ? LayoutOptions.End : LayoutOptions.Start;
    }
}