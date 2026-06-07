using Model;
using System;
using System.Collections.Generic;
using System.ServiceModel;
using ViewDB;

namespace BusinessLogic
{
    /// <summary>
    /// Business logic for notifications: send, query, read-state management, and
    /// the overdue-invoice notification generator (deduped by invoice number).
    /// </summary>
    public class NotificationLogic
    {
        private readonly NotificationDB notifDB = new NotificationDB();
        private readonly InvoiceDB      invDB   = new InvoiceDB();
        private readonly CustomerDB     custDB  = new CustomerDB();

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
    }
}
