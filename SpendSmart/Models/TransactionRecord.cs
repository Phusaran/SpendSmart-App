using SQLite;

namespace SpendSmart.Models
{
    public class TransactionRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } // "Expense" หรือ "Income"
        public string SubCategory { get; set; }
        public DateTime Date { get; set; }
        public string Note { get; set; }

        [Indexed]
        public int PocketId { get; set; }

        public string ReceiptImagePath { get; set; } // เก็บ Path ของรูปภาพสลิป/ใบเสร็จ
    }
}