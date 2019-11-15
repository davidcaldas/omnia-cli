﻿using System;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Options;
using Omnia.CLI.Extensions;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;
using Microsoft.AspNetCore.JsonPatch;

namespace Omnia.CLI.Commands.Tenants
{
    [Command(Name = "associate-admin", Description = "Associate admin user to Tenant.")]
    [HelpOption("-h|--help")]
    public class AssociateAdminCommand
    {
        private readonly AppSettings _settings;
        private readonly HttpClient _httpClient;
        public AssociateAdminCommand(IOptions<AppSettings> options, IHttpClientFactory httpClientFactory)
        {
            _settings = options.Value;
            _httpClient = httpClientFactory.CreateClient();
        }
    
        [Option("--subscription", CommandOptionType.SingleValue, Description = "Name of the configured subscription.")]
        public string Subscription { get; set; }
        [Option("--tenant-code", CommandOptionType.SingleValue, Description = "Tenant where the user will be associated.")]
        public string TenantCode { get; set; }
        [Option("--username", CommandOptionType.SingleValue, Description = "Username of administrator.")]
        public string Username { get; set; }
        

        public async Task<int> OnExecute(CommandLineApplication cmd)
        {
            if (string.IsNullOrWhiteSpace(Subscription))
            {
                Console.WriteLine($"{nameof(Subscription)} is required");
                return (int)StatusCodes.InvalidArgument;
            }

            if (string.IsNullOrWhiteSpace(TenantCode))
            {
                Console.WriteLine($"{nameof(TenantCode)} is required");
                return (int)StatusCodes.InvalidArgument;
            }

            if (string.IsNullOrWhiteSpace(Username))
            {
                Console.WriteLine($"{nameof(Username)} is required");
                return (int)StatusCodes.InvalidArgument;
            }

            if (!_settings.Exists(Subscription))
            {
                Console.WriteLine($"Subscription {Subscription} can't be found.");
                return (int)StatusCodes.InvalidOperation;
            }

            var sourceSettings = _settings.GetSubscription(Subscription);
            
            await _httpClient.WithSubscription(sourceSettings);

            await AddUserToRole(_httpClient, TenantCode, Username);

            return (int) StatusCodes.Success;
        }

        private static async Task<int> AddUserToRole(HttpClient httpClient, string tenantCode, string _username)
        {
            var patch = new JsonPatchDocument().Add("/subjects/-", new { username = _username });

            var response = await httpClient.PatchAsJsonAsync($"/api/v1/management/Security/AuthorizationRole/Administration{tenantCode}", patch);

            if (response.IsSuccessStatusCode)
                return (int)StatusCodes.Success;

            var apiError = await GetErrorFromApiResponse(response);

            Console.WriteLine($"{apiError.Code}: {apiError.Message}");

            return (int)StatusCodes.InvalidOperation;
        }

        private static async Task<ApiError> GetErrorFromApiResponse(HttpResponseMessage response)
            => JsonConvert.DeserializeObject<ApiError>(await response.Content.ReadAsStringAsync());

        private class ApiError
        {
            public string Code { get; set; }
            public string Message { get; set; }
        }
    }
}