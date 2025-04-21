import numpy as np
import torch
from torch.utils.data import Dataset, DataLoader
import os
import glob
import random
import torch.nn as nn
import torch.nn.functional as F
from torch.optim.lr_scheduler import StepLR
import torch.optim as optim
import argparse
from pathlib import Path
import json


def load_all_3d_arrays(file_path):
    """
    Loads multiple 3D double arrays from a single binary file
    and returns them as a list of NumPy arrays.
    """
    arrays_list = []

    with open(file_path, 'rb') as f:
        # 1) Read how many 3D arrays in total
        count = np.fromfile(f, dtype=np.int32, count=1)[0]

        for _ in range(count):
            # 2) Read dimensions (each is a 32-bit int)
            dims = np.fromfile(f, dtype=np.int32, count=3)
            dim1, dim2, dim3 = dims

            # 3) Read the data (float64) for this 3D array
            num_elements = dim1 * dim2 * dim3
            data = np.fromfile(f, dtype=np.float32, count=num_elements)

            # 4) Reshape
            arr_3d = data.reshape((dim1, dim2, dim3))
            arrays_list.append(arr_3d)

    return arrays_list


class LuxAIDataset(Dataset):
    def __init__(self, file_path_list, batch_size, transform=None):
        self.file_path_list = file_path_list  # loads all at once
        self.transform = transform
        self.batch_size = batch_size
    
    def __len__(self):
        return len(self.file_path_list)
    
    def __getitem__(self, idx):
        # Take one 3D array from the list
        file_name = self.file_path_list[idx]
        in_array_list = load_all_3d_arrays(file_name)[0:505]   
        out_zip_name = file_name.replace('in', 'out')  
        label_array_list = load_all_3d_arrays(out_zip_name)[0:505] 

        stacked_in = np.stack(in_array_list, axis=0).astype(np.float32)
        stacked_in_mirror = stacked_in.copy()[:,::-1, ::-1, :].transpose(0, 2, 1, 3)
        stacked_in = np.concatenate([stacked_in, stacked_in_mirror], axis=0)
        tensor_4d_in = torch.from_numpy(stacked_in)

        stacked_out = np.stack(label_array_list, axis=0).astype(np.float32)
        stacked_out_mirror = stacked_out.copy()[:,::-1, ::-1, :].transpose(0, 2, 1, 3)
        channel_map = np.arange(11)
        channel_map[1], channel_map[2] = 2, 1
        channel_map[3], channel_map[4] = 4, 3
        stacked_out_mirror = stacked_out_mirror[..., channel_map]
        stacked_out = np.concatenate([stacked_out, stacked_out_mirror], axis=0)
        tensor_4d_out = torch.from_numpy(stacked_out)

        #get random batch_size indexes from 0 and 606
        indexes = random_numbers = np.random.randint(1, 605, size=self.batch_size)
  
        # # Yield exactly one "batch" for this file
        return tensor_4d_in[indexes, ...], tensor_4d_out[indexes, ...]


def weighted_bce_loss_masked(
    preds: torch.Tensor,
    targets: torch.Tensor,
    pos_weight: torch.Tensor,
    inputs: torch.Tensor,
) -> torch.Tensor:
    loss_per_pixel = F.binary_cross_entropy(preds, targets, reduction='none')  
    # Shape: [N, C, H, W]
    
    inputs = F.pad(inputs, (0, 0, 0, 8, 0, 8, 0, 0))

    my_shipMask = inputs[:,:,:,9] > 0
    my_sap_mask = inputs[:,:,:,29]

    loss_mask = torch.stack([my_shipMask, my_shipMask, my_shipMask, my_shipMask, my_shipMask, my_shipMask, my_sap_mask, my_sap_mask, my_sap_mask, my_sap_mask, my_sap_mask], dim=3).float() 

    # 2) Build a mask for positive targets
    positive_mask = (targets == 1).float()  # same shape [N, C, H, W]

    # 3) Multiply loss at positive positions by channel-specific weight.
    #    We want to broadcast pos_weight across N, H, W but apply it per channel.
    pos_weight = pos_weight.view(1, 1, 1, 11)  # shape [1, C, 1, 1] for broadcasting
    weighted_loss = loss_per_pixel * (1.0 + (pos_weight - 1.0) * positive_mask) * loss_mask
    # Explanation:
    #    - Where targets==0, positive_mask=0, so factor is 1 + (anything)*0 => 1.0
    #    - Where targets==1, positive_mask=1, so factor is 1 + (pos_weight - 1) => pos_weight

    # 4) Now reduce the loss
    loss = weighted_loss.sum()  # or .sum()
    loss = loss / (loss_mask.sum() + 1e-6)
    return loss


