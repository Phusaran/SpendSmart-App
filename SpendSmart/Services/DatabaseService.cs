/*
=========================================================================================
[สรุปภาพรวมการทำงานของคลาส DatabaseService]
ไฟล์นี้คือ "ผู้จัดการฐานข้อมูลหลัก (Data Access Layer)" ของแอป SpendSmart ทำหน้าที่ติดต่อประสานงาน
กับฐานข้อมูลเครื่อง local ในรูปแบบ SQLiteAsyncConnection (ทำงานแบบอาร์ซิงโครนัส ไม่ทำให้แอปหน่วง)

ความสำคัญและหน้าที่หลักมี 4 ส่วน:
1. สร้างฐานข้อมูลและตาราง (Database Initialization): สร้างไฟล์ SpendSmart.db และตารางข้อมูลทั้ง 4 
   (Pocket, TransactionRecord, FinancialGoal, UserProfile) ทันทีที่แอปเปิดใช้งานครั้งแรก
2. ระบบจัดการกระเป๋าเงินพ่วงประวัติ (Cascade Delete): มีลอจิกดักจับว่า ถ้าลบกระเป๋าเงินใบไหน 
   ระบบจะต้องตามไปลบประวัติการเงินทั้งหมดที่ผูกกับกระเป๋าใบนั้น พร้อมสั่งลบไฟล์ภาพสลิปในเครื่องทิ้งด้วยเพื่อไม่ให้รกเครื่อง
3. ระบบคืนยอดเงินอัจฉริยะ (Undo Transaction): เมื่อผู้ใช้สั่งลบรายการประวัติในหน้า History 
   ระบบจะคำนวณย้อนกลับ (Undo) หากรายการที่ลบเป็น "รายจ่าย" จะนำเงินไปบวกคืนกระเป๋า | หากเป็น "รายรับ" จะหักออก 
   และหากเป็น "การโยกเงิน" จะทำการดึงเงินจากกระเป๋าปลายทางกลับคืนสู้กระเป๋าต้นทางให้โดยอัตโนมัติ
4. จัดการคะแนนโปรไฟล์ (Profile States): คอยดูแลการดึงและบันทึกแต้มเลเวล EXP ของผู้ใช้
=========================================================================================
*/

using SQLite;
using SpendSmart.Models;

namespace SpendSmart.Services
{
    public class DatabaseService
    {
        // ตัวแปรเชื่อมต่อฐานข้อมูล SQLite แบบเปิดค้างไว้เพื่อรอรับคำสั่ง
        private SQLiteAsyncConnection _db;

        /// <summary>
        /// [ฟังก์ชันเริ่มต้นและสร้างฐานข้อมูล (Internal Init)]
        /// ทำหน้าที่เช็กและสร้างไฟล์ฐานข้อมูลพร้อมตารางต่างๆ ในครั้งแรก
        /// </summary>
        private async Task Init()
        {
            // เซฟตี้: ถ้าฐานข้อมูลเคยถูกเปิดใช้งานและเชื่อมต่ออยู่แล้ว ให้ข้ามฟังก์ชันนี้ไปเลย (ป้องกันการเชื่อมต่อซ้ำซ้อน)
            if (_db is not null)
                return;

            // 1. กำหนดตำแหน่งที่ตั้งไฟล์ฐานข้อมูล โดยบันทึกไว้ในโฟลเดอร์ AppDataDirectory ของระบบปฏิบัติการมือถือ
            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "SpendSmart.db");

            // 2. เริ่มต้นเปิดท่อเชื่อมต่อกับไฟล์ฐานข้อมูลตามที่อยู่ด้านบน
            _db = new SQLiteAsyncConnection(databasePath);

            // 3. สั่งสร้างตาราง (Tables) ทั้ง 4 ตารางตามโมเดลที่เราออกแบบไว้ (ถ้าเคยสร้างแล้ว SQLite จะข้ามให้อัตโนมัติไม่ทำซ้ำ)
            await _db.CreateTableAsync<Pocket>();             // ตารางกระเป๋าเงิน
            await _db.CreateTableAsync<TransactionRecord>(); // ตารางประวัติธุรกรรม
            await _db.CreateTableAsync<FinancialGoal>();     // ตารางเป้าหมายการเงิน
            await _db.CreateTableAsync<UserProfile>();       // ตารางแต้มโปรไฟล์ผู้ใช้
        }

