﻿using McMaster.Extensions.CommandLineUtils;

namespace Omnia.CLI.Commands.Model
{
    [Command(Name = "model", Description = "")]
    [HelpOption("-h|--help")]
    [Subcommand(typeof(ExportCommand))]
    public class BaseCommand
    {
        public void OnExecute(CommandLineApplication app)
        {

        }
    }
}