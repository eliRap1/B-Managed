using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using WcfServiceLibrary1;

namespace ConsoleHost
{
    /// <summary>
    /// Standalone WCF host. Run this project (set as startup, F5) to expose
    /// Service1 at http://localhost:8733/. Endpoint config + behaviors are
    /// taken from this project's App.config.
    /// </summary>
    internal class Program
    {
        private static void Main()
        {
            using (var host = new ServiceHost(typeof(Service1)))
            {
                host.Opened += (s, e) =>
                {
                    Console.WriteLine("=========================================");
                    Console.WriteLine(" B-Managed WCF host running");
                    foreach (var ep in host.Description.Endpoints)
                        Console.WriteLine("  " + ep.Address);
                    Console.WriteLine("=========================================");
                    Console.WriteLine("Press <Enter> to stop.");
                };

                try
                {
                    host.Open();
                    Console.ReadLine();
                    host.Close();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Host failed: " + ex.Message);
                    Console.ResetColor();
                    Console.ReadLine();
                }
            }
        }
    }
}
