using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using ToolControl.Contracts;
using ToolControl.Service;

namespace ToolControl.ServiceHost
{
    internal static class Program
    {
        private static void Main()
        {
            var baseAddress = new Uri("net.tcp://localhost:9000/ToolControl");
            using (var host = new ServiceHost(typeof(ToolControlService), baseAddress))
            {
                var binding = new NetTcpBinding(SecurityMode.None)
                {
                    ReliableSession = { Enabled = true },
                    SendTimeout = TimeSpan.FromSeconds(30),
                    ReceiveTimeout = TimeSpan.FromMinutes(10)
                };
                host.AddServiceEndpoint(typeof(IToolControlService), binding, string.Empty);
                host.Description.Behaviors.Add(new ServiceMetadataBehavior { HttpGetEnabled = false });
                host.Open();
                Console.WriteLine("Tool Control Service is running at " + baseAddress);
                Console.WriteLine("Press ENTER to stop.");
                Console.ReadLine();
                host.Close();
            }
        }
    }
}
