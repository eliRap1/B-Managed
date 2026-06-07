using Model;
using System.Collections.Generic;
using System.ServiceModel;
using ViewDB;

namespace BusinessLogic
{
    /// <summary>
    /// Business logic for customers / CRM, including the owner-scoped variants
    /// that enforce tenant ownership via Guards.
    /// </summary>
    public class CustomerLogic
    {
        private readonly CustomerDB custDB = new CustomerDB();

        public int  AddCustomer(Customer c)             => custDB.Insert(c);
        public void UpdateCustomer(Customer c)          => custDB.Update(c);
        public void DeleteCustomer(int id)              => custDB.Delete(id);
        public Customer GetCustomerById(int id)         => custDB.GetById(id);
        public Customer GetCustomerByIdForOwner(int id, int ownerId)
            => custDB.GetByIdForOwner(id, ownerId);
        public void UpdateCustomerForOwner(Customer c, int ownerId)
        {
            if (c == null) throw new FaultException("Customer is required.");
            Guards.RequireCustomerOwner(c.Id, ownerId);
            c.OwnerId = ownerId;
            custDB.Update(c);
        }
        public void DeleteCustomerForOwner(int id, int ownerId)
        {
            Guards.RequireCustomerOwner(id, ownerId);
            custDB.Delete(id);
        }
        public List<Customer> GetCustomersForOwner(int ownerId) => custDB.GetByOwner(ownerId);
        public List<Customer> SearchCustomers(string keyword, int ownerId)
            => custDB.Search(keyword, ownerId);
    }
}
