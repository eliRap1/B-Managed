using Model;
using Model.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using ViewDB;

namespace WcfServiceLibrary1
{
    // =========================================================================
    // Service1 — concrete WCF service implementation.
    // -------------------------------------------------------------------------
    // Topology:
    //   WPF / Web (BManagedClient, BManagedWeb)
    //       │
    //       ▼   BasicHttpBinding, port 8733
    //   Service1  ←  this class. One instance per WCF call (PerCall).
    //       │
    //       ▼   delegated calls (no business logic here, just orchestration)
    //   ViewDB classes (UserDB, CustomerDB, InvoiceDB, ReportsDB, LoanDB ...)
    //       │
    //       ▼   parameterised OleDb queries
    //   MS Access .accdb file (BManaged.accdb)
    //
    // Conventions:
    //   * Mutating ops are wrapped in try/catch and rewrap to FaultException
    //     so the WCF client gets the real error message, not a generic 500.
    //   * Reads are direct delegations — let exceptions bubble.
    //   * No tenant scoping happens here; it lives in ViewDB methods that
    //     take an ownerId parameter (e.g. GetUsersForOwner).
    // =========================================================================
    public class Service1 : IService1
    {
        // Per-call instances of the ViewDB classes. Each ViewDB owns its own
        // OleDbConnection lifecycle (open/close per query) so two calls into
        // Service1 never fight over the same connection. EnsureSchema runs
        // exactly once per process via static lock in each ViewDB ctor.
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
        private readonly LoanDB     loanDB     = new LoanDB();

        private void RequireCustomerOwner(int customerId, int ownerId)
        {
            if (!custDB.BelongsToOwner(customerId, ownerId))
                throw new FaultException("Customer does not belong to this owner.");
        }

        private void RequireProjectOwner(int projectId, int ownerId)
        {
            if (!projDB.BelongsToOwner(projectId, ownerId))
                throw new FaultException("Project does not belong to this owner.");
        }

        private void RequireInvoiceOwner(int invoiceId, int ownerId)
        {
            if (!invDB.BelongsToOwner(invoiceId, ownerId))
                throw new FaultException("Invoice does not belong to this owner.");
        }

        private void RequireEmployeeOwner(int employeeId, int ownerId)
        {
            var u = userDB.GetById(employeeId);
            if (u == null || u.Role != "Employee" || u.OwnerId != ownerId || !u.IsActive)
                throw new FaultException("Employee does not belong to this owner.");
        }

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

        public List<User> GetUsersForOwner(int ownerId)     => userDB.GetUsersForOwner(ownerId);
        public List<User> GetPendingForOwner(int ownerId)   => userDB.GetPendingForOwner(ownerId);
        public List<User> GetEmployeesForOwner(int ownerId) => userDB.GetEmployeesForOwner(ownerId);

        public void UpdateUserProfile(int userId, string email, string phone, string preferredCurrency)
            => userDB.UpdateProfile(userId, email, phone, preferredCurrency);

        public void SetBusinessType(int userId, string businessType)
            => userDB.SetBusinessType(userId, businessType);

        public void SetIsZair(int userId, bool isZair)
            => userDB.SetIsZair(userId, isZair);

        public void SetOwnerId(int userId, int ownerId)
        {
            userDB.SetOwnerId(userId, ownerId > 0 ? ownerId : (int?)null);
            // When linking a brand-new (still inactive) Employee/Client to an
            // Owner, drop a notification on that Owner's inbox so they know
            // someone joined and can approve them.
            try
            {
                if (ownerId > 0)
                {
                    var u = userDB.GetById(userId);
                    if (u != null && !u.IsActive)
                    {
                        notifDB.Insert(new Notification
                        {
                            UserId           = ownerId,
                            Title            = "New " + (u.Role ?? "user") + " request",
                            Message          = "'" + (u.Username ?? "?") +
                                               "' joined your company with the invite code. " +
                                               "Open Manage Users to approve.",
                            NotificationType = "JoinRequest",
                            IsRead           = false,
                            CreatedAt        = DateTime.Now,
                        });
                    }
                }
            }
            catch (Exception ex)
            { System.Diagnostics.Debug.WriteLine("SetOwnerId notify: " + ex.Message); }
        }

