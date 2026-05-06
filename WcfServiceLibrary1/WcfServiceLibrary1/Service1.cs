using Model;
using Model.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using ViewDB;

namespace WcfServiceLibrary1
{
    /// <summary>
    /// Concrete implementation of <see cref="IService1"/>. Each method
    /// delegates to a per-table ViewDB class. Errors are caught and
    /// rewrapped as <see cref="FaultException"/> so the real cause
    /// surfaces to clients (lesson learned from Driver-moodle).
    /// </summary>
    public class Service1 : IService1
    {
        private readonly UserDB         userDB     = new UserDB();
        private readonly CustomerDB     custDB     = new CustomerDB();
        private readonly ProjectDB      projDB     = new ProjectDB();
        private readonly InvoiceDB      invDB      = new InvoiceDB();
        private readonly InvoiceLineDB  lineDB     = new InvoiceLineDB();
        private readonly ExpenseDB      expDB      = new ExpenseDB();
        private readonly NotificationDB notifDB    = new NotificationDB();
        private readonly ExchangeRateDB fxDB       = new ExchangeRateDB();
        private readonly ReportsDB      reportsDB  = new ReportsDB();
        private readonly ProjectAssignmentDB assignDB = new ProjectAssignmentDB();
        private readonly ContractDB contractDB = new ContractDB();

        // ===================================================================
        // AUTH & USERS
        // ===================================================================

        public bool CheckUserPassword(string u, string p)
            => userDB.VerifyPassword(u, p);

        public bool CheckUserExist(string u)
            => userDB.UserExists(u);

        public User GetUserById(int id)
            => userDB.GetById(id);

        public int GetUserId(string username)
            => userDB.GetIdByUsername(username);

        public bool AddUser(string username, string password, string email,
                            string phone, string role, string preferredCurrency)
        {
            if (!SecurityHelper.IsSafeString(username, 50)) return false;
            if (string.IsNullOrEmpty(password) || password.Length < 4) return false;
            if (userDB.UserExists(username)) return false;

            string r = role ?? "Client";
            // Pending approval: Clients/Employees signing up default to inactive
            // until an Owner approves them. Owner-tier signup (admin seed) is
            // always active.
            bool active = r == "Owner";

            var u = new User
            {
                Username = username,
                PasswordHash = SecurityHelper.HashPassword(password),
                Email = email,
                Phone = phone,
                Role = r,
                IsActive = active,
                CreatedAt = DateTime.Now,
                PreferredCurrency = preferredCurrency ?? "ILS"
            };
            return userDB.Insert(u) > 0;
        }

        public List<User> GetPendingUsers()
            => userDB.GetInactive();

        public void SetUserActive(int userId, bool isActive)
            => userDB.SetActive(userId, isActive);

        public void DeleteUser(int userId)
            => userDB.Delete(userId);

        public void ResetPassword(int userId, string newPassword)
        {
            try { userDB.SetPassword(userId, SecurityHelper.HashPassword(newPassword)); }
            catch (Exception ex) { throw new FaultException("ResetPassword failed: " + ex.Message); }
        }

        public void UpdateUserRole(int userId, string newRole)
        {
            try { userDB.UpdateRole(userId, newRole); }
            catch (Exception ex) { throw new FaultException("UpdateUserRole failed: " + ex.Message); }
        }

        public bool IsOwner(string username) => userDB.IsRole(username, "Owner");

        public AllUsers GetAllUsers() => userDB.GetAll();

        public List<User> GetAllEmployees() => userDB.GetByRole("Employee");

        public void UpdateUserProfile(int userId, string email, string phone, string preferredCurrency)
            => userDB.UpdateProfile(userId, email, phone, preferredCurrency);

        public void SetBusinessType(int userId, string businessType)
            => userDB.SetBusinessType(userId, businessType);

        // ===================================================================
        // CUSTOMERS / CRM
        // ===================================================================

        public int  AddCustomer(Customer c)             => custDB.Insert(c);
        public void UpdateCustomer(Customer c)          => custDB.Update(c);
        public void DeleteCustomer(int id)              => custDB.Delete(id);
        public Customer GetCustomerById(int id)         => custDB.GetById(id);
        public List<Customer> GetCustomersForOwner(int ownerId) => custDB.GetByOwner(ownerId);
        public List<Customer> SearchCustomers(string keyword, int ownerId)
            => custDB.Search(keyword, ownerId);

        // ===================================================================
        // PROJECTS
        // ===================================================================

