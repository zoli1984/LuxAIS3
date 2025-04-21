Hardware I used
--------------------

Processor: Intel® Core™ i5-6500 Processor (4 cores)
Memory: 32 GB
GPU: NVIDIA GeForce RTX 3060 (12GB)
OS: Windows 10

Tools
--------------------
Microsoft Visual Studio Community 2022 (64-bit) - Version 17.13.1
Visual Studio Code - Version 1.99.3

Create datasets
--------------------
1. Download and extract kaggle metadata from https://www.kaggle.com/datasets/kaggle/meta-kaggle
2. Adjust settings.json paths.
	kaggle_meta_dir: Your extracted matadata directory
	frog_parade/replays_path: There will be frog_parade replays downloaded. Ensure directory exists and empty.
	frog_parade/dataset_path: There will be frog_parade dataset created. Ensure directory exists and empty.
	flat_neurons/replays_path: There will be flat_neurons replays downloaded. Ensure directory exists and empty.
	flat_neurons/dataset_path: There will be flat_neurons dataset created. Ensure directory exists and empty.

3. Build .net application
dotnet publish Code\DatasetCreator\DatasetCreator.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

2. Create Frog Parade replays. Sniplet download only 1000 episode, for training I used ~20k samples.
python Code\ReplayDownloader\replay_downloader.py --episode_limit_size 1000 --settings_path SETTINGS.json --player_node frog_parade

3. Create Frog Parade dataset
Code\DatasetCreator\bin\Release\net8.0\win-x64\publish\DatasetCreator.exe SETTINGS.json frog_parade

4. Create Flat Neurons replays. Sniplet download only 1000 episode, for training I used ~1450 samples.
python Code\ReplayDownloader\replay_downloader.py --episode_limit_size 1000 --settings_path SETTINGS.json --player_node flat_neurons

5. Create Flat Neurons dataset
Code\DatasetCreator\bin\Release\net8.0\win-x64\publish\DatasetCreator.exe SETTINGS.json flat_neurons

Train model 1 - This is the smaller model
--------------------------
1. Train a model based on Frog Parade dataset. 
python Code\Train\train.py --settings_path SETTINGS.json --player_node frog_parade --is_larger_model False --epochs 10 --checkpoint_dir <your_checkpoint_dir>
2. FineTune the model with Flat Neurons dataset from the last checkpoint from previous step
python Code\Train\train.py --settings_path SETTINGS.json --player_node flat_neurons --is_larger_model False --epochs 10 --checkpoint_dir <your_checkpoint_dir> --pretrained_model_path <latest_chekpoint pth file>
3. Take the last onnx model from step 2 and rename to model1.onnx

Train model 2 - This is the larger model
--------------------------
1. Train a model based on Frog Parade dataset. 
python Code\Train\train.py --settings_path SETTINGS.json --player_node frog_parade --is_larger_model True --epochs 10 --checkpoint_dir <your_checkpoint_dir>
2. FineTune the model with Flat Neurons dataset from the last checkpoint from previous step
python Code\Train\train.py --settings_path SETTINGS.json --player_node flat_neurons --is_larger_model True --epochs 10 --checkpoint_dir <your_checkpoint_dir> --pretrained_model_path <latest_chekpoint pth file>
3. Take the last onnx model from step 2 and rename to model2.onnx

Agent setup
--------------------------
1. Copy model1.onnx and model2.onnx to Code\LuxRunner directory. The directory already contains these files, they are my final submission, simply override them.

2. Create submission agent
run Code\publish.bat -> this will create a release.tar.gz in the same directory.