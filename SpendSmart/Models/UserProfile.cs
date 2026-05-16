using SQLite;

namespace SpendSmart.Models
{
    public class UserProfile
    {
        [PrimaryKey]
        public int Id { get; set; } = 1;

        public int TotalExp { get; set; }

        public int CurrentLevel => Math.Max(1, TotalExp / 10);

        public int DisplayExp => Math.Min(TotalExp, 50);

        public int ExpCap
        {
            get
            {
                if (TotalExp < 30)
                    return 30;

                return 50;
            }
        }

        public double LevelProgress
        {
            get
            {
                if (ExpCap <= 0)
                    return 0;

                return Math.Min(1.0, (double)DisplayExp / ExpCap);
            }
        }

        public string ExpText => $"{DisplayExp} / {ExpCap} EXP";
    }
}