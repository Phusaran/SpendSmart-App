using SQLite;
using SpendSmart.Models;

namespace SpendSmart.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _db;

        // ฟังก์ชันสำหรับตั้งค่าและสร้างไฟล์ Database
        private async Task Init()
        {
            if (_db is not null)
                return;

            // กำหนดที่เก็บไฟล์ Database ในเครื่องมือถือ
            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "SpendSmart.db");
            _db = new SQLiteAsyncConnection(databasePath);

            // สร้างตารางข้อมูลจาก Models ของเรา
            await _db.CreateTableAsync<Pocket>();
            await _db.CreateTableAsync<TransactionRecord>();
            await _db.CreateTableAsync<FinancialGoal>();
            await _db.CreateTableAsync<UserProfile>();
        }

        // ==========================================
        // ส่วนจัดการข้อมูลกระเป๋าเงิน (Pockets)
        // ==========================================
        public async Task<List<Pocket>> GetPocketsAsync()
        {
            await Init();
            return await _db.Table<Pocket>().ToListAsync();
        }

        public async Task<int> SavePocketAsync(Pocket pocket)
        {
            await Init();
            if (pocket.Id != 0)
                return await _db.UpdateAsync(pocket);
            else
                return await _db.InsertAsync(pocket);
        }

        public async Task<int> DeletePocketAsync(Pocket pocket)
        {
            await Init();
            return await _db.DeleteAsync(pocket);
        }

        // ==========================================
        // ส่วนจัดการประวัติรายการ (Transactions)
        // ==========================================
        public async Task<List<TransactionRecord>> GetTransactionsAsync()
        {
            await Init();
            // เรียงลำดับจากวันที่ล่าสุดขึ้นก่อน
            return await _db.Table<TransactionRecord>().OrderByDescending(t => t.Date).ToListAsync();
        }

        public async Task<int> SaveTransactionAsync(TransactionRecord transaction)
        {
            await Init();
            if (transaction.Id != 0)
                return await _db.UpdateAsync(transaction);
            else
                return await _db.InsertAsync(transaction);
        }

        public async Task<int> DeleteTransactionAsync(TransactionRecord transaction)
        {
            await Init();
            return await _db.DeleteAsync(transaction);
        }

        // ==========================================
        // ส่วนจัดการเป้าหมายการออม (Financial Goals)
        // ==========================================
        public async Task<List<FinancialGoal>> GetGoalsAsync()
        {
            await Init();
            return await _db.Table<FinancialGoal>().ToListAsync();
        }

        public async Task<int> SaveGoalAsync(FinancialGoal goal)
        {
            await Init();
            if (goal.Id != 0)
                return await _db.UpdateAsync(goal);
            else
                return await _db.InsertAsync(goal);
        }
        public async Task<UserProfile> GetUserProfileAsync()
        {
            await Init();
            var profile = await _db.Table<UserProfile>().FirstOrDefaultAsync();
            if (profile == null)
            {
                // ถ้าเพิ่งเปิดแอปครั้งแรก ให้สร้างโปรไฟล์เลเวล 1 ให้เลย
                profile = new UserProfile { Id = 1, TotalExp = 0 };
                await _db.InsertAsync(profile);
            }
            return profile;
        }

        public async Task<int> SaveUserProfileAsync(UserProfile profile)
        {
            await Init();
            return await _db.UpdateAsync(profile);
        }
    }
}