        public int  AddProject(Project p)               => projDB.Insert(p);
        public void UpdateProject(Project p)            => projDB.Update(p);
        public void SetProjectStatus(int id, string s)  => projDB.SetStatus(id, s);
        public void AssignEmployee(int id, int empId)   => projDB.AssignEmployee(id, empId);
        public List<Project> GetProjectsByCustomer(int customerId) => projDB.GetByCustomer(customerId);
        // Returns every project the employee is on — both legacy single-assign
        // (Projects.assignedEmployeeId) and new multi-assign (ProjectAssignments).
        public List<Project> GetProjectsForEmployee(int empId)
        {
            var result = new Dictionary<int, Project>();
            foreach (var p in projDB.GetByEmployee(empId))
                if (p != null) result[p.Id] = p;
            foreach (var pid in assignDB.GetProjectsByEmployee(empId))
            {
                if (result.ContainsKey(pid)) continue;
                var pr = projDB.GetById(pid);
                if (pr != null) result[pid] = pr;
            }
            return result.Values.ToList();
        }

        public List<Project> GetProjectsByStatus(string status, int ownerId)
            => projDB.GetByStatus(status, ownerId);
        public Project GetProjectById(int id)           => projDB.GetById(id);

        public void AddProjectAssignment(int projectId, int employeeId)
            => assignDB.Add(projectId, employeeId);
        public void RemoveProjectAssignment(int projectId, int employeeId)
            => assignDB.Remove(projectId, employeeId);
        public List<User> GetProjectAssignees(int projectId)
        {
            var ids = assignDB.GetAssigneesByProject(projectId);
            var users = new List<User>();
            foreach (var id in ids)
            {
                var u = userDB.GetById(id);
                if (u != null) users.Add(u);
            }
            return users;
        }

        // ===================================================================
        // CONTRACTS
        // ===================================================================

        public int CreateContract(Contract c)
        {
            if (c == null) throw new FaultException("Contract is required.");
            if (c.CustomerId <= 0) throw new FaultException("CustomerId required.");
            return contractDB.Insert(c);
        }

        public void UpdateContract(Contract c)         => contractDB.SetStatus(c.Id, c.Status, c.SignedDate);
        public void DeleteContract(int id)             => contractDB.Delete(id);
        public void MarkContractSigned(int id, DateTime signedDate)
            => contractDB.SetStatus(id, "Signed", signedDate);
        public Contract GetContractById(int id)        => contractDB.GetById(id);
        public List<Contract> GetContractsForOwner(int ownerId)
            => contractDB.GetForOwner(ownerId);
        public List<Contract> GetContractsByProject(int projectId)
            => contractDB.GetByProject(projectId);
        public List<Contract> GetContractsByCustomer(int customerId)
            => contractDB.GetByCustomer(customerId);

        public byte[] GenerateContractPdf(int contractId)
        {
            var c = contractDB.GetById(contractId);
            if (c == null) throw new FaultException("Contract not found.");
            var cust = custDB.GetById(c.CustomerId);
            var proj = c.ProjectId > 0 ? projDB.GetById(c.ProjectId) : null;
            var owner = cust != null ? userDB.GetById(cust.OwnerId) : null;
            var pdf = new BusinessLogic.ContractPdfBuilder();
            return pdf.Render(c, cust, proj, owner?.Username);
        }

        // ===================================================================
        // INVOICES
        // ===================================================================

        public int  CreateInvoice(Invoice inv)
        {
            if (string.IsNullOrEmpty(inv.InvoiceNumber))
                inv.InvoiceNumber = invDB.NextInvoiceNumber();
            return invDB.Insert(inv);
        }

        public int  AddInvoiceLine(InvoiceLine l)
        {
            l.LineTotal = (decimal)l.Quantity * l.UnitPrice;
            int id = lineDB.Insert(l);
            invDB.RecalcTotals(l.InvoiceId);
            return id;
        }

        public void UpdateInvoiceStatus(int id, string s) => invDB.UpdateStatus(id, s);

        public void MarkInvoicePaid(int id, DateTime paidDate)
        {
            try { invDB.MarkPaid(id, paidDate); }
            catch (Exception ex) { throw new FaultException("MarkInvoicePaid failed: " + ex.Message); }
        }

        public void RecalcInvoiceTotals(int invoiceId) => invDB.RecalcTotals(invoiceId);
        public Invoice GetInvoiceById(int id)              => invDB.GetById(id);
        public List<InvoiceLine> GetInvoiceLines(int id)   => lineDB.GetByInvoice(id);
        public List<Invoice> GetInvoicesByCustomer(int cid) => invDB.GetByCustomer(cid);
        public List<Invoice> GetUnpaidInvoices(int ownerId) => invDB.GetUnpaidForOwner(ownerId);
        public List<Invoice> GetOverdueInvoices(int ownerId)=> invDB.GetOverdueForOwner(ownerId);

