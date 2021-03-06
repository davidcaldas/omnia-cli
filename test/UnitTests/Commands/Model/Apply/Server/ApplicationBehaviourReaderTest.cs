﻿using Omnia.CLI.Commands.Model.Apply.Readers.Server;
using Shouldly;
using System;
using System.Linq;
using Xunit;

namespace UnitTests.Commands.Model.Apply.Server
{
    public class ApplicationBehaviourReaderTest
    {

        private const string FileText =
@"
/***************************************************************
****************************************************************
	THIS CODE HAS BEEN AUTOMATICALLY GENERATED
	10/08/2020 22:18:45
****************************************************************
****************************************************************/

using Omnia.Behaviours.T99.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Omnia.Libraries.Infrastructure.Connector;
using Omnia.Libraries.Infrastructure.Connector.Client;
using Omnia.Libraries.Infrastructure.Behaviours;
using Omnia.Libraries.Infrastructure.Behaviours.Query;
using Action = Omnia.Libraries.Infrastructure.Behaviours.Action;
using MyCompany.CustomDll;


namespace Omnia.Behaviours.T99.Internal.System
{
    public static partial class SystemApplicationBehaviours
    {
		/// <summary>
		/// HelloWorld
        /// Send a message:
		/// Hello World!
		/// </summary>
        public static async Task<IDictionary<string,object>> HelloWorldAsync(IDictionary<string,object> args = null, Context context = null)
        {
            if(context == null) 
            {
                throw new Exception(""Missing context"");
            }
            return new Dictionary<string, object>() { { ""GreetingMessage"", ""Hello World!"" } };
        }
    }
}";

        [Fact]
        public void ExtractData_UsesCommentName()
        {
            var reader = new ApplicationBehaviourReader();

            var applicationBehaviour = reader.ExtractData(FileText);
            
            applicationBehaviour.Name.ShouldBe("HelloWorld");
        }

        [Fact]
        public void ExtractData_UsesCommentDescription()
        {
            var reader = new ApplicationBehaviourReader();

            var applicationBehaviour = reader.ExtractData(FileText);

            applicationBehaviour.Description.ShouldBe($"Send a message:{Environment.NewLine}Hello World!");
        }

        [Fact]
        public void ExtractData_ValidExpression()
        {
            var reader = new ApplicationBehaviourReader();

            var expression = reader.ExtractData(FileText).Expression;

            expression.ShouldBe(@"if(context == null) 
            {
                throw new Exception(""Missing context"");
            }
            return new Dictionary<string, object>() { { ""GreetingMessage"", ""Hello World!"" } };");
        }

        [Fact]
        public void ExtractData_SuccessfullyExtractUsings()
        {
            var reader = new ApplicationBehaviourReader();

            var entity = reader.ExtractData(FileText);

            entity.Usings.ShouldNotBeNull();
            entity.Usings.Count.ShouldBe(1);
            entity.Usings.Single().ShouldBe("MyCompany.CustomDll");
        }

        [Fact]
        public void ExtractData_NamespaceMatch()
        {
            var reader = new ApplicationBehaviourReader();

            var entity = reader.ExtractData(FileText);

            entity.Namespace.ShouldBe("Omnia.Behaviours.T99.Internal.System");
        }
    }
}
