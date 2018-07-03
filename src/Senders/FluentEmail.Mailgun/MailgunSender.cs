﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentEmail.Core;
using FluentEmail.Core.Interfaces;
using FluentEmail.Core.Models;
using FluentEmail.Mailgun.HttpHelpers;

namespace FluentEmail.Mailgun
{
    public class MailgunSender : ISender
    {
        private readonly string _apiKey;
        private readonly string _domainName;

        public MailgunSender(string domainName, string apiKey)
        {
            _domainName = domainName;
            _apiKey = apiKey;
        }

        public SendResponse Send(IFluentEmail email, CancellationToken? token = null)
        {
            return SendAsync(email, token).GetAwaiter().GetResult();
        }

        public async Task<SendResponse> SendAsync(IFluentEmail email, CancellationToken? token = null)
        {
            var client = new HttpClient()
            {
                BaseAddress = new Uri($"https://api.mailgun.net/v3/{_domainName}/")
            };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{_apiKey}")));

            var parameters = new List<KeyValuePair<string, string>>();

            parameters.Add(new KeyValuePair<string, string>("from", $"{email.Data.FromAddress.Name} <{email.Data.FromAddress.EmailAddress}>"));
            email.Data.ToAddresses.ForEach(x => {
                parameters.Add(new KeyValuePair<string, string>("to", $"{x.Name} <{x.EmailAddress}>"));
            });
            email.Data.CcAddresses.ForEach(x => {
                parameters.Add(new KeyValuePair<string, string>("cc", $"{x.Name} <{x.EmailAddress}>"));
            });
            email.Data.BccAddresses.ForEach(x => {
                parameters.Add(new KeyValuePair<string, string>("bcc", $"{x.Name} <{x.EmailAddress}>"));
            });
            email.Data.ReplyToAddresses.ForEach(x => {
                parameters.Add(new KeyValuePair<string, string>("h:Reply-To", $"{x.Name} <{x.EmailAddress}>"));
            });
            parameters.Add(new KeyValuePair<string, string>("subject", email.Data.Subject));

            parameters.Add(new KeyValuePair<string, string>(email.Data.IsHtml ? "html" : "text", email.Data.Body));

            if (!string.IsNullOrEmpty(email.Data.PlaintextAlternativeBody))
            {
                parameters.Add(new KeyValuePair<string, string>("text", email.Data.PlaintextAlternativeBody));
            }

            email.Data.Tags.ForEach(x =>
            {
                parameters.Add(new KeyValuePair<string, string>("o:tag", x));
            });

            var files = new List<HttpFile>();
            email.Data.Attachments.ForEach(x =>
            {
                string param;

                if (x.IsInline)
                    param = "inline";
                else
                    param = "attachment";

                files.Add(new HttpFile()
                {
                    ParameterName = param,
                    Data = x.Data,
                    Filename = x.Filename,
                    ContentType = x.ContentType
                });
            });

            var response = await client.PostMultipart<MailgunResponse>("messages", parameters, files);
        
            var result = new SendResponse();
            if (!response.Success)
            {
                result.ErrorMessages.AddRange(response.Errors.Select(x => x.ErrorMessage));
                return result;
            }
            
            return result;
        }
    }
}
