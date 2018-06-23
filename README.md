# BeatSaberSongLoader - Difficulty Calculator
A plugin for adding custom songs into Beat Saber.
Now tries to compute difficulty of a song!

*This mod works on both the Steam and Oculus Store versions.*

## Installation Instructions
 1. Make sure you first download the original song loader: https://github.com/xyonico/BeatSaberSongLoader/releases
 2. Download the latest release from here: https://github.com/nbayt/BeatSaberSongLoader/releases
 3. Replace the SongLoaderPlugin.dll in your "..\Beat Saber\Plugins" folder with the one you downloaded here.

## Usage
 1. Same as the original song loader. Select a song and you will see the Difficulty text near the song info box!

# Difficulty Calculation
See [here](https://github.com/nbayt/BeatSaberSongLoader/wiki/Difficulty-Calculation).

# TODO List
**Urgent**
 - [x] Get a rough version of readability done.
 - [x] Move to `score += strain * (1.0f + bonus);` instead of `score += (float)Math.Pow(strain / peak_strain, 2f) * (1f + bonus);`

**Unfinished**
 - [x] Use cut angles for cut awkwardness.
 - [ ] Tweak strain bonus formula.
 - [ ] Rebalance note speed.

**Finished**
 - [x] Port code over.
 - [x] Display difficulty on song select.

**Planned**
 - [ ] Obstacle support (readability).
 - [ ] Bombs.
 - [ ] Maze runner style maps.