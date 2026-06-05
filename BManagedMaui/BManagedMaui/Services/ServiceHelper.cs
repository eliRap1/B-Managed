using System.ServiceModel;
using BManagedMaui.BMsrv;

namespace BManagedMaui.Services;

/// <summary>
/// Builds and safely disposes WCF clients for Service1. Mirrors the
/// ServiceHelper concept from the Driver-moodle MAUI app: every call gets a
/// fresh client, and CallAsync guarantees it is closed (or aborted) afterwards.
/// </summary>
public static class ServiceHelper
{
    // WCF endpoint for the B-Managed Service1 (port 8733). Adjust per environment:
    //   • Android emulator      -> http://10.0.2.2:8733/...        (host loopback)
    //   • Physical phone/tablet -> http://<your-PC-WiFi-IP>:8733/...
    //   • Windows / Mac local   -> http://localhost:8733/...
    private const string ServiceUrl =
        "http://10.0.2.2:8733/Design_Time_Addresses/WcfServiceLibrary1/Service1/";

    public static Service1Client GetClient()
    {
        var binding = new BasicHttpBinding
        {
            MaxReceivedMessageSize = 5_000_000,
            OpenTimeout    = TimeSpan.FromSeconds(30),
            SendTimeout    = TimeSpan.FromSeconds(30),
            ReceiveTimeout = TimeSpan.FromSeconds(30),
        };
        return new Service1Client(binding, new EndpointAddress(ServiceUrl));
    }

    /// <summary>Runs a service call that returns a value, then closes the client.</summary>
    public static async Task<T> CallAsync<T>(Func<Service1Client, Task<T>> action)
    {
        var client = GetClient();
        try
        {
            return await action(client);
        }
        finally
        {
            try { await client.CloseAsync(); }
            catch { client.Abort(); }
        }
    }

    /// <summary>Runs a void service call, then closes the client.</summary>
    public static async Task CallAsync(Func<Service1Client, Task> action)
    {
        var client = GetClient();
        try
        {
            await action(client);
        }
        finally
        {
            try { await client.CloseAsync(); }
            catch { client.Abort(); }
        }
    }
}
