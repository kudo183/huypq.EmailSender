using huypq.EmailTemplateProcessor;
using huypq.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Mail;

namespace huypq.gmailsender.console
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 6 && (args.Length != 7 || args[6] != "-t"))
            {
                PrintHelp();
                return;
            }
            Dictionary<String, String> arguments = new Dictionary<string, string>();
            arguments.Add(args[0], args[1]);
            arguments.Add(args[2], args[3]);
            arguments.Add(args[4], args[5]);
            if (arguments.ContainsKey("-i") == false
                || arguments.ContainsKey("-u") == false
                || arguments.ContainsKey("-p") == false)
            {
                PrintHelp();
                return;
            }

            int interval = int.Parse(arguments["-i"]) * 1000;
            var user = arguments["-u"];
            var pass = arguments["-p"];

            var loggerProvider = new LoggerProvider((cat, logLevel) => logLevel >= LogLevel.Information, false, new LoggerProcessor());
            var gmailSender = new GmailSender(loggerProvider.CreateLogger<GmailSender>(), user, pass);
            if (args.Length == 7 && gmailSender.SendTestMail() == false)
            {
                return;
            }

            var config = new Config()
            {
                SubjectTemplateFileNameSubfix = ".subject.txt",
                BodyTemplateFileNameSubfix = ".body.html",
                MailFolderPath = "./emails",
                EmailKey = "$user",
                PurposeKey = "$purpose",
                ProcessedFolderName = "processed",
                TemplateFolder = "template",
                Interval = interval
            };

            var processor = new EmailTemplateProcessor.EmailTemplateProcessor(gmailSender,
                loggerProvider.CreateLogger<EmailTemplateProcessor.EmailTemplateProcessor>(), config);

            if (processor.Start() == false)
                return;

            Console.Read();
        }

        static void PrintHelp()
        {
            Console.WriteLine("Example:");
            Console.WriteLine("huypq.gmailsender.console.exe -i 10 -u mail@gmail.com -p password -t");
            Console.WriteLine();
            Console.WriteLine("\t-i\t interval (in seconds)");
            Console.WriteLine("\t-u\t gmail address");
            Console.WriteLine("\t-p\t gmail password");
            Console.WriteLine("\t-t\t send test mail. Optional, must be last parameter if present.");
        }
    }

    class GmailSender : ISender
    {
        readonly string _user;
        readonly string _pass;
        readonly ILogger _logger;
        public GmailSender(ILogger logger, string user, string pass)
        {
            _logger = logger;
            _user = user;
            _pass = pass;
        }

        public bool SendTestMail()
        {
            _logger.LogInformation("SendTestMail");
            try
            {
                Send(_user, "test", "test");
                _logger.LogInformation(" success.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return false;
            }
        }

        public void Send(string to, string subject, string body)
        {
            var client = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new System.Net.NetworkCredential(_user, _pass),
                EnableSsl = true
            };
            var mail = new MailMessage(_user, to, subject, body);
            mail.IsBodyHtml = true;
            mail.BodyTransferEncoding = System.Net.Mime.TransferEncoding.QuotedPrintable;
            client.Send(mail);
        }
    }
}
