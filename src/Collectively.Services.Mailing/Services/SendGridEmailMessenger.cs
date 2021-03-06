﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Collectively.Common.Domain;
using Collectively.Common.Extensions;
using Collectively.Services.Mailing.Domain;
using Collectively.Services.Mailing.Repositories;
using Serilog;

namespace Collectively.Services.Mailing.Services
{
    public class SendGridEmailMessenger : IEmailMessenger
    {
        private static readonly ILogger Logger = Log.Logger;
        private readonly ISendGridClient _sendGridClient;
        private readonly IEmailTemplateRepository _emailTemplateRepository;
        private readonly SendGridSettings _sendGridSettings;

        public SendGridEmailMessenger(ISendGridClient sendGridClient,
            IEmailTemplateRepository emailTemplateRepository,
            SendGridSettings sendGridSettings)
        {
            _sendGridClient = sendGridClient;
            _emailTemplateRepository = emailTemplateRepository;
            _sendGridSettings = sendGridSettings;
        }

        public async Task SendSupportAsync(string email, string name, string title, string message)
        {
            var emailMessage = CreateMessage(_sendGridSettings.SupportEmailAccount,
                title, message, email);
            Logger.Debug($"Sending support email from {email} via sendgrid");
            await _sendGridClient.SendMessageAsync(emailMessage);
        }

        public async Task SendResetPasswordAsync(string email, string endpoint, string token, string culture)
        {
            var template = await GetTemplateOrFallbackToDefaultOrFailAsync(EmailTemplates.ResetPassword, culture);
            var resetPasswordUrl = $"{endpoint}?email={email}&token={token}";
            var emailMessage = CreateMessage(email, template.Subject);
            ApplyTemplate(template.TemplateId, emailMessage,
                EmailTemplateParameter.Create("resetPasswordUrl", resetPasswordUrl));

            Logger.Debug($"Sending {EmailTemplates.ResetPassword} email to {email} via sendgrid");
            await _sendGridClient.SendMessageAsync(emailMessage);
        }

        public async Task SendActivateAccountAsync(string email, string username,
            string endpoint, string token, string culture)
        {
            var template = await GetTemplateOrFallbackToDefaultOrFailAsync(EmailTemplates.ActivateAccount, culture);
            var activateAccountUrl = $"{endpoint}?email={email}&token={token}";
            var emailMessage = CreateMessage(email, template.Subject);
            ApplyTemplate(template.TemplateId, emailMessage,
                EmailTemplateParameter.Create("url", activateAccountUrl),
                EmailTemplateParameter.Create("username", username));
            Logger.Debug($"Sending {EmailTemplates.ActivateAccount} email to {email} via sendgrid");

            await _sendGridClient.SendMessageAsync(emailMessage);
        }

        public async Task SendRemarkCreatedAsync(string email, Guid remarkId, 
            string category, string address, string username, 
            DateTime date, string culture, string url)
        {
            var template = await GetTemplateOrFallbackToDefaultOrFailAsync(EmailTemplates.RemarkCreated, culture);
            var emailMessage = CreateMessage(email, template.Subject);
            ApplyTemplate(template.TemplateId, emailMessage,
                EmailTemplateParameter.Create("remarkId", remarkId.ToString()),
                EmailTemplateParameter.Create("category", category),
                EmailTemplateParameter.Create("address", address),
                EmailTemplateParameter.Create("username", username),
                EmailTemplateParameter.Create("date", GetDateTimeString(date, culture)),
                EmailTemplateParameter.Create("url", url));

            Logger.Debug($"Sending {EmailTemplates.RemarkCreated} email to {email} via sendgrid");
            await _sendGridClient.SendMessageAsync(emailMessage);
        }

        public async Task SendRemarkStateChangedAsync(string email, Guid remarkId, string category, string address, string username,
            DateTime date, string culture, string url, string state)
        {
            var template = await GetTemplateOrFallbackToDefaultOrFailAsync(EmailTemplates.RemarkStateChanged, culture);
            var emailMessage = CreateMessage(email, template.Subject);
            ApplyTemplate(template.TemplateId, emailMessage,
                EmailTemplateParameter.Create("remarkId", remarkId.ToString()),
                EmailTemplateParameter.Create("category", category),
                EmailTemplateParameter.Create("address", address),
                EmailTemplateParameter.Create("username", username),
                EmailTemplateParameter.Create("date", GetDateTimeString(date, culture)),
                EmailTemplateParameter.Create("state", state),
                EmailTemplateParameter.Create("url", url));

            Logger.Debug($"Sending {EmailTemplates.RemarkStateChanged} email to {email} via sendgrid");
            await _sendGridClient.SendMessageAsync(emailMessage);
        }

