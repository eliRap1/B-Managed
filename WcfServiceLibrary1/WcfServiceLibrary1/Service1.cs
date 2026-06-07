using BusinessLogic;
using Model;
using System;
using System.Collections.Generic;
using ViewDB;

namespace WcfServiceLibrary1
{
    // =========================================================================
    // Service1 — concrete WCF service implementation (thin facade).
    // -------------------------------------------------------------------------
    // Topology:
    //   WPF / Web (BManagedClient, BManagedWeb)
    //       │
    //       ▼   BasicHttpBinding, port 8733
    //   Service1  ←  this class. One instance per WCF call (PerCall).
    //       │
    //       ▼   delegates to the BusinessLogic layer (validation, guards,
    //           orchestration, error handling all live there)
    //   BusinessLogic classes (UserLogic, InvoiceLogic, ReportsLogic ...)
    //       │
    //       ▼
    //   ViewDB classes (UserDB, CustomerDB, InvoiceDB ...)
    //       │
    //       ▼   parameterised OleDb queries
    //   MS Access .accdb file (BManaged.accdb)
    //
    // This class contains no business logic — only WCF endpoint exposure. Each
    // method news up the matching logic class per call (PerCall semantics; each
    // ViewDB owns its own OleDbConnection lifecycle).
    // =========================================================================
    public class Service1 : IService1
    {
        // ===================================================================
        // AUTH & USERS
        // ===================================================================

        public bool CheckUserPassword(string u, string p) => new UserLogic().CheckUserPassword(u, p);
        public bool CheckUserExist(string u)              => new UserLogic().CheckUserExist(u);
        public User GetUserById(int id)                   => new UserLogic().GetUserById(id);
        public int  GetUserId(string username)            => new UserLogic().GetUserId(username);

        public bool AddUser(string username, string password, string email,
                            string phone, string role, string preferredCurrency)
            => new UserLogic().AddUser(username, password, email, phone, role, preferredCurrency);

        public List<User> GetPendingUsers()              => new UserLogic().GetPendingUsers();
        public void SetUserActive(int userId, bool isActive) => new UserLogic().SetUserActive(userId, isActive);
        public void DeleteUser(int userId)               => new UserLogic().DeleteUser(userId);
        public void ResetPassword(int userId, string newPassword) => new UserLogic().ResetPassword(userId, newPassword);
        public void UpdateUserRole(int userId, string newRole)    => new UserLogic().UpdateUserRole(userId, newRole);
        public bool IsOwner(string username)             => new UserLogic().IsOwner(username);
        public AllUsers GetAllUsers()                    => new UserLogic().GetAllUsers();
        public List<User> GetAllEmployees()              => new UserLogic().GetAllEmployees();
        public List<User> GetUsersForOwner(int ownerId)     => new UserLogic().GetUsersForOwner(ownerId);
        public List<User> GetPendingForOwner(int ownerId)   => new UserLogic().GetPendingForOwner(ownerId);
        public List<User> GetEmployeesForOwner(int ownerId) => new UserLogic().GetEmployeesForOwner(ownerId);

        public void UpdateUserProfile(int userId, string email, string phone, string preferredCurrency)
            => new UserLogic().UpdateUserProfile(userId, email, phone, preferredCurrency);

        public void SetBusinessType(int userId, string businessType) => new UserLogic().SetBusinessType(userId, businessType);
        public void SetIsZair(int userId, bool isZair)               => new UserLogic().SetIsZair(userId, isZair);
        public void SetOwnerId(int userId, int ownerId)              => new UserLogic().SetOwnerId(userId, ownerId);
        public List<User> GetActiveOwners()                          => new UserLogic().GetActiveOwners();
        public void SetBusinessName(int userId, string businessName) => new UserLogic().SetBusinessName(userId, businessName);
        public string SetInviteCode(int userId, string inviteCode)   => new UserLogic().SetInviteCode(userId, inviteCode);
        public User GetOwnerByInviteCode(string code)                => new UserLogic().GetOwnerByInviteCode(code);

        // ===================================================================
        // CUSTOMERS / CRM
        // ===================================================================