        public List<User> GetActiveOwners()
            => userDB.GetActiveOwners();

        public void SetBusinessName(int userId, string businessName)
            => userDB.SetBusinessName(userId, businessName);

        public string SetInviteCode(int userId, string inviteCode)
            => userDB.SetInviteCode(userId, inviteCode);

        public User GetOwnerByInviteCode(string code)
            => userDB.GetOwnerByInviteCode(code);

        // ===================================================================
        // CUSTOMERS / CRM
        // ===================================================================

        public int  AddCustomer(Customer c)             => custDB.Insert(c);
        public void UpdateCustomer(Customer c)          => custDB.Update(c);
        public void DeleteCustomer(int id)              => custDB.Delete(id);
        public Customer GetCustomerById(int id)         => custDB.GetById(id);
        public Customer GetCustomerByIdForOwner(int id, int ownerId)
            => custDB.GetByIdForOwner(id, ownerId);
        public void UpdateCustomerForOwner(Customer c, int ownerId)
        {
            if (c == null) throw new FaultException("Customer is required.");
            RequireCustomerOwner(c.Id, ownerId);
            c.OwnerId = ownerId;
            custDB.Update(c);
        }
        public void DeleteCustomerForOwner(int id, int ownerId)
        {
            RequireCustomerOwner(id, ownerId);
            custDB.Delete(id);
        }
        public List<Customer> GetCustomersForOwner(int ownerId) => custDB.GetByOwner(ownerId);
        public List<Customer> SearchCustomers(string keyword, int ownerId)
            => custDB.Search(keyword, ownerId);

        // ===================================================================
        // PROJECTS
        // ===================================================================

        public int  AddProject(Project p)               => projDB.Insert(p);
        public int  AddProjectForOwner(Project p, int ownerId)
        {
            if (p == null) throw new FaultException("Project is required.");
            RequireCustomerOwner(p.CustomerId, ownerId);
            return projDB.Insert(p);
        }
        public void UpdateProject(Project p)            => projDB.Update(p);
        public void SetProjectStatus(int id, string s)  => projDB.SetStatus(id, s);
        public void SetProjectStatusForOwner(int id, int ownerId, string s)
        {
            RequireProjectOwner(id, ownerId);
            projDB.SetStatus(id, s);
        }
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
        public Project GetProjectByIdForOwner(int id, int ownerId)
            => projDB.GetByIdForOwner(id, ownerId);

        public void AddProjectAssignment(int projectId, int employeeId)
            => assignDB.Add(projectId, employeeId);
        public void RemoveProjectAssignment(int projectId, int employeeId)
            => assignDB.Remove(projectId, employeeId);
        public void AddProjectAssignmentForOwner(int projectId, int ownerId, int employeeId)
        {
            RequireProjectOwner(projectId, ownerId);
            RequireEmployeeOwner(employeeId, ownerId);
            assignDB.Add(projectId, employeeId);
        }
        public void RemoveProjectAssignmentForOwner(int projectId, int ownerId, int employeeId)
        {
            RequireProjectOwner(projectId, ownerId);
            RequireEmployeeOwner(employeeId, ownerId);
            assignDB.Remove(projectId, employeeId);
        }
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

        public int CreateInvoiceForOwner(Invoice inv, int ownerId)
        {
            if (inv == null) throw new FaultException("Invoice is required.");
            RequireCustomerOwner(inv.CustomerId, ownerId);
            if (inv.ProjectId.HasValue && inv.ProjectId.Value > 0)
                RequireProjectOwner(inv.ProjectId.Value, ownerId);
            if (inv.ContractId.HasValue && inv.ContractId.Value > 0 &&
                !contractDB.BelongsToOwner(inv.ContractId.Value, ownerId))
                throw new FaultException("Contract does not belong to this owner.");
            return CreateInvoice(inv);
        }

