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
A quick overview on how difficulty is calculated for songs.
 1. First each note in the song is assigned a difficulty score accoring to the following criteria.
	- Firstly the distance is considered between the current note and the previous same color note.
	- Next we compute the "Positional Difficulty" current note.
	  - Basically if a blue note is on the far left, it is harder to hit in general, vice versa for red.
	  - Another bonus if the note is high up.
	- Next is cut awkwardness, which is defined as cutting in the same direction.
	  - This only applies if the note is "behind" the previous note cut direction wise.
	  - Plan is to support based on cut angles as well.
	- Next would be readability, WIP right now but high priority for sure!
	- Finally the temporal distance from the last note of the same color.
	  - Currently `Math.Pow(diff_score, 1f + (0.375 / (dist_time*_secondsPerBeat)) * 0.87f)`
	  - Time was changed to be based on a 160 BPM song, so 1/1s on a 80 are not the same as 1/1s on 160
	- Another scalar is applied is the note is a circle note.
	  - If prior same color note is also circle, then severe difficulty reduction.

 2. Next up! Average difficulty
	- Self explanatory, sum up red and divide by total, sum up blue divide by total.
	- Then `var total_avg = (red_avg + blue_avg) / 1.96f;` no strong reason why I divide by 1.96.
	- We multiply this by 5.0 later, to bring in line with the next step. Keep tweaking.

 3. Almost done, now we compute the strain/endurance of the song.
	- Break the song up into `N` beat chunks, where `N` will be: 1,2,4,8.
	  - We find the strain value for each chunk (sum of diffs that fall into that beat section).
	  - Use these chunks to find the avg and std deviation of the strain values.
	  - Currently strain is the sum of `score += (float)Math.Pow(strain / peak_strain, 2f) * (1f + bonus);` for each chunk.
	    - Bonus is a bonus mult that goes up when the curr strain is near or above avg.
		  - If it falls below avg then bonus gets reduced (think long pauses and the such, you have time to recover).
	  - Then it is modified by `score *= 1f + ((float)i / (float)strains.Count) * 0.3f;`
	    - I did this to make the end of the song worth more in strain, because you have lots of energy (I hope) at the start!
	  - Want to add a further bonus purely based on length.
	  - I then added `strain_score *= (peak_strain / 3f) * (strain_avg / 2f);` to handle easy songs and weight them less.
	    - Needs more testing but doesn't seem to do much...
	  - I then log it `strain_score = (float)(Math.Log(strain_score) / Math.Log(1.2));` otherwise it can explode (Looking at you TTFAF...)
    - We return these values for each `N` chunks and do a weighted avg based on `N`.

 4. Finally, add the avg and endurance scores together and preform a length bonus on it.
	- `final_scaled_diff = final_scaled_diff * (1f + (_length*_secondsPerBeat / 90f) * 0.20f);`
	- I divide by 15 at the end to bring the numbers down.

This is obviously not perfect and needs a lot more work but I think it does a decent job honestly.

# TODO List
**Urgent**
 - [] Get a rough version of readability done.
 - [] Move to `score += strain * (1.0f + bonus);` instead of `score += (float)Math.Pow(strain / peak_strain, 2f) * (1f + bonus);`

**Unfinished**
 - [] Use cut angles for cut awkwardness.
 - [] Tweak strain bonus formula.
 - [] Rebalance note speed.

**Finished**
 - [x] Port code over.
 - [x] Display difficulty o nsong select.

**Planned**
 - [] Obstacle support (readability).
 - [] Bombs.
 - [] Maze runner style maps.