using System;
using UnityEngine;

namespace Willowstead.World
{
    /// <summary>
    /// Central holder for the active world's seed. ProceduralGridGenerator and any
    /// other deterministic generator should fetch their variation offset from here
    /// at world-start (e.g. inside Awake) and subscribe to <see cref="OnSeedChanged"/>
    /// to regenerate when the player picks a new seed at the World Setup panel or
    /// via the dev console.
    ///
    /// Survives scene reloads via <see cref="DontDestroyOnLoad"/>. Persists across
    /// game restarts via PlayerPrefs. Self-bootstraps at the first scene load so
    /// the seed is available before any world generator's Awake runs.
    /// </summary>
    public class WorldSeedService : MonoBehaviour
    {
        private const string PlayerPrefsKey = "Willowstead.WorldSeed";
        private const string PlayerPrefsStringKey = "Willowstead.WorldSeedString";

        public const int DefaultSeed = 0;

        public static WorldSeedService Instance { get; private set; }

        /// <summary>
        /// The seed currently driving world generation. Set through
        /// <see cref="SetSeed"/>; assigned back from PlayerPrefs automatically.
        /// </summary>
        public int CurrentSeed { get; private set; }

        /// <summary>
        /// The raw string representation of the current seed (e.g. "Willowstead", "12345").
        /// </summary>
        public string CurrentSeedString { get; private set; } = string.Empty;

        /// <summary>
        /// Mixable offset derived from <see cref="CurrentSeed"/>. Add this to any
        /// position hash or to Perlin-noise coordinates before sampling so two
        /// seeds yield visibly different terrain and decor.
        /// </summary>
        public int SeedOffset
        {
            get
            {
                unchecked
                {
                    return (int)(CurrentSeed * unchecked((int)0x9E3779B1));
                }
            }
        }

        /// <summary>
        /// True if the active seed was explicitly typed by the player (or supplied
        /// via dev console); false if it was auto-generated because no value had
        /// ever been stored. Drives whether WorldSetupUI re-prompts on launch.
        /// </summary>
        public bool LastSeedWasUserProvided { get; private set; }

        /// <summary>Fires whenever the seed flips. Subscribers should regenerate.</summary>
        public event Action<int> OnSeedChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[WorldSeedService]");
            DontDestroyOnLoad(go);
            go.AddComponent<WorldSeedService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                CurrentSeed = PlayerPrefs.GetInt(PlayerPrefsKey, DefaultSeed);
                CurrentSeedString = PlayerPrefs.GetString(PlayerPrefsStringKey, CurrentSeed.ToString());
                LastSeedWasUserProvided = true;
            }
            else
            {
                SetSeed(GenerateRandomSeed(), userProvided: false);
                LastSeedWasUserProvided = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Converts any input string (numeric, alphanumeric, words) into a deterministic 32-bit integer seed.
        /// If the string is a valid integer, it parses it directly; otherwise it computes a deterministic FNV-1a hash.
        /// </summary>
        public static int ParseSeedString(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;
            string trimmed = raw.Trim();
            if (int.TryParse(trimmed, System.Globalization.NumberStyles.Integer,
                             System.Globalization.CultureInfo.InvariantCulture, out int parsedInt))
            {
                return parsedInt;
            }

            // FNV-1a 32-bit hash for deterministic alphanumeric seed support across platforms/sessions
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < trimmed.Length; i++)
                {
                    hash ^= trimmed[i];
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }

        /// <summary>
        /// Sets a string seed (accepting numbers or words/letters).
        /// </summary>
        public void SetSeed(string seedString, bool userProvided)
        {
            string cleanStr = string.IsNullOrWhiteSpace(seedString) ? string.Empty : seedString.Trim();
            int seedInt = ParseSeedString(cleanStr);
            CurrentSeed = seedInt;
            CurrentSeedString = string.IsNullOrEmpty(cleanStr) ? seedInt.ToString() : cleanStr;
            LastSeedWasUserProvided = userProvided;

            PlayerPrefs.SetInt(PlayerPrefsKey, CurrentSeed);
            PlayerPrefs.SetString(PlayerPrefsStringKey, CurrentSeedString);
            PlayerPrefs.Save();

            try { OnSeedChanged?.Invoke(CurrentSeed); }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }

        /// <summary>
        /// Applies a new seed integer and fires <see cref="OnSeedChanged"/>. Pass
        /// <paramref name="userProvided"/> = true when the value was typed
        /// by the player (or set via dev console); false when auto-generated.
        /// Persists through PlayerPrefs across restarts.
        /// </summary>
        public void SetSeed(int seed, bool userProvided)
        {
            CurrentSeed = seed;
            CurrentSeedString = seed.ToString();
            LastSeedWasUserProvided = userProvided;
            PlayerPrefs.SetInt(PlayerPrefsKey, seed);
            PlayerPrefs.SetString(PlayerPrefsStringKey, CurrentSeedString);
            PlayerPrefs.Save();
            try { OnSeedChanged?.Invoke(seed); }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }

        /// <summary>
        /// Picks a fresh 32-bit random seed (any int, can be negative). Returns
        /// the new seed so callers can show it back to the player.
        /// </summary>
        public int GenerateRandomSeed()
        {
            // System.Random gives a full 32-bit range without bias; UnityEngine.Random
            // is global and would couple this to whatever else already called it.
            return new System.Random().Next(int.MinValue, int.MaxValue);
        }

        /// <summary>
        /// Deterministic [0,1) float derived from the active seed + input salt.
        /// Mirrors the formulae in ProceduralGridGenerator so anything that wants
        /// the same seed-driven variation can call this without copying the
        /// maths around.
        /// </summary>
        public float Scrambled01(int salt)
        {
            unchecked
            {
                uint h = (uint)(CurrentSeed * 2654435761u
                                ^ (salt * 1442695040888963407L));
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>
        /// Deterministic non-negative index derived from the active seed + input
        /// salt, modulo <paramref name="modulo"/>. Suitable for picking variants
        /// from an array.
        /// </summary>
        public int ScrambledInt(int salt, int modulo)
        {
            if (modulo <= 0) return 0;
            unchecked
            {
                uint h = (uint)(CurrentSeed * 2654435761u
                                ^ (salt * 1442695040888963407L));
                h ^= h >> 16;
                return (int)(h % (uint)modulo);
            }
        }
    }
}
