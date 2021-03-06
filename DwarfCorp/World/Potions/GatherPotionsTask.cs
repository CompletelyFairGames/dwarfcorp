﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DwarfCorp
{
    public class GatherPotionsTask : Task
    {
        public GatherPotionsTask()
        {
            Name = "Gather Potions";
            ReassignOnDeath = false;
            AutoRetry = false;
            Priority = TaskPriority.Medium;
        }

        public override float ComputeCost(Creature agent, bool alreadyCheckedFeasible = false)
        {
            return 1.0f;
        }

        public override bool ShouldRetry(Creature agent)
        {
            return false;
        }

        public override Feasibility IsFeasible(Creature agent)
        {
            return agent.World.GetResourcesWithTag("Potion").Count > 0 ? Feasibility.Feasible : Feasibility.Infeasible;
        }

        public override MaybeNull<Act> CreateScript(Creature agent)
        {
            return new GetResourcesWithTag(agent.AI, new List<ResourceTagAmount>() { new ResourceTagAmount("Potion", 1)});
        }
    }
}
