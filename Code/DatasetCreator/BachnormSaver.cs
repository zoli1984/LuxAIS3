using LuxRunner;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Threading.Tasks;

namespace DatasetCreator
{
    public static class BachnormSaver
    {
        static Random r = new Random(2);
        static List<int> indexToSaveList;
        static int currentSaveIndex;
        static int count;
        public static void StartBatchMixSaving(string replayFolder, string outputFolder)
        {
            var jsonFiles = Directory.GetFiles(replayFolder, "*.json");
            jsonFiles = jsonFiles.OrderBy(_ => r.NextDouble()).ToArray();
            count = jsonFiles.Length;
            indexToSaveList = Enumerable.Range(0, jsonFiles.Length * 303).OrderBy(_ => r.NextDouble()).ToList();
            int i = 0;
            var temp = new LuxLogicState();
            foreach (var file in jsonFiles)
            {

                i++;
                Console.WriteLine(i.ToString() + " " + file);
                var tempFile = file;
                SaveNNDataset(tempFile, outputFolder);
            }
        }


        static void SaveNNDataset(string fileName, string outputDir)
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            LuxLogicState luxLogicState = new LuxLogicState();
            var (episode, playerId, envcgf, sapDrop, energyVoid) = GetEpisode(fileName);
            var nnInput = new List<double[,,]>();
            var nnOutput = new List<double[,,]>();
            for (var i = 0; i < 303; i++)
            {
                var input = GetInput(episode, i, playerId, envcgf);
                luxLogicState.Update(input);
                var output = GetOutput(episode, i, playerId);
                luxLogicState.LastSapPositions = new List<int>();
                for (int k = 0; k < output.Length; k++)
                {
                    var o = output[k];
                    if (o[0] == 5)
                    {
                        var ship = luxLogicState.MyShips.SingleOrDefault(s => s.ID == k);
                        if (ship != null)
                        {
                            luxLogicState.LastSapPositions.Add(LuxLogicState.GetPos(o[1] + ship.X, o[2] + ship.Y));
                        }
                    }
                }

                var nnInputLabel = NNHandler.CreateNNInputLabel(luxLogicState);
                var nnOutputLabel = CreateNNOutputLabel(luxLogicState, output);
                nnInput.Add(nnInputLabel);
                nnOutput.Add(nnOutputLabel);
                if (luxLogicState.SapDropFactor.HasValue && luxLogicState.SapDropFactor.Value != sapDrop) luxLogicState.SapDropFactor = sapDrop;
                if (luxLogicState.EnergyVoidFactor.HasValue && luxLogicState.EnergyVoidFactor.Value != energyVoid) luxLogicState.EnergyVoidFactor = energyVoid;
            }
            SaveAll3dArraysInput(outputDir, ".in", nnInput, currentSaveIndex);
            SaveAll3dArraysInput(outputDir, ".out", nnOutput, currentSaveIndex);
            currentSaveIndex += nnInput.Count;            
        }


        public static void SaveAll3dArraysInput(string outputDir, string postfix, List<double[,,]> allArrays, int startIndex)
        {
            for (int a = 0; a < allArrays.Count; a++)
            {
                double[,,] array3D = allArrays[a];
                int dim1 = array3D.GetLength(0);
                int dim2 = array3D.GetLength(1);
                int dim3 = array3D.GetLength(2);

                var currentFile = indexToSaveList[startIndex] % count;

                var filePath = Path.Combine(outputDir,$"{currentFile}"+postfix);
                if (!File.Exists(filePath))
                {
                    using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    using (BinaryWriter bw = new BinaryWriter(fs))
                    {
                        bw.Write(allArrays.Count);
                    }
                }

                using (FileStream fs = new FileStream(filePath, FileMode.Append, FileAccess.Write))
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    bw.Write(dim1);
                    bw.Write(dim2);
                    bw.Write(dim3);
                    // Write the double data in row-major order
                    for (int i = 0; i < dim1; i++)
                    {
                        for (int j = 0; j < dim2; j++)
                        {
                            for (int k = 0; k < dim3; k++)
                            {
                                bw.Write((float)array3D[i, j, k]);
                            }
                        }
                    }
                }
                startIndex += 1;
            }
        }


        public static double[,,] CreateNNOutputLabel(LuxLogicState luxLogicState, int[][] output)
        {
            var nnOutput = new double[24, 24, 6 + 5];

            var myShips = luxLogicState.MyShips.Where(s => s.Energy >= 0).ToList();
            foreach (var ship in myShips)
            {
                var outputValue = output[ship.ID];
                nnOutput[ship.X, ship.Y, outputValue[0]] = 1;
                if (outputValue[0] == 5)
                {
                    int i = 0;
                    if (ship.X + outputValue[1] < 0 || ship.X + outputValue[1] >= 24) continue;
                    if (ship.Y + outputValue[2] < 0 || ship.Y + outputValue[2] >= 24) continue;
                    while (i < 5)
                    {
                        if (nnOutput[ship.X + outputValue[1], ship.Y + outputValue[2], 6 + i] == 0)
                        {
                            nnOutput[ship.X + outputValue[1], ship.Y + outputValue[2], 6 + i] = 1;
                            break;
                        }
                        i++;
                    }
                }
            }

            return nnOutput;
        }

        public static (JsonNode, int, Env_Cfg, double, double) GetEpisode(string fileName)
        {
            JsonNode jsonObject = JsonObject.Parse(File.ReadAllText(fileName));
            var infoNode = jsonObject["info"];
            var teamNames = infoNode["TeamNames"];
            var player1_name = teamNames[0].ToString();
            var configuration = jsonObject["configuration"];
            var env_cfg_jsonnode = configuration["env_cfg"];
            var sapDropFactor = double.Parse(jsonObject["steps"][0][0]["info"]["replay"]["params"]["unit_sap_dropoff_factor"].ToString().Replace(".", ","));
            var energyVoidFactor = double.Parse(jsonObject["steps"][0][0]["info"]["replay"]["params"]["unit_energy_void_factor"].ToString().Replace(".", ","));
            var env_cfg = JsonSerializer.Deserialize<Env_Cfg>(env_cfg_jsonnode.ToString());

            return (jsonObject, player1_name == "Frog Parade" ? 0 : 1, env_cfg, sapDropFactor, energyVoidFactor);
        }
        public static int[][] GetOutput(JsonNode jsonNode, int turn, int playerId)
        {
            var episodeSteps = jsonNode["steps"][turn + 1];
            var action = episodeSteps[playerId]["action"];

            return JsonSerializer.Deserialize<int[][]>(action.ToString());
        }

        public static LuxState GetInput(JsonNode jsonNode, int turn, int playerId, Env_Cfg config)
        {
            var episodeSteps = jsonNode["steps"][turn];
            var obsersation = episodeSteps[playerId]["observation"];
            var obs = JsonSerializer.Deserialize<Obs>(obsersation["obs"].ToString());
            var steps = episodeSteps[0]["observation"]["step"].GetValue<int>();
            var remainingOverageTime = obsersation["remainingOverageTime"].GetValue<double>();
            var player = obsersation["player"].ToString();
            var reward = obsersation["reward"].GetValue<float>();

            var luxState = new LuxState
            {
                obs = obs,
                step = steps,
                remainingOverageTime = remainingOverageTime,
                player = player,
                reward = reward,
                info = new Info
                {
                    env_cfg = config
                }
            };
            return luxState;
        }
    }
}
