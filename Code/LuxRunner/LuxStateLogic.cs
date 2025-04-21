using MessagePack;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LuxRunner
{
    public class MapBit
    {
        public const byte AsteroidBit = 2;
        public const byte NebulaBit = 4;
        public const byte NotScoreBit = 8;
        public const byte EnemyStayed = 16;
        public const byte EnemyMoved = 32;
        public const byte Visible = 64;
        public const byte PseudoNebulaBit = 128;
    }

    [MessagePackObject]
    public class Tile
    {
        [Key(1)]
        public int Energy { get; set; }

        [Key(2)]
        public int X { get; set; }

        [Key(3)]
        public int Y { get; set; }
        [Key(4)]
        public int Pos { get; set; }
        [Key(5)]
        public int TimePos { get; set; }

        [Key(6)]
        public int Time { get; set; }

        [Key(7)]
        public bool IsNebula { get; set; }

        [Key(8)]
        public bool IsAsteroid { get; set; }
        [Key(9)]
        public bool IsRelic { get; set; }
        [Key(10)]
        public bool IsVisible { get; set; }
        [Key(11)]
        public bool IsScore { get; set; }
        [Key(12)]
        public bool ScoreCandidate { get; set; }
        [Key(13)]
        public int MyUnitCount { get; set; }
        [Key(14)]
        public int MyUnitNeighbourCount { get; set; }
        [Key(15)]
        public int MyUnitNeighbourNeighbourCount { get; set; }


        [Key(16)]
        public List<int> Neighbours { get; set; }
        [Key(17)]
        public List<int> SapNeighbours { get; set; }
        [Key(18)]
        public List<int> SapNeighboursNeighbours { get; set; }

        public Tile()
        {
            Neighbours = new List<int>();
            SapNeighbours = new List<int>();
            SapNeighboursNeighbours = new List<int>();
        }
    }

    [MessagePackObject]
    public class Ship
    {
        [Key(0)]
        public int Pos { get; set; }

        [Key(1)]
        public int X { get; set; }

        [Key(2)]
        public int Y { get; set; }

        [Key(3)]
        public int Energy { get; set; }

        [Key(4)]
        public int ID { get; set; }

        [IgnoreMember]
        public int Owner { get; set; }
        [IgnoreMember]
        public bool IsMoved { get; set; }

        [IgnoreMember]
        public (double[], int?[], int[] energyMap, int?[] routeDistances) DijsktraResult { get; set; }

        [IgnoreMember]
        public bool HasDijsktraResult { get; set; }

        [IgnoreMember]
        public bool IsPending { get; set; }
        [IgnoreMember]
        public bool IsRoutePending { get; set; }
        [IgnoreMember]
        public int? PendingTarget { get; set; }
        [IgnoreMember]
        public int? PredictedPos { get; set; }
        [IgnoreMember]
        public int PredictedSap { get; set; }
        [IgnoreMember]
        public int? TargetEnemyId { get; set; }
        [IgnoreMember]
        public bool Targeted { get; set; }
        [IgnoreMember]
        public bool IsTank { get; set; }
    }

    [MessagePackObject]
    public class CandidateGroup
    {
        [Key(0)]
        public List<int> CandidatePos { get; set; }
        [Key(1)]
        public int Points { get; set; }
        [IgnoreMember]
        public List<(int, int)> CandidateXY => CandidatePos.Select(p => LuxLogicState.GetXY(p)).ToList();

        public CandidateGroup()
        {
            CandidatePos = new List<int>();
        }
    }

    [MessagePackObject]
    public class LuxLogicState
    {
        [IgnoreMember]
        public static int Size = 24;

        [IgnoreMember]
        public static int Size2 = 24 * 24;

        [IgnoreMember]
        public static int[][] MapDistances;


        [Key(0)]
        public double? ObjectRoundPerMove { get; set; }

        [Key(1)]
        public int? EnergyRoundPerMove { get; set; }

        [Key(2)]
        public int ObjectDirection { get; set; }

        [Key(3)]
        public int StartStep { get; set; }

        //0:discovered
        //1:relic
        //2:score
        //3:score candidate
        //4:asteroid
        //5:nebula
        //6:unit moved

        [Key(4)]
        public byte[] Map { get; set; }


        //127: Not visible
        [Key(5)]
        public sbyte[] EnergyMap { get; set; }
        [Key(6)]
        public int[] DiscoveredMap { get; set; }

        [Key(8)]
        public List<Ship> MyShips { get; set; }

        [Key(9)]
        public List<Ship> EnemyShips { get; set; }
        [Key(10)]
        public List<Ship> LastEnemyShips { get; set; } = new List<Ship>();
        [Key(11)]
        public List<Ship> PreviousEnemyShips { get; set; } = new List<Ship>();

        [Key(12)]
        public int MyScore { get; set; }
        [Key(23)]
        public int EnemyScore { get; set; }

        [Key(13)]
        public List<int> RelicList { get; set; } = new List<int>();
        [Key(14)]
        public List<int> NewRelicList { get; set; } = new List<int>();
        [Key(15)]
        public List<int> ScoreNodes { get; set; } = new List<int>();
        [Key(16)]
        public List<CandidateGroup> CandidateGroupList { get; set; } = new List<CandidateGroup>();
        [Key(17)]
        public int? NebulaEnergyModified { get; set; }
        [Key(18)]
        public Env_Cfg Env_cfg { get; set; }
        [Key(19)]
        public double? SapDropFactor { get; set; } = null;
        [Key(20)]
        public double? EnergyVoidFactor { get; set; } = null;
        [Key(21)]
        public List<int> LastSapPositions { get; set; } = new List<int>();
        [Key(22)]
        public bool IsRelicFound { get; set; } = false;

        [IgnoreMember]
        public List<(int, int)> RelicXYList => RelicList.Select(p => GetXY(p)).ToList();
        [IgnoreMember]
        public List<(int, int)> ScoreXYList => ScoreNodes.Select(p => GetXY(p)).ToList();
        [IgnoreMember]
        public List<int> PredictedEnemyPos { get; set; } = new List<int>();
        [IgnoreMember]
        public Tile[] Tiles { get; set; }

        [IgnoreMember]
        public int PlayerId { get; set; }

        [IgnoreMember]
        public int EnemyId { get; set; }

        [IgnoreMember]
        public LuxState LuxInput { get; set; }


        public LuxLogicState()
        {
            Map = new byte[Size2];
            EnergyMap = new sbyte[Size2];
            DiscoveredMap = new int[Size2];
            Array.Fill(EnergyMap, (sbyte)127);
        }

        static LuxLogicState()
        {
            MapDistances = new int[Size2][];


            for (int x = 0; x < Size; x++)
            {
                for (int y = 0; y < Size; y++)
                {
                    var pos = GetPos(x, y);
                    MapDistances[pos] = new int[Size2];
                    for (int x2 = 0; x2 < Size; x2++)
                    {
                        for (int y2 = 0; y2 < Size; y2++)
                        {
                            var pos2 = GetPos(x2, y2);
                            MapDistances[pos][pos2] = Math.Abs(x - x2) + Math.Abs(y - y2);
                        }
                    }
                }
            }
        }

        public static int GetSapDistance(int pos1, int pos2)
        {
            var (x1, y1) = LuxLogicState.GetXY(pos1);
            var (x2, y2) = LuxLogicState.GetXY(pos2);
            return Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));
        }

        public void Update(LuxState state)
        {
            PreviousEnemyShips = EnemyShips;
            for (var i = 0; i < Size2; i++)
            {
                DiscoveredMap[i] = (int)Math.Max(0, DiscoveredMap[i] - 1);
            }

            LuxInput = state;
            if (state.obs.steps == 0)
            {
                Env_cfg = state.info.env_cfg;
            }
            StartStep = state.obs.steps;
            if (StartStep % 101 == 0 && StartStep < 300)
            {
                NewRelicList.Clear();
                IsRelicFound = false;
                for (var i = 0; i < Size2; i++)
                {
                    DiscoveredMap[i] = 0;
                    if ((Map[i] & MapBit.NotScoreBit) == MapBit.NotScoreBit)
                    {
                        Map[i] -= MapBit.NotScoreBit;
                    }
                }
            }
            PlayerId = state.player == "player_0" ? 0 : 1;
            EnemyId = PlayerId == 1 ? 0 : 1;
            UpdateEnergyMap(state);

            var myShips = ExtractVisibleShips(state, PlayerId);

            if (!NebulaEnergyModified.HasValue)
            {
                foreach (var ship in myShips)
                {
                    if (ship.Energy == 0) continue;
                    var (x, y) = GetXY(ship.Pos);
                    if (((Map[ship.Pos] & MapBit.NebulaBit) == MapBit.NebulaBit || (Map[ship.Pos] & MapBit.PseudoNebulaBit) == MapBit.PseudoNebulaBit) && state.obs.map_features.tile_type[x][y] == TileType.Nebula)
                    {
                        var oldShip = MyShips.SingleOrDefault(s => s.ID == ship.ID && Math.Abs(s.X - ship.X) + Math.Abs(s.Y - ship.Y) <= 1);
                        if (oldShip != null)
                        {
                            var energyDif = ship.Energy - oldShip.Energy + Env_cfg.unit_move_cost - EnergyMap[ship.Pos];
                            NebulaEnergyModified = energyDif;
                        }

                    }
                }
            }

            var lastMyShips = MyShips;
            MyShips = myShips.Where(s => s.Energy >= 0).ToList();
            EnemyShips = ExtractVisibleShips(state, EnemyId);

            if (StartStep <= 1) return;
            UpdateMap(state, MyShips, LastEnemyShips, EnemyShips);

            DetectSapDropFactor(EnemyShips, LastEnemyShips, lastMyShips);
            LastEnemyShips = ExtractVisibleShips(state, EnemyId);
        }


        void DetectSapDropFactor(List<Ship> currentEnemyShips, List<Ship> lastEnemyShips, List<Ship> lastMyShips)
        {
            if (SapDropFactor.HasValue && EnergyVoidFactor.HasValue) return;
            foreach (var ship in currentEnemyShips)
            {
                if ((Map[ship.Pos] & MapBit.NebulaBit) == MapBit.NebulaBit) continue;
                var lastTurnShip = lastEnemyShips.SingleOrDefault(ls => ls.ID == ship.ID);
                if (lastTurnShip == null) continue;

                var currentEnergyWithoutSap = lastTurnShip.Energy;
                if (lastTurnShip.Pos != ship.Pos)
                    currentEnergyWithoutSap -= Env_cfg.unit_move_cost;

                currentEnergyWithoutSap -= LastSapPositions.Count(s => s == ship.Pos) * Env_cfg.unit_sap_cost;

                var numberOfNearSap = LastSapPositions.Count(s => s != ship.Pos && IsIn8x8(s, ship.Pos));

                var nearMyShips = MyShips.Where(s => IsIn8x4(s.Pos, ship.Pos)).ToList();
                var nearMyShipsEnergy = lastMyShips.Where(n => n.Energy > 0 && nearMyShips.Any(nm => nm.ID == n.ID)).Select(s => s.Energy).ToList();
                double auraDivider = Math.Max(1.0, currentEnemyShips.Count(s => s.Energy > 0 && s.Pos == ship.Pos));

                var sapTrials = new List<double> { 1, 0.5, 0.25 };
                if (SapDropFactor.HasValue)
                {
                    sapTrials = new List<double> { SapDropFactor.Value };
                }
                var auraTrials = new List<double> { 0.0625, 0.125, 0.25, 0.375 };
                if (EnergyVoidFactor.HasValue)
                {
                    auraTrials = new List<double> { EnergyVoidFactor.Value };
                }

                foreach (var auraTrial in auraTrials)
                {
                    foreach (var sapTrial in sapTrials)
                    {
                        var sapValue = sapTrial * Env_cfg.unit_sap_cost;
                        var auraValue = nearMyShipsEnergy.Sum(s => (int)(s * auraTrial / auraDivider));

                        var currentEnergyWithoutSapTrial = currentEnergyWithoutSap;
                        if (currentEnergyWithoutSapTrial >= 0)
                        {
                            currentEnergyWithoutSapTrial += EnergyMap[ship.Pos];
                        }

                        if (Math.Abs(currentEnergyWithoutSapTrial - (int)(sapValue * numberOfNearSap) - auraValue - ship.Energy) <= 1)
                        {
                            if (numberOfNearSap > 0)
                            {
                                if (SapDropFactor.HasValue && SapDropFactor != sapTrial)
                                {
                                    SapDropFactor = null;
                                }
                                else
                                {
                                    SapDropFactor = sapTrial;
                                }
                            }
                            if (nearMyShips.Count > 0 && auraValue > 3)
                            {
                                if (EnergyVoidFactor.HasValue && EnergyVoidFactor != auraTrial)
                                {
                                    EnergyVoidFactor = null;
                                }
                                else
                                {
                                    EnergyVoidFactor = auraTrial;
                                }
                            }
                        }

                        if (ship.Pos == lastTurnShip.Pos && Math.Abs(currentEnergyWithoutSapTrial - Env_cfg.unit_sap_cost - sapValue * numberOfNearSap - auraValue - ship.Energy) <= 1)
                        {
                            if (numberOfNearSap > 0)
                            {
                                if (SapDropFactor.HasValue && SapDropFactor != sapTrial)
                                {
                                    SapDropFactor = null;
                                }
                                else
                                {
                                    SapDropFactor = sapTrial;
                                }
                            }
                            if (nearMyShips.Count > 0 && auraValue > 3)
                            {
                                if (EnergyVoidFactor.HasValue && EnergyVoidFactor != auraTrial)
                                {
                                    EnergyVoidFactor = null;
                                }
                                else
                                {
                                    EnergyVoidFactor = auraTrial;
                                }
                            }
                        }
                    }
                }
            }
        }

        bool IsIn8x8(int pos, int pos2)
        {
            var (x, y) = GetXY(pos);
            var (x2, y2) = GetXY(pos2);
            return Math.Abs(x - x2) <= 1 && Math.Abs(y - y2) <= 1;
        }
        bool IsIn8x4(int pos, int pos2)
        {
            var (x, y) = GetXY(pos);
            var (x2, y2) = GetXY(pos2);
            return Math.Abs(x - x2) + Math.Abs(y - y2) == 1;
        }

        public void UpdateMap(LuxState state, List<Ship> myShips, List<Ship> lastEnemyShips, List<Ship> currentEnemyShips)
        {
            var (newMap, relicList) = CreateMap(state, lastEnemyShips, currentEnemyShips);

            bool astreoidMoveDetected = false;
            if (!ObjectRoundPerMove.HasValue)
            {
                if (state.step == 41 || state.step == 21 || state.step == 11 || state.step == 7)
                {
                    var negativeDirectiionDetected = IsAsteroidDirectionDetected(newMap, Map, true);
                    var positiveDirectiionDetected = IsAsteroidDirectionDetected(newMap, Map, false);

                    if (negativeDirectiionDetected && !positiveDirectiionDetected)
                    {
                        astreoidMoveDetected = true;
                        ObjectDirection = -1;
                        if (state.obs.steps > 1)
                        {
                            ObjectRoundPerMove = 1.0 / (state.obs.steps - 1);
                            if (state.step == 7)
                            {
                                ObjectRoundPerMove = 0.15;
                            }
                        }
                    }
                    else if (!negativeDirectiionDetected && positiveDirectiionDetected)
                    {
                        astreoidMoveDetected = true;
                        ObjectDirection = 1;
                        if (state.obs.steps > 1)
                        {
                            ObjectRoundPerMove = 1.0 / (state.obs.steps - 1);
                            if (state.step == 7)
                            {
                                ObjectRoundPerMove = 0.15;
                            }
                        }

                    }
                }
            }
            else
            {
                astreoidMoveDetected = ((state.obs.steps - 1) % (int)Math.Floor(1.0 / ObjectRoundPerMove.Value)) == 0;
            }
            if (astreoidMoveDetected)
            {
                MoveAsteroids(Map, ObjectDirection);
            }

            if (state.step >= 41 && !ObjectRoundPerMove.HasValue)
            {
                for (var i = 0; i < Map.Length; i++)
                {
                    if ((Map[i] & MapBit.AsteroidBit) == MapBit.AsteroidBit)
                    {
                        Map[i] -= MapBit.AsteroidBit;
                    }
                    if ((Map[i] & MapBit.NebulaBit) == MapBit.NebulaBit)
                    {
                        Map[i] -= MapBit.NebulaBit;
                    }
                }
            }

            for (var i = 0; i < Size2; i++)
            {
                if ((Map[i] & MapBit.Visible) == MapBit.Visible)
                {
                    Map[i] -= MapBit.Visible;
                }

                if ((Map[i] & MapBit.PseudoNebulaBit) == MapBit.PseudoNebulaBit)
                {
                    Map[i] -= MapBit.PseudoNebulaBit;
                }

                if ((Map[i] & MapBit.EnemyStayed) == MapBit.EnemyStayed)
                {
                    Map[i] -= MapBit.EnemyStayed;
                }
                if ((Map[i] & MapBit.EnemyMoved) == MapBit.EnemyMoved)
                {
                    Map[i] -= MapBit.EnemyMoved;
                }
            }

            Map = CreateMergedMap(newMap, Map);

            var newRelics = relicList.Except(RelicList).ToList();
            if (newRelics.Count > 0)
            {
                for (int i = 0; i < Size2; i++)
                {
                    if (ScoreNodes.Any(s => s == i)) continue;
                    Map[i] |= MapBit.NotScoreBit;
                }

                foreach (var relic in newRelics.Union(RelicList))
                {
                    if (!IsRelicFound)
                    {
                        for (int x = 0; x < Size; x++)
                        {
                            for (int y = 0; y < Size; y++)
                            {
                                var pos = GetPos(x, y);
                                DiscoveredMap[pos] = 0;
                                if (state.obs.map_features.tile_type[x][y] != TileType.Unknown)
                                {
                                    DiscoveredMap[pos] = 400;
                                }
                            }
                        }
                    }

                    IsRelicFound = true;


                    var (rx, ry) = GetXY(relic);
                    for (var dx = -2; dx <= 2; dx++)
                    {
                        for (var dy = -2; dy <= 2; dy++)
                        {
                            var newX = rx + dx;
                            var newY = ry + dy;
                            if (newX >= 0 && newX < Size && newY >= 0 && newY < Size)
                            {
                                var newPos = GetPos(newX, newY);
                                if ((Map[newPos] & MapBit.NotScoreBit) == MapBit.NotScoreBit)
                                {
                                    Map[newPos] = (byte)(Map[newPos] - MapBit.NotScoreBit);
                                }
                            }
                        }
                    }
                }
                RelicList.AddRange(newRelics);
                NewRelicList.AddRange(newRelics);
            }
            if (IsRelicFound)
            {
                for (int p = 0; p < Size2; p++)
                {
                    if (isNotScore(Map, p))
                    {
                        Map[p] = (byte)(Map[p] | MapBit.NotScoreBit);
                        Map[GetSymetricPos(p)] = (byte)(Map[GetSymetricPos(p)] | MapBit.NotScoreBit);
                    }
                }
            }


            int expectedPoints = 0;
            var shipPosList = myShips.Select(s => s.Pos).Distinct().ToList();
            foreach (var shipPos in shipPosList)
            {
                foreach (var scoreNode in ScoreNodes)
                {
                    if (shipPos == scoreNode)
                    {
                        expectedPoints++;
                    }
                }
            }

            if (state.obs.team_points[PlayerId] == MyScore + expectedPoints)
            {
                foreach (var ship in myShips)
                {
                    var isScoreNode = false;
                    foreach (var scoreNode in ScoreNodes)
                    {
                        if (ship.Pos == scoreNode)
                        {
                            isScoreNode = true;
                            break;
                        }
                    }
                    if (!isScoreNode && IsRelicFound)
                    {
                        Map[ship.Pos] |= MapBit.NotScoreBit;
                        Map[GetSymetricPos(ship.Pos)] |= MapBit.NotScoreBit;
                    }
                }
            }


            if (state.obs.team_points[PlayerId] > MyScore + expectedPoints)
            {
                if (!IsRelicFound)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        for (int y = 0; y < Size; y++)
                        {
                            var pos = GetPos(x, y);
                            DiscoveredMap[pos] = 0;
                            if (state.obs.map_features.tile_type[x][y] != TileType.Unknown)
                            {
                                DiscoveredMap[pos] = 400;
                            }
                        }
                    }
                }

                IsRelicFound = true;
                var newCandidateGroup = new CandidateGroup();
                newCandidateGroup.Points = state.obs.team_points[PlayerId] - MyScore - expectedPoints;
                foreach (var ship in myShips)
                {
                    if ((Map[ship.Pos] & MapBit.NotScoreBit) != MapBit.NotScoreBit)
                    {
                        var isScoreNode = false;
                        foreach (var scoreNode in ScoreNodes)
                        {
                            if (ship.Pos == scoreNode)
                            {
                                isScoreNode = true;
                                break;
                            }
                        }
                        if (!isScoreNode)
                        {
                            newCandidateGroup.CandidatePos.Add(ship.Pos);
                        }
                    }
                }
                newCandidateGroup.CandidatePos = newCandidateGroup.CandidatePos.Distinct().ToList();
                CandidateGroupList.Add(newCandidateGroup);
            }
            MyScore = state.obs.team_points[PlayerId];
            var enemyScoreDif = state.obs.team_points[EnemyId] - EnemyScore;
            EnemyScore = state.obs.team_points[EnemyId];
            ProcessCandidates();

            var myBasePos = PlayerId == 0 ? 0 : 575;
            var enemyBasePos = PlayerId == 0 ? 575 : 0;
            var knownEnemyShipsOnScores = currentEnemyShips.Count(s => ScoreNodes.Contains(s.Pos));
            enemyScoreDif -= knownEnemyShipsOnScores;
            var enemyScores = ScoreNodes.Where(s => (Map[s] & MapBit.Visible) == 0)
                                .OrderBy(s => GetSapDistance(enemyBasePos, s)).ToList();

            PredictedEnemyPos = enemyScores.Take(enemyScoreDif).ToList();
        }


        void ProcessCandidates()
        {
            bool hasChanges = true;
            while (hasChanges)
            {
                hasChanges = false;
                foreach (var candidateGroup in CandidateGroupList)
                {
                    var newCandidateList = candidateGroup.CandidatePos.Where(cp => (Map[cp] & MapBit.NotScoreBit) != MapBit.NotScoreBit).ToList();
                    if (newCandidateList.Count != candidateGroup.CandidatePos.Count)
                    {
                        hasChanges = true;
                    }
                    candidateGroup.CandidatePos = newCandidateList;

                    newCandidateList = candidateGroup.CandidatePos.Where(cp => !ScoreNodes.Contains(cp)).ToList();
                    if (newCandidateList.Count != candidateGroup.CandidatePos.Count)
                    {
                        hasChanges = true;
                    }
                    candidateGroup.Points -= candidateGroup.CandidatePos.Count - newCandidateList.Count;
                    candidateGroup.CandidatePos = newCandidateList;

                    if (candidateGroup.Points == 0 && candidateGroup.CandidatePos.Count > 0)
                    {
                        hasChanges = true;
                        foreach (var candidatePos in candidateGroup.CandidatePos)
                        {
                            Map[candidatePos] |= MapBit.NotScoreBit;
                            Map[GetSymetricPos(candidatePos)] |= MapBit.NotScoreBit;
                        }
                        candidateGroup.CandidatePos.Clear();
                    }

                    if (candidateGroup.Points > 0 && candidateGroup.CandidatePos.Count == candidateGroup.Points)
                    {
                        hasChanges = true;
                        foreach (var candidatePos in candidateGroup.CandidatePos)
                        {
                            ScoreNodes.Add(candidatePos);
                            var symetricPos = GetSymetricPos(candidatePos);
                            if (symetricPos != candidatePos)
                            {
                                ScoreNodes.Add(GetSymetricPos(candidatePos));
                            }
                            ScoreNodes = ScoreNodes.Distinct().ToList();
                        }
                        candidateGroup.CandidatePos.Clear();
                    }
                }
                CandidateGroupList = CandidateGroupList.Where(cg => cg.CandidatePos.Count > 0).ToList();
            }
        }


        public bool isNotScore(byte[] map, int pos)
        {
            var (x, y) = GetXY(pos);
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    var newX = x + dx;
                    var newY = y + dy;
                    if (newX >= 0 && newX < Size && newY >= 0 && newY < Size)
                    {
                        var newPos = GetPos(newX, newY);
                        if (DiscoveredMap[newPos] <= 0)
                        {
                            return false;
                        }
                        foreach (var relicPos in RelicList)
                        {
                            if (relicPos == newPos)
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            return true;
        }

        public void UpdateEnergyMap(LuxState state)
        {
            var newEnergyMap = CreateEnergyMap(state);
            bool energyMoveDetected = false;
            if (!EnergyRoundPerMove.HasValue)
            {
                if (IsEnergyChangeDetected(newEnergyMap, EnergyMap))
                {
                    energyMoveDetected = true;
                    if (state.obs.steps > 2)
                    {
                        EnergyRoundPerMove = state.obs.steps - 2;
                    }
                }
            }
            else
            {
                energyMoveDetected = ((state.obs.steps - 2) % EnergyRoundPerMove) == 0;
            }
            if (energyMoveDetected)
            {
                EnergyMap = new sbyte[Size2];
                Array.Fill(EnergyMap, (sbyte)127);
            }
            EnergyMap = CreateMergedEnergyMap(newEnergyMap, EnergyMap);
        }


        void MoveAsteroids(byte[] map, int asteroidDirection)
        {
            var movedMap = new byte[Size2];
            var deltaX = asteroidDirection;
            var deltaY = -asteroidDirection;
            for (int p = 0; p < Size2; p++)
            {
                movedMap[p] = map[p];
                if ((movedMap[p] & MapBit.AsteroidBit) == MapBit.AsteroidBit)
                {
                    movedMap[p] -= MapBit.AsteroidBit;
                }
                if ((movedMap[p] & MapBit.NebulaBit) == MapBit.NebulaBit)
                {
                    movedMap[p] -= MapBit.NebulaBit;
                }
            }
            for (int p = 0; p < Size2; p++)
            {
                var (x, y) = GetXY(p);
                var newX = x + deltaX;
                var newY = y + deltaY;
                var newPos = GetPos(newX, newY);
                if ((map[p] & MapBit.AsteroidBit) == MapBit.AsteroidBit)
                {
                    if (newX < 0 || newX >= Size || newY < 0 || newY >= Size) continue;
                    movedMap[newPos] = (byte)(movedMap[newPos] | MapBit.AsteroidBit);
                }
                else if ((map[p] & MapBit.NebulaBit) == MapBit.NebulaBit)
                {
                    if (newX < 0 || newX >= Size || newY < 0 || newY >= Size) continue;
                    movedMap[newPos] = (byte)(movedMap[newPos] | MapBit.NebulaBit);
                }
            }
            for (int p = 0; p < Size2; p++)
            {
                map[p] = movedMap[p];
            }
        }

        byte[] CreateMergedMap(byte[] newMap, byte[] oldMap)
        {
            for (int p = 0; p < Size2; p++)
            {
                if ((newMap[p] & MapBit.Visible) == MapBit.Visible &&
                (((newMap[p] & MapBit.AsteroidBit) != MapBit.AsteroidBit && ((oldMap[p] & MapBit.AsteroidBit) == MapBit.AsteroidBit)) ||
                ((newMap[p] & MapBit.NebulaBit) != MapBit.NebulaBit && ((oldMap[p] & MapBit.NebulaBit) == MapBit.NebulaBit))))
                {
                    if (ObjectRoundPerMove.HasValue)
                    {
                        ObjectRoundPerMove = ObjectRoundPerMove.Value * 2;
                    }
                    if (ObjectRoundPerMove > 0.1)
                    {
                        ObjectRoundPerMove = null;
                    }
                    for (int i = 0; i < Size2; i++)
                    {
                        if ((oldMap[i] & MapBit.AsteroidBit) == MapBit.AsteroidBit)
                        {
                            oldMap[i] -= MapBit.AsteroidBit;
                        }
                        if ((oldMap[i] & MapBit.NebulaBit) == MapBit.NebulaBit)
                        {
                            oldMap[i] -= MapBit.NebulaBit;
                        }
                    }
                    break;
                }
            }


            var mergedMap = new byte[Size2];
            for (int p = 0; p < Size2; p++)
            {
                mergedMap[p] = newMap[p];
                mergedMap[p] = (byte)(mergedMap[p] |
                               (oldMap[p] & MapBit.AsteroidBit) |
                               (oldMap[p] & MapBit.NebulaBit) |
                               (oldMap[p] & MapBit.NotScoreBit) |
                               (oldMap[p] & MapBit.EnemyMoved) |
                               (oldMap[p] & MapBit.PseudoNebulaBit) |
                               (oldMap[p] & MapBit.EnemyStayed));
            }
            return mergedMap;
        }

        sbyte[] CreateMergedEnergyMap(sbyte[] newEnergyMap, sbyte[] oldEnergyMap)
        {
            var mergedEnergyMap = new sbyte[Size2];
            for (int p = 0; p < Size2; p++)
            {
                if (newEnergyMap[p] != 127)
                {
                    mergedEnergyMap[p] = newEnergyMap[p];
                }
                else if (oldEnergyMap[p] != 127)
                {
                    mergedEnergyMap[p] = oldEnergyMap[p];
                }
                else
                {
                    mergedEnergyMap[p] = 127;
                }
            }
            return mergedEnergyMap;

        }

        (byte[], List<int>) CreateMap(LuxState state, List<Ship> lastEnemyShips, List<Ship> currentEnemyShips)
        {
            var newMap = new byte[Size2];
            var relicList = new List<int>();
            for (int i = 0; i < state.obs.relic_nodes_mask.Length; i++)
            {
                if (state.obs.relic_nodes_mask[i])
                {
                    var pos = GetPos(state.obs.relic_nodes[i][0], state.obs.relic_nodes[i][1]);
                    relicList.Add(pos);
                    var symetricPos = GetSymetricPos(pos);
                    if (symetricPos != pos)
                    {
                        relicList.Add(GetSymetricPos(pos));
                    }
                }
            }
            relicList = relicList.Distinct().ToList();
            for (int x = 0; x < Size; x++)
            {
                for (int y = 0; y < Size; y++)
                {
                    var pos = GetPos(x, y);
                    var mapValue = 0;
                    if (state.obs.map_features.tile_type[x][y] == TileType.Asteroid)
                    {
                        mapValue |= MapBit.AsteroidBit;
                    }
                    else if (state.obs.map_features.tile_type[x][y] == TileType.Nebula)
                    {
                        mapValue |= MapBit.NebulaBit;
                    }
                    if (state.obs.map_features.tile_type[x][y] != TileType.Unknown)
                    {
                        DiscoveredMap[pos] = 15;
                        if (IsRelicFound)
                        {
                            DiscoveredMap[pos] = 400;
                        }

                        mapValue |= MapBit.Visible;
                    }
                    else
                    {
                        if (MyShips.Any(s => GetSapDistance(s.Pos, pos) <= Env_cfg.unit_sensor_range))
                        {
                            mapValue |= MapBit.PseudoNebulaBit;
                        }
                    }
                    newMap[pos] = (byte)mapValue;
                }
            }
            for (int p = 0; p < Size2; p++)
            {
                var (x, y) = GetXY(p);
                if (state.obs.map_features.tile_type[x][y] != TileType.Unknown)
                {
                    var symetricPos = GetSymetricPos(p);
                    var isVisible = (newMap[symetricPos] & MapBit.Visible) == MapBit.Visible;
                    newMap[GetSymetricPos(p)] = newMap[p];
                    if (!isVisible)
                    {
                        if ((newMap[symetricPos] & MapBit.Visible) == MapBit.Visible)
                        {
                            newMap[symetricPos] -= MapBit.Visible;
                        }
                    }
                }
            }

            foreach (var lastEnemyShip in lastEnemyShips)
            {
                var currentEnemyShip = currentEnemyShips.SingleOrDefault(c => c.ID == lastEnemyShip.ID);
                if (currentEnemyShip == null) continue;
                var (lx, ly) = GetXY(lastEnemyShip.Pos);
                if (state.obs.map_features.tile_type[lx][ly] != TileType.Unknown)
                {
                    if (currentEnemyShip.Pos == lastEnemyShip.Pos)
                    {
                        newMap[currentEnemyShip.Pos] = (byte)(newMap[currentEnemyShip.Pos] | MapBit.EnemyStayed);
                    }
                    else
                    {
                        newMap[currentEnemyShip.Pos] = (byte)(newMap[currentEnemyShip.Pos] | MapBit.EnemyMoved);
                    }
                }
            }

            return (newMap, relicList);
        }

        sbyte[] CreateEnergyMap(LuxState state)
        {
            var newEnergyMap = new sbyte[Size2];
            for (int x = 0; x < Size; x++)
            {
                for (int y = 0; y < Size; y++)
                {
                    var pos = GetPos(x, y);
                    if (state.obs.map_features.tile_type[x][y] != TileType.Unknown)
                    {
                        newEnergyMap[pos] = (sbyte)state.obs.map_features.energy[x][y];
                    }
                    else
                    {
                        newEnergyMap[pos] = 127;
                    }
                }
            }
            for (int p = 0; p < Size2; p++)
            {
                if (newEnergyMap[p] != 127)
                {
                    newEnergyMap[GetSymetricPos(p)] = newEnergyMap[p];
                }
            }
            return newEnergyMap;
        }


        bool IsEnergyChangeDetected(sbyte[] newEnergyMap, sbyte[] oldEnergyMap)
        {
            for (int p = 0; p < Size2; p++)
            {
                if (newEnergyMap[p] != 127 && oldEnergyMap[p] != 127 && newEnergyMap[p] != oldEnergyMap[p])
                {
                    return true;
                }
            }
            return false;
        }

        bool IsAsteroidDirectionDetected(byte[] newMap, byte[] oldMap, bool isNegative)
        {
            bool isObjectExamined = false;
            for (int p = 0; p < Size2; p++)
            {
                var (x, y) = GetXY(p);
                var oldX = x + (isNegative ? 1 : -1);
                var oldY = y + (isNegative ? -1 : 1);
                if (oldX < 0 || oldX >= Size || oldY < 0 || oldY >= Size) continue;
                var oldPos = GetPos(oldX, oldY);
                if ((oldMap[oldPos] & MapBit.Visible) == 0 || (newMap[p] & MapBit.Visible) == 0) continue;

                if ((newMap[p] & MapBit.AsteroidBit) != (oldMap[oldPos] & MapBit.AsteroidBit))
                {
                    return false;
                }
                if ((newMap[p] & MapBit.NebulaBit) != (oldMap[oldPos] & MapBit.NebulaBit))
                {
                    return false;
                }

                if ((newMap[p] & MapBit.AsteroidBit) == MapBit.AsteroidBit || (newMap[p] & MapBit.NebulaBit) == MapBit.NebulaBit)
                {
                    isObjectExamined = true;
                }
            }
            return isObjectExamined;
        }


        private List<Ship> ExtractVisibleShips(LuxState state, int pid)
        {
            List<Ship> ships = new List<Ship>();
            var mask = state.obs.units_mask[pid];
            var positions = state.obs.units.position[pid];
            var energy = state.obs.units.energy[pid];
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i])
                {
                    ships.Add(new Ship() { ID = i, Owner = pid, X = positions[i][0], Y = positions[i][1], Energy = energy[i], Pos = GetPos(positions[i][0], positions[i][1]) });
                }
            }
            return ships;
        }

        public static int GetTimePos(int time, int pos)
        {
            return time * Size2 + pos;
        }

        public static (int, int) GetTimeAndPos(int timePos)
        {
            return (timePos / Size2, timePos % Size2);
        }

        public static int GetPos(int x, int y)
        {
            return x * Size + y;
        }

        public static (int, int) GetXY(int pos)
        {
            return (pos / Size, pos % Size);
        }

        public static int GetSymetricPos(int pos)
        {
            var (x, y) = GetXY(pos);
            return GetPos(Size - 1 - y, Size - 1 - x);
        }
    }
}
