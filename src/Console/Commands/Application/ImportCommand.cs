﻿using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Options;
using Omnia.CLI.Extensions;
using Omnia.CLI.Infrastructure;
using ShellProgressBar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Omnia.CLI.Commands.Application.Infrastructure;

namespace Omnia.CLI.Commands.Application
{
    [Command(Name = "import", Description = "Import application data.")]
    [HelpOption("-h|--help")]
    public class ImportCommand
    {
        private readonly AppSettings _settings;
        private readonly HttpClient _httpClient;

        public ImportCommand(IOptions<AppSettings> options, IHttpClientFactory httpClientFactory)
        {
            _settings = options.Value;
            _httpClient = httpClientFactory.CreateClient();
        }

        [Option("--subscription", CommandOptionType.SingleValue, Description = "Name of the configured subscription.")]
        public string Subscription { get; set; }

        [Option("--tenant", CommandOptionType.SingleValue, Description = "Tenant to export.")]
        public string Tenant { get; set; }

        [Option("--environment", CommandOptionType.SingleValue, Description = "Environment to export.")]
        public string Environment { get; set; } = Constants.DefaultEnvironment;

        [Option("--path", CommandOptionType.SingleValue, Description = "Complete path to the file.")]
        public string Path { get; set; }

        public async Task<int> OnExecute(CommandLineApplication cmd)
        {
            if (string.IsNullOrEmpty(Path))
            {
                Console.WriteLine($"{nameof(Path)} is required");
                return (int)StatusCodes.InvalidArgument;
            }

            if (!File.Exists(Path))
            {
                Console.WriteLine($"The value of --path parameters \"{Path}\" is not a valid file.");
                return (int)StatusCodes.InvalidArgument;
            }

            var sourceSettings = _settings.GetSubscription(Subscription);

            await _httpClient.WithSubscription(sourceSettings);

            var reader = new ImportDataReader();
            try
            {
                var data = reader.ReadExcel(this.Path);

                var success = await ProcessDefinitions(data);

                if (!success) return (int)StatusCodes.UnknownError;

                Console.WriteLine($"Successfully imported data to tenant \"{Tenant}\".");
                return (int)StatusCodes.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in importation : {ex.GetBaseException().Message}.");
                return (int)StatusCodes.UnknownError;
            }
        }

        private async Task<bool> ProcessDefinitions(ICollection<ImportData> data)
        {
            var failed = new List<string>();
            var options = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Cyan,
                ProgressCharacter = '─',
                ProgressBarOnBottom = true
            };

            using (var progressBar = new ProgressBar(data.Count, "Processing file...", options))
            {
                foreach (var dataEntry in data)
                {
                    var (success, messages) =
                        await CreateEntities(progressBar, _httpClient, Tenant, Environment, dataEntry.Definition, dataEntry.DataSource, dataEntry.Data);
                    progressBar.Tick();

                    if (success)
                        continue;

                    failed.AddRange(messages);
                }
            }

            Console.WriteLine($"----- Failed: {failed.Count} -----");
            foreach (var message in failed)
                Console.WriteLine(message);

            return !failed.Any();
        }

        private static async Task<(bool Success, string[] Messages)> CreateEntities(ProgressBarBase progressBar, HttpClient httpClient,
            string tenantCode,
            string environmentCode,
            string definition,
            string dataSource,
            ICollection<(int RowNumber, IDictionary<string, object> Values)> data)
        {
            var failedEntities = new List<string>();

            using (var child = progressBar.Spawn(data.Count, $"Processing entity {definition} with {data.Count} records..."))
            {
                foreach (var (rowNumber, values) in data)
                {
                    var (statusCode, errors) = await CreateEntity(httpClient, tenantCode, environmentCode, definition, dataSource, values);

                    child.Tick(statusCode == (int)StatusCodes.Success ? null : $"Error creating entity for {dataSource} {definition}");
                    
                    if (statusCode == (int) StatusCodes.Success) continue;
                    
                    child.ForegroundColor = ConsoleColor.DarkRed;
                    
                    failedEntities.Add($"Error to import {dataSource}.{definition}: In row {rowNumber} with errors: {GetErrors(errors)}");
                }
            }

            return (!failedEntities.Any(), failedEntities.ToArray());

            static string GetErrors(ApiError errors) => errors != null ? ProcessErrors(errors) : "Unknown Error";

            static string ProcessErrors(ApiError errors)
                    => errors.Errors != null ? JoinErrors(errors) : $" \n\r {errors.Code} - {errors.Message}";

            static string JoinErrors(ApiError errors)
                => string.Join("", errors.Errors.Select(c => $"\n\r {c.Name} - {c.Message}"));
        }

        private static async Task<(int statusCode, ApiError errors)> CreateEntity(HttpClient httpClient,
            string tenantCode,
            string environmentCode,
            string definition,
            string dataSource,
            IDictionary<string, object> data)
        {
            var response = await httpClient.PostAsJsonAsync($"/api/v1/{tenantCode}/{environmentCode}/application/{definition}/{dataSource}", data);
            if (response.IsSuccessStatusCode)
            {
                return ((int)StatusCodes.Success, null);
            }

            var apiError = await GetErrorFromApiResponse(response) ?? new ApiError()
            {
                Code = ((int)response.StatusCode).ToString(),
                Message = (int)response.StatusCode != 403 ? response.StatusCode.ToString() : "Access denied!"
            };

            return ((int)StatusCodes.InvalidOperation, apiError);
        }

        private static Task<ApiError> GetErrorFromApiResponse(HttpResponseMessage response)
            => response.Content.ReadAsJsonAsync<ApiError>();
    }
}