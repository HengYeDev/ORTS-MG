﻿// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Processes;
using Orts.Common;
using System;
using System.Diagnostics;
using System.Threading;

namespace Orts.ActivityRunner.Viewer3D.Processes
{
    public class UpdaterProcess
    {
        public readonly Profiler Profiler = new Profiler("Updater");
        readonly ProcessState State = new ProcessState("Updater");
        readonly Game Game;
        readonly Thread Thread;
        readonly WatchdogToken WatchdogToken;

        public GameComponentCollection GameComponents { get; } = new GameComponentCollection();

        public UpdaterProcess(Game game)
        {
            Game = game;
            Thread = new Thread(UpdaterThread);
            WatchdogToken = new WatchdogToken(Thread);
        }

        public void Start()
        {
            Game.WatchdogProcess.Register(WatchdogToken);
            Thread.Start();
        }

        public void Stop()
        {
            Game.WatchdogProcess.Unregister(WatchdogToken);
            State.SignalTerminate();
        }

        public void WaitTillFinished()
        {
            State.WaitTillFinished();
        }

        void UpdaterThread()
        {
            Profiler.SetThread();
            Game.SetThreadLanguage();

            while (true)
            {
                // Wait for a new Update() command
                State.WaitTillStarted();
                if (State.Terminated)
                    break;
                try
                {
                    if (!DoUpdate())
                        return;
                }
                finally
                {
                    // Signal finished so RenderProcess can start drawing
                    State.SignalFinish();
                }
            }
        }

        private RenderFrame CurrentFrame;
        private GameTime gameTime;

        //[CallOnThread("Render")]
        internal void StartUpdate(RenderFrame frame, GameTime gameTime)
        {
            Debug.Assert(State.Finished);
            CurrentFrame = frame;
            this.gameTime = gameTime;
            State.SignalStart();
        }

        bool DoUpdate()
        {
            if (Debugger.IsAttached)
            {
                Update();
            }
            else
            {
                try
                {
                    Update();
                }
                catch (Exception error)
                {
                    // Unblock anyone waiting for us, report error and die.
                    State.SignalTerminate();
                    Game.ProcessReportError(error);
                    return false;
                }
            }
            return true;
        }

        //[CallOnThread("Updater")]
        public void Update()
        {
            Profiler.Start();
            try
            {
                WatchdogToken.Ping();
                CurrentFrame.Clear();
                foreach (GameComponent component in GameComponents)
                    if (component.Enabled)
                        component.Update(gameTime);
                if (Game.State != null)
                {
                    Game.State.Update(CurrentFrame, gameTime.TotalGameTime.TotalSeconds, gameTime);
                    CurrentFrame.Sort();
                }
            }
            finally
            {
                Profiler.Stop();
            }
        }
    }
}
