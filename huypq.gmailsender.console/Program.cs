using huypq.EmailTemplateProcessor;
using huypq.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Net.Mail;
using System.ServiceProcess;

namespace huypq.gmailsender.console
{
    class Program
    {
        public static string ServiceName = "ESP8266GmailSender";

        public static void Start(string[] args)
        {
            int interval = int.Parse(ConfigurationManager.AppSettings["interval"]) * 1000;
            var user = ConfigurationManager.AppSettings["user"];
            var pass = ConfigurationManager.AppSettings["pass"];

            //ensure elasticsearch is started
            var elasticsearchURL = "http://localhost:9200";
            var httpClient = new HttpClient();
            var isElasticsearchStarted = false;
            while (isElasticsearchStarted == false)
            {
                try
                {
                    var result = httpClient.GetAsync(elasticsearchURL).Result;
                    isElasticsearchStarted = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                System.Threading.Thread.Sleep(2000);
            }

            var logWriters = new List<ILogBatchWriter>();
            if (Environment.UserInteractive)
            {
                logWriters.Add(new ConsoleBatchWriter());
            }
            logWriters.Add(new ElasticsearchBatchWriter(elasticsearchURL, "mailsender"));
            var loggerProvider = new LoggerProvider((cat, logLevel) => logLevel >= LogLevel.Information, false, new LoggerBatchingProcessor(logWriters));
            var gmailSender = new GmailSender(loggerProvider.CreateLogger<GmailSender>(), user, pass);
            
            var config = new Config()
            {
                SubjectTemplateFileNameSubfix = ConfigurationManager.AppSettings["SubjectTemplateFileNameSubfix"],
                BodyTemplateFileNameSubfix = ConfigurationManager.AppSettings["BodyTemplateFileNameSubfix"],
                MailFolderPath = ConfigurationManager.AppSettings["MailFolderPath"],
                EmailKey = ConfigurationManager.AppSettings["EmailKey"],
                PurposeKey = ConfigurationManager.AppSettings["PurposeKey"],
                ProcessedFolderName = ConfigurationManager.AppSettings["ProcessedFolderName"],
                TemplateFolder = ConfigurationManager.AppSettings["TemplateFolder"],
                Interval = interval
            };

            var processor = new EmailTemplateProcessor.EmailTemplateProcessor(gmailSender,
                loggerProvider.CreateLogger<EmailTemplateProcessor.EmailTemplateProcessor>(), config);

            if (processor.Start() == false)
                return;

            if (Environment.UserInteractive == true)
            {
                Console.Read();
            }
        }

        static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                // running as console app
                Start(args);
            }
            else
            {
                // running as service
                using (var service = new MyService())
                {
                    ServiceBase.Run(service);
                }
            }
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