        public async Task SendCommentAddedToRemarkAsync(string email, Guid remarkId, string category, string address, string username,
            DateTime date, string culture, string url, string comment)
        {
            var template = await GetTemplateOrFallbackToDefaultOrFailAsync(EmailTemplates.CommentAddedToRemark, culture);
            var emailMessage = CreateMessage(email, template.Subject);
            ApplyTemplate(template.TemplateId, emailMessage,
                EmailTemplateParameter.Create("remarkId", remarkId.ToString()),
                EmailTemplateParameter.Create("category", category),
                EmailTemplateParameter.Create("address", address),
                EmailTemplateParameter.Create("username", username),
                EmailTemplateParameter.Create("date", GetDateTimeString(date, culture)),
                EmailTemplateParameter.Create("comment", comment),
                EmailTemplateParameter.Create("url", url));

            Logger.Debug($"Sending {EmailTemplates.CommentAddedToRemark} email to {email} via sendgrid");
            await _sendGridClient.SendMessageAsync(emailMessage);
        }

        public async Task SendPhotosAddedToRemarkEmailAsync(string email, Guid remarkId, string category,
            string address, string culture, string url)
        {
            var template = await GetTemplateOrFallbackToDefaultOrFailAsync(EmailTemplates.PhotosAddedToRemark, culture);
            var emailMessage = CreateMessage(email, template.Subject);
            ApplyTemplate(template.TemplateId, emailMessage,
                EmailTemplateParameter.Create("remarkId", remarkId.ToString()),
                EmailTemplateParameter.Create("category", category),
                EmailTemplateParameter.Create("address", address),
                EmailTemplateParameter.Create("url", url));

            await _sendGridClient.SendMessageAsync(emailMessage);
        }

        private async Task<EmailTemplate> GetTemplateOrFallbackToDefaultOrFailAsync(string codename, string culture)
        {
            var template = await _emailTemplateRepository.GetByCodenameAsync(codename, culture);
            if (template.HasValue)
                return template.Value;

            template = await _emailTemplateRepository.GetByCodenameAsync(codename, _sendGridSettings.DefaultCulture);
            if (template.HasValue)
                return template.Value;

            throw new ServiceException(OperationCodes.EmailTemplateNotFound,
                $"Email template: '{codename}' has not been found.");
        }

        private SendGridEmailMessage CreateMessage(string receiver,
            string subject, string message = null, string sender = null)
        {
            var emailMessage = new SendGridEmailMessage
            {
                From = new SendGridEmailMessage.Person
                {
                    Email = sender.Empty() ? _sendGridSettings.NoReplyEmailAccount : sender
                },
                Subject = subject,
                Personalizations = new List<SendGridEmailMessage.Personalization>()
            };
            emailMessage.Personalizations.Add(new SendGridEmailMessage.Personalization
            {
                To = new List<SendGridEmailMessage.Person>
                {
                    new SendGridEmailMessage.Person
                    {
                        Email = receiver
                    }
                },
                Substitutions = new Dictionary<string, string>()
            });
            if (message.NotEmpty())
            {
                emailMessage.Content = new List<SendGridEmailMessage.MessageContent>()
                {
                    new SendGridEmailMessage.MessageContent
                    {
                        Value = message
                    }
                };
            }

            return emailMessage;
        }

        private void ApplyTemplate(string templateId, SendGridEmailMessage emailMessage,
            params EmailTemplateParameter[] parameters)
        {
            emailMessage.Content = null;
            emailMessage.TemplateId = templateId;
            var personalization = emailMessage.Personalizations.First();
            foreach (var parameter in parameters)
            {
                var parameterValue = string.Format("{0}", parameter.Values.FirstOrDefault());
                personalization.Substitutions[$"-{parameter.ReplacementTag}-"] = parameterValue;
            }
        }

        private string GetDateTimeString(DateTime date, string culture)
        {
            var cultureInfo = new CultureInfo(culture);
            var dateString = date.ToString("f", cultureInfo);

            return dateString;
        }
    }
}