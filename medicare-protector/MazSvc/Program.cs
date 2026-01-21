using System;
using System.ServiceProcess;

namespace MazSvc
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new MazService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
