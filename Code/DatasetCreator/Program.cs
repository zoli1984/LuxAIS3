using LuxRunner;
using MessagePack.Resolvers;
using System.Dynamic;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using TorchSharp;
using static TorchSharp.torch;

namespace DatasetCreator
{
    internal class Program
    {
        static object lockObject = new object();
        static void Main(string[] args)
        {
            var json = JsonObject.Parse(File.ReadAllText(args[0]));
            var playerNode = json[args[1]];
            BachnormSaver.StartBatchMixSaving((string)playerNode["replays_path"], (string)playerNode["dataset_path"]);
        }
    }
}
