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
            client.Credentials = new NetworkCredential("apikey", "");//need input key runtime instead of set in code.
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