        public int  AddCustomer(Customer c)             => new CustomerLogic().AddCustomer(c);
        public void UpdateCustomer(Customer c)          => new CustomerLogic().UpdateCustomer(c);
        public void DeleteCustomer(int id)              => new CustomerLogic().DeleteCustomer(id);
        public Customer GetCustomerById(int id)         => new CustomerLogic().GetCustomerById(id);
        public Customer GetCustomerByIdForOwner(int id, int ownerId)
            => new CustomerLogic().GetCustomerByIdForOwner(id, ownerId);
        public void UpdateCustomerForOwner(Customer c, int ownerId)
            => new CustomerLogic().UpdateCustomerForOwner(c, ownerId);
        public void DeleteCustomerForOwner(int id, int ownerId)
            => new CustomerLogic().DeleteCustomerForOwner(id, ownerId);
        public List<Customer> GetCustomersForOwner(int ownerId) => new CustomerLogic().GetCustomersForOwner(ownerId);
        public List<Customer> SearchCustomers(string keyword, int ownerId)
            => new CustomerLogic().SearchCustomers(keyword, ownerId);

        // ===================================================================
        // PROJECTS
        // ===================================================================

        public int  AddProject(Project p)               => new ProjectLogic().AddProject(p);
        public int  AddProjectForOwner(Project p, int ownerId) => new ProjectLogic().AddProjectForOwner(p, ownerId);
        public void UpdateProject(Project p)            => new ProjectLogic().UpdateProject(p);
        public void SetProjectStatus(int id, string s)  => new ProjectLogic().SetProjectStatus(id, s);
        public void SetProjectStatusForOwner(int id, int ownerId, string s)
            => new ProjectLogic().SetProjectStatusForOwner(id, ownerId, s);
        public void AssignEmployee(int id, int empId)   => new ProjectLogic().AssignEmployee(id, empId);
        public List<Project> GetProjectsByCustomer(int customerId) => new ProjectLogic().GetProjectsByCustomer(customerId);
        public List<Project> GetProjectsForEmployee(int empId)     => new ProjectLogic().GetProjectsForEmployee(empId);
        public List<Project> GetProjectsByStatus(string status, int ownerId)
            => new ProjectLogic().GetProjectsByStatus(status, ownerId);
        public Project GetProjectById(int id)           => new ProjectLogic().GetProjectById(id);
        public Project GetProjectByIdForOwner(int id, int ownerId)
            => new ProjectLogic().GetProjectByIdForOwner(id, ownerId);
        public void AddProjectAssignment(int projectId, int employeeId)
            => new ProjectLogic().AddProjectAssignment(projectId, employeeId);
        public void RemoveProjectAssignment(int projectId, int employeeId)
            => new ProjectLogic().RemoveProjectAssignment(projectId, employeeId);
        public void AddProjectAssignmentForOwner(int projectId, int ownerId, int employeeId)
            => new ProjectLogic().AddProjectAssignmentForOwner(projectId, ownerId, employeeId);
        public void RemoveProjectAssignmentForOwner(int projectId, int ownerId, int employeeId)
            => new ProjectLogic().RemoveProjectAssignmentForOwner(projectId, ownerId, employeeId);
        public List<User> GetProjectAssignees(int projectId) => new ProjectLogic().GetProjectAssignees(projectId);

        // ===================================================================
        // CONTRACTS
        // ===================================================================

        public int CreateContract(Contract c)          => new ContractLogic().CreateContract(c);
        public void UpdateContract(Contract c)         => new ContractLogic().UpdateContract(c);
        public void DeleteContract(int id)             => new ContractLogic().DeleteContract(id);
        public void MarkContractSigned(int id, DateTime signedDate)
            => new ContractLogic().MarkContractSigned(id, signedDate);
        public Contract GetContractById(int id)        => new ContractLogic().GetContractById(id);
        public List<Contract> GetContractsForOwner(int ownerId)   => new ContractLogic().GetContractsForOwner(ownerId);
        public List<Contract> GetContractsByProject(int projectId) => new ContractLogic().GetContractsByProject(projectId);
        public List<Contract> GetContractsByCustomer(int customerId) => new ContractLogic().GetContractsByCustomer(customerId);
        public byte[] GenerateContractPdf(int contractId) => new ContractLogic().GenerateContractPdf(contractId);

