import numpy as np
import matplotlib.pyplot as plt

# Load data
slope = np.loadtxt("../../terrain_slope.csv", delimiter=",")
height = np.loadtxt("../../terrain_height.csv", delimiter=",")

fig, axs = plt.subplots(1, 2, figsize=(14, 6))

# Height map
im0 = axs[0].imshow(height, cmap="terrain", origin="lower")
axs[0].set_title("Terrain Height (m)")
axs[0].set_xlabel("X")
axs[0].set_ylabel("Y")
plt.colorbar(im0, ax=axs[0], fraction=0.046, pad=0.04, label="Height (m)")

# Slope steepness map
im1 = axs[1].imshow(slope, cmap="inferno", origin="lower")
axs[1].set_title("Terrain Steepness (°)")
axs[1].set_xlabel("X")
axs[1].set_ylabel("Y")
plt.colorbar(im1, ax=axs[1], fraction=0.046, pad=0.04, label="Slope (degrees)")

plt.tight_layout()
plt.show()
