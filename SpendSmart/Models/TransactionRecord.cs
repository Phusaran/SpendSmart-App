using SQLite;
using Microsoft.Maui.Graphics;

namespace SpendSmart.Models
{
    public class TransactionRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public decimal Amount { get; set; }

        // "Expense" or "Income"
        public string Type { get; set; }

        public string SubCategory { get; set; }

        public DateTime Date { get; set; }

        public string Note { get; set; }

        [Indexed]
        public int PocketId { get; set; }

        // Path ของรูปภาพสลิป/ใบเสร็จ
        public string ReceiptImagePath { get; set; }

        // Display amount with currency
        [Ignore]
        public string AmountDisplay
        {
            get
            {
                return $"{Amount:N2} ฿";
            }
        }

        // Auto color based on transaction type
        [Ignore]
        public Color AmountColor
        {
            get
            {
                if (Type == "Income")
                    return Color.FromArgb("#27AE60");
                if (Type == "โยกเงิน")
                    return Color.FromArgb("#3498DB");
                return Color.FromArgb("#E74C3C");
            }
        }
        [Ignore]
        public bool HasReceipt => !string.IsNullOrWhiteSpace(ReceiptImagePath);

        [Ignore]
        public bool NoReceipt => string.IsNullOrWhiteSpace(ReceiptImagePath);
    }
}