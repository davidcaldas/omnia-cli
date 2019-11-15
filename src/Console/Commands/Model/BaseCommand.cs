﻿using McMaster.Extensions.CommandLineUtils;
using System;

namespace Omnia.CLI.Commands.Model
{
    [Command(Name = "model", Description = "Commands related to Tenant Model.")]
    [HelpOption("-h|--help")]
    [Subcommand(typeof(ExportCommand))]
    [Subcommand(typeof(ImportCommand))]
    public class BaseCommand
    {
        public void OnExecute(CommandLineApplication app)
        {
            Console.WriteLine("Use -h or --help to know how to use it");
        }
    }
}