        public int  AddInvoiceLine(InvoiceLine l)
        {
            l.LineTotal = (decimal)l.Quantity * l.UnitPrice;
            int id = lineDB.Insert(l);
            invDB.RecalcTotals(l.InvoiceId);
            return id;
        }

        public int AddInvoiceLineForOwner(InvoiceLine l, int ownerId)
        {
            if (l == null) throw new FaultException("Invoice line is required.");
            RequireInvoiceOwner(l.InvoiceId, ownerId);
            return AddInvoiceLine(l);
        }

        public void UpdateInvoiceStatus(int id, string s) => invDB.UpdateStatus(id, s);
        public void UpdateInvoiceStatusForOwner(int id, int ownerId, string s)
        {
            RequireInvoiceOwner(id, ownerId);
            invDB.UpdateStatus(id, s);
        }

        public void MarkInvoicePaid(int id, DateTime paidDate)
        {
            try
            {
                invDB.MarkPaid(id, paidDate);

                // If the invoice was raised from a contract, mark the contract
                // 'Fulfilled' so it stops appearing as outstanding work. We do
                // not surface contract changes if the contract is already
                // Fulfilled / Cancelled.
                try
                {
                    var inv = invDB.GetById(id);
                    if (inv != null && inv.ContractId.HasValue && inv.ContractId.Value > 0)
                    {
                        var c = contractDB.GetById(inv.ContractId.Value);
                        if (c != null && c.Status != "Fulfilled" && c.Status != "Cancelled")
                        {
                            contractDB.SetStatus(c.Id, "Fulfilled", c.SignedDate);
                        }
                    }
                }
                catch (Exception inner)
                { System.Diagnostics.Debug.WriteLine("MarkInvoicePaid (contract sync): " + inner.Message); }
            }
            catch (Exception ex) { throw new FaultException("MarkInvoicePaid failed: " + ex.Message); }
        }

        public void MarkInvoicePaidForOwner(int id, int ownerId, DateTime paidDate)
        {
            RequireInvoiceOwner(id, ownerId);
            MarkInvoicePaid(id, paidDate);
        }

        public void RecalcInvoiceTotals(int invoiceId) => invDB.RecalcTotals(invoiceId);
        public Invoice GetInvoiceById(int id)              => invDB.GetById(id);
        public Invoice GetInvoiceByIdForOwner(int id, int ownerId)
            => invDB.GetByIdForOwner(id, ownerId);
        public List<InvoiceLine> GetInvoiceLines(int id)   => lineDB.GetByInvoice(id);
        public List<InvoiceLine> GetInvoiceLinesForOwner(int id, int ownerId)
        {
            RequireInvoiceOwner(id, ownerId);
            return lineDB.GetByInvoice(id);
        }
        public List<Invoice> GetInvoicesByCustomer(int cid) => invDB.GetByCustomer(cid);
        public List<Invoice> GetUnpaidInvoices(int ownerId) => invDB.GetUnpaidForOwner(ownerId);
        public List<Invoice> GetOverdueInvoices(int ownerId)=> invDB.GetOverdueForOwner(ownerId);
        public List<Invoice> GetInvoicesForOwner(int ownerId) => invDB.GetForOwner(ownerId);

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

