// Developer console concrete commands. Compiled out of release builds.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using Willowstead.Player;

namespace Willowstead.Debugging.Commands
{

    public sealed class HelpCommand : DevConsoleCommand
    {
        public override string Id   => "help";
        public override string Help => "help - List all registered commands";
        public override void Run(DevConsole ctx, string[] args)
        {
            ctx.Print("── Registered commands ──");
            foreach (DevConsoleCommand cmd in DevConsole.GetAllCommands())
            {
                ctx.Print("  " + cmd.Help);
            }
        }
    }

    public sealed class GiveCommand : DevConsoleCommand
    {
        public override string Id   => "give";
        public override string Help => "give <itemName> [count=1] - Add items to player inventory";
        public override void Run(DevConsole ctx, string[] args)
        {
            if (args.Length < 1)
            {
                ctx.PrintError("Usage: give <itemName> [count]. Example: give Carrot_seeds 10");
                return;
            }
            string itemName = args[0];
            int count = 1;
            if (args.Length >= 2 && !int.TryParse(args[1], out count))
            {
                ctx.PrintError($"'{args[1]}' is not a number; defaulting to 1.");
                count = 1;
            }
            var inv = Object.FindAnyObjectByType<Willowstead.Player.InventoryManager>();
            if (inv == null) { ctx.PrintError("No InventoryManager in scene."); return; }
            inv.AddItem(itemName, count);
            ctx.PrintOk($"+ {count} {itemName}");
        }
    }

    public sealed class GoldCommand : DevConsoleCommand
    {
        public override string Id   => "gold";
        public override string Help =>
            "gold [amount] - 'gold' alone prints balance; with N sets gold to N";
        public override void Run(DevConsole ctx, string[] args)
        {
            var inv = Object.FindAnyObjectByType<Willowstead.Player.InventoryManager>();
            if (inv == null) { ctx.PrintError("No InventoryManager in scene."); return; }

            int current = inv.GetItemCount("Gold");
            if (args.Length == 0)
            {
                ctx.Print($"Gold: {current}");
                return;
            }
            if (!int.TryParse(args[0], out int target))
            {
                ctx.PrintError($"'{args[0]}' is not a number.");
                return;
            }
            int delta = target - current;
            if (delta != 0) inv.AddItem("Gold", delta);
            ctx.PrintOk($"Gold set to {target} (was {current}).");
        }
    }

    public sealed class TimeCommand : DevConsoleCommand
    {
        public override string Id   => "time";
        public override string Help =>
            "time <0..1> - Set time of day (0=midnight, 0.5=noon).";
        public override void Run(DevConsole ctx, string[] args)
        {
            if (args.Length < 1)
            {
                ctx.PrintError("Usage: time <0..1>. Example: time 0.5 (noon)");
                return;
            }
            if (!float.TryParse(args[0],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float t))
            {
                ctx.PrintError($"'{args[0]}' is not a number.");
                return;
            }
            var day = Object.FindAnyObjectByType<Willowstead.World.DayNightCycle>();
            if (day == null) { ctx.PrintError("No DayNightCycle in scene."); return; }
            day.SetTime01(t);
            ctx.PrintOk($"Time set to {t:F3}");
        }
    }

    public sealed class DayCommand : DevConsoleCommand
    {
        public override string Id   => "day";
        public override string Help =>
            "day - Advance one full day (fire GridManager.AdvanceDay).";
        public override void Run(DevConsole ctx, string[] args)
        {
            var grid = Willowstead.World.GridManager.Instance;
            if (grid == null) { ctx.PrintError("No GridManager instance."); return; }
            grid.AdvanceDay();
            ctx.PrintOk("Day advanced.");
        }
    }

    public sealed class GrowCommand : DevConsoleCommand
    {
        public override string Id   => "grow";
        public override string Help =>
            "grow - Trigger the midday half-day growth tick for all crops.";
        public override void Run(DevConsole ctx, string[] args)
        {
            var grid = Willowstead.World.GridManager.Instance;
            if (grid == null) { ctx.PrintError("No GridManager instance."); return; }
            grid.AdvanceHalfDayGrowthTick();
            ctx.PrintOk("Half-day growth tick fired.");
        }
    }

    public sealed class ClearCommand : DevConsoleCommand
    {
        public override string Id   => "clear";
        public override string Help =>
            "clear - Clear every slot in inventory (gold untouched).";
        public override void Run(DevConsole ctx, string[] args)
        {
            var inv = Object.FindAnyObjectByType<Willowstead.Player.InventoryManager>();
            if (inv == null) { ctx.PrintError("No InventoryManager in scene."); return; }
            int cleared = 0;
            for (int i = 0; i < inv.slots.Length; i++)
            {
                // InventorySlot is a sibling class in Willowstead.Player (not nested in
                // `using Willowstead.Player;`.
                InventorySlot slot = inv.slots[i];
                if (slot == null || slot.IsEmpty) continue;
                inv.RemoveItem(slot.itemName, slot.quantity);
                cleared++;
            }
            ctx.PrintOk($"Inventory cleared ({cleared} slots emptied).");
        }
    }

    public sealed class TpCommand : DevConsoleCommand
    {
        public override string Id   => "tp";
        public override string Help => "tp <x> <y> - Teleport the player to world coords.";
        public override void Run(DevConsole ctx, string[] args)
        {
            if (args.Length < 2)
            {
                ctx.PrintError("Usage: tp <x> <y>. Example: tp 0 0");
                return;
            }
            if (!float.TryParse(args[0], out float x) || !float.TryParse(args[1], out float y))
            {
                ctx.PrintError("Both x and y must be numbers.");
                return;
            }
            var player = Object.FindAnyObjectByType<Willowstead.Player.PlayerController>();
            if (player == null) { ctx.PrintError("No PlayerController in scene."); return; }

            // Teleporting via Rigidbody2D.position is the canonical path: physics step
            // picks it up and propagates to the transform the same frame.
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.position = new Vector2(x, y);
            else             player.transform.position = new Vector3(x, y, 0f);
            ctx.PrintOk($"Teleported to ({x:F2}, {y:F2}).");
        }
    }    // ─── weather [clear|windy|rainy|random|get] ─────────────────────────
    public sealed class WeatherCommand : DevConsoleCommand
    {
        public override string Id   => "weather";
        public override string Help => "weather [clear|windy [light/moderate/strong]|rainy|random|get|indoors [on|off|toggle]] - Get or set weather; 'indoors' swaps the audio ambience between outdoor loops and the indoor clip.";
        public override void Run(DevConsole ctx, string[] args)
        {
            var weatherCycle = Object.FindAnyObjectByType<Willowstead.World.WeatherCycle>();
            if (weatherCycle == null) { ctx.PrintError("No WeatherCycle in scene."); return; }

            if (args.Length == 0) args = new[] { "get" };
            string target = args[0].ToLowerInvariant();

            if (target == "get")
            {
                ctx.Print($"Current weather: {weatherCycle.CurrentWeather} ({weatherCycle.CurrentIntensity})");
                return;
            }

            switch (target)
            {
                case "clear":
                    weatherCycle.SetWeather(Willowstead.World.WeatherType.Clear);
                    ctx.PrintOk("Weather set to Clear.");
                    break;
                case "windy":
                case "wind":
                    var intensity = ParseWindIntensity(args, 1);
                    weatherCycle.SetWeather(Willowstead.World.WeatherType.Windy, intensity);
                    ctx.PrintOk($"Weather set to Windy ({intensity}).");
                    break;
                case "rainy":
                case "rain":
                    weatherCycle.SetWeather(Willowstead.World.WeatherType.Rainy);
                    ctx.PrintOk("Weather set to Rainy.");
                    break;
                case "indoors":
                case "indoor":
                case "in":
                    ParseIndoorsToggle(args, weatherCycle, ctx);
                    break;
                case "random":
                case "rand":
                    var next = (Willowstead.World.WeatherType)Random.Range(0, System.Enum.GetValues(typeof(Willowstead.World.WeatherType)).Length);
                    weatherCycle.SetWeather(next);
                    ctx.PrintOk($"Weather rolled to: {next}");
                    break;
                default:
                    ctx.PrintError($"Unknown weather state '{args[0]}'. Use: clear, windy [light/moderate/strong], rainy, random, or get.");
                    break;
            }
        }

        private Willowstead.World.WindIntensity ParseWindIntensity(string[] args, int index)
        {
            if (args.Length <= index) return Willowstead.World.WindIntensity.Moderate;
            string s = args[index].ToLowerInvariant();
            return s switch
            {
                "light" => Willowstead.World.WindIntensity.Light,
                "strong" => Willowstead.World.WindIntensity.Strong,
                _ => Willowstead.World.WindIntensity.Moderate,
            };
        }

        /// <summary>
        /// Parse the optional second arg of <c>weather indoors ...</c>. Defaults
        /// to a toggle when no second arg is supplied so <c>weather indoors</c>
        /// reads as "flip the indoor flag, tell me the new state".
        /// </summary>
        private void ParseIndoorsToggle(string[] args, Willowstead.World.WeatherCycle cycle, DevConsole ctx)
        {
            bool? next = null;
            if (args.Length >= 2)
            {
                string s = args[1].ToLowerInvariant();
                if (s == "on" || s == "true" || s == "1") next = true;
                else if (s == "off" || s == "false" || s == "0") next = false;
            }
            bool resolved = next ?? !(cycle != null && Willowstead.World.RainAudio.Instance != null
                ? Willowstead.World.RainAudio.Instance.IsIndoors
                : false);
            cycle.SetIndoors(resolved);
            ctx.PrintOk($"Indoor state: {(resolved ? "indoors" : "outdoors")}.");
        }
    }

    /// <summary>
    /// Save the current world into a manual slot (1..3) or the autosave.
    /// </summary>
    public sealed class SaveCommand : DevConsoleCommand
    {
        public override string Id   => "save";
        public override string Help =>
            "save [1..3|auto] - Capture current world state into the named slot.";
        public override void Run(DevConsole ctx, string[] args)
        {
            var mgr = Willowstead.Persistence.SaveGameManager.Instance;
            if (mgr == null) { ctx.PrintError("No SaveGameManager in scene."); return; }

            string target = (args.Length > 0 ? args[0] : "1").ToLowerInvariant();
            if (target == "auto" || target == "autosave")
            {
                if (mgr.SaveToAutosave()) ctx.PrintOk("Saved to autosave.");
                else ctx.PrintError("Autosave failed; see console.");
                return;
            }
            if (!int.TryParse(target, out int slot) || slot < 1 || slot > Willowstead.Persistence.SaveGameManager.SlotCount)
            {
                ctx.PrintError($"Slot must be 1..{Willowstead.Persistence.SaveGameManager.SlotCount} or 'auto'.");
                return;
            }
            if (mgr.SaveToSlot(slot)) ctx.PrintOk($"Saved into slot {slot}.");
            else ctx.PrintError($"Save slot {slot} failed; see console.");
        }
    }

    public sealed class LoadCommand : DevConsoleCommand
    {
        public override string Id   => "load";
        public override string Help =>
            "load [1..3|auto] - Restore the named slot into the running world.";
        public override void Run(DevConsole ctx, string[] args)
        {
            var mgr = Willowstead.Persistence.SaveGameManager.Instance;
            if (mgr == null) { ctx.PrintError("No SaveGameManager in scene."); return; }

            string target = (args.Length > 0 ? args[0] : "1").ToLowerInvariant();
            bool ok = (target == "auto" || target == "autosave")
                ? mgr.LoadFromAutosave()
                : (int.TryParse(target, out int slot) && slot >= 1 && slot <= Willowstead.Persistence.SaveGameManager.SlotCount
                    && mgr.LoadFromSlot(slot));
            if (ok) ctx.PrintOk($"Loaded {target}.");
            else ctx.PrintError($"Load '{target}' failed; either missing or invalid.");
        }
    }

    public sealed class SavesCommand : DevConsoleCommand
    {
        public override string Id   => "saves";
        public override string Help =>
            "saves - List every save slot with name, seed, playtime, and timestamp.";
        public override void Run(DevConsole ctx, string[] args)
        {
            var mgr = Willowstead.Persistence.SaveGameManager.Instance;
            if (mgr == null) { ctx.PrintError("No SaveGameManager in scene."); return; }

            var slots = mgr.ListSlots();
            ctx.Print("── Save Slots ──");
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (!s.exists)
                {
                    ctx.Print($"  [{(s.slotIndex < 0 ? "auto" : s.slotIndex.ToString())}] <empty>");
                    continue;
                }
                string name = string.IsNullOrEmpty(s.saveName) ? "Untitled" : s.saveName;
                int mins = (int)(s.playTimeSeconds / 60f);
                ctx.Print($"  [{(s.slotIndex < 0 ? "auto" : s.slotIndex.ToString())}] '{name}' seed={s.worldSeed} {mins}min @ {s.saveTimestampUtc}");
            }
        }
    }

    public sealed class DeleteSaveCommand : DevConsoleCommand
    {
        public override string Id   => "delsave";
        public override string Help =>
            "delsave [1..3|auto] - Delete the named save slot.";
        public override void Run(DevConsole ctx, string[] args)
        {
            var mgr = Willowstead.Persistence.SaveGameManager.Instance;
            if (mgr == null) { ctx.PrintError("No SaveGameManager in scene."); return; }

            if (args.Length == 0)
            {
                ctx.PrintError("Usage: delsave [1..3|auto].");
                return;
            }
            string target = args[0].ToLowerInvariant();
            bool ok = (target == "auto" || target == "autosave")
                ? mgr.DeleteAutosave()
                : (int.TryParse(target, out int slot) && mgr.DeleteSlot(slot));
            if (ok) ctx.PrintOk($"Deleted {target}.");
            else ctx.PrintError($"Could not delete {target} (missing or invalid).");
        }
    }

    public sealed class FpsCommand : DevConsoleCommand
    {
        public override string Id   => "fps";
        public override string Help => "fps - Toggle the FPS counter overlay.";
        public override void Run(DevConsole ctx, string[] args) => DevConsole.ToggleFps();
    }

    /// <summary>
    /// Show, set, or randomise the active world seed. Setting flips
    /// <c>WorldSeedService.CurrentSeed</c> and triggers
    /// <c>ProceduralGridGenerator.Regenerate()</c>, which rebuilds every chunk
    /// from scratch with the new seed offset mixed into every hash.
    /// </summary>
    public sealed class SeedCommand : DevConsoleCommand
    {
        public override string Id   => "seed";
        public override string Help =>
            "seed [seed_string | int | random | show] - Show current seed; with value/random sets and regenerates the world.";
        public override void Run(DevConsole ctx, string[] args)
        {
            Willowstead.World.WorldSeedService seedSvc =
                Willowstead.World.WorldSeedService.Instance;
            if (seedSvc == null)
            {
                ctx.PrintError("No WorldSeedService instance present.");
                return;
            }

            if (args.Length == 0)
            {
                string disp = !string.IsNullOrEmpty(seedSvc.CurrentSeedString) ? seedSvc.CurrentSeedString : seedSvc.CurrentSeed.ToString();
                ctx.Print($"Current seed: {disp} (int: {seedSvc.CurrentSeed}, user-provided: {seedSvc.LastSeedWasUserProvided})");
                return;
            }

            string target = args[0];
            string lower = target.ToLowerInvariant();
            if (lower == "show")
            {
                string disp = !string.IsNullOrEmpty(seedSvc.CurrentSeedString) ? seedSvc.CurrentSeedString : seedSvc.CurrentSeed.ToString();
                ctx.Print($"Current seed: {disp} (int: {seedSvc.CurrentSeed}, user-provided: {seedSvc.LastSeedWasUserProvided})");
                return;
            }

            int previous = seedSvc.CurrentSeed;
            if (lower == "random" || lower == "rand")
            {
                int newSeed = seedSvc.GenerateRandomSeed();
                seedSvc.SetSeed(newSeed, userProvided: true);
            }
            else
            {
                seedSvc.SetSeed(target, userProvided: true);
            }

            Willowstead.World.ProceduralGridGenerator gen =
                Willowstead.World.ProceduralGridGenerator.Instance;
            if (gen != null) gen.Regenerate();

            string currentDisp = !string.IsNullOrEmpty(seedSvc.CurrentSeedString) ? seedSvc.CurrentSeedString : seedSvc.CurrentSeed.ToString();
            ctx.PrintOk($"Seed changed: {previous} -> {currentDisp} (int: {seedSvc.CurrentSeed}). World regenerated.");
        }
    }

    public sealed class GodCommand : DevConsoleCommand
    {
        public override string Id => "god";
        public override string Help => "god - Toggles god mode (invulnerable + infinite stamina)";
        public override void Run(DevConsole ctx, string[] args)
        {
            var stats = Object.FindAnyObjectByType<PlayerStats>();
            if (stats == null) { ctx.PrintError("No PlayerStats found in scene."); return; }
            stats.GodMode = !stats.GodMode;
            ctx.PrintOk($"God mode: {(stats.GodMode ? "ON" : "OFF")}");
        }
    }

    public sealed class HealCommand : DevConsoleCommand
    {
        public override string Id => "heal";
        public override string Help => "heal [amount=100] - Restores player health";
        public override void Run(DevConsole ctx, string[] args)
        {
            var stats = Object.FindAnyObjectByType<PlayerStats>();
            if (stats == null) { ctx.PrintError("No PlayerStats found in scene."); return; }
            float amt = 100f;
            if (args.Length > 0 && float.TryParse(args[0], out float custom)) amt = custom;
            stats.Heal(amt);
            ctx.PrintOk($"Healed {amt}. Health: {stats.CurrentHealth}/{stats.MaxHealth}");
        }
    }

    public sealed class DamageCommand : DevConsoleCommand
    {
        public override string Id => "damage";
        public override string Help => "damage <amount> - Deals damage to player health";
        public override void Run(DevConsole ctx, string[] args)
        {
            var stats = Object.FindAnyObjectByType<PlayerStats>();
            if (stats == null) { ctx.PrintError("No PlayerStats found in scene."); return; }
            float amt = 20f;
            if (args.Length > 0 && float.TryParse(args[0], out float custom)) amt = custom;
            stats.TakeDamage(amt);
            ctx.PrintOk($"Took {amt} damage. Health: {stats.CurrentHealth}/{stats.MaxHealth}");
        }
    }

    public sealed class StaminaCommand : DevConsoleCommand
    {
        public override string Id => "stamina";
        public override string Help => "stamina [amount=100] - Restores player stamina";
        public override void Run(DevConsole ctx, string[] args)
        {
            var stats = Object.FindAnyObjectByType<PlayerStats>();
            if (stats == null) { ctx.PrintError("No PlayerStats found in scene."); return; }
            float amt = 100f;
            if (args.Length > 0 && float.TryParse(args[0], out float custom)) amt = custom;
            stats.RestoreStamina(amt);
            ctx.PrintOk($"Restored {amt} stamina. Stamina: {stats.CurrentStamina}/{stats.MaxStamina}");
        }
    }
}
#endif
