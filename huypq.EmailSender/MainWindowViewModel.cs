using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace huypq.EmailSender
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public MainWindowViewModel()
        {
            messages = new ObservableCollection<string>();
            interval = 5;
            canRun = true;
            canStop = false;
        }

        private bool canRun;

        public bool CanRun
        {
            get { return canRun; }
            set
            {
                if (Equals(canRun, value))
                    return;

                canRun = value;
                OnPropertyChanged();
            }
        }

        private bool canStop;

        public bool CanStop
        {
            get { return canStop; }
            set
            {
                if (Equals(canStop, value))
                    return;

                canStop = value;
                OnPropertyChanged();
            }
        }

        private string mailFrom;

        public string MailFrom
        {
            get { return mailFrom; }
            set
            {
                if (Equals(mailFrom, value))
                    return;

                mailFrom = value;
                OnPropertyChanged();
            }
        }

        private string mailFolderPath;

        public string MailFolderPath
        {
            get { return mailFolderPath; }
            set
            {
                if (Equals(mailFolderPath, value))
                    return;

                mailFolderPath = value;
                OnPropertyChanged();
            }
        }

        private int interval;

        public int Interval
        {
            get { return interval; }
            set
            {
                if (Equals(interval, value))
                    return;

                interval = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<string> messages;

        public ObservableCollection<string> Messages
        {
            get { return messages; }
            set
            {
                if (Equals(messages, value))
                    return;

                messages = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
