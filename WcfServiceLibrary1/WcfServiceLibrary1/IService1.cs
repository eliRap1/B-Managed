using Model;
using System;
using System.Collections.Generic;
using System.ServiceModel;

namespace WcfServiceLibrary1
{
    /// <summary>
    /// B-Managed service contract.  Operations are grouped by topic; each
    /// group has its own banner so the WSDL stays readable.  Returning
    /// concrete <see cref="Model"/> classes (not anonymous tuples) keeps
    /// the contract strongly-typed for both WPF and Web clients.
    /// </summary>
    [ServiceContract]
    public interface IService1
    {
        // ==================== AUTH & USERS ====================

        /// <summary>Verify password against stored PBKDF2 hash.</summary>
        [OperationContract] bool CheckUserPassword(string username, string password);

        /// <summary>True if the username exists (any role).</summary>
    [OperationContract] bool CheckUserExist(string username);

        /// <summary>Get a single user row (any role).</summary>
        [OperationContract] User GetUserById(int id);

        /// <summary>Resolve username -&gt; id (any role).</summary>
        [OperationContract] int GetUserId(string username);

        /// <summary>Insert a new user. Owner role is allowed only for the seed admin.</summary>
        [OperationContract] bool AddUser(string username, string password, string email,
                                         string phone, string role, string preferredCurrency);

        /// <summary>Replace an existing password (admin / self).</summary>
        [OperationContract] void ResetPassword(int userId, string newPassword);

        /// <summary>Promote / demote a user (Owner only).</summary>
        [OperationContract] void UpdateUserRole(int userId, string newRole);

        /// <summary>Owner inbox — users awaiting approval.</summary>
        [OperationContract] List<User> GetPendingUsers();

        /// <summary>Owner approves or blocks a user.</summary>
        [OperationContract] void SetUserActive(int userId, bool isActive);

        /// <summary>Hard delete a user.</summary>
        [OperationContract] void DeleteUser(int userId);

        /// <summary>True iff the user has role = "Owner".</summary>
        [OperationContract] bool IsOwner(string username);

        /// <summary>List every user (Owner-only UIs).</summary>
        [OperationContract] AllUsers GetAllUsers();

        /// <summary>List employees only (assignment dropdowns). DEPRECATED — use GetEmployeesForOwner for tenant-safe results.</summary>
        [OperationContract] List<User> GetAllEmployees();
        /// <summary>Tenant-scoped: every user in the Owner's company (Owner + their employees / clients).</summary>
        [OperationContract] List<User> GetUsersForOwner(int ownerId);
        /// <summary>Tenant-scoped: pending (inactive) users awaiting THIS Owner's approval.</summary>
        [OperationContract] List<User> GetPendingForOwner(int ownerId);
        /// <summary>Tenant-scoped: this Owner's active employees only — for project-assignment dropdowns.</summary>
        [OperationContract] List<User> GetEmployeesForOwner(int ownerId);

        /// <summary>Update non-credential profile fields.</summary>
        [OperationContract] void UpdateUserProfile(int userId, string email, string phone,
                                                   string preferredCurrency);
        [OperationContract] void SetBusinessType(int userId, string businessType);
        /// <summary>Toggle Osek Zair income-tax status (independent of BusinessType).</summary>
        [OperationContract] void SetIsZair(int userId, bool isZair);
        /// <summary>Link an Employee/Client to its Owner ('the company they belong to').</summary>
        [OperationContract] void SetOwnerId(int userId, int ownerId);
        /// <summary>List active Owners — kept for admin tooling. Employees use invite codes.</summary>
        [OperationContract] List<User> GetActiveOwners();
        /// <summary>Update the Owner's display business name (e.g. "Acme Studio").</summary>
        [OperationContract] void SetBusinessName(int userId, string businessName);
        /// <summary>Rotate the Owner's invite code; returns the new value.</summary>
        [OperationContract] string SetInviteCode(int userId, string inviteCode);
        /// <summary>Resolve invite code -> Owner. Used by Employee signup so the user does NOT see every company on the platform.</summary>
        [OperationContract] User GetOwnerByInviteCode(string code);

        // ==================== CUSTOMERS / CRM ====================

