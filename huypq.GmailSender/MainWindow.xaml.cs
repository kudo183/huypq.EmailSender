using huypq.GmailSender.Properties;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace huypq.GmailSender
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainWindowViewModel _vm = new MainWindowViewModel();
        DispatcherTimer _timer = new DispatcherTimer();
        const string SubjectTemplateFileNameSubfix = ".subject.txt";
        const string BodyTemplateFileNameSubfix = ".body.html";
        const string EmailKey = "$user";
        string _subjectTemplate = string.Empty;
        string _bodyTemplate = string.Empty;
        System.IO.DirectoryInfo _contentDirectoryInfo;
        Dictionary<string, EmailTemplate> _emailTemplateDictionary = new Dictionary<string, EmailTemplate>();

        class EmailTemplate
        {
            public string SubjectTemplate { get; set; }
            public string BodyTemplate { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();

            DataContext = _vm;
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;

            _timer.Interval = new TimeSpan(1 * TimeSpan.TicksPerSecond);
            _timer.Tick += Timer_Tick;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _vm.GmailAccount = Settings.Default.GmailAccount;
            _vm.MailFolderPath = Settings.Default.MailFolderPath;
            _vm.Interval = Settings.Default.Interval;
            _vm.MaxMessage = Settings.Default.MaxMessage;
            LoadMailContentFolderAndTemplate();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            Settings.Default.GmailAccount = _vm.GmailAccount;
            Settings.Default.MailFolderPath = _vm.MailFolderPath;
            Settings.Default.Interval = _vm.Interval;
            Settings.Default.MaxMessage = _vm.MaxMessage;
            Settings.Default.Save();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_vm.NextSend > 0)
            {
                _vm.NextSend = _vm.NextSend - 1;
                return;
            }

            _vm.NextSend = _vm.Interval;

            if (_contentDirectoryInfo == null)
                return;

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
                    if (mailContents.Length < 2)
                    {
                        continue;
                    }

                    var contentKeyValue = new Dictionary<string, string>();
                    for (int i = 1; i < mailContents.Length; i++)
                    {
                        var temp = mailContents[i].Split('\t');
                        contentKeyValue.Add(temp[0], temp[1]);
                    }

                    var emailTemplate = _emailTemplateDictionary[contentKeyValue["$purpose"].ToLower()];
                    var subject = emailTemplate.SubjectTemplate;
                    var body = emailTemplate.BodyTemplate;
                    var mailAddress = string.Empty;

                    foreach (var item in contentKeyValue)
                    {
                        subject = subject.Replace(item.Key, item.Value);
                        body = body.Replace(item.Key, item.Value);
                        if (string.IsNullOrEmpty(mailAddress) == true && item.Key == EmailKey)
                        {
                            mailAddress = item.Value;
                        }
                    }

                    var message = new MailMessage(_vm.GmailAccount, mailAddress, subject, body);

                    //cannot send because server IP is in black list, can check with https://mxtoolbox.com/SuperTool.aspx
                    var client = new SmtpClient("smtp.gmail.com", 587)
                    {
                        Credentials = new NetworkCredential(_vm.GmailAccount, _vm.Password),
                        EnableSsl = true
                    };
                    client.Send(message);

                    var sb = new StringBuilder();
                    sb.AppendLine(mailAddress);
                    sb.AppendLine(subject);
                    sb.AppendLine(body);
                    AddMessageAndScrollToEnd(sb.ToString());
                    fi.Delete();
                }
            }
            catch (Exception ex)
            {
                AddMessageAndScrollToEnd(ex.Message);
            }
        }

        void RunClick(object sender, RoutedEventArgs e)
        {
            _vm.CanRun = false;
            _vm.CanStop = true;
            _vm.NextSend = _vm.Interval;
            _timer.Start();
        }

        void StopClick(object sender, RoutedEventArgs e)
        {
            _vm.CanRun = true;
            _vm.CanStop = false;
            _timer.Stop();
        }

        System.Windows.Forms.FolderBrowserDialog _mailFolderPathFD = new System.Windows.Forms.FolderBrowserDialog();
        void MailFolderPathClick(object sender, RoutedEventArgs e)
        {
            if (_mailFolderPathFD.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _vm.MailFolderPath = _mailFolderPathFD.SelectedPath;
                LoadMailContentFolderAndTemplate();
            }
        }

        void LoadMailContentFolderAndTemplate()
        {
            if (System.IO.Directory.Exists(_vm.MailFolderPath) == true)
            {
                _contentDirectoryInfo = new System.IO.DirectoryInfo(_vm.MailFolderPath);
            }

            if (_contentDirectoryInfo == null)
            {
                return;
            }

            foreach (var fi in _contentDirectoryInfo.GetFiles())
            {
                var lowerName = fi.Name.ToLower();
                if (lowerName.EndsWith(SubjectTemplateFileNameSubfix) == true)
                {
                    var templateName = lowerName.Substring(1, lowerName.IndexOf('.') - 1);
                    if (_emailTemplateDictionary.TryGetValue(templateName, out EmailTemplate template) == false)
                    {
                        template = new EmailTemplate();
                        _emailTemplateDictionary.Add(templateName, template);
                    }
                    template.SubjectTemplate = System.IO.File.ReadAllText(fi.FullName);
                }
                else if (lowerName.EndsWith(BodyTemplateFileNameSubfix) == true)
                {
                    var templateName = lowerName.Substring(1, lowerName.IndexOf('.') - 1);
                    if (_emailTemplateDictionary.TryGetValue(templateName, out EmailTemplate template) == false)
                    {
                        template = new EmailTemplate();
                        _emailTemplateDictionary.Add(templateName, template);
                    }
                    template.BodyTemplate = System.IO.File.ReadAllText(fi.FullName);
                }
            }
        }

        void AddMessageAndScrollToEnd(string msg)
        {
            if (_vm.Messages.Count == _vm.MaxMessage)
            {
                _vm.Messages.RemoveAt(0);
            }

            _vm.Messages.Add(msg);

            msgListBox.SelectedIndex = msgListBox.Items.Count - 1;
            msgListBox.ScrollIntoView(msgListBox.SelectedItem);
        }

        private void TestClick(object sender, RoutedEventArgs e)
        {
            var client = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(_vm.GmailAccount, _vm.Password),
                EnableSsl = true
            };
            client.Send(_vm.GmailAccount, _vm.GmailAccount, "test", "test");
            AddMessageAndScrollToEnd("Test send mail success.");
        }

        private void passwordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _vm.Password = passwordBox.Password;
        }
    }
}
