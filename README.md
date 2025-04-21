1. Download and extract kaggle metadata from https://www.kaggle.com/datasets/kaggle/meta-kaggle
2. Adjust settings.json paths
	kaggle_meta_dir: Your extracted matadata directory
	frog_parade/replays_path: There will be frog_parade replays downloaded
	frog_parade/dataset_path: There will be frog_parade dataset created
	flat_neurons/replays_path: There will be flat_neurons replays downloaded
	flat_neurons/dataset_path: There will be flat_neurons dataset created

2. Create Frog Parade replays. Sniplet download only 1000 episode, for training I used ~20k samples.
python code/python/replay_downloader.py --episode_limit_size 1000 --settings_path <settings.json path> --player_node frog_parade
3. Create Frog Parade dataset
LuxReplayLoader.exe <settings.json path> frog_parade

4. Create Flat Neurons replays. Sniplet download only 1000 episode, for training I used ~1450 samples.
python code/python/replay_downloader.py --episode_limit_size 1000 --settings_path <settings.json path> --player_node flat_neurons
5. Create Flat Neurons dataset
LuxReplayLoader.exe <settings.json path> flat_neurons
