using huypq.EmailSender.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace huypq.EmailSender
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainWindowViewModel _vm = new MainWindowViewModel();
        DispatcherTimer _timer = new DispatcherTimer();
        const string SubjectTemplateFileName = "#subjecttemplate.txt";
        const string BodyTemplateFileName = "#bodytemplate.html";
        string _subjectTemplate = string.Empty;
        string _bodyTemplate = string.Empty;
        System.IO.DirectoryInfo _contentDirectoryInfo;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            _timer.Tick += Timer_Tick;

            //var mailMessage = new System.Net.Mail.MailMessage("gaucon.net<noreply@gaucon.net>", "quochuy101@gmail.com");

            //mailMessage.Subject = "xác nhận tài khoản tại gaucon.net";//dkim fail because has unicode character          
            ////mailMessage.Subject = "xac nhan tai khoan tai gaucon.net";

            //mailMessage.Body = "test";
            ////mailMessage.Body = System.IO.File.ReadAllText(@"c:\bodytemplate.html");
            ////mailMessage.BodyTransferEncoding = System.Net.Mime.TransferEncoding.QuotedPrintable;
            ////mailMessage.IsBodyHtml = true;

            ////var headers = new string[] { "From", "To", "Content-Type", "Date" };
            //var headers = new string[] { "From", "To", "Content-Type" };

            //DKIM.SignMailMessage.Sign(ref mailMessage, Encoding.UTF8, _dkimPrivateKeySigner, _vm.MailDomain, _vm.DkimSelector, headers);
            //try
            //{
            //    new SmtpClient("localhost").Send(mailMessage);
            //}
            //catch (Exception ex)
            //{

            //}

            //SendGrid.Send("noreply@luoithepvinhphat1.com", "kudo183@gmail.com", "test without MX record", "test");
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _vm.MailDomain = Settings.Default.MailDomain;
            _vm.MailFrom = Settings.Default.MailFrom;
            _vm.DkimPrivateKeyPath = Settings.Default.DkimPrivateKeyPath;
            _vm.DkimSelector = Settings.Default.DkimSelector;
            _vm.MailFolderPath = Settings.Default.MailFolderPath;
            _vm.Interval = Settings.Default.Interval;
            _vm.MaxMessage = Settings.Default.MaxMessage;
            LoadMailContentFolderAndTemplate();
            LoadDkimPrivateKey();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            Settings.Default.MailDomain = _vm.MailDomain;
            Settings.Default.MailFrom = _vm.MailFrom;
            Settings.Default.DkimPrivateKeyPath = _vm.DkimPrivateKeyPath;
            Settings.Default.DkimSelector = _vm.DkimSelector;
            Settings.Default.MailFolderPath = _vm.MailFolderPath;
            Settings.Default.Interval = _vm.Interval;
            Settings.Default.MaxMessage = _vm.MaxMessage;
            Settings.Default.Save();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            AddMessageAndScrollToEnd(DateTime.UtcNow.ToString());

            if (_contentDirectoryInfo == null)
                return;

            foreach (var fi in _contentDirectoryInfo.GetFiles())
            {
                var lowerName = fi.Name.ToLower();
                if (string.Equals(lowerName, SubjectTemplateFileName) == true || string.Equals(lowerName, BodyTemplateFileName) == true)
                {
                    continue;
                }

                var mailContents = System.IO.File.ReadAllLines(fi.FullName);
                if (mailContents.Length < 2)
                {
                    continue;
                }

                var mailAddress = System.IO.Path.GetFileNameWithoutExtension(fi.Name);

                var subject = string.Empty;
                if (string.IsNullOrEmpty(_subjectTemplate) == false)
                {
                    var subjectContent = mailContents[0].Split('\t');
                    if (subjectContent.Length == 2)
                    {
                        subject = _subjectTemplate.Replace(subjectContent[0], subjectContent[1]);
                    }
                    else
                    {
                        subject = mailContents[0];
                    }
                }
                else
                {
                    subject = mailContents[0];
                }

                var body = string.Empty;
                if (string.IsNullOrEmpty(_bodyTemplate) == false)
                {
                    var bodyContent = mailContents[1].Split('\t');
                    if (bodyContent.Length == 2)
                    {
                        body = _bodyTemplate.Replace(bodyContent[0], bodyContent[1]);

                        //replace other key
                        for (var i = 2; i < mailContents.Length; i++)
                        {
                            bodyContent = mailContents[i].Split('\t');
                            if (bodyContent.Length == 2)
                            {
                                body = body.Replace(bodyContent[0], bodyContent[1]);
                            }
                        }
                    }
                    else
                    {
                        body = mailContents[1];
                    }
                }
                else
                {
                    body = mailContents[1];
                }

                var message = new MailMessage(string.Format("{0}@{1}", _vm.MailFrom, _vm.MailDomain), mailAddress, subject, body);

                //have problem with Subject, Date
                var headers = new string[] { "From", "To", "Content-Type" };

                DKIM.SignMailMessage.Sign(ref message, Encoding.UTF8, _dkimPrivateKeySigner, _vm.MailDomain, _vm.DkimSelector, headers);

                //cannot send because server IP is in black list, can check with https://mxtoolbox.com/SuperTool.aspx
                try
                {
                    //EmailSender.Send(message);
                    //SendGrid.Send(message);
                    new SmtpClient("localhost").Send(message);

                    var sb = new StringBuilder();
                    sb.AppendLine(mailAddress);
                    sb.AppendLine(subject);
                    sb.AppendLine(body);
                    AddMessageAndScrollToEnd(sb.ToString());
                    fi.Delete();
                }
                catch (Exception ex)
                {
                    AddMessageAndScrollToEnd(ex.Message);
                }
            }
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

            var subjectTemplatePath = System.IO.Path.Combine(_vm.MailFolderPath, SubjectTemplateFileName);
            if (System.IO.File.Exists(subjectTemplatePath) == true)
            {
                _subjectTemplate = System.IO.File.ReadAllText(subjectTemplatePath);
            }

            var bodyTemplatePath = System.IO.Path.Combine(_vm.MailFolderPath, BodyTemplateFileName);
            if (System.IO.File.Exists(bodyTemplatePath) == true)
            {
                _bodyTemplate = System.IO.File.ReadAllText(bodyTemplatePath);
            }
        }

        Microsoft.Win32.OpenFileDialog _dkimPrivateKeyPathFD = new Microsoft.Win32.OpenFileDialog();
        DKIM.IPrivateKeySigner _dkimPrivateKeySigner;
        void DkimPrivateKeyPathClick(object sender, RoutedEventArgs e)
        {
            if (_dkimPrivateKeyPathFD.ShowDialog() == true)
            {
                _vm.DkimPrivateKeyPath = _dkimPrivateKeyPathFD.FileName;
                LoadDkimPrivateKey();
            }
        }

        void LoadDkimPrivateKey()
        {
            if (System.IO.File.Exists(_vm.DkimPrivateKeyPath) == true)
            {
                _dkimPrivateKeySigner = DKIM.PrivateKeySigner.LoadFromFile(_vm.DkimPrivateKeyPath);
            }
        }

        void RunClick(object sender, RoutedEventArgs e)
        {
            _vm.CanRun = false;
            _vm.CanStop = true;
            _timer.Interval = new TimeSpan(_vm.Interval * TimeSpan.TicksPerSecond);
            _timer.Start();
        }

        void StopClick(object sender, RoutedEventArgs e)
        {
            _vm.CanRun = true;
            _vm.CanStop = false;
            _timer.Stop();
        }

        int MaxMessage = 30;
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
    }
}
