using System.Collections.Generic;
using System.Linq;
using BManagedWeb.bsrv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BManagedWeb.Pages.Owner
{
    public class ProjectsModel : PageModel
    {
        private readonly Service1Client _srv = new Service1Client();

        [BindProperty(SupportsGet = true)] public string StatusFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string Q { get; set; }
        [BindProperty] public int NewCustomerId { get; set; }
        [BindProperty] public string NewTitle { get; set; }
        [BindProperty] public decimal NewBudget { get; set; }

        public List<Project> Projects { get; set; } = new();
        public List<Customer> Customers { get; set; } = new();

        public IActionResult OnGet()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            int id = HttpContext.Session.GetInt32("UserId") ?? 0;

            var custArr = _srv.GetCustomersForOwner(id);
            Customers = custArr?.ToList() ?? new List<Customer>();

            Project[] arr = string.IsNullOrEmpty(StatusFilter)
                ? Customers.SelectMany(c => _srv.GetProjectsByCustomer(c.Id) ?? new Project[0]).ToArray()
                : _srv.GetProjectsByStatus(StatusFilter, id);
            var list = arr?.ToList() ?? new List<Project>();
            if (!string.IsNullOrWhiteSpace(Q))
            {
                var q = Q.Trim().ToLowerInvariant();
                list = list.Where(p =>
                    (p.Title ?? "").ToLowerInvariant().Contains(q) ||
                    (p.Description ?? "").ToLowerInvariant().Contains(q)).ToList();
            }
            Projects = list;
            return Page();
        }

        public IActionResult OnPostAdd()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Owner") return RedirectToPage("/Login");
            if (NewCustomerId <= 0 || string.IsNullOrWhiteSpace(NewTitle)) return OnGet();
            var currency = HttpContext.Session.GetString("Currency") ?? "ILS";

            _srv.AddProject(new Project
            {
                CustomerId = NewCustomerId,
                Title = NewTitle,
                Status = "Active",
                StartDate = System.DateTime.Today,
                DueDate = System.DateTime.Today.AddDays(30),
                TotalBudget = NewBudget,
                Currency = currency
            });
            return RedirectToPage();
        }

        // ---- Multi-employee assignment ----

        public IActionResult OnGetAssignees(int projectId)
        {
            if (HttpContext.Session.GetString("Role") != "Owner")
                return new JsonResult(new { error = "unauthorized" });
            try
            {
                var assigned = _srv.GetProjectAssignees(projectId) ?? new User[0];
                var allUsers = _srv.GetAllUsers();
                IEnumerable<User> all = allUsers != null ? (IEnumerable<User>)allUsers : new User[0];
                var assignedIds = assigned.Select(u => u.Id).ToHashSet();
                var available = all
                    .Where(u => u.Role == "Employee" && u.IsActive && !assignedIds.Contains(u.Id))
                    .Select(u => new { id = u.Id, username = u.Username })
                    .ToArray();
                var assignedDto = assigned
                    .Select(u => new { id = u.Id, username = u.Username })
                    .ToArray();
                return new JsonResult(new { assigned = assignedDto, available });
            }
            catch (System.Exception ex) { return new JsonResult(new { error = ex.Message }); }
        }

        public IActionResult OnPostAddAssignee(int projectId, int employeeId)
        {
            if (HttpContext.Session.GetString("Role") != "Owner") return Unauthorized();
            try { _srv.AddProjectAssignment(projectId, employeeId); return new JsonResult(new { ok = true }); }
            catch (System.Exception ex) { return new JsonResult(new { error = ex.Message }); }
        }

        public IActionResult OnPostRemoveAssignee(int projectId, int employeeId)
        {
            if (HttpContext.Session.GetString("Role") != "Owner") return Unauthorized();
            try { _srv.RemoveProjectAssignment(projectId, employeeId); return new JsonResult(new { ok = true }); }
            catch (System.Exception ex) { return new JsonResult(new { error = ex.Message }); }
        }

        public IActionResult OnPostStatus(int projectId, string newStatus)
        {
            if (HttpContext.Session.GetString("Role") != "Owner") return Unauthorized();
            try { _srv.SetProjectStatus(projectId, newStatus); }
            catch { }
            return RedirectToPage(new { Q, StatusFilter });
        }
    }
}