        public byte[] GenerateInvoicePdf(int invoiceId)
        {
            try
            {
                var inv = invDB.GetById(invoiceId);
                var lines = lineDB.GetByInvoice(invoiceId);
                var customer = custDB.GetById(inv.CustomerId);
                return new BusinessLogic.InvoicePdfBuilder().Render(inv, lines, customer);
            }
            catch (Exception ex)
            {
                throw new FaultException("GenerateInvoicePdf failed: " + ex.Message);
            }
        }

        // ===================================================================
        // EXPENSES
        // ===================================================================

        public int  AddExpense(Expense e)              => expDB.Insert(e);
        public void UpdateExpense(Expense e)           => expDB.Update(e);
        public void DeleteExpense(int id)              => expDB.Delete(id);
        public List<Expense> GetExpensesByOwner(int ownerId) => expDB.GetByOwner(ownerId);
        public List<Expense> GetExpensesByCategory(int ownerId, int catId)
            => expDB.GetByCategory(ownerId, catId);
        public List<Expense> GetExpensesByPeriod(int ownerId, DateTime from, DateTime to)
            => expDB.GetByPeriod(ownerId, from, to);
        public List<ExpenseCategory> GetExpenseCategories() => expDB.GetCategories();

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
            catch (Exception ex) { throw new FaultException("UploadReceipt failed: " + ex.Message); }
        }

        // ===================================================================
        // REPORTS / VAT
        // ===================================================================

        public VatSummary GetVatSummary(int ownerId, int year, int month, string displayCurrency)
            => reportsDB.VatSummary(ownerId, year, month, displayCurrency ?? "ILS");

        public decimal GetMonthlyTaxSetAside(int ownerId, int year, int month, string displayCurrency)
            => reportsDB.MonthlyTaxSetAside(ownerId, year, month, displayCurrency ?? "ILS");

        public ProfitLoss GetProfitLoss(int ownerId, DateTime from, DateTime to, string displayCurrency)
            => reportsDB.ProfitLoss(ownerId, from, to, displayCurrency ?? "ILS");

        public List<CustomerRevenueRow> GetTopCustomersByRevenue(int ownerId, string displayCurrency)
            => reportsDB.TopCustomersByRevenue(ownerId, displayCurrency ?? "ILS");

        public List<ExpenseBreakdownRow> GetExpenseBreakdown(int ownerId, DateTime from, DateTime to, string displayCurrency)
            => reportsDB.ExpenseBreakdown(ownerId, from, to, displayCurrency ?? "ILS");

        public List<EmployeeRevenueRow> GetEmployeeRevenueReport(int ownerId, string displayCurrency)
            => reportsDB.EmployeeRevenueReport(ownerId, displayCurrency ?? "ILS");

        // ===================================================================
        // CURRENCY
        // ===================================================================

        public double GetExchangeRate(string from, string to, DateTime asOfDate)
            => fxDB.GetLatestRate(from, to, asOfDate);

        public void SetExchangeRate(string from, string to, double rate)
            => fxDB.Insert(new ExchangeRate { FromCurrency = from, ToCurrency = to, Rate = rate, EffectiveDate = DateTime.Now });

        public string[] GetSupportedCurrencies() => new[] { "ILS", "USD" };

        // ===================================================================
        // NOTIFICATIONS
        // ===================================================================

        public int SendNotification(Notification n)
        {
            try { return notifDB.Insert(n); }
            catch (Exception ex) { throw new FaultException("SendNotification failed: " + ex.Message); }
        }

        public List<Notification> GetUserNotifications(int userId) => notifDB.GetByUser(userId);

        public int GetUnreadNotificationCount(int userId)          => notifDB.UnreadCount(userId);

        public void MarkNotificationAsRead(int id)
        {
            try { notifDB.MarkAsRead(id); }
            catch (Exception ex) { throw new FaultException("MarkAsRead failed: " + ex.Message); }
        }

        public void MarkAllNotificationsAsRead(int userId)
        {
            try { notifDB.MarkAllAsRead(userId); }
            catch (Exception ex) { throw new FaultException("MarkAllAsRead failed: " + ex.Message); }
        }

        public void DeleteNotification(int id)
        {
            try { notifDB.Delete(id); }
            catch (Exception ex) { throw new FaultException("DeleteNotification failed: " + ex.Message); }
        }
    }
}