        // ===================================================================
        // INVOICES
        // ===================================================================

        public int  CreateInvoice(Invoice inv)         => new InvoiceLogic().CreateInvoice(inv);
        public int  CreateInvoiceForOwner(Invoice inv, int ownerId) => new InvoiceLogic().CreateInvoiceForOwner(inv, ownerId);
        public int  AddInvoiceLine(InvoiceLine l)      => new InvoiceLogic().AddInvoiceLine(l);
        public int  AddInvoiceLineForOwner(InvoiceLine l, int ownerId) => new InvoiceLogic().AddInvoiceLineForOwner(l, ownerId);
        public void UpdateInvoiceStatus(int id, string s) => new InvoiceLogic().UpdateInvoiceStatus(id, s);
        public void UpdateInvoiceStatusForOwner(int id, int ownerId, string s)
            => new InvoiceLogic().UpdateInvoiceStatusForOwner(id, ownerId, s);
        public void MarkInvoicePaid(int id, DateTime paidDate) => new InvoiceLogic().MarkInvoicePaid(id, paidDate);
        public void MarkInvoicePaidForOwner(int id, int ownerId, DateTime paidDate)
            => new InvoiceLogic().MarkInvoicePaidForOwner(id, ownerId, paidDate);
        public void RecalcInvoiceTotals(int invoiceId) => new InvoiceLogic().RecalcInvoiceTotals(invoiceId);
        public Invoice GetInvoiceById(int id)          => new InvoiceLogic().GetInvoiceById(id);
        public Invoice GetInvoiceByIdForOwner(int id, int ownerId) => new InvoiceLogic().GetInvoiceByIdForOwner(id, ownerId);
        public List<InvoiceLine> GetInvoiceLines(int id) => new InvoiceLogic().GetInvoiceLines(id);
        public List<InvoiceLine> GetInvoiceLinesForOwner(int id, int ownerId)
            => new InvoiceLogic().GetInvoiceLinesForOwner(id, ownerId);
        public List<Invoice> GetInvoicesByCustomer(int cid) => new InvoiceLogic().GetInvoicesByCustomer(cid);
        public List<Invoice> GetUnpaidInvoices(int ownerId) => new InvoiceLogic().GetUnpaidInvoices(ownerId);
        public List<Invoice> GetOverdueInvoices(int ownerId)=> new InvoiceLogic().GetOverdueInvoices(ownerId);
        public List<Invoice> GetInvoicesForOwner(int ownerId) => new InvoiceLogic().GetInvoicesForOwner(ownerId);
        public byte[] GenerateInvoicePdf(int invoiceId) => new InvoiceLogic().GenerateInvoicePdf(invoiceId);
        public byte[] GenerateInvoicePdfForOwner(int invoiceId, int ownerId)
            => new InvoiceLogic().GenerateInvoicePdfForOwner(invoiceId, ownerId);

        // ===================================================================
        // EXPENSES
        // ===================================================================

        public int  AddExpense(Expense e)              => new ExpenseLogic().AddExpense(e);
        public void UpdateExpense(Expense e)           => new ExpenseLogic().UpdateExpense(e);
        public void DeleteExpense(int id)              => new ExpenseLogic().DeleteExpense(id);
        public List<Expense> GetExpensesByOwner(int ownerId) => new ExpenseLogic().GetExpensesByOwner(ownerId);
        public List<Expense> GetExpensesByCategory(int ownerId, int catId)
            => new ExpenseLogic().GetExpensesByCategory(ownerId, catId);
        public List<Expense> GetExpensesByPeriod(int ownerId, DateTime from, DateTime to)
            => new ExpenseLogic().GetExpensesByPeriod(ownerId, from, to);
        public List<ExpenseCategory> GetExpenseCategories() => new ExpenseLogic().GetExpenseCategories();
        public string UploadReceipt(int expenseId, byte[] fileBytes, string fileName)
            => new ExpenseLogic().UploadReceipt(expenseId, fileBytes, fileName);

        // ===================================================================
        // REPORTS / VAT
        // ===================================================================

