using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LuxRunner
{
    public enum MoveType
    {
        Nothing,
        Up,
        Right,
        Down,
        Left,
        Sap
    }
    public class Step
    {
        public MoveType MoveType { get; set; }
        public int UnitId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class ProcessLogicNN
    {
        public int[] NextPosTarget { get; set; } = new int[LuxLogicState.Size2];
        public int[] SapMap { get; set; } = new int[LuxLogicState.Size2];
        public List<Step> Steps = new List<Step>();
        public int MyBasePos { get; set; }
        public int EnemyBasePos { get; set; }


        public List<Step> Run(LuxLogicState luxLogicState, List<Ship> myShips, List<Ship> enemyShips)
        {
            luxLogicState.LastSapPositions = new List<int>();
            var myBasePos = luxLogicState.PlayerId == 0 ? 0 : 575;
            MyBasePos = myBasePos;
            var enemyBasePos = luxLogicState.PlayerId == 0 ? 575 : 0;
            EnemyBasePos = enemyBasePos;

            var enemySccores = luxLogicState.ScoreNodes.Where(s => LuxLogicState.MapDistances[EnemyBasePos][s] < LuxLogicState.MapDistances[MyBasePos][s]).ToList();
            var origEnemyShips = luxLogicState.EnemyShips;
            foreach (var enemyScore in luxLogicState.PredictedEnemyPos)
            {
                if ((luxLogicState.Map[enemyBasePos] & MapBit.Visible) == 0)
                {
                    var ship = new Ship
                    {
                        Pos = enemyScore,
                        X = LuxLogicState.GetXY(enemyScore).Item1,
                        Y = LuxLogicState.GetXY(enemyScore).Item2,
                        Energy = 100
                    };
                    luxLogicState.EnemyShips.Add(ship);
                }
            }
            var oldScoreNodes = luxLogicState.ScoreNodes;
            var nnOutput = NNHandler.GetNNOutput(luxLogicState);
            luxLogicState.ScoreNodes = oldScoreNodes;
            luxLogicState.EnemyShips = origEnemyShips;
            foreach (var ship in myShips.Where(s => !s.IsMoved))
            {
                if (ship.Energy < luxLogicState.Env_cfg.unit_move_cost) continue;
                var (x, y) = LuxLogicState.GetXY(ship.Pos);
                var moveType = GetNextPos(luxLogicState, nnOutput, ship.Pos);
                if (moveType == MoveType.Sap && ship.Energy >= luxLogicState.Env_cfg.unit_sap_cost)
                {
                    var maxSapPos = GetMaxSapPos(luxLogicState, nnOutput, ship.Pos);
                    if (maxSapPos!=-1)
                    {
                        var (mx, my) = LuxLogicState.GetXY(maxSapPos);
                        ship.IsMoved = true;
                        Steps.Add(new Step() { MoveType = MoveType.Sap, UnitId = ship.ID, X = mx - x, Y = my - y });
                        luxLogicState.LastSapPositions.Add(maxSapPos);
                        SapMap[maxSapPos]++;
                    }
                    else
                    {
                        throw new Exception("Ide nem jutunk");
                    }
                }
                else
                {
                    ship.IsMoved = true;
                    Steps.Add(new Step() { MoveType = moveType, UnitId = ship.ID });
                }
                NextPosTarget[GetPosByMoveType(ship.Pos, moveType)]++;
            }

            return Steps;
        }

        int GetMaxSapPos(LuxLogicState luxLogicState, float[,,] nnOutput, int pos)
        {
            var maxSapValue = 0.0;
            var maxSapPos = -1;
            var (x,y) = LuxLogicState.GetXY(pos);
            for (var dx = -luxLogicState.Env_cfg.unit_sap_range; dx <= luxLogicState.Env_cfg.unit_sap_range; dx++)
            {
                for (var dy = -luxLogicState.Env_cfg.unit_sap_range; dy <= luxLogicState.Env_cfg.unit_sap_range; dy++)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    var nPos = LuxLogicState.GetPos(nx, ny);
                    if (nx < 0 || nx >= 24 || ny < 0 || ny >= 24) continue;
                    if (nnOutput[nx, ny, 6 + Math.Min(SapMap[nPos], 4)] > maxSapValue)
                    {
                        maxSapValue = nnOutput[nx, ny, 6];
                        maxSapPos = nPos;
                    }
                }
            }
            var sapThreshold = Configuration.SapDrop10Threshold;
            if (luxLogicState.SapDropFactor == 0.25) sapThreshold = Configuration.SapDrop025Threshold;
            if (luxLogicState.SapDropFactor == 0.5) sapThreshold = Configuration.SapDrop05Threshold;
            if (luxLogicState.SapDropFactor == null) sapThreshold = Configuration.SapDropUnknown;
            if (maxSapValue > sapThreshold)
            {
                return maxSapPos;
            }
            else
            {
                return -1;
            }
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
            if (NextPosTarget[pos] > 0) actualValue /= 2;
            for (var i = (int)moveType + 1; i < 6; i++)
            {
                if (!IsValid(luxLogicState, pos, (MoveType)i)) continue;
                if (i == 5 && GetMaxSapPos(luxLogicState, nnOutput, pos) == -1) continue;
                var nextPos = GetPosByMoveType(pos, (MoveType)i);
                if (NextPosTarget[nextPos] > 0)
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
    }
}
