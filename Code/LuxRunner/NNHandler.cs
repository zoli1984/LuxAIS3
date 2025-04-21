using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;


namespace LuxRunner
{
    public static class NNHandler
    {
        static InferenceSession session1 = new InferenceSession("model5x1_test.onnx");
        static InferenceSession session2 = new InferenceSession("model5x1_test2.onnx");

        static int inputDim = 33;

        public static float[,,] GetNNOutput(LuxLogicState luxLogicState)
        {
            var nnInputLabel = CreateNNInputLabel(luxLogicState);
            var flatInput = new float[1 * 24 * 24 * inputDim];
            for (int x = 0; x < 24; x++)
            {
                for (int y = 0; y < 24; y++)
                {
                    for (int z = 0; z < inputDim; z++)
                    {
                        flatInput[x * 24 * inputDim + y * inputDim + z] = (float)nnInputLabel[x, y, z];
                    }
                }
            }

            var inputTensor = new DenseTensor<float>(flatInput, new int[] { 1, 24, 24, inputDim });
            var inputs = new NamedOnnxValue[]
            {
                NamedOnnxValue.CreateFromTensor("input", inputTensor)
            };
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session1.Run(inputs);
            var outputTensor = results.First().AsTensor<float>().ToArray();

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results2 = session2.Run(inputs);
            var outputTensor2 = results2.First().AsTensor<float>().ToArray();

            for (int i = 0; i < outputTensor.Length; i++)
            {
                outputTensor[i] = (outputTensor[i] + outputTensor2[i]) / 2;
            }


            var outputArray = new float[32, 32, 6 + 5];
            Buffer.BlockCopy(outputTensor, 0, outputArray, 0, outputTensor.Length * sizeof(float));
            return outputArray;
        }

        static double[,] CreateMask(List<int> posList, int radius)
        {
            var mask = new double[24, 24];
            for (var x = 0; x < 24; x++)
            {
                for (var y = 0; y < 24; y++)
                {
                    foreach (var pos in posList)
                    {
                        var (px, py) = LuxLogicState.GetXY(pos);
                        var dist = Math.Max(Math.Abs(x - px), Math.Abs(y - py));
                        if (dist <= radius)
                        {
                            mask[x, y] = 1;
                        }
                    }
                }
            }
            return mask;
        }

