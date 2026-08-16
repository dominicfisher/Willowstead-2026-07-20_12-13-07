using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Willowstead.Input
{
    public enum KeyAction
    {
        MoveUp,
        MoveDown,
        MoveLeft,
        MoveRight,
        Sprint,
        Inventory,
        Shop,
        Map,
        Chat
    }

    /// <summary>
    /// Central manager for customizable player keybindings.
    /// Supports remapping keys at runtime and persists custom bindings via PlayerPrefs.
    /// </summary>
    public static class KeyRebindingManager
    {
        private const string PrefPrefix = "keybind_";

        private static readonly Dictionary<KeyAction, Key> DefaultBindings = new Dictionary<KeyAction, Key>()
        {
            { KeyAction.MoveUp,    Key.W },
            { KeyAction.MoveDown,  Key.S },
            { KeyAction.MoveLeft,  Key.A },
            { KeyAction.MoveRight, Key.D },
            { KeyAction.Sprint,    Key.LeftShift },
            { KeyAction.Inventory, Key.I },
            { KeyAction.Shop,      Key.P },
            { KeyAction.Map,       Key.M },
            { KeyAction.Chat,      Key.T }
        };

        private static readonly Dictionary<KeyAction, Key> CurrentBindings = new Dictionary<KeyAction, Key>();

        public static event Action OnKeybindingsChanged;

        static KeyRebindingManager()
        {
            LoadBindings();
        }

        public static void LoadBindings()
        {
            CurrentBindings.Clear();
            foreach (var kvp in DefaultBindings)
            {
                string savedKeyStr = PlayerPrefs.GetString(PrefPrefix + kvp.Key.ToString(), string.Empty);
                if (!string.IsNullOrEmpty(savedKeyStr) && Enum.TryParse(savedKeyStr, out Key parsedKey))
                {
                    CurrentBindings[kvp.Key] = parsedKey;
                }
                else
                {
                    CurrentBindings[kvp.Key] = kvp.Value;
                }
            }
        }

        public static Key GetKey(KeyAction action)
        {
            if (CurrentBindings.TryGetValue(action, out Key key))
            {
                return key;
            }
            if (DefaultBindings.TryGetValue(action, out Key defKey))
            {
                return defKey;
            }
            return Key.None;
        }

        public static string GetActionLabel(KeyAction action)
        {
            switch (action)
            {
                case KeyAction.MoveUp:    return "Move Up";
                case KeyAction.MoveDown:  return "Move Down";
                case KeyAction.MoveLeft:  return "Move Left";
                case KeyAction.MoveRight: return "Move Right";
                case KeyAction.Sprint:    return "Sprint";
                case KeyAction.Inventory: return "Inventory";
                case KeyAction.Shop:      return "Shop";
                case KeyAction.Map:       return "World Map";
                case KeyAction.Chat:      return "Chat";
                default:                  return action.ToString();
            }
        }

        public static string GetKeyDisplayName(KeyAction action)
        {
            Key key = GetKey(action);
            return FormatKeyName(key);
        }

        public static string FormatKeyName(Key key)
        {
            switch (key)
            {
                case Key.LeftShift: return "L-Shift";
                case Key.RightShift: return "R-Shift";
                case Key.LeftCtrl: return "L-Ctrl";
                case Key.RightCtrl: return "R-Ctrl";
                case Key.LeftAlt: return "L-Alt";
                case Key.RightAlt: return "R-Alt";
                case Key.UpArrow: return "Up";
                case Key.DownArrow: return "Down";
                case Key.LeftArrow: return "Left";
                case Key.RightArrow: return "Right";
                case Key.Backquote: return "`";
                default: return key.ToString();
            }
        }

        public static void SetKey(KeyAction action, Key newKey)
        {
            if (newKey == Key.None || newKey == Key.Escape) return;
            CurrentBindings[action] = newKey;
            PlayerPrefs.SetString(PrefPrefix + action.ToString(), newKey.ToString());
            PlayerPrefs.Save();
            OnKeybindingsChanged?.Invoke();
        }

        public static void ResetToDefaults()
        {
            foreach (var kvp in DefaultBindings)
            {
                CurrentBindings[kvp.Key] = kvp.Value;
                PlayerPrefs.DeleteKey(PrefPrefix + kvp.Key.ToString());
            }
            PlayerPrefs.Save();
            OnKeybindingsChanged?.Invoke();
        }

        public static bool IsPressed(KeyAction action)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            Key key = GetKey(action);
            if (key == Key.None) return false;
            var control = kb[key];
            return control != null && control.isPressed;
        }

        public static bool WasPressedThisFrame(KeyAction action)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            Key key = GetKey(action);
            if (key == Key.None) return false;
            var control = kb[key];
            return control != null && control.wasPressedThisFrame;
        }

        public static bool WasReleasedThisFrame(KeyAction action)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            Key key = GetKey(action);
            if (key == Key.None) return false;
            var control = kb[key];
            return control != null && control.wasReleasedThisFrame;
        }
    }
}
