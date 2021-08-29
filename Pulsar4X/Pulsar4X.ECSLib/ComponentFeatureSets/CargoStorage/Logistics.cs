using System;
using System.Collections.Generic;
using Pulsar4X.Orbital;
namespace Pulsar4X.ECSLib
{
    /// <summary>
    /// Contains info on how an entitiy can be stored.
    /// NOTE an entity with this datablob must also have a MassVolumeDB
    /// </summary>
    public class LogiBaseDB : BaseDataBlob 
    {

        public Dictionary<ICargoable,(int count, int demandSupplyWeight)> ListedItems = new Dictionary<ICargoable, (int count, int demandSupplyWeight)>(); 
    
        public Dictionary<ICargoable,(Guid shipingEntity, double amountVolume)> ItemsWaitingPickup = new Dictionary<ICargoable, (Guid shipingEntity, double amountVolume)>();
        public Dictionary<ICargoable,(Guid shipingEntity, double amountVolume)> ItemsInTransit = new Dictionary<ICargoable, (Guid shipingEntity, double amountVolume)>();

        public List<(Entity ship, LogisticsCycle.CargoTask cargoTask)> TradeShipBids = new List<(Entity ship, LogisticsCycle.CargoTask cargoTask)>();

        public LogiBaseDB()
        {

        }

        internal override void OnSetToEntity()
        {
            
        }
        public override object Clone()
        {
            throw new NotImplementedException();
        }
    }

    public class LogiShipperDB : BaseDataBlob
    {
        public enum States
        {
            Bidding,
            MoveToSupply,
            Loading,
            MoveToDestination,
            Unloading,
            ResuplySelf,
            Waiting
        }

        public Guid From; 
        public List<(ICargoable item, int count)>  ItemsToShip =  new List<(ICargoable item, int count)>();
        public Dictionary<Guid, double>  TradeSpace =  new Dictionary<Guid, double>();
        public Guid To; 

        public States CurrentState = States.Waiting;

        public List<LogisticsCycle.CargoTask> ActiveCargoTasks = new List<LogisticsCycle.CargoTask>();

        public LogiShipperDB()
        {


        }

        internal override void OnSetToEntity()
        {
            var cdb = base.OwningEntity.GetDataBlob<VolumeStorageDB>();
            TradeSpace = new Dictionary<Guid, double>();
            foreach(var kvp in cdb.TypeStores)
            {
                TradeSpace.Add(kvp.Key, 0);
            }
            if(!OwningEntity.HasDataBlob<NewtonThrustAbilityDB>())
                throw new Exception("Non moving entites can't be shippers");
        }
        public override object Clone()
        {
            throw new NotImplementedException();
        }
    }



    public class LogiShipProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency
        {
            get { return TimeSpan.FromHours(1); }
        }

        public TimeSpan FirstRunOffset => TimeSpan.FromHours(0);

        public Type GetParameterType => typeof(LogiShipperDB);

