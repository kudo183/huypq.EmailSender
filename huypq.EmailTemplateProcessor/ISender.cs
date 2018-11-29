using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace huypq.EmailTemplateProcessor
{
    public interface ISender
    {
        void Send(string to, string subject, string body);
    }
}
