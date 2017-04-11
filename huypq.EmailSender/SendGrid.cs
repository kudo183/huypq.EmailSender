using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace huypq.EmailSender
{
    //SendGrid account: kudo183, email: kudo183@gmail.com
    public static class SendGrid
    {
        public static void Send(string from, string to, string subject, string body)
        {
            var client = new SmtpClient();
            client.Host = "smtp.sendgrid.net";
            client.Port = 587;
            client.UseDefaultCredentials = false;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential("apikey", "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");
            try
            {
                client.Send(from, to, subject, body);
            }
            catch (Exception ex)
            {

            }
        }
    }
}
