using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LuxRunner
{

    class Dijkstra
    {
        // 9 bit: Pos
        // 9 bit: Energy
        // 9 bit: Time


        static UInt32 CreateNode(int pos, int energy, int time)
        {
            var result = (UInt32)(pos << 18 | energy << 9 | time);
            return result;
        }

        static (int pos, int energy, int time) GetNode(UInt32 node)
        {
            return ((int)(node >> 18), (int)(node >> 9 & 0x1FF), (int)(node & 0x1FF));
        }


        public static List<int> GetRoute(int?[] parents, int targetPos)
        {
            var route = new List<int>();
            if (parents[targetPos] == null)
            {
                route.Add(targetPos);
                return route;
            }
            var pos = targetPos;
            while (parents[pos].HasValue)
            {
                route.Add(pos);
                pos = parents[pos].Value;
            }
            route.Add(pos);
            route.Reverse();
            return route;
        }

        public static int GetNextPos(int?[] parents, int targetPos)
        {
            var route = GetRoute(parents, targetPos);
            if (route.Count == 1) return targetPos;
            return route[1];
        }

        public static (double[], int?[], int[], int?[]) ShortestPaths(sbyte[] dangerMap, Tile[] tiles, int startPos, int startEnergy, int startTime,
                                                            double distanceWeight, double energyWeight, double nebulaWeight, double samePositionModifier, double sameNaighbourModifier, double sameNaighbourNeighbourModifier,
                                                            int moveEnergyCost, int nebulaEnergyModifier,
                                                            List<Ship> enemyShips,
                                                            List<int> scoreNodes)
        {
            var startNode = CreateNode(startPos, startEnergy, startTime);

            var distances = new double[LuxLogicState.Size2];
            var parents = new int?[LuxLogicState.Size2];
            var energyMap = new int[LuxLogicState.Size2];
            var routeDistances=new int?[LuxLogicState.Size2];
            energyMap[startPos] = startEnergy;
            routeDistances[startPos] = 0;
            Array.Fill(distances, double.MaxValue);
            var priorityQueue = new SortedSet<(double distance, uint vertex)>(Comparer<(double distance, uint vertex)>.Create((a, b) =>
            {
                int compare = a.distance.CompareTo(b.distance);
                return compare == 0 ? a.vertex.CompareTo(b.vertex) : compare;
            }));


            distances[(uint)startPos] = 0;
            priorityQueue.Add((0, startNode));

            while (priorityQueue.Count > 0)
            {
                var (currentDistance, currentVertex) = priorityQueue.Min;
                priorityQueue.Remove(priorityQueue.Min);
                (int pos, int energy, int time) = GetNode(currentVertex);
                var tile = tiles[LuxLogicState.GetTimePos(time,pos)];

                foreach (var neigbour in tile.Neighbours)
                {
                    var (ntime, npos) = LuxLogicState.GetTimeAndPos(neigbour);
                    if (npos==pos) continue;

                    var neighbourTile = tiles[neigbour];
                    if (neighbourTile.IsAsteroid) continue;
                    if (energy < moveEnergyCost) continue;
                    var energyCost = -neighbourTile.Energy + moveEnergyCost;
                    if (neighbourTile.IsNebula) energyCost -= nebulaEnergyModifier;
                    if (energy - energyCost < 0) continue;

                    var enemShipsOnPos=enemyShips.Where(x => LuxLogicState.MapDistances[x.Pos][npos] <= 1).ToList();
                    if (enemShipsOnPos.Count > 0)
                    {
                        var maxEnergy = enemShipsOnPos.Max(x => x.Energy);
                        if (maxEnergy > energy - energyCost) continue;
                    }

                    double distanceEdge = scoreNodes.Any(x => x == npos && neighbourTile.Energy > 0) ? 0 : distanceWeight;
                    var newDistance = currentDistance + distanceEdge + Math.Pow(Math.Abs(energyCost),1.01) * 0.09 * Math.Sign(energyCost);
                    
                    if (neighbourTile.IsNebula) newDistance += nebulaWeight;

                    if (dangerMap[npos] > 0)
                    {
                        newDistance += samePositionModifier * neighbourTile.MyUnitCount;
                        newDistance += sameNaighbourModifier * neighbourTile.MyUnitNeighbourCount;
                        newDistance += sameNaighbourNeighbourModifier * neighbourTile.MyUnitNeighbourNeighbourCount;
                    }

                    if (!neighbourTile.IsVisible && neighbourTile.Neighbours.Any(nn => tiles[nn].IsNebula))
                    {
                        newDistance += -nebulaEnergyModifier * energyWeight*3;
                    }

                    if (newDistance <= currentDistance)
                    {
                        newDistance = currentDistance + 1e-2;
                    }

                    var newEnergy = energy - energyCost;
                    var newTime = time + 1;
                    var newVertex = CreateNode(npos, newEnergy, newTime);

                    if (distances[(uint)npos] > newDistance)
                    {
                        distances[(uint)npos] = newDistance;
                        parents[npos] = pos;
                        energyMap[npos] = newEnergy;
                        routeDistances[npos] = routeDistances[pos] + 1;
                        priorityQueue.Add((newDistance, newVertex));
                    }
                }
            }

            return (distances, parents, energyMap, routeDistances);
        }

        public static (double[], int?[], int[]) ShortestPaths2(Tile[] tiles, int startPos, int startEnergy, int startTime,
                                                    double distanceWeight, double energyWeight, double nebulaWeight, double samePositionModifier, double sameNaighbourModifier,
                                                    int moveEnergyCost, int nebulaEnergyModifier)
        {
            var startNode = CreateNode(startPos, startEnergy, startTime);

            var distances = new double[LuxLogicState.Size2];
            var distancesTime = new double[LuxLogicState.Size2*505];
            var parents = new int?[LuxLogicState.Size2];
            var energyMap = new int[LuxLogicState.Size2];
            energyMap[startPos] = startEnergy;
            Array.Fill(distances, double.MaxValue);
            Array.Fill(distancesTime, double.MaxValue);
            var priorityQueue = new SortedSet<(double distance, uint vertex)>(Comparer<(double distance, uint vertex)>.Create((a, b) =>
            {
                int compare = a.distance.CompareTo(b.distance);
                return compare == 0 ? a.vertex.CompareTo(b.vertex) : compare;
            }));


            distances[(uint)startPos] = 0;
            distancesTime[LuxLogicState.GetTimePos(startTime, startPos)] = 0;
            priorityQueue.Add((0, startNode));

            while (priorityQueue.Count > 0)
            {
                var (currentDistance, currentVertex) = priorityQueue.Min;
                priorityQueue.Remove(priorityQueue.Min);
                (int pos, int energy, int time) = GetNode(currentVertex);
                var tile = tiles[LuxLogicState.GetTimePos(time, pos)];

                foreach (var neigbour in tile.Neighbours)
                {
                    var (ntime, npos) = LuxLogicState.GetTimeAndPos(neigbour);
                    if (npos == pos) continue;

                    var neighbourTile = tiles[neigbour];
                    if (neighbourTile.IsAsteroid) continue;
                    if (energy < moveEnergyCost) continue;
                    var energyCost = -neighbourTile.Energy + moveEnergyCost;
                    if (neighbourTile.IsNebula) energyCost -= nebulaEnergyModifier;
                    if (energy - energyCost < 0) continue;

                    var newDistance = currentDistance + distanceWeight + energyCost * energyWeight;
                    if (neighbourTile.IsNebula) newDistance += nebulaWeight;

                    newDistance += samePositionModifier * neighbourTile.MyUnitCount;
                    newDistance += sameNaighbourModifier * neighbourTile.MyUnitNeighbourCount;

                    var newEnergy = energy - energyCost;
                    var newTime = time + 1;
                    var newVertex = CreateNode(npos, newEnergy, newTime);

                    if (distancesTime[LuxLogicState.GetTimePos(ntime, npos)] > newDistance)
                    {
                        distances[(uint)npos] = newDistance;
                        distancesTime[LuxLogicState.GetTimePos(ntime, npos)] = newDistance;
                        parents[npos] = pos;
                        energyMap[npos] = newEnergy;
                        priorityQueue.Add((newDistance, newVertex));
                    }
                }
            }

            return (distances, parents, energyMap);
        }
    }
}