        // ==========================================================================
        // 🌟 กลุ่มฟังก์ชันจัดการ "กระเป๋าเงิน (Pocket CRUD)"
        // ==========================================================================

        /// <summary>
        /// ดึงข้อมูลกระเป๋าเงินทั้งหมดออกมาจากฐานข้อมูล
        /// </summary>
        public async Task<List<Pocket>> GetPocketsAsync()
        {
            await Init(); // เรียกความพร้อมฐานข้อมูล
            return await _db.Table<Pocket>().ToListAsync(); // เปลี่ยนตาราง Pocket ให้เป็น List ของ C# ส่งกลับไป
        }

        /// <summary>
        /// บันทึกหรืออัปเดตข้อมูลกระเป๋าเงิน
        /// </summary>
        public async Task<int> SavePocketAsync(Pocket pocket)
        {
            await Init();
            // ลอจิกตรวจสอบ: ถ้า Id ไม่ใช่ 0 แสดงว่าเป็นกระเป๋าเดิมที่มีอยู่แล้ว -> สั่ง Update ข้อมูลทับ
            if (pocket.Id != 0)
                return await _db.UpdateAsync(pocket);

            // แต่ถ้า Id เป็น 0 แสดงว่าเป็นกระเป๋าใบใหม่เอี่ยม -> สั่ง Insert เพิ่มแถวลงตาราง
            return await _db.InsertAsync(pocket);
        }

        /// <summary>
        /// ลบกระเป๋าเงินแบบปกติ (ลบเฉพาะตัวกระเป๋า)
        /// </summary>
        public async Task<int> DeletePocketAsync(Pocket pocket)
        {
            await Init();
            return await _db.DeleteAsync(pocket);
        }

        /// <summary>
        /// 🌟 [ลบกระเป๋าเงินแบบ Cascade]
        /// ลบกระเป๋าเงินใบที่เลือก พร้อมล้างประวัติธุรกรรมและไฟล์รูปสลิปทั้งหมดที่เกี่ยงข้อง (ป้องกันประวัติผีค้างในระบบ)
        /// </summary>
        public async Task DeletePocketAndTransactionsAsync(Pocket pocket)
        {
            await Init();
            if (pocket == null) return; // เซฟตี้ดักค่าว่าง

            // 1. ใช้ LINQ ค้นหาทุกรายการในตารางประวัติ ที่กระเป๋าใบนี้ไปเกี่ยวข้อง ไม่ว่าจะในฐานะ "ต้นทาง (PocketId)" หรือ "ปลายทาง (TargetPocketId)"
            var relatedTransactions = await _db.Table<TransactionRecord>()
                                               .Where(t => t.PocketId == pocket.Id || t.TargetPocketId == pocket.Id)
                                               .ToListAsync();

            // 2. วนลูปสั่งทำความสะอาดข้อมูลที่ละรายการ
            foreach (var transaction in relatedTransactions)
            {
                // ตรวจสอบ: ถ้าประวัตินี้เคยมีรูปภาพสลิปแนบอยู่ และไฟล์นั้นมีอยู่จริงบนเมมโมรี่เครื่องมือถือ
                if (!string.IsNullOrEmpty(transaction.ReceiptImagePath) && File.Exists(transaction.ReceiptImagePath))
                {
                    // สั่งลบไฟล์ภาพสลิปขยะชิ้นนั้นทิ้งทันที เพื่อประหยัดพื้นที่ความจุในมือถือของผู้ใช้
                    try { File.Delete(transaction.ReceiptImagePath); } catch { /* ignore ถ้าลบไม่ผ่านให้ข้ามไป */ }
                }

                // สั่งลบแถวประวัติธุรกรรมนี้ออกจากตารางฐานข้อมูล
                await _db.DeleteAsync(transaction);
            }

            // 3. หลังจากเคลียร์ประวัติพ่วงเสร็จหมดแล้ว จึงค่อยสั่งลบตัวกระเป๋าเงินใบนั้นทิ้งเป็นขั้นตอนสุดท้าย
            await _db.DeleteAsync(pocket);
        }

        // ==========================================================================
        // 🌟 กลุ่มฟังก์ชันจัดการ "ประวัติธุรกรรม (Transaction CRUD)"
        // ==========================================================================

        /// <summary>
        /// ดึงประวัติธุรกรรมทั้งหมด โดยเรียงลำดับจาก "วันที่ล่าสุด" ลงไป (วันที่ใหม่ขึ้นก่อนเสมอ)
        /// </summary>
        public async Task<List<TransactionRecord>> GetTransactionsAsync()
        {
            await Init();
            return await _db.Table<TransactionRecord>()
                            .OrderByDescending(t => t.Date) // เรียงลำดับจากใหม่ไปเก่า (Descending)
                            .ToListAsync();
        }

        /// <summary>
        /// บันทึกหรืออัปเดตประวัติธุรกรรม (Insert หากเป็นรายการใหม่ / Update หากเป็นรายการเดิม)
        /// </summary>
        public async Task<int> SaveTransactionAsync(TransactionRecord transaction)
        {
            await Init();
            if (transaction.Id != 0)
                return await _db.UpdateAsync(transaction);
            return await _db.InsertAsync(transaction);
        }

        /// <summary>
        /// ลบประวัติธุรกรรมแบบปกติออกตรงๆ
        /// </summary>
        public async Task<int> DeleteTransactionAsync(TransactionRecord transaction)
        {
            await Init();
            return await _db.DeleteAsync(transaction);
        }

        /// <summary>
        /// 🌟 [ระบบลบประวัติพร้อมคืนเงินอัจฉริยะ (Undo Transaction)]
        /// ทำหน้าที่ลบรายการประวัติพร้อมคำนวณเงินไหลย้อนกลับเข้ากระเป๋าเดิมให้ถูกต้อง รองรับทั้ง รายรับ, รายจ่าย และการโยกเงิน
        /// </summary>
        public async Task DeleteTransactionAndUndoPocketAsync(TransactionRecord transaction)
        {
            await Init();
            if (transaction == null) return;

            // ----------------------------------------------------
            // 🛠️ กรณีที่ 1: ลบประวัติการ "โยกเงินระหว่างกระเป๋า"
            // ลอจิกคืนเงิน: ต้องเอาเงินไปคืนให้กระเป๋าต้นทาง และตามไปดักหักเงินคืนออกจากกระเป๋าปลายทาง
            // ----------------------------------------------------
            if (transaction.Type == "โยกเงิน")
            {
                // ดึงข้อมูลกระเป๋าต้นทางและปลายทางขึ้นมาจากตารางฐานข้อมูล
                var sourcePocket = await _db.Table<Pocket>().Where(p => p.Id == transaction.PocketId).FirstOrDefaultAsync();
                var targetPocket = await _db.Table<Pocket>().Where(p => p.Id == transaction.TargetPocketId).FirstOrDefaultAsync();

                // ออมเงินคืนให้กระเป๋าต้นทาง (เพราะตอนโอนโดนหักไป ต้องเอาไปบวกคืนให้)
                if (sourcePocket != null)
                {
                    sourcePocket.CurrentBalance += transaction.Amount;
                    await _db.UpdateAsync(sourcePocket);
                }

                // ตามไปหักเงินออกจากกระเป๋าปลายทาง (เพราะตอนโอนเคยได้รับเงินไป ต้องไปหักออก)
                if (targetPocket != null)
                {
                    targetPocket.CurrentBalance -= transaction.Amount;
                    if (targetPocket.CurrentBalance < 0) targetPocket.CurrentBalance = 0; // ดักไม่ให้ยอดกระเป๋าติดลบ
                    await _db.UpdateAsync(targetPocket);
                }
            }
            // ----------------------------------------------------
            // 🛠️ กรณีที่ 2: ลบประวัติ รายรับ (Income) หรือ รายจ่าย (Expense) ปกติ
            // ----------------------------------------------------
            else
            {
                // ดึงกระเป๋าเงินใบที่เกี่ยวข้องกับรายการนี้ขึ้นมา
                var pocket = await _db.Table<Pocket>().Where(p => p.Id == transaction.PocketId).FirstOrDefaultAsync();
                if (pocket != null)
                {
                    // ถ้ารายการที่จะลบคือ "รายรับ" -> แปลว่าต้องไปหักยอดเงินในกระเป๋าออก
                    if (transaction.Type == "Income")
                        pocket.CurrentBalance -= transaction.Amount;
                    // ถ้ารายการที่จะลบคือ "รายจ่าย" -> แปลว่าต้องนำยอดเงินบวกคืนเข้ากระเป๋าไป
                    else
                        pocket.CurrentBalance += transaction.Amount;

                    if (pocket.CurrentBalance < 0) pocket.CurrentBalance = 0; // ป้องกันกระเป๋าเงินติดลบ
                    await _db.UpdateAsync(pocket); // บันทึกยอดเงินใหม่ที่แก้ไขแล้วลง SQLite
                }
            }

            // 🌟 ลบรูปภาพสลิปที่แนบอยู่กับรายการนี้ทิ้งด้วยก่อนลบตัวตนประวัติออกจาก DB เพื่อเคลียร์ไฟล์ขยะในเครื่อง
            if (!string.IsNullOrEmpty(transaction.ReceiptImagePath) && File.Exists(transaction.ReceiptImagePath))
            {
                try { File.Delete(transaction.ReceiptImagePath); } catch { /* ignore */ }
            }

            // สั่งลบประวัติธุรกรรมชิ้นนี้ออกจากตาราง SQLite ถาวร
            await _db.DeleteAsync(transaction);
        }

