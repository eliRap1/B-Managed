using Model;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using ViewDB;

namespace BusinessLogic
{
    /// <summary>
    /// Business logic for projects and project/employee assignments, including the
    /// owner-scoped variants that enforce tenant ownership via Guards.
    /// </summary>
    public class ProjectLogic
    {
        private readonly ProjectDB           projDB   = new ProjectDB();
        private readonly ProjectAssignmentDB assignDB = new ProjectAssignmentDB();
        private readonly UserDB              userDB   = new UserDB();

        public int  AddProject(Project p)               => projDB.Insert(p);
        public int  AddProjectForOwner(Project p, int ownerId)
        {
            if (p == null) throw new FaultException("Project is required.");
            Guards.RequireCustomerOwner(p.CustomerId, ownerId);
            return projDB.Insert(p);
        }
        public void UpdateProject(Project p)            => projDB.Update(p);
        public void SetProjectStatus(int id, string s)  => projDB.SetStatus(id, s);
        public void SetProjectStatusForOwner(int id, int ownerId, string s)
        {
            Guards.RequireProjectOwner(id, ownerId);
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
            Guards.RequireProjectOwner(projectId, ownerId);
            Guards.RequireEmployeeOwner(employeeId, ownerId);
            assignDB.Add(projectId, employeeId);
        }
        public void RemoveProjectAssignmentForOwner(int projectId, int ownerId, int employeeId)
        {
            Guards.RequireProjectOwner(projectId, ownerId);
            Guards.RequireEmployeeOwner(employeeId, ownerId);
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
    }
}
