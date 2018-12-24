using System.ServiceProcess;

namespace huypq.gmailsender.console
{
    class MyService : ServiceBase
    {
        public MyService()
        {
            ServiceName = Program.ServiceName;
        }

        protected override void OnStart(string[] args)
        {
            Program.Start(args);
        }
    }
}
