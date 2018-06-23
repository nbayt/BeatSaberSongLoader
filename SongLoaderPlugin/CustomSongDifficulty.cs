using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace SongLoaderPlugin
{
    [Serializable]
    public class CustomSongDifficulty
    {
        public string _version;
        public float _beatsPerMinute;
        public float _beatsPerBar;
        public float _noteJumpSpeed;
        public float _shuffle;
        public float _shufflePeriod;
        public Event[] _events;
        public Note[] _notes;
        public Obstacle[] _obstacles;

        [Serializable]
        public class Event
        {
            public float _time;
            public int _type;
            public float value;
        }

        [Serializable]
        public class Note
        {
            public float _time;
            public int _lineIndex;
            public int _lineLayer;
            public int _type;
            public int _cutDirection;
        }

        [Serializable]
        public class Obstacle
        {
            public float _time;
            public int _lineIndex;
            public int _type;
            public float _duration;
            public float _width;
        }

        private float _secondsPerBeat = 0.0f;
        private float _length = 0.0f;

        private StreamWriter writer;

        public int GetDifficulty(string song_name, LevelStaticData.Difficulty difficulty)
        {
            writer = new StreamWriter(Environment.CurrentDirectory.Replace('\\', '/') + "/CustomSongs/.Output" + "/"+song_name+"_"+difficulty+"_song_parse.txt", false);
            writer.Write(song_name+" "+difficulty+Environment.NewLine);

            if (_notes.Length < 1)
            {
                writer.Write("No Notes in file, not handled!");
                return 1;
            }

            Array.Sort(_notes, delegate (Note x, Note y) { return x._time.CompareTo(y._time); }); // Not always sorted in some cases
            _secondsPerBeat = 1.0f/(_beatsPerMinute / 60.0f);
            _length = _notes[_notes.Length-1]._time - _notes[0]._time;

            //float temp = UnityEngine.Random.Range(0.0f, 10.0f);
            //int temp2 = (int)(temp * 100.0f);
            int temp = 0;
            float diff_score = 0.0f;
            try
            {
                ArrayList note_diffs = getNoteDifficulty();
                diff_score = parseDifficultyData(note_diffs);
            }
            catch(Exception e)
            {
                writer.Write(e.Message);
                writer.Write(e.StackTrace);
            }
            temp = (int)(diff_score * 100.0f); // Don't worry, DifficultyDisplay.cs will fix this

            writer.Close();
            return temp;
        }

        // private classes
        private class NoteDiff
        {
            public float _time;
            public float _diffScore;
            public int _type;
        }

        // all helper code will go here

        private float parseDifficultyData(ArrayList note_data)
        {
            // plan was avg = 60% of score, endurance = 40% of score, but...
            // ...ended up with total = (avg * 5 + endurance) * length_bonus

            // first pass is average diff for each color
            float red_sum = 0f, blue_sum = 0f;
            ArrayList red_diffs = new ArrayList();
            ArrayList blue_diffs = new ArrayList();
            foreach(NoteDiff note in note_data)
            {
                float diff = note._diffScore;
                if (note._type == 0) { red_diffs.Add(diff); red_sum += diff; }
                else if (note._type == 1) { blue_diffs.Add(diff); blue_sum += diff; }
            }
            float red_avg = 0f;
            float blue_avg = 0f;
            if (red_diffs.Count > 0) { red_avg = red_sum / (float)red_diffs.Count; }
            if (blue_diffs.Count > 0) { blue_avg = blue_sum / (float)blue_diffs.Count; }
            var total_avg = (red_avg + blue_avg) / 1.96f; // no real reason other than that if both arms are working hard, slight boost overall.

            // second pass is how often we reach peak strain, this is endurance score
            // multiple passes for 8 beats, 4 beats, 2 beats, and 1 beat. Weighted avg.
            float strain_score = 0f;
            strain_score += calcSongEnduranceScore(note_data, 8) * 8f;
            strain_score += calcSongEnduranceScore(note_data, 4) * 4f;
            strain_score += calcSongEnduranceScore(note_data, 2) * 2f;
            strain_score += calcSongEnduranceScore(note_data, 1) * 1f;
            strain_score = strain_score / 15f;

            // take avg diff, increase multiplier by how overall agressive the song was with timing, which is also scaled by the raw bpm of song. Higer bpm song is considered harder overall due to speed that notes move in,
            // but (1 / 2 rhythms on 80 bpm are as hard as 1 / 1 on 160 bpm).
            // regardless, 80 in this case should be considered harder since diff should be based on song bpm. Higher bpm songs will just push diff points higher when mapped the same - NOT ANYMORE
            // TODO TEST!!!!!
            //var final_scaled_diff = (total_avg * (1 + (speed_data.spb / speed_data.avg_spn) + (bpm / (bpm + 130) * 0.2)) * 5.0); // 5x mult to bring score in line with average strain scores
            float final_scaled_diff = total_avg * 5.0f;
            writer.Write("Total Avg: " + total_avg + Environment.NewLine);
            writer.Write("Strain Score Total Avg: " + strain_score + Environment.NewLine);

            // specifically the Han Solo song breaks the rating due to super low BPM (45.5), so 1/4 rhythms are very easy to hit (180 1/1s), but code over weights
            // not sure how to really fix it, trying the seconds per note thing below helps a bit

            // next we add in the strain score
            final_scaled_diff += strain_score;
            writer.Write("Final Scaled Diff Pre Length: " + final_scaled_diff + Environment.NewLine);
            final_scaled_diff = final_scaled_diff * (1f + (_length*_secondsPerBeat / 90f) * 0.20f); // longer songs are harder, partially accounted for in strain with bonus mult
            writer.Write("Final Scaled Diff: " + final_scaled_diff + Environment.NewLine);
            writer.Write("Star Diff: " + final_scaled_diff/18.0f + Environment.NewLine);
            return final_scaled_diff / 18.0f;
        }

        private float calcSongEnduranceScore(ArrayList note_data, int beats_per_section)
        {
            // first find peak strain value
            NoteDiff first_note = (NoteDiff)note_data[0];
            NoteDiff last_note = (NoteDiff)note_data[note_data.Count - 1];
            float first_beat = first_note._time;
            float last_beat = last_note._time;
            float peak_strain = -1f;
            //float curr_strain = 0;
            ArrayList strains = new ArrayList(); // init all possible beat sections to 0
            for (int itr = (int)Math.Floor(first_beat / beats_per_section) * beats_per_section; itr <= (int)Math.Floor(last_beat / beats_per_section) * beats_per_section; itr += beats_per_section)
            {
                //strains[itr / beats_per_section] = 0;
                strains.Add(0.0f);
            }
            float curr_beat = first_beat;
            foreach(NoteDiff note in note_data)
            {        
                int beat_pos = (int)Math.Floor(note._time / beats_per_section);
                if (beat_pos < strains.Count)
                {
                    strains[beat_pos] = (float)(strains[beat_pos]) + note._diffScore;
                }
                else
                {
                    strains.Add(note._diffScore);
                }
            }
            
            foreach(float strain in strains)
            {
                if (strain > peak_strain) { peak_strain = strain; }
            }

            // analyze the strain over time
            // first find average
            float strain_avg = 0f, strain_sum = 0f;
            foreach(float strain in strains) { strain_sum += strain; }
            strain_avg = strain_sum / (float)(strains.Count);

            // next std deviation
            float strain_dev = 0;
            strain_sum = 0;
            foreach(float strain in strains) { strain_sum += (float)(Math.Pow(strain - strain_avg, 2.0f)); }
            strain_dev = (float)Math.Sqrt(strain_sum / ((float)strains.Count));

            // do some bullshit math to get score
            float strain_score = 0f;
            float bonus = 0.0f; // the longer we are close to or above avg by std, then increase bonus because is tiring to work above avg all the time... something like that?
            float max_bonus = 5.0f;
            for(int i = 0; i < strains.Count; i++)
            {
                float strain = (float)strains[i];
                float score = 0;
                if (strain > strain_avg)
                {
                    if (strain > strain_avg + strain_dev * 3f)
                    {
                        bonus = Math.Min(bonus + 0.3f, max_bonus);
                    }
                    else if (strain > strain_avg + strain_dev * 2f)
                    {
                        bonus = Math.Min(bonus + 0.11f, max_bonus);
                    }
                    else if (strain > strain_avg + strain_dev * 1f)
                    {
                        bonus = Math.Min(bonus + 0.05f, max_bonus);
                    }
                    else if (strain > strain_avg + strain_dev * 0.5f)
                    {
                        bonus = Math.Min(bonus + 0.03f, max_bonus); // new
                    }
                    else if (strain > strain_avg + strain_dev * 0f)
                    {
                        bonus = Math.Min(bonus + 0.02f, max_bonus); // was 0.03
                    }
                }
                else
                {
                    if (strain < strain_avg - strain_dev * 3f)
                    {
                        bonus = Math.Max(bonus - 0.20f, 0.0f);
                    }
                    else if (strain < strain_avg - strain_dev * 2f)
                    {
                        bonus = Math.Max(bonus - 0.10f, 0.0f);
                    }
                    else if (strain < strain_avg - strain_dev * 1f)
                    {
                        bonus = Math.Max(bonus - 0.05f, 0.0f);
                    }
                    else if (strain < strain_avg - strain_dev * 0.5f)
                    {
                        bonus = Math.Max(bonus - 0.01f, max_bonus);
                    }
                    else if (strain < strain_avg - strain_dev * 0f)
                    {
                        bonus = Math.Max(bonus + 0.002f, max_bonus);
                    }

                    // trying out having strain for a section not compared against peak strain
                    // TODO tried it, need more balancing for sure, helped some songs, made many more worse
                    score += strain * (1.0f + bonus);
                    //score += (float)Math.Pow(strain / peak_strain, 2f) * (1f + bonus);
                }
                score *= 1f + ((float)i / (float)strains.Count) * 0.3f;

                // TODO Balance
                //writer.Write("Strain Pre Length: " + score + Environment.NewLine);
                //score *= 1.0f + ((float)i / (25.0f * (float)beats_per_section));
                //writer.Write("Strain Post Length: " + score + Environment.NewLine);

                strain_score += score;
            }

            strain_score *= (peak_strain / 3f) * (strain_avg / 2f); // bonus scalar so easy songs that are uniform in diff don't get high strain scores
            writer.Write("Strain Score Pre Log: " + strain_score + Environment.NewLine);
            strain_score = (float)(Math.Log(strain_score) / Math.Log(1.2));
            // This should never happen anymore, but leaving just in case
            if (strain_score < 0f)
            {
                strain_score = 0f;
            }
            writer.Write("Strain Score For "+beats_per_section+" Beats: "+strain_score+Environment.NewLine);
            return strain_score;
        }

        private ArrayList getNoteDifficulty()
        {
            ArrayList[] note_stack = { new ArrayList(), new ArrayList() };
            Note[] prior_color_note = { null, null };
            ArrayList result_note_data = new ArrayList();

            // not used for anything just yet
            ArrayList prior_note_stack = new ArrayList();

            for (int itr = 0; itr < _notes.Length; itr++)
            {
                Note note = _notes[itr];
                // keep a history of previous notes
                if (note._type != 2)
                {
                    prior_note_stack.Add(note);
                }

                if (note._type == 0 || note._type == 1)
                {
                    float score = 0.0f;
                    Note next_note = getNextNote(_notes, note._type, itr);
                    if (next_note != null)
                    {
                        if (note._time == next_note._time)
                        {
                            //red_note_stack.push(note);
                            note_stack[note._type].Add(note);
                            continue;
                        }
                    }
                    if (note_stack[note._type].Count > 0)
                    {
                        Note last_of_type = (Note)note_stack[note._type][note_stack[note._type].Count - 1];
                        if (note._time == last_of_type._time)
                        { // don't handle stack yet, may be more notes
                            note_stack[note._type].Add(note);
                            //red_note_stack.push(note);
                            if (getNextNote(_notes, note._type, itr) != null) { continue; }
                        }
                        // handle stack here
                        if (note_stack[note._type].Count == 2)
                        {
                            // handle two note stack here
                            // figure out which note is first, based on distance. Really need to know the last note
                            Note note_a = (Note)note_stack[note._type][0], note_b = (Note)note_stack[note._type][1], first_note = null, last_note = null;
                            if (GetPhysicalNoteDistance(note_a, note_b) < GetPhysicalNoteDistance(note_b, note_a)) { first_note = note_a; last_note = note_b; }
                            else { first_note = note_b; last_note = note_a; }

                            // add the average to each note to give a difficulty boost
                            float score_a = NoteHitDifficulty(prior_note_stack, prior_color_note[note._type], first_note), score_b = NoteHitDifficulty(prior_note_stack, prior_color_note[note._type], last_note);
                            float avg_score_a_b = ((score_a + score_b) / 2.0f);
                            score_a += avg_score_a_b;
                            score_b += avg_score_a_b;

                            NoteDiff a = new NoteDiff() { _time = note_a._time, _diffScore = score_a, _type = note_a._type };
                            NoteDiff b = new NoteDiff() { _time = note_b._time, _diffScore = score_b, _type = note_b._type };
                            result_note_data.Add(a);
                            result_note_data.Add(b);
                            prior_color_note[note._type] = last_note;
                        }
                        else
                        {
                            ArrayList dist_data = new ArrayList();
                            float largest_dist = -1;
                            int largest_index = 0;
                            ArrayList diffs = new ArrayList();

                            for(int i = 0; i < note_stack[note._type].Count; i++)
                            {
                                for(int j=0; j< note_stack[note._type].Count; j++)
                                {
                                    Note note_a = (Note)note_stack[note._type][i];
                                    Note note_b = (Note)note_stack[note._type][j];
                                    if (i != j)
                                    {
                                        var dist = GetPhysicalNoteDistance(note_a, note_b);
                                        if (dist >= largest_dist)
                                        {
                                            largest_dist = dist;
                                            largest_index = i;
                                        }
                                    }
                                }
                                diffs.Add(NoteHitDifficulty(prior_note_stack, prior_color_note[note._type], note));
                            }
                            for(int i=0;i < note_stack[note._type].Count; i++)
                            {
                                for(int j=0;j < note_stack[note._type].Count; j++)
                                {
                                    Note note_a = (Note)note_stack[note._type][i];
                                    Note note_b = (Note)note_stack[note._type][j];
                                    if (i != j)
                                    {
                                        var dist = GetPhysicalNoteDistance(note_a, note_b);
                                        if (dist >= largest_dist)
                                        {
                                            largest_dist = dist;
                                            largest_index = i;
                                        }
                                    }
                                }
                                diffs.Add(NoteHitDifficulty(prior_note_stack, prior_color_note[note._type], note));
                            }
                            Note last_note = (Note)note_stack[note._type][largest_index];
                            float sum = 0;
                            foreach(float diff in diffs) { sum += diff; }
                            float sum_avg = sum / (float)(diffs.Count);
                            for(int i = 0; i < diffs.Count; i++)
                            {
                                if(i >= note_stack[note._type].Count) { continue; }
                                Note note_a = (Note)note_stack[note._type][i];
                                float diff = (float)diffs[i];
                                NoteDiff a = new NoteDiff() { _time = note_a._time, _diffScore = diff + sum_avg, _type = note_a._type };
                                result_note_data.Add(a);
                            }
                            prior_color_note[note._type] = last_note;
                        }
                        note_stack[note._type].Clear();
                    }
                    if (note_stack[note._type].Count == 0)
                    {
                        score = NoteHitDifficulty(prior_note_stack, prior_color_note[note._type], note);
                        prior_color_note[note._type] = note;
                        NoteDiff nd = new NoteDiff() { _time = note._time, _diffScore = score, _type = note._type };
                        result_note_data.Add(nd);
                    }
                }
                else
                {
                    // skip because this is a bomb or wall ...
                }
                // pop off old notes more than a beat away
                Note temp = (Note)prior_note_stack[0];
                while (prior_note_stack.Count > 0 && temp._time < note._time - 1) { prior_note_stack.RemoveAt(0); }

            }
            //return { 'red_diffs': red_diffs, 'blue_diffs': blue_diffs };
            writer.Write("Calculated Note Difficulty for " + _notes.Length + " notes."+Environment.NewLine);
            return result_note_data;
        }

        private float NoteHitDifficulty(ArrayList prior_note_stack,Note prior_note,Note curr_note)
        {
            if (prior_note == null) { return (0.05f * GetPositionalDifficulty(curr_note)); } // first note of this color, assume it is stupid easy for note to be hit, less diff score for pos
            float diff_score = 0.08f;
            float dist_physical = GetPhysicalNoteDistance(prior_note, curr_note) * 1.45f; // some more weighting for distance, keep tweaking
            float dist_time = curr_note._time - prior_note._time;

            if (dist_time == 0) { return 0; } // same time is handled later in caller function

            // no point to consider actual prior note because of large aim and recovery time, bonus points for it being hard to reach though and other readability cases
            if (dist_time >= 4) { return ((diff_score + 0.1f) * GetPositionalDifficulty(curr_note) * GetReadabilityDifficulty(prior_note_stack, prior_note, curr_note)); } 
                                                                                                      // does not penalize circle notes, diff is already low enough
            // diff_score += Math.sqrt(Math.pow(dist_physical, 2) + Math.pow(dist_time * 4, 2));
            diff_score += dist_physical;

            // scale diff if note is in weird pos and hard to cut in general
            diff_score *= GetPositionalDifficulty(curr_note);

            // How difficult the next cut is due to flow and position.
            diff_score *= getCutAwkwardness(prior_note_stack, prior_note, curr_note);

            // next would be readibility, another bonus mult, pass in set of prior notes up to one beat ago
            // opposing color will give more score to readability, prior_note_stack <-- use this TODO
            diff_score *= GetReadabilityDifficulty(prior_note_stack, prior_note, curr_note);

            // 0.375 secs per beat at 160 BPM, Base Line
            //diff_score = (float)(Math.Pow(diff_score, 1f + (1f / (dist_time)) * 0.85f));
            diff_score = (float)(Math.Pow(diff_score, 0.0f + (0.375 / (dist_time * _secondsPerBeat)) * 0.87f));

            // scale the score down for circle notes, further scale down if previous note is also circle note
            if (curr_note._cutDirection == 8)
            {
                if (prior_note._cutDirection == 8) { diff_score *= 0.5f; }
            }
            else { diff_score *= 0.75f; }

            return diff_score;
        }

        private float getCutAwkwardness(ArrayList prior_note_stack, Note previous_note, Note curr_note)
        {
            float diff_mult = 1.0f;
            var time_delta = curr_note._time - previous_note._time;
            // need to only apply to cuts at same pos or opposite of prior cut dir TODO
            if (angDiff(getCutAngle(curr_note), getCutAngle(previous_note)) <= 90 && previous_note._time != curr_note._time && !isNoteInFront(curr_note, previous_note))
            {
                diff_mult += 0.15f;
                diff_mult *= (1.0f) * (1f / time_delta);
                diff_mult *= 1.0f + (angDiff(getCutAngle(curr_note), getCutAngle(previous_note))) / 90;
            }
            else if (angDiff(getCutAngle(curr_note), getCutAngle(previous_note)) <= 45 && time_delta<0.34 && isNoteInFront(curr_note, previous_note))
            {
                //diff_mult += 0.15f;           
                diff_mult *= (time_delta / 1.0f); // for those weird 1/4 space notes that flow and such
            }

            return diff_mult;
        }

        private float GetPositionalDifficulty(Note note)
        {
            float bonus_mult = 1.0f;
            if (note._type == 0)
            {
                bonus_mult *= 1 + Math.Max(0.0f, note._lineIndex - 2) * .05f; // .05 if col 3, .10 if col 4, else 0
                                                                            // bonus if cut direction is toward center from hard edge
                if (note._lineIndex > 1 && note._cutDirection == 2)
                {
                    bonus_mult *= 1.05f;
                }
                else if (note._lineIndex > 1 && (note._cutDirection == 4 || note._cutDirection == 6))
                {
                    bonus_mult *= 1.025f;
                }
            }
            else if (note._type == 1)
            {
                bonus_mult *= 1 + Math.Max(0.0f, 2 - note._lineIndex) * .05f;
                if (note._lineIndex < 2 && note._cutDirection == 3)
                {
                    bonus_mult *= 1.05f;
                }
                else if (note._lineIndex < 2 && (note._cutDirection == 5 || note._cutDirection == 7))
                {
                    bonus_mult *= 1.025f;
                }
            }

            // bonus from down cuts on top
            if (note._lineLayer == 2)
            {
                if (note._cutDirection == 1)
                {
                    bonus_mult *= 1.05f;
                }
                else if (note._cutDirection == 6 || note._cutDirection == 7)
                {
                    bonus_mult *= 1.025f;
                }
                bonus_mult *= 1.01f; // top layer is harder to hit, compare to bot and mid at least
            }
            return bonus_mult;
        }

        private float GetReadabilityDifficulty(ArrayList prior_note_stack, Note prior_note, Note curr_note)
        {
            float diff_mult = 0.0f;
            for(int i = 0; i < prior_note_stack.Count; i++)
            {
                Note a = (Note)prior_note_stack[i];
                float time_delta = curr_note._time - a._time;
                if (time_delta>1.0f || time_delta==0.0f) { continue; }
                if(a._lineIndex==curr_note._lineIndex && a._lineLayer == curr_note._lineLayer) // Same pos
                {
                    if (a._type == curr_note._type) { diff_mult += (0.5f * (1.0f / time_delta)); } // same color note
                    else{ diff_mult += 0.7f * (1.0f / time_delta); } // opposite color note
                }
                else if (a._lineLayer==1 && curr_note._lineLayer==1)
                {
                    if ((a._lineIndex==1 || a._lineIndex==2) && Math.Abs(a._lineIndex - curr_note._lineIndex) <= 1) // layer 1 and off by 1 in index
                    {
                        if (a._type == curr_note._type) { diff_mult += (0.2f * (1.0f / time_delta)); } // same color note
                        else { diff_mult += 0.3f * (1.0f / time_delta); } // opposite color note
                    }
                }
                else if (a._lineLayer == 1 && curr_note._lineLayer == 0)
                {
                    if ((a._lineIndex == 1 || a._lineIndex == 2) && Math.Abs(a._lineIndex - curr_note._lineIndex) <= 1) // layer 1 and index 1,2 and off by 1 in index
                    {
                        if (a._type == curr_note._type) { diff_mult += (0.1f * (1.0f / time_delta)); } // same color note
                        else { diff_mult += 0.2f * (1.0f / time_delta); } // opposite color note
                    }
                }
            }
            return 1.0f+diff_mult;
        }

        private float GetPhysicalNoteDistance(Note prior_note, Note curr_note)
        {
            var prior_pos = getNoteExitPoint(prior_note);
            var curr_pos = getNoteEntryPoint(curr_note);
            float dist_physical = (float)(Math.Sqrt(Math.Pow(prior_pos.x - curr_pos.x, 2f) + Math.Pow(prior_pos.y - curr_pos.y, 2f)));
            return dist_physical;
        }

        // Helpers for the helpers, little elves...
        private Vector2 getNoteExitPoint(Note note)
        {
            // TODO error handling
            float x_pos = note._lineIndex * 0.5f, y_pos = note._lineLayer * 0.5f;
            switch (note._cutDirection)
            {
                case 0:
                    y_pos += 0.5f;
                    break;
                case 1:
                    y_pos -= 0.5f;
                    break;
                case 2:
                    x_pos -= 0.5f;
                    break;
                case 3:
                    x_pos += 0.5f;
                    break;
                case 4:
                    x_pos -= 0.25f;
                    y_pos += 0.25f;
                    break;
                case 5:
                    x_pos += 0.25f;
                    y_pos += 0.25f;
                    break;
                case 6:
                    x_pos -= 0.25f;
                    y_pos -= 0.25f;
                    break;
                case 7:
                    x_pos += 0.25f;
                    y_pos -= 0.25f;
                    break;
                default:
                    break;
            }
            Vector2 pos = new Vector2(x_pos, y_pos);
            return pos;
        }

        // identical to getNoteExitPoint, just signs are flipped on switch TODO COMBINE
        private Vector2 getNoteEntryPoint(Note note)
        {
            // TODO error handling
            float x_pos = note._lineIndex * 0.5f, y_pos = note._lineLayer * 0.5f;
            // diagonal not handled, no dir block makes no sense;
            switch (note._cutDirection)
            {
                case 0:
                    y_pos -= 0.5f;
                    break;
                case 1:
                    y_pos += 0.5f;
                    break;
                case 2:
                    x_pos += 0.5f;
                    break;
                case 3:
                    x_pos -= 0.5f;
                    break;
                case 4:
                    x_pos += 0.25f;
                    y_pos -= 0.25f;
                    break;
                case 5:
                    x_pos -= 0.25f;
                    y_pos -= 0.25f;
                    break;
                case 6:
                    x_pos += 0.25f;
                    y_pos += 0.25f;
                    break;
                case 7:
                    x_pos -= 0.25f;
                    y_pos += 0.25f;
                    break;
                default:
                    break;
            }
            Vector2 pos = new Vector2(x_pos, y_pos);
            return pos;
        }

        private float angDiff(float a, float b)
        {
            float res = Math.Abs(a - b);
            if (res > 180.0f) { res = Math.Abs(res - 360.0f); }
            return res;
        }

        private float getCutAngle(Note n)
        {
            Vector2 exit = getNoteExitPoint(n);
            Vector2 entry = getNoteEntryPoint(n);
            float ang = getAngle(exit.x - entry.x, exit.y - entry.y);
            return ang;
        }

        // 0 will be up, 90 right, 180 down, 270 left
        private float getAngle(float dx, float dy)
        {
            var ang = Math.Atan2(dy, dx) * 180f / Math.PI;
            if (ang < 0) { ang += 360f; }
            return (float)ang;
        }

        private Note getNextNote(Note[] note_list, int type, int index)
        {
            for (int itr = index + 1; itr < note_list.Length; itr++)
            {
                Note n = (Note)note_list[itr];
                if (n._type == type) { return n; }
            }
            return null;
        }

        private bool isNoteInFront(Note prior_note, Note curr_note)
        {
            switch (prior_note._cutDirection)
            {
                case 0:
                    return isNoteUp(prior_note, curr_note);
                    break;
                case 1:
                    return isNoteDown(prior_note, curr_note);
                    break;
                case 2:
                    return isNoteLeft(prior_note, curr_note);
                    break;
                case 3:
                    return isNoteRight(prior_note, curr_note);
                    break;
                case 4:
                    return (isNoteUp(prior_note, curr_note) && isNoteLeft(prior_note, curr_note));
                    break;
                case 5:
                    return (isNoteUp(prior_note, curr_note) && isNoteRight(prior_note, curr_note));
                    break;
                case 6:
                    return (isNoteDown(prior_note, curr_note) && isNoteLeft(prior_note, curr_note));
                    break;
                case 7:
                    return (isNoteDown(prior_note, curr_note) && isNoteRight(prior_note, curr_note));
                    break;
                case 8:
                    return true;
                    break;
                default:
                    return true;
                    break;
            }
        }

        private bool isNoteUp(Note a, Note b)
        {
            return (a._lineLayer > b._lineLayer);
        }
        private bool isNoteDown(Note a, Note b)
        {
            return (a._lineLayer < b._lineLayer);
        }
        private bool isNoteLeft(Note a, Note b)
        {
            return (a._lineIndex < b._lineIndex);
        }
        private bool isNoteRight(Note a, Note b)
        {
            return (a._lineIndex > b._lineIndex);
        }
    }


}