        public void Init(Game game)
        {
            
        }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            var tradingBases = entity.Manager.GetAllDataBlobsOfType<LogiBaseDB>();
            LogisticsCycle.LogiShipBidding(entity, tradingBases);
        }

        public void ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var tradingBases = manager.GetAllDataBlobsOfType<LogiBaseDB>();
            var shippingEntities = manager.GetAllEntitiesWithDataBlob<LogiShipperDB>();
            foreach(Entity shipper in shippingEntities)
            {
               LogisticsCycle.LogiShipBidding(shipper, tradingBases);
            }
        }
    }


    public class LogiBaseProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency
        {
            get { return TimeSpan.FromHours(1); }
        }

        public TimeSpan FirstRunOffset => TimeSpan.FromHours(0.25);

        public Type GetParameterType => typeof(LogiBaseDB);

        public void Init(Game game)
        {
            
        }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            LogisticsCycle.LogiBaseBidding(entity.GetDataBlob<LogiBaseDB>());
        }

        public void ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var tradingBases = manager.GetAllDataBlobsOfType<LogiBaseDB>();
            var shippingEntities = manager.GetAllEntitiesWithDataBlob<LogiShipperDB>();
            foreach(LogiBaseDB tradeBase in tradingBases)
            {
                LogisticsCycle.LogiBaseBidding(tradeBase);
            }
        }
    }
    public static class LogisticsCycle
    {
        public static List<Entity> ShippingEntites;


        public class CargoTask
        {
            public double Profit = 0;
            public Entity Source;
            public Entity Destination;

            public ICargoable item;

            public long NumberOfItems;
            public double timeInSeconds;
            public double fuelUseDV;
        }


        public static void LogiShipBidding(Entity shippingEntity, List<LogiBaseDB> tradingBases)
        {
            
            DateTime currentDateTime = shippingEntity.StarSysDateTime;
            LogiShipperDB shiperdb = shippingEntity.GetDataBlob<LogiShipperDB>();
            List<CargoTask> cargoTasks = new List<CargoTask>();
            if(shiperdb.CurrentState != LogiShipperDB.States.Waiting)
                return;
            shiperdb.CurrentState = LogiShipperDB.States.Bidding;

            double fuelCost = 1; //we need to work out how much fuel costs from the economy at some point. 
            double timeCost = 1; //per second. the ship will need to work this out for itself, should include maintanance, amount of crew etc. 

            var sourceTradeItems = new List<CargoTask>(); 
            var demandTradeItems = new List<CargoTask>();
            double travelTimeToSource = 0;
            foreach(var tbase in tradingBases)
            {                
                OrbitDB odb;// = tbase.OwningEntity.GetDataBlob<OrbitDB>();
                if(tbase.OwningEntity.HasDataBlob<OrbitDB>())
                     odb = tbase.OwningEntity.GetDataBlob<OrbitDB>();
                else
                    odb = tbase.OwningEntity.GetSOIParentEntity().GetDataBlob<OrbitDB>();

                (Vector3 position, DateTime atDateTime) sourceIntercept;
                if(shippingEntity.HasDataBlob<WarpAbilityDB>())  
                {
                    sourceIntercept = OrbitProcessor.GetInterceptPosition_m
                    (
                        shippingEntity, 
                        odb, 
                        currentDateTime
                    );
                    travelTimeToSource = (shippingEntity.StarSysDateTime - sourceIntercept.atDateTime).TotalSeconds;
                }
                else
                {
                    List<Entity> shipparents = new List<Entity>();
                    Entity soiParent = shippingEntity.GetSOIParentEntity();
                    Entity soiroot = shippingEntity.GetDataBlob<PositionDB>().Root;
                    shipparents.Add(soiParent);
                    while (soiParent != soiroot)
                    {
                        soiParent = soiParent.GetSOIParentEntity();
                        shipparents.Add(soiParent);
                    }


                    Entity soiTargetParent = tbase.OwningEntity.GetSOIParentEntity();
                    Entity soiTargetRoot = tbase.OwningEntity.GetDataBlob<PositionDB>().Root;
                    if(soiroot != soiTargetRoot)
                        throw new Exception("Impossibru!");//this should only happen if we're in different systems, need to eventualy handle that. else the tree has gotten fucked up
                    List<Entity> soiTargetParents = new List<Entity>();
                    soiTargetParents.Add(soiTargetParent);
                    while (soiTargetParent != soiroot)
                    {
                        soiTargetParent = soiTargetParent.GetSOIParentEntity();
                        soiTargetParents.Add(soiTargetParent);
                        
                    }
                    

                    //we cycle through both lists from the top till the soi body doesn't match, the last one will be the shared parent.
                    Entity sharedSOIBody = soiroot;
                    int i = shipparents.Count -1;
                    int j = soiTargetParents.Count -1;
                    while (shipparents[i] == soiTargetParents[j])
                    {
                        sharedSOIBody = shipparents[i];
                        i--;
                        j--;
                    }
                    double shipMass = shippingEntity.GetDataBlob<MassVolumeDB>().MassTotal;
                    double totalDeltaV = 0;
                    double TotalSeconds = 0;
                    var pos = shippingEntity.GetRalitivePosition();
                    var time = shippingEntity.StarSysDateTime;
                    List<(double deltav, double secTillNextManuver)> dvandTimes = new List<(double deltav, double secTillNextManuver)>();
                    double totalTimeInSeconds = 0;
                    for (int k = 0; k < i; k++)
                    {
                        var nextParent = shipparents[k];
                        var bodyMass = nextParent.GetDataBlob<MassVolumeDB>().MassTotal;
                        var sgp = OrbitMath.CalculateStandardGravityParameterInM3S2(shipMass, bodyMass);
                        //var ke = OrbitMath.FromPosition(pos, sgp, time);
                        //var r1 = ke.SemiMajorAxis; //is this right? feel like it's not, but then is current position right
                        var r1 = pos.Length(); //or is this one right?
                        var r2 = nextParent.GetSOI_m();
                        var wca1 = Math.Sqrt(sgp / r1);
                        var wca2 = Math.Sqrt((2 * r2) / (r1 + r2)) - 1;
                        var dva = wca1 * wca2;
                        var timeToPeriaps = Math.PI * Math.Sqrt((Math.Pow(r1 + r2, 3)) / (8 * sgp));
                        dvandTimes.Add((dva, timeToPeriaps));
                        totalTimeInSeconds += timeToPeriaps;
                    }
                    for (int k = soiTargetParents.Count; k < 0; k--)
                    {
                        var nextParent = soiTargetParents[k];
                        var bodyMass = nextParent.GetDataBlob<MassVolumeDB>().MassTotal;
                        var sgp = OrbitMath.CalculateStandardGravityParameterInM3S2(shipMass, bodyMass);
                        //var ke = OrbitMath.FromPosition(pos, sgp, time);
                        //var r1 = ke.SemiMajorAxis; //is this right? feel like it's not, but then is current position right
                        var r1 = pos.Length(); //or is this one right?
                        var r2 = nextParent.GetSOI_m();
                        var wca1 = Math.Sqrt(sgp / r1);
                        var wca2 = Math.Sqrt((2 * r2) / (r1 + r2)) - 1;
                        var dva = wca1 * wca2;
                        var timeToPeriaps = Math.PI * Math.Sqrt((Math.Pow(r1 + r2, 3)) / (8 * sgp));
                        dvandTimes.Add((dva, timeToPeriaps));
                        totalTimeInSeconds += timeToPeriaps;
                    }

                    travelTimeToSource = totalTimeInSeconds; 

                }

                double fuelToSource = 1; //todo calcualte actual amount of fuel to get there. this is going to be orbital manuver fuel since warp just uses power...
                
                

                // var hohmann = InterceptCalcs.Hohmann(sgp, r1, r2); //need to calculate the ideal ranges 
                // var dvDif = hohmann[0].deltaV.Length() + hohmann[1].deltaV.Length();
                
                double loadtime = 3600;
                
                double unloadTime = 3600;

                double profit = 0; //this is used by the ship to decide where it should source and destination from. 

                foreach(var cargoSpace in shiperdb.TradeSpace) //for each type of cargospace
                {
                    double possibleTradeVolume = cargoSpace.Value;

                    //cycle through the base items and add them to the above dictionary if we can carry them
                    foreach(var tradingItems in tbase.ListedItems)
                    {
                        if(tradingItems.Key.CargoTypeID == cargoSpace.Key)
                        {
                            //we abs this since if it's demand, it'll be negative. we want to find how much to carry.
                            
                            var volCanCarry = Math.Min(possibleTradeVolume, Math.Abs(tradingItems.Value.count * tradingItems.Key.VolumePerUnit));
                            long numCanCarry =  (long)(volCanCarry / tradingItems.Key.VolumePerUnit);
                            var ct = new CargoTask();
                            
                            ct.item = tradingItems.Key;
                            ct.NumberOfItems = numCanCarry;
                            ct.Profit = tradingItems.Value.demandSupplyWeight;
                            if(tradingItems.Value.count > 0) //if it's a supply (we ship FROM)
                            {

                                //var loadRate = CargoTransferProcessor.CalcTransferRate(dvDif, tbase.OwningEntity.GetDataBlob<VolumeStorageDB>(), shiperdb.OwningEntity.GetDataBlob<VolumeStorageDB>())
                                ct.Source = tbase.OwningEntity;
                                sourceTradeItems.Add(ct);
                                ct.timeInSeconds =  travelTimeToSource;
                                ct.fuelUseDV = fuelToSource;
                            }
                            else //it's a demand item. (we ship TO)
                            {
                                //var unloadRate = CargoTransferProcessor.CalcTransferRate(dvDif, shiperdb.OwningEntity.GetDataBlob<VolumeStorageDB>(), tbase.OwningEntity.GetDataBlob<VolumeStorageDB>())
                
                                ct.Destination = tbase.OwningEntity;
                                demandTradeItems.Add(ct);
                            }
                        }
                    }
                }

            }

            //now we need to compare the source and demand lists and find the most profitable combinations.

            List<CargoTask> possibleCombos = new List<CargoTask>();
            foreach(var stask in sourceTradeItems)
            {
                foreach(var dtask in demandTradeItems)
                {
                    var sourceEntity = stask.Source;
                    var destinEntity = dtask.Destination;

                    var pcombo = new CargoTask();
                    
                    pcombo.Source = sourceEntity;
                    pcombo.Destination = destinEntity;
                    pcombo.item = stask.item;
                    pcombo.NumberOfItems = Math.Min(stask.NumberOfItems, dtask.NumberOfItems);

                    DateTime arriveSourceTime = currentDateTime + TimeSpan.FromSeconds(stask.timeInSeconds);
                    TimeSpan loadTime = TimeSpan.FromSeconds(3600); //TODO: calculate this properly
                    DateTime DepartSourceTime = arriveSourceTime + loadTime;

                    OrbitDB odb;// = tbase.OwningEntity.GetDataBlob<OrbitDB>();
                    if(destinEntity.HasDataBlob<OrbitDB>())
                        odb = destinEntity.GetDataBlob<OrbitDB>();
                    else
                        odb = destinEntity.GetSOIParentEntity().GetDataBlob<OrbitDB>();
                        //throw new NotImplementedException("Currently we can only predict the movement of stable orbits - target must have an orbitDB");
                    (Vector3 position, DateTime atDateTime) targetIntercept = OrbitProcessor.GetInterceptPosition_m
                    (
                        shippingEntity, 
                        odb, 
                        DepartSourceTime
                    );

                    double timeFromSourceToDestination = (targetIntercept.atDateTime - currentDateTime).TotalSeconds;

                    double fuelToDestDV = 1; //TODO calculate amount of fuel required for manuvers for orbital manuvers.
                    double fuelToSourceDV = stask.fuelUseDV;


                    double unloadTime = 36000; //TODO: calculate this.

                    double totalTimeInSeconds = stask.timeInSeconds + timeFromSourceToDestination + unloadTime;
                    
                    double totalTimeCost = timeCost * totalTimeInSeconds;
                    double totalFuelCost = fuelToDestDV + fuelToSourceDV * fuelCost;

                    //stask.profit should be a negitive number, dtask.profit should be positive, and for the trade to be profitable more than the others.
                    double totalcosts = stask.Profit - (totalTimeCost + totalFuelCost);
                    pcombo.Profit = dtask.Profit - totalcosts;
                    
                    pcombo.timeInSeconds = totalTimeInSeconds;

                    if(possibleCombos.Count < 1)
                    {
                        possibleCombos.Add(pcombo);
                    }
                    else //add to the list in order, the list *should* be small enough that this won't be inefficent... I think...
                    {
                        for (int i = possibleCombos.Count - 1; i >= 0; i--)
                        {
                            var listItem = possibleCombos[i];
                            if(listItem.Profit > pcombo.Profit || i == 0)
                            {
                                possibleCombos.Insert(i, pcombo);
                                i = -1;
                            }
                        }
                    }
                }
            }



            //I could maybe just remove the possibleCombos list and go straight to the tradebase TradeShipBids list. however it might be usefull for now to see the full possilbeCombos list. 
            //now we have a list of potential trade routes this ship is capable of doing, we need to bid for them:
            //var minimumProfit = 0;

            foreach (var pcombo in possibleCombos)
            {
                var tbdb = pcombo.Destination.GetDataBlob<LogiBaseDB>(); //we're going to add it to the *DESTINATON* base
                
                if(tbdb.TradeShipBids.Count < 1)
                {
                    tbdb.TradeShipBids.Add((shippingEntity, pcombo));
                }
                else //add to the list in order of the shortest time, the list *should* be small enough that this won't be inefficent... I think...
                { //note! TODO: this needs to incorperate volume somehow, ie more volume on a slightly slower transport shouldn't be penalised too much, but maybe that will come into the profit side fo things
                    for (int i = tbdb.TradeShipBids.Count - 1; i >= 0; i--)
                    {
                        var listItem = tbdb.TradeShipBids[i];
                        if(listItem.cargoTask.timeInSeconds > pcombo.timeInSeconds || i == 0) //TODONEXT: this needs to be largest to smallest not smallest to largest
                        {
                            tbdb.TradeShipBids.Insert(i, (shippingEntity, pcombo));
                            i = -1;
                        }
                    }
                }                
            }

            
        }
    
        public static void LogiBaseBidding(LogiBaseDB tradeBase)
        {
            if (tradeBase.TradeShipBids.Count == 0)
                return;
            int last = tradeBase.TradeShipBids.Count -1;
            var cargoTask = tradeBase.TradeShipBids[last].cargoTask;
            var ship = tradeBase.TradeShipBids[last].ship;
            var shiptradedb = ship.GetDataBlob<LogiShipperDB>();
            Entity source = cargoTask.Source;
            Entity destin = cargoTask.Destination;

            for (int i = last; i > -1 ; i--)
            {
                var bidTask = tradeBase.TradeShipBids[i];
                if(bidTask.ship == ship && bidTask.cargoTask.Source == source && bidTask.cargoTask.Destination == destin) //if the ship and the source are the same (it should be for the first one)
                {
                    
                    var freespace = shiptradedb.TradeSpace[cargoTask.item.CargoTypeID];
                    var volOfItems = cargoTask.NumberOfItems * cargoTask.item.VolumePerUnit;
                    if(freespace >= volOfItems)
                    {
                        tradeBase.TradeShipBids.RemoveAt(i); //remove it from the list
                        shiptradedb.ActiveCargoTasks.Add(bidTask.cargoTask);
                        shiptradedb.TradeSpace[cargoTask.item.CargoTypeID]-= cargoTask.NumberOfItems;
                        List<(ICargoable, long)> tradeItems = new List<(ICargoable, long)>();
                        tradeItems.Add((cargoTask.item, cargoTask.NumberOfItems));

                        var shipOwner = ship.FactionOwner;//.GetDataBlob<ObjectOwnershipDB>().OwningEntity;
                        
                        //moveto source(if requred)
                        Manuvers(ship, source);


                        CargoLoadFromOrder.CreateCommand(shipOwner, source, ship, tradeItems);
                        //CargoUnloadToOrder.CreateCommand(shipOwner, source, ship, tradeItems);//we need to use the above...
                        //moveto destination.
                        Manuvers(ship, destin);
                        CargoUnloadToOrder.CreateCommand(shipOwner, ship, destin, tradeItems);
 
                    }

                }
            }
            



        }


        private static void Manuvers(Entity ship, Entity target)
        {
            var targetBody = target.GetSOIParentEntity();

            var myMass = ship.GetDataBlob<MassVolumeDB>().MassTotal;
            var tgtBdyMass = target.GetSOIParentEntity().GetDataBlob<MassVolumeDB>().MassTotal;
            var sgpTgtBdy = OrbitMath.CalculateStandardGravityParameterInM3S2(myMass, tgtBdyMass);

            if (ship.GetSOIParentEntity() == target.GetSOIParentEntity())
            {
                var dvdif = CargoTransferProcessor.CalcDVDifference_m(target, ship);
                var cargoDBLeft = ship.GetDataBlob<VolumeStorageDB>();
                var cargoDBRight = target.GetDataBlob<VolumeStorageDB>();
                var dvMaxRangeDiff_ms = Math.Max(cargoDBLeft.TransferRangeDv_mps, cargoDBRight.TransferRangeDv_mps);

                if (target.HasDataBlob<ColonyInfoDB>())
                {
                    if (dvdif > dvMaxRangeDiff_ms * 0.01)//fix, whats the best dv dif for a colony on a planet? can we land? do we want to?
                    {
                        
                        double mySMA = 0;
                        if (ship.HasDataBlob<OrbitDB>())
                            mySMA = ship.GetDataBlob<OrbitDB>().SemiMajorAxis;
                        if (ship.HasDataBlob<OrbitUpdateOftenDB>())
                            mySMA = ship.GetDataBlob<OrbitUpdateOftenDB>().SemiMajorAxis;
                        if (ship.HasDataBlob<NewtonMoveDB>())
                            mySMA = ship.GetDataBlob<NewtonMoveDB>().GetElements().SemiMajorAxis;

                        double targetSMA = OrbitMath.LowOrbitRadius(targetBody);

                        var manuvers = InterceptCalcs.Hohmann2(sgpTgtBdy, mySMA, targetSMA);
                        var tnow = ship.Manager.StarSysDateTime;
                        NewtonThrustCommand.CreateCommand(ship.FactionOwner, ship, tnow, manuvers[0].deltaV, "Thrust: Homann Manuver");
                        DateTime futureDate = tnow + TimeSpan.FromSeconds(manuvers[1].timeInSeconds);
                        NewtonThrustCommand.CreateCommand(ship.FactionOwner, ship, futureDate, manuvers[1].deltaV, "Thrust: Circularize");
                    }


                }
                else //we're moving between two objects who are in orbit, we shoudl be able to match orbit.
                {
                    if (dvdif > dvMaxRangeDiff_ms * 0.01)//if we're less than 10% of perfect
                    {

                        double mySMA = 0;
                        if (ship.HasDataBlob<OrbitDB>())
                            mySMA = ship.GetDataBlob<OrbitDB>().SemiMajorAxis;
                        if (ship.HasDataBlob<OrbitUpdateOftenDB>())
                            mySMA = ship.GetDataBlob<OrbitUpdateOftenDB>().SemiMajorAxis;
                        if (ship.HasDataBlob<NewtonMoveDB>())
                            mySMA = ship.GetDataBlob<NewtonMoveDB>().GetElements().SemiMajorAxis;

                        double targetSMA = 0;
                        if (target.HasDataBlob<OrbitDB>())
                            targetSMA = target.GetDataBlob<OrbitDB>().SemiMajorAxis;
                        if (target.HasDataBlob<OrbitUpdateOftenDB>())
                            targetSMA = target.GetDataBlob<OrbitUpdateOftenDB>().SemiMajorAxis;
                        if (target.HasDataBlob<NewtonMoveDB>())
                            targetSMA = target.GetDataBlob<NewtonMoveDB>().GetElements().SemiMajorAxis;

                        var manuvers = InterceptCalcs.Hohmann2(sgpTgtBdy, mySMA, targetSMA);
                        var tnow = ship.Manager.StarSysDateTime;
                        NewtonThrustCommand.CreateCommand(ship.FactionOwner, ship, tnow, manuvers[0].deltaV, "Thrust: Homann Manuver");
                        DateTime futureDate = tnow + TimeSpan.FromSeconds(manuvers[1].timeInSeconds);
                        NewtonThrustCommand.CreateCommand(ship.FactionOwner, ship, futureDate, manuvers[1].deltaV, "Thrust: Circularize");
                    }
                }
            }
            else //if we're not orbiting the same parent as the source, we have to warpmove
            {

                double targetRad = OrbitMath.LowOrbitRadius(targetBody);
                var ourState = ship.GetRelativeState();

                var departTime = ship.StarSysDateTime;
                OrbitDB targetOrbit = targetBody.GetDataBlob<OrbitDB>();
                (Vector3 position, DateTime eti) targetIntercept = OrbitProcessor.GetInterceptPosition_m(ship, targetOrbit, departTime);
                Vector3 insertionVector = OrbitProcessor.GetOrbitalInsertionVector_m(ourState.Velocity, targetOrbit, targetIntercept.eti);
                var insertionSpeed = insertionVector.Length();
                var idealSpeed = Math.Sqrt(targetRad / sgpTgtBdy);//for a circular orbit
                var deltaV = insertionSpeed - idealSpeed;

                var targetInsertionPosition = Vector3.Normalise(ourState.pos) * targetRad;
                var thrustVector = Vector3.Normalise(insertionVector) * -deltaV;
                //should we expend deltaV now or when we get there?
                WarpMoveCommand.CreateCommand(ship.FactionOwner, ship, targetBody, targetInsertionPosition, targetIntercept.eti, new Vector3(0,0,0));
                NewtonThrustCommand.CreateCommand(ship.FactionOwner, ship, targetIntercept.eti, thrustVector, "Thrust: Circularize");
            }
        }
    }



    public class SetLogisticsOrder : EntityCommand
    {
        public enum OrderTypes
        {
            AddLogiBaseDB,
            RemoveLogiBaseDB,
            SetBaseItems,
            AddLogiShipDB,
            RemoveLogiShipDB,
            SetShipTypeAmounts
        }

        //public SetLogisticsOrder Order;
        private OrderTypes _type;
        public override ActionLaneTypes ActionLanes { get; } = ActionLaneTypes.InstantOrder;

        public override bool IsBlocking { get; } = false;

        public override string Name { get; } = "Set TradeObject";

        public override string Details { get; } = "Set Logi"; 
        

        Entity _entityCommanding;
        internal override Entity EntityCommanding { get { return _entityCommanding; } }

        Entity factionEntity;

        private Dictionary<ICargoable,(int count, int demandSupplyWeight)> _baseChanges;
        private Dictionary<Guid, double> _shipChanges;

        internal override bool IsValidCommand(Game game)
        {
            
            if (CommandHelpers.IsCommandValid(game.GlobalManager, RequestingFactionGuid, EntityCommandingGuid, out factionEntity, out _entityCommanding))
            {
                
                return true;
                
            }
            return false;
        }

        public static void CreateCommand(Entity entity, OrderTypes ordertype )
        {

            SetLogisticsOrder cmd = new SetLogisticsOrder();
            cmd.EntityCommandingGuid = entity.Guid;
            cmd.RequestingFactionGuid = entity.FactionOwner;
            cmd._type = ordertype;


            StaticRefLib.Game.OrderHandler.HandleOrder(cmd);
            
        }

        public static void CreateCommand_SetBaseItems(Entity entity, Dictionary<ICargoable,(int count, int demandSupplyWeight)> changes )
        {

            SetLogisticsOrder cmd = new SetLogisticsOrder();
            cmd.EntityCommandingGuid = entity.Guid;
            cmd.RequestingFactionGuid = entity.FactionOwner;
            cmd._type = OrderTypes.SetBaseItems;
            cmd._baseChanges = changes;


            StaticRefLib.Game.OrderHandler.HandleOrder(cmd);
        }
        
        public static void CreateCommand_SetShipTypeAmounts(Entity entity, Dictionary<Guid, double> changes )
        {

            SetLogisticsOrder cmd = new SetLogisticsOrder();
            cmd.EntityCommandingGuid = entity.Guid;
            cmd.RequestingFactionGuid = entity.FactionOwner;
            cmd._type = OrderTypes.SetShipTypeAmounts;
            cmd._shipChanges = changes;

            StaticRefLib.Game.OrderHandler.HandleOrder(cmd);
            
            StaticRefLib.ProcessorManager.GetProcessor<LogiShipperDB>().ProcessEntity(entity, 0);
            StaticRefLib.ProcessorManager.GetProcessor<LogiBaseDB>().ProcessManager(entity.Manager, 0);
        }

        internal override void ActionCommand(DateTime atDateTime)
        {
            
            switch (_type)
            {
                case OrderTypes.AddLogiBaseDB:
                {
                    var db = new LogiBaseDB();
                    EntityCommanding.SetDataBlob(db);
                    break;
                }
                case OrderTypes.RemoveLogiBaseDB:
                {
                    EntityCommanding.RemoveDataBlob<LogiBaseDB>();
                    break;
                }
                case OrderTypes.SetBaseItems:
                {

                    var db = EntityCommanding.GetDataBlob<LogiBaseDB>();
                    foreach (var item in _baseChanges)
                    {
                        db.ListedItems[item.Key] = item.Value;
                    }
                    //NOTE: possibly some conflict here. 
                    //we might need to consider what to do if a ship is already contracted to grab stuff, 
                    //and then we change this and remove the items before the ship has collected them.
                    
                    break;
                }


                case OrderTypes.AddLogiShipDB:
                {
                    var db = new LogiShipperDB();
                    EntityCommanding.SetDataBlob(db);
                    break;
                }
                case OrderTypes.RemoveLogiShipDB:
                {
                    EntityCommanding.RemoveDataBlob<LogiShipperDB>();
                    break;
                }
                case OrderTypes.SetShipTypeAmounts:
                {
                    var db = EntityCommanding.GetDataBlob<LogiShipperDB>();
                    foreach (var item in _shipChanges)
                    {
                        db.TradeSpace[item.Key] = item.Value;
                    }
                    //db.TradeSpace = 
                    
                    break;
                }
                
            }
        }

        public override bool IsFinished()
        {
            return true;
        }
    }

}