        [OperationContract] int    AddCustomer(Customer c);
        [OperationContract] void   UpdateCustomer(Customer c);
        [OperationContract] void   DeleteCustomer(int id);
        [OperationContract] Customer GetCustomerById(int id);
        [OperationContract] Customer GetCustomerByIdForOwner(int id, int ownerId);
        [OperationContract] void   UpdateCustomerForOwner(Customer c, int ownerId);
        [OperationContract] void   DeleteCustomerForOwner(int id, int ownerId);
        [OperationContract] List<Customer> GetCustomersForOwner(int ownerId);
        [OperationContract] List<Customer> SearchCustomers(string keyword, int ownerId);

        // ==================== PROJECTS ====================

        [OperationContract] int  AddProject(Project p);
        [OperationContract] int  AddProjectForOwner(Project p, int ownerId);
        [OperationContract] void UpdateProject(Project p);
        [OperationContract] void SetProjectStatus(int projectId, string status);
        [OperationContract] void SetProjectStatusForOwner(int projectId, int ownerId, string status);
        [OperationContract] void AssignEmployee(int projectId, int employeeId);
        [OperationContract] List<Project> GetProjectsByCustomer(int customerId);
        [OperationContract] List<Project> GetProjectsForEmployee(int employeeId);
        [OperationContract] List<Project> GetProjectsByStatus(string status, int ownerId);
        [OperationContract] Project GetProjectById(int id);
        [OperationContract] Project GetProjectByIdForOwner(int id, int ownerId);

        // Many-to-many: a project can have multiple assigned employees.
        [OperationContract] void AddProjectAssignment(int projectId, int employeeId);
        [OperationContract] void RemoveProjectAssignment(int projectId, int employeeId);
        [OperationContract] void AddProjectAssignmentForOwner(int projectId, int ownerId, int employeeId);
        [OperationContract] void RemoveProjectAssignmentForOwner(int projectId, int ownerId, int employeeId);
        [OperationContract] List<User> GetProjectAssignees(int projectId);

        // ==================== CONTRACTS ====================
        [OperationContract] int       CreateContract(Contract c);
        [OperationContract] void      UpdateContract(Contract c);
        [OperationContract] void      DeleteContract(int id);
        [OperationContract] void      MarkContractSigned(int id, DateTime signedDate);
        [OperationContract] Contract  GetContractById(int id);
        [OperationContract] List<Contract> GetContractsForOwner(int ownerId);
        [OperationContract] List<Contract> GetContractsByProject(int projectId);
        [OperationContract] List<Contract> GetContractsByCustomer(int customerId);
        [OperationContract] byte[]    GenerateContractPdf(int contractId);

        // ==================== INVOICES ====================

        /// <summary>Creates a Draft invoice with auto-generated invoiceNumber.
        /// Lines must be added separately via <see cref="AddInvoiceLine"/>.</summary>
        [OperationContract] int  CreateInvoice(Invoice inv);
        [OperationContract] int  CreateInvoiceForOwner(Invoice inv, int ownerId);
        [OperationContract] int  AddInvoiceLine(InvoiceLine line);
        [OperationContract] int  AddInvoiceLineForOwner(InvoiceLine line, int ownerId);
        [OperationContract] void UpdateInvoiceStatus(int invoiceId, string status);
        [OperationContract] void UpdateInvoiceStatusForOwner(int invoiceId, int ownerId, string status);
        [OperationContract] void MarkInvoicePaid(int invoiceId, DateTime paidDate);
        [OperationContract] void MarkInvoicePaidForOwner(int invoiceId, int ownerId, DateTime paidDate);
        [OperationContract] void RecalcInvoiceTotals(int invoiceId);
        [OperationContract] Invoice GetInvoiceById(int id);
        [OperationContract] Invoice GetInvoiceByIdForOwner(int id, int ownerId);
        [OperationContract] List<InvoiceLine> GetInvoiceLines(int invoiceId);
        [OperationContract] List<InvoiceLine> GetInvoiceLinesForOwner(int invoiceId, int ownerId);
        [OperationContract] List<Invoice> GetInvoicesByCustomer(int customerId);
        [OperationContract] List<Invoice> GetUnpaidInvoices(int ownerId);
        [OperationContract] List<Invoice> GetOverdueInvoices(int ownerId);
        /// <summary>All invoices for an Owner — single JOIN, replaces per-customer loop in UI.</summary>
        [OperationContract] List<Invoice> GetInvoicesForOwner(int ownerId);

