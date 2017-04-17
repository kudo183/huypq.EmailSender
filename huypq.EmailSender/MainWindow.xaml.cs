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
            _timer.Tick += Timer_Tick;
            //SendGrid.Send("noreply@luoithepvinhphat1.com", "kudo183@gmail.com", "test without MX record", "test");
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            AddMessageAndScrollToEnd(DateTime.UtcNow.ToString());

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

                        //replace another key
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

                var message = new MailMessage(_vm.MailFrom, mailAddress, subject, body);
                var pks = DKIM.PrivateKeySigner.Create(@"-----BEGIN RSA PRIVATE KEY-----
MIICXgIBAAKBgQDIrjKcACPBYfnggPyWtuhHtj4EdAnD+1Q6deAwhj6vRXgxgVpa
ArMzEhYwSTVG2b9j7uA671UyMbTQY4802EEkpWD+gMsykIM9FnX3n8x/BM5D4I8Q
aoWgl2dv27teXrU8yTxjITkjfYGoC2XHGIl12mWvDWlDc23it2ulHhxsmwIDAQAB
AoGBAMAEjkBekdfT27hJM2cylIcphaXVMhLoROu7JkzH5urmWd9kmgNOWhEf2UNS
iTvF1i/UvZEmuCpTDWPoH3q9AXZRVi6VVpKBo5Jk5NLJMHJiAdSJTp/B3os1bfdP
ftwk+jiK1UDkDawQ/7wiNESwyKTo7YxjrcwCBnrJ4adpfPqhAkEA4tEQwhG2v8oi
0tnnxT2ApgVFu2c6lcUJlXgJuOMJExyoZ1keI3SqMOY3I6LmAYsVxWwp2M+o16Y+
sHixeoPKhwJBAOKAQKcTEZs8Jd9hor9CLkFPKcPiHqBsNcwXa4CqmGT+zAu906RN
nBppH+9aV2CmuiZfP6s1rx6JngyMXREZ7k0CQQDKRoRZbo6577yqqaYmoriuVplf
jaoTkwr+/3Q8LE0VfEM7F6DnIc39mfGmmEnjKnSaAeI39smuuBXEL36nsgzVAkEA
rBoxhgG6Pg784/gnFlZuGRfSr1FwXgr68aHWAIUfmJX2avGJD+4csACy0Eb/36Ow
DBoRocD/2B9PJU++I5FlfQJAPJrEGatvGuYnkiTHC3gFOtNn1SICrXUKHbtoxBC0
own4bVXnIYUpOigJpMJAG5Vk2enxWkjrAUjUaAEzYf6nCg==
-----END RSA PRIVATE KEY-----");

                var headers = new string[] { "From", "To", "Content-Type", "Date" };

                DKIM.SignMailMessage.Sign(ref message, Encoding.UTF8, _dkimPrivateKeySigner, _vm.MailDomain, _vm.DkimSelector, headers);

                //cannot send because server IP is in black list, can check with https://mxtoolbox.com/SuperTool.aspx

                EmailSender.Send(message);
                SendGrid.Send(message);
                new SmtpClient().Send(message);

                AddMessageAndScrollToEnd(mailAddress);
                AddMessageAndScrollToEnd(subject);
                AddMessageAndScrollToEnd(body);
                fi.Delete();
            }
        }

        System.Windows.Forms.FolderBrowserDialog _mailFolderPathFD = new System.Windows.Forms.FolderBrowserDialog();
        void MailFolderPathClick(object sender, RoutedEventArgs e)
        {
            if (_mailFolderPathFD.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _vm.MailFolderPath = _mailFolderPathFD.SelectedPath;
                _contentDirectoryInfo = new System.IO.DirectoryInfo(_vm.MailFolderPath);

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
        }

        Microsoft.Win32.OpenFileDialog _dkimPrivateKeyPathFD = new Microsoft.Win32.OpenFileDialog();
        DKIM.IPrivateKeySigner _dkimPrivateKeySigner;
        void DkimPrivateKeyPathClick(object sender, RoutedEventArgs e)
        {
            if (_dkimPrivateKeyPathFD.ShowDialog() == true)
            {
                _vm.DkimPrivateKeyPath = _dkimPrivateKeyPathFD.FileName;
                _dkimPrivateKeySigner = DKIM.PrivateKeySigner.LoadFromFile(_vm.DkimPrivateKeyPath);
            }
        }

        //void BodyTemplatePathClick(object sender, RoutedEventArgs e)
        //{
        //    var dlg = new Microsoft.Win32.OpenFileDialog();

        //    dlg.DefaultExt = ".html";
        //    dlg.Filter = "Html (*.html)|*.html|Text (*.txt)|*.txt";

        //    if (dlg.ShowDialog() == true)
        //    {
        //        _vm.MailBodyTemplatePath = dlg.FileName;
        //        _bodyTemplate = System.IO.File.ReadAllText(_vm.MailBodyTemplatePath);

        //        var rootPath = System.IO.Path.GetDirectoryName(_vm.MailBodyTemplatePath);
        //        _contentDirectoryInfo = new System.IO.DirectoryInfo(System.IO.Path.Combine(rootPath, "content"));
        //    }
        //}

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

        void AddMessageAndScrollToEnd(string msg)
        {
            _vm.Messages.Add(msg);

            if (msgListBox.Items.Count == 0)
                return;

            msgListBox.SelectedIndex = msgListBox.Items.Count - 1;
            msgListBox.ScrollIntoView(msgListBox.SelectedItem);
        }
    }
}
