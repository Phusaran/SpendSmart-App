using SQLite;

namespace SpendSmart.Models
{
    public class UserProfile
    {
        [PrimaryKey]
        public int Id { get; set; } = 1; // มีแค่ Record เดียวเสมอ
        public int TotalExp { get; set; }
        public int CurrentLevel => (TotalExp / 100) + 1; // ทุกๆ 100 EXP = 1 เลเวล

        // คำนวณหลอด EXP ปัจจุบัน (เศษที่ยังไม่เต็มร้อย)
        public double LevelProgress => (TotalExp % 100) / 100.0;
        public string ExpText => $"{TotalExp % 100} / 100 EXP";
    }
}