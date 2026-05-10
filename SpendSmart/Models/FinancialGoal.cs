using SQLite;

namespace SpendSmart.Models
{
    public class FinancialGoal
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Title { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal CurrentSavedAmount { get; set; }
        public DateTime TargetDate { get; set; }
        public decimal RecommendedMonthlySaving { get; set; }
    }
}