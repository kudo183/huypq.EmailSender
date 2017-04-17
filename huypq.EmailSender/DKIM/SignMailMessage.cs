using System;
using System.Net.Mail;
using System.Text;

namespace huypq.EmailSender.DKIM
{
    //base on https://github.com/dmcgiv/DKIM.Net
    public static class SignMailMessage
    {
        static DomainKeyCanonicalizationAlgorithm domainKeyCanonicalization = DomainKeyCanonicalizationAlgorithm.Simple;
        static DkimCanonicalizationAlgorithm dkimHeaderCanonicalization = DkimCanonicalizationAlgorithm.Simple;
        static DkimCanonicalizationAlgorithm dkimBodyCanonicalization = DkimCanonicalizationAlgorithm.Simple;

        static SigningAlgorithm signingAlgorithm = SigningAlgorithm.RSASha256;

        public static void Sign(ref MailMessage message, Encoding encoding, IPrivateKeySigner privateKeySigner, string domain, string selector, string[] headersToSign)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            if (encoding == null)
            {
                throw new ArgumentNullException("encoding");
            }

            message.BodyEncoding = encoding;
            message.SubjectEncoding = encoding;
            message.HeadersEncoding = encoding;

            var email = Parse(message);

            if (!CanSign(email))
            {
                throw new InvalidOperationException("Unable to Domain Key sign the message");
            }

            var signature = GenerateDomainKeySignature(email, encoding, privateKeySigner, domain, selector, headersToSign);

            message.Headers.Prepend(Constant.DomainKeySignatureKey, signature);

            email = Parse(message);

            var value = GenerateDkimHeaderValue(email, encoding, privateKeySigner, domain, selector, headersToSign);

            // signature value get formatted so add dummy signature value then remove it
            message.Headers.Prepend(Constant.DkimSignatureKey, value + new string('0', 70));
            email = Parse(message);
            var formattedSig = email.Headers[Constant.DkimSignatureKey].Value;
            email.Headers[Constant.DkimSignatureKey].Value = formattedSig.Substring(0, formattedSig.Length - 70);

            // sign email
            value += GenerateDkimSignature(email, encoding, privateKeySigner, headersToSign);
            message.Headers.Set(Constant.DkimSignatureKey, value);
        }

        private static Email Parse(MailMessage message)
        {
            var text = message.GetText();
            var email = Email.Parse(text);
            return email;
        }

        private static bool CanSign(Email email)
        {

            if (email.Headers.ContainsKey("Content-Type"))
            {
                // fails for:
                // multipart/alternative
                // multipart/mixed

                return !email.Headers["Content-Type"].Value.Trim()
                    .StartsWith("multipart/", StringComparison.InvariantCultureIgnoreCase);
            }

            return true;
        }

        private static string GenerateDomainKeySignature(Email email, Encoding encoding, IPrivateKeySigner privateKeySigner, string domain, string selector, string[] headersToSign)
        {

            var signatureValue = new StringBuilder();

            const string start = /*Email.NewLine +*/ " ";
            const string end = ";";

            // algorithm used
            //signatureValue.Append(" ");
            signatureValue.Append("a=");
            signatureValue.Append("rsa-sha1");// only rsa-sha1 suprted
            signatureValue.Append(end);


            // Canonicalization
            signatureValue.Append(start);
            signatureValue.Append("c=");
            signatureValue.Append(domainKeyCanonicalization.ToString().ToLower());
            signatureValue.Append(end);


            // signing domain
            signatureValue.Append(start);
            signatureValue.Append("d=");
            signatureValue.Append(domain);
            signatureValue.Append(end);


            // headers to be signed
            if (headersToSign != null && headersToSign.Length > 0)
            {
                signatureValue.Append(start);
                signatureValue.Append("h=");
                foreach (var header in headersToSign)
                {
                    signatureValue.Append(header);
                    signatureValue.Append(':');
                }
                signatureValue.Length--;
                signatureValue.Append(end);
            }


            // public key location
            signatureValue.Append(start);
            signatureValue.Append("q=dns");
            signatureValue.Append(end);

            // selector
            signatureValue.Append(start);
            signatureValue.Append("s=");
            signatureValue.Append(selector);
            signatureValue.Append(end);


            // signature data
            signatureValue.Append(start);
            signatureValue.Append("b=");

            signatureValue.Append(DomainKeySignSignature(email, encoding, privateKeySigner, headersToSign));
            signatureValue.Append(end);

            return signatureValue.ToString();
        }

