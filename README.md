Imitation Learning solution for Lux AI Season 3 Competition

## Summary

My solution was based on imitation learning using a U-Net architecture. I created a custom dataset by processing replay files with C# in Visual Studio, and implemented custom logic to extract important features such as player scores and relic node positions. For training, I used PyTorch with a U-Net implementation adapted from an open-source baseline, running in Visual Studio Code. The final submission was an ensemble of two models, each trained for about one day. For inference, I converted the models to ONNX and integrated them into a C# agent, along with some additional post-processing. I also benefited from several helpful community resources, including notebooks and discussion posts shared on Kaggle.

## Feature Selection

All of my features were added at the input of the U-Net with a shape of **24×24**.  
For energy-related features, I normalized the values by dividing them by 100.  
For example, both ship energy and node energy values were divided by 100 to help the model learn the relationships between these components more easily.

**My features:**

- **EnergyMap:** The energy value of a given node. If the value is unknown, it is set to 0.
- **EnergyMask:** Indicates whether the energy value of a node is known. Values are either 0 or 1.
- **AsteroidMap:** Indicates whether a given node contains an asteroid. Values are either 0 or 1.
- **NebulaMap:** Indicates whether a given node contains a nebula. Values are either 0 or 1.
- **ScoreMap:** Indicates whether a node is a score node. Values are either 0 or 1. If the status is unknown, the value is set to 0.
- **NotScoreMap:** Indicates whether a node is known *not* to be a score node. Values are either 0 or 1. If unknown, the value is set to 0.
- **CandidateMap:** After finding a score, this map marks candidate nodes that could be score nodes. Values are 0 or 1.
- **VisibleMap:** Indicates whether the node is currently visible. Values are either 0 or 1.
- **DiscoveredMap:** A heatmap that tracks exploration. When a node is visible, the value is set to 15; otherwise, it is decremented by 1 each turn.
- **IsRelicFoundMap:** Indicates whether the relic has been found in the current round. Values are 0 or 1. After round 3, this value is always 1.
- **NebulaModifierMap:** Represents the nebula modifier for each node. If unknown, the value is 0.
- **MyShipEnergyAvg:** The average energy of my ships located at the given node.
- **MyShipCount:** The number of my ships at the given node.
- **EnemyShipEnergyAvg:** The average energy of enemy ships at the given node.
- **EnemyShipCount:** The number of enemy ships at the given node.
- **LastEnemyShipEnergyAvg:** The average energy of enemy ships at the given node from the previous turn.
- **LastEnemyShipCount:** The number of enemy ships at the given node from the previous turn.
- **PredictedEnemyPosMap:** Based on enemy score changes in the given turn, this map predicts enemy ship positions on score nodes. Values are 0 or 1.
- **MyShipSapRangeMap:** A mask indicating where my ships can perform the sap action. Values are either 0 or 1.
- **ScoreSapRangeMapMask:** Indicates nodes that can be targeted for sap from score nodes. Values are either 0 or 1.
- **ScoreVisionRangeMapMask:** Indicates nodes that can be seen from score nodes, based on `UnitSensorRange`. Values are either 0 or 1.
- **EnemyVisionMask:** Indicates nodes that are within the vision range of enemy ships, based on `UnitSensorRange`. Values are either 0 or 1.

Additionally, I included scalar features such as:
- `UnitSensorRange`
- `SapDropFactor`
- `EnergyVoidFactor`
- `TurnNumber`
- `SapCost`
- `MoveCost`

These scalar values were broadcasted to **24×24** and added as extra input channels.
## Feature Transformation

1. **Score Node Detection Based on Score Changes**
   - When I gained more score points than expected (based on known score nodes), I marked the positions of all my ships as score candidate nodes. These formed one candidate group.
   - When I gained exactly the expected score from known score nodes, I marked all remaining ship positions as not score nodes.
   - If a candidate group contained only not score nodes except one, I marked that remaining node as a confirmed score node.

2. **Enemy Position Prediction from Score Differences**  
   Based on the enemy's score increase in a given turn, I inferred how many score nodes were occupied by enemy ships.  
   I sorted the score nodes by their distance to the enemy base and predicted enemy ship presence on the first *K* nodes, where *K* equals the enemy score gain.

3. **Range-Based Masks**  
   I transformed range values into binary masks to represent areas of influence:
   - I dilated my ship positions with the sap range → `MyShipSapRangeMap`
   - I dilated score node positions with the sap range → `ScoreSapRangeMapMask`
   - I dilated score node positions with the vision range → `ScoreVisionRangeMapMask`
   - I dilated enemy ship positions with the vision range → `EnemyVisionMask`

I applied the same normalization method to all energy-related features by dividing them by 100. This was intended to help the model learn relationships between different energy components more easily. However, I did not explicitly benchmark the effectiveness of this approach.

## Training

- **Optimizer:** Adam with a learning rate of 1e-3.

- **Phases:**
  1. Trained on Frog Parade replays (~20k samples) for about 10 epochs.  
     The checkpoint with the best validation loss was saved.
  2. Fine-tuned on Flat Neurons replays (~1.4k samples) for about 20 epochs.

- **Loss Function:**  
  Weighted binary cross-entropy loss with masking.  
  - Movement actions were upweighted.
  - Sap actions were masked to be considered only on valid nodes.  
    For example, sap predictions were excluded from the loss if they targeted positions too far from my ships.

- **Architecture:**  
  U-Net. The 24×24 input was padded to 32×32 to allow the use of four max-pooling layers.

- **Batch Size:** 256

- **Augmentation:** Diagonal symmetric flip

- **Output Layers:**
  - **Channels 0–5:** Represent actions — stay, up, right, down, left, sap.
  - **Channels 6–10:** Represent sap target positions:
    - If a position was targeted by 1 ship → channel 6 = 1, others = 0
    - If targeted by 2 ships → channels 6 and 7 = 1
    - And so on...
    - If targeted by more than 5 ships → treated the same as the 5-ship case

I trained two different models: the first had fewer feature channels, and the second had more. In the final submission, I created an ensemble of both models with equal weighting.
Since the goal was to create the strongest agent, not necessarily the one most similar to the training data, the validation error served only as an indicator — not an accurate metric for our objective.
To evaluate performance more realistically, I created a .bat script to run 40 matches between different versions of the ensembled models. I compared them head-to-head to determine which version performed best.


# Hardware I Used

- **Processor:** Intel® Core™ i5-6500 (4 cores)  
- **Memory:** 32 GB  
- **GPU:** NVIDIA GeForce RTX 3060 (12 GB)  
- **OS:** Windows 10

# Tools

- **Microsoft Visual Studio Community 2022 (64-bit)** – Version 17.13.1  
- **Visual Studio Code** – Version 1.99.3

# Creating Datasets

1. Download and extract the Kaggle metadata from  
   https://www.kaggle.com/datasets/kaggle/meta-kaggle

2. Adjust `SETTINGS.json` paths:
   - `kaggle_meta_dir`: Your extracted metadata directory
   - `frog_parade/replays_path`: Directory for downloaded Frog Parade replays (must exist and be empty)
   - `frog_parade/dataset_path`: Directory where the Frog Parade dataset will be created (must exist and be empty)
   - `flat_neurons/replays_path`: Directory for downloaded Flat Neurons replays (must exist and be empty)
   - `flat_neurons/dataset_path`: Directory where the Flat Neurons dataset will be created (must exist and be empty)

3. Build the .NET application:  
   `dotnet publish Source\DatasetCreator\DatasetCreator.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`

4. Create Frog Parade replays (snippet downloads only 1000 episodes; for training I used ~20k samples):  
   `python Source\ReplayDownloader\replay_downloader.py --episode_limit_size 1000 --settings_path SETTINGS.json --player_node frog_parade`

5. Create Frog Parade dataset:  
   `Source\DatasetCreator\bin\Release\net8.0\win-x64\publish\DatasetCreator.exe SETTINGS.json frog_parade`

6. Create Flat Neurons replays (snippet downloads only 1000 episodes; for training I used ~1450 samples):  
   `python Source\ReplayDownloader\replay_downloader.py --episode_limit_size 1000 --settings_path SETTINGS.json --player_node flat_neurons`

7. Create Flat Neurons dataset:  
   `Source\DatasetCreator\bin\Release\net8.0\win-x64\publish\DatasetCreator.exe SETTINGS.json flat_neurons`

# Train Model 1 – Small Model

1. Train a model based on the Frog Parade dataset:  
   `python Source\Train\train.py --settings_path SETTINGS.json --player_node frog_parade --is_larger_model False --epochs 10`

2. Fine-tune the model using the Flat Neurons dataset, based on the checkpoint from step 1:  
   `python Source\Train\train.py --settings_path SETTINGS.json --player_node flat_neurons --is_larger_model False --epochs 10 --pretrained_model_path <latest_checkpoint.pth>`

3. Rename the resulting ONNX model to:  
   `model1.onnx`

# Train Model 2 – Large Model

1. Train a model based on the Frog Parade dataset:  
   `python Source\Train\train.py --settings_path SETTINGS.json --player_node frog_parade --is_larger_model True --epochs 10`

2. Fine-tune the model using the Flat Neurons dataset, based on the checkpoint from step 1:  
   `python Source\Train\train.py --settings_path SETTINGS.json --player_node flat_neurons --is_larger_model True --epochs 10 --pretrained_model_path <latest_checkpoint.pth>`

3. Rename the resulting ONNX model to:  
   `model2.onnx`

# Agent Setup

1. Copy `model1.onnx` and `model2.onnx` to the `Source\LuxRunner` directory.  
   This directory already contains these files (from final submission), so you can overwrite them.

2. Create submission agent:  
   Run `Source\publish.bat`  
   This will generate a `release.tar.gz` file in the same directory.