        public static double[,,] CreateNNInputLabel(LuxLogicState luxLogicState)
        {
            List<double[,]> input = new List<double[,]>();

            var energyMap = new double[24, 24];
            var energyMapMask = new double[24, 24];
            var asteroidMap = new double[24, 24];
            var nebulaMap = new double[24, 24];
            var notScoreMap = new double[24, 24];
            var visibleMap = new double[24, 24];
            var discoveredMap = new double[24, 24];
            var sapDropMap = new double[24, 24];
            var sapRangeMap = new double[24, 24];
            var sapCostMap = new double[24, 24];
            var moveCostMap = new double[24, 24];
            var sensorRangeMap = new double[24, 24];
            var turnNumberMap = new double[24, 24];
            var isRelicFoundMap = new double[24, 24];
            var nebulaModifierMap = new double[24, 24];
            var sapDropMaskMap = new double[24, 24];
            var sapDropEnergyValue = new double[24, 24];
            var energyVoidMap = new double[24, 24];
            var energyVoidMaskMap = new double[24, 24];
            var predictedEnemyPosMap = new double[24, 24];
            for (int x = 0; x < 24; x++)
            {
                for (int y = 0; y < 24; y++)
                {
                    var value = luxLogicState.EnergyMap[LuxLogicState.GetPos(x, y)];
                    energyMap[x, y] = ((value == 127 ? 0 : value) + (luxLogicState.NebulaEnergyModified ?? 0)) / 100.0;
                    energyMapMask[x, y] = value == 127 ? 0 : 1;
                    asteroidMap[x, y] = (luxLogicState.Map[LuxLogicState.GetPos(x, y)] & MapBit.AsteroidBit) == MapBit.AsteroidBit ? 1 : 0;
                    nebulaMap[x, y] = (luxLogicState.Map[LuxLogicState.GetPos(x, y)] & MapBit.NebulaBit) == MapBit.NebulaBit ? 1 : 0;
                    notScoreMap[x, y] = (luxLogicState.Map[LuxLogicState.GetPos(x, y)] & MapBit.NotScoreBit) == MapBit.NotScoreBit ? 1 : 0;
                    visibleMap[x, y] = (luxLogicState.Map[LuxLogicState.GetPos(x, y)] & MapBit.Visible) == MapBit.Visible ? 1 : 0;
                    discoveredMap[x, y] = luxLogicState.DiscoveredMap[LuxLogicState.GetPos(x, y)] / 15.0;
                    if (discoveredMap[x, y] > 1)
                    {
                        discoveredMap[x, y] = 1;
                    }
                    sapDropMap[x, y] = luxLogicState.SapDropFactor ?? 1;
                    sapDropEnergyValue[x, y] = (luxLogicState.SapDropFactor ?? 1)*luxLogicState.Env_cfg.unit_sap_cost / 100.0;
                    sapDropMaskMap[x, y] = luxLogicState.SapDropFactor.HasValue ? 1 : 0;
                    energyVoidMap[x, y] = luxLogicState.EnergyVoidFactor ?? 0;
                    energyVoidMaskMap[x, y] = luxLogicState.EnergyVoidFactor.HasValue ? 1 : 0;
                    sapRangeMap[x, y] = luxLogicState.Env_cfg.unit_sap_range / 8.0;
                    sapCostMap[x, y] = luxLogicState.Env_cfg.unit_sap_cost / 100.0;
                    moveCostMap[x, y] = luxLogicState.Env_cfg.unit_move_cost / 100.0;
                    sensorRangeMap[x, y] = luxLogicState.Env_cfg.unit_sensor_range / 6.0;
                    turnNumberMap[x, y] = (luxLogicState.StartStep % 101) / 101.0;
                    isRelicFoundMap[x, y] = luxLogicState.IsRelicFound ? 1 : 0;
                    nebulaModifierMap[x, y] = luxLogicState.NebulaEnergyModified.HasValue ? luxLogicState.NebulaEnergyModified.Value / 100.0 : 0.0;
                    predictedEnemyPosMap[x, y] = luxLogicState.PredictedEnemyPos.Contains(LuxLogicState.GetPos(x, y)) ? 1 : 0;
                }
            }

            var relicMap = new double[24, 24];
            foreach (var relic in luxLogicState.RelicXYList)
            {
                relicMap[relic.Item1, relic.Item2] = 1;
            }

            var scoreMap = new double[24, 24];
            foreach (var score in luxLogicState.ScoreXYList)
            {
                scoreMap[score.Item1, score.Item2] = 1;
            }

            var candidateMap = new double[24, 24];
            foreach (var candidateGroup in luxLogicState.CandidateGroupList)
            {
                foreach (var candidate in candidateGroup.CandidateXY)
                {
                    candidateMap[candidate.Item1, candidate.Item2] = 1;
                }
            }

            var myShips = luxLogicState.MyShips.Where(s => s.Energy >= 0).ToList();
            var enemyShips = luxLogicState.EnemyShips.Where(s => s.Energy >= 0).ToList();

            var groupedMyShips = myShips.GroupBy(s => s.Pos);
            var myShipEnergyAvg = new double[24, 24];
            var myShipCount = new double[24, 24];
            foreach (var group in groupedMyShips)
            {
                var pos = group.Key;
                var count = group.Count();
                var energy = group.Average(s => s.Energy);
                var (x, y) = LuxLogicState.GetXY(pos);
                myShipEnergyAvg[x, y] = energy / 100.0;
                myShipCount[x, y] = count / 2.0;
            }

            var groupedEnemyShips = enemyShips.GroupBy(s => s.Pos);
            var enemyShipEnergyAvg = new double[24, 24];
            var enemyShipCount = new double[24, 24];
            foreach (var group in groupedEnemyShips)
            {
                var pos = group.Key;
                var count = group.Count();
                var energy = group.Average(s => s.Energy);
                var (x, y) = LuxLogicState.GetXY(pos);
                enemyShipEnergyAvg[x, y] = energy / 100.0;
                enemyShipCount[x, y] = count / 2.0;
            }

            var lastEnemyShipEnergyAvg = new double[24, 24];
            var lastEnemyShipCount = new double[24, 24];
            if (luxLogicState.PreviousEnemyShips != null)
            {
                var lastGroupedEnemyShips = luxLogicState.PreviousEnemyShips.Where(s => s.Energy >= 0).ToList().GroupBy(s => s.Pos);
                foreach (var group in lastGroupedEnemyShips)
                {
                    var pos = group.Key;
                    var count = group.Count();
                    var energy = group.Average(s => s.Energy);
                    var (x, y) = LuxLogicState.GetXY(pos);
                    lastEnemyShipEnergyAvg[x, y] = energy / 100.0;
                    lastEnemyShipCount[x, y] = count / 2.0;
                }
            }

            var myShipSapRangeMap = CreateMask(luxLogicState.MyShips.Where(s => s.Energy >= luxLogicState.Env_cfg.unit_sap_cost).Select(s=>s.Pos).ToList(), luxLogicState.Env_cfg.unit_sap_range);
            var scoreSapRangeMapMask = CreateMask(luxLogicState.ScoreNodes, luxLogicState.Env_cfg.unit_sap_range);
            var enemyVisionMapMask = CreateMask(luxLogicState.EnemyShips.Where(s => s.Energy>=0).Select(s=>s.Pos).ToList(), luxLogicState.Env_cfg.unit_sensor_range);
            var scoreVisionRangeMapMask = CreateMask(luxLogicState.ScoreNodes, luxLogicState.Env_cfg.unit_sensor_range);


            input.Add(energyMap);
            input.Add(energyMapMask);
            input.Add(moveCostMap);
            input.Add(sapCostMap);
            input.Add(sapDropEnergyValue);
            input.Add(nebulaModifierMap);
            input.Add(predictedEnemyPosMap);
            input.Add(enemyShipEnergyAvg);
            input.Add(enemyShipCount);
            input.Add(myShipEnergyAvg);
            input.Add(myShipCount);
            input.Add(lastEnemyShipEnergyAvg);
            input.Add(lastEnemyShipCount);


            input.Add(isRelicFoundMap);
            input.Add(discoveredMap);
            input.Add(visibleMap);
            input.Add(notScoreMap);
            input.Add(relicMap);
            input.Add(scoreMap);
            input.Add(candidateMap);

            input.Add(asteroidMap);
            input.Add(nebulaMap);

            input.Add(sapDropMap);
            input.Add(sapDropMaskMap);
            input.Add(energyVoidMap);
            input.Add(energyVoidMaskMap);

            input.Add(sapRangeMap);
            input.Add(sensorRangeMap);
            input.Add(turnNumberMap);

            input.Add(myShipSapRangeMap);
            input.Add(scoreSapRangeMapMask);
            input.Add(enemyVisionMapMask);
            input.Add(scoreVisionRangeMapMask);


            var inputArray = new double[24, 24, input.Count];
            for (int i = 0; i < input.Count; i++)
            {
                for (int x = 0; x < 24; x++)
                {
                    for (int y = 0; y < 24; y++)
                    {
                        inputArray[x, y, i] = input[i][x, y];
                    }
                }
            }

            return inputArray;
        }
    }
}
