// Developer console — compiled out of release builds.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Willowstead.Debugging
{
    /// <summary>
    /// Abstract base for every developer-console command. Concrete commands
    /// set their <see cref="Id"/>, <see cref="Help"/> text, and execute logic
    /// in <see cref="Run"/>. Commands are registered via
    /// <see cref="DevConsole.Register"/> from a class's static initializer or
    /// from the console's own <c>RuntimeInitializeOnLoadMethod</c> bootstrap.
    /// </summary>
    public abstract class DevConsoleCommand
    {
        /// <summary>The lowercase command name, e.g. "give" or "fps".</summary>
        public abstract string Id { get; }

        /// <summary>One-line description, surfaced by the <c>help</c> command.</summary>
        public abstract string Help { get; }

        /// <summary>Execute the command. Output goes through the console's Print* API.</summary>
        public abstract void Run(DevConsole ctx, string[] args);
    }
}
#endif
