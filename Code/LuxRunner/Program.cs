using MessagePack.Resolvers;
using MessagePack;
using System;
using System.Data;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LuxRunner
{
    internal class Program
    {

        static void Main(string[] args)
        {
            int playerId;
            int opponentId;
            int maxUnits = 0;
            int currentEnemyWin = 0;
            LuxLogicState luxLogicState = new LuxLogicState();
            Random r = new Random();
            LuxState state;
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                string input;
                string luxLogicStateJsonString;
                if (Debugger.IsAttached)
                {
                    (state, luxLogicState) = LoadState(@$"h:\kaggle\LuxAILog\Current\testGame.txt", 1);
                    //(state, luxLogicState) = LoadState(@$"h:\kaggle\LuxAILog\agentlog.json", 104);

                }
                else
                {
                    input = Console.ReadLine();
                    state = JsonSerializer.Deserialize<LuxState>(input);
                }

                byte[] serializedLogic = MessagePack.MessagePackSerializer.Serialize(luxLogicState);
                luxLogicStateJsonString = Convert.ToBase64String(serializedLogic);


                byte[] serialized = MessagePack.MessagePackSerializer.Serialize(state);
                input = Convert.ToBase64String(serialized);
                var serializedGameString = GetSerializedState(input, luxLogicStateJsonString);
                if (sb.Length > 0)
                {
                    sb.Append(",");
                }
                sb.AppendLine(GetRoundStringForSave(input, luxLogicStateJsonString));
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !Debugger.IsAttached)
                {
                    File.WriteAllText(@$"h:\kaggle\LuxAILog\Current\{state.step}.txt", serializedGameString);
                    File.WriteAllText(@$"h:\kaggle\LuxAILog\Current\testGame.txt", $"[{sb.ToString()}]");
                    if (state.obs.steps == 500)
                    {
                        var scoreFileName = @$"h:\kaggle\LuxAILog\Score.txt";
                        var sumWin0List = new List<int>();
                        var sumWin1List = new List<int>();
                        var scoreSb = new StringBuilder();
                        if (File.Exists(scoreFileName))
                        {
                            var oldResults = File.ReadAllLines(scoreFileName);
                            oldResults = oldResults.Take(oldResults.Length - 2).ToArray();
                            foreach (var oldResult in oldResults)
                            {
                                scoreSb.AppendLine(oldResult);
                            }
                        }
                        var team0LastWin = state.obs.team_points[0] > state.obs.team_points[1];
                        if (luxLogicState.PlayerId == 0)
                        {
                            scoreSb.AppendLine($"{state.obs.team_wins[0] + (team0LastWin ? 1 : 0)} {state.obs.team_wins[1] + (team0LastWin ? 0 : 1)}");
                        }
                        else
                        {
                            scoreSb.AppendLine($"{state.obs.team_wins[1] + (team0LastWin ? 0 : 1)} {state.obs.team_wins[0] + (team0LastWin ? 1 : 0)}");
                        }
                        var lines = scoreSb.ToString().Split(Environment.NewLine);
                        foreach (var line in lines)
                        {
                            //Console.Error.WriteLine("Line:" + line);
                            var parts = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 2)
                            {
                                var win0 = int.Parse(parts[0]);
                                var win1 = int.Parse(parts[1]);

                                sumWin0List.Add(win0);
                                sumWin1List.Add(win1);
                            }
                        }
                        var player0Wins = 0;
                        for (int i = 0; i < sumWin0List.Count; i++)
                        {
                            player0Wins += sumWin0List[i] > sumWin1List[i] ? 1 : 0;
                        }
                        scoreSb.AppendLine($"{sumWin0List.Sum()} {sumWin1List.Sum()}");
                        scoreSb.AppendLine($"Total: {player0Wins}({sumWin0List.Sum()}) {sumWin0List.Count - player0Wins}({sumWin1List.Sum()})");
                        File.WriteAllText(scoreFileName, scoreSb.ToString());
                    }
                }
                luxLogicState.Update(state);

                if (luxLogicState.PlayerId == 0)
                {
                    if (state.obs.team_wins[1] > currentEnemyWin)
                    {
                        NNHandler.SwitchNN();
                        currentEnemyWin = state.obs.team_wins[1];
                    }
                }
                else
                {
                    if (state.obs.team_wins[0] > currentEnemyWin)
                    {
                        NNHandler.SwitchNN();
                        currentEnemyWin = state.obs.team_wins[0];
                    }
                }

                    Console.Error.WriteLine(serializedGameString); //input json for debugging


                //    Dijkstra.ShortestPaths(luxLogicState.Tiles, 0, 100, luxLogicState.StartStep, 1, 0.09, 0.01, 10, 0,
                //luxLogicState.Env_cfg.unit_move_cost, luxLogicState.NebulaEnergyModified ?? 0);

                maxUnits = luxLogicState.Env_cfg.max_units;

                playerId = state.player == "player_0" ? 0 : 1;
                opponentId = playerId == 1 ? 0 : 1;

                //Console.Error.WriteLine("Step: " + state.step);

                var actionsAr = new int[maxUnits][];

                for (int i = 0; i < maxUnits; i++)
                {
                    actionsAr[i] = new int[3];
                }

                if (luxLogicState.StartStep<500 || (luxLogicState.RelicList.Count>0 && luxLogicState.ScoreNodes.Count>0))
                {
                    //var processLogic = new ProcessLogic();
                    var processLogic = new ProcessLogicNN();
                    var steps = processLogic.Run(luxLogicState, luxLogicState.MyShips, luxLogicState.EnemyShips);

                    foreach (var step in steps)
                    {
                        actionsAr[step.UnitId][0] = (int)step.MoveType;
                        actionsAr[step.UnitId][1] = step.X;
                        actionsAr[step.UnitId][2] = step.Y;
                    }
                }
                else
                {
                    var processLogic = new ProcessLogic();
                    //var processLogic = new ProcessLogicNN();
                    var steps = processLogic.Run(luxLogicState, luxLogicState.MyShips, luxLogicState.EnemyShips);

                    foreach (var step in steps)
                    {
                        actionsAr[step.UnitId][0] = (int)step.MoveType;
                        actionsAr[step.UnitId][1] = step.X;
                        actionsAr[step.UnitId][2] = step.Y;
                    }
                }

                //var processLogic = new ProcessLogicMixed();
                //var steps = processLogic.Run(luxLogicState, luxLogicState.MyShips, luxLogicState.EnemyShips);

                //foreach (var step in steps)
                //{
                //    actionsAr[step.UnitId][0] = (int)step.MoveType;
                //    actionsAr[step.UnitId][1] = step.X;
                //    actionsAr[step.UnitId][2] = step.Y;
                //}

                //foreach (var ship in luxLogicState.MyShips)
                //{
                //    var (dist, parent) = Dijkstra.ShortestPaths(luxLogicState.Tiles, ship.Pos, ship.Energy, luxLogicState.StartStep, 1, 0.01, 0.01, luxLogicState.Env_cfg.unit_move_cost, luxLogicState.NebulaEnergyReduction??0);
                //    actionsAr[ship.ID][0] = r.Next(5); //move direction
                //    actionsAr[ship.ID][1] = 0;
                //    actionsAr[ship.ID][2] = 0;
                //}   

                //var a =MessagePack.MessagePackSerializer.Serialize(luxLogicState);
                //var b = MessagePack.MessagePackSerializer.Deserialize<LuxLogicState>(a);

                var output = JsonSerializer.Serialize(new LuxActions() { action = actionsAr });


                //Console.Error.WriteLine(output); //output json for debugging
                Console.WriteLine(output);
            }
        }

        private static List<Ship> ExtractVisibleShips(LuxState state, int pid)
        {
            List<Ship> ships = new List<Ship>();
            var mask = state.obs.units_mask[pid];
            var positions = state.obs.units.position[pid];
            var energy = state.obs.units.energy[pid];
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i]) //unit exists and is visible
                {
                    ships.Add(new Ship() { ID = i, Owner = pid, X = positions[i][0], Y = positions[i][1], Energy = energy[i] });
                }
            }
            return ships;

        }


        static string GetSerializedState(string input, string luxLogicState)
        {
            return CompressString(input + "___" + luxLogicState);
        }

        static string GetRoundStringForSave(string input, string luxLogicState)
        {
            var compressed = GetSerializedState(input, luxLogicState);
            return "[{\"stderr\": \"" + compressed + "\"}]";
        }

        public static string CompressString(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            byte[] inputBytes = Encoding.UTF8.GetBytes(text);

            using (var outputStream = new MemoryStream())
            {
                using (var compressor = new BrotliStream(outputStream, CompressionLevel.SmallestSize, true))
                {
                    compressor.Write(inputBytes, 0, inputBytes.Length);
                }

                // Convert compressed bytes to a Base64 string
                return Convert.ToBase64String(outputStream.ToArray());

            }
        }

        // Decompress a string
        public static string DecompressString(string compressedText)
        {
            if (string.IsNullOrEmpty(compressedText))
                return compressedText;

            byte[] inputBytes = Convert.FromBase64String(compressedText);

            using (var inputStream = new MemoryStream(inputBytes))
            using (var gzipStream = new BrotliStream(inputStream, CompressionMode.Decompress))
            using (var outputStream = new MemoryStream())
            {
                gzipStream.CopyTo(outputStream);
                byte[] outputBytes = outputStream.ToArray();

                // Convert decompressed bytes back to a string
                return Encoding.UTF8.GetString(outputBytes);
            }
        }

        static (LuxState, LuxLogicState) LoadState(string filePath, int round)
        {
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(filePath)))
            {
                JsonElement root = doc.RootElement;
                var input_luxLogicState = root[round][0].GetProperty("stderr").GetString().Split('\n')[0];
                input_luxLogicState = DecompressString(input_luxLogicState);
                var input = input_luxLogicState.Split("___")[0];
                var luxLogicStateJsonString = input_luxLogicState.Split("___")[1];
                var state = MessagePack.MessagePackSerializer.Deserialize<LuxState>(Convert.FromBase64String(input));
                var luxLogicState = MessagePack.MessagePackSerializer.Deserialize<LuxLogicState>(Convert.FromBase64String(luxLogicStateJsonString));

                return (state, luxLogicState);
            }
        }

    }


}