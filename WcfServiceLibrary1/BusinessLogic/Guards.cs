using Model;
using System.ServiceModel;
using ViewDB;

namespace BusinessLogic
{
    /// <summary>
    /// Ownership / tenant-scoping guards shared by the logic classes. Each throws
    /// a FaultException if the entity does not belong to the given owner.
    /// Moved verbatim from the private Require* helpers that used to live in Service1.
    /// </summary>
    public static class Guards
    {
        public static void RequireCustomerOwner(int customerId, int ownerId)
        {
            if (!new CustomerDB().BelongsToOwner(customerId, ownerId))
                throw new FaultException("Customer does not belong to this owner.");
        }

        public static void RequireProjectOwner(int projectId, int ownerId)
        {
            if (!new ProjectDB().BelongsToOwner(projectId, ownerId))
                throw new FaultException("Project does not belong to this owner.");
        }

        public static void RequireInvoiceOwner(int invoiceId, int ownerId)
        {
            if (!new InvoiceDB().BelongsToOwner(invoiceId, ownerId))
                throw new FaultException("Invoice does not belong to this owner.");
        }

        public static void RequireEmployeeOwner(int employeeId, int ownerId)
        {
            var u = new UserDB().GetById(employeeId);
            if (u == null || u.Role != "Employee" || u.OwnerId != ownerId || !u.IsActive)
                throw new FaultException("Employee does not belong to this owner.");
        }
    }
}
