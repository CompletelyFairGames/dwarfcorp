using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DwarfCorp
{
    /// <summary>
    /// A creature takes an item to an open stockpile and leaves it there.
    /// </summary>
    [Newtonsoft.Json.JsonObject(IsReference = true)]
    public class GatherItemAct : CompoundCreatureAct
    {
        public GameComponent ItemToGather { get; set; }
        public string ItemID { get; set; }

        public GatherItemAct()
        {
            
        }

        public IEnumerable<Status> AddItemToGatherManager()
        {
            Agent.GatherManager.ItemsToGather.Add(ItemToGather);
            yield return Status.Success;
        }

        public IEnumerable<Status> Finally(CreatureAI creature)
        {
            if (creature.Blackboard.GetData<bool>("NoPath", false))
            {
                var designation = creature.Faction.Designations.GetEntityDesignation(ItemToGather, DesignationType.Gather);
                if (designation != null)
                {
                    creature.World.MakeAnnouncement(String.Format("{0} cancelled gather task because it is unreachable", creature.Stats.FullName));
                    if (creature.Faction == creature.World.PlayerFaction)
                    {
                        creature.World.TaskManager.CancelTask(designation.Task);
                    }
                }
                Agent.SetMessage("Failed to gather. No path.");
                yield return Act.Status.Fail;
            }

            yield return Status.Fail;
        }

        public IEnumerable<Status> RemoveItemFromGatherManager()
        {
            //if (!ItemToGather.IsDead)
            //{
            //    yield return Status.Fail;
            //}

            if(Agent.GatherManager.ItemsToGather.Contains(ItemToGather))
            {
                Agent.GatherManager.ItemsToGather.Remove(ItemToGather);
            }
            yield return Status.Success;
        }

        public IEnumerable<Status> AddStockOrder()
        {
            if (ItemToGather is CoinPile)
            {
                Agent.GatherManager.StockMoneyOrders.Add(new GatherManager.StockMoneyOrder()
                {
                    Destination = null,
                    Money = (ItemToGather as CoinPile).Money
                });   
            }
            else
            {
                Agent.GatherManager.StockOrders.Add(new GatherManager.StockOrder()
                {
                    Destination = null,
                    Resource = new ResourceAmount(ItemToGather)
                });   
            }

            yield return Status.Success;
        }

        public GatherItemAct(CreatureAI agent, string item) :
            base(agent)
        {
            ItemID = item;
            Tree = null;
            ItemToGather = null;
            Name = "Gather Item";
        }

        public GatherItemAct(CreatureAI agent, GameComponent item) :
            base(agent)
        {
            ItemToGather = item;
            Name = "Gather Item";
            Tree = null;
        }

        public override void Initialize()
        {
            base.Initialize();
        }



        public override IEnumerable<Status> Run()
        {
            if(Tree == null)
            {
                if(ItemToGather == null)
                {
                    ItemToGather = Agent.Blackboard.GetData<GameComponent>(ItemID);
                }


                if(ItemToGather != null)
                {
                    Tree = new Sequence(
                        new SetBlackboardData<GameComponent>(Agent, "GatherItem", ItemToGather),
                        new Wrap(AddItemToGatherManager),
                        new GoToEntityAct(ItemToGather, Agent),
                        new StashAct(Agent, StashAct.PickUpType.None, null, "GatherItem", "GatheredResource"),
                        new Wrap(RemoveItemFromGatherManager),
                        new Wrap(AddStockOrder)
                        ) 
                        | (new Wrap(RemoveItemFromGatherManager) & new Wrap(() => Finally(Agent)) & false);

                    Tree.Initialize();
                }
            }

            if(Tree == null)
            {
                yield return Status.Fail;
            }
            else
            {
                foreach(Status s in base.Run())
                {
                    yield return s;
                }
            }
        }
    }

}