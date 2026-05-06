using BManagedClient.BMsrv;
using System;
using System.ServiceModel;

namespace BManagedClient
{
    public static class ServiceGateway
    {
        public static T Use<T>(Func<Service1Client, T> action)
        {
            var client = new Service1Client();
            try
            {
                T result = action(client);
                Close(client);
                return result;
            }
            catch
            {
                Abort(client);
                throw;
            }
        }

        public static void Use(Action<Service1Client> action)
        {
            Use(client =>
            {
                action(client);
                return true;
            });
        }

        private static void Close(Service1Client client)
        {
            try
            {
                if (client.State != CommunicationState.Faulted)
                    client.Close();
                else
                    client.Abort();
            }
            catch { client.Abort(); }
        }

        private static void Abort(Service1Client client)
        {
            try { client.Abort(); } catch { }
        }
    }

    /// <summary>
    /// Holds the currently signed-in user's identity + role for the WPF session.
    /// Roles in B-Managed: Owner, Employee, Client.
    /// </summary>
    public static class ClientSession
    {
        public static int CurrentUserId => LogIn.sign?.Id ?? -1;
        public static string Role => LogIn.sign?.Role ?? "";
        public static bool IsOwner    => Role == "Owner";
        public static bool IsEmployee => Role == "Employee";
        public static bool IsClient   => Role == "Client";
        public static bool IsLoggedIn => CurrentUserId > 0 && !string.IsNullOrEmpty(Role);
    }
}
