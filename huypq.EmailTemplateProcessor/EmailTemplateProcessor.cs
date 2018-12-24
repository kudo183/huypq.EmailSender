using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace huypq.EmailTemplateProcessor
{
    public class EmailTemplate
    {
        public string SubjectTemplate { get; set; }
        public string BodyTemplate { get; set; }
    }

    public class Config
    {
        public string SubjectTemplateFileNameSubfix;
        public string BodyTemplateFileNameSubfix;
        public string MailFolderPath;
        public string EmailKey;
        public string PurposeKey;
        public string ProcessedFolderName;
        public string TemplateFolder;
        public int Interval;
    }

    public class EmailTemplateProcessor
    {
        ISender _sender;
        ILogger _logger;
        Dictionary<string, EmailTemplate> _emailTemplateDictionary = new Dictionary<string, EmailTemplate>();
        System.Threading.Timer _timer;
        System.IO.DirectoryInfo _contentDirectoryInfo;
        System.IO.DirectoryInfo _processedDirectoryInfo;
        Config _config;

        public EmailTemplateProcessor(ISender sender, ILogger logger, Config config)
        {
            _sender = sender;
            _logger = logger;
            _config = config;
        }

        public bool Start()
        {
            if (System.IO.Directory.Exists(_config.MailFolderPath) == false)
            {
                _logger.LogError("emails folder not exist.");
                return false;
            }
            _contentDirectoryInfo = new System.IO.DirectoryInfo(_config.MailFolderPath);
            _processedDirectoryInfo = _contentDirectoryInfo.CreateSubdirectory(_config.ProcessedFolderName);

            if (LoadTemplate() == false)
            {
                _logger.LogError("no template folder.");
                return false;
            }

            if (_emailTemplateDictionary.Count == 0)
            {
                _logger.LogError("no email template.");
                return false;
            }

            foreach (var item in _emailTemplateDictionary)
            {
                _logger.LogInformation("*****Template Name: " + item.Key);
                _logger.LogInformation("SubjectTemplate");
                _logger.LogInformation(item.Value.SubjectTemplate);
                _logger.LogInformation("BodyTemplate");
                _logger.LogInformation(item.Value.BodyTemplate);
            }

            _timer = new System.Threading.Timer(Callback, null, 0, _config.Interval);

            return true;
        }

        void Callback(object obj)
        {
            _logger.LogDebug("Callback");
            try
            {
                foreach (var fi in _contentDirectoryInfo.GetFiles())
                {
                    var lowerName = fi.Name.ToLower();
                    if (lowerName.EndsWith(_config.SubjectTemplateFileNameSubfix) == true || lowerName.EndsWith(_config.BodyTemplateFileNameSubfix) == true)
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
                    if (contentKeyValue.TryGetValue(_config.PurposeKey, out templateName) == false)
                    {
                        _logger.LogInformation(fi.Name + " not contain " + _config.PurposeKey);
                        System.IO.File.Move(fi.FullName, System.IO.Path.Combine(_processedDirectoryInfo.FullName, fi.Name));
                        continue;
                    }

                    string mailAddress;
                    if (contentKeyValue.TryGetValue(_config.EmailKey, out mailAddress) == false)
                    {
                        _logger.LogInformation(fi.Name + " not contain " + _config.EmailKey);
                        System.IO.File.Move(fi.FullName, System.IO.Path.Combine(_processedDirectoryInfo.FullName, fi.Name));
                        continue;
                    }

                    EmailTemplate emailTemplate;

                    var type = mailContents[0];
                    string key = type + "_" + templateName.ToLower();
                    if (_emailTemplateDictionary.TryGetValue(key, out emailTemplate) == false)
                    {
                        _logger.LogInformation("no template for " + key);
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

                    _sender.Send(mailAddress, subject, body);
                    _logger.LogInformation("send {0}", mailAddress);
                    //cannot send because server IP is in black list, can check with https://mxtoolbox.com/SuperTool.aspx

                    fi.Delete();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                _logger.LogTrace(ex.StackTrace);
            }

            //clean all file
            foreach (var fi in _contentDirectoryInfo.GetFiles())
            {
                var lowerName = fi.Name.ToLower();
                if (lowerName.EndsWith(_config.SubjectTemplateFileNameSubfix) == true || lowerName.EndsWith(_config.BodyTemplateFileNameSubfix) == true)
                {
                    continue;
                }
                System.IO.File.Move(fi.FullName, System.IO.Path.Combine(_processedDirectoryInfo.FullName, fi.Name));
            }
        }

        bool LoadTemplate()
        {
            System.IO.DirectoryInfo templateDirectoryInfo = null;
            string path = System.IO.Path.Combine(_config.MailFolderPath, _config.TemplateFolder);
            if (System.IO.Directory.Exists(path) == false)
            {
                return false;
            }
            templateDirectoryInfo = new System.IO.DirectoryInfo(path);

            var temp = new Dictionary<string, EmailTemplate>();
            foreach (var fi in templateDirectoryInfo.GetFiles())
            {
                var lowerName = fi.Name.ToLower();
                if (lowerName.EndsWith(_config.SubjectTemplateFileNameSubfix) == true)
                {
                    var templateName = lowerName.Substring(1, lowerName.IndexOf('.') - 1);
                    if (temp.TryGetValue(templateName, out EmailTemplate template) == false)
                    {
                        template = new EmailTemplate();
                        temp.Add(templateName, template);
                    }
                    template.SubjectTemplate = System.IO.File.ReadAllText(fi.FullName);
                }
                else if (lowerName.EndsWith(_config.BodyTemplateFileNameSubfix) == true)
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
                    _logger.LogError("Invalid template name: " + item.Key + " Template name structure [type]_[purpose]");
                    continue;
                }
                if (string.IsNullOrEmpty(item.Value.SubjectTemplate) == true)
                {
                    _logger.LogError(item.Key + " SubjectTemplate is empty");
                    continue;
                }
                if (string.IsNullOrEmpty(item.Value.BodyTemplate) == true)
                {
                    _logger.LogError(item.Key + " BodyTemplate is empty");
                    continue;
                }
                _emailTemplateDictionary.Add(item.Key, item.Value);
            }

            return true;
        }
    }
}