        /// <summary>Render invoice as a PDF byte array using PdfSharp.</summary>
        [OperationContract] byte[] GenerateInvoicePdf(int invoiceId);
        [OperationContract] byte[] GenerateInvoicePdfForOwner(int invoiceId, int ownerId);

        // ==================== EXPENSES ====================

        [OperationContract] int  AddExpense(Expense e);
        [OperationContract] void UpdateExpense(Expense e);
        [OperationContract] void DeleteExpense(int id);
        [OperationContract] List<Expense> GetExpensesByOwner(int ownerId);
        [OperationContract] List<Expense> GetExpensesByCategory(int ownerId, int categoryId);
        [OperationContract] List<Expense> GetExpensesByPeriod(int ownerId, DateTime from, DateTime to);
        [OperationContract] List<ExpenseCategory> GetExpenseCategories();

        /// <summary>Persist receipt bytes; server writes to /Receipts and stores relative path on Expense.</summary>
        [OperationContract] string UploadReceipt(int expenseId, byte[] fileBytes, string fileName);

        // ==================== REPORTS / VAT ====================

        [OperationContract] VatSummary GetVatSummary(int ownerId, int year, int month, string displayCurrency);
        [OperationContract] decimal    GetMonthlyTaxSetAside(int ownerId, int year, int month, string displayCurrency);
        [OperationContract] ProfitLoss GetProfitLoss(int ownerId, DateTime from, DateTime to, string displayCurrency);
        [OperationContract] List<CustomerRevenueRow> GetTopCustomersByRevenue(int ownerId, string displayCurrency);
        [OperationContract] List<ExpenseBreakdownRow> GetExpenseBreakdown(int ownerId, DateTime from, DateTime to, string displayCurrency);
        [OperationContract] List<EmployeeRevenueRow>  GetEmployeeRevenueReport(int ownerId, string displayCurrency);

        // Project next-N-months cashflow based on trailing-3-month average + outstanding invoices.
        [OperationContract] List<ProfitLoss> GetCashFlowForecast(int ownerId, int months, string displayCurrency);

        // Composite KPI scorecard: receivables aging + payment lag + customer
        // concentration + trailing-3-month flow + runway. Used by Reports +
        // Owner Home dashboards.
        [OperationContract] AnalyticsKpis GetAdvancedKpis(int ownerId, string displayCurrency);
        /// <summary>Single payload for /Owner/Reports — replaces ~7 calls.</summary>
        [OperationContract] ReportsSnapshot GetReportsSnapshot(int ownerId, int year, int month, string displayCurrency);
        [OperationContract] OwnerDashboardSnapshot GetOwnerDashboardSnapshot(int ownerId, string displayCurrency);

        // ==================== LOANS (קרן / business loans) ====================
        [OperationContract] int  AddLoan(Loan l);
        [OperationContract] void UpdateLoan(Loan l);
        [OperationContract] void DeleteLoan(int id);
        [OperationContract] Loan GetLoanById(int id);
        [OperationContract] List<Loan> GetLoansForOwner(int ownerId);
        [OperationContract] int  RecordLoanPayment(LoanPayment p);
        [OperationContract] List<LoanPayment> GetLoanPayments(int loanId);
        [OperationContract] LoanSummary GetLoanSummary(int ownerId, string displayCurrency);

        // Walks unpaid invoices, fires a notification per overdue one (idempotent — checks
        // for an existing Notification with the same invoice number in title).
        [OperationContract] int EnsureOverdueNotifications(int ownerId);

        // ==================== CURRENCY ====================

        [OperationContract] double GetExchangeRate(string from, string to, DateTime asOfDate);
        [OperationContract] void   SetExchangeRate(string from, string to, double rate);
        [OperationContract] string[] GetSupportedCurrencies();

        // ==================== NOTIFICATIONS ====================

        [OperationContract] int  SendNotification(Notification n);
        [OperationContract] List<Notification> GetUserNotifications(int userId);
        [OperationContract] int  GetUnreadNotificationCount(int userId);
        [OperationContract] void MarkNotificationAsRead(int notificationId);
        [OperationContract] void MarkAllNotificationsAsRead(int userId);
        [OperationContract] void DeleteNotification(int notificationId);
    }
}
