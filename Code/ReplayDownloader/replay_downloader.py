import pandas as pd
import numpy as np
from pathlib import Path
import os
import requests
import json
from tqdm.auto import tqdm
import datetime
import time
import polars as pl 
import argparse


BASE_URL = "https://www.kaggle.com/api/i/competitions.EpisodeService/"
GET_URL = BASE_URL + "GetEpisodeReplay"
COMPETITION_ID = 86411  # lux-ai-s3



def create_info_json(epid:int) -> dict:
    create_seconds = int(episodes_df.filter(pl.col('EpisodeId') == epid)['CreateTime'].item() / 1e9)
    end_seconds = int(episodes_df.filter(pl.col('EpisodeId') == epid)['CreateTime'].item() / 1e9)

    agents_df_filtered = agents_df.filter(pl.col('EpisodeId') == epid).sort('Index')

    agents = []
    for row in agents_df_filtered.iter_rows(named=True):
        agent = {
            "id": int(row["Id"]),
            "state": int(row["State"]),
            "submissionId": int(row['SubmissionId']),
            "reward": float(row['Reward']),
            "index": int(row['Index']),
            "initialScore": float(row['InitialScore']),
            "initialConfidence": float(row['InitialConfidence']),
            "updatedScore": float(row['UpdatedScore']),
            "updatedConfidence": float(row['UpdatedConfidence']),
            "teamId": int(99999)
        }
        agents.append(agent)

    info = {
        "id": int(epid),
        "competitionId": COMPETITION_ID,
        "createTime": {
            "seconds": create_seconds
        },
        "endTime": {
            "seconds": end_seconds
        },
        "agents": agents
    }

    return info

def saveEpisode(epid:int, sub_id:int) -> None:
    # request
    re = requests.post(GET_URL, json = {"episodeId": int(epid)})
        
    # save replay
    replay = re.json()
    with open(REPLAYS_PATH / f'{sub_id}_{epid}.json', 'w') as f:
        json.dump(replay, f)



# Argument parser
parser = argparse.ArgumentParser(description="Run script with settings file and dataset config.")
parser.add_argument(
    "--settings_path",
    type=Path,
    # required=True,
    default=Path(f"h:\kaggle\LuxAIS3\settings.json"),
    help="Path to JSON settings file"
)

parser.add_argument(
    "--player_node",
    type=str,
    # required=True,
    default="frog_parade",
    help="Name of the player node in settings file (e.g. 'frog_parade')"
)
parser.add_argument(
    "--episode_limit_size",
    type=int,
    default=1000,
    help="Limit on number of episodes"
)

args = parser.parse_args()

# Load and parse JSON
with open(args.settings_path, "r") as f:
    settings = json.load(f)

# Extract dataset settings
dataset_config = settings[args.player_node]

# Optional: include meta_dir from the top level
meta_dir = Path(settings["kaggle_meta_dir"])

# Assign to variables if needed
TARGET_SUBMISSION_IDS = dataset_config["submission_ids"]
EPISODE_LIMIT_SIZE = args.episode_limit_size
REPLAYS_PATH = Path(dataset_config["replays_path"])
META_DIR = meta_dir


episodes_df = pl.scan_csv(META_DIR / "Episodes.csv")
episodes_df = (
    episodes_df
    .filter(pl.col('CompetitionId')==COMPETITION_ID)
    .with_columns(
        pl.col("CreateTime").str.to_datetime("%m/%d/%Y %H:%M:%S", strict=False),
        pl.col("EndTime").str.to_datetime("%m/%d/%Y %H:%M:%S", strict=False),
    )
    .sort("Id")
    .collect()
)
print(f'Episodes.csv: {len(episodes_df)} rows.')

agents_df = pl.scan_csv(
    META_DIR / "EpisodeAgents.csv", 
    schema_overrides={'Reward':pl.Float32, 'UpdatedConfidence': pl.Float32, 'UpdatedScore': pl.Float32}
)

agents_df = (
    agents_df
    .filter(pl.col("EpisodeId").is_in(episodes_df['Id'].to_list()))
    .with_columns([
        pl.when(pl.col("InitialConfidence").cast(pl.Utf8) == "")
        .then(None)
        .otherwise(pl.col("InitialConfidence"))
        .cast(pl.Float64)
        .alias("InitialConfidence"),
        
        pl.when(pl.col("InitialScore").cast(pl.Utf8) == "")
        .then(None)
        .otherwise(pl.col("InitialScore"))
        .cast(pl.Float64)
        .alias("InitialScore")])
    .collect()
)
print(f'EpisodeAgents.csv: {len(agents_df)} rows.')

target_episodes_df = agents_df.filter(pl.col("SubmissionId").is_in(TARGET_SUBMISSION_IDS))

start_time = datetime.datetime.now()
episode_count = 0
for _sub_id, df in target_episodes_df.group_by('SubmissionId'):
    sub_id = _sub_id[0]
    ep_ids = df['EpisodeId'].unique()
    print(f'submission {sub_id} has {len(ep_ids)} episodes')
    for epid in ep_ids:
        if os.path.exists(REPLAYS_PATH / f'{sub_id}_{epid}.json'):
            print(f'{sub_id}_{epid}.json already exists')
            continue 

        saveEpisode(epid, sub_id); 
        episode_count+=1
        try:
            size = os.path.getsize(REPLAYS_PATH / f'{sub_id}_{epid}.json') / 1e6
            print(str(episode_count) + f': saved episode #{epid}')
        except:
            print(f'  file {sub_id}_{epid}.json did not seem to save')

        # process 1 episode/sec
        spend_seconds = (datetime.datetime.now() - start_time).seconds
        if episode_count > spend_seconds:
            time.sleep(episode_count - spend_seconds)
            
        if episode_count >= EPISODE_LIMIT_SIZE:
            break 
    if episode_count >= EPISODE_LIMIT_SIZE:
        break 
        
    print(f'Episodes saved: {episode_count}')