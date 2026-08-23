using Model;
using System;
using System.Collections.Generic;
using System.ServiceModel;
using ViewDB;

namespace BusinessLogic
{
    /// <summary>
    /// Business logic for expenses: CRUD, category/period queries, and receipt
    /// upload (validation, safe filename, disk write under /Receipts).
    /// </summary>
    public class ExpenseLogic
    {
        private readonly ExpenseDB expDB = new ExpenseDB();

        public int  AddExpense(Expense e)              => expDB.Insert(e);
        public void UpdateExpense(Expense e)           => expDB.Update(e);
        public void DeleteExpense(int id)              => expDB.Delete(id);
        public List<Expense> GetExpensesByOwner(int ownerId) => expDB.GetByOwner(ownerId);
        public List<Expense> GetExpensesByCategory(int ownerId, int catId)
            => expDB.GetByCategory(ownerId, catId);
        public List<Expense> GetExpensesByPeriod(int ownerId, DateTime from, DateTime to)
            => expDB.GetByPeriod(ownerId, from, to);
        public List<ExpenseCategory> GetExpenseCategories() => expDB.GetCategories();

        // TODO(security): UploadReceipt has no ownership check — any caller with a valid
        // expenseId can overwrite another user's receipt path (IDOR). Fixing this requires
        // adding an ownerId parameter to IService1/Service1/client-proxy (>5 files), which
        // is out of scope for this audit pass. Track as a dedicated fix.
        public string UploadReceipt(int expenseId, byte[] fileBytes, string fileName)
        {
            try
            {
                if (fileBytes == null || fileBytes.Length == 0)
                    throw new FaultException("Empty file.");
                if (fileBytes.Length > 5 * 1024 * 1024)
                    throw new FaultException("Receipt larger than 5 MB.");

                string root = AppDomain.CurrentDomain.BaseDirectory;
                string dir = System.IO.Path.Combine(root, "Receipts");
                System.IO.Directory.CreateDirectory(dir);

                string safeName = System.IO.Path.GetFileName(fileName ?? "receipt.bin");
                foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                    safeName = safeName.Replace(c, '_');
                string stamped = $"{expenseId}_{DateTime.Now:yyyyMMddHHmmss}_{safeName}";
                string fullPath = System.IO.Path.Combine(dir, stamped);
                System.IO.File.WriteAllBytes(fullPath, fileBytes);

                string rel = "Receipts/" + stamped;
                expDB.SetReceiptPath(expenseId, rel);
                return rel;
            }
            catch (FaultException) { throw; }
            catch { throw new FaultException("Receipt upload failed."); }
        }
    }
}
