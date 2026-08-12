using BepInEx;
using LitJson;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using static gorillasounds.Source.Plugin;

namespace gorillasounds.Source
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        Rect r = new Rect(20, 20, 400, 400);
        Rect r2 = new Rect(20, 420, 400, 400);
        AudioSource src = null;
        Vector2 scrollpos = Vector2.zero;
        public int selected;
        public bool showing;
        bool s;
        public class Song
        {
            public string name;
            public string path;
            public AudioClip clip;
        }
        public int currentSong;
        public List<Song> songs = new List<Song>();
        bool playing;
        public Stack<int> prevSongs = new Stack<int>();
        public Stack<int> shuffledSongs = new Stack<int>();
        bool shuffling;
        // i honestly dont want to change this I know its redundant
        Dictionary<string, string> songNameOverrides = new Dictionary<string, string>()
        {
            ["monkeneedtoswing"] = "Forest Music",
            ["ForestSpeakerAudioSrc_BackupCampfireSong"] = "Campfire Song",
        };
        Dictionary<string, string> overridenSongs = new Dictionary<string, string>();
        Dictionary<string, AudioClip> savedClips = new Dictionary<string, AudioClip>();
        void Start()
        {
            src = gameObject.AddComponent<AudioSource>();
            if (File.Exists(Paths.GameRootPath + "\\GS Files\\save.json")) overridenSongs = JsonMapper.ToObject<Dictionary<string, string>>(File.ReadAllText(Paths.GameRootPath + "\\GS Files\\save.json"));
            if (overridenSongs == null)
            {
                overridenSongs = new Dictionary<string, string>();
            }
            if (!Directory.Exists(Paths.GameRootPath + "\\GS Files\\Sounds")) Directory.CreateDirectory(Paths.GameRootPath + "\\GS Files\\Sounds");
            foreach (string filePath in Directory.GetFiles(Paths.GameRootPath + "\\GS Files\\Sounds").Where(x => x.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)))
            {
                Song add = new Song { name = Path.GetFileNameWithoutExtension(filePath), path = filePath };
                StartCoroutine(downloadAudioFile(filePath, add));
                songs.Add(add);
                
            }
            src.clip = songs[0]?.clip;

        }
        void OnDestroy()
        {
            File.WriteAllText(Paths.GameRootPath + "\\GS Files\\save.json", JsonMapper.ToJson(overridenSongs));
        }
        void Update()
        {
            for (int i = 0; i < overridenSongs.Keys.Count; i++)
            {
                string[] keys = overridenSongs.Keys.ToArray();
                if (!savedClips.ContainsKey(keys[i]) && songs.Select(s => s.name).ToArray().Contains(overridenSongs[keys[i]]) && MusicManager.Instance.activeSources.Select(s => s.audioSource.name == keys[i]).ToArray().Length > 0)
                {
                    ReplaceClip(MusicManager.Instance.activeSources.Where(s => s.audioSource.name == keys[i]).ToArray()[0].audioSource, songs.Where(s => s.name == overridenSongs[keys[i]]).ToArray()[0]);
                }
            }
            foreach (MusicSource m in MusicManager.Instance.activeSources)
            {
                if (m.audioSource.clip == null)
                {
                    ResetClip(m.audioSource);
                }
            }
            try
            {
                if (playing && !src.isPlaying)
                {
                    prevSongs.Push(currentSong);
                    if (!shuffling)
                    {
                        currentSong++;
                        currentSong %= songs.Count;
                    }
                    else
                    {
                        currentSong = shuffledSongs.Pop();
                        if (shuffledSongs.Count <= 0)
                        {
                            Shuffle();
                        }
                    }
                    src.clip = songs[currentSong].clip;
                    src.Play();
                }
                if (Keyboard.current.f7Key.isPressed && s)
                {
                    showing = !showing;
                    s = false;
                }
                else if (!Keyboard.current.f7Key.isPressed)
                {
                    s = true;
                }
            }
            catch (Exception e)
            {
                if (e.ToString().ToLower().Contains("error"))
                {
                    Debug.LogError(e);
                }
            }
        }

        void OnGUI()
        {
            if (showing) 
            { 
                r = GUI.Window(0, r, Window, "Gorilla Sounds");
                r2 = GUI.Window(1, r2, Window, "Gorilla Sounds Replacer");
            }
        }
        void Shuffle()
        {
            foreach (Song s in songs)
            {
                GrabRandom(shuffledSongs, 0, songs.Count);
            }
        }
        void GrabRandom(Stack<int> stack, int min, int max)
        {
            int rng = UnityEngine.Random.Range(min, max);
            if (stack.Contains(rng))
            {
                GrabRandom(stack, min, max);
            }
            else stack.Push(rng);
        }
        void Window(int id)
        {
            if (id == 0)
            {
                if (GUI.Button(new Rect(220, 350, 40, 20), ">"))
                {
                    prevSongs.Push(currentSong);
                    if (!shuffling)
                    {
                        currentSong++;
                        currentSong %= songs.Count;
                    }
                    else
                    {
                        currentSong = shuffledSongs.Pop();
                        if (shuffledSongs.Count <= 0)
                        {
                            Shuffle();
                        }
                    }
                    src.Stop();
                    src.clip = songs[currentSong].clip;
                    src.Play();
                    playing = true;
                }
                if (GUI.Button(new Rect(140, 350, 40, 20), "<"))
                {
                    if (prevSongs.Count > 0) currentSong = prevSongs.Pop();
                    src.Stop();
                    src.clip = songs[currentSong].clip;
                    src.Play();
                    playing = true;
                }
                if (GUI.Button(new Rect(100, 350, 40, 20), "~"))
                {
                    shuffledSongs.Clear();
                    Shuffle();
                    shuffling = true;
                    src.Stop();
                    currentSong = shuffledSongs.Pop();
                    src.clip = songs[currentSong].clip;
                    src.Play();
                    playing = true;
                }
                if (GUI.Button(new Rect(260, 350, 40, 20), "(R)"))
                {
                    songs.Clear();
                    foreach (string filePath in Directory.GetFiles(Paths.GameRootPath + "\\GS Files\\Sounds").Where(x => x.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)))
                    {
                        Song add = new Song { name = Path.GetFileNameWithoutExtension(filePath), path = filePath };
                        StartCoroutine(downloadAudioFile(filePath, add));
                        songs.Add(add);
                    }
                }
                if (GUI.Button(new Rect(180, 350, 40, 20), "v"))
                {
                    playing = !playing;
                    if (playing)
                    {
                        src.clip = songs[currentSong].clip;
                        src.Play();
                    }
                    else src.Pause();
                }
                if (GUI.Button(new Rect(180, 370, 40, 20), "#"))
                {
                    playing = false;
                    shuffling = false;
                    src.Stop();
                    currentSong = 0;
                }
                try
                {
                    GUI.Label(new Rect(165, 330, 80, 20), FormatTime(src.time) + " / " + FormatTime(songs[currentSong].clip.length));
                }
                catch (Exception e)
                {
                    if (e.ToString().ToLower().Contains("error"))
                    {
                        Debug.LogError(e);
                    }
                }
                scrollpos = GUI.BeginScrollView(new Rect(110, 50, 220, 270), scrollpos, new Rect(0, 0, 220, 270), false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
                for (int i = 0; i < songs.Count; i++)
                {
                    if (currentSong == i && playing) GUI.Label(new Rect(-50, i * 20, 220, 20), "(playing)");
                    if (selected == i && !playing) GUI.Label(new Rect(-50, i * 20, 220, 20), "(selected)");
                    GUI.Box(new Rect(0, i * 20, 200, 20), "");
                    
                    GUI.Label(new Rect(0, i * 20, 160, 20), songs[i].name);
                    if (GUI.Button(new Rect(180, i * 20, 20, 20), ">"))
                    {
                        currentSong = i;
                        src.Stop();
                        src.clip = songs[currentSong].clip;
                        src.Play();
                        playing = true;
                    }
                    if (GUI.Button(new Rect(160, i * 20, 20, 20), "-"))
                    {
                        selected = i;
                    }
                }
                GUI.EndScrollView();
            }
            else
            {
                for (int i = 0; i < MusicManager.Instance.activeSources.Count; i++)
                {
                    string sound = MusicManager.Instance.activeSources.ToArray()[i].name;
                    if (GUI.Button(new Rect(370, 20 + i * 20, 20, 20), ""))
                    {
                        ReplaceClip(MusicManager.Instance.activeSources.ToArray()[i].audioSource, songs[selected]);
                    }
                    if (GUI.Button(new Rect(350, 20 + i * 20, 20, 20), "R"))
                    {
                        ResetClip(MusicManager.Instance.activeSources.ToArray()[i].audioSource);   
                    }
                    if (songNameOverrides.ContainsKey(sound.Trim())) sound = songNameOverrides[sound.Trim()];
                    GUI.Label(new Rect(10, 20 + i * 20, 400, 20), $"replace {sound} with selected");
                }
            }
            GUI.DragWindow();
        }
        public void ReplaceClip(AudioSource source, Song song)
        {
            if (savedClips.ContainsKey(source.name)) savedClips.Remove(source.name);
            savedClips.Add(source.name, source.clip);
            if (overridenSongs.ContainsKey(source.name) && overridenSongs[source.name] != song.name) overridenSongs.Remove(source.name);
            else if (!overridenSongs.ContainsKey(source.name))
            overridenSongs.Add(source.name, song.name);
            source.clip = song.clip;
        }
        public void ResetClip(AudioSource source)
        {
            if (savedClips.ContainsKey(source.name)) 
            { 
                source.clip = savedClips[source.name];
                overridenSongs.Remove(source.name);
            }
        }
        string FormatTime(float t)
        {
            TimeSpan ts = TimeSpan.FromSeconds(t);
            return ts.ToString(@"mm\:ss");
        }

        // hi this is cody, i optimized support for .wav & .ogg :)
        IEnumerator downloadAudioFile(string path, Song s)
        {
            AudioType audioType = Path.GetExtension(path).ToLower() switch
            {
                ".wav" => AudioType.WAV,
                ".ogg" => AudioType.OGGVORBIS,
                ".mp3" => AudioType.MPEG,
                _ => AudioType.UNKNOWN
            };
        
            UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip("file:///" + path, audioType);
            yield return req.SendWebRequest();
        
            s.clip = DownloadHandlerAudioClip.GetContent(req);
        }
    }
}
