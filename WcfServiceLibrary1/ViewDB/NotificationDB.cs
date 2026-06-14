using Model;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;

namespace ViewDB
{
    /// <summary>
    /// Notifications table access. Same hardened pattern as Driver-moodle:
    /// direct OleDbCommand for mutations + InvalidOperationException so the
    /// real cause reaches WCF clients via FaultException.
    /// </summary>
    public class NotificationDB : BaseDB
    {
        protected override Base NewEntity() => new Notification();

        protected override void CreateModel(Base entity)
        {
            base.CreateModel(entity);
            if (!(entity is Notification n)) return;
            try { n.UserId           = Convert.ToInt32(reader["userId"]); }                catch { }
            try { n.Title            = reader["title"].ToString(); }                       catch { }
            try { n.Message          = reader["message"].ToString(); }                     catch { }
            try { n.NotificationType = reader["notificationType"].ToString(); }            catch { n.NotificationType = "Info"; }
            try { n.IsRead           = Convert.ToBoolean(reader["isRead"]); }              catch { }
            try { n.CreatedAt        = DateTime.Parse(reader["createdAt"].ToString()); }   catch { }
            try
            {
                var v = reader["readAt"];
                n.ReadAt = v == DBNull.Value ? (DateTime?)null : DateTime.Parse(v.ToString());
            } catch { }
        }

        public List<Notification> GetByUser(int userId)
            => Select("SELECT * FROM [Notifications] WHERE [userId] = ? ORDER BY [createdAt] DESC",
                new OleDbParameter("@u", userId)).OfType<Notification>().ToList();

        public int UnreadCount(int userId)
        {
            object r = SelectScalar(
                "SELECT COUNT(*) FROM [Notifications] WHERE [userId] = ? AND [isRead] = ?",
                new OleDbParameter("@u", userId),
                new OleDbParameter("@r", false));
            return (r != null && r != DBNull.Value) ? Convert.ToInt32(r) : 0;
        }

        public int Insert(Notification n)
        {
            string sql = @"INSERT INTO [Notifications]
                ([userId],[title],[message],[notificationType],[isRead],[createdAt])
                VALUES (?,?,?,?,?,?)";
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(sql, conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@u",  OleDbType.Integer)        { Value = n.UserId });
                cmd.Parameters.Add(new OleDbParameter("@t",  OleDbType.VarWChar, 150)  { Value = n.Title ?? "" });
                cmd.Parameters.Add(new OleDbParameter("@m",  OleDbType.LongVarWChar)   { Value = n.Message ?? "" });
                cmd.Parameters.Add(new OleDbParameter("@nt", OleDbType.VarWChar, 30)   { Value = n.NotificationType ?? "Info" });
                cmd.Parameters.Add(new OleDbParameter("@r",  OleDbType.Boolean)        { Value = false });
                // DBTimeStamp preserves time-of-day; OleDbType.Date truncates to midnight.
                cmd.Parameters.Add(new OleDbParameter("@c",  OleDbType.DBTimeStamp)    { Value = n.CreatedAt });
                try
                {
                    conn.Open();
                    int affected = cmd.ExecuteNonQuery();
                    if (affected <= 0)
                        throw new InvalidOperationException("Notifications INSERT affected 0 rows.");
                    return affected;
                }
                catch (OleDbException ex)
                {
                    throw new InvalidOperationException("Notifications INSERT failed: " + ex.Message, ex);
                }
            }
        }

        public void MarkAsRead(int notificationId)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                "UPDATE [Notifications] SET [isRead]=?, [readAt]=? WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@r",  OleDbType.Boolean)     { Value = true });
                // DBTimeStamp preserves time-of-day; OleDbType.Date truncates to midnight.
                cmd.Parameters.Add(new OleDbParameter("@d",  OleDbType.DBTimeStamp) { Value = DateTime.Now });
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer)     { Value = notificationId });
                conn.Open();
                if (cmd.ExecuteNonQuery() <= 0)
                    throw new InvalidOperationException("MarkAsRead: id=" + notificationId + " not found.");
            }
        }

        public void MarkAllAsRead(int userId)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand(
                @"UPDATE [Notifications] SET [isRead]=?, [readAt]=?
                  WHERE [userId]=? AND [isRead]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@r",  OleDbType.Boolean)     { Value = true });
                // DBTimeStamp preserves time-of-day; OleDbType.Date truncates to midnight.
                cmd.Parameters.Add(new OleDbParameter("@d",  OleDbType.DBTimeStamp) { Value = DateTime.Now });
                cmd.Parameters.Add(new OleDbParameter("@u",  OleDbType.Integer) { Value = userId });
                cmd.Parameters.Add(new OleDbParameter("@ro", OleDbType.Boolean) { Value = false });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void Delete(int notificationId)
        {
            using (var conn = GetConnection())
            using (var cmd = new OleDbCommand("DELETE FROM [Notifications] WHERE [id]=?", conn))
            {
                cmd.Parameters.Add(new OleDbParameter("@id", OleDbType.Integer) { Value = notificationId });
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