        private static string DomainKeySignSignature(Email email, Encoding encoding, IPrivateKeySigner privateKeySigner, string[] headersToSign)
        {
            var text = DomainKeyCanonicalizer.Canonicalize(email, domainKeyCanonicalization, headersToSign);

            return Convert.ToBase64String(privateKeySigner.Sign(encoding.GetBytes(text), SigningAlgorithm.RSASha1));
        }

        //see http://www.dkim.org/specs/rfc4871-dkimbase.html#dkim-sig-hdr
        private static string GenerateDkimHeaderValue(Email email, Encoding encoding, IPrivateKeySigner privateKeySigner, string domain, string selector, string[] headersToSign)
        {
            // timestamp  - seconds since 00:00:00 on January 1, 1970 UTC
            TimeSpan t = DateTime.Now.ToUniversalTime() -
                         DateTime.SpecifyKind(DateTime.Parse("00:00:00 January 1, 1970"), DateTimeKind.Utc);

            var signatureValue = new StringBuilder();

            const string start = /*Email.NewLine +*/ " ";
            const string end = ";";

            signatureValue.Append("v=1;");

            // algorithm used
            signatureValue.Append(start);
            signatureValue.Append("a=");
            signatureValue.Append(GetAlgorithmName());
            signatureValue.Append(end);

            // Canonicalization
            signatureValue.Append(start);
            signatureValue.Append("c=");
            signatureValue.Append(dkimHeaderCanonicalization.ToString().ToLower());
            signatureValue.Append('/');
            signatureValue.Append(dkimBodyCanonicalization.ToString().ToLower());
            signatureValue.Append(end);

            // signing domain
            signatureValue.Append(start);
            signatureValue.Append("d=");
            signatureValue.Append(domain);
            signatureValue.Append(end);

            // headers to be signed
            signatureValue.Append(start);
            signatureValue.Append("h=");
            foreach (var header in headersToSign)
            {
                signatureValue.Append(header);
                signatureValue.Append(':');
            }
            signatureValue.Length--;
            signatureValue.Append(end);

            // i=identity
            // not supported

            // l=body length
            //not supported

            // public key location
            signatureValue.Append(start);
            signatureValue.Append("q=dns/txt");
            signatureValue.Append(end);

            // selector
            signatureValue.Append(start);
            signatureValue.Append("s=");
            signatureValue.Append(selector);
            signatureValue.Append(end);

            // time sent
            signatureValue.Append(start);
            signatureValue.Append("t=");
            signatureValue.Append((int)t.TotalSeconds);
            signatureValue.Append(end);

            // x=expiration
            // not supported

            // hash of body
            signatureValue.Append(start);
            signatureValue.Append("bh=");
            signatureValue.Append(DkimSignBody(email.Body, encoding, privateKeySigner));
            signatureValue.Append(end);

            // x=copied header fields
            // not supported

            signatureValue.Append(start);
            signatureValue.Append("b=");

            return signatureValue.ToString();
        }

        private static string GetAlgorithmName()
        {
            switch (signingAlgorithm)
            {
                case SigningAlgorithm.RSASha1:
                    {
                        return "rsa-sha1";
                    }
                case SigningAlgorithm.RSASha256:
                    {
                        return "rsa-sha256";
                    }
                default:
                    {
                        throw new InvalidOperationException("Invalid SigningAlgorithm");
                    }
            }
        }

        private static string DkimSignBody(string body, Encoding encoding, IPrivateKeySigner privateKeySigner)
        {
            var cb = DkimCanonicalizer.CanonicalizeBody(body, dkimBodyCanonicalization);

            return Convert.ToBase64String(privateKeySigner.Hash(encoding.GetBytes(cb), signingAlgorithm));
        }

        private static string GenerateDkimSignature(Email email, Encoding encoding, IPrivateKeySigner privateKeySigner, string[] headersToSign)
        {
            if (email == null)
            {
                throw new ArgumentNullException("email");
            }

            if (email.Headers == null)
            {
                throw new ArgumentException("email headers property is null");
            }

            var headers = DkimCanonicalizer.CanonicalizeHeaders(email.Headers, dkimHeaderCanonicalization, true, headersToSign);

            // assumes signature ends with "b="
            return Convert.ToBase64String(privateKeySigner.Sign(encoding.GetBytes(headers), signingAlgorithm));
        }
    }
}
