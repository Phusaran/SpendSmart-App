using SQLite;
using SpendSmart.Models;

namespace SpendSmart.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _db;

        private async Task Init()
        {
            if (_db is not null)
                return;

            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "SpendSmart.db");
            _db = new SQLiteAsyncConnection(databasePath);

            await _db.CreateTableAsync<Pocket>();
            await _db.CreateTableAsync<TransactionRecord>();
            await _db.CreateTableAsync<FinancialGoal>();
            await _db.CreateTableAsync<UserProfile>();
        }

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

            return await _db.InsertAsync(pocket);
        }

        public async Task<int> DeletePocketAsync(Pocket pocket)
        {
            await Init();
            return await _db.DeleteAsync(pocket);
        }

        public async Task DeletePocketAndTransactionsAsync(Pocket pocket)
        {
            await Init();

            if (pocket == null)
                return;

            var relatedTransactions = await _db.Table<TransactionRecord>()
                                               .Where(t => t.PocketId == pocket.Id)
                                               .ToListAsync();

            foreach (var transaction in relatedTransactions)
            {
                await _db.DeleteAsync(transaction);
            }

            await _db.DeleteAsync(pocket);
        }

        public async Task<List<TransactionRecord>> GetTransactionsAsync()
        {
            await Init();

            return await _db.Table<TransactionRecord>()
                            .OrderByDescending(t => t.Date)
                            .ToListAsync();
        }

        public async Task<int> SaveTransactionAsync(TransactionRecord transaction)
        {
            await Init();

            if (transaction.Id != 0)
                return await _db.UpdateAsync(transaction);

            return await _db.InsertAsync(transaction);
        }

        public async Task<int> DeleteTransactionAsync(TransactionRecord transaction)
        {
            await Init();
            return await _db.DeleteAsync(transaction);
        }

        public async Task DeleteTransactionAndUndoPocketAsync(TransactionRecord transaction)
        {
            await Init();

            if (transaction == null)
                return;

            var pocket = await _db.Table<Pocket>()
                                  .Where(p => p.Id == transaction.PocketId)
                                  .FirstOrDefaultAsync();

            if (pocket != null)
            {
                if (transaction.Type == "Income")
                    pocket.CurrentBalance -= transaction.Amount;
                else
                    pocket.CurrentBalance += transaction.Amount;

                if (pocket.CurrentBalance < 0)
                    pocket.CurrentBalance = 0;

                await _db.UpdateAsync(pocket);
            }

            await _db.DeleteAsync(transaction);
        }

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

            return await _db.InsertAsync(goal);
        }

        public async Task<UserProfile> GetUserProfileAsync()
        {
            await Init();

            var profile = await _db.Table<UserProfile>().FirstOrDefaultAsync();

            if (profile == null)
            {
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