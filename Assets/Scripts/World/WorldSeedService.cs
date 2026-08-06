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
        // ─── Persistence ────────────────────────────────────────────────
        private const string PlayerPrefsKey = "Willowstead.WorldSeed";

        // Default value used when the game has never had a seed stored. 0 keeps
        // the ProceduralGridGenerator on its legacy behaviour (offset = 0, so
        // the maths stays bit-identical to the pre-seed codebase).
        public const int DefaultSeed = 0;

        // ─── Singleton ──────────────────────────────────────────────────
        public static WorldSeedService Instance { get; private set; }

        /// <summary>
        /// The seed currently driving world generation. Set through
        /// <see cref="SetSeed"/>; assigned back from PlayerPrefs automatically.
        /// </summary>
        public int CurrentSeed { get; private set; }

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
                    // Knuth multiplicative hash on the user seed → stable, large,
                    // signed offset that hides any "seed = small int" pattern from
                    // the integer-hash functions in ProceduralGridGenerator.
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

        // ─── Bootstrap ──────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[WorldSeedService]");
            DontDestroyOnLoad(go);
            go.AddComponent<WorldSeedService>();
        }

        // ─── Lifecycle ──────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Pull the previous-session seed if one was saved. Treat any stored
            // value as user-provided so the World Setup UI doesn't re-prompt on
            // the next launch — the player already made a choice before.
            if (PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                CurrentSeed = PlayerPrefs.GetInt(PlayerPrefsKey, DefaultSeed);
                LastSeedWasUserProvided = true;
            }
            else
            {
                // On a fresh install (no PlayerPrefs key yet) auto-roll a real seed
                // so ProceduralGridGenerator's first Start-time generation isn't
                // offset=0. Without this, the world renders once with DefaultSeed
                // terrain and then snaps to the player's later pick on Create.
                SetSeed(GenerateRandomSeed(), userProvided: false);
                LastSeedWasUserProvided = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── Public API ─────────────────────────────────────────────────

        /// <summary>
        /// Applies a new seed and fires <see cref="OnSeedChanged"/>. Pass
        /// <paramref name="userProvided"/> = true when the value was typed
        /// by the player (or set via dev console); false when auto-generated.
        /// Persists through PlayerPrefs across restarts.
        /// </summary>
        public void SetSeed(int seed, bool userProvided)
        {
            CurrentSeed = seed;
            LastSeedWasUserProvided = userProvided;
            PlayerPrefs.SetInt(PlayerPrefsKey, seed);
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