        // ==========================================================================
        // 🌟 กลุ่มฟังก์ชันจัดการ "เป้าหมายการเงิน (Financial Goal CRUD)"
        // ==========================================================================

        /// <summary>
        /// ดึงรายการเป้าหมายการออมเงินทั้งหมด
        /// </summary>
        public async Task<List<FinancialGoal>> GetGoalsAsync()
        {
            await Init();
            return await _db.Table<FinancialGoal>().ToListAsync();
        }

        /// <summary>
        /// บันทึกหรือแก้ไขเป้าหมายการออมเงิน (Insert ใหม่ / Update อันเดิม)
        /// </summary>
        public async Task<int> SaveGoalAsync(FinancialGoal goal)
        {
            await Init();
            if (goal.Id != 0)
                return await _db.UpdateAsync(goal);
            return await _db.InsertAsync(goal);
        }

        // ==========================================================================
        // 🌟 กลุ่มฟังก์ชันจัดการ "โปรไฟล์และแต้มประสบการณ์ (UserProfile Operations)"
        // ==========================================================================

        /// <summary>
        /// ดึงโปรไฟล์แต้มผู้ใช้ (มีลอจิกพิเศษ: ถ้ายังไม่มีแถวโปรไฟล์ในระบบเลย จะสร้างขึ้นให้อัตโนมัติ)
        /// </summary>
        public async Task<UserProfile> GetUserProfileAsync()
        {
            await Init();
            // ค้นหาแถวโปรไฟล์แถวแรกสุดในตาราง
            var profile = await _db.Table<UserProfile>().FirstOrDefaultAsync();

            // ลอจิกเซฟตี้ (First-time initialization): ถ้าเปิดแอปครั้งแรกสุดตารางจะว่างเปล่า (Null)
            if (profile == null)
            {
                // สร้างโปรไฟล์แถวแรก ล็อก Id = 1 และเริ่มที่ 0 EXP ทันที
                profile = new UserProfile { Id = 1, TotalExp = 0 };
                await _db.InsertAsync(profile); // บันทึกลงตารางไว้รอ
            }
            return profile; // ส่งคืนวัตถุโปรไฟล์กลับไปใช้งาน
        }

        /// <summary>
        /// สั่งอัปเดตเซฟคะแนน EXP ล่าสุดของผู้ใช้ลงตารางฐานข้อมูล
        /// </summary>
        public async Task<int> SaveUserProfileAsync(UserProfile profile)
        {
            await Init();
            return await _db.UpdateAsync(profile); // สั่งอัปเดตแถวข้อมูลโปรไฟล์ทับที่เดิม
        }
    }
}