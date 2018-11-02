using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace huypq.gmailsender.console
{
    class EmailTemplate
    {
        public string SubjectTemplate { get; set; }
        public string BodyTemplate { get; set; }
    }

    class Program
    {
        const string SubjectTemplateFileNameSubfix = ".subject.txt";
        const string BodyTemplateFileNameSubfix = ".body.html";
        const string MailFolderPath = "./emails";
        const string EmailKey = "$user";
        const string PurposeKey = "$purpose";
        const string ProcessedFolderName = "processed";
        const string TemplateFolder = "template";

        static System.Threading.Timer _timer;
        static System.IO.DirectoryInfo _contentDirectoryInfo;
        static System.IO.DirectoryInfo _processedDirectoryInfo;
        static Dictionary<string, EmailTemplate> _emailTemplateDictionary = new Dictionary<string, EmailTemplate>();
        static string User;
        static string Password;

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
            User = arguments["-u"];
            Password = arguments["-p"];

            if (System.IO.Directory.Exists(MailFolderPath) == false)
            {
                Console.WriteLine("emails folder not exist.");
                return;
            }
            _contentDirectoryInfo = new System.IO.DirectoryInfo(MailFolderPath);
            _processedDirectoryInfo = _contentDirectoryInfo.CreateSubdirectory(ProcessedFolderName);

            if (LoadTemplate() == false)
            {
                Console.WriteLine();
                Console.WriteLine("no template folder.");
                return;
            }

            if (_emailTemplateDictionary.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("no email template.");
                return;
            }

            foreach (var item in _emailTemplateDictionary)
            {
                Console.WriteLine();
                Console.WriteLine("*****Template Name: " + item.Key);
                Console.WriteLine("SubjectTemplate");
                Console.WriteLine(item.Value.SubjectTemplate);
                Console.WriteLine();
                Console.WriteLine("BodyTemplate");
                Console.WriteLine(item.Value.BodyTemplate);
            }
            Console.WriteLine();

            if (args.Length == 7 && SendTestMail() == false)
            {
                return;
            }
#if DEBUG
            _timer = new System.Threading.Timer(Callback, null, 0, interval);
#else
            _timer = new System.Threading.Timer(Callback, null, interval, interval);
#endif
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

        static void Callback(object obj)
        {
            Console.WriteLine("Callback");
            try
            {
                foreach (var fi in _contentDirectoryInfo.GetFiles())
                {
                    var lowerName = fi.Name.ToLower();
                    if (lowerName.EndsWith(SubjectTemplateFileNameSubfix) == true || lowerName.EndsWith(BodyTemplateFileNameSubfix) == true)
                    {
                        continue;
                    }

                    var mailContents = System.IO.File.ReadAllLines(fi.FullName);
                    //first line is type ("user" or "tenant", or something else) and another line for PurposeKey and EmailKey
                    if (mailContents.Length < 3)
                    {
                        continue;
                    }

                    var contentKeyValue = new Dictionary<string, string>();
                    for (int i = 1; i < mailContents.Length; i++)
                    {
                        var temp = mailContents[i].Split('\t');
                        contentKeyValue.Add(temp[0], temp[1]);
                    }

                    string templateName;
                    if (contentKeyValue.TryGetValue(PurposeKey, out templateName) == false)
                    {
                        Console.WriteLine(fi.Name + " not contain " + PurposeKey);
                        System.IO.File.Move(fi.FullName, System.IO.Path.Combine(_processedDirectoryInfo.FullName, fi.Name));
                        continue;
                    }

                    string mailAddress;
                    if (contentKeyValue.TryGetValue(EmailKey, out mailAddress) == false)
                    {
                        Console.WriteLine(fi.Name + " not contain " + EmailKey);
                        System.IO.File.Move(fi.FullName, System.IO.Path.Combine(_processedDirectoryInfo.FullName, fi.Name));
                        continue;
                    }

                    EmailTemplate emailTemplate;

                    var type = mailContents[0];
                    String key = type + "_" + templateName.ToLower();
                    if (_emailTemplateDictionary.TryGetValue(key, out emailTemplate) == false)
                    {
                        Console.WriteLine("no template for " + key);
                        System.IO.File.Move(fi.FullName, System.IO.Path.Combine(_processedDirectoryInfo.FullName, fi.Name));
                        continue;
                    }

                    var subject = emailTemplate.SubjectTemplate;
                    var body = emailTemplate.BodyTemplate;

                    foreach (var item in contentKeyValue)
                    {
                        subject = subject.Replace(item.Key, item.Value);
                        body = body.Replace(item.Key, item.Value);
                    }

                    SendGmail(mailAddress, subject, body);

                    //cannot send because server IP is in black list, can check with https://mxtoolbox.com/SuperTool.aspx


                    var sb = new StringBuilder();
                    sb.AppendLine("email: " + mailAddress + " template: " + key);
                    Console.WriteLine(sb.ToString());
                    fi.Delete();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            //clean all file
            foreach (var fi in _contentDirectoryInfo.GetFiles())
            {
                var lowerName = fi.Name.ToLower();
                if (lowerName.EndsWith(SubjectTemplateFileNameSubfix) == true || lowerName.EndsWith(BodyTemplateFileNameSubfix) == true)
                {
                    continue;
                }
                System.IO.File.Move(fi.FullName, System.IO.Path.Combine(_processedDirectoryInfo.FullName, fi.Name));
            }
        }

        static bool SendTestMail()
        {
            Console.Write("SendTestMail");
            try
            {
                SendGmail(User, "test", "test");
                Console.WriteLine(" success.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(" failed.");
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        static void SendGmail(string to, string subject, string body)
        {
            var client = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new System.Net.NetworkCredential(User, Password),
                EnableSsl = true
            };
            client.Send(User, to, subject, body);
        }

        static bool LoadTemplate()
        {
            System.IO.DirectoryInfo templateDirectoryInfo = null;
            string path = System.IO.Path.Combine(MailFolderPath, TemplateFolder);
            if (System.IO.Directory.Exists(path) == false)
            {
                return false;
            }
            templateDirectoryInfo = new System.IO.DirectoryInfo(path);

            var temp = new Dictionary<string, EmailTemplate>();
            foreach (var fi in templateDirectoryInfo.GetFiles())
            {
                var lowerName = fi.Name.ToLower();
                if (lowerName.EndsWith(SubjectTemplateFileNameSubfix) == true)
                {
                    var templateName = lowerName.Substring(1, lowerName.IndexOf('.') - 1);
                    if (temp.TryGetValue(templateName, out EmailTemplate template) == false)
                    {
                        template = new EmailTemplate();
                        temp.Add(templateName, template);
                    }
                    template.SubjectTemplate = System.IO.File.ReadAllText(fi.FullName);
                }
                else if (lowerName.EndsWith(BodyTemplateFileNameSubfix) == true)
                {
                    var templateName = lowerName.Substring(1, lowerName.IndexOf('.') - 1);
                    if (temp.TryGetValue(templateName, out EmailTemplate template) == false)
                    {
                        template = new EmailTemplate();
                        temp.Add(templateName, template);
                    }
                    template.BodyTemplate = System.IO.File.ReadAllText(fi.FullName);
                }
            }

            foreach (var item in temp)
            {
                if (item.Key.Split('_').Length != 2)
                {
                    Console.WriteLine("invalid template name: " + item.Key);
                    Console.WriteLine("Template name structure [type]_[purpose]");
                    continue;
                }
                if (string.IsNullOrEmpty(item.Value.SubjectTemplate) == true)
                {
                    Console.WriteLine(item.Key + " SubjectTemplate is empty");
                    continue;
                }
                if (string.IsNullOrEmpty(item.Value.BodyTemplate) == true)
                {
                    Console.WriteLine(item.Key + " BodyTemplate is empty");
                    continue;
                }
                _emailTemplateDictionary.Add(item.Key, item.Value);
            }

            return true;
        }

    }
}