        public byte[] GenerateInvoicePdfForOwner(int invoiceId, int ownerId)
        {
            RequireInvoiceOwner(invoiceId, ownerId);
            return GenerateInvoicePdf(invoiceId);
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

        // Cashflow forecast: trailing 3-month average for income/expenses,
        // projected forward for `months` periods. Adds outstanding invoices
        // due in the projection window to the income side of the matching month.
        public List<ProfitLoss> GetCashFlowForecast(int ownerId, int months, string displayCurrency)
        {
            var cur = displayCurrency ?? "ILS";
            decimal sumInc = 0, sumExp = 0;
            int n = 3;
            var anchor = DateTime.Today;
            for (int i = 1; i <= n; i++)
            {
                var first = new DateTime(anchor.Year, anchor.Month, 1).AddMonths(-i);
                var last  = first.AddMonths(1).AddDays(-1);
                var pl = reportsDB.ProfitLoss(ownerId, first, last, cur);
                if (pl == null) continue;
                sumInc += pl.Income;
                sumExp += pl.Expenses;
            }
            decimal avgInc = sumInc / n;
            decimal avgExp = sumExp / n;

            // Outstanding invoices boost the month their dueDate falls in.
            var outstanding = invDB.GetUnpaidForOwner(ownerId);

            var result = new List<ProfitLoss>();
            for (int i = 1; i <= months; i++)
            {
                var first = new DateTime(anchor.Year, anchor.Month, 1).AddMonths(i);
                var last  = first.AddMonths(1).AddDays(-1);

                decimal extraIncome = 0;
                if (outstanding != null)
                {
                    foreach (var inv in outstanding)
                    {
                        if (inv.DueDate.Date >= first && inv.DueDate.Date <= last)
                            extraIncome += inv.Total;
                    }
                }

                result.Add(new ProfitLoss
                {
                    Income          = avgInc + extraIncome,
                    Expenses        = avgExp,
                    Profit          = (avgInc + extraIncome) - avgExp,
                    DisplayCurrency = cur,
                });
            }
            return result;
        }

        public AnalyticsKpis GetAdvancedKpis(int ownerId, string displayCurrency)
            => reportsDB.AdvancedKpis(ownerId, string.IsNullOrEmpty(displayCurrency) ? "ILS" : displayCurrency);

        public ReportsSnapshot GetReportsSnapshot(int ownerId, int year, int month, string displayCurrency)
        {
            string cur = string.IsNullOrEmpty(displayCurrency) ? "ILS" : displayCurrency;
            var first = new DateTime(year, month, 1);
            var last  = first.AddMonths(1).AddDays(-1);
            var yearStart = new DateTime(year, 1, 1);
            var yearEnd   = new DateTime(year, 12, 31);

            var snap = new ReportsSnapshot { DisplayCurrency = cur };
            try { snap.Vat              = reportsDB.VatSummary(ownerId, year, month, cur); } catch { }
            try { snap.TopCustomers     = reportsDB.TopCustomersByRevenue(ownerId, cur); } catch { }
            try { snap.ExpenseBreakdown = reportsDB.ExpenseBreakdown(ownerId, first, last, cur); } catch { }
            try { snap.MonthPl          = reportsDB.ProfitLoss(ownerId, first, last, cur); } catch { }
            try { snap.YearPl           = reportsDB.ProfitLoss(ownerId, yearStart, yearEnd, cur); } catch { }
            try { snap.Kpis             = reportsDB.AdvancedKpis(ownerId, cur); } catch { }
            try { snap.LoanSummary      = GetLoanSummary(ownerId, cur); } catch { }
            try
            {
                var u = userDB.GetById(ownerId);
                if (u != null)
                {
                    snap.BusinessType = string.IsNullOrEmpty(u.BusinessType) ? "Individual" : u.BusinessType;
                    snap.IsZair = u.IsZair;
                }
            }
            catch { }
            return snap;
        }

        public OwnerDashboardSnapshot GetOwnerDashboardSnapshot(int ownerId, string displayCurrency)
        {
            string cur = string.IsNullOrEmpty(displayCurrency) ? "ILS" : displayCurrency;
            try { EnsureOverdueNotifications(ownerId); } catch { }

            var now = DateTime.Today;
            var monthFirst = new DateTime(now.Year, now.Month, 1);
            var monthLast  = monthFirst.AddMonths(1).AddDays(-1);
            var prevFirst  = monthFirst.AddMonths(-1);
            var prevLast   = monthFirst.AddDays(-1);

            // Pull invoices once via a single JOINed query, derive both the
            // unpaid total and the recent-invoices preview from the same list.
            var allInvoices = invDB.GetForOwner(ownerId) ?? new List<Invoice>();
            decimal unpaidTotal = allInvoices
                .Where(i => i.Status != "Paid")
                .Sum(i => i.Total);
            var customers = custDB.GetByOwner(ownerId) ?? new List<Customer>();
            var custLookup = customers.ToDictionary(c => c.Id, c => c.BusinessName ?? "");
            var recent = allInvoices
                .OrderByDescending(i => i.IssueDate)
                .Take(6)
                .Select(i => new RecentInvoice
                {
                    InvoiceNumber = i.InvoiceNumber,
                    CustomerName  = custLookup.TryGetValue(i.CustomerId, out var n) ? n : "",
                    Total         = i.Total,
                    Currency      = i.Currency,
                    Status        = i.Status,
                }).ToList();

            // VAT + tax set-aside (current month).
            var vat = reportsDB.VatSummary(ownerId, now.Year, now.Month, cur);
            decimal taxSetAside = reportsDB.MonthlyTaxSetAside(ownerId, now.Year, now.Month, cur);

            // Top expense category this month + revenue trend vs prior month.
            string topCat = null; decimal topAmt = 0m;
            try
            {
                var brk = reportsDB.ExpenseBreakdown(ownerId, monthFirst, monthLast, cur);
                if (brk != null && brk.Count > 0)
                {
                    var top = brk.OrderByDescending(b => b.Total).First();
                    topCat = top.CategoryName; topAmt = top.Total;
                }
            }
            catch { }

            decimal revPct = 0m;
            try
            {
                var thisPl = reportsDB.ProfitLoss(ownerId, monthFirst, monthLast, cur);
                var prevPl = reportsDB.ProfitLoss(ownerId, prevFirst, prevLast, cur);
                if (thisPl != null && prevPl != null && prevPl.Income > 0)
                    revPct = Math.Round(((thisPl.Income - prevPl.Income) / prevPl.Income) * 100m, 1);
            }
            catch { }

            return new OwnerDashboardSnapshot
            {
                CustomersCount = customers.Count,
                UnpaidCount = allInvoices.Count(i => i.Status != "Paid"),
                OverdueCount = invDB.GetOverdueForOwner(ownerId)?.Count ?? 0,
                ActiveProjectsCount = projDB.GetByStatus("Active", ownerId)?.Count ?? 0,
                UnreadNotificationsCount = notifDB.UnreadCount(ownerId),
                CashFlowForecast = GetCashFlowForecast(ownerId, 3, cur),
                Kpis = reportsDB.AdvancedKpis(ownerId, cur),
                LoanSummary = GetLoanSummary(ownerId, cur),
                UnpaidTotal = unpaidTotal,
                VatDue = vat?.VatDue ?? 0m,
                MonthlyTaxSetAside = taxSetAside,
                TopExpenseCategory = topCat,
                TopExpenseAmount = topAmt,
                RevenueChangePct = revPct,
                RecentInvoices = recent,
                DisplayCurrency = cur,
            };
        }

        // ==================== LOANS ====================
        public int  AddLoan(Loan l)
        {
            try { return loanDB.Insert(l); }
            catch (Exception ex) { throw new FaultException("AddLoan failed: " + ex.Message); }
        }
        public void UpdateLoan(Loan l)
        {
            try { loanDB.Update(l); }
            catch (Exception ex) { throw new FaultException("UpdateLoan failed: " + ex.Message); }
        }
        public void DeleteLoan(int id)
        {
            try { loanDB.Delete(id); }
            catch (Exception ex) { throw new FaultException("DeleteLoan failed: " + ex.Message); }
        }
        public Loan GetLoanById(int id) => loanDB.GetById(id);
        public List<Loan> GetLoansForOwner(int ownerId) => loanDB.GetForOwner(ownerId);
        public int RecordLoanPayment(LoanPayment p)
        {
            try { return loanDB.InsertPayment(p); }
            catch (Exception ex) { throw new FaultException("RecordLoanPayment failed: " + ex.Message); }
        }
        public List<LoanPayment> GetLoanPayments(int loanId) => loanDB.GetPaymentsByLoan(loanId);

        public LoanSummary GetLoanSummary(int ownerId, string displayCurrency)
        {
            string cur = string.IsNullOrEmpty(displayCurrency) ? "ILS" : displayCurrency;
            var loans = loanDB.GetForOwner(ownerId) ?? new List<Loan>();
            var s = new LoanSummary { DisplayCurrency = cur };
            var fx = new ViewDB.CurrencyConverter();
            DateTime today = DateTime.Today;

            foreach (var l in loans.Where(x => x.IsActive))
            {
                s.LoanCount++;
                if (l.IsKerenBacked) s.KerenBackedCount++;
                s.TotalPrincipal       += fx.Convert(l.Principal,        l.Currency, cur, today);
                s.TotalRemaining       += fx.Convert(l.RemainingBalance, l.Currency, cur, today);
                s.MonthlyPaymentTotal  += fx.Convert(l.MonthlyPayment,   l.Currency, cur, today);

                if (l.NextPaymentDate.HasValue)
                {
                    if (!s.NextPaymentDate.HasValue || l.NextPaymentDate.Value < s.NextPaymentDate.Value)
                    {
                        s.NextPaymentDate   = l.NextPaymentDate;
                        s.NextPaymentAmount = fx.Convert(l.MonthlyPayment, l.Currency, cur, today);
                    }
                }
            }

            // Debt-service ratios joined against trailing-3-month income
            try
            {
                var kpis = reportsDB.AdvancedKpis(ownerId, cur);
                if (kpis.AvgMonthlyIncome > 0)
                {
                    decimal annual = kpis.AvgMonthlyIncome * 12m;
                    s.DebtToAnnualIncomePct = annual <= 0 ? 0
                        : Math.Round((double)(s.TotalRemaining / annual) * 100.0, 1);
                    s.MonthlyDebtServiceRatioPct = Math.Round(
                        (double)(s.MonthlyPaymentTotal / kpis.AvgMonthlyIncome) * 100.0, 1);
                }
            }
            catch { }

            return s;
        }

        public int EnsureOverdueNotifications(int ownerId)
        {
            int created = 0;
            try
            {
                var overdue = invDB.GetOverdueForOwner(ownerId) ?? new List<Invoice>();
                var existing = notifDB.GetByUser(ownerId) ?? new List<Notification>();
                var existingNumbers = new HashSet<string>();
                foreach (var n in existing)
                {
                    if (!string.IsNullOrEmpty(n.Title) && n.NotificationType == "Overdue")
                        existingNumbers.Add(n.Title);
                }

                foreach (var inv in overdue)
                {
                    string title = "Overdue: " + inv.InvoiceNumber;
                    if (existingNumbers.Contains(title)) continue;
                    var cust = custDB.GetById(inv.CustomerId);
                    notifDB.Insert(new Notification
                    {
                        UserId  = ownerId,
                        Title   = title,
                        Message = "Invoice " + inv.InvoiceNumber +
                                  " (" + (cust?.BusinessName ?? "?") + ") is past due since " +
                                  inv.DueDate.ToString("dd/MM/yyyy") +
                                  ". Total " + inv.Total.ToString("N2") + " " + inv.Currency + ".",
                        NotificationType = "Overdue",
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                    });
                    created++;
                }
            }
            catch { }
            return created;
        }

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