class DoubleConv(nn.Module):
    """(convolution => [BN] => ReLU) * 2"""

    def __init__(self, in_channels, out_channels, mid_channels=None, kernel_size=3, padding=1):
        super().__init__()
        if not mid_channels:
            mid_channels = out_channels
        self.double_conv = nn.Sequential(
            nn.Conv2d(in_channels, mid_channels, kernel_size=kernel_size, padding=padding, bias=False),
            nn.BatchNorm2d(mid_channels),
            nn.ReLU(inplace=True),
            nn.Conv2d(mid_channels, out_channels, kernel_size=kernel_size, padding=padding, bias=False),
            nn.BatchNorm2d(out_channels),
            nn.ReLU(inplace=True)
        )

    def forward(self, x):
        return self.double_conv(x)


class Down(nn.Module):
    """Downscaling with maxpool then double conv"""

    def __init__(self, in_channels, out_channels):
        super().__init__()
        self.maxpool_conv = nn.Sequential(
            nn.MaxPool2d(2),
            DoubleConv(in_channels, out_channels)
        )

    def forward(self, x):
        return self.maxpool_conv(x)


class Up(nn.Module):
    """Upscaling then double conv"""

    def __init__(self, in_channels, out_channels, bilinear=True):
        super().__init__()

        # if bilinear, use the normal convolutions to reduce the number of channels
        if bilinear:
            self.up = nn.Upsample(scale_factor=2, mode='bilinear', align_corners=True)
            self.conv = DoubleConv(in_channels, out_channels, in_channels // 2)
        else:
            self.up = nn.ConvTranspose2d(in_channels, in_channels // 2, kernel_size=2, stride=2)
            self.conv = DoubleConv(in_channels, out_channels)

    def forward(self, x1, x2):
        x1 = self.up(x1)

        diffY = x2.size()[2] - x1.size()[2]
        diffX = x2.size()[3] - x1.size()[3]

        x1 = F.pad(x1, [diffX // 2, diffX - diffX // 2,
                        diffY // 2, diffY - diffY // 2])
        
        x = torch.cat([x2, x1], dim=1)
        return self.conv(x)


class OutConv(nn.Module):
    def __init__(self, in_channels, out_channels):
        super(OutConv, self).__init__()
        self.conv = nn.Conv2d(in_channels, out_channels, kernel_size=1)

    def forward(self, x):
        return self.conv(x)
    

class UNet(nn.Module):
    def __init__(self, n_channels, n_classes, bilinear, is_larger):
        print(f"Using larger model: {is_larger}")
        super(UNet, self).__init__()
        self.n_channels = n_channels
        self.n_classes = n_classes
        self.bilinear = bilinear

        if (is_larger):
            self.inc = (DoubleConv(n_channels, int(192)))
            self.down1 = (Down(int(192), int(192)))
            self.down2 = (Down(int(192), int(192)))
            self.down3 = (Down(int(192), int(192)))
            factor = 2 if bilinear else 1
            self.down4 = (Down(int(192), int(192) // factor))
            self.up1 = (Up(int(288), int(192) // factor, bilinear))
            self.up2 = (Up(int(288), int(192) // factor, bilinear))
            self.up3 = (Up(int(288), int(192) // factor, bilinear))
            self.up4 = (Up(int(288), int(192), bilinear))
            self.outc = (OutConv(int(192), n_classes))
        else:
            self.inc = (DoubleConv(n_channels, int(128)))
            self.down1 = (Down(int(128), int(128)))
            self.down2 = (Down(int(128), int(128)))
            self.down3 = (Down(int(128), int(128)))
            factor = 2 if bilinear else 1
            self.down4 = (Down(int(128), int(128) // factor))
            self.up1 = (Up(int(192), int(128) // factor, bilinear))
            self.up2 = (Up(int(192), int(128) // factor, bilinear))
            self.up3 = (Up(int(192), int(128) // factor, bilinear))
            self.up4 = (Up(int(192), int(128), bilinear))
            self.outc = (OutConv(int(128), n_classes))

    def forward(self, x):
        # my input shape is (B, H, W ,C)
        x = F.pad(x, (0, 0, 0, 8, 0, 8, 0, 0))
        x = x.permute(0, 3, 1, 2).contiguous()  # => (B, C, H, W )
        #pad to 32x32

        x1 = self.inc(x)
        x2 = self.down1(x1)
        x3 = self.down2(x2)
        x4 = self.down3(x3)
        x5 = self.down4(x4)
        x = self.up1(x5, x4)
        x = self.up2(x, x3)
        x = self.up3(x, x2)
        x = self.up4(x, x1)
        logits = self.outc(x)
        logits = torch.sigmoid(logits)
        logits = logits.permute(0, 2, 3, 1).contiguous()  # => (B, H, W, C). 
        return logits



if __name__ == '__main__':
    # Argument parser
    parser = argparse.ArgumentParser(description="Run script with settings file and dataset config.")
    parser.add_argument(
        "--settings_path",
        type=Path,
        required=True,
        help="Path to JSON settings file"
    )

    parser.add_argument(
        "--player_node",
        type=str,
        default="frog_parade",
        help="Name of the player node in settings file (e.g. 'frog_parade')"
    )

    parser.add_argument(
        "--pretrained_model_path",
        type=str,
        default="",
        help="Pretrained model name. It is the trained FrogParade model when we finetune with FlatNeurons"
    )

    parser.add_argument(
        "--is_larger_model",
        type=lambda x: x.lower() == 'true',
        default=False,
        help="Use more features in U-net"
    )

    parser.add_argument(
        "--epochs",
        type=int,
        default=10,
        help="Use more features in U-net"
    )

    args = parser.parse_args()

    # Load and parse JSON
    with open(args.settings_path, "r") as f:
        settings = json.load(f)

    # Extract dataset settings
    player_config = settings[args.player_node]

    # Optional: include meta_dir from the top level
    dataset_dir = Path(player_config["dataset_path"])

    dataset_files = glob.glob(os.path.join(dataset_dir, '*.in'))
    random.Random(42).shuffle(dataset_files)

    train_dataset_files = dataset_files[:int(len(dataset_files)*player_config["train_ratio"])]
    val_dataset_files = dataset_files[int(len(dataset_files)*player_config["train_ratio"]):]

    print(f"Training files: {len(train_dataset_files)}") 
    print(f"Validation files: {len(val_dataset_files)}")

    train_dataset = LuxAIDataset(train_dataset_files, 256, transform=None)
    val_dataset = LuxAIDataset(val_dataset_files, 606, transform=None)
    train_loader = DataLoader(train_dataset, batch_size=None, shuffle=True, num_workers=4, pin_memory=True)
    val_loader = DataLoader(val_dataset, batch_size=None, shuffle=False, num_workers=4, pin_memory=True)

    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model = UNet(33,11, bilinear=True, is_larger=args.is_larger_model)
    if args.pretrained_model_path != "":
        model.load_state_dict(torch.load(args.pretrained_model_path))
    model.to(device)
    criterion = nn.BCELoss()  # or CrossEntropyLoss, etc.        
    optimizer = optim.Adam(model.parameters(), lr=1e-3)

    num_epochs = args.epochs
    best_loss = float('inf')
    weights = torch.tensor([1,5,5,5,5,1,5,5,5,5,5]).to(device)
    for epoch in range(num_epochs):
        print(f"Epoch [{epoch+1}/{num_epochs}] -------------------------------")

        # --------------------- TRAINING ---------------------
        model.train()
        running_loss = 0.0
        train_batches = 0

        for i, (inputs, labels) in enumerate(train_loader):
            train_batches += 1

            inputs = inputs.to(device)
            labels = labels.to(device)
            labels = F.pad(labels, (0, 0, 0, 8, 0, 8, 0, 0))

            outputs = model(inputs)
            loss = weighted_bce_loss_masked(outputs, labels, weights, inputs)
            optimizer.zero_grad()
            loss.backward()
            optimizer.step()

            running_loss += loss.item()
            if (i%100 == 0):
                print(f"  Batch {i} loss: {running_loss/train_batches:.4f}")
            
        avg_train_loss = running_loss / max(train_batches, 1)
        print(f"  Train loss: {avg_train_loss:.4f}")

        # -------------------- VALIDATION --------------------
        model.eval()
        val_loss = 0.0
        val_batches = 0
        with torch.no_grad():
            for j, (inputs, labels) in enumerate(val_loader):
        
                inputs = inputs.to(device)
                labels = labels.to(device)
                labels = F.pad(labels, (0, 0, 0, 8, 0, 8, 0, 0))
                val_batches += 1
                outputs = model(inputs)
                loss = weighted_bce_loss_masked(outputs, labels, weights, inputs)
                val_loss += loss.item()

        avg_val_loss = val_loss / max(val_batches, 1)
        print(f"  Val   loss: {avg_val_loss:.4f}")
        if avg_val_loss < best_loss:
            best_loss = avg_val_loss
        torch.save(model.state_dict(), rf'{settings["model_checkpoint_dir"]}/{epoch}_{avg_train_loss}_{avg_val_loss}.pth')
        mock_input=torch.rand(1,24,24,33).to(device)
        torch.onnx.export(model, mock_input, rf'{settings["model_checkpoint_dir"]}/{epoch}_{avg_train_loss}_{avg_val_loss}.onnx', input_names=["input"], output_names=["output"])
        print("Saved model!")

