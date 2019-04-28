using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace huypq.gmailsender.console
{
    [RunInstaller(true)]
    public class MyServiceInstaller : Installer
    {
        public MyServiceInstaller()
        {
            var spi = new ServiceProcessInstaller();
            var si = new ServiceInstaller();

            spi.Account = ServiceAccount.LocalSystem;
            spi.Username = null;
            spi.Password = null;

            si.DisplayName = Program.ServiceName;
            si.ServiceName = Program.ServiceName;
            si.ServicesDependedOn = new string[] { "mosquitto" };
            si.StartType = ServiceStartMode.Automatic;

            Installers.Add(spi);
            Installers.Add(si);
        }
    }
}