        public VatSummary GetVatSummary(int ownerId, int year, int month, string displayCurrency)
            => new ReportsLogic().GetVatSummary(ownerId, year, month, displayCurrency);
        public decimal GetMonthlyTaxSetAside(int ownerId, int year, int month, string displayCurrency)
            => new ReportsLogic().GetMonthlyTaxSetAside(ownerId, year, month, displayCurrency);
        public ProfitLoss GetProfitLoss(int ownerId, DateTime from, DateTime to, string displayCurrency)
            => new ReportsLogic().GetProfitLoss(ownerId, from, to, displayCurrency);
        public List<CustomerRevenueRow> GetTopCustomersByRevenue(int ownerId, string displayCurrency)
            => new ReportsLogic().GetTopCustomersByRevenue(ownerId, displayCurrency);
        public List<ExpenseBreakdownRow> GetExpenseBreakdown(int ownerId, DateTime from, DateTime to, string displayCurrency)
            => new ReportsLogic().GetExpenseBreakdown(ownerId, from, to, displayCurrency);
        public List<EmployeeRevenueRow> GetEmployeeRevenueReport(int ownerId, string displayCurrency)
            => new ReportsLogic().GetEmployeeRevenueReport(ownerId, displayCurrency);
        public List<ProfitLoss> GetCashFlowForecast(int ownerId, int months, string displayCurrency)
            => new ReportsLogic().GetCashFlowForecast(ownerId, months, displayCurrency);
        public AnalyticsKpis GetAdvancedKpis(int ownerId, string displayCurrency)
            => new ReportsLogic().GetAdvancedKpis(ownerId, displayCurrency);
        public ReportsSnapshot GetReportsSnapshot(int ownerId, int year, int month, string displayCurrency)
            => new ReportsLogic().GetReportsSnapshot(ownerId, year, month, displayCurrency);
        public OwnerDashboardSnapshot GetOwnerDashboardSnapshot(int ownerId, string displayCurrency)
            => new ReportsLogic().GetOwnerDashboardSnapshot(ownerId, displayCurrency);

        // ===================================================================
        // LOANS
        // ===================================================================

        public int  AddLoan(Loan l)                    => new LoanLogic().AddLoan(l);
        public void UpdateLoan(Loan l)                 => new LoanLogic().UpdateLoan(l);
        public void DeleteLoan(int id)                 => new LoanLogic().DeleteLoan(id);
        public Loan GetLoanById(int id)                => new LoanLogic().GetLoanById(id);
        public List<Loan> GetLoansForOwner(int ownerId) => new LoanLogic().GetLoansForOwner(ownerId);
        public int RecordLoanPayment(LoanPayment p)    => new LoanLogic().RecordLoanPayment(p);
        public List<LoanPayment> GetLoanPayments(int loanId) => new LoanLogic().GetLoanPayments(loanId);
        public LoanSummary GetLoanSummary(int ownerId, string displayCurrency)
            => new LoanLogic().GetLoanSummary(ownerId, displayCurrency);

        public int EnsureOverdueNotifications(int ownerId) => new NotificationLogic().EnsureOverdueNotifications(ownerId);

        // ===================================================================
        // CURRENCY
        // ===================================================================

        public double GetExchangeRate(string from, string to, DateTime asOfDate)
            => new CurrencyLogic().GetExchangeRate(from, to, asOfDate);
        public void SetExchangeRate(string from, string to, double rate)
            => new CurrencyLogic().SetExchangeRate(from, to, rate);
        public string[] GetSupportedCurrencies() => new CurrencyLogic().GetSupportedCurrencies();

        // ===================================================================
        // NOTIFICATIONS
        // ===================================================================

        public int SendNotification(Notification n)    => new NotificationLogic().SendNotification(n);
        public List<Notification> GetUserNotifications(int userId) => new NotificationLogic().GetUserNotifications(userId);
        public int GetUnreadNotificationCount(int userId)          => new NotificationLogic().GetUnreadNotificationCount(userId);
        public void MarkNotificationAsRead(int id)     => new NotificationLogic().MarkNotificationAsRead(id);
        public void MarkAllNotificationsAsRead(int userId) => new NotificationLogic().MarkAllNotificationsAsRead(userId);
        public void DeleteNotification(int id)         => new NotificationLogic().DeleteNotification(id);
    }
}
