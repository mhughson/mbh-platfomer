using Mono8;
using System;

namespace mbh_platformer
{
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            using (var game = new Mono8Game<Game1>())
                game.Run();
        }
    }
}
