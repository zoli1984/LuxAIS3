using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LuxRunner
{

    public class ProcessLogicMixed
    {
        public float[,,] NNoutput { get; set; }
        public int[] Targeted { get; set; }
        public int[] SapMap { get; set; } = new int[LuxLogicState.Size2];
        public List<int> TargetList { get; set; } = new List<int>();
        public int[] ExchangeTargeted { get; set; }
        public int[] SapTargeted { get; set; }
        public int[] SapValues { get; set; }
        public int[] TargetedAura { get; set; }

        public bool IsWentToCandidate { get; set; }

        public int?[] RouteDistancesToEnemyBase { get; set; }
        public int?[] RouteDistancesToMyBase { get; set; }
        public int MyBasePos { get; set; }
        public int EnemyBasePos { get; set; }
        public sbyte[] DangerMap { get; set; }

        public List<int> VoronoiTargets { get; set; }
        public int[] ValueMap { get; set; }
        public int MaxTargetDistance { get; set; }
        public int TargetRadius { get; set; }

        double SamePosWeight { get; set; } = 0;

        public List<Step> Steps = new List<Step>();
        MoveType GetDirection(int from, int to)
        {
            if (from == to) return MoveType.Nothing;
            if (from - 1 == to) return MoveType.Up;
            if (from + 1 == to) return MoveType.Down;
            if (from - 24 == to) return MoveType.Left;
            if (from + 24 == to) return MoveType.Right;

            throw new Exception("Invalid direction");
        }

        void UpdateTiles(LuxLogicState luxLogicState, List<int> route)
        {
            for (var i = 0; i < route.Count; i++)
            {
                var time = luxLogicState.StartStep + i;
                if (time >= 505) break;
                var timePos = LuxLogicState.GetTimePos(time, route[i]);
                luxLogicState.Tiles[timePos].MyUnitCount++;
                foreach (var sapNeigbour in luxLogicState.Tiles[timePos].SapNeighbours)
                {
                    luxLogicState.Tiles[sapNeigbour].MyUnitNeighbourCount++;
                }
                foreach (var sapNeighbourNeighbour in luxLogicState.Tiles[timePos].SapNeighboursNeighbours)
                {
                    luxLogicState.Tiles[sapNeighbourNeighbour].MyUnitNeighbourNeighbourCount++;
                }
            }
            for (int i = route.Count; i < 504 - luxLogicState.StartStep; i++)
            {
                var time = luxLogicState.StartStep + i;
                if (time >= 505) break;
                var timePos = LuxLogicState.GetTimePos(time, route.Last());
                luxLogicState.Tiles[timePos].MyUnitCount++;
                foreach (var sapNeigbour in luxLogicState.Tiles[timePos].SapNeighbours)
                {
                    luxLogicState.Tiles[sapNeigbour].MyUnitNeighbourCount++;
                }
                foreach (var sapNeighbourNeighbour in luxLogicState.Tiles[timePos].SapNeighboursNeighbours)
                {
                    luxLogicState.Tiles[sapNeighbourNeighbour].MyUnitNeighbourNeighbourCount++;
                }
            }
            ClearDijsktraResults(luxLogicState);
        }


        bool DiscoverDraft(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);
            var otherShips = myShips.Where(s => s.ID != ship.ID).ToList();

            int closestUnDiscoveredDist = int.MaxValue;
            for (var x = 0; x < luxLogicState.Env_cfg.map_width; x++)
            {
                for (var y = 0; y < luxLogicState.Env_cfg.map_height; y++)
                {
                    var pos = LuxLogicState.GetPos(x, y);
                    if (parent[pos] == null) continue;
                    if (myShips.Where(s => s.ID != ship.ID).Any(ms => GetSapDistance(ms.Pos, pos) < luxLogicState.Env_cfg.unit_sensor_range * 2)) continue;
                    if (luxLogicState.DiscoveredMap[pos]>0) continue;
                    if (LuxLogicState.MapDistances[pos][ship.Pos] < closestUnDiscoveredDist)
                    {
                        closestUnDiscoveredDist = LuxLogicState.MapDistances[pos][ship.Pos];
                    }
                }
            }

            int? farestUndiscoveredPos = null;
            int? farestUndiscoveredDist = null;
            for (var x = 0; x < luxLogicState.Env_cfg.map_width; x++)
            {
                for (var y = 0; y < luxLogicState.Env_cfg.map_height; y++)
                {
                    var pos = LuxLogicState.GetPos(x, y);
                    if (LuxLogicState.MapDistances[pos][ship.Pos] != closestUnDiscoveredDist) continue;
                    if (parent[pos] == null) continue;
                    //if (luxLogicState.PlayerId == 0 && x + y > 23) continue;
                    //if (luxLogicState.PlayerId == 1 && x + y < 23) continue;
                    if (myShips.Where(s => s.ID != ship.ID).Any(ms => GetSapDistance(ms.Pos, pos) < luxLogicState.Env_cfg.unit_sensor_range * 2)) continue;
                    if (luxLogicState.DiscoveredMap[pos] > 0) continue;

                    int closestShipDist = int.MaxValue;
                    if (otherShips.Count > 0)
                    {
                        closestShipDist = otherShips.Min(s => Math.Abs(s.X - x) + Math.Abs(s.Y - y));
                    }

                    if (farestUndiscoveredDist == null || closestShipDist > farestUndiscoveredDist)
                    {
                        farestUndiscoveredDist = closestShipDist;
                        farestUndiscoveredPos = pos;
                    }
                }
            }
            if (farestUndiscoveredDist != null)
            {
                var nextPos = Dijkstra.GetNextPos(parent, farestUndiscoveredPos.Value);
                var direction = GetDirection(ship.Pos, nextPos);
                Steps.Add(new Step() { MoveType = direction, UnitId = ship.ID });
                return true;
            }
            return false;
        }

        bool Discover(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);
            var otherShips = myShips.Where(s => s.ID != ship.ID).ToList();

            int closestUnDiscoveredDist = int.MaxValue;
            for (var x = 0; x < luxLogicState.Env_cfg.map_width; x++)
            {
                for (var y = 0; y < luxLogicState.Env_cfg.map_height; y++)
                {
                    var pos = LuxLogicState.GetPos(x, y);
                    if (luxLogicState.DiscoveredMap[pos] > 0) continue;
                    if (parent[pos] == null) continue;
                    if (LuxLogicState.MapDistances[pos][ship.Pos] < closestUnDiscoveredDist)
                    {
                        closestUnDiscoveredDist = LuxLogicState.MapDistances[pos][ship.Pos];
                    }
                }
            }

            int? farestUndiscoveredPos = null;
            int? farestUndiscoveredDist = null;
            for (var x = 0; x < luxLogicState.Env_cfg.map_width; x++)
            {
                for (var y = 0; y < luxLogicState.Env_cfg.map_height; y++)
                {
                    var pos = LuxLogicState.GetPos(x, y);
                    if (LuxLogicState.MapDistances[pos][ship.Pos] != closestUnDiscoveredDist) continue;
                    if (parent[pos] == null) continue;
                    //if (luxLogicState.PlayerId == 0 && x + y > 23) continue;
                    //if (luxLogicState.PlayerId == 1 && x + y < 23) continue;
                    if (luxLogicState.DiscoveredMap[pos] > 0) continue;

                    int closestShipDist = int.MaxValue;
                    if (otherShips.Count > 0)
                    {
                        closestShipDist = otherShips.Min(s => Math.Abs(s.X - x) + Math.Abs(s.Y - y));
                    }

                    if (farestUndiscoveredDist == null || closestShipDist > farestUndiscoveredDist)
                    {
                        farestUndiscoveredDist = closestShipDist;
                        farestUndiscoveredPos = pos;
                    }
                }
            }
            if (farestUndiscoveredDist != null)
            {
                var nextPos = Dijkstra.GetNextPos(parent, farestUndiscoveredPos.Value);
                var direction = GetDirection(ship.Pos, nextPos);
                Steps.Add(new Step() { MoveType = direction, UnitId = ship.ID });
                return true;
            }
            return false;
        }

        bool GoToAsssasin(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips, int minEnergy)
        {
            List<int> enemyScorePosList;
            if (luxLogicState.PlayerId == 0)
            {
                enemyScorePosList = luxLogicState.ScoreNodes.Where(s => IsLowerPos(s)).ToList();
            }
            else
            {
                enemyScorePosList = luxLogicState.ScoreNodes.Where(s => IsUpperPos(s)).ToList(); ;
            }

            if (luxLogicState.ScoreNodes.Any(s => s == ship.Pos) && (luxLogicState.Map[ship.Pos] & MapBit.AsteroidBit) == MapBit.AsteroidBit) return false;


            enemyScorePosList = enemyScorePosList.Where(s => ship.Pos == s || (luxLogicState.Map[s] & MapBit.AsteroidBit) != MapBit.AsteroidBit).ToList();

            if (enemyScorePosList.Count == 0) return false;

            int minTarget = enemyScorePosList.Min(e => Targeted[e]);
            enemyScorePosList = enemyScorePosList.Where(s => Targeted[s] == minTarget).ToList();

            if (enemyScorePosList.Count == 0) return false;
            var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);

            var closestScorePosList = enemyScorePosList.Where(s => parent[s] != null || s == ship.Pos).Where(s => energyMap[s] >= minEnergy).OrderByDescending(s => LuxLogicState.MapDistances[ship.Pos][s] == 1 && ((GetPosSum(s) > GetPosSum(ship.Pos)) == (luxLogicState.PlayerId == 0))).ThenBy(s => dist[s]).ToList();
            if (closestScorePosList.Count > 0)
            {
                var closestScorePos = closestScorePosList.First();
                Targeted[closestScorePos]++;

                if (SapUnvisibleTarget(ship, luxLogicState, closestScorePos, enemyScorePosList)) return true;

                UpdateTiles(luxLogicState, Dijkstra.GetRoute(parent, closestScorePos));

                var nextPos = Dijkstra.GetNextPos(parent, closestScorePos);
                var direction = GetDirection(ship.Pos, nextPos);
                Steps.Add(new Step() { MoveType = direction, UnitId = ship.ID });
                return true;
            }

            return false;
        }

        bool GoToScore(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips, bool isMyScore, int minEnergyNode, int maxDistanceFromBase = 100)
        {
            var scorePosList = new List<int>();
            int minTarget = Targeted.Min();
            var upperScores = luxLogicState.ScoreNodes.Where(s => Targeted[s] == minTarget && IsUpperPos(s) && luxLogicState.EnergyMap[s] >= minEnergyNode).ToList();
            var lowerScores = luxLogicState.ScoreNodes.Where(s => Targeted[s] == minTarget && IsLowerPos(s) && luxLogicState.EnergyMap[s] >= minEnergyNode).ToList();

            List<int> enemyFilteredScorePosList;
            List<int> enemyAllScorePosList;
            List<int> myAllScorePosList;

            var myScores = luxLogicState.ScoreNodes.Where(s => myShips.Any(ms => ms.Pos == s)).ToList();
            var filteredEnemyScores = myScores.Select(s => LuxLogicState.GetSymetricPos(s)).ToList();

            if (luxLogicState.PlayerId == 0)
            {
                enemyFilteredScorePosList = luxLogicState.ScoreNodes.Where(s => IsLowerPos(s) && filteredEnemyScores.Contains(s)).ToList();
                enemyAllScorePosList = luxLogicState.ScoreNodes.Where(s => IsLowerPos(s)).ToList();
                myAllScorePosList = luxLogicState.ScoreNodes.Where(s => IsUpperPos(s)).ToList();
            }
            else
            {
                enemyFilteredScorePosList = luxLogicState.ScoreNodes.Where(s => IsUpperPos(s) && filteredEnemyScores.Contains(s)).ToList();
                myAllScorePosList = luxLogicState.ScoreNodes.Where(s => IsLowerPos(s)).ToList();
                enemyAllScorePosList = luxLogicState.ScoreNodes.Where(s => IsUpperPos(s)).ToList();
            }

            if (isMyScore == (luxLogicState.PlayerId == 0))
            {
                scorePosList = upperScores;
            }
            else
            {
                scorePosList = lowerScores;
            }
            if (isMyScore)
            {
                scorePosList = scorePosList.Where(s => LuxLogicState.MapDistances[s][MyBasePos] <= maxDistanceFromBase).ToList();
            }

            if (enemyAllScorePosList.Count > 0 && isMyScore && enemyAllScorePosList.Min(e => LuxLogicState.MapDistances[e][ship.Pos]) < myAllScorePosList.Min(e => LuxLogicState.MapDistances[e][ship.Pos])) return false;

            scorePosList = scorePosList.Where(s => ship.Pos == s || (luxLogicState.Map[s] & MapBit.AsteroidBit) != MapBit.AsteroidBit).ToList();
            if (luxLogicState.NebulaEnergyModified < minEnergyNode)
            {
                scorePosList = scorePosList.Where(s => (luxLogicState.Map[s] & MapBit.NebulaBit) != MapBit.NebulaBit).ToList();
            }
            if (scorePosList.Count == 0) return false;

            var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);
            var bestScoreList = scorePosList.Where(s=>s==ship.Pos || parent[s]!=null).OrderBy(s => LuxLogicState.MapDistances[s][MyBasePos]/5).ThenByDescending(s=> GetSumEnergyModifier(s,luxLogicState)).ToList();
            if (bestScoreList.Count == 0)
            {
                return false;
            }
            var bestScore = bestScoreList.First();

            var closestMyShip = myShips.Where(s => !s.IsMoved && (s.Pos==bestScore || GetDijsktraResult(luxLogicState, s).ToTuple().Item2[bestScore] != null)).OrderBy(ms => GetDijsktraResult(luxLogicState, ms).ToTuple().Item1[bestScore]).First();
            if (closestMyShip.ID != ship.ID) return false;


            //var closestScorePosList = scorePosList.Where(s => parent[s] != null || s == ship.Pos).OrderBy(s => dist[s]).ToList();

            //var closestScorePosList = scorePosList.Where(s => parent[s] != null || s == ship.Pos).OrderBy(s => dist[s]).ToList();
            //var closestScorePosList = scorePosList.Where(s => parent[s] != null || s == ship.Pos).OrderByDescending(s => LuxLogicState.MapDistances[ship.Pos][s] == 1 && ((GetPosSum(s) > GetPosSum(ship.Pos)) == (luxLogicState.PlayerId == 0)) && luxLogicState.EnergyMap[ship.Pos] <= luxLogicState.EnergyMap[s]).ThenBy(s => dist[s]).ToList();

            //if (closestScorePosList.Count > 0)
            //{
            var closestScorePos = bestScore;
                Targeted[closestScorePos]++;

            var scoreDist = myAllScorePosList.Min(ms => enemyAllScorePosList.Min(es => LuxLogicState.MapDistances[ms][es]));
                if (isMyScore && closestScorePos == ship.Pos && SapUnvisibleTarget(ship, luxLogicState, closestScorePos, enemyFilteredScorePosList)) return true;
      //         if (RouteDistancesToEnemyBase[closestScorePos] < 30 && GatherEnergy(ship, luxLogicState, isMyScore, closestScorePos, scoreDist)) return true;
            //    if (isMyScore && closestScorePos==ship.Pos && SapUnvisibleTarget(ship, luxLogicState, closestScorePos, enemyFilteredScorePosList)) return true;

            if (!isMyScore)
            {
                var bestExchangeShip = myShips.Where(ms => ms.IsMoved && ExchangeTargeted[ms.Pos] == 0 && luxLogicState.ScoreNodes.Any(s => ms.Pos == s) && GetDijsktraResult(luxLogicState, ms).ToTuple().Item1[closestScorePos] < dist[closestScorePos]
                && ms.Energy + GetSumEnergyModifier(ms.Pos, luxLogicState) * routeDistances[ms.Pos] > energyMap[ms.Pos] + 50)
                    .OrderByDescending(ms => GetSumEnergyModifier(ms.Pos, luxLogicState) * routeDistances[ms.Pos]).FirstOrDefault();
                if (bestExchangeShip != null)
                {
                    ExchangeTargeted[bestExchangeShip.Pos]++;
                    closestScorePos = bestExchangeShip.Pos;
                }
            }

            UpdateTiles(luxLogicState, Dijkstra.GetRoute(parent, closestScorePos));

                var nextPos = Dijkstra.GetNextPos(parent, closestScorePos);
                var myPendingShip = myShips.FirstOrDefault(s => s.Pos == nextPos && (s.IsPending || !s.IsMoved) && s.Energy >= ship.Energy);
                if (myPendingShip != null)
                {
                    myPendingShip.IsMoved = true;
                    myPendingShip.IsPending = false;
                    myPendingShip.PendingTarget = closestScorePos;
                }

                var direction = GetDirection(ship.Pos, nextPos);
                if (direction == MoveType.Nothing)
                {
                    ship.IsPending = true;
                }
                else
                {
                    Steps.Add(new Step() { MoveType = direction, UnitId = ship.ID });
                }
                return true;
            //}

            return false;
        }

        bool MovePendings(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (!ship.PendingTarget.HasValue) return false;
            var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);
            if (parent[ship.PendingTarget.Value] == null) return false;
            var route = Dijkstra.GetRoute(parent, ship.PendingTarget.Value);
            //UpdateTiles(luxLogicState, route);
            var nextPos = Dijkstra.GetNextPos(parent, ship.PendingTarget.Value);
            var myPendingShip = myShips.FirstOrDefault(s => s.Pos == nextPos && s.IsPending);
            if (myPendingShip != null)
            {
                myPendingShip.IsPending = false;
                myPendingShip.PendingTarget = ship.PendingTarget;
            }
            ship.PendingTarget = null;
            var direction = GetDirection(ship.Pos, nextPos);
            Steps.Add(new Step() { MoveType = direction, UnitId = ship.ID });
            return true;
        }

        bool StayPendings(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (!ship.IsPending) return false;
            ship.IsPending = false;
            Steps.Add(new Step() { MoveType = MoveType.Nothing, UnitId = ship.ID });
            return true;
        }

        bool GatherEnergy(Ship ship, LuxLogicState luxLogicState, bool isMyScore, int targetPos, int scoreDist)
        {
            if (luxLogicState.StartStep % 101 > 33 - ship.ID % 5) return false;
            //if (ship.Energy>130) return false;
            var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);
            var (tx, ty) = LuxLogicState.GetXY(targetPos);
            var requiredEnergy = isMyScore ? (tx + ty) : (23 - tx + 23 - ty);
            requiredEnergy = 400;
            var additionalEnergyNeeded = requiredEnergy - energyMap[targetPos];

            if (additionalEnergyNeeded <= 0) return false;

            var route = Dijkstra.GetRoute(parent, targetPos);

            var maxEnergyGain = route.Max(r => luxLogicState.EnergyMap[r] == 127 ? 0 : luxLogicState.EnergyMap[r]);
            if (maxEnergyGain < 8) return false;
            //if (route.Count + luxLogicState.StartStep % 100 > 75 - ship.ID % 5) return false;
            if (luxLogicState.EnergyMap[ship.Pos] != maxEnergyGain) return false;

            Steps.Add(new Step() { MoveType = MoveType.Nothing, UnitId = ship.ID });
            //Targeted[ship.Pos]++;
            return true;
        }

        public static bool IsUpperPos(int pos)
        {
            var (x, y) = LuxLogicState.GetXY(pos);
            return x + y <= 23;
        }
        public static bool IsLowerPos(int pos)
        {
            var (x, y) = LuxLogicState.GetXY(pos);
            return x + y >= 23;
        }

        bool IsDiagonalPos(int pos)
        {
            var (x, y) = LuxLogicState.GetXY(pos);
            return x + y == 23;
        }

        int GetPosSum(int pos)
        {
            var (x, y) = LuxLogicState.GetXY(pos);
            return x + y;
        }

        int GetSumEnergyModifier(int pos, LuxLogicState luxLogicState)
        {
            var (x, y) = LuxLogicState.GetXY(pos);
            var energyValue = luxLogicState.EnergyMap[pos];
            if (energyValue == 127) energyValue = 4;
            if ((luxLogicState.Map[pos] & MapBit.NebulaBit) == MapBit.NebulaBit && luxLogicState.NebulaEnergyModified.HasValue)
            {
                return energyValue + luxLogicState.NebulaEnergyModified.Value;
            }
            return energyValue;
        }


        //public void CalcSniperTargets(LuxLogicState luxLogicState)
        //{
        //    var upperScores = luxLogicState.ScoreNodes.Where(s => IsUpperPos(s)).ToList();
        //    var lowerScores = luxLogicState.ScoreNodes.Where(s => IsLowerPos(s)).ToList();
        //    var enemyScorePosList = new List<int>();
        //    var myScorePosList = new List<int>();
        //    if (luxLogicState.PlayerId == 0)
        //    {
        //        enemyScorePosList = lowerScores;
        //        myScorePosList = upperScores;
        //    }
        //    else
        //    {
        //        enemyScorePosList = upperScores;
        //        myScorePosList = lowerScores;
        //    }
        //    var refPos = MyBasePos;

        //    double? bestSniperDist = null;
        //    int? bestSniperPos = null;
        //    for (var i = 0; i < LuxLogicState.Size2; i++)
        //    {
        //        var energy = GetSumEnergyModifier(i, luxLogicState);
        //        if (energy < 4) continue;
        //        if (enemyScorePosList.All(s => GetSapDistance(s, i) > luxLogicState.Env_cfg.unit_sap_range)) continue;
        //        if (TargetList.Any(t => GetSapDistance(t, i) <= 2)) continue;
        //        if (parent[i] == null && i != ship.Pos) continue;

        //        var targetValue = TargetList.Count > 0 ? TargetList.Min(t => GetSapDistance(t, i)) : 0;
        //        if (Targeted[i] == 0 && myScorePosList.Any(s => s == i))
        //            targetValue -= 40;
        //        var cdist = LuxLogicState.MapDistances[refPos][i] - energy * 3 + targetValue;
        //        if (!bestSniperDist.HasValue || cdist < bestSniperDist)
        //        {
        //            bestSniperDist = cdist;
        //            bestSniperPos = i;
        //        }
        //    }
        //    if (!bestSniperPos.HasValue) return false;
        //}

        public bool GoToSniperPos(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips, bool isMine)
        {        
            var upperScores = luxLogicState.ScoreNodes.Where(s => IsUpperPos(s)).ToList();
            var lowerScores = luxLogicState.ScoreNodes.Where(s => IsLowerPos(s)).ToList();
            var enemyScorePosList = new List<int>();
            var myScorePosList = new List<int>();
            if (luxLogicState.PlayerId == 0)
            {
                enemyScorePosList = lowerScores;
                myScorePosList = upperScores;
            }
            else
            {
                enemyScorePosList = upperScores;
                myScorePosList = lowerScores;
            }
            var refPos = MyBasePos;
            if (isMine)
            {
                refPos = EnemyBasePos;
                enemyScorePosList = myScorePosList;
            }
            //enemyScorePosList = new List<int> { EnemyBasePos };

            var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);

            double? bestSniperDist = null;
            int? bestSniperPos = null;
            for (var i = 0; i< LuxLogicState.Size2; i++)
            {
                var energy = GetSumEnergyModifier(i,luxLogicState);
                if (energy < 4) continue;
                if (enemyScorePosList.All(s=>GetSapDistance(s,i)>luxLogicState.Env_cfg.unit_sap_range)) continue;
                if (TargetList.Any(t => GetSapDistance(t, i) <= 2)) continue;
                if (parent[i] == null && i != ship.Pos) continue;

                var targetValue = TargetList.Count > 0 ? TargetList.Min(t => GetSapDistance(t, i)) : 0;
                if (Targeted[i] == 0 && myScorePosList.Any(s=>s==i))
                    targetValue -= 40;
                var cdist = LuxLogicState.MapDistances[refPos][i] - energy*3 + targetValue;
                if (!bestSniperDist.HasValue || cdist < bestSniperDist)
                {
                    bestSniperDist = cdist;
                    bestSniperPos = i;
                }
            }
            if (!bestSniperPos.HasValue) return false;

            if (myShips.Any(ms => !ms.IsMoved && GetDijsktraResult(luxLogicState, ms).ToTuple().Item1[bestSniperPos.Value] < dist[bestSniperPos.Value])) return false;

            var bestExchangeShip = myShips.Where(ms => ms.IsMoved && ExchangeTargeted[ms.Pos] == 0 && luxLogicState.ScoreNodes.Any(s => ms.Pos == s) && GetDijsktraResult(luxLogicState, ms).ToTuple().Item1[bestSniperPos.Value] < dist[bestSniperPos.Value]
            && ms.Energy + GetSumEnergyModifier(ms.Pos, luxLogicState) * routeDistances[ms.Pos] > energyMap[ms.Pos] + 50)
                .OrderByDescending(ms => GetSumEnergyModifier(ms.Pos, luxLogicState) * routeDistances[ms.Pos]).FirstOrDefault();
            if (bestExchangeShip != null)
            {
                ExchangeTargeted[bestExchangeShip.Pos]++;
                bestSniperPos = bestExchangeShip.Pos;
            }

            TargetList.Add(bestSniperPos.Value);
            Targeted[bestSniperPos.Value]++;
            var route = Dijkstra.GetRoute(parent, bestSniperPos.Value);
            UpdateTiles(luxLogicState, route);
            var nextPos = Dijkstra.GetNextPos(parent, bestSniperPos.Value);
            var direction = GetDirection(ship.Pos, nextPos);

            if (!isMine && direction == MoveType.Nothing && SapUnvisibleTarget(ship, luxLogicState, bestSniperPos.Value, enemyScorePosList)) return true;
            if (GatherEnergy(ship, luxLogicState, true, bestSniperPos.Value, 0)) return true;

            Steps.Add(new Step() { MoveType = direction, UnitId = ship.ID });
            return true;
        }

        public bool GoToEnemy(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            var upperScores = luxLogicState.ScoreNodes.Where(s => Targeted[s] == 0 && IsUpperPos(s) && !IsDiagonalPos(s)).ToList();
            if (luxLogicState.NebulaEnergyModified != null)
            {
                upperScores = upperScores.Where(s => (luxLogicState.Map[s] & MapBit.NebulaBit) != MapBit.NebulaBit).ToList();
            }
            var lowerScores = luxLogicState.ScoreNodes.Where(s => Targeted[s] == 0 && IsLowerPos(s) && !IsDiagonalPos(s)).ToList();
            if (luxLogicState.NebulaEnergyModified != null)
            {
                lowerScores = lowerScores.Where(s => (luxLogicState.Map[s] & MapBit.NebulaBit) != MapBit.NebulaBit).ToList();
            }
            var scorePosList = new List<int>();
            if (luxLogicState.PlayerId == 0)
            {
                scorePosList = lowerScores;
            }
            else
            {
                scorePosList = upperScores;
            }
            scorePosList = scorePosList.Where(s => GetSniperPos(luxLogicState, s, ship.Pos).HasValue).ToList();
            scorePosList = scorePosList.Where(s => (luxLogicState.Map[s] & MapBit.Visible) != MapBit.Visible).ToList();
            if (scorePosList.Count == 0) return false;

            var closestScorePos = scorePosList.OrderBy(s => myShips.Min(ms => LuxLogicState.MapDistances[GetSniperPos(luxLogicState, s, ship.Pos).Value][ms.Pos])).First();
            var closestSniperPos = GetSniperPos(luxLogicState, closestScorePos, ship.Pos).Value;
            var closestMyShip = myShips.Where(s => !s.IsMoved).OrderBy(sh => LuxLogicState.MapDistances[sh.Pos][closestSniperPos]).First();
            if (closestMyShip.ID != ship.ID) return false;

            Targeted[closestScorePos]++;
            if (ship.Pos == closestSniperPos)
            {
                if (ship.Energy >= luxLogicState.Env_cfg.unit_sap_cost)
                {
                    var (sx, sy) = LuxLogicState.GetXY(closestScorePos);
                    Steps.Add(new Step() { MoveType = MoveType.Sap, UnitId = ship.ID, X = sx - ship.X, Y = sy - ship.Y });
                }
                else
                {
                    Steps.Add(new Step() { MoveType = MoveType.Nothing, UnitId = ship.ID });
                }
                ship.IsMoved = true;
                return true;
            }
            else
            {
                var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);
                if (parent[closestScorePos] == null) return false;
                var route = Dijkstra.GetRoute(parent, closestScorePos);
                UpdateTiles(luxLogicState, route);

                var nextPos = Dijkstra.GetNextPos(parent, closestScorePos);
                var direction = GetDirection(ship.Pos, nextPos);
                Steps.Add(new Step() { MoveType = direction, UnitId = ship.ID });
                return true;
            }
        }

        int? GetSniperPos(LuxLogicState luxLogicState, int target, int from)
        {
            var (tx, ty) = LuxLogicState.GetXY(target);
            var (fx, fy) = LuxLogicState.GetXY(from);
            int minDistValue = int.MaxValue;
            int? maxPos = null;
            bool isUpperScore = true;
            if (tx + ty > 23) isUpperScore = false;
            for (var sapDx = -luxLogicState.Env_cfg.unit_sap_range; sapDx <= luxLogicState.Env_cfg.unit_sap_range; sapDx++)
            {
                for (var sapDy = -luxLogicState.Env_cfg.unit_sap_range; sapDy <= luxLogicState.Env_cfg.unit_sap_range; sapDy++)
                {
                    if (isUpperScore && sapDx + sapDy < 0) continue;
                    if (!isUpperScore && sapDx + sapDy > 0) continue;
                    var nx = tx + sapDx;
                    var ny = ty + sapDy;
                    if (nx < 0 || nx >= luxLogicState.Env_cfg.map_width) continue;
                    if (ny < 0 || ny >= luxLogicState.Env_cfg.map_height) continue;
                    var newPos = LuxLogicState.GetPos(nx, ny);

                    if ((luxLogicState.Map[newPos] & MapBit.AsteroidBit) == MapBit.AsteroidBit) continue;
                    if (luxLogicState.NebulaEnergyModified < 0 && (luxLogicState.Map[newPos] & MapBit.NebulaBit) == MapBit.NebulaBit) continue;
                    if (luxLogicState.EnergyMap[newPos] == 127) continue;
                    if (luxLogicState.EnergyMap[newPos] < 9) continue;

                    var energyValue = luxLogicState.EnergyMap[newPos];
                    if (energyValue == 127) energyValue = 0;

                    var distValue = -energyValue;
                    if (distValue < minDistValue)
                    {
                        minDistValue = distValue;
                        maxPos = newPos;
                    }
                }
            }
            return maxPos;
        }

        bool DiscoverRelic(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (luxLogicState.ScoreNodes.Any(s => ship.Pos == s && myShips.Count(ms => ms.Pos == s) == 1)) return false;
            var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);
            var otherShips = myShips.Where(s => s.ID != ship.ID).ToList();
            int? minDist = null;
            int? minDistPos = null;
            foreach (var relic in luxLogicState.NewRelicList.OrderBy(r => LuxLogicState.MapDistances[MyBasePos][r]))
            {
                var (x, y) = LuxLogicState.GetXY(relic);
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        var nx = x + dx;
                        var ny = y + dy;
                        if (nx < 0 || nx >= luxLogicState.Env_cfg.map_width) continue;
                        if (ny < 0 || ny >= luxLogicState.Env_cfg.map_height) continue;

                        var newPos = LuxLogicState.GetPos(nx, ny);

                        if (luxLogicState.ScoreNodes.Contains(newPos)) continue;
                        if (luxLogicState.CandidateGroupList.Any(g => g.CandidatePos.Contains(newPos))) continue;
                        if ((luxLogicState.Map[newPos] & MapBit.NotScoreBit) == MapBit.NotScoreBit) continue;
                        if (otherShips.Where(os => !os.IsMoved).Count() > 0 && LuxLogicState.MapDistances[newPos][ship.Pos] > otherShips.Where(os => !os.IsMoved).Min(s => LuxLogicState.MapDistances[newPos][s.Pos])) continue;
                        if (Targeted[newPos] > 0) continue;
                        if (parent[newPos] == null) continue;
                        if (minDist == null || LuxLogicState.MapDistances[newPos][ship.Pos] < minDist)
                        {
                            minDist = LuxLogicState.MapDistances[newPos][ship.Pos];
                            minDistPos = newPos;
                        }
                    }
                }
            }
            if (minDistPos != null)
            {
                var nextPos = Dijkstra.GetNextPos(parent, minDistPos.Value);
                var direction = GetDirection(ship.Pos, nextPos);
                Steps.Add(new Step() { MoveType = direction, UnitId = ship.ID });
                Targeted[minDistPos.Value]++;
                return true;
            }
            return false;
        }

        bool GoToMaxEnergy(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (luxLogicState.RelicList.Count == 0) return false;
            var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);


            int minTargetedAura = int.MaxValue;
            foreach (var relic in luxLogicState.RelicList)
            {
                if (TargetedAura[relic] < minTargetedAura) minTargetedAura = TargetedAura[relic];
            }

            var targetedRelic = luxLogicState.RelicList.Where(r => TargetedAura[r] == minTargetedAura).OrderBy(r => LuxLogicState.MapDistances[r][ship.Pos]).First();

            var aura = 2;
            int? maxEnergy = null;
            int? maxEnergyPos = null;
            while (!maxEnergy.HasValue)
            {
                aura++;
                var (rx, ry) = LuxLogicState.GetXY(targetedRelic);
                for (var dx = -aura; dx <= aura; dx++)
                {
                    for (var dy = -aura; dy <= aura; dy++)
                    {
                        var nx = rx + dx;
                        var ny = ry + dy;
                        if (nx < 0 || nx >= luxLogicState.Env_cfg.map_width) continue;
                        if (ny < 0 || ny >= luxLogicState.Env_cfg.map_height) continue;

                        var newPos = LuxLogicState.GetPos(nx, ny);
                        if (Targeted[newPos] > 0) continue;
                        if (parent[newPos] == null && newPos != ship.Pos) continue;

                        if (!maxEnergy.HasValue || maxEnergy < GetSumEnergyModifier(newPos, luxLogicState))
                        {
                            maxEnergy = GetSumEnergyModifier(newPos, luxLogicState);
                            maxEnergyPos = newPos;
                        }
                    }
                }
                if (aura == 7) break;
            }

            if (maxEnergyPos != null)
            {
                var nextPos = Dijkstra.GetNextPos(parent, maxEnergyPos.Value);
                var direction = GetDirection(ship.Pos, nextPos);
                Targeted[maxEnergyPos.Value]++;
                TargetedAura[targetedRelic]++;
                Steps.Add(new Step() { MoveType = direction, UnitId = ship.ID });
                return true;
            }
            return false;
        }


        //bool GoToMaxEnergy(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        //{
        //    var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);
        //    var aura = 2;
        //    double? maxEnergy = null;
        //    int? maxEnergyPos = null;
        //    var (rx, ry) = LuxLogicState.GetXY(ship.Pos);
        //    while (!maxEnergy.HasValue)
        //    {
        //        aura++;
        //        for (var dx = -aura; dx <= aura; dx++)
        //        {
        //            for (var dy = -aura; dy <= aura; dy++)
        //            {
        //                var nx = rx + dx;
        //                var ny = ry + dy;
        //                if (nx < 0 || nx >= luxLogicState.Env_cfg.map_width) continue;
        //                if (ny < 0 || ny >= luxLogicState.Env_cfg.map_height) continue;

        //                var newPos = LuxLogicState.GetPos(nx, ny);
        //                //   if (Targeted[newPos] > 0) continue;
        //                if (parent[newPos] == null && newPos != ship.Pos) continue;

        //                if (!maxEnergy.HasValue || maxEnergy < GetSumEnergyModifier(newPos, luxLogicState) - LuxLogicState.MapDistances[newPos][ship.Pos] / 100.0)
        //                {
        //                    maxEnergy = GetSumEnergyModifier(newPos, luxLogicState) - LuxLogicState.MapDistances[newPos][ship.Pos] / 100.0;
        //                    maxEnergyPos = newPos;
        //                }
        //            }
        //        }
        //        if (aura == 7) break;
        //    }

        //    if (maxEnergyPos != null)
        //    {
        //        var nextPos = Dijkstra.GetNextPos(parent, maxEnergyPos.Value);
        //        var direction = GetDirection(ship.Pos, nextPos);
        //        Targeted[maxEnergyPos.Value]++;
        //        if (direction != MoveType.Nothing)
        //        {
        //            Steps.Add(new Step() { MoveType = direction, UnitId = ship.ID });
        //            return true;
        //        }
        //        return false;
        //    }
        //    return false;
        //}

        bool WaitForGap(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (LuxLogicState.MapDistances[MyBasePos][ship.Pos] < 15) return false;
            if (GetSumEnergyModifier(ship.Pos, luxLogicState) < 5) return false;
            if (myShips.Any(ms => !luxLogicState.ScoreNodes.Any(s => s == ms.Pos)
            && (LuxLogicState.MapDistances[MyBasePos][ms.Pos] > LuxLogicState.MapDistances[MyBasePos][ship.Pos] || (LuxLogicState.MapDistances[MyBasePos][ms.Pos] == LuxLogicState.MapDistances[MyBasePos][ship.Pos] && ms.ID < ship.ID))
            && GetSapDistance(ms.Pos, ship.Pos) < 3))
            {
                Targeted[ship.Pos]++;
                Steps.Add(new Step() { MoveType = MoveType.Nothing, UnitId = ship.ID });
                return true;
            }
            return false;
        }

        bool WaitForTime(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (LuxLogicState.MapDistances[MyBasePos][ship.Pos] < 10) return false;
            if (GetSumEnergyModifier(ship.Pos, luxLogicState) < 5) return false;
            if (luxLogicState.StartStep % 10 >= 5) return false;
            Targeted[ship.Pos]++;
            Steps.Add(new Step() { MoveType = MoveType.Nothing, UnitId = ship.ID });
            return true;
        }

        bool GoCloserToEnemy(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (luxLogicState.ScoreNodes.Any(s => s == ship.Pos)) return false;
            if (enemyShips.Count == 0) return false;
            var closestEnemy = enemyShips.OrderBy(e => LuxLogicState.MapDistances[ship.Pos][e.Pos]).First();
            if (myShips.Where(s => !s.IsMoved).Any(s => LuxLogicState.MapDistances[s.Pos][closestEnemy.Pos] < LuxLogicState.MapDistances[ship.Pos][closestEnemy.Pos])) return false;
            var targetEnemyShip = enemyShips.OrderBy(es => LuxLogicState.MapDistances[ship.Pos][es.Pos]).First();
            if (LuxLogicState.MapDistances[ship.Pos][targetEnemyShip.Pos] == 1 && ship.Energy - 2 < targetEnemyShip.Energy) return false;
            var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);

            if (parent[targetEnemyShip.Pos] == null) return false;

            var nextPos = Dijkstra.GetNextPos(parent, targetEnemyShip.Pos);
            var direction = GetDirection(ship.Pos, nextPos);

            Steps.Add(new Step() { MoveType = direction, UnitId = ship.ID });
            return true;
        }


        bool SapSolo(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (ship.Energy < luxLogicState.Env_cfg.unit_sap_cost) return false;
            if (enemyShips.Any(e => GetSapDistance(e.PendingTarget??e.Pos, ship.Pos) <= luxLogicState.Env_cfg.unit_sensor_range)) return false;

            int? bestSapPos = null;
            double? bestSapValue = null;
            double bestMinEnemyEnergy = double.MaxValue;

            var predictedEnemies = enemyShips.Where(e => e.PredictedPos.HasValue).ToList();
            var unPredictedEnemies = enemyShips.Where(e => !e.PredictedPos.HasValue).ToList();

            for (var dx = -luxLogicState.Env_cfg.unit_sap_range; dx <= luxLogicState.Env_cfg.unit_sap_range; dx++)
            {
                for (var dy = -luxLogicState.Env_cfg.unit_sap_range; dy <= luxLogicState.Env_cfg.unit_sap_range; dy++)
                {
                    var nx = ship.X + dx;
                    var ny = ship.Y + dy;
                    if (nx < 0 || nx >= luxLogicState.Env_cfg.map_width) continue;
                    if (ny < 0 || ny >= luxLogicState.Env_cfg.map_height) continue;

                    var sapPos = LuxLogicState.GetPos(nx, ny);

                    double sapValue = predictedEnemies.Count(e => (e.PredictedPos ?? e.Pos) == sapPos && e.Energy >= 0);
                    sapValue += unPredictedEnemies.Count(e => e.Pos == sapPos && e.Energy >= 0) * (luxLogicState.SapDropFactor??1);
                    //if ((luxLogicState.Map[sapPos] & MapBit.EnemyStayed) != MapBit.EnemyStayed && !luxLogicState.ScoreNodes.Any(s => s == sapPos))
                    //{
                    //    sapValue *= luxLogicState.SapDropFactor ?? 1;
                    //}
                    //     sapValue += enemyShips.Count(e => e.Pos != sapPos && e.Energy > 0 && Math.Abs(e.X - nx) <= 1 && Math.Abs(e.Y - ny) <= 1) * ((luxLogicState.SapDropFactor ?? 1)*0.5 - 0.001);

                    for (int dx2 = -1; dx2 <= 1; dx2++)
                    {
                        for (int dy2 = -1; dy2 <= 1; dy2++)
                        {
                            if (dx2 == 0 && dy2 == 0) continue;
                            var nx2 = nx + dx2;
                            var ny2 = ny + dy2;
                            if (nx2 < 0 || nx2 >= luxLogicState.Env_cfg.map_width) continue;
                            if (ny2 < 0 || ny2 >= luxLogicState.Env_cfg.map_height) continue;

                            var sapPos2 = LuxLogicState.GetPos(nx2, ny2);

                            if (luxLogicState.LuxInput.obs.map_features.tile_type[nx2][ny2] == TileType.Unknown)
                            {
                                //   sapValue += 0.01;
                                if (luxLogicState.ScoreNodes.Any(s => s == sapPos2))
                                {
                                    //  sapValue += 0.05;
                                }
                            }

                            sapValue += predictedEnemies.Count(e => (e.PredictedPos ?? e.Pos) == sapPos2 && e.Energy >= 0) * (luxLogicState.SapDropFactor ?? 1) * 0.9;
                            sapValue += unPredictedEnemies.Count(e => (e.PredictedPos ?? e.Pos) == sapPos2 && e.Energy >= 0) * (luxLogicState.SapDropFactor ?? 1) * 0.9;
                        }
                    }

                    var sapedEnemies = enemyShips.Where(e => e.Pos == sapPos).ToList();
                    double currentSapedEnergyEnemy = double.MaxValue;
                    if (sapedEnemies.Count > 0)
                    {
                        currentSapedEnergyEnemy = sapedEnemies.Min(e => e.Energy);
                    }
                    if (sapValue >= 1 && (bestSapValue == null || (sapValue > bestSapValue || (sapValue == bestSapPos && currentSapedEnergyEnemy < bestMinEnemyEnergy))))
                    {
                        bestSapValue = sapValue;
                        bestSapPos = sapPos;

                        bestMinEnemyEnergy = currentSapedEnergyEnemy;
                    }
                }
            }

            if (bestSapPos != null)
            {
                var (sx, sy) = LuxLogicState.GetXY(bestSapPos.Value);
                Targeted[ship.Pos]++;
                Steps.Add(new Step() { MoveType = MoveType.Sap, UnitId = ship.ID, X = sx - ship.X, Y = sy - ship.Y });
                var directEnemies = enemyShips.Where(e => (e.PredictedPos ?? e.Pos)  == bestSapPos).ToList();
                foreach (var enemy in directEnemies)
                {
                    enemy.Energy -= luxLogicState.Env_cfg.unit_sap_cost;
                }
                var nearEnemies = enemyShips.Where(e => (e.PredictedPos ?? e.Pos) != bestSapPos && Math.Abs(e.X - sx) <= 1 && Math.Abs(e.Y - sy) <= 1).ToList();
                foreach (var enemy in nearEnemies)
                {
                    enemy.Energy -= (int)Math.Round(luxLogicState.Env_cfg.unit_sap_cost * luxLogicState.SapDropFactor ?? 1);
                }
                luxLogicState.LastSapPositions.Add(bestSapPos.Value);
                UpdateTiles(luxLogicState, new List<int>() { ship.Pos });
                return true;
            }

            return false;
        }

        bool SapCrowd(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (ship.Energy < luxLogicState.Env_cfg.unit_sap_cost) return false;

            int? bestSapPos = null;
            double? bestSapValue = null;

            var predictedEnemies = enemyShips.Where(e => e.PredictedPos.HasValue).ToList();
            var unPredictedEnemies = enemyShips.Where(e => !e.PredictedPos.HasValue).ToList();

            for (var dx = -luxLogicState.Env_cfg.unit_sap_range; dx <= luxLogicState.Env_cfg.unit_sap_range; dx++)
            {
                for (var dy = -luxLogicState.Env_cfg.unit_sap_range; dy <= luxLogicState.Env_cfg.unit_sap_range; dy++)
                {
                    var nx = ship.X + dx;
                    var ny = ship.Y + dy;
                    if (nx < 0 || nx >= luxLogicState.Env_cfg.map_width) continue;
                    if (ny < 0 || ny >= luxLogicState.Env_cfg.map_height) continue;

                    var sapPos = LuxLogicState.GetPos(nx, ny);

                    double sapValue = predictedEnemies.Count(e => (e.PredictedPos ?? e.Pos) == sapPos && e.Energy >= 0);
                    sapValue += unPredictedEnemies.Count(e => e.Pos == sapPos && e.Energy >= 0) * (luxLogicState.SapDropFactor ?? 1);

                    //     sapValue += enemyShips.Count(e => e.Pos != sapPos && e.Energy > 0 && Math.Abs(e.X - nx) <= 1 && Math.Abs(e.Y - ny) <= 1) * ((luxLogicState.SapDropFactor ?? 1)*0.5 - 0.001);

                    for (int dx2 = -1; dx2 <= 1; dx2++)
                    {
                        for (int dy2 = -1; dy2 <= 1; dy2++)
                        {
                            if (dx2 == 0 && dy2 == 0) continue;
                            var nx2 = nx + dx2;
                            var ny2 = ny + dy2;
                            if (nx2 < 0 || nx2 >= luxLogicState.Env_cfg.map_width) continue;
                            if (ny2 < 0 || ny2 >= luxLogicState.Env_cfg.map_height) continue;

                            var sapPos2 = LuxLogicState.GetPos(nx2, ny2);

                            if (luxLogicState.LuxInput.obs.map_features.tile_type[nx2][ny2] == TileType.Unknown)
                            {
                                //   sapValue += 0.01;
                                if (luxLogicState.ScoreNodes.Any(s => s == sapPos2))
                                {
                                    //  sapValue += 0.05;
                                }
                            }

                            sapValue += enemyShips.Count(e => (e.PredictedPos ?? e.Pos) == sapPos2 && e.Energy >= 0) * (luxLogicState.SapDropFactor ?? 1) * 0.99;
                        }
                    }

                    if (sapValue > 1 && (bestSapValue == null || sapValue > bestSapValue))
                    {
                        bestSapValue = sapValue;
                        bestSapPos = sapPos;
                    }
                }
            }

            if (bestSapPos != null)
            {
                var (sx, sy) = LuxLogicState.GetXY(bestSapPos.Value);
                Targeted[ship.Pos]++;
                Steps.Add(new Step() { MoveType = MoveType.Sap, UnitId = ship.ID, X = sx - ship.X, Y = sy - ship.Y });
                var directEnemies = enemyShips.Where(e => (e.PredictedPos ?? e.Pos) == bestSapPos).ToList();
                foreach (var enemy in directEnemies)
                {
                    enemy.Energy -= luxLogicState.Env_cfg.unit_sap_cost;
                }
                var nearEnemies = enemyShips.Where(e => (e.PredictedPos ?? e.Pos) != bestSapPos && Math.Abs(e.X - sx) <= 1 && Math.Abs(e.Y - sy) <= 1).ToList();
                foreach (var enemy in nearEnemies)
                {
                    enemy.Energy -= (int)Math.Round(luxLogicState.Env_cfg.unit_sap_cost * luxLogicState.SapDropFactor ?? 1);
                }
                luxLogicState.LastSapPositions.Add(bestSapPos.Value);
                UpdateTiles(luxLogicState, new List<int>() { ship.Pos });
                return true;
            }

            return false;
        }

        bool SapKill(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (ship.Energy < luxLogicState.Env_cfg.unit_sap_cost) return false;

            var bestTarget = enemyShips.Where(e => e.Energy >= 0)
                .Where(e => e.Energy - myShips.Count(ms => !ms.IsMoved && ms.Energy >= luxLogicState.Env_cfg.unit_sap_cost && GetSapDistance(ms.Pos, e.PendingTarget??e.Pos) <= luxLogicState.Env_cfg.unit_sap_range) * luxLogicState.Env_cfg.unit_sap_cost < 0)
                .Where(e => (luxLogicState.Map[e.PredictedPos ?? e.Pos] & MapBit.EnemyStayed) == MapBit.EnemyStayed || luxLogicState.ScoreNodes.Any(s => s == (e.PredictedPos ?? e.Pos)) || e.Energy < luxLogicState.Env_cfg.unit_move_cost || e.Energy < (luxLogicState.Env_cfg.unit_sap_cost * luxLogicState.SapDropFactor ?? 1))
                .OrderByDescending(e => luxLogicState.ScoreNodes.Any(s => s == (e.PredictedPos ?? e.Pos)))
                .ThenBy(e => e.Energy).FirstOrDefault();

            //var bestTarget = enemyShips.Where(e => e.Energy >= 0)
            //    .Where(e => e.Energy - myShips.Count(ms => !ms.IsMoved && ms.Energy >= luxLogicState.Env_cfg.unit_sap_cost && GetSapDistance(ms.Pos, e.Pos) <= luxLogicState.Env_cfg.unit_sap_range) * luxLogicState.Env_cfg.unit_sap_cost < 0)
            //    .OrderByDescending(e => luxLogicState.ScoreNodes.Any(s => s == e.Pos))
            //    .ThenBy(e => e.Energy).FirstOrDefault();

            if (bestTarget == null) return false;
            if (!bestTarget.PredictedPos.HasValue || luxLogicState.SapDropFactor==0.25) return false;
            if (GetSapDistance(ship.Pos, bestTarget.PredictedPos ?? bestTarget.Pos) > luxLogicState.Env_cfg.unit_sap_range) return false;
            if ((luxLogicState.Map[bestTarget.PredictedPos ?? bestTarget.Pos] & MapBit.EnemyStayed) == 0) return false;

            var bestSapPos = bestTarget.PredictedPos ?? bestTarget.Pos;
            var (sx, sy) = LuxLogicState.GetXY(bestSapPos);
            Targeted[ship.Pos]++;
            Steps.Add(new Step() { MoveType = MoveType.Sap, UnitId = ship.ID, X = sx - ship.X, Y = sy - ship.Y });
            var directEnemies = enemyShips.Where(e => (e.PredictedPos ?? e.Pos) == bestSapPos).ToList();
            foreach (var enemy in directEnemies)
            {
                enemy.Energy -= luxLogicState.Env_cfg.unit_sap_cost;
            }
            var nearEnemies = enemyShips.Where(e => (e.PredictedPos ?? e.Pos) != bestSapPos && Math.Abs(e.X - sx) <= 1 && Math.Abs(e.Y - sy) <= 1).ToList();
            foreach (var enemy in nearEnemies)
            {
                enemy.Energy -= (int)Math.Round(luxLogicState.Env_cfg.unit_sap_cost * luxLogicState.SapDropFactor ?? 1);
            }
            luxLogicState.LastSapPositions.Add(bestSapPos);
            UpdateTiles(luxLogicState, new List<int>() { ship.Pos });

            return true;
        }

        bool MoveTank(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (!ship.IsTank) return false;
            var targetEnemy = enemyShips.Single(e => e.ID == ship.TargetEnemyId);
            var (x,y) = LuxLogicState.GetXY(ship.Pos);
            var targetPos = ship.Pos;
            for(var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    if (nx < 0 || nx >= luxLogicState.Env_cfg.map_width) continue;
                    if (ny < 0 || ny >= luxLogicState.Env_cfg.map_height) continue;

                    if (Math.Abs(dx) + Math.Abs(dy) != 1) continue;
                    var newPos = LuxLogicState.GetPos(nx, ny);

                    if ((luxLogicState.Map[newPos] & MapBit.AsteroidBit) == MapBit.AsteroidBit) continue;
                    if (GetSapDistance(newPos, targetEnemy.Pos) > luxLogicState.Env_cfg.unit_sensor_range) continue;
                    if (LuxLogicState.MapDistances[newPos][MyBasePos]< LuxLogicState.MapDistances[targetPos][MyBasePos] ||
                        (LuxLogicState.MapDistances[newPos][MyBasePos] == LuxLogicState.MapDistances[targetPos][MyBasePos] && GetSumEnergyModifier(targetPos, luxLogicState) < GetSumEnergyModifier(newPos, luxLogicState))){
                        targetPos = newPos;
                    }
                }
            }
            Targeted[targetPos]++;
            if (targetPos == ship.Pos) return true;

            var step = new Step() { MoveType = GetDirection(ship.Pos, targetPos), UnitId = ship.ID };
            Steps.Add(step);

            return true;
        }

        bool MovePredator(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (ship.IsTank) return false;
            if (!ship.TargetEnemyId.HasValue) return false;
            var targetEnemy = enemyShips.Single(e => e.ID == ship.TargetEnemyId);

            if (GetSapDistance(targetEnemy.Pos, ship.Pos) > luxLogicState.Env_cfg.unit_sap_range)
            {
                var targetPos = targetEnemy.Pos;
                var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);
                double currentDist = dist[targetPos];
                for(var dx = -luxLogicState.Env_cfg.unit_sap_range; dx <= luxLogicState.Env_cfg.unit_sap_range; dx++)
                {
                    for (var dy = -luxLogicState.Env_cfg.unit_sap_range; dy <= luxLogicState.Env_cfg.unit_sap_range; dy++)
                    {
                        var nx = targetEnemy.X + dx;
                        var ny = targetEnemy.Y + dy;
                        if (nx < 0 || nx >= luxLogicState.Env_cfg.map_width) continue;
                        if (ny < 0 || ny >= luxLogicState.Env_cfg.map_height) continue;

                        var newPos = LuxLogicState.GetPos(nx, ny);
                        if (parent[newPos] == null) continue;
                        if (dist[newPos] < currentDist)
                        {
                            targetPos = newPos;
                            currentDist = dist[newPos];
                        }
                    }
                }

                if (parent[targetPos] == null) return false;
                Targeted[targetPos]++;
                var nextPos = Dijkstra.GetNextPos(parent, targetPos);
                var direction = GetDirection(ship.Pos, nextPos);
                Steps.Add(new Step() { MoveType = direction, UnitId = ship.ID });
                return true;
            }
            return false;
            //else
            //{
            //    if (DangerMap[ship.Pos] > 0)
            //    {
            //        var targetPos = ship.Pos;
            //        var (x,y) = LuxLogicState.GetXY(ship.Pos);
            //        for (var dx = -1; dx <= 1; dx++)
            //        {
            //            for (var dy = -1; dy <= 1; dy++)
            //            {
            //                var nx = x + dx;
            //                var ny = y + dy;
            //                if (nx < 0 || nx >= luxLogicState.Env_cfg.map_width) continue;
            //                if (ny < 0 || ny >= luxLogicState.Env_cfg.map_height) continue;

            //                if (Math.Abs(dx) + Math.Abs(dy) != 1) continue;
            //                var newPos = LuxLogicState.GetPos(nx, ny);

            //                if ((luxLogicState.Map[newPos] & MapBit.AsteroidBit) == MapBit.AsteroidBit) continue;
            //                if (GetSapDistance(newPos, targetEnemy.Pos) > luxLogicState.Env_cfg.unit_sap_range) continue;
            //                if (LuxLogicState.MapDistances[newPos][MyBasePos] > LuxLogicState.MapDistances[targetPos][MyBasePos]) continue;

            //                if (GetSapDistance(newPos, targetEnemy.Pos) > GetSapDistance(targetPos, targetEnemy.Pos) ||
            //                    (GetSapDistance(newPos, targetEnemy.Pos) == GetSapDistance(targetPos, targetEnemy.Pos) && GetSumEnergyModifier(targetPos, luxLogicState) < GetSumEnergyModifier(newPos, luxLogicState))){
            //                    targetPos = newPos;
            //                }
            //            }
            //        }
            //        Targeted[targetPos]++;
            //        if (targetPos == ship.Pos) return false;

            //        var step = new Step() { MoveType = GetDirection(ship.Pos, targetPos), UnitId = ship.ID };
            //        Steps.Add(step);
            //        return true;
            //    }
            //    return false;
            //}
        }


        bool MovePredatorAfterKillStep(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (ship.IsTank) return false;
            if (!ship.TargetEnemyId.HasValue) return false;
            var targetEnemy = enemyShips.Single(e => e.ID == ship.TargetEnemyId);
            var (x,y) = LuxLogicState.GetXY(ship.Pos);
            var targetPos = ship.Pos;
            if (GetSapDistance(targetEnemy.Pos, ship.Pos) <= luxLogicState.Env_cfg.unit_sap_range)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        var nx = x + dx;
                        var ny = y + dy;
                        if (nx < 0 || nx >= luxLogicState.Env_cfg.map_width) continue;
                        if (ny < 0 || ny >= luxLogicState.Env_cfg.map_height) continue;

                        if (Math.Abs(dx) + Math.Abs(dy) != 1) continue;
                        var newPos = LuxLogicState.GetPos(nx, ny);

                        if (DangerMap[newPos] > 0) continue;
                        if (GetSapDistance(targetEnemy.Pos, newPos) > luxLogicState.Env_cfg.unit_sap_range) continue;
                        if ((luxLogicState.Map[newPos] & MapBit.AsteroidBit) == MapBit.AsteroidBit) continue;
                        if (GetSumEnergyModifier(targetPos, luxLogicState) < GetSumEnergyModifier(newPos, luxLogicState))
                        {
                            targetPos = newPos;
                        }
                    }
                }
                Targeted[targetPos]++;
                if (targetPos == ship.Pos) return true;

                var step = new Step() { MoveType = GetDirection(ship.Pos, targetPos), UnitId = ship.ID };
                Steps.Add(step);
                return true;
            }
            return false;
        }


        bool SapTargetKill(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (ship.Energy < luxLogicState.Env_cfg.unit_sap_cost) return false;
            if (ship.TargetEnemyId==null) return false;
            var targetEnemy = enemyShips.Single(e => e.ID == ship.TargetEnemyId);
            if (targetEnemy.Energy < 0) return false;
        //    if ((luxLogicState.Map[targetEnemy.Pos] & MapBit.EnemyStayed) != MapBit.EnemyStayed) return false;
       //     if (GetSapDistance(ship.Pos, targetEnemy.Pos)>luxLogicState.Env_cfg.unit_sap_range) return false;

            //var sumSap = myShips.Where(ms => !ms.IsMoved && ms.TargetEnemyId==targetEnemy.ID && GetSapDistance(ms.Pos, targetEnemy.Pos) <= luxLogicState.Env_cfg.unit_sap_range).Sum(ms => ms.Energy/luxLogicState.Env_cfg.unit_sap_cost);
            var sumSap = myShips.Where(ms => !ms.IsMoved && ms.TargetEnemyId == targetEnemy.ID && GetSapDistance(ms.Pos, targetEnemy.Pos) <= luxLogicState.Env_cfg.unit_sap_range).Count(ms => ms.Energy >= luxLogicState.Env_cfg.unit_sap_cost);
            if (targetEnemy.Energy - sumSap * luxLogicState.Env_cfg.unit_sap_cost + GetSumEnergyModifier(targetEnemy.Pos, luxLogicState) >= 0) return false;

            //var bestTarget = enemyShips.Where(e => e.Energy >= 0)
            //    .Where(e => e.Energy - myShips.Count(ms => !ms.IsMoved && ms.Energy >= luxLogicState.Env_cfg.unit_sap_cost && GetSapDistance(ms.Pos, e.Pos) <= luxLogicState.Env_cfg.unit_sap_range) * luxLogicState.Env_cfg.unit_sap_cost < 0)
            //    .OrderByDescending(e => luxLogicState.ScoreNodes.Any(s => s == e.Pos))
            //    .ThenBy(e => e.Energy).FirstOrDefault();

            var bestSapPos = targetEnemy.Pos;
            var (sx, sy) = LuxLogicState.GetXY(bestSapPos);
            Targeted[ship.Pos]++;
            Steps.Add(new Step() { MoveType = MoveType.Sap, UnitId = ship.ID, X = sx - ship.X, Y = sy - ship.Y });
            var directEnemies = enemyShips.Where(e => e.Pos == bestSapPos).ToList();
            foreach (var enemy in directEnemies)
            {
                //                enemy.Energy -= luxLogicState.Env_cfg.unit_sap_cost;
                enemy.Energy -= (int)Math.Round(luxLogicState.Env_cfg.unit_sap_cost * (luxLogicState.SapDropFactor ?? 1));
            }
            var nearEnemies = enemyShips.Where(e => e.Pos != bestSapPos && Math.Abs(e.X - sx) <= 1 && Math.Abs(e.Y - sy) <= 1).ToList();
            foreach (var enemy in nearEnemies)
            {
                enemy.Energy -= (int)Math.Round(luxLogicState.Env_cfg.unit_sap_cost * (luxLogicState.SapDropFactor ?? 1));
            }
            luxLogicState.LastSapPositions.Add(bestSapPos);
            UpdateTiles(luxLogicState, new List<int>() { ship.Pos });

            return true;
        }

        bool ChaseEnemy(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            enemyShips = enemyShips.Where(e => e.Energy >= 0 && LuxLogicState.MapDistances[ship.Pos][e.Pos] == 1 && (luxLogicState.Map[e.Pos] & MapBit.AsteroidBit) != MapBit.AsteroidBit).ToList();
            if (enemyShips.Count == 0) return false;
            var closestEnemy = enemyShips.Where(e => ship.Energy - luxLogicState.Env_cfg.unit_move_cost > e.Energy).OrderByDescending(e => e.Energy).FirstOrDefault();
            if (closestEnemy == null) return false;
            if (myShips.Any(ms => LuxLogicState.MapDistances[closestEnemy.Pos][ms.Pos] == 1 && (ms.Energy + ms.ID / 100.0) > (ship.Energy + ship.ID / 100.0))) return false;

            var direction = GetDirection(ship.Pos, closestEnemy.Pos);
            closestEnemy.Energy = -1;
            Steps.Add(new Step() { MoveType = direction, UnitId = ship.ID });
            return true;
        }

        bool MoveToEnemy(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            enemyShips = enemyShips.Where(e => e.Energy >= 0).ToList();
            if (enemyShips.Count == 0) return false;
            var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);
            //var closestEnemy = enemyShips.Where(e=> routeDistances[e.Pos].HasValue).OrderBy(e => routeDistances[e.Pos]).FirstOrDefault();
            //if (luxLogicState.ScoreNodes.Any(s => s == ship.Pos))
            //{s
            var closestEnemy = enemyShips.Where(e => routeDistances[e.Pos].HasValue && routeDistances[e.Pos] < luxLogicState.Env_cfg.unit_sensor_range * 2 && GetSapDistance(ship.Pos, e.Pos) <= luxLogicState.Env_cfg.unit_sensor_range && luxLogicState.ScoreNodes.Any(s => s == e.Pos) && energyMap[e.Pos] > e.Energy + luxLogicState.EnergyMap[e.Pos] * routeDistances[e.Pos]).OrderBy(e => routeDistances[e.Pos]).FirstOrDefault();
            //}
            if (closestEnemy == null) return false;
            if (myShips.Any(s => GetDijsktraResult(luxLogicState, s).ToTuple().Item4[closestEnemy.Pos] < routeDistances[closestEnemy.Pos] &&
                GetDijsktraResult(luxLogicState, s).ToTuple().Item3[closestEnemy.Pos] > closestEnemy.Energy + luxLogicState.EnergyMap[closestEnemy.Pos] * GetDijsktraResult(luxLogicState, s).ToTuple().Item4[closestEnemy.Pos])) return false;

            var nextPos = Dijkstra.GetNextPos(parent, closestEnemy.Pos);
            var direction = GetDirection(ship.Pos, nextPos);
            UpdateTiles(luxLogicState, Dijkstra.GetRoute(parent, closestEnemy.Pos));
     //       closestEnemy.Energy = -1;
            Steps.Add(new Step() { MoveType = direction, UnitId = ship.ID });
            return true;
        }


        bool SapUnvisibleTarget(Ship ship, LuxLogicState luxLogicState, int moveTarget, List<int> sapTargets)
        {
            if (luxLogicState.LuxInput.obs.team_points[1 - luxLogicState.PlayerId] < 20) return false;
            if (ship.Energy < luxLogicState.Env_cfg.unit_sap_cost) return false;
            var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);
            if (energyMap[moveTarget] < luxLogicState.Env_cfg.unit_sap_cost) return false;

            var avaibleTargets = sapTargets.Where(s => GetSapDistance(ship.Pos, s) <= luxLogicState.Env_cfg.unit_sap_range &&
                                                  (luxLogicState.Map[s] & MapBit.Visible) != MapBit.Visible &&
                                                  SapTargeted[s] == 0).ToList();
            if (avaibleTargets.Count == 0) return false;

            //var bestTarget = avaibleTargets.OrderByDescending(t=>sapTargets.Count(s=>GetSapDistance(s,t)<=1)).ThenByDescending(s => LuxLogicState.MapDistances[s][ship.Pos]).First();
            var bestTarget = Enumerable.Range(0, LuxLogicState.Size2).Where(p=>GetSapDistance(p,ship.Pos)<=luxLogicState.Env_cfg.unit_sap_range).OrderByDescending(p => avaibleTargets.Count(t => GetSapDistance(t, p) == 1) * (luxLogicState.SapDropFactor ?? 1.0) + avaibleTargets.Count(t => t == p)).First();
            // var bestTarget = avaibleTargets.OrderByDescending(s => LuxLogicState.MapDistances[s][ship.Pos]).First();

            var sapValue = avaibleTargets.Count(t => GetSapDistance(t, bestTarget) == 1) * (luxLogicState.SapDropFactor ?? 1.0) + avaibleTargets.Count(t => t == bestTarget);
            

            if (sapValue <= 1) return false;

            SapTargeted[bestTarget]++;
            var (sx, sy) = LuxLogicState.GetXY(bestTarget);
            Steps.Add(new Step() { MoveType = MoveType.Sap, UnitId = ship.ID, X = sx - ship.X, Y = sy - ship.Y });
            return true;
        }

        bool Escape(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            var dangerEnemies = enemyShips.Where(e => LuxLogicState.MapDistances[e.Pos][ship.Pos] == 1 && e.Energy > ship.Energy + luxLogicState.Env_cfg.unit_move_cost).ToList();
            if (dangerEnemies.Count == 0) return false;

            int? maxPos = null;
            int? maxPosEnergyValue = null;
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var newX = ship.X + dx;
                    var newY = ship.Y + dy;
                    if (newX < 0 || newX >= luxLogicState.Env_cfg.map_width) continue;
                    if (newY < 0 || newY >= luxLogicState.Env_cfg.map_height) continue;
                    if (Math.Abs(dx) + Math.Abs(dy) != 1) continue;
                    var newPos = LuxLogicState.GetPos(newX, newY);
                    if (luxLogicState.Map[newPos] == MapBit.AsteroidBit) continue;
                    if (dangerEnemies.Any(e => e.Pos == newPos)) continue;
                    if (!maxPosEnergyValue.HasValue || GetSumEnergyModifier(newPos, luxLogicState) > maxPosEnergyValue)
                    {
                        maxPosEnergyValue = GetSumEnergyModifier(newPos, luxLogicState);
                        maxPos = newPos;
                    }
                }
            }
            if (!maxPosEnergyValue.HasValue) return false;
            Targeted[maxPos.Value]++;
            Steps.Add(new Step() { MoveType = GetDirection(ship.Pos, maxPos.Value), UnitId = ship.ID });
            return true;
        }

        bool GoToCandidate(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (IsWentToCandidate) return false;
            if (luxLogicState.CandidateGroupList.Count == 0) return false;
            if (luxLogicState.ScoreNodes.Any(s => ship.Pos == s && myShips.Count(ms => ms.Pos == s) == 1)) return false;

            var notAsteroidCandidates = luxLogicState.CandidateGroupList.SelectMany(c => c.CandidatePos).Where(c => (luxLogicState.Map[c] & MapBit.AsteroidBit) != MapBit.AsteroidBit).ToList();
            if (notAsteroidCandidates.Count == 0) return false;
            var closestCandidateDist = notAsteroidCandidates.Min(c => myShips.Where(s => !s.IsMoved).Min(s => LuxLogicState.MapDistances[s.Pos][c]));
            var closestCandidate = luxLogicState.CandidateGroupList.SelectMany(c => c.CandidatePos).OrderBy(c => LuxLogicState.MapDistances[ship.Pos][c]).First();

            if (closestCandidateDist < LuxLogicState.MapDistances[ship.Pos][closestCandidate]) return false;

            var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);
            if (parent[closestCandidate] == null && ship.Pos != closestCandidate) return false;

            var route = Dijkstra.GetRoute(parent, closestCandidate);
            UpdateTiles(luxLogicState, route);
            IsWentToCandidate = true;
            var nextPos = Dijkstra.GetNextPos(parent, closestCandidate);
            var direction = GetDirection(ship.Pos, nextPos);
            Steps.Add(new Step() { MoveType = direction, UnitId = ship.ID });
            return true;
        }


        bool GoToNN(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (ship.Energy < luxLogicState.Env_cfg.unit_move_cost) return true;
            var (x, y) = LuxLogicState.GetXY(ship.Pos);
            var moveType = GetNextPos(luxLogicState, NNoutput, ship.Pos);
            if ((moveType == MoveType.Sap || moveType == MoveType.Nothing) && ship.Energy >= luxLogicState.Env_cfg.unit_sap_cost)
            {
                //throw new Exception("Sap");
                var maxSapValue = 0.0;
                var maxSapPos = -1;
                for (var dx = -luxLogicState.Env_cfg.unit_sap_range; dx <= luxLogicState.Env_cfg.unit_sap_range; dx++)
                {
                    for (var dy = -luxLogicState.Env_cfg.unit_sap_range; dy <= luxLogicState.Env_cfg.unit_sap_range; dy++)
                    {
                        var nx = x + dx;
                        var ny = y + dy;
                        var nPos = LuxLogicState.GetPos(nx, ny);
                        if (nx < 0 || nx >= 24 || ny < 0 || ny >= 24) continue;
                        if (NNoutput[nx, ny, 6] > maxSapValue && SapMap[nPos] == 0)
                        {
                            maxSapValue = NNoutput[nx, ny, 6];
                            maxSapPos = nPos;
                        }
                    }
                }
                var sapThreshold = 0.5;
                if (luxLogicState.SapDropFactor == 0.5) sapThreshold = 0.25;
                if (luxLogicState.SapDropFactor == 1) sapThreshold = 0.1;
                if (maxSapValue > sapThreshold - NNoutput[ship.X, ship.Y, 6])
                {
                    var (mx, my) = LuxLogicState.GetXY(maxSapPos);
                    ship.IsMoved = true;
                    Steps.Add(new Step() { MoveType = MoveType.Sap, UnitId = ship.ID, X = mx - x, Y = my - y });
                    SapMap[maxSapPos]++;
                }
                else
                {
                    ship.IsMoved = true;
                    Steps.Add(new Step() { MoveType = MoveType.Nothing, UnitId = ship.ID });
                }
            }
            else
            {
                ship.IsMoved = true;
                Steps.Add(new Step() { MoveType = moveType, UnitId = ship.ID });
            }
            Targeted[GetPosByMoveType(ship.Pos, moveType)]++;
            return true;
        }

        int GetPosByMoveType(int pos, MoveType moveType)
        {
            var (x, y) = LuxLogicState.GetXY(pos);
            switch (moveType)
            {
                case MoveType.Sap:
                case MoveType.Nothing:
                    return pos;
                case MoveType.Up:
                    var nx = x;
                    var ny = y - 1;
                    return LuxLogicState.GetPos(nx, ny);
                case MoveType.Down:
                    nx = x;
                    ny = y + 1;
                    return LuxLogicState.GetPos(nx, ny);
                case MoveType.Left:
                    nx = x - 1;
                    ny = y;
                    return LuxLogicState.GetPos(nx, ny);
                case MoveType.Right:
                    nx = x + 1;
                    ny = y;
                    return LuxLogicState.GetPos(nx, ny);
                default:
                    throw new Exception("Nem jo movetype");
            }
        }

        MoveType GetNextPos(LuxLogicState luxLogicState, float[,,] nnOutput, int pos)
        {
            var (x, y) = LuxLogicState.GetXY(pos);
            var moveType = MoveType.Nothing;
            var actualValue = nnOutput[x, y, (int)moveType];
            if (luxLogicState.ScoreNodes.Any(s => s == pos) && luxLogicState.StartStep % 101 > 70) actualValue *= 4;
            if (Targeted[pos] > 0 || (luxLogicState.StartStep % 101 > 70 && !luxLogicState.ScoreNodes.Any(s => s == pos))) actualValue /= 2;
            for (var i = (int)moveType + 1; i < 6; i++)
            {
                if (!IsValid(luxLogicState, pos, (MoveType)i)) continue;
                var nextPos = GetPosByMoveType(pos, (MoveType)i);
                if (LuxLogicState.MapDistances[pos][EnemyBasePos] > LuxLogicState.MapDistances[nextPos][EnemyBasePos])
                {
                    nnOutput[x, y, i] *= 4;
                }
                if (Targeted[nextPos] > 0)
                {
                    nnOutput[x, y, i] /= 2;
                }
                if (nnOutput[x, y, i] > actualValue)
                {
                    actualValue = nnOutput[x, y, i];
                    moveType = (MoveType)i;
                }
            }
            return moveType;
        }

        bool IsValid(LuxLogicState luxLogicState, int pos, MoveType direction)
        {
            var (x, y) = LuxLogicState.GetXY(pos);
            switch (direction)
            {
                case MoveType.Nothing:
                    return true;
                case MoveType.Sap:
                    return true;
                case MoveType.Up:
                    return y > 0 && (luxLogicState.Map[LuxLogicState.GetPos(x, y - 1)] & MapBit.AsteroidBit) != MapBit.AsteroidBit;
                case MoveType.Down:
                    return y < 23 && (luxLogicState.Map[LuxLogicState.GetPos(x, y + 1)] & MapBit.AsteroidBit) != MapBit.AsteroidBit;
                case MoveType.Left:
                    return x > 0 && (luxLogicState.Map[LuxLogicState.GetPos(x - 1, y)] & MapBit.AsteroidBit) != MapBit.AsteroidBit;
                case MoveType.Right:
                    return x < 23 && (luxLogicState.Map[LuxLogicState.GetPos(x + 1, y)] & MapBit.AsteroidBit) != MapBit.AsteroidBit;
                default:
                    throw new Exception("Nem jo movetype");
            }
        }

        public (double[], int?[], int[] energyMap, int?[] routeDistances) GetDijsktraResult(LuxLogicState luxLogicState, Ship ship)
        {
            if (!ship.HasDijsktraResult)
            {
                var zeroDistNodes = luxLogicState.ScoreNodes.Where(s => RouteDistancesToMyBase[s]<15).ToList();

                ship.DijsktraResult = Dijkstra.ShortestPaths(DangerMap, luxLogicState.Tiles, ship.Pos, ship.Energy, luxLogicState.StartStep, 1, 0.09, -0.01, 0, 0, 0,
                            luxLogicState.Env_cfg.unit_move_cost, luxLogicState.NebulaEnergyModified ?? 0, luxLogicState.EnemyShips, zeroDistNodes);
                ship.HasDijsktraResult = true;
            }
            return ship.DijsktraResult;
        }

        public void ClearDijsktraResults(LuxLogicState luxLogicState)
        {
            foreach (var ship in luxLogicState.MyShips)
            {
               // ship.HasDijsktraResult = false;
                ship.DijsktraResult = GetDijsktraResult(luxLogicState, ship);
            }
        }

        void SetDangerMap(LuxLogicState luxLogicState, List<Ship> enemyShips)
        {
            DangerMap = new sbyte[LuxLogicState.Size2];
            if (enemyShips.Count == 0) return;
            var addition = 0;
            bool hasSapPosition = false;
            while (!hasSapPosition)
            {
                DangerMap = new sbyte[LuxLogicState.Size2];
                foreach (var ship in enemyShips)
                {
                    for (int dx = -luxLogicState.Env_cfg.unit_sensor_range - addition; dx <= luxLogicState.Env_cfg.unit_sensor_range + addition; dx++)
                    {
                        for (int dy = -luxLogicState.Env_cfg.unit_sensor_range - addition; dy <= luxLogicState.Env_cfg.unit_sensor_range + addition; dy++)
                        {
                            var nx = ship.X + dx;
                            var ny = ship.Y + dy;
                            if (nx < 0 || nx >= luxLogicState.Env_cfg.map_width) continue;
                            if (ny < 0 || ny >= luxLogicState.Env_cfg.map_height) continue;
                            var newPos = LuxLogicState.GetPos(nx, ny);
                            DangerMap[newPos]++;
                        }
                    }
                }
                foreach (var ship in enemyShips)
                {
                    for (int dx = -luxLogicState.Env_cfg.unit_sap_range; dx <= luxLogicState.Env_cfg.unit_sap_range; dx++)
                    {
                        for (int dy = -luxLogicState.Env_cfg.unit_sap_range; dy <= luxLogicState.Env_cfg.unit_sap_range; dy++)
                        {
                            var nx = ship.X + dx;
                            var ny = ship.Y + dy;
                            if (nx < 0 || nx >= luxLogicState.Env_cfg.map_width) continue;
                            if (ny < 0 || ny >= luxLogicState.Env_cfg.map_height) continue;
                            var newPos = LuxLogicState.GetPos(nx, ny);
                            if (DangerMap[newPos] == 0)
                            {
                                hasSapPosition = true;
                            }
                        }
                    }
                }
                addition--;
            }
        }

        bool IsTargetAble(LuxLogicState luxLogicState, Ship enemyShip, int minDanger)
        {
            for (int dx = -luxLogicState.Env_cfg.unit_sensor_range; dx <= luxLogicState.Env_cfg.unit_sensor_range; dx++)
            {
                for(int dy = -luxLogicState.Env_cfg.unit_sensor_range; dy <= luxLogicState.Env_cfg.unit_sensor_range; dy++)
                {
                    var nx = enemyShip.X + dx;
                    var ny = enemyShip.Y + dy;
                    if (nx < 0 || nx >= luxLogicState.Env_cfg.map_width) continue;
                    if (ny < 0 || ny >= luxLogicState.Env_cfg.map_height) continue;
                    var newPos = LuxLogicState.GetPos(nx, ny);
                    if (DangerMap[newPos] <= minDanger) return true;
                }
            }
            return true;
        }

        private void AssingTargets(LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            if (enemyShips.Count == 0) return;
            var minDangerValue = DangerMap.Where(d => d > 0).Min(d => d);
            enemyShips = enemyShips.Where(e => e.Energy>=0 && IsTargetAble(luxLogicState, e, minDangerValue)).ToList();
            bool targetAssigned = true;
            while (targetAssigned)
            {
                targetAssigned=false;
                var energyOrderedEnemy = enemyShips.Where(e=>!e.Targeted).OrderBy(e => LuxLogicState.MapDistances[MyBasePos][e.Pos]).ThenBy(e=> e.Energy).ToList();
                foreach (var eShip in energyOrderedEnemy)
                {
                    var requiredSapCount = (int)Math.Ceiling(eShip.Energy + GetSumEnergyModifier(eShip.Pos, luxLogicState)/(luxLogicState.SapDropFactor??1) + 1) / luxLogicState.Env_cfg.unit_sap_cost + 1;
                    //var closestShips = myShips.Where(s => LuxLogicState.MapDistances[s.Pos][eShip.Pos] <= enemyShips.Min(oe => LuxLogicState.MapDistances[s.Pos][oe.Pos])).ToList();
                    var closestShips = myShips.Where(s => !s.TargetEnemyId.HasValue && GetSapDistance(s.Pos,eShip.Pos) <= enemyShips.Min(oe => GetSapDistance(s.Pos,oe.Pos))).ToList();
                    closestShips = closestShips.OrderBy(s => GetSapDistance(s.Pos, eShip.Pos)).ThenByDescending(s=>s.Energy).ToList();
                    if (closestShips.Sum(cs => cs.Energy / luxLogicState.Env_cfg.unit_sap_cost) >= requiredSapCount)
                    {
                        eShip.Targeted = true;
                        foreach (var ms in closestShips)
                        {
                            ms.TargetEnemyId = eShip.ID;
                            if (ms.Energy> luxLogicState.Env_cfg.unit_sap_cost)
                            {
                                requiredSapCount--;
                            }
                            //requiredSapCount-=ms.Energy / luxLogicState.Env_cfg.unit_sap_cost;
                            if (requiredSapCount <= 0)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            var groupedShips = myShips.GroupBy(s => s.TargetEnemyId);
            foreach (var groupedShip in groupedShips)
            {
                if (groupedShip.Key != null)
                {
                    var enemyPos = enemyShips.Single(e => e.ID == groupedShip.Key).Pos;
                    //var closestShipToEnemy = groupedShip.OrderBy(s => LuxLogicState.MapDistances[s.Pos][enemyPos]).First();
                    var closestShipToEnemy = groupedShip.OrderBy(s => GetSapDistance(s.Pos,enemyPos)).First();
                    closestShipToEnemy.IsTank = true;
                }
            }
        }

        int CalcVoronoiValue(LuxLogicState luxLogicState, List<int> mySeeds, List<int> enemySeeds)
        {
            var queue = new int[LuxLogicState.Size2];
            var pushIndex = 0;
            var index = 0;
            var voronoiMap = new int[LuxLogicState.Size2];
            foreach (var mySeed in mySeeds)
            {
                voronoiMap[mySeed] = 1;
                queue[pushIndex++] = mySeed;
            }
            enemySeeds = enemySeeds.Where(e => !mySeeds.Contains(e)).ToList();
            foreach (var enemySeed in enemySeeds)
            {
                voronoiMap[enemySeed] = 2;
                queue[pushIndex++] = enemySeed;
            }

            while (index < pushIndex)
            {
                var pos = queue[index++];
                var (x, y) = LuxLogicState.GetXY(pos);
                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        if (Math.Abs(dx) + Math.Abs(dy) != 1) continue;
                        var nx = x + dx;
                        var ny = y + dy;
                        if (nx < 0 || nx >= luxLogicState.Env_cfg.map_width) continue;
                        if (ny < 0 || ny >= luxLogicState.Env_cfg.map_height) continue;
                        var newPos = LuxLogicState.GetPos(nx, ny);
                        if (voronoiMap[newPos] != 0) continue;
                        if ((luxLogicState.Map[newPos] & MapBit.AsteroidBit) == MapBit.AsteroidBit) continue;
                        voronoiMap[newPos] = voronoiMap[pos];
                        queue[pushIndex++] = newPos;
                    }
                }
            }

            var myVoronoi = voronoiMap.Count(v => v == 1);
            var enemyVoronoi = voronoiMap.Count(v => v == 2);
            return myVoronoi - enemyVoronoi;
        }

        //int GetNextTarget(LuxLogicState luxLogicState, List<int> mySeeds, List<int> enemySeeds)
        //{
        //    var bestValue = int.MinValue;
        //    var bestPos = -1;
        //    var currentVoronoiValue = CalcVoronoiValue(luxLogicState, mySeeds, enemySeeds);
        //    for (var i = 0; i < LuxLogicState.Size2; i++)
        //    {
        //        if (mySeeds.Any(s => s == i)) continue;
        //        if (mySeeds.All(s => GetSapDistance(s, i) > luxLogicState.Env_cfg.unit_sap_range)) continue;
        //        if (mySeeds.Count >= 2 && mySeeds.Count(s => GetSapDistance(s, i) <= luxLogicState.Env_cfg.unit_sap_range) < 2) continue;
        //        if ((luxLogicState.Map[i] & MapBit.AsteroidBit) == MapBit.AsteroidBit) continue;
        //        if (luxLogicState.ScoreNodes.Any(s => s == i)) continue;
        //        var mySeedsCopy = new List<int>(mySeeds);
        //        var enemySeedsCopy = new List<int>(enemySeeds);
        //        mySeedsCopy.Add(i);
        //        var value = CalcVoronoiValue(luxLogicState, mySeedsCopy, enemySeedsCopy) - currentVoronoiValue;
        //        if (value < 0) continue;
        //        value += GetSumEnergyModifier(i, luxLogicState);
        //        value += mySeeds.Count(s => GetSapDistance(s, i) > luxLogicState.Env_cfg.unit_sap_range) * 2;
        //        if (value > bestValue)
        //        {
        //            bestValue = value;
        //            bestPos = i;
        //        }
        //    }
        //    return bestPos;
        //}

        //List<int> GetTargets(LuxLogicState luxLogicState, int count, List<int> mySeeds, List<int> enemySeeds, List<int> alreadyTargeted)
        //{
        //    var targets = new List<int>();
        //    while (targets.Count(t=> !alreadyTargeted.Contains(t))<count)
        //    {
        //        var target = GetNextTarget(luxLogicState, mySeeds, enemySeeds);
        //        targets.Add(target);
        //        mySeeds.Add(target);
        //    }
        //    return targets.OrderByDescending(t => LuxLogicState.MapDistances[t][MyBasePos]).ToList();
        //}


        int GetNextTarget(LuxLogicState luxLogicState, List<int> mySeeds)
        {
            var bestValue = double.MinValue;
            var bestPos = -1;
            int[] notOccupiedMap = new int[LuxLogicState.Size2];
            for (var i = 0; i < LuxLogicState.Size2; i++)
            {
                notOccupiedMap[i] = 1;
            }
            foreach (var seed in mySeeds)
            {
                SetNotOccupied(luxLogicState, seed, notOccupiedMap);
            }

            for (var i = 0; i < LuxLogicState.Size2; i++)
            {
                if (mySeeds.Any(s => s == i)) continue;
                if (mySeeds.All(s => GetSapDistance(s, i) > MaxTargetDistance)) continue;
                if (mySeeds.Count >= 2 && mySeeds.Count(s => GetSapDistance(s, i) <= MaxTargetDistance) < 2) continue;
                if (mySeeds.Count >= 3 && mySeeds.Count(s => GetSapDistance(s, i) <= MaxTargetDistance) < 3) continue;
                if ((luxLogicState.Map[i] & MapBit.AsteroidBit) == MapBit.AsteroidBit) continue;
                var value = GetTargetValue(luxLogicState, notOccupiedMap, i);
                value = value*(Math.Max(0, GetSumEnergyModifier(i, luxLogicState)+5)) / 40;
                if (value > bestValue)
                {
                    bestValue = value;
                    bestPos = i;
                }
            }
            return bestPos;
        }

        List<int> GetTargets(LuxLogicState luxLogicState, int count, List<int> mySeeds, List<int> alreadyTargeted)
        {
            var targets = new List<int>();
            var currentSeeds = new List<int>(mySeeds);
            while (targets.Count(t => !alreadyTargeted.Contains(t)) < count)
            {
                var target = GetNextTarget(luxLogicState, currentSeeds);
                targets.Add(target);
                currentSeeds.Add(target);
            }
            return targets.Union(alreadyTargeted).OrderByDescending(t => LuxLogicState.MapDistances[t][MyBasePos]).ToList();
        }


        double GetTargetValue(LuxLogicState luxLogicState, int[] notOccuipiedMap, int seed)
        {
            double sum = 0;
            var (x, y) = LuxLogicState.GetXY(seed);
            for (var dx = -TargetRadius; dx <= TargetRadius; dx++)
            {
                for (var dy = -TargetRadius; dy <= TargetRadius; dy++)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    if (nx < 0 || nx >= luxLogicState.Env_cfg.map_width) continue;
                    if (ny < 0 || ny >= luxLogicState.Env_cfg.map_height) continue;
                    var newPos = LuxLogicState.GetPos(nx, ny);
                    sum += notOccuipiedMap[newPos] * ValueMap[newPos];
                }
            }
            return sum;
        }

        void SetNotOccupied(LuxLogicState luxLogixState, int seed, int[] notOccupiedMap)
        {
            var (x,y) = LuxLogicState.GetXY(seed);
            for(var dx = -TargetRadius; dx <= TargetRadius; dx++)
            {
                for (var dy = -TargetRadius; dy <= TargetRadius; dy++)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    if (nx < 0 || nx >= luxLogixState.Env_cfg.map_width) continue;
                    if (ny < 0 || ny >= luxLogixState.Env_cfg.map_height) continue;
                    var newPos = LuxLogicState.GetPos(nx, ny);
                    notOccupiedMap[newPos]=0;
                }
            }
        }



        void SetValueMap(LuxLogicState luxLogicState)
        {
            var valueMap = new int[LuxLogicState.Size2];
            var enemySeedScores = luxLogicState.ScoreNodes.Where(s => LuxLogicState.MapDistances[MyBasePos][s] > 15).ToList();
            enemySeedScores.Add(EnemyBasePos);
            for(var i = 0; i < LuxLogicState.Size2; i++)
            {
//                valueMap[i] = enemySeedScores.Min(s => 25-GetSapDistance(i,s));
                valueMap[i] = enemySeedScores.Min(s => 50 - LuxLogicState.MapDistances[i][s]);
            }
            ValueMap = valueMap;
        }

        private bool GoToVoronoi(Ship ship, LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            var target = VoronoiTargets.FirstOrDefault(t => Targeted[t] == 0);
            if (target == 0) return false;
            var (dist, parent, energyMap, routeDistances) = GetDijsktraResult(luxLogicState, ship);
            if (ship.Pos != target && parent[target] == null) return false;
            if (myShips.Any(s => !s.IsMoved && GetDijsktraResult(luxLogicState, s).ToTuple().Item1[target] < dist[target] &&
                           (s.Pos == target || GetDijsktraResult(luxLogicState, s).ToTuple().Item2[target] != null))) return false;

            Targeted[target]++;
            var route = Dijkstra.GetRoute(parent, target);
            UpdateTiles(luxLogicState, route);
            TargetedAura[target]++;
            var nextPos = Dijkstra.GetNextPos(parent, target);
            var direction = GetDirection(ship.Pos, nextPos);
            Steps.Add(new Step() { MoveType = direction, UnitId = ship.ID });
            return true;
        }


        public List<Step> Run(LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            NNoutput = NNHandler.GetNNOutput(luxLogicState);
            //MaxTargetDistance = luxLogicState.Env_cfg.unit_sap_range;
            //TargetRadius = luxLogicState.Env_cfg.unit_sensor_range;
            MaxTargetDistance = 6;
            TargetRadius = 6;

            //TargetRadius = luxLogicState.Env_cfg.unit_sap_range;
            SetDangerMap(luxLogicState, enemyShips);
            AssingTargets(luxLogicState, myShips, enemyShips);
            var myBasePos = luxLogicState.PlayerId == 0 ? 0 : 575;
            MyBasePos = myBasePos;
            var enemyBasePos = luxLogicState.PlayerId == 0 ? 575 : 0;
            EnemyBasePos = enemyBasePos;
            SetValueMap(luxLogicState);
            var (dist, parent, energyMap, routeDistances) = Dijkstra.ShortestPaths(new sbyte[LuxLogicState.Size2], luxLogicState.Tiles, enemyBasePos, 400, luxLogicState.StartStep, 1, 0.09, 0.01, 0, 0, 0,
                            luxLogicState.Env_cfg.unit_move_cost, luxLogicState.NebulaEnergyModified ?? 0, luxLogicState.EnemyShips, luxLogicState.ScoreNodes);
            RouteDistancesToEnemyBase = routeDistances;

            (dist, parent, energyMap, routeDistances) = Dijkstra.ShortestPaths(new sbyte[LuxLogicState.Size2], luxLogicState.Tiles, myBasePos, 400, luxLogicState.StartStep, 1, 0.09, 0.01, 0, 0, 0,
                luxLogicState.Env_cfg.unit_move_cost, luxLogicState.NebulaEnergyModified ?? 0, luxLogicState.EnemyShips, luxLogicState.ScoreNodes);
            RouteDistancesToMyBase = routeDistances;

            for (var i = 0; i < LuxLogicState.Size2; i++)
            {
                var (x, y) = LuxLogicState.GetXY(i);
                if (!RouteDistancesToEnemyBase[i].HasValue)
                {
                    RouteDistancesToEnemyBase[i] = luxLogicState.PlayerId == 0 ? (23 + 23 - (x + y)) : x + y;
                }
            }
            var myScores = luxLogicState.ScoreNodes.Where(s => IsUpperPos(s)).ToList();

            luxLogicState.LastSapPositions.Clear();
            Targeted = new int[LuxLogicState.Size2];
            ExchangeTargeted = new int[LuxLogicState.Size2];
            SapTargeted = new int[LuxLogicState.Size2];
            TargetedAura = new int[LuxLogicState.Size2];
            SapValues = new int[LuxLogicState.Size2];
            IsWentToCandidate = false;
            var minEnergyNode = 1 - luxLogicState.Env_cfg.unit_move_cost;
            var shipAction = (Func<Ship, LuxLogicState, List<Ship>, List<Ship>, bool> innerAction) =>
            {
                var hadMove = true;
                while (hadMove)
                {
                    hadMove = false;
                    foreach (var ship in myShips)
                    {
                        if (ship.IsMoved) continue;
                        if (innerAction(ship, luxLogicState, myShips, enemyShips))
                        {
                            ship.IsMoved = true;
                            hadMove = true;
                        }
                    }
                }
            };
            var shipPendingAction = (Func<Ship, LuxLogicState, List<Ship>, List<Ship>, bool> innerAction) =>
            {
                var hadMove = true;
                while (hadMove)
                {
                    hadMove = false;
                    foreach (var ship in myShips)
                    {
                        if (innerAction(ship, luxLogicState, myShips, enemyShips))
                        {
                            ship.IsMoved = true;
                            hadMove = true;
                        }
                    }
                }
            };

            //myShips = myShips.OrderBy(s => routeDistances[s.Pos] ?? 999).ThenByDescending(s => luxLogicState.PlayerId == 0 ? (s.X + s.Y) : -(s.X + s.Y)).ThenBy(s => s.Energy).ToList();
            //shipAction(GoToAsssasin);
            //myShips = myShips.OrderByDescending(s => s.Energy).ToList();
            //shipAction(SapCrowd);
            //shipAction(SapKill);
            //myShips = myShips.OrderBy(s=> routeDistances[s.Pos]??999).ThenByDescending(s => luxLogicState.PlayerId == 0 ? (s.X + s.Y) : -(s.X + s.Y)).ThenBy(s => s.Energy).ToList();
            //shipAction((ship, luxLogicState, myShips, enemyShips) => GoToScore(ship, luxLogicState, myShips, enemyShips, true, minEnergyNode));
            //shipAction((ship, luxLogicState, myShips, enemyShips) => GoToScore(ship, luxLogicState, myShips, enemyShips, true, -100));
            //shipAction(GoToCandidate);
            //shipAction(DiscoverRelic);
            //myShips = myShips.OrderBy(s => routeDistances[s.Pos] ?? 999).ThenByDescending(s => luxLogicState.PlayerId == 0 ? (s.X + s.Y) : -(s.X + s.Y)).ThenByDescending(s => s.Energy).ToList();
            //shipAction((ship, luxLogicState, myShips, enemyShips) => GoToScore(ship, luxLogicState, myShips, enemyShips, false, minEnergyNode));
            //myShips = myShips.OrderBy(s => routeDistances[s.Pos] ?? 999).ThenByDescending(s => luxLogicState.PlayerId == 0 ? (s.X + s.Y) : -(s.X + s.Y)).ThenBy(s => s.Energy).ToList();
            //shipAction((ship, luxLogicState, myShips, enemyShips) => GoToScore(ship, luxLogicState, myShips, enemyShips, true, -10));
            //shipAction(Discover);
            //shipAction(GoToMaxEnergy);

     //       shipAction(Escape);
     //       //shipAction(ChaseEnemy);
     //       myShips = myShips.OrderByDescending(s => luxLogicState.PlayerId == 0 ? (s.X + s.Y) : -(s.X + s.Y)).ThenBy(s => s.Energy).ToList();
     ////    shipAction((ship, luxLogicState, myShips, enemyShips) => GoToAsssasin(ship, luxLogicState, myShips, enemyShips, 380));
     //       myShips = myShips.OrderBy(s => enemyShips.Any(es=>GetSapDistance(es.Pos,s.Pos)<=luxLogicState.Env_cfg.unit_sensor_range)).ThenByDescending(s => s.Energy).ToList();
     //       shipAction(SapCrowd);
     //       shipAction(MoveToEnemy);
     //       shipAction(SapKill);
     //       shipAction(SapSolo);
            //shipAction(MovePredator);
            //shipAction(MovePredatorAfterKillStep);
            //if (luxLogicState.StartStep % 101 < 50)
            //{
            //    shipAction(GoToSniperPos);
            //}
            myShips = myShips.OrderByDescending(s => luxLogicState.PlayerId == 0 ? (s.X + s.Y) : -(s.X + s.Y)).ThenBy(s => s.Energy).ToList();
            //shipAction(WaitForTime);
            if (luxLogicState.ScoreNodes.Any() && GetDistStd(luxLogicState.ScoreNodes) < 7)
            {
                shipAction(GoToNN);
                if (luxLogicState.StartStep % 101 < 40)
                {
                    shipAction(GoToNN);
//                    shipAction((ship, luxLogicState, myShips, enemyShips) => GoToSniperPos(ship, luxLogicState, myShips, enemyShips, false));
                    //shipAction(GoToSniperPos);
                }
                shipAction((ship, luxLogicState, myShips, enemyShips) => GoToScore(ship, luxLogicState, myShips, enemyShips, true, minEnergyNode));
                SamePosWeight = 1;
                ClearDijsktraResults(luxLogicState);
                shipAction((ship, luxLogicState, myShips, enemyShips) => GoToScore(ship, luxLogicState, myShips, enemyShips, false, minEnergyNode));
                SamePosWeight = 0;
                ClearDijsktraResults(luxLogicState);
            }
            shipAction((ship, luxLogicState, myShips, enemyShips) => GoToScore(ship, luxLogicState, myShips, enemyShips, true, -100));
            shipAction(GoToCandidate);
            shipAction(DiscoverRelic);
            shipAction(GoToNN);

            //  shipAction(GoToEnemy);
            //            shipAction((ship, luxLogicState, myShips, enemyShips) => GoToScore(ship, luxLogicState, myShips, enemyShips, false, minEnergyNode));
            myShips = myShips.OrderByDescending(s => luxLogicState.PlayerId == 0 ? (s.X + s.Y) : -(s.X + s.Y)).ThenByDescending(s => s.Energy).ToList();
            SamePosWeight = 1;
            ClearDijsktraResults(luxLogicState);

            var mySeedScores = luxLogicState.ScoreNodes.Where(s => LuxLogicState.MapDistances[MyBasePos][s]<=15).ToList();
            mySeedScores.Add(MyBasePos);
            //var enemySeedScores = luxLogicState.ScoreNodes.Where(s => LuxLogicState.MapDistances[MyBasePos][s] > 15).ToList();
            //enemySeedScores.Add(EnemyBasePos);
            //enemySeedScores.Add(LuxLogicState.GetPos(0, 23));
            //enemySeedScores.Add(LuxLogicState.GetPos(23, 0));
            var allTargets = GetTargets(luxLogicState, 30, mySeedScores, new List<int>());
            var occupiedTargets = myShips.Where(s => allTargets.Contains(s.Pos)).Select(s => s.Pos).Distinct().ToList();
            var targetCount = myShips.Count(s => !s.IsMoved && !occupiedTargets.Contains(s.Pos));

            //if (luxLogicState.ScoreNodes?.Count > 0)
            //{
            //    VoronoiTargets = GetTargets(luxLogicState, targetCount, mySeedScores, occupiedTargets);
            //    shipAction(GoToVoronoi);
            //}
            shipAction(GoToNN);
//            shipAction((ship, luxLogicState, myShips, enemyShips) => GoToSniperPos(ship, luxLogicState, myShips, enemyShips, false));
            shipAction((ship, luxLogicState, myShips, enemyShips) => GoToScore(ship, luxLogicState, myShips, enemyShips, false, -100));
            SamePosWeight = 0;
            ClearDijsktraResults(luxLogicState);
            //myShips = myShips.OrderByDescending(s => luxLogicState.PlayerId == 0 ? (s.X + s.Y) : -(s.X + s.Y)).ThenBy(s => s.Energy).ToList();
            //shipAction((ship, luxLogicState, myShips, enemyShips) => GoToScore(ship, luxLogicState, myShips, enemyShips, false, -100));
            if (luxLogicState.LuxInput.obs.relic_nodes.Count() > luxLogicState.RelicList.Count)
            {
                shipAction(DiscoverDraft);
                shipAction(Discover);
            }
            myShips = myShips.OrderByDescending(s => s.ID).ToList();
            SamePosWeight = 1;
            ClearDijsktraResults(luxLogicState);
            shipAction((ship, luxLogicState, myShips, enemyShips) => GoToAsssasin(ship, luxLogicState, myShips, enemyShips, 100));
            SamePosWeight = 0;
            ClearDijsktraResults(luxLogicState);
            shipAction(GoToMaxEnergy);
            shipPendingAction(MovePendings);
            shipPendingAction(StayPendings);
            return Steps;
        }

        double GetDistStd(List<int> nodes)
        {
            var avgX = nodes.Average(n => LuxLogicState.GetXY(n).Item1);
            var avgY = nodes.Average(n => LuxLogicState.GetXY(n).Item2);
            var dist = nodes.Select(n => Math.Abs(LuxLogicState.GetXY(n).Item1 - avgX) + Math.Abs(LuxLogicState.GetXY(n).Item2 - avgY)).ToList();
            return dist.Average();
        }

        //Ship GetBestShip(LuxLogicState luxLogicState)
        //{
        //    var notMovedShips = luxLogicState.MyShips.Where(s => !s.IsMoved).ToList();
        //    if (notMovedShips.Count == 0) return null;
        //    return notMovedShips.First();
        //    //            return notMovedShips.OrderBy(s=>GetClosestUntargetedScorePosDist(s,luxLogicState)).First();
        //}

        //int GetClosestUntargetedScorePosDist(Ship ship, LuxLogicState luxLogicState)
        //{
        //    var scorePosList = luxLogicState.ScoreNodes.Where(s => Targeted[s] == 0).ToList();
        //    if (scorePosList.Count == 0) return int.MaxValue;

        //    return scorePosList.Min(s => LuxLogicState.MapDistances[s][ship.Pos]);
        //}

        public static int GetSapDistance(int pos1, int pos2)
        {
            var (x1, y1) = LuxLogicState.GetXY(pos1);
            var (x2, y2) = LuxLogicState.GetXY(pos2);
            return Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));
        }


        void PrintAsteroinds(string filename, LuxLogicState luxLogicState)
        {
            var sb = new StringBuilder();
            for (var x = 0; x < 24; x++)
            {
                for (var y = 0; y < 24; y++)
                {
                    var pos = LuxLogicState.GetPos(x, y);
                    if ((luxLogicState.Map[pos] & MapBit.AsteroidBit) == MapBit.AsteroidBit)
                    {
                        sb.Append("1");
                    }
                    else
                    {
                        sb.Append("0");
                    }
                }
                sb.AppendLine();
            }
            System.IO.File.WriteAllText(filename, sb.ToString());
        }

        void PrintNebula(string filename, LuxLogicState luxLogicState)
        {
            var sb = new StringBuilder();
            for (var x = 0; x < 24; x++)
            {
                for (var y = 0; y < 24; y++)
                {
                    var pos = LuxLogicState.GetPos(x, y);
                    if ((luxLogicState.Map[pos] & MapBit.NebulaBit) == MapBit.NebulaBit)
                    {
                        sb.Append("1");
                    }
                    else
                    {
                        sb.Append("0");
                    }
                }
                sb.AppendLine();
            }
            System.IO.File.WriteAllText(filename, sb.ToString());
        }

        void PrintParents(string fileName, int?[] parents)
        {
            var sb = new StringBuilder();
            for (var x = 0; x < 24; x++)
            {
                for (var y = 0; y < 24; y++)
                {
                    var pos = LuxLogicState.GetPos(x, y);
                    if (parents[pos].HasValue)
                    {
                        sb.Append("1");
                    }
                    else
                    {
                        sb.Append("0");
                    }
                }
                sb.AppendLine();
            }
            System.IO.File.WriteAllText(fileName, sb.ToString());
        }
    }
}
