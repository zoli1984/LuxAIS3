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