﻿using System.Collections.Generic;

namespace Omnia.CLI.Commands.Model.Behaviours.Data
{
    public class Entity
    {
        public Entity(string @namespace, IList<EntityBehaviour> behaviours, IList<string> usings)
        {
            Namespace = @namespace;
            Behaviours = behaviours;
            Usings = usings;
        }

        public IList<EntityBehaviour> Behaviours { get; }
        public IList<string> Usings { get; }
        public string Namespace { get; }
    }
}
