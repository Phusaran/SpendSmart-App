using SQLite;

namespace SpendSmart.Models
{
    public class Pocket
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; }

        // 🌟 เพิ่มบรรทัดนี้: แยกประเภทกระเป๋า
        public string PocketType { get; set; } = "Saving"; // "Saving" (เงินเก็บ) หรือ "Spending" (ใช้จ่าย)

        public decimal TargetBudget { get; set; }
        public decimal CurrentBalance { get; set; }
        public string Icon { get; set; } = "💰";
        public string ThemeColor { get; set; } = "#00E5FF";
        public bool IsDefault { get; set; }
        public DateTime? TargetDate { get; set; }
        public decimal MonthlySavingAim { get; set; }
        public decimal GoalAmount { get; set; }

        // ซ่อนหลอดเป้าหมายถ้าเป็นกระเป๋าใช้จ่าย (GoalAmount = 0)
        public double Progress => GoalAmount > 0 ? (double)(CurrentBalance / GoalAmount) : 0;
        public string ProgressText => GoalAmount > 0 ? $"฿{CurrentBalance:N0} / ฿{GoalAmount:N0}" : "กระเป๋าสำหรับใช้จ่าย";
    }
}