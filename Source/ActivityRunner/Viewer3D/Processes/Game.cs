﻿// COPYRIGHT 2013, 2014 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

using Orts.Common;
using Orts.Common.Info;
using Orts.Common.Input;
using Orts.Common.Logging;
using Orts.Settings;
using Orts.View.Xna;

namespace Orts.ActivityRunner.Viewer3D.Processes
{
    /// <summary>
    /// Provides the foundation for running the game.
    /// </summary>
    //[CallOnThread("Render")]
    public class Game : Microsoft.Xna.Framework.Game
    {
        /// <summary>
        /// Gets the <see cref="UserSettings"/> for the game.
        /// </summary>
        public UserSettings Settings { get; private set; }

        /// <summary>
        /// Gets the directory path containing game-specific content.
        /// </summary>
        public string ContentPath { get; private set; }

        /// <summary>
        /// Exposes access to the <see cref="WatchdogProcess"/> for the game.
        /// </summary>
        public WatchdogProcess WatchdogProcess { get; private set; }

        /// <summary>
        /// Exposes access to the <see cref="RenderProcess"/> for the game.
        /// </summary>
        public RenderProcess RenderProcess { get; private set; }

        /// <summary>
        /// Exposes access to the <see cref="UpdaterProcess"/> for the game.
        /// </summary>
        public UpdaterProcess UpdaterProcess { get; private set; }

        /// <summary>
        /// Exposes access to the <see cref="LoaderProcess"/> for the game.
        /// </summary>
        public LoaderProcess LoaderProcess { get; private set; }



        /// <summary>
        /// Exposes access to the <see cref="SoundProcess"/> for the game.
        /// </summary>
        public SoundProcess SoundProcess { get; private set; }

        /// <summary>
        /// Exposes access to the <see cref="WebServer"/> for the game.
        /// </summary>
        public WebServerProcess WebServerProcess { get; private set; }

        /// <summary>
        /// Gets the current <see cref="GameState"/>, if there is one, or <c>null</c>.
        /// </summary>
        public GameState State { get { return States.Count > 0 ? States.Peek() : null; } }

        Stack<GameState> States;

        /// <summary>
        /// Initializes a new instance of the <see cref="Game"/> based on the specified <see cref="UserSettings"/>.
        /// </summary>
        /// <param name="settings">The <see cref="UserSettings"/> for the game to use.</param>
        public Game(UserSettings settings)
        {
            Settings = settings;
            ContentPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "Content");
            Exiting += new System.EventHandler<System.EventArgs>(Game_Exiting);
            WatchdogProcess = new WatchdogProcess(this);
            RenderProcess = new RenderProcess(this);
            UpdaterProcess = new UpdaterProcess(this);
            LoaderProcess = new LoaderProcess(this);
            SoundProcess = new SoundProcess(this);
            WebServerProcess = new WebServerProcess(this);
            States = new Stack<GameState>();
        }

        protected override void BeginRun()
        {
            // At this point, GraphicsDevice is initialized and set up.
            WebServerProcess.Start();
            SoundProcess.Start();
            LoaderProcess.Start();
            UpdaterProcess.Start();
            RenderProcess.Start();
#if !DEBUG
            WatchdogProcess.Start();
#endif
            base.BeginRun();
        }

        protected override void Update(Microsoft.Xna.Framework.GameTime gameTime)
        {
            // The first Update() is called before the window is displayed, with a gameTime == 0. The second is called
            // after the window is displayed.
            if (State == null)
                Exit();
            else
                RenderProcess.Update(gameTime);
            base.Update(gameTime);
        }

        protected override bool BeginDraw()
        {
            if (!base.BeginDraw())
                return false;
            RenderProcess.BeginDraw();
            return true;
        }

        protected override void Draw(Microsoft.Xna.Framework.GameTime gameTime)
        {
            RenderProcess.Draw();
            base.Draw(gameTime);
        }

        protected override void EndDraw()
        {
            RenderProcess.EndDraw();
            base.EndDraw();
        }

        protected override void EndRun()
        {
            base.EndRun();
            WatchdogProcess.Stop();
            RenderProcess.Stop();
            UpdaterProcess.Stop();
            LoaderProcess.Stop();
            SoundProcess.Stop();
            // WebServerProcess.Stop(); Again
            WebServerProcess.Stop();
        }

        private void Game_Exiting(object sender, EventArgs e)
        {
            while (State != null)
                PopState();
        }

        internal void PushState(GameState state)
        {
            state.Game = this;
            States.Push(state);
            Trace.TraceInformation("Game.PushState({0})  {1}", state.GetType().Name, String.Join(" | ", States.Select(s => s.GetType().Name).ToArray()));
        }

        internal void PopState()
        {
            State.Dispose();
            States.Pop();
            Trace.TraceInformation("Game.PopState()  {0}", String.Join(" | ", States.Select(s => s.GetType().Name).ToArray()));
        }

        //[CallOnThread("Loader")]
        internal void ReplaceState(GameState state)
        {
            if (State != null)
            {
                State.Dispose();
                States.Pop();
            }
            state.Game = this;
            States.Push(state);
            Trace.TraceInformation("Game.ReplaceState({0})  {1}", state.GetType().Name, String.Join(" | ", States.Select(s => s.GetType().Name).ToArray()));
        }

        /// <summary>
        /// Updates the calling thread's <see cref="Thread.CurrentUICulture"/> to match the <see cref="Game"/>'s <see cref="Settings"/>.
        /// </summary>
        //[CallOnThread("Render")]
        //[CallOnThread("Updater")]
        //[CallOnThread("Loader")]
        //[CallOnThread("Watchdog")]
        public void SetThreadLanguage()
        {
            if (Settings.Language.Length > 0)
            {
                try
                {
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(Settings.Language);
                }
                catch (CultureNotFoundException) { }
            }
        }

        /// <summary>
        /// Reports an <see cref="Exception"/> to the log file and/or user, exiting the game in the process.
        /// </summary>
        /// <param name="error">The <see cref="Exception"/> to report.</param>
        //[CallOnThread("Render")]
        //[CallOnThread("Updater")]
        //[CallOnThread("Loader")]
        //[CallOnThread("Sound")]
        public void ProcessReportError(Exception error)
        {
            // Turn off the watchdog since we're going down.
            WatchdogProcess.Stop();
            // Log the error first in case we're burning.
            Trace.WriteLine(new FatalException(error));
            // Stop the world!
            Exit();
            // Show the user that it's all gone horribly wrong.
            if (Settings.ShowErrorDialogs)
            {
                string errorSummary = error.GetType().FullName + ": " + error.Message;
                string logFile = Path.Combine(Settings.LoggingPath, Settings.LoggingFilename);
                DialogResult openTracker = MessageBox.Show($"A fatal error has occured and {RuntimeInfo.ProductName} cannot continue.\n\n" +
                        $"    {errorSummary}\n\n" +
                        $"This error may be due to bad data or a bug. You can help improve {RuntimeInfo.ProductName} by reporting this error in our bug tracker at https://github.com/perpetualKid/ORTS-MG/issues and attaching the log file {logFile}.\n\n" +
                        ">>> Click OK to report this error on the GitHub bug tracker <<<",
                        $"{RuntimeInfo.ProductName} {VersionInfo.Version}", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                if (openTracker == DialogResult.OK)
                    Process.Start(new ProcessStartInfo("https://github.com/perpetualKid/ORTS-MG/issues") { UseShellExecute = true });
            }
        }
    }
}
