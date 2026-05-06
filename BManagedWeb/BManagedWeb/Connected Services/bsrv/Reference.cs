// ----------------------------------------------------------------------------
// Hand-written equivalent of dotnet-svcutil-generated Reference.cs.
// Lets BManagedWeb compile without running the svcutil tool.
//
// .NET 8 ServiceModel does NOT read app.config — Service1Client below
// configures BasicHttpBinding programmatically using the URL from
// appsettings.json (or hardcoded fallback localhost:8744).
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace BManagedWeb.bsrv
{
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model")]
    public class Base { [DataMember] public int Id { get; set; } }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model")]
    public class User : Base
    {
        [DataMember] public string Username { get; set; }
        [DataMember] public string PasswordHash { get; set; }
        [DataMember] public string Email { get; set; }
        [DataMember] public string Phone { get; set; }
        [DataMember] public string Role { get; set; }
        [DataMember] public bool IsActive { get; set; }
        [DataMember] public DateTime CreatedAt { get; set; }
        [DataMember] public string PreferredCurrency { get; set; }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model")]
    public class Customer : Base
    {
        [DataMember] public string BusinessName { get; set; }
        [DataMember] public string ContactName { get; set; }
        [DataMember] public string Email { get; set; }
        [DataMember] public string Phone { get; set; }
        [DataMember] public string TaxId { get; set; }
        [DataMember] public string Address { get; set; }
        [DataMember] public int OwnerId { get; set; }
        [DataMember] public string PreferredCurrency { get; set; }
        [DataMember] public string Notes { get; set; }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model")]
    public class Project : Base
    {
        [DataMember] public int CustomerId { get; set; }
        [DataMember] public string Title { get; set; }
        [DataMember] public string Description { get; set; }
        [DataMember] public string Status { get; set; }
        [DataMember] public DateTime? StartDate { get; set; }
        [DataMember] public DateTime? DueDate { get; set; }
        [DataMember] public int? AssignedEmployeeId { get; set; }
        [DataMember] public decimal TotalBudget { get; set; }
        [DataMember] public string Currency { get; set; }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model")]
    public class Invoice : Base
    {
        [DataMember] public string InvoiceNumber { get; set; }
        [DataMember] public int? ProjectId { get; set; }
        [DataMember] public int CustomerId { get; set; }
        [DataMember] public DateTime IssueDate { get; set; }
        [DataMember] public DateTime DueDate { get; set; }
        [DataMember] public decimal Subtotal { get; set; }
        [DataMember] public double VatRate { get; set; }
        [DataMember] public decimal VatAmount { get; set; }
        [DataMember] public decimal Total { get; set; }
        [DataMember] public string Currency { get; set; }
        [DataMember] public string Status { get; set; }
        [DataMember] public DateTime? PaidDate { get; set; }
        [DataMember] public string Notes { get; set; }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model")]
    public class InvoiceLine : Base
    {
        [DataMember] public int InvoiceId { get; set; }
        [DataMember] public string Description { get; set; }
        [DataMember] public double Quantity { get; set; }
        [DataMember] public decimal UnitPrice { get; set; }
        [DataMember] public decimal LineTotal { get; set; }
        [DataMember] public string Currency { get; set; }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model")]
    public class Expense : Base
    {
        [DataMember] public int OwnerId { get; set; }
        [DataMember] public int? CategoryId { get; set; }
        [DataMember] public DateTime Date { get; set; }
        [DataMember] public decimal Amount { get; set; }
        [DataMember] public decimal VatPaid { get; set; }
        [DataMember] public string Vendor { get; set; }
        [DataMember] public string Description { get; set; }
        [DataMember] public int? ProjectId { get; set; }
        [DataMember] public string ReceiptPath { get; set; }
        [DataMember] public string Currency { get; set; }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model")]
    public class ExpenseCategory : Base
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public bool IsVatDeductible { get; set; }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model")]
    public class Notification : Base
    {
        [DataMember] public int UserId { get; set; }
        [DataMember] public string Title { get; set; }
        [DataMember] public string Message { get; set; }
        [DataMember] public string NotificationType { get; set; }
        [DataMember] public bool IsRead { get; set; }
        [DataMember] public DateTime CreatedAt { get; set; }
        [DataMember] public DateTime? ReadAt { get; set; }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model")]
    public class ExchangeRate : Base
    {
        [DataMember] public string FromCurrency { get; set; }
        [DataMember] public string ToCurrency { get; set; }
        [DataMember] public double Rate { get; set; }
        [DataMember] public DateTime EffectiveDate { get; set; }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model")]
    public class TaxPeriod : Base
    {
        [DataMember] public int Year { get; set; }
        [DataMember] public int Month { get; set; }
        [DataMember] public decimal VatCollected { get; set; }
        [DataMember] public decimal VatPaid { get; set; }
        [DataMember] public decimal VatDue { get; set; }
        [DataMember] public decimal IncomeTotal { get; set; }
        [DataMember] public decimal ExpensesTotal { get; set; }
        [DataMember] public decimal ProfitEstimate { get; set; }
        [DataMember] public decimal TaxSetAside { get; set; }
        [DataMember] public string Currency { get; set; }
    }

    [CollectionDataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model",
                            ItemName = "User")]
    public class AllUsers : List<User> { }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model")]
    public class VatSummary
    {
        [DataMember] public int Year { get; set; }
        [DataMember] public int Month { get; set; }
        [DataMember] public decimal VatCollected { get; set; }
        [DataMember] public decimal VatPaid { get; set; }
        [DataMember] public decimal VatDue { get; set; }
        [DataMember] public decimal IncomeTotal { get; set; }
        [DataMember] public decimal ExpensesTotal { get; set; }
        [DataMember] public string DisplayCurrency { get; set; }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model")]
    public class ProfitLoss
    {
        [DataMember] public decimal Income { get; set; }
        [DataMember] public decimal Expenses { get; set; }
        [DataMember] public decimal Profit { get; set; }
        [DataMember] public decimal TaxSetAsideEstimate { get; set; }
        [DataMember] public string DisplayCurrency { get; set; }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model")]
    public class CustomerRevenueRow
    {
        [DataMember] public int CustomerId { get; set; }
        [DataMember] public string BusinessName { get; set; }
        [DataMember] public decimal TotalInvoiced { get; set; }
        [DataMember] public decimal TotalPaid { get; set; }
        [DataMember] public string DisplayCurrency { get; set; }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model")]
    public class ExpenseBreakdownRow
    {
        [DataMember] public string CategoryName { get; set; }
        [DataMember] public decimal Total { get; set; }
        [DataMember] public bool IsVatDeductible { get; set; }
        [DataMember] public string DisplayCurrency { get; set; }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Model")]
    public class EmployeeRevenueRow
    {
        [DataMember] public int EmployeeId { get; set; }
        [DataMember] public string EmployeeName { get; set; }
        [DataMember] public int ProjectsCount { get; set; }
        [DataMember] public decimal RevenueGenerated { get; set; }
        [DataMember] public string DisplayCurrency { get; set; }
    }

    [ServiceContract(Namespace = "http://tempuri.org/", ConfigurationName = "bsrv.IService1")]
    public interface IService1
    {
        [OperationContract] bool CheckUserPassword(string username, string password);
        [OperationContract] bool CheckUserExist(string username);
        [OperationContract] User GetUserById(int id);
        [OperationContract] int  GetUserId(string username);
        [OperationContract] bool AddUser(string username, string password, string email,
                                         string phone, string role, string preferredCurrency);
        [OperationContract] void ResetPassword(int userId, string newPassword);
        [OperationContract] void UpdateUserRole(int userId, string newRole);
        [OperationContract] bool IsOwner(string username);
        [OperationContract] AllUsers GetAllUsers();
        [OperationContract] List<User> GetAllEmployees();
        [OperationContract] void UpdateUserProfile(int userId, string email, string phone, string preferredCurrency);

        [OperationContract] int    AddCustomer(Customer c);
        [OperationContract] void   UpdateCustomer(Customer c);
        [OperationContract] void   DeleteCustomer(int id);
        [OperationContract] Customer GetCustomerById(int id);
        [OperationContract] Customer[] GetCustomersForOwner(int ownerId);
        [OperationContract] Customer[] SearchCustomers(string keyword, int ownerId);

        [OperationContract] int  AddProject(Project p);
        [OperationContract] void UpdateProject(Project p);
        [OperationContract] void SetProjectStatus(int projectId, string status);
        [OperationContract] void AssignEmployee(int projectId, int employeeId);
        [OperationContract] Project[] GetProjectsByCustomer(int customerId);
        [OperationContract] Project[] GetProjectsForEmployee(int employeeId);
        [OperationContract] Project[] GetProjectsByStatus(string status, int ownerId);
        [OperationContract] Project GetProjectById(int id);

        [OperationContract] int  CreateInvoice(Invoice inv);
        [OperationContract] int  AddInvoiceLine(InvoiceLine line);
        [OperationContract] void UpdateInvoiceStatus(int invoiceId, string status);
        [OperationContract] void MarkInvoicePaid(int invoiceId, DateTime paidDate);
        [OperationContract] void RecalcInvoiceTotals(int invoiceId);
        [OperationContract] Invoice GetInvoiceById(int id);
        [OperationContract] InvoiceLine[] GetInvoiceLines(int invoiceId);
        [OperationContract] Invoice[] GetInvoicesByCustomer(int customerId);
        [OperationContract] Invoice[] GetUnpaidInvoices(int ownerId);
        [OperationContract] Invoice[] GetOverdueInvoices(int ownerId);
        [OperationContract] byte[] GenerateInvoicePdf(int invoiceId);

        [OperationContract] int  AddExpense(Expense e);
        [OperationContract] void UpdateExpense(Expense e);
        [OperationContract] void DeleteExpense(int id);
        [OperationContract] Expense[] GetExpensesByOwner(int ownerId);
        [OperationContract] Expense[] GetExpensesByCategory(int ownerId, int categoryId);
        [OperationContract] Expense[] GetExpensesByPeriod(int ownerId, DateTime from, DateTime to);
        [OperationContract] ExpenseCategory[] GetExpenseCategories();

        [OperationContract] VatSummary GetVatSummary(int ownerId, int year, int month, string displayCurrency);
        [OperationContract] decimal    GetMonthlyTaxSetAside(int ownerId, int year, int month, string displayCurrency);
        [OperationContract] ProfitLoss GetProfitLoss(int ownerId, DateTime from, DateTime to, string displayCurrency);
        [OperationContract] CustomerRevenueRow[] GetTopCustomersByRevenue(int ownerId, string displayCurrency);
        [OperationContract] ExpenseBreakdownRow[] GetExpenseBreakdown(int ownerId, DateTime from, DateTime to, string displayCurrency);
        [OperationContract] EmployeeRevenueRow[]  GetEmployeeRevenueReport(int ownerId, string displayCurrency);

        [OperationContract] double GetExchangeRate(string from, string to, DateTime asOfDate);
        [OperationContract] void   SetExchangeRate(string from, string to, double rate);
        [OperationContract] string[] GetSupportedCurrencies();

        [OperationContract] int  SendNotification(Notification n);
        [OperationContract] Notification[] GetUserNotifications(int userId);
        [OperationContract] int  GetUnreadNotificationCount(int userId);
        [OperationContract] void MarkNotificationAsRead(int notificationId);
        [OperationContract] void MarkAllNotificationsAsRead(int userId);
        [OperationContract] void DeleteNotification(int notificationId);
    }

    public partial class Service1Client : System.ServiceModel.ClientBase<IService1>, IService1
    {
        // .NET 8 has no app.config; binding is configured programmatically
        // (URL fallback to localhost:8744 — change with WcfUrl env var).
        private const string DefaultUrl =
            "http://localhost:8744/Design_Time_Addresses/WcfServiceLibrary1/Service1/";

        public Service1Client()
            : base(MakeBinding(), MakeAddress()) { }
        public Service1Client(string url)
            : base(MakeBinding(), new EndpointAddress(url)) { }

        private static BasicHttpBinding MakeBinding() => new BasicHttpBinding
        {
            MaxReceivedMessageSize = 20_000_000,
            MaxBufferSize          = 20_000_000,
            OpenTimeout    = TimeSpan.FromSeconds(30),
            SendTimeout    = TimeSpan.FromSeconds(30),
            ReceiveTimeout = TimeSpan.FromSeconds(30),
        };

        private static EndpointAddress MakeAddress()
        {
            string url = Environment.GetEnvironmentVariable("WcfUrl") ?? DefaultUrl;
            return new EndpointAddress(url);
        }

        public bool CheckUserPassword(string u, string p) => Channel.CheckUserPassword(u, p);
        public bool CheckUserExist(string u) => Channel.CheckUserExist(u);
        public User GetUserById(int id) => Channel.GetUserById(id);
        public int  GetUserId(string u) => Channel.GetUserId(u);
        public bool AddUser(string u, string p, string e, string ph, string r, string c)
            => Channel.AddUser(u, p, e, ph, r, c);
        public void ResetPassword(int id, string np) => Channel.ResetPassword(id, np);
        public void UpdateUserRole(int id, string r) => Channel.UpdateUserRole(id, r);
        public bool IsOwner(string u) => Channel.IsOwner(u);
        public AllUsers GetAllUsers() => Channel.GetAllUsers();
        public List<User> GetAllEmployees() => Channel.GetAllEmployees();
        public void UpdateUserProfile(int id, string e, string p, string c) => Channel.UpdateUserProfile(id, e, p, c);

        public int      AddCustomer(Customer c)             => Channel.AddCustomer(c);
        public void     UpdateCustomer(Customer c)          => Channel.UpdateCustomer(c);
        public void     DeleteCustomer(int id)              => Channel.DeleteCustomer(id);
        public Customer GetCustomerById(int id)             => Channel.GetCustomerById(id);
        public Customer[] GetCustomersForOwner(int ownerId) => Channel.GetCustomersForOwner(ownerId);
        public Customer[] SearchCustomers(string k, int o)  => Channel.SearchCustomers(k, o);

        public int  AddProject(Project p)                  => Channel.AddProject(p);
        public void UpdateProject(Project p)               => Channel.UpdateProject(p);
        public void SetProjectStatus(int id, string s)     => Channel.SetProjectStatus(id, s);
        public void AssignEmployee(int id, int e)          => Channel.AssignEmployee(id, e);
        public Project[] GetProjectsByCustomer(int cId)    => Channel.GetProjectsByCustomer(cId);
        public Project[] GetProjectsForEmployee(int eId)   => Channel.GetProjectsForEmployee(eId);
        public Project[] GetProjectsByStatus(string s, int o) => Channel.GetProjectsByStatus(s, o);
        public Project   GetProjectById(int id)            => Channel.GetProjectById(id);

        public int  CreateInvoice(Invoice inv)             => Channel.CreateInvoice(inv);
        public int  AddInvoiceLine(InvoiceLine l)          => Channel.AddInvoiceLine(l);
        public void UpdateInvoiceStatus(int id, string s)  => Channel.UpdateInvoiceStatus(id, s);
        public void MarkInvoicePaid(int id, DateTime d)    => Channel.MarkInvoicePaid(id, d);
        public void RecalcInvoiceTotals(int id)            => Channel.RecalcInvoiceTotals(id);
        public Invoice     GetInvoiceById(int id)          => Channel.GetInvoiceById(id);
        public InvoiceLine[] GetInvoiceLines(int id)       => Channel.GetInvoiceLines(id);
        public Invoice[] GetInvoicesByCustomer(int cId)    => Channel.GetInvoicesByCustomer(cId);
        public Invoice[] GetUnpaidInvoices(int oId)        => Channel.GetUnpaidInvoices(oId);
        public Invoice[] GetOverdueInvoices(int oId)       => Channel.GetOverdueInvoices(oId);
        public byte[]    GenerateInvoicePdf(int id)        => Channel.GenerateInvoicePdf(id);

        public int  AddExpense(Expense e)                  => Channel.AddExpense(e);
        public void UpdateExpense(Expense e)               => Channel.UpdateExpense(e);
        public void DeleteExpense(int id)                  => Channel.DeleteExpense(id);
        public Expense[] GetExpensesByOwner(int oId)       => Channel.GetExpensesByOwner(oId);
        public Expense[] GetExpensesByCategory(int o, int c)=> Channel.GetExpensesByCategory(o, c);
        public Expense[] GetExpensesByPeriod(int o, DateTime f, DateTime t)
            => Channel.GetExpensesByPeriod(o, f, t);
        public ExpenseCategory[] GetExpenseCategories()    => Channel.GetExpenseCategories();

        public VatSummary GetVatSummary(int o, int y, int m, string c)
            => Channel.GetVatSummary(o, y, m, c);
        public decimal GetMonthlyTaxSetAside(int o, int y, int m, string c)
            => Channel.GetMonthlyTaxSetAside(o, y, m, c);
        public ProfitLoss GetProfitLoss(int o, DateTime f, DateTime t, string c)
            => Channel.GetProfitLoss(o, f, t, c);
        public CustomerRevenueRow[] GetTopCustomersByRevenue(int o, string c)
            => Channel.GetTopCustomersByRevenue(o, c);
        public ExpenseBreakdownRow[] GetExpenseBreakdown(int o, DateTime f, DateTime t, string c)
            => Channel.GetExpenseBreakdown(o, f, t, c);
        public EmployeeRevenueRow[] GetEmployeeRevenueReport(int o, string c)
            => Channel.GetEmployeeRevenueReport(o, c);

        public double GetExchangeRate(string f, string t, DateTime d) => Channel.GetExchangeRate(f, t, d);
        public void   SetExchangeRate(string f, string t, double r)   => Channel.SetExchangeRate(f, t, r);
        public string[] GetSupportedCurrencies()                      => Channel.GetSupportedCurrencies();

        public int  SendNotification(Notification n)                  => Channel.SendNotification(n);
        public Notification[] GetUserNotifications(int userId)        => Channel.GetUserNotifications(userId);
        public int  GetUnreadNotificationCount(int userId)            => Channel.GetUnreadNotificationCount(userId);
        public void MarkNotificationAsRead(int notificationId)        => Channel.MarkNotificationAsRead(notificationId);
        public void MarkAllNotificationsAsRead(int userId)            => Channel.MarkAllNotificationsAsRead(userId);
        public void DeleteNotification(int notificationId)            => Channel.DeleteNotification(notificationId);
    }
}
