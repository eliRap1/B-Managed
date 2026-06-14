using Model;
using System;
using System.Collections.Generic;
using System.ServiceModel;
using ViewDB;

namespace BusinessLogic
{
    /// <summary>
    /// Business logic for contracts: lifecycle (create / sign / fulfil / delete),
    /// queries, and PDF generation via ContractPdfBuilder.
    /// </summary>
    public class ContractLogic
    {
        private readonly ContractDB contractDB = new ContractDB();
        private readonly CustomerDB custDB     = new CustomerDB();
        private readonly ProjectDB  projDB     = new ProjectDB();
        private readonly UserDB     userDB     = new UserDB();

        public int CreateContract(Contract c)
        {
            if (c == null) throw new FaultException("Contract is required.");
            if (c.CustomerId <= 0) throw new FaultException("CustomerId required.");
            return contractDB.Insert(c);
        }

        // TODO(audit): UpdateContract only persists Status + SignedDate (via SetStatus).
        // Title, Body, TotalAmount, Currency, PdfPath and all other fields on the
        // Contract object are silently discarded. Callers who expect a full update
        // (e.g. editing the contract body) will lose their changes with no error.
        // Add a ContractDB.Update(Contract) method that sets all editable columns,
        // and call it here instead of SetStatus.
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
            var pdf = new ContractPdfBuilder();
            return pdf.Render(c, cust, proj, owner?.Username);
        }
    }
}
