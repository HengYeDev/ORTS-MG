﻿// COPYRIGHT 2012, 2013 by the Open Rails project.
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

// #define DEBUG_MULTIPLAYER
// DEBUG flag for debug prints

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation;
using Orts.Simulation.Commanding;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Signalling;
using Orts.Simulation.Track;

namespace Orts.MultiPlayer
{
    public class Message
    {
        public static Message Decode(string m)
        {
#if DEBUG_MULTIPLAYER
            Trace.TraceInformation("MP message received: {0}", m);
#endif
            int index = m.IndexOf(' ');
            string key = m.Substring(0, index);
            if (key == "MOVE") return new MSGMove(m.Substring(index + 1));
            else if (key == "SWITCHSTATES") return new MSGSwitchStatus(m.Substring(index + 1));
            else if (key == "SIGNALSTATES") return new MSGSignalStatus(m.Substring(index + 1));
            else if (key == "TEXT") return new MSGText(m.Substring(index + 1));
            else if (key == "LOCOINFO") return new MSGLocoInfo(m.Substring(index + 1));
            else if (key == "ALIVE") return new MSGAlive(m.Substring(index + 1));
            else if (key == "TRAIN") return new MSGTrain(m.Substring(index + 1));
            else if (key == "PLAYER") return new MSGPlayer(m.Substring(index + 1));
            else if (key == "PLAYERTRAINSW") return new MSGPlayerTrainSw(m.Substring(index + 1));
            else if (key == "ORGSWITCH") return new MSGOrgSwitch(m.Substring(index + 1));
            else if (key == "SWITCH") return new MSGSwitch(m.Substring(index + 1));
            else if (key == "RESETSIGNAL") return new MSGResetSignal(m.Substring(index + 1));
            else if (key == "REMOVETRAIN") return new MSGRemoveTrain(m.Substring(index + 1));
            else if (key == "SERVER") return new MSGServer(m.Substring(index + 1));
            else if (key == "MESSAGE") return new MSGMessage(m.Substring(index + 1));
            else if (key == "EVENT") return new MSGEvent(m.Substring(index + 1));
            else if (key == "UNCOUPLE") return new MSGUncouple(m.Substring(index + 1));
            else if (key == "COUPLE") return new MSGCouple(m.Substring(index + 1));
            else if (key == "GETTRAIN") return new MSGGetTrain(m.Substring(index + 1));
            else if (key == "UPDATETRAIN") return new MSGUpdateTrain(m.Substring(index + 1));
            else if (key == "CONTROL") return new MSGControl(m.Substring(index + 1));
            else if (key == "LOCCHANGE") return new MSGLocoChange(m.Substring(index + 1));
            else if (key == "QUIT") return new MSGQuit(m.Substring(index + 1));
            else if (key == "LOST") return new MSGLost(m.Substring(index + 1));
            else if (key == "AVATAR") return new MSGAvatar(m.Substring(index + 1));
            else if (key == "WEATHER") return new MSGWeather(m.Substring(index + 1));
            else if (key == "AIDER") return new MSGAider(m.Substring(index + 1));
            else if (key == "SIGNALCHANGE") return new MSGSignalChange(m.Substring(index + 1));
            else if (key == "EXHAUST") return new MSGExhaust(m.Substring(index + 1));
            else if (key == "FLIP") return new MSGFlip(m.Substring(index + 1));
            else throw new Exception("Unknown Keyword" + key);
        }

        public virtual void HandleMsg() { Trace.WriteLine("test"); return; }
    }

#region MSGMove
    public class MSGMove : Message
    {
        class MSGMoveItem
        {
            public string user;
            public float speed;
            public float travelled;
            public int num, count;
            public int TileX, TileZ, trackNodeIndex, direction, tdbDir;
            public float X, Z;
            public float Length;
            public MSGMoveItem(string u, float s, float t, int n, int tX, int tZ, float x, float z, int tni, int cnt, int dir, int tDir, float len)
            {
                user = u; speed = s; travelled = t; num = n; TileX = tX; TileZ = tZ; X = x; Z = z; trackNodeIndex = tni; count = cnt; direction = dir; tdbDir = tDir; Length = len;
            }
            public override string ToString()
            {
                return user + " " + speed.ToString(CultureInfo.InvariantCulture) + " " + travelled.ToString(CultureInfo.InvariantCulture) + " " + num + " " +
                    TileX + " " + TileZ + " " + X.ToString(CultureInfo.InvariantCulture) + " " + Z.ToString(CultureInfo.InvariantCulture) + " " + trackNodeIndex + " " +
                    count + " " + direction + " " + tdbDir + " " + Length.ToString(CultureInfo.InvariantCulture);
            }
        }
        List<MSGMoveItem> items;
        public MSGMove(string m)
        {
            m = m.Trim();
            string[] areas = m.Split(' ');
            if (areas.Length % 13 != 0 && !(areas.Length == 1 && areas[0].Length == 0)) //check for correct formatting
            {
                throw new Exception("Parsing error " + m);
            }
            try
            {
                int i = 0;
                items = new List<MSGMoveItem>();
                if (areas.Length > 1)
                    for (i = 0; i < areas.Length / 13; i++)
                        items.Add(new MSGMoveItem(areas[13 * i], float.Parse(areas[13 * i + 1], CultureInfo.InvariantCulture), float.Parse(areas[13 * i + 2], CultureInfo.InvariantCulture), int.Parse(areas[13 * i + 3]),
                            int.Parse(areas[13 * i + 4]), int.Parse(areas[13 * i + 5]), float.Parse(areas[13 * i + 6], CultureInfo.InvariantCulture), float.Parse(areas[13 * i + 7], CultureInfo.InvariantCulture),
                            int.Parse(areas[13 * i + 8]), int.Parse(areas[13 * i + 9]), int.Parse(areas[13 * i + 10]), int.Parse(areas[13 * i + 11]), float.Parse(areas[13 * i + 12], CultureInfo.InvariantCulture)));
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        static Dictionary<int, int> MissingTimes;

        //a train is missing, but will wait for 10 messages then ask
        static bool CheckMissingTimes(int TNumber)
        {
            if (MissingTimes == null) MissingTimes = new Dictionary<int, int>();
            try
            {
                if (MissingTimes[TNumber] < 10) { MissingTimes[TNumber]++; return false; }
                else { MissingTimes[TNumber] = 0; return true; }
            }
            catch (Exception)
            {
                MissingTimes.Add(TNumber, 1);
                return false;
            }

        }

        public MSGMove()
        {
        }

        public void AddNewItem(string u, Train t)
        {
            if (items == null) items = new List<MSGMoveItem>();
            items.Add(new MSGMoveItem(u, t.SpeedMpS, t.DistanceTravelled, t.Number, t.RearTDBTraveller.TileX, t.RearTDBTraveller.TileZ, t.RearTDBTraveller.X, t.RearTDBTraveller.Z, t.RearTDBTraveller.TrackNodeIndex, t.Cars.Count, (int)t.MUDirection, (int)t.RearTDBTraveller.Direction, t.Length));
            t.LastReportedSpeed = t.SpeedMpS;
        }

        public bool OKtoSend()
        {
            if (items != null && items.Count > 0) return true;
            return false;
        }
        public override string ToString()
        {
            string tmp = "MOVE ";
            if (items != null && items.Count > 0)
            for (var i = 0; i < items.Count; i++) tmp += items[i].ToString() + " ";
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            foreach (MSGMoveItem m in items)
            {
                bool found = false; //a train may not be in my sim
                if (m.user == MPManager.GetUserName())//about itself, check if the number of car has changed, otherwise ignore
                {
                    //if I am a remote controlled train now
                    if (MPManager.Simulator.PlayerLocomotive.Train.TrainType == TrainType.Remote)
                    {
                        MPManager.Simulator.PlayerLocomotive.Train.ToDoUpdate(m.trackNodeIndex, m.TileX, m.TileZ, m.X, m.Z, m.travelled, m.speed, m.direction, m.tdbDir, m.Length);
                    }
                    found = true;/*
                    try
                    {
                        if (m.count != MPManager.Simulator.PlayerLocomotive.Train.Cars.Count)
                        {
                            if (!MPManager.IsServer() && CheckMissingTimes(MPManager.Simulator.PlayerLocomotive.Train.Number)) MPManager.SendToServer((new MSGGetTrain(MPManager.GetUserName(), MPManager.Simulator.PlayerLocomotive.Train.Number)).ToString());
                        }
                    }
                    catch (Exception) { }*/
                    continue;
                }
                if (m.user.Contains("0xAI") || m.user.Contains("0xUC"))
                {
                    foreach (Train t in MPManager.Simulator.Trains)
                    {
                        if (t.Number == m.num)
                        {
                            found = true;
                            if (t.Cars.Count != m.count) //the number of cars are different, client will not update it, ask for new information
                            {
                                if (!MPManager.IsServer())
                                {
                                    if (CheckMissingTimes(t.Number)) MPManager.SendToServer((new MSGGetTrain(MPManager.GetUserName(), t.Number)).ToString());
                                    continue;
                                }
                            }
                            if (t.TrainType == TrainType.Remote)
                            {
                                var reverseTrav = false;
//                                 Alternate way to check for train flip
//                                if (m.user.Contains("0xAI") && m.trackNodeIndex == t.RearTDBTraveller.TrackNodeIndex && m.tdbDir != (int)t.RearTDBTraveller.Direction)
//                                {
//                                    reverseTrav = true;
//                                }
                                t.ToDoUpdate(m.trackNodeIndex, m.TileX, m.TileZ, m.X, m.Z, m.travelled, m.speed, m.direction, m.tdbDir, m.Length, reverseTrav);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    Train t = MPManager.FindPlayerTrain(m.user);
                    if (t != null)
                    {
                        // skip the case where this train is merged with yours and you are the boss of that train
                        if (t.Number == MPManager.Simulator.PlayerLocomotive.Train.Number &&
                            MPManager.Simulator.PlayerLocomotive == MPManager.Simulator.PlayerLocomotive.Train.LeadLocomotive &&
                            t.TrainType != TrainType.Remote && t.TrainType != TrainType.Static) continue;
                        found = true;
                        t.ToDoUpdate(m.trackNodeIndex, m.TileX, m.TileZ, m.X, m.Z, m.travelled, m.speed, m.direction, m.tdbDir, m.Length);
                        // This is necessary as sometimes a train isn't in the Trains list
                        MPManager.Instance().AddOrRemoveTrain(t, true);
 //                       if (MPManager.IsServer()) MPManager.Instance().AddOrRemoveLocomotives(m.user, t, true);
                    }
                }
                if (found == false) //I do not have the train, tell server to send it to me
                {
                    if (!MPManager.IsServer() && CheckMissingTimes(m.num)) MPManager.SendToServer((new MSGGetTrain(MPManager.GetUserName(), m.num)).ToString());
                }
            }
        }
    }
#endregion MSGMove

#region MSGRequired
    public class MSGRequired : Message
    {

    }
#endregion

#region MSGPlayer
    public class MSGPlayer : MSGRequired
    {
        private WorldLocation location;
        public ref readonly WorldLocation Location => ref location;
        public string user = "";
        public string code = "";
        public int num; //train number
        public string con; //consist
        public string path; //path consist and path will always be double quoted
        public string route;
        public int dir; //direction
        public float Travelled;
        public double seconds;
        public int season, weather;
        public int pantofirst, pantosecond, pantothird, pantofourth;
        public string frontorrearcab;
        public int headlight;
        public float trainmaxspeed;
        public string leadingID;
        public string[] cars;
        public string[] ids;
        public int[] flipped; //if a wagon is engine
        public int[] lengths; //if a wagon is engine
        public string url;
        public int version;
        public string MD5 = "";
        public MSGPlayer() { }
        public MSGPlayer(string m)
        {
            string[] areas = m.Split('\r');
            if (areas.Length <= 6)
            {
                throw new Exception("Parsing error in MSGPlayer" + m);
            }
            try
            {
                var tmp = areas[0].Trim();
                string[] data = tmp.Split(' ');
                user = data[0];
                if (MPManager.IsServer() && !MPManager.Instance().AllowNewPlayer)//server does not want to have more people
                {
                    MPManager.BroadCast((new MSGMessage(user, "Error", "The dispatcher does not want to add more player")).ToString());
                    throw (new Exception("Not want to add new player"));
                }
                code = data[1];
                num = int.Parse(data[2]);
                location = new WorldLocation(int.Parse(data[3]), int.Parse(data[4]), float.Parse(data[5], CultureInfo.InvariantCulture), 0, float.Parse(data[6], CultureInfo.InvariantCulture));
                Travelled = float.Parse(data[7], CultureInfo.InvariantCulture);
                trainmaxspeed = float.Parse(data[8], CultureInfo.InvariantCulture);
                seconds = double.Parse(data[9], CultureInfo.InvariantCulture);
                season = int.Parse(data[10]);
                weather = int.Parse(data[11]);
                pantofirst = int.Parse(data[12]);
                pantosecond = int.Parse(data[13]);
                pantothird = int.Parse(data[14]);
                pantofourth = int.Parse(data[15]);
                frontorrearcab = data[16];
                headlight = int.Parse(data[17]);
                //user = areas[0].Trim();
                con = areas[2].Trim();
                route = areas[3].Trim();
                path = areas[4].Trim();
                dir = int.Parse(areas[5].Trim());
                url = areas[6].Trim();
                ParseTrainCars(areas[7].Trim());
                leadingID = areas[1].Trim();
                int index = path.LastIndexOf("\\PATHS\\", StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    path = path.Remove(0, index + 7);
                }
                index = con.LastIndexOf("\\CONSISTS\\", StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    con = con.Remove(0, index + 10);
                }
                if (areas.Length >= 9) { version = int.Parse(areas[8]); }
                if (areas.Length >= 10)
                {
                    MD5 = areas[9];
                    if (MPManager.Instance().MD5Check == "")
                    {
                        MPManager.Instance().GetMD5HashFromTDBFile();
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }


        private void ParseTrainCars(string m)
        {
            string[] areas = m.Split('\t');
            var numCars = areas.Length;
            cars = new string[numCars];//with an empty "" at end
            ids = new string[numCars];
            flipped = new int[numCars];
            lengths = new int[numCars];
            int index, last;
            for (var i = 0; i < numCars; i++)
            {
                index = areas[i].IndexOf('\"');
                last = areas[i].LastIndexOf('\"');
                cars[i] = areas[i].Substring(index + 1, last - index - 1);
                string tmp = areas[i].Remove(0, last + 1);
                tmp = tmp.Trim();
                string[] carinfo = tmp.Split('\n');
                ids[i] = carinfo[0];
                flipped[i] = int.Parse(carinfo[1]);
                lengths[i] = int.Parse(carinfo[2]);
            }

        }
        public MSGPlayer(string n, string cd, string c, string p, Train t, int tn, string avatar)
        {
            url = avatar;
            route = MPManager.Simulator.RoutePathName;
            int index = p.LastIndexOf("\\PATHS\\", StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                p = p.Remove(0, index + 7);
            }
            index = c.LastIndexOf("\\CONSISTS\\", StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                c = c.Remove(0, index + 10);
            }
            user = n; code = cd; con = c; path = p;
            if (t != null)
            {
                dir = (int)t.RearTDBTraveller.Direction; num = tn;
                location = t.RearTDBTraveller.WorldLocation;
                Travelled = t.DistanceTravelledM;
                trainmaxspeed = t.TrainMaxSpeedMpS;
            }
            seconds = (int)MPManager.Simulator.ClockTime; season = (int)MPManager.Simulator.Season; weather = (int)MPManager.Simulator.WeatherType;
            pantofirst = pantosecond = pantothird = pantofourth = 0;
            MSTSWagon w = (MSTSWagon)MPManager.Simulator.PlayerLocomotive;
            if (w != null)
            {
                pantofirst = w.Pantographs[1].CommandUp ? 1 : 0;
                pantosecond = w.Pantographs[2].CommandUp ? 1 : 0;
                pantothird = w.Pantographs.List.Count > 2 && w.Pantographs[3].CommandUp ? 1 : 0;
                pantofourth = w.Pantographs.List.Count > 3 && w.Pantographs[4].CommandUp ? 1 : 0;
                frontorrearcab = (w as MSTSLocomotive).UsingRearCab ? "R" : "F";
                headlight = w.Headlight;
            }

            cars = new string[t.Cars.Count];
            ids = new string[t.Cars.Count];
            flipped = new int[t.Cars.Count];
            lengths = new int[t.Cars.Count];
            for (var i = 0; i < t.Cars.Count; i++)
            {
                cars[i] = t.Cars[i].RealWagFilePath;
                ids[i] = t.Cars[i].CarID;
                if (t.Cars[i].Flipped == true) flipped[i] = 1;
                else flipped[i] = 0;
                lengths[i] = (int)(t.Cars[i].CarLengthM * 100);
            }
            if (t.LeadLocomotive != null) leadingID = t.LeadLocomotive.CarID;
            else leadingID = "NA";

            version = MPManager.Instance().version;

            if (MPManager.Instance().MD5Check == "") MPManager.Instance().GetMD5HashFromTDBFile();
            MD5 = MPManager.Instance().MD5Check;
        }
        public override string ToString()
        {
            string tmp = "PLAYER " + user + " " + code + " " + num + " " + location.TileX + " " + location.TileZ + " " + location.Location.X.ToString(CultureInfo.InvariantCulture) + " " + location.Location.Z.ToString(CultureInfo.InvariantCulture)
                + " " + Travelled.ToString(CultureInfo.InvariantCulture) + " " + trainmaxspeed.ToString(CultureInfo.InvariantCulture) + " " + seconds.ToString(CultureInfo.InvariantCulture) + " " + season + " " + weather + " " + pantofirst + " " + pantosecond + " " + pantothird + " " + pantofourth + " " + frontorrearcab + " " + headlight + " \r" +
                leadingID + "\r" + con + "\r" + route + "\r" + path + "\r" + dir + "\r" + url + "\r";
            for (var i = 0; i < cars.Length; i++)
            {
                var c = cars[i];
                var index = c.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    c = c.Remove(0, index + 17);
                }//c: wagon path without folder name

                tmp += "\"" + c + "\"" + " " + ids[i] + "\n" + flipped[i] + "\n" + lengths[i] + "\t";
            }

            tmp += "\r" + MPManager.Instance().version + "\r" + MD5;
            return " " + tmp.Length + ": " + tmp;
        }

        private object lockObjPlayer = new object();
        public override void HandleMsg()
        {
            if (this.version != MPManager.Instance().version)
            {
                var reason = "Wrong version of protocol, please update to version " + MPManager.Instance().version;
                if (MPManager.IsServer())
                {
                    MPManager.BroadCast((new MSGMessage(this.user, "Error", reason)).ToString());//server will broadcast this error
                    throw new Exception("Player has wrong version of protocol");//ignore this player message
                }
                else
                {
                    Trace.WriteLine("Wrong version of protocol, will play in single mode, please update to version " + MPManager.Instance().version);
                    throw new MultiPlayerError();//client, close the connection
                }
            }
            if (MPManager.IsServer() && MPManager.Instance().MD5Check != "NA")//I am the server and have MD5 check values, client should have matching MD5, if file is accessible
            {
                if ((MD5 != "NA" && MD5 != MPManager.Instance().MD5Check) || route.ToLower() != MPManager.Simulator.RoutePathName.ToLower())
                {
                    MPManager.BroadCast((new MSGMessage(this.user, "Error", "Wrong route dir or TDB file, the dispatcher uses a different route")).ToString());//server will broadcast this error
                    throw new Exception("Player has wrong version of route");//ignore this player message
                }
            }
            //check if other players with the same name is online
            if (MPManager.IsServer())
            {
                //if someone with the same name is there, will throw a fatal error
                lock (lockObjPlayer)
                {
                    if (MPManager.FindPlayerTrain(user) != null || MPManager.GetUserName() == user)
                    {
                        try
                        {
                            MPManager.OnlineTrains.Players[user].protect = true;
                        }
                        catch { }
                        MPManager.BroadCast((new MSGMessage(user, "SameNameError", "A user with the same name exists")).ToString());
                        throw new Exception("Same Name");
                    }
                }
            }
            lock (lockObjPlayer)
            {

                if (MPManager.FindPlayerTrain(user) != null) return; //already added the player, ignore
                //if the client comes back after disconnected within 10 minutes
                if (MPManager.IsServer() && MPManager.Instance().lostPlayer != null && MPManager.Instance().lostPlayer.ContainsKey(user))
                {
                    var p1 = MPManager.Instance().lostPlayer[user];
                    var p1Train = p1.Train;

                    //check to see if the player gets back with the same set of cars
                    bool identical = true;
                    if (cars.Length != p1Train.Cars.Count) identical = false;
                    if (identical != false)
                    {
                        string wagonFilePath = MPManager.Simulator.BasePath + @"\trains\trainset\";
                        for (int i = 0; i < cars.Length; i++)
                        {
                            if (wagonFilePath + cars[i] != p1Train.Cars[i].RealWagFilePath) { identical = false; break; }
                        }
                    }

                    //if distance is higher than 1 Km from starting point of path

                    if (WorldLocation.GetDistanceSquared2D(location, p1Train.RearTDBTraveller.WorldLocation) > 1000000)
                    {
                        MPManager.OnlineTrains.Players.Add(user, p1);
                        p1.CreatedTime = MPManager.Simulator.GameTime;
                        MPManager.Instance().AddOrRemoveTrain(p1Train, true);
                        if (MPManager.IsServer()) MPManager.Instance().AddOrRemoveLocomotives(user, p1Train, true);
                        MPManager.Instance().lostPlayer.Remove(user);
                    }
                    else//if the player uses different train cars
                    {
                        MPManager.Instance().lostPlayer.Remove(user);
                    }
                }
                MPManager.OnlineTrains.AddPlayers(this, null);

                //Trace.WriteLine(this.ToString());
                if (MPManager.IsServer())// && MPManager.Server.IsRemoteServer())
                {
                    MPManager.BroadCast((new MSGOrgSwitch(user, MPManager.Instance().OriginalSwitchState)).ToString());
                    MPManager.Instance().PlayerAdded = true;
                }
                else //client needs to handle environment
                {
                    if (MPManager.GetUserName() == this.user && !MPManager.Client.Connected) //a reply from the server, update my train number
                    {
                        MPManager.Client.Connected = true;
                        Train t = null;
                        if (MPManager.Simulator.PlayerLocomotive == null) t = MPManager.Simulator.Trains[0];
                        else t = MPManager.Simulator.PlayerLocomotive.Train;
                        t.Number = this.num;
                        if (WorldLocation.GetDistanceSquared2D(location, t.RearTDBTraveller.WorldLocation) > 1000000)
                        {
                            t.UpdateTrainJump(location, dir, Travelled, trainmaxspeed);
                            //check to see if the player gets back with the same set of cars
                            bool identical = true;
                            if (cars.Length != t.Cars.Count) identical = false;
                            if (identical != false)
                            {
                                string wagonFilePath = MPManager.Simulator.BasePath + @"\trains\trainset\";
                                for (int i = 0; i < cars.Length; i++)
                                {
                                    if (wagonFilePath + cars[i] != t.Cars[i].RealWagFilePath) { identical = false; break; }
                                }
                            }
                            if (!identical)
                            { 
                            var carsCount = t.Cars.Count;
                            t.Cars.RemoveRange(0, carsCount);
                                for (int i = 0; i < cars.Length; i++)
                                {
                                    string wagonFilePath = MPManager.Simulator.BasePath + @"\trains\trainset\" + cars[i];
                                    if (!File.Exists(wagonFilePath))
                                    {
                                        Trace.TraceWarning($"Ignored missing rolling stock {wagonFilePath}");
                                        continue;
                                    }

                                    try // Load could fail if file has bad data.
                                    {
                                        TrainCar car = RollingStock.Load(MPManager.Simulator, wagonFilePath);
                                        car.Flipped = flipped[i] == 0 ? false : true;
                                        car.CarID = ids[i];
                                        var carID = car.CarID;
                                        carID = carID.Remove(0, carID.LastIndexOf('-') + 2);
                                        var uid = 0;
                                        if (Int32.TryParse(carID, out uid)) car.UiD = uid;
                                        car.Train = t;
                                        t.Cars.Add(car);
                                        if (car.CarID == leadingID)
                                        {
                                            t.LeadLocomotiveIndex = i;
                                            MPManager.Simulator.PlayerLocomotive = t.LeadLocomotive as MSTSLocomotive;

                                        }
                                    }
                                    catch (Exception error)
                                    {
                                        Trace.WriteLine(new FileLoadException(wagonFilePath, error));
                                    }
                                }
                            }
                            else
                            {
                                var i = 0;
                                foreach (var car in t.Cars)
                                {
                                    car.CarID = ids[i];
                                    i++;
                                }
                            }
                            t.UpdateMSGReceived = true;
                            t.RequestJump = true; // server has requested me to jump after I re-entered the game
                        }
                    }
                    MPManager.Simulator.ClockTime = this.seconds;
                    MPManager.Simulator.SetWeather((WeatherType)weather, (SeasonType)season);
                }
            }
        }

        //this version only intends for the one started on server computer
        public void HandleMsg(OnlinePlayer p)
        {
            if (!MPManager.IsServer()) return; //only intended for the server, when it gets the player message in OnlinePlayer Receive
            if (this.version != MPManager.Instance().version)
            {
                var reason = "Wrong version of protocol, please update to version " + MPManager.Instance().version;
                MPManager.BroadCast((new MSGMessage(this.user, "Error", reason)).ToString());
                throw new Exception("Wrong version of protocol");
            }
            if ((MPManager.Instance().MD5Check != "NA" && MD5 != "NA" && MD5 != MPManager.Instance().MD5Check) || route.ToLower() != MPManager.Simulator.RoutePathName.ToLower())
            {
                MPManager.BroadCast((new MSGMessage(this.user, "Error", "Wrong route dir or TDB file, the dispatcher uses a different route")).ToString());//server will broadcast this error
                throw new Exception("Player has wrong version of route");//ignore this player message
            }

            //check if other players with the same name is online
            //if someone with the same name is there, will throw a fatal error
            if (MPManager.FindPlayerTrain(user) != null || MPManager.GetUserName() == user)
            {
                MPManager.BroadCast((new MSGMessage(user, "SameNameError", "A user with the same name exists")).ToString());
                throw new SameNameError();
            }

            //if the client comes back after disconnected within 10 minutes
            if (MPManager.Instance().lostPlayer != null && MPManager.Instance().lostPlayer.ContainsKey(user))
            {
                var p1 = MPManager.Instance().lostPlayer[user];
                var p1Train = p1.Train;

                //check to see if the player gets back with the same set of cars
                bool identical = true;
                if (cars.Length != p1Train.Cars.Count) identical = false;
                if (identical != false)
                {
                    string wagonFilePath = MPManager.Simulator.BasePath + @"\trains\trainset\";
                    for (int i = 0; i < cars.Length; i++)
                    {
                        if (wagonFilePath + cars[i] != p1Train.Cars[i].RealWagFilePath) { identical = false; break; }
                    }
                }

                //if the client player run already for more than 1 Km, we acknowledge him that run
                if (WorldLocation.GetDistanceSquared2D(location, p1Train.RearTDBTraveller.WorldLocation) > 1000000)
                {
                    p.Train = p1Train; p.url = p1.url;
                    p.LeadingLocomotiveID = p1.LeadingLocomotiveID;
                    p.con = p1.con;
                    p.Train.IsTilting = p1.Train.IsTilting;
                    p.CreatedTime = MPManager.Simulator.GameTime;
                    p.path = p1.path;
                    p.Username = p1.Username;
                    MPManager.OnlineTrains.Players.Add(user, p);
                    MPManager.Instance().AddOrRemoveTrain(p.Train, true);
                    if (MPManager.IsServer()) MPManager.Instance().AddOrRemoveLocomotives(user, p.Train, true);
                    MPManager.Instance().lostPlayer.Remove(user);
                }
                else//if the player uses different train cars
                {
                    MPManager.Instance().lostPlayer.Remove(user);
                }
            }

            //client connected directly to the server, thus will send the game status to the player directly (avoiding using broadcast)
            MPManager.OnlineTrains.AddPlayers(this, p);
            //Trace.WriteLine(this.ToString());
            SendToPlayer(p, (new MSGOrgSwitch(user, MPManager.Instance().OriginalSwitchState)).ToString());

            MPManager.Instance().lastPlayerAddedTime = MPManager.Simulator.GameTime;

            MSGPlayer host = new MSGPlayer(MPManager.GetUserName(), "1234", MPManager.Simulator.conFileName, MPManager.Simulator.patFileName, MPManager.Simulator.PlayerLocomotive.Train,
                MPManager.Simulator.PlayerLocomotive.Train.Number, MPManager.Simulator.Settings.AvatarURL);
            SendToPlayer(p, host.ToString() + MPManager.OnlineTrains.AddAllPlayerTrain());

            //send the train information to the new player
            Train[] trains = MPManager.Simulator.Trains.ToArray();

            foreach (Train t in trains)
            {
                if (MPManager.Simulator.PlayerLocomotive != null && t == MPManager.Simulator.PlayerLocomotive.Train) continue; //avoid broadcast player train
                if (MPManager.FindPlayerTrain(t)) continue;
                if (MPManager.Instance().removedTrains.Contains(t)) continue;//this train is going to be removed, should avoid it.
                SendToPlayer(p, (new MSGTrain(t, t.Number)).ToString());
            }
            if (MPManager.Instance().CheckSpad == false) { p.Send((new MultiPlayer.MSGMessage("All", "OverSpeedOK", "OK to go overspeed and pass stop light")).ToString()); }
            else { SendToPlayer(p, (new MultiPlayer.MSGMessage("All", "NoOverSpeed", "Penalty for overspeed and passing stop light")).ToString()); }
            SendToPlayer(p, MPManager.Instance().GetEnvInfo());//update weather

            //send the new player information to everyone else
            host = new MSGPlayer(p.Username, "1234", p.con, p.path, p.Train, p.Train.Number, p.url);
            var players = MPManager.OnlineTrains.Players.ToArray();
            string newPlayer = host.ToString();
            foreach (var op in players)
            {
                SendToPlayer(op.Value, newPlayer);
            }

            //Trace.WriteLine(host.ToString() + MPManager.Simulator.OnlineTrains.AddAllPlayerTrain());

        }

        public void SendToPlayer(OnlinePlayer p, string msg)
        {
#if DEBUG_MULTIPLAYER
            Trace.TraceInformation("Message {1} sent to player {0}", p.Username, msg); 
#endif
            p.Send(msg);
        }

    }

#endregion MSGPlayer

#region MSGPlayerTrainSw
    public class MSGPlayerTrainSw : MSGRequired
    {
        public string user = "";
        public int num; //train number
        public bool oldTrainReverseFormation = false;
        public bool newTrainReverseFormation = false;
        public string leadingID;
        public MSGPlayerTrainSw() { }
        public MSGPlayerTrainSw(string m)
        {
            string[] areas = m.Split('\r');
            if (areas.Length <= 1)
            {
                throw new Exception("Parsing error in MSGPlayerTrainSw" + m);
            }
            try
            {
                var tmp = areas[0].Trim();
                string[] data = tmp.Split(' ');
                user = data[0];
                num = int.Parse(data[1]);
                oldTrainReverseFormation = bool.Parse(data[2]);
                newTrainReverseFormation = bool.Parse(data[3]);
                leadingID = areas[1].Trim();
            }
            catch (Exception e)
            {
                throw e;
            }
        }


        public MSGPlayerTrainSw(string n, Train t, int tn, bool oldRevForm, bool newRevForm)
        {
            user = n;
            if (t != null) num = tn;
            if (t.LeadLocomotive != null) leadingID = t.LeadLocomotive.CarID;
            else leadingID = "NA";
            oldTrainReverseFormation = oldRevForm;
            newTrainReverseFormation = newRevForm;
        }

        public override string ToString()
        {
            string tmp = "PLAYERTRAINSW " + user + " " + num + " " + oldTrainReverseFormation + " " + newTrainReverseFormation + " " + "\r" + leadingID + "\r";
            return " " + tmp.Length + ": " + tmp;
        }

        private object lockObjPlayer = new object();
        public override void HandleMsg()
        {
            lock (lockObjPlayer)
            {
                MPManager.OnlineTrains.SwitchPlayerTrain(this);

                if (MPManager.IsServer())
                {
                    MPManager.Instance().PlayerAdded = true;
                }
                else //client needs to handle environment
                {
                    if (MPManager.GetUserName() == this.user && !MPManager.Client.Connected) //a reply from the server, update my train number
                    {
                        MPManager.Client.Connected = true;
                        Train t = null;
                        if (MPManager.Simulator.PlayerLocomotive == null) t = MPManager.Simulator.Trains[0];
                        else t = MPManager.Simulator.PlayerLocomotive.Train;
                    }
                }
            }
        }

        //this version only intends for the one started on server computer
        /*        public void HandleMsg(OnlinePlayer p)
                {
                    if (!MPManager.IsServer()) return; //only intended for the server, when it gets the player message in OnlinePlayer Receive

                    //client connected directly to the server, thus will send the game status to the player directly (avoiding using broadcast)
                    MPManager.OnlineTrains.SwitchPlayerTrain(this);

                    MSGPlayerTrainSw host = new MSGPlayerTrainSw(MPManager.GetUserName(), MPManager.Simulator.PlayerLocomotive.Train, MPManager.Simulator.PlayerLocomotive.Train.Number,);
                    p.Send(host.ToString());

                    //send the new player information to everyone else
                    host = new MSGPlayerTrainSw(p.Username, p.Train, p.Train.Number);
                    var players = MPManager.OnlineTrains.Players.ToArray();
                    string newPlayer = host.ToString();
                    foreach (var op in players)
                    {
                        op.Value.Send(newPlayer);
                    }
                }*/
    }

#endregion MSGPlayerTrainSw

#region MGSwitch

    public class MSGSwitch : Message
    {
        public string user;
        public int TileX, TileZ, WorldID, Selection;
        public bool HandThrown;
        bool OK = true;

        public MSGSwitch(string m)
        {

            string[] tmp = m.Split(' ');
            if (tmp.Length != 6) throw new Exception("Parsing error " + m);
            user = tmp[0];
            TileX = int.Parse(tmp[1]);
            TileZ = int.Parse(tmp[2]);
            WorldID = int.Parse(tmp[3]);
            Selection = int.Parse(tmp[4]);
            HandThrown = bool.Parse(tmp[5]);
        }

        public MSGSwitch(string n, int tX, int tZ, int u, int s, bool handThrown)
        {
            if (!MPManager.Instance().AmAider && MPManager.Instance().TrySwitch == false)
            {
                if (handThrown && MPManager.Simulator.Confirmer != null)
                    MPManager.Simulator.Confirmer.Information(MPManager.Catalog.GetString("Dispatcher does not allow hand throw at this time"));
                OK = false;
                return;
            }
            user = n;
            WorldID = u;
            TileX = tX;
            TileZ = tZ;
            Selection = s;
            HandThrown = handThrown;
        }

        public override string ToString()
        {
            if (!OK) return null;
            string tmp = "SWITCH " + user + " " + TileX + " " + TileZ + " " + WorldID + " " + Selection + " " + HandThrown;
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            //Trace.WriteLine(this.ToString());
            if (MPManager.IsServer()) //server got this message from Client
            {
                //if a normal user, and the dispatcher does not want hand throw, just ignore it
                if (HandThrown == true && !MPManager.AllowedManualSwitch && !MPManager.Instance().aiderList.Contains(user))
                {
                    MPManager.BroadCast((new MSGMessage(user, "SwitchWarning", "Server does not allow hand thrown of switch")).ToString());
                    return;
                }
                TrackJunctionNode trj = MPManager.Simulator.TDB.TrackDB.GetJunctionNode(TileX, TileZ, WorldID);
                bool state = MPManager.Simulator.SignalEnvironment.RequestSetSwitch(trj, this.Selection);
                if (state == false)
                    MPManager.BroadCast((new MSGMessage(user, "Warning", "Train on the switch, cannot throw")).ToString());
                else MPManager.BroadCast(this.ToString()); //server will tell others
            }
            else
            {
                TrackJunctionNode trj = MPManager.Simulator.TDB.TrackDB.GetJunctionNode(TileX, TileZ, WorldID);
                SetSwitch(trj, Selection);
                //trj.SelectedRoute = Selection; //although the new signal system request Signals.RequestSetSwitch, client may just change
                if (user == MPManager.GetUserName() && HandThrown == true)//got the message with my name, will confirm with the player
                {
                    MPManager.Simulator.Confirmer.Information(MPManager.Catalog.GetString("Switched, current route is {0}",
                        Selection == 0 ? MPManager.Catalog.GetString("main route") : MPManager.Catalog.GetString("side route")));
                    return;
                }
            }
        }

        public static void SetSwitch(TrackNode switchNode, int desiredState)
        {
            TrackCircuitSection switchSection = TrackCircuitSection.TrackCircuitList[switchNode.TrackCircuitCrossReferences[0].Index];
            (MPManager.Simulator.TDB.TrackDB.TrackNodes[switchSection.OriginalIndex] as TrackJunctionNode).SelectedRoute = switchSection.JunctionSetManual = desiredState;
            switchSection.JunctionLastRoute = switchSection.JunctionSetManual;

            // update linked signals
            foreach (int thisSignalIndex in switchSection.LinkedSignals ?? Enumerable.Empty<int>())
            {
                Signal thisSignal = MPManager.Simulator.SignalEnvironment.Signals[thisSignalIndex];
                thisSignal.Update();
            }
        }
    }

#endregion MGSwitch

#region MSGResetSignal
    public class MSGResetSignal : Message
    {
        public string user;
        public int TileX, TileZ, WorldID, Selection;

        public MSGResetSignal(string m)
        {
            user = m.Trim();
        }

        public override string ToString()
        {
            string tmp = "RESETSIGNAL " + user;
            //Trace.WriteLine(tmp);
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            if (MPManager.IsServer())
            {
                try
                {
                    var t = MPManager.FindPlayerTrain(user);
                    if (t != null) t.RequestSignalPermission(Direction.Forward);
                    MultiPlayer.MPManager.BroadCast((new MSGSignalStatus()).ToString());
                }
                catch (Exception) { }
            }
        }
    }
#endregion MSGResetSignal

#region MSGOrgSwitch
    public class MSGOrgSwitch : MSGRequired
    {
        SortedList<uint, TrackJunctionNode> SwitchState;
        public string msgx = "";
        string user = "";
        byte[] switchStatesArray;
        public MSGOrgSwitch(string u, string m)
        {
            user = u; msgx = m;
        }

        public MSGOrgSwitch(string m)
        {
            string[] tmp = m.Split('\t');
            user = tmp[0].Trim();
            byte[] gZipBuffer = Convert.FromBase64String(tmp[1]);
            using (var memoryStream = new MemoryStream())
            {
                int dataLength = BitConverter.ToInt32(gZipBuffer, 0);
                memoryStream.Write(gZipBuffer, 4, gZipBuffer.Length - 4);

                switchStatesArray = new byte[dataLength];

                memoryStream.Position = 0;
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    gZipStream.Read(switchStatesArray, 0, switchStatesArray.Length);
                }
            }

        }

        public override void HandleMsg() //only client will get message, thus will set states
        {
            if (MPManager.IsServer() || user != MPManager.GetUserName()) return; //server will ignore it
            uint key = 0;
            SwitchState = new SortedList<uint, TrackJunctionNode>();
            try
            {
                foreach (TrackNode t in MPManager.Simulator.TDB.TrackDB.TrackNodes)
                {
                    if (t is TrackJunctionNode trackJunctionNode)
                    {
                        key = t.Index;
                        SwitchState.Add(key, trackJunctionNode);
                    }
                }
            }
            catch (Exception e) { SwitchState = null; throw e; } //if error, clean the list and wait for the next signal

            int i = 0, state = 0;
            foreach (KeyValuePair<uint, TrackJunctionNode> t in SwitchState)
            {
                state = (int)switchStatesArray[i];
                if (t.Value.SelectedRoute != state)
                {
                    SetSwitch(t.Value, state);
                    //t.Value.SelectedRoute = state;
                }
                i++;
            }

        }

        public static void SetSwitch(TrackNode switchNode, int desiredState)
        {
            TrackCircuitSection switchSection = TrackCircuitSection.TrackCircuitList[switchNode.TrackCircuitCrossReferences[0].Index];
            (MPManager.Simulator.TDB.TrackDB.TrackNodes[switchSection.OriginalIndex] as TrackJunctionNode).SelectedRoute = switchSection.JunctionSetManual = desiredState;
            switchSection.JunctionLastRoute = switchSection.JunctionSetManual;

            // update linked signals
            foreach (int thisSignalIndex in switchSection.LinkedSignals ?? Enumerable.Empty<int>())
            {
                Signal thisSignal = MPManager.Simulator.SignalEnvironment.Signals[thisSignalIndex];
                thisSignal.Update();
            }
        }

        public override string ToString()
        {
            string tmp = "ORGSWITCH " + user + "\t" + msgx;
            return " " + tmp.Length + ": " + tmp;
        }
    }
#endregion MSGOrgSwitch

#region MSGSwitchStatus
    public class MSGSwitchStatus : Message
    {
        static byte[] preState;
        static SortedList<uint, TrackJunctionNode> SwitchState;
        public bool OKtoSend = false;
        static byte[] switchStatesArray;
        public MSGSwitchStatus()
        {
            var i = 0;
            if (SwitchState == null)
            {
                SwitchState = new SortedList<uint, TrackJunctionNode>();
                uint key = 0;
                foreach (TrackNode t in MPManager.Simulator.TDB.TrackDB.TrackNodes)
                {
                    if (t is TrackJunctionNode trackJunctionNode)
                    {
                        key = t.Index;
                        SwitchState.Add(key, trackJunctionNode);
                    }
                }
                switchStatesArray = new byte[SwitchState.Count() + 2];
            }
            if (preState == null)
            {
                preState = new byte[SwitchState.Count() + 2];
                for (i = 0; i < preState.Length; i++) preState[i] = 0;
            }
            i = 0;
            foreach (KeyValuePair<uint, TrackJunctionNode> t in SwitchState)
            {
                switchStatesArray[i] = (byte)t.Value.SelectedRoute;
                i++;
            }
            OKtoSend = false;
            for (i = 0; i < SwitchState.Count; i++)
            {
                if (switchStatesArray[i] != preState[i]) { OKtoSend = true; }//something is different, will send
                preState[i] = switchStatesArray[i];
            }
            if (OKtoSend == false)
            {
                //new player added, will keep sending for a while
                if (MPManager.Simulator.GameTime - MPManager.Instance().lastPlayerAddedTime < 3 * MPManager.Instance().MPUpdateInterval) OKtoSend = true;
            }
        }

        public MSGSwitchStatus(string m)
        {
            if (SwitchState == null)
            {
                uint key = 0;
                SwitchState = new SortedList<uint, TrackJunctionNode>();
                try
                {
                    foreach (TrackNode t in MPManager.Simulator.TDB.TrackDB.TrackNodes)
                    {
                        if (t is TrackJunctionNode trackJunctionNode)
                        {
                            key = t.Index;
                            SwitchState.Add(key, trackJunctionNode);
                        }
                    }
                    switchStatesArray = new byte[SwitchState.Count + 128];//a bit more for safety
                }
                catch (Exception e) { SwitchState = null; throw e; } //if error, clean the list and wait for the next signal

            }
            byte[] gZipBuffer = Convert.FromBase64String(m);
            using (var memoryStream = new MemoryStream())
            {
                BitConverter.ToInt32(gZipBuffer, 0);
                memoryStream.Write(gZipBuffer, 4, gZipBuffer.Length - 4);

                memoryStream.Position = 0;
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    gZipStream.Read(switchStatesArray, 0, switchStatesArray.Length);
                }

            }
        }

        public override void HandleMsg() //only client will get message, thus will set states
        {
            if (MPManager.IsServer()) return; //server will ignore it


            int i = 0, state = 0;
            foreach (KeyValuePair<uint, TrackJunctionNode> t in SwitchState)
            {
                state = (int)switchStatesArray[i];
                if (t.Value.SelectedRoute != state)
                {
                    if (!SwitchOccupiedByPlayerTrain(t.Value))
                    {
                        SetSwitch(t.Value, state);
                        //t.Value.SelectedRoute = state;
                    }
                }
                i++;
            }

        }

        public static void SetSwitch(TrackNode switchNode, int desiredState)
        {
            TrackCircuitSection switchSection = TrackCircuitSection.TrackCircuitList[switchNode.TrackCircuitCrossReferences[0].Index];
            (MPManager.Simulator.TDB.TrackDB.TrackNodes[switchSection.OriginalIndex] as TrackJunctionNode).SelectedRoute = switchSection.JunctionSetManual = desiredState;
            switchSection.JunctionLastRoute = switchSection.JunctionSetManual;

            // update linked signals
            foreach (int thisSignalIndex in switchSection.LinkedSignals ?? Enumerable.Empty<int>())
            {
                Signal thisSignal = MPManager.Simulator.SignalEnvironment.Signals[thisSignalIndex];
                thisSignal.Update();
            }
        }

        static bool SwitchOccupiedByPlayerTrain(TrackJunctionNode junctionNode)
        {
            if (MPManager.Simulator.PlayerLocomotive == null) return false;
            Train train = MPManager.Simulator.PlayerLocomotive.Train;
            if (train == null) return false;
            if (train.FrontTDBTraveller.TrackNodeIndex == train.RearTDBTraveller.TrackNodeIndex)
                return false;
            Traveller traveller = new Traveller(train.RearTDBTraveller);
            while (traveller.NextSection())
            {
                if (traveller.TrackNodeIndex == train.FrontTDBTraveller.TrackNodeIndex)
                    break;
                if (traveller.TN == junctionNode)
                    return true;
            }
            return false;
        }

        public override string ToString()
        {
            byte[] buffer = switchStatesArray;
            var memoryStream = new MemoryStream();
            using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
            {
                gZipStream.Write(buffer, 0, buffer.Length);
            }

            memoryStream.Position = 0;

            var compressedData = new byte[memoryStream.Length];
            memoryStream.Read(compressedData, 0, compressedData.Length);

            var gZipBuffer = new byte[compressedData.Length + 4];
            Buffer.BlockCopy(compressedData, 0, gZipBuffer, 4, compressedData.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gZipBuffer, 0, 4);

            string tmp = "SWITCHSTATES " + Convert.ToBase64String(gZipBuffer);
            return " " + tmp.Length + ": " + tmp;
        }
    }
#endregion MSGSwitchStatus
#region MSGTrain
    //message to add new train from either a string (received message), or a Train (building a message)
    public class MSGTrain : Message
    {
        string[] cars;
        string[] ids;
        int[] flipped; //if a wagon is engine
        int[] lengths;
        int TrainNum;
        int direction;
        float Travelled;
        int mDirection;
        string name;

        private WorldLocation location;

        public MSGTrain(string m)
        {
            //Trace.WriteLine(m);
            int index = m.IndexOf(' '); int last = 0;
            TrainNum = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            direction = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            int tileX = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            int tileZ = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            float x = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            float z = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            location = new WorldLocation(tileX, tileZ, x, 0, z);
            Travelled = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            mDirection = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            string[] areas = m.Split('\t');
            cars = new string[areas.Length - 2];//with an empty "" at end
            ids = new string[areas.Length - 2];
            flipped = new int[areas.Length - 2];
            lengths = new int[areas.Length - 2];
            for (var i = 0; i < cars.Length; i++)
            {
                index = areas[i].IndexOf('\"');
                last = areas[i].LastIndexOf('\"');
                cars[i] = areas[i].Substring(index + 1, last - index - 1);
                string tmp = areas[i].Remove(0, last + 1);
                tmp = tmp.Trim();
                string[] carinfo = tmp.Split('\n');
                ids[i] = carinfo[0];
                flipped[i] = int.Parse(carinfo[1]);
                lengths[i] = int.Parse(carinfo[2]);
            }
            index = areas[areas.Length - 2].IndexOf('\n');
            last = areas[areas.Length - 2].Length;
            name = areas[areas.Length - 2].Substring(index + 1, last - index - 1);

            //Trace.WriteLine(this.ToString());

        }

        public MSGTrain(Train t, int n)
        {
            cars = new string[t.Cars.Count];
            ids = new string[t.Cars.Count];
            flipped = new int[t.Cars.Count];
            lengths = new int[t.Cars.Count];
            for (var i = 0; i < t.Cars.Count; i++)
            {
                cars[i] = t.Cars[i].RealWagFilePath;
                ids[i] = t.Cars[i].CarID;
                lengths[i] = (int)t.Cars[i].CarLengthM;
                if (t.Cars[i].Flipped == true) flipped[i] = 1;
                else flipped[i] = 0;
            }
            TrainNum = n;
            direction = t.RearTDBTraveller.Direction == Traveller.TravellerDirection.Forward ? 1 : 0;
            location = t.RearTDBTraveller.WorldLocation;
            Travelled = t.DistanceTravelled;
            mDirection = (int)t.MUDirection;
            name = t.Name;
        }

        public override void HandleMsg() //only client will get message, thus will set states
        {
            if (MPManager.IsServer()) return; //server will ignore it
            //Trace.WriteLine(this.ToString());
            // construct train data
            Train train = null;
            train = new Train();
            train.Number = this.TrainNum;

            train.TrainType = TrainType.Remote;
            train.DistanceTravelled = Travelled;
            train.MUDirection = (MidpointDirection)this.mDirection;
            train.RearTDBTraveller = new Traveller(MPManager.Simulator.TSectionDat, MPManager.Simulator.TDB.TrackDB.TrackNodes, location, direction == 1 ? Traveller.TravellerDirection.Forward : Traveller.TravellerDirection.Backward);
            //if (consistDirection != 1)
            //	train.RearTDBTraveller.ReverseDirection();
            for (var i = 0; i < cars.Length; i++)// cars.Length-1; i >= 0; i--) {
            {
                string wagonFilePath = MPManager.Simulator.BasePath + @"\trains\trainset\" + cars[i];
                TrainCar car = null;
                try
                {
                    car = RollingStock.Load(MPManager.Simulator, wagonFilePath);
                    car.CarLengthM = lengths[i];
                }
                catch (Exception error)
                {
                    Trace.WriteLine(wagonFilePath + " " + error);
                    car = MPManager.Instance().SubCar(wagonFilePath, lengths[i]);
                }
                if (car == null) continue;
                bool flip = true;
                if (flipped[i] == 0) flip = false;
                car.Flipped = flip;
                car.CarID = ids[i];
                train.Cars.Add(car);
                car.Train = train;

            }// for each rail car

            if (train.Cars.Count == 0) return;
            train.Name = name;

            train.InitializeBrakes();
            //train.InitializeSignals(false);//client do it won't have impact
            train.CheckFreight();
            TrackCircuitPartialPathRoute tempRoute = train.CalculateInitialTrainPosition();

            train.SetInitialTrainRoute(tempRoute);
            train.CalculatePositionOfCars();
            train.ResetInitialTrainRoute(tempRoute);

            train.CalculatePositionOfCars();
            train.AITrainBrakePercent = 100;

            if (train.Cars[0] is MSTSLocomotive) train.LeadLocomotive = train.Cars[0];
            if (train.Cars[0].CarID.StartsWith("AI"))
            {
                // It's an AI train for the server, raise pantos and light lights
                if (train.LeadLocomotive != null) train.LeadLocomotive.SignalEvent(TrainEvent.HeadlightOn);
                foreach (TrainCar car in train.Cars)
                {
                    if (car is MSTSElectricLocomotive)
                    {
                        car.Train.SignalEvent(PowerSupplyEvent.RaisePantograph, 1);
                        break;
                    }
                }
            }
            if (MPManager.Instance().AddOrRemoveTrain(train, true) == false) return; //add train, but failed
            // if (MPManager.IsServer()) MPManager.Instance().AddOrRemoveLocomotives(user, train, true);
        }

        public override string ToString()
        {
            string tmp = "TRAIN " + TrainNum + " " + direction + " " + location.TileX + " " + location.TileZ + " " + location.Location.X.ToString(CultureInfo.InvariantCulture) + " " + location.Location.Z.ToString(CultureInfo.InvariantCulture) + " " + Travelled.ToString(CultureInfo.InvariantCulture) + " " + mDirection + " ";
            for (var i = 0; i < cars.Length; i++)
            {
                var c = cars[i];
                var index = c.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    c = c.Remove(0, index + 17);
                }//c: wagon path without folder name

                tmp += "\"" + c + "\"" + " " + ids[i] + "\n" + flipped[i] + "\n" + lengths[i] + "\t";
            }
            tmp += "\n" + name  + "\t";
            return " " + tmp.Length + ": " + tmp;
        }
    }

#endregion MSGTrain

#region MSGUpdateTrain

    //message to add new train from either a string (received message), or a Train (building a message)
    public class MSGUpdateTrain : Message
    {
        string[] cars;
        string[] ids;
        int[] flipped; //if a wagon is engine
        int[] lengths; //if a wagon is engine
        int TrainNum;
        int direction;
        float Travelled;
        int mDirection;
        string user;
        private WorldLocation location;

        public MSGUpdateTrain(string m)
        {
            //Trace.WriteLine(m);
            int index = m.IndexOf(' '); int last = 0;
            user = m.Substring(0, index + 1);
            m = m.Remove(0, index + 1);
            user = user.Trim();

            index = m.IndexOf(' ');
            TrainNum = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            direction = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            int tileX = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            int tileZ = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            float x = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            float z = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            location = new WorldLocation(tileX, tileZ, x, 0, z);
            Travelled = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            mDirection = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            string[] areas = m.Split('\t');
            cars = new string[areas.Length - 1];//with an empty "" at end
            ids = new string[areas.Length - 1];
            flipped = new int[areas.Length - 1];
            lengths = new int[areas.Length - 1];
            for (var i = 0; i < cars.Length; i++)
            {
                index = areas[i].IndexOf('\"');
                last = areas[i].LastIndexOf('\"');
                cars[i] = areas[i].Substring(index + 1, last - index - 1);
                string tmp = areas[i].Remove(0, last + 1);
                tmp = tmp.Trim();
                string[] carinfo = tmp.Split('\n');
                ids[i] = carinfo[0];
                flipped[i] = int.Parse(carinfo[1]);
                lengths[i] = int.Parse(carinfo[2]);
            }

            //Trace.WriteLine(this.ToString());

        }
        public MSGUpdateTrain(string u, Train t, int n)
        {
            user = u;
            cars = new string[t.Cars.Count];
            ids = new string[t.Cars.Count];
            flipped = new int[t.Cars.Count];
            lengths = new int[t.Cars.Count];
            for (var i = 0; i < t.Cars.Count; i++)
            {
                cars[i] = t.Cars[i].RealWagFilePath;
                ids[i] = t.Cars[i].CarID;
                lengths[i] = (int)t.Cars[i].CarLengthM;
                if (t.Cars[i].Flipped == true) flipped[i] = 1;
                else flipped[i] = 0;
            }
            TrainNum = n;
            direction = t.RearTDBTraveller.Direction == Traveller.TravellerDirection.Forward ? 1 : 0;
            location = t.RearTDBTraveller.WorldLocation;
            Travelled = t.DistanceTravelled;
            mDirection = (int)t.MUDirection;
        }

        static TrainCar findCar(Train t, string name)
        {
            foreach (TrainCar car in t.Cars)
            {
                if (car.CarID == name) return car;
            }
            return null;
        }
        private object lockObj = new object();

        public override void HandleMsg() //only client will get message, thus will set states
        {
            if (MPManager.IsServer()) return; //server will ignore it
            if (user != MPManager.GetUserName()) return; //not the one requested GetTrain
            bool found = false; Train train1 = null; Train train = null;
            lock (lockObj)
            {
                // construct train data
                foreach (Train t in MPManager.Simulator.Trains)
                {
                    if (t.Number == this.TrainNum) //the train exists, update information
                    {
                        train = t;
                        found = true; break;
                    }
                }
                if (!found)
                {
                    //not found, create new train
                    train1 = new Train(); train1.Number = this.TrainNum;
                }
            }
            if (found)
            {
                Traveller traveller = new Traveller(MPManager.Simulator.TSectionDat, MPManager.Simulator.TDB.TrackDB.TrackNodes, location, direction == 1 ? Traveller.TravellerDirection.Forward : Traveller.TravellerDirection.Backward);
                List<TrainCar> tmpCars = new List<TrainCar>();
                for (var i = 0; i < cars.Length; i++)// cars.Length-1; i >= 0; i--) {
                {
                    string wagonFilePath = MPManager.Simulator.BasePath + @"\trains\trainset\" + cars[i];
                    TrainCar car = findCar(train, ids[i]);
                    try
                    {
                        if (car == null) car = RollingStock.Load(MPManager.Simulator, wagonFilePath);
                        car.CarLengthM = lengths[i];
                    }
                    catch (Exception error)
                    {
                        Trace.WriteLine(wagonFilePath + " " + error);
                        car = MPManager.Instance().SubCar(wagonFilePath, lengths[i]);
                    }
                    if (car == null) continue;
                    bool flip = true;
                    if (flipped[i] == 0) flip = false;
                    car.Flipped = flip;
                    car.CarID = ids[i];
                    tmpCars.Add(car);
                    car.Train = train;

                }// for each rail car

                if (tmpCars.Count == 0) return;

                train.Cars.Clear();
                train.Cars.AddRange(tmpCars);
                train.MUDirection = (MidpointDirection)mDirection;
                train.RearTDBTraveller = traveller;
                train.CalculatePositionOfCars();
                train.DistanceTravelled = Travelled;
                train.CheckFreight();
                return;
            }
            train1.TrainType = TrainType.Remote;
            train1.DistanceTravelled = Travelled;
            train1.RearTDBTraveller = new Traveller(MPManager.Simulator.TSectionDat, MPManager.Simulator.TDB.TrackDB.TrackNodes, location, direction == 1 ? Traveller.TravellerDirection.Forward : Traveller.TravellerDirection.Backward);
            for (var i = 0; i < cars.Length; i++)// cars.Length-1; i >= 0; i--) {
            {
                string wagonFilePath = MPManager.Simulator.BasePath + @"\trains\trainset\" + cars[i];
                TrainCar car = null;
                try
                {
                    car = RollingStock.Load(MPManager.Simulator, wagonFilePath);
                    car.CarLengthM = lengths[i];
                }
                catch (Exception error)
                {
                    Trace.WriteLine(wagonFilePath + " " + error);
                    car = MPManager.Instance().SubCar(wagonFilePath, lengths[i]);
                }
                if (car == null) continue;
                bool flip = true;
                if (flipped[i] == 0) flip = false;
                car.Flipped = flip;
                car.CarID = ids[i];
                train1.Cars.Add(car);
                car.Train = train1;
            }// for each rail car

            if (train1.Cars.Count == 0) return;
            train1.MUDirection = (MidpointDirection)mDirection;
            //train1.CalculatePositionOfCars(0);
            train1.InitializeBrakes();
            //train1.InitializeSignals(false);
            train1.CheckFreight();
            TrackCircuitPartialPathRoute tempRoute = train1.CalculateInitialTrainPosition();

            train1.SetInitialTrainRoute(tempRoute);
            train1.CalculatePositionOfCars();
            train1.ResetInitialTrainRoute(tempRoute);

            train1.CalculatePositionOfCars();
            train1.AITrainBrakePercent = 100;

            if (train1.Cars[0] is MSTSLocomotive) train1.LeadLocomotive = train1.Cars[0];
            MPManager.Instance().AddOrRemoveTrain(train1, true);
            if (MPManager.IsServer()) MPManager.Instance().AddOrRemoveLocomotives(user, train1, true);
        }

        public override string ToString()
        {
            string tmp = "UPDATETRAIN " + user + " " + TrainNum + " " + direction + " " + location.TileX + " " + location.TileZ + " " + location.Location.X.ToString(CultureInfo.InvariantCulture)
                + " " + location.Location.Z.ToString(CultureInfo.InvariantCulture) + " " + Travelled.ToString(CultureInfo.InvariantCulture) + " " + mDirection + " ";
            for (var i = 0; i < cars.Length; i++)
            {
                var c = cars[i];
                var index = c.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    c = c.Remove(0, index + 17);
                }//c: wagon path without folder name

                tmp += "\"" + c + "\"" + " " + ids[i] + "\n" + flipped[i] + "\n" + lengths[i] + "\t";
            }
            return " " + tmp.Length + ": " + tmp;
        }
    }

#endregion MSGUpdateTrain

#region MSGRemoveTrain
    //remove AI trains
    public class MSGRemoveTrain : Message
    {
        public List<int> trains;

        public MSGRemoveTrain(string m)
        {
            string[] tmp = m.Split(' ');
            trains = new List<int>();
            for (var i = 0; i < tmp.Length; i++)
            {
                trains.Add(int.Parse(tmp[i]));
            }
        }

        public MSGRemoveTrain(List<Train> ts)
        {
            trains = new List<int>();
            foreach (Train t in ts)
            {
                trains.Add(t.Number);
            }
        }

        public override string ToString()
        {

            string tmp = "REMOVETRAIN";
            foreach (int i in trains)
            {
                tmp += " " + i;
            }
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            foreach (int i in trains)
            {
                foreach (Train train in MPManager.Simulator.Trains)
                {
                    if (i == train.Number)
                    {
                        if (MPManager.IsServer()) MPManager.Instance().AddOrRemoveLocomotives("", train, false);
                        MPManager.Instance().AddOrRemoveTrain(train, false);//added to the removed list, treated later to be thread safe
                    }
                }
            }
        }

    }

#endregion MSGRemoveTrain

#region MSGServer
    public class MSGServer : MSGRequired
    {
        string user; //true: I am a server now, false, not
        public MSGServer(string m)
        {
            user = m.Trim();
        }


        public override string ToString()
        {
            string tmp = "SERVER " + user;
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            //in the public port mode, old server may get disconnected, the port will ask anyone who is an aider to be the server
            //will response, but whoever reaches the port first will be declared dispatcher.
            if (user == "WhoCanBeServer")
            {
                if (MPManager.Instance().AmAider)
                {
                    string tmp = "SERVER MakeMeServer";
                    MPManager.Notify(" " + tmp.Length + ": " + tmp);
                }
                return;
            }

            if (MPManager.GetUserName() == user || user == "YOU")
            {
                if (MPManager.Server != null) return; //already a server, not need to worry
                MPManager.Client.Connected = true;
                MPManager.Instance().NotServer = false;
                MPManager.Server = new Server(MPManager.Client.UserName + ' ' + MPManager.Client.Code, MPManager.Client);
                MPManager.Instance().OnServerChanged(true);
                MPManager.Instance().RememberOriginalSwitchState();
                Trace.TraceInformation("You are the new dispatcher. Enjoy!");
                if (MPManager.Simulator.Confirmer != null)
                    MPManager.Simulator.Confirmer.Information(MPManager.Catalog.GetString("You are the new dispatcher. Enjoy!"));
                //Trace.WriteLine(this.ToString());
            }
            else
            {
                MPManager.Instance().NotServer = true;
                MPManager.Instance().OnServerChanged(false);
                if (MPManager.Simulator.Confirmer != null)
                    MPManager.Simulator.Confirmer.Information(MPManager.Catalog.GetString("New dispatcher is {0}", user));
                Trace.TraceInformation("New dispatcher is {0}", user);
            }
        }
    }
#endregion MSGServer

#region MSGAlive
    public class MSGAlive : Message
    {
        string user;
        public MSGAlive(string m)
        {
            user = m;
        }


        public override string ToString()
        {
            string tmp = "ALIVE " + user;
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            //nothing to worry at this stage
            //Trace.WriteLine(this.ToString());
        }
    }
#endregion MSGAlive

#region MSGTrainMerge
    //message to add new train from either a string (received message), or a Train (building a message)
    public class MSGTrainMerge : Message
    {
        int TrainNumRetain;
        int TrainNumRemoved;
        int direction;
        int TileX, TileZ;
        float X, Z, Travelled;
        public MSGTrainMerge(string m)
        {
            m = m.Trim();
            string[] areas = m.Split(' ');
            TrainNumRetain = int.Parse(areas[0]);
            TrainNumRemoved = int.Parse(areas[1]);
            direction = int.Parse(areas[2]);
            TileX = int.Parse(areas[3]);
            TileZ = int.Parse(areas[4]);
            X = float.Parse(areas[5], CultureInfo.InvariantCulture);
            Z = float.Parse(areas[6], CultureInfo.InvariantCulture);
            Travelled = float.Parse(areas[7], CultureInfo.InvariantCulture);
        }
        public MSGTrainMerge(Train t1, Train t2)
        {
            TrainNumRetain = t1.Number;
            TrainNumRemoved = t2.Number;
            direction = t1.RearTDBTraveller.Direction == Traveller.TravellerDirection.Forward ? 1 : 0;
            TileX = t1.RearTDBTraveller.TileX;
            TileZ = t1.RearTDBTraveller.TileZ;
            X = t1.RearTDBTraveller.X;
            Z = t1.RearTDBTraveller.Z;
            Travelled = t1.DistanceTravelled;

        }

        public override void HandleMsg()
        {

        }

        public override string ToString()
        {
            string tmp = "TRAINMERGE " + TrainNumRetain + " " + TrainNumRemoved + " " + direction + " " + TileX + " " + TileZ + " " + X.ToString(CultureInfo.InvariantCulture) + " " + Z.ToString(CultureInfo.InvariantCulture) + " " + Travelled.ToString(CultureInfo.InvariantCulture);
            return " " + tmp.Length + ": " + tmp;
        }
    }
#endregion MSGTrainMerge

#region MSGMessage
    //warning, error or information from the server, a client receives Error will disconnect itself
    public class MSGMessage : MSGRequired
    {
        string msgx;
        string level;
        string user;
        public MSGMessage(string m)
        {
            string[] t = m.Split('\t');
            user = t[0].Trim();
            level = t[1].Trim();
            msgx = t[2];

        }

        public MSGMessage(string u, string l, string m)
        {
            user = u;
            level = l;

            msgx = m;
        }

        public override void HandleMsg()
        {
            if (MPManager.GetUserName() == user || user == "All")
            {
                Trace.WriteLine($"{level}: {msgx}");
                if (MPManager.Simulator.Confirmer != null && level == "Error")
                    MPManager.Simulator.Confirmer.Message(ConfirmLevel.Error, msgx);

                if (level == "Error" && !MPManager.IsServer())//if is a client, fatal error, will close the connection, and get into single mode
                {
                    MPManager.Notify((new MSGQuit(MPManager.GetUserName())).ToString());//to be nice, still send a quit before close the connection
                    throw new MultiPlayerError();//this is a fatal error, thus the client will be stopped in ClientComm
                }
                else if (level == "SameNameError" && !MPManager.IsServer())//someone with my name but I have been admitted into the game, will ignore it, otherwise, will quit
                {
                    Trace.WriteLine(MPManager.OnlineTrains.Players.Count);

                    if (MPManager.OnlineTrains.Players.Count < 1)
                    {
                        if (MPManager.Simulator.Confirmer != null)
                            MPManager.Simulator.Confirmer.Message(ConfirmLevel.Error, MPManager.Catalog.GetString("Name conflicted with people in the game, will play in single mode"));
                        throw new SameNameError();//this is a fatal error, thus the client will be stopped in ClientComm
                    }
                }
                else if (level == "SwitchWarning")
                {
                    MPManager.Instance().TrySwitch = false;
                    return;
                }
                else if (level == "SwitchOK")
                {
                    MPManager.Instance().TrySwitch = true;
                    return;
                }
                else if (level == "OverSpeedOK")
                {
                    MPManager.Instance().CheckSpad = false;
                    return;
                }
                else if (level == "NoOverSpeed")
                {
                    MPManager.Instance().CheckSpad = true;
                    return;
                }
                else if (level == "TimeCheck" && !MPManager.IsServer())
                {
                    var t = double.Parse(msgx, CultureInfo.InvariantCulture);
                    MPManager.Instance().serverTimeDifference = t - MPManager.Simulator.ClockTime;
                    return;
                }
                if (MPManager.Simulator.Confirmer != null)
                    MPManager.Simulator.Confirmer.Message(level == "Warning" ? ConfirmLevel.Warning : level == "Info" ? ConfirmLevel.Information : ConfirmLevel.None, msgx);

            }
        }

        public override string ToString()
        {
            string tmp = "MESSAGE " + user + "\t" + level + "\t" + msgx;
            return " " + tmp.Length + ": " + tmp;
        }
    }

#endregion MSGMessage

#region MSGControl
    //message to ask for the control of a train or confirm it
    public class MSGControl : Message
    {
        int num;
        string level;
        string user;
        float trainmaxspeed;
        public MSGControl(string m)
        {
            m.Trim();
            string[] t = m.Split('\t');
            user = t[0];
            level = t[1];
            num = int.Parse(t[2]);
            trainmaxspeed = float.Parse(t[3], CultureInfo.InvariantCulture);
        }

        public MSGControl(string u, string l, Train t)
        {
            user = u;
            level = l;
            num = t.Number;
            trainmaxspeed = (MPManager.Simulator.PlayerLocomotive as MSTSLocomotive).MaxSpeedMpS;
        }

        public override void HandleMsg()
        {
            if (MPManager.GetUserName() == user && level == "Confirm")
            {
                Train train = MPManager.Simulator.PlayerLocomotive.Train;
                train.TrainType = TrainType.Player; train.LeadLocomotive = MPManager.Simulator.PlayerLocomotive;
                InitializeBrakesCommand.Receiver = MPManager.Simulator.PlayerLocomotive.Train;
                train.InitializeSignals(false);
                if (MPManager.Simulator.Confirmer != null)
                    MPManager.Simulator.Confirmer.Information(MPManager.Catalog.GetString("You gained back the control of your train"));
                MPManager.Instance().RemoveUncoupledTrains(train);
            }
            else if (level == "Confirm") //server inform me that a train is now remote
            {
                foreach (var p in MPManager.OnlineTrains.Players)
                {
                    if (p.Key == user)
                    {
                        foreach (var t in MPManager.Simulator.Trains)
                        {
                            if (t.Number == this.num) p.Value.Train = t;
                        }
                        MPManager.Instance().RemoveUncoupledTrains(p.Value.Train);
                        p.Value.Train.TrainType = TrainType.Remote;
                        p.Value.Train.TrainMaxSpeedMpS = trainmaxspeed;
                        break;
                    }
                }
            }
            else if (MPManager.IsServer() && level == "Request")
            {
                foreach (var p in MPManager.OnlineTrains.Players)
                {
                    if (p.Key == user)
                    {
                        foreach (var t in MPManager.Simulator.Trains)
                        {
                            if (t.Number == this.num) p.Value.Train = t;
                        }
                        p.Value.Train.TrainType = TrainType.Remote;
                        p.Value.Train.TrainMaxSpeedMpS = trainmaxspeed;
                        p.Value.Train.InitializeSignals(false);
                        MPManager.Instance().RemoveUncoupledTrains(p.Value.Train);
                        MPManager.BroadCast((new MSGControl(user, "Confirm", p.Value.Train)).ToString());
                        break;
                    }
                }
            }
        }

        public override string ToString()
        {
            string tmp = "CONTROL " + user + "\t" + level + "\t" + num + "\t" + trainmaxspeed.ToString(CultureInfo.InvariantCulture);
            return " " + tmp.Length + ": " + tmp;
        }
    }

#endregion MSGControl

#region MSGLocoChange
    //message to add new train from either a string (received message), or a Train (building a message)
    public class MSGLocoChange : Message
    {
        int num;
        string engine;
        string user;
        string frontOrRearCab;
        public MSGLocoChange(string m)
        {
            m.Trim();
            string[] t = m.Split('\t');
            user = t[0];
            engine = t[1];
            frontOrRearCab = t[2];
            num = int.Parse(t[3]);
        }

        public MSGLocoChange(string u, string l, string f, Train t)
        {
            user = u;
            engine = l;
            frontOrRearCab = f;
            num = t.Number;
        }

        public override void HandleMsg()
        {
            foreach (var t in MPManager.Simulator.Trains)
            {
                foreach (var car in t.Cars)
                {
                    if (car.CarID == engine)
                    {
                        car.Train.LeadLocomotive = car;
                        (car.Train.LeadLocomotive as MSTSLocomotive).UsingRearCab = frontOrRearCab == "F" ? false : true;
                        foreach (var p in MPManager.OnlineTrains.Players)
                        {
                            if (p.Value.Train == t)
                            {
                                p.Value.LeadingLocomotiveID = car.CarID;
                                break;
                            }
                        }
                        if (MPManager.IsServer()) MPManager.BroadCast((new MSGLocoChange(user, engine, frontOrRearCab, t)).ToString());
                        return;
                    }
                }
            }
        }

        public override string ToString()
        {
            string tmp = "LOCCHANGE " + user + "\t" + engine + "\t" + frontOrRearCab + "\t" + num;
            return " " + tmp.Length + ": " + tmp;
        }
    }

#endregion MSGLocoChange

#region MSGEvent
    public class MSGEvent : Message
    {
        public string user;
        public string EventName;
        public int EventState;

        public MSGEvent(string m)
        {
            string[] tmp = m.Split(' ');
            if (tmp.Length != 3) throw new Exception("Parsing error " + m);
            user = tmp[0].Trim();
            EventName = tmp[1].Trim();
            EventState = int.Parse(tmp[2]);
        }

        public MSGEvent(string m, string e, int ID)
        {
            user = m.Trim();
            EventName = e;
            EventState = ID;
        }

        public override string ToString()
        {

            string tmp = "EVENT " + user + " " + EventName + " " + EventState;
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            if (user == MPManager.GetUserName()) return; //avoid myself
            Train t = MPManager.FindPlayerTrain(user);
            if (t == null) return;

            if (EventName == "HORN")
            {
                if (t.LeadLocomotive != null)
                {
                    t.LeadLocomotive.SignalEvent(EventState == 0 ? TrainEvent.HornOff : TrainEvent.HornOn);
                    MPManager.BroadCast(this.ToString()); //if the server, will broadcast
                }
            }
            else if (EventName == "PANTO1")
            {
                t.SignalEvent((EventState == 1 ? PowerSupplyEvent.RaisePantograph : PowerSupplyEvent.LowerPantograph), 1);

                MPManager.BroadCast(this.ToString()); //if the server, will broadcast
            }
            else if (EventName == "PANTO2")
            {
                t.SignalEvent((EventState == 1 ? PowerSupplyEvent.RaisePantograph : PowerSupplyEvent.LowerPantograph), 2);

                MPManager.BroadCast(this.ToString()); //if the server, will broadcast
            }
            else if (EventName == "PANTO3")
            {
                t.SignalEvent((EventState == 1 ? PowerSupplyEvent.RaisePantograph : PowerSupplyEvent.LowerPantograph), 3);

                MPManager.BroadCast(this.ToString()); //if the server, will broadcast
            }
            else if (EventName == "PANTO4")
            {
                t.SignalEvent((EventState == 1 ? PowerSupplyEvent.RaisePantograph : PowerSupplyEvent.LowerPantograph), 4);

                MPManager.BroadCast(this.ToString()); //if the server, will broadcast
            }
            else if (EventName == "BELL")
            {
                if (t.LeadLocomotive != null && t.LeadLocomotive is MSTSLocomotive)
                {
                    (t.LeadLocomotive as MSTSLocomotive).Bell = (EventState == 0 ? false : true);
                    t.LeadLocomotive.SignalEvent(EventState == 0 ? TrainEvent.BellOff : TrainEvent.BellOn);
                    MPManager.BroadCast(this.ToString()); //if the server, will broadcast
                }
            }
            else if (EventName == "WIPER")
            {
                if (t.LeadLocomotive != null) t.LeadLocomotive.SignalEvent(EventState == 0 ? TrainEvent.WiperOff : TrainEvent.WiperOn);
                MPManager.BroadCast(this.ToString()); //if the server, will broadcast
            }
            else if (EventName == "DOORL")
            {
                if (t.LeadLocomotive != null) ((MSTSWagon)(t.LeadLocomotive)).ToggleDoorsLeft();
                MPManager.BroadCast(this.ToString()); //if the server, will broadcast
            }
            else if (EventName == "DOORR")
            {
                if (t.LeadLocomotive != null) ((MSTSWagon)(t.LeadLocomotive)).ToggleDoorsRight();
                MPManager.BroadCast(this.ToString()); //if the server, will broadcast
            }
            else if (EventName == "MIRRORS")
            {
                if (t.LeadLocomotive != null) ((MSTSWagon)(t.LeadLocomotive)).ToggleMirrors();
                MPManager.BroadCast(this.ToString()); //if the server, will broadcast
            }
            else if (EventName == "HEADLIGHT")
            {
                if (t.LeadLocomotive != null && EventState == 0) t.LeadLocomotive.SignalEvent(TrainEvent.HeadlightOff);
                if (t.LeadLocomotive != null && EventState == 1) t.LeadLocomotive.SignalEvent(TrainEvent.HeadlightDim);
                if (t.LeadLocomotive != null && EventState == 2) t.LeadLocomotive.SignalEvent(TrainEvent.HeadlightOn);
                MPManager.BroadCast(this.ToString()); //if the server, will broadcast
            }
            else return;
        }

    }

#endregion MSGEvent

#region MSGQuit
    public class MSGQuit : Message
    {
        public string user;

        public MSGQuit(string m)
        {
            user = m.Trim();
        }

        public override string ToString()
        {

            string tmp = "QUIT " + user;
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            if (user == MPManager.GetUserName()) return; //avoid myself

            bool ServerQuit = false;
            if (MPManager.Client != null && user.Contains("ServerHasToQuit")) //the server quits, will send a message with ServerHasToQuit\tServerName
            {
                if (MPManager.Simulator.Confirmer != null)
                    MPManager.Simulator.Confirmer.Error(MPManager.Catalog.GetString("Server quits, will play as single mode"));
                user = user.Replace("ServerHasToQuit\t", ""); //get the user name of server from the message
                ServerQuit = true;
            }
            OnlinePlayer p = null;
            if (MPManager.OnlineTrains.Players.ContainsKey(user))
            {
                p = MPManager.OnlineTrains.Players[user];
            }
            if (p == null) return;
            if (MPManager.Simulator.Confirmer != null)
                MPManager.Simulator.Confirmer.Information(MPManager.Catalog.GetString("{0} quit.", this.user));
            if (MPManager.IsServer())
            {
                if (p.protect == true) { p.protect = false; return; }
                MPManager.BroadCast(this.ToString()); //if the server, will broadcast
                //if the one quit controls my train, I will gain back the control
                if (p.Train == MPManager.Simulator.PlayerLocomotive.Train)
                    MPManager.Simulator.PlayerLocomotive.Train.TrainType = TrainType.Player;
                MPManager.Instance().AddRemovedPlayer(p);
                //the client may quit because of lost connection, will remember it so it may recover in the future when the player log in again
                if (p.Train != null && p.status != OnlinePlayer.Status.Removed) //if this player has train and is not removed by the dispatcher
                {
                    if (!MPManager.Instance().lostPlayer.ContainsKey(p.Username)) MPManager.Instance().lostPlayer.Add(p.Username, p);
                    p.quitTime = MPManager.Simulator.GameTime;
                    p.Train.SpeedMpS = 0.0f;
                    p.status = OnlinePlayer.Status.Quit;
                }
                MPManager.BroadCast(this.ToString()); //broadcast twice

            }
            else //client will remove train
            {
                //if the one quit controls my train, I will gain back the control
                if (p.Train == MPManager.Simulator.PlayerLocomotive.Train)
                    MPManager.Simulator.PlayerLocomotive.Train.TrainType = TrainType.Player;
                MPManager.Instance().AddRemovedPlayer(p);
                if (ServerQuit)//warning, need to remove other player trains if there are not AI, in the future
                {
                    //no matter what, let player gain back the control of the player train
                    MPManager.Simulator.PlayerLocomotive.Train.TrainType = TrainType.Player;
                    throw new MultiPlayerError(); //server quit, end communication by throwing this error 
                }
            }
        }

    }

#endregion MSGQuit


#region MSGLost
    public class MSGLost : Message
    {
        public string user;

        public MSGLost(string m)
        {
            user = m.Trim();
        }

        public override string ToString()
        {

            string tmp = "LOST " + user;
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            if (user == MPManager.GetUserName()) return; //avoid myself

            if (!MPManager.IsServer())
            {
                return; //only server will handle this
            }
            OnlinePlayer p = null;
            if (MPManager.OnlineTrains.Players.ContainsKey(user))
            {
                p = MPManager.OnlineTrains.Players[user];
            }
            if (p == null) return;
            if (MPManager.Simulator.Confirmer != null)
                MPManager.Simulator.Confirmer.Information(MPManager.Catalog.GetString("{0} lost.", this.user));
            if (p.protect == true) { p.protect = false; return; }
            MPManager.BroadCast((new MSGQuit(user)).ToString()); //if the server, will broadcast a quit to every one
            //if the one quit controls my train, I will gain back the control
            if (p.Train == MPManager.Simulator.PlayerLocomotive.Train)
                MPManager.Simulator.PlayerLocomotive.Train.TrainType = TrainType.Player;
            MPManager.Instance().AddRemovedPlayer(p);
            //the client may quit because of lost connection, will remember it so it may recover in the future when the player log in again
            if (p.Train != null && p.status != OnlinePlayer.Status.Removed) //if this player has train and is not removed by the dispatcher
            {
                if (!MPManager.Instance().lostPlayer.ContainsKey(p.Username)) MPManager.Instance().lostPlayer.Add(p.Username, p);
                p.quitTime = MPManager.Simulator.GameTime;
                p.Train.SpeedMpS = 0.0f;
                p.status = OnlinePlayer.Status.Quit;
            }
            MPManager.BroadCast((new MSGQuit(user)).ToString()); //broadcast twice

        }

    }

#endregion MSGLost

#region MSGGetTrain
    public class MSGGetTrain : Message
    {
        public int num;
        public string user;

        public MSGGetTrain(string u, int m)
        {
            user = u; num = m;
        }

        public MSGGetTrain(string m)
        {
            string[] tmp = m.Split(' ');
            user = tmp[0]; num = int.Parse(tmp[1]);
        }

        public override string ToString()
        {

            string tmp = "GETTRAIN " + user + " " + num;
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            if (MPManager.IsServer())
            {
                foreach (Train t in MPManager.Simulator.Trains)
                {
                    if (t == null) continue;
                    if (t.Number == num) //found it, broadcast to everyone
                    {
                        MPManager.BroadCast((new MSGUpdateTrain(user, t, t.Number)).ToString());
                    }
                }
            }
        }

    }
#endregion MSGGetTrain

#region MSGUncouple

    public class MSGUncouple : Message
    {
        public string user, newTrainName, carID, firstCarIDOld, firstCarIDNew;
        public int mDirection1;
        public float Travelled1, Speed1;
        public int trainDirection;
        public int mDirection2;
        public float Travelled2, Speed2;
        public int train2Direction;
        public int newTrainNumber;
        public int oldTrainNumber;
        public int whichIsPlayer;
        string[] ids1;
        string[] ids2;
        int[] flipped1;
        int[] flipped2;

        private WorldLocation location1;
        private WorldLocation location2;

        static TrainCar FindCar(List<TrainCar> list, string id)
        {
            foreach (TrainCar car in list) if (car.CarID == id) return car;
            return null;
        }
        public MSGUncouple(string m)
        {
            string[] areas = m.Split('\t');
            user = areas[0].Trim();

            whichIsPlayer = int.Parse(areas[1].Trim());

            firstCarIDOld = areas[2].Trim();

            firstCarIDNew = areas[3].Trim();

            string[] tmp = areas[4].Split(' ');
            location1 = new WorldLocation(int.Parse(tmp[0]), int.Parse(tmp[1]), float.Parse(tmp[2], CultureInfo.InvariantCulture), 0, float.Parse(tmp[3], CultureInfo.InvariantCulture));
            Travelled1 = float.Parse(tmp[4], CultureInfo.InvariantCulture); Speed1 = float.Parse(tmp[5], CultureInfo.InvariantCulture); trainDirection = int.Parse(tmp[6]);
            oldTrainNumber = int.Parse(tmp[7]);
            mDirection1 = int.Parse(tmp[8]);
            tmp = areas[5].Split('\n');
            ids1 = new string[tmp.Length - 1];
            flipped1 = new int[tmp.Length - 1];
            for (var i = 0; i < ids1.Length; i++)
            {
                string[] field = tmp[i].Split('\r');
                ids1[i] = field[0].Trim();
                flipped1[i] = int.Parse(field[1].Trim());
            }

            tmp = areas[6].Split(' ');
            location2 = new WorldLocation(int.Parse(tmp[0]), int.Parse(tmp[1]), float.Parse(tmp[2], CultureInfo.InvariantCulture), 0, float.Parse(tmp[3], CultureInfo.InvariantCulture));
            Travelled2 = float.Parse(tmp[4], CultureInfo.InvariantCulture); Speed2 = float.Parse(tmp[5], CultureInfo.InvariantCulture); train2Direction = int.Parse(tmp[6]);
            newTrainNumber = int.Parse(tmp[7]);
            mDirection2 = int.Parse(tmp[8]);

            tmp = areas[7].Split('\n');
            ids2 = new string[tmp.Length - 1];
            flipped2 = new int[tmp.Length - 1];
            for (var i = 0; i < ids2.Length; i++)
            {
                string[] field = tmp[i].Split('\r');
                ids2[i] = field[0].Trim();
                flipped2[i] = int.Parse(field[1].Trim());
            }
        }

        public MSGUncouple(Train t, Train newT, string u, string ID, TrainCar car)
        {
            if (t.Cars.Count == 0 || newT.Cars.Count == 0) { user = ""; return; }//no cars in one of the train, not sure how to handle, so just return;
            Train temp = null; int tmpNum;
            if (!t.Cars.Contains(MPManager.Simulator.PlayerLocomotive))
            {//the old train should have the player, otherwise, 
                tmpNum = t.Number; t.Number = newT.Number; newT.Number = tmpNum;
                temp = t; t = newT; newT = temp;
            }
            carID = ID;
            user = u;
            //TileX1 = t.RearTDBTraveller.TileX; TileZ1 = t.RearTDBTraveller.TileZ; X1 = t.RearTDBTraveller.X; Z1 = t.RearTDBTraveller.Z;
            Travelled1 = t.DistanceTravelled; Speed1 = t.SpeedMpS;
            trainDirection = t.RearTDBTraveller.Direction == Traveller.TravellerDirection.Forward ? 0 : 1;//0 forward, 1 backward
            mDirection1 = (int)t.MUDirection;
            //TileX2 = newT.RearTDBTraveller.TileX; TileZ2 = newT.RearTDBTraveller.TileZ; X2 = newT.RearTDBTraveller.X; Z2 = newT.RearTDBTraveller.Z;
            Travelled2 = newT.DistanceTravelled; Speed2 = newT.SpeedMpS;
            train2Direction = newT.RearTDBTraveller.Direction == Traveller.TravellerDirection.Forward ? 0 : 1;//0 forward, 1 backward
            mDirection2 = (int)newT.MUDirection;

            if (MPManager.IsServer()) newTrainNumber = newT.Number;//serer will use the correct number
            else
            {
                newTrainNumber = 1000000 + MPManager.Random.Next(1000000);//client: temporary assign a train number 1000000-2000000, will change to the correct one after receiving response from the server
                newT.TrainType = TrainType.Remote; //by default, uncoupled train will be controlled by the server
            }
            if (!newT.Cars.Contains(MPManager.Simulator.PlayerLocomotive)) //if newT does not have player locomotive, it may be controlled remotely
            {
                if (newT.LeadLocomotive == null) { newT.LeadLocomotiveIndex = -1; newT.LeadNextLocomotive(); }

                foreach (TrainCar car1 in newT.Cars)
                {
                    car1.Train = newT;
                    foreach (var p in MPManager.OnlineTrains.Players)
                    {
                        if (car1.CarID.StartsWith(p.Value.LeadingLocomotiveID))
                        {
                            p.Value.Train = car1.Train;
                            car1.Train.TrainType = TrainType.Remote;
                            break;
                        }
                    }
                }
            }

            if (!t.Cars.Contains(MPManager.Simulator.PlayerLocomotive)) //if t (old train) does not have player locomotive, it may be controlled remotely
            {
                if (t.LeadLocomotive == null) { t.LeadLocomotiveIndex = -1; t.LeadNextLocomotive(); }

                foreach (TrainCar car1 in t.Cars)
                {
                    car1.Train = t;
                    foreach (var p in MPManager.OnlineTrains.Players)
                    {
                        if (car1.CarID.StartsWith(p.Value.LeadingLocomotiveID))
                        {
                            p.Value.Train = car1.Train;
                            car1.Train.TrainType = TrainType.Remote;
                            break;
                        }
                    }
                }
            }


            if (t.Cars.Contains(MPManager.Simulator.PlayerLocomotive) || newT.Cars.Contains(MPManager.Simulator.PlayerLocomotive))
            {
                if (MPManager.Simulator.Confirmer != null)
                    MPManager.Simulator.Confirmer.Information(MPManager.Catalog.GetString("Trains uncoupled, gain back control by Alt-E"));
            }

            /*
            //if one of the train holds other player's lead locomotives
            foreach (var pair in MPManager.OnlineTrains.Players)
            {
                string check = pair.Key + " - 0";
                foreach (var car1 in t.Cars) if (car1.CarID.StartsWith(check)) { t.TrainType = Train.TRAINTYPE.REMOTE; break; }
                foreach (var car1 in newT.Cars) if (car1.CarID.StartsWith(check)) { newT.TrainType = Train.TRAINTYPE.REMOTE; break; }
            }*/
            oldTrainNumber = t.Number;
            newTrainName = "UC" + newTrainNumber; newT.Number = newTrainNumber;

            if (newT.LeadLocomotive != null) firstCarIDNew = "Leading " + newT.LeadLocomotive.CarID;
            else firstCarIDNew = "First " + newT.Cars[0].CarID;

            if (t.LeadLocomotive != null) firstCarIDOld = "Leading " + t.LeadLocomotive.CarID;
            else firstCarIDOld = "First " + t.Cars[0].CarID;

            ids1 = new string[t.Cars.Count];
            flipped1 = new int[t.Cars.Count];
            for (var i = 0; i < ids1.Length; i++)
            {
                ids1[i] = t.Cars[i].CarID;
                flipped1[i] = t.Cars[i].Flipped == true ? 1 : 0;
            }

            ids2 = new string[newT.Cars.Count];
            flipped2 = new int[newT.Cars.Count];
            for (var i = 0; i < ids2.Length; i++)
            {
                ids2[i] = newT.Cars[i].CarID;
                flipped2[i] = newT.Cars[i].Flipped == true ? 1 : 0;
            }

            //to see which train contains the car (PlayerLocomotive)
            if (t.Cars.Contains(car)) whichIsPlayer = 0;
            else if (newT.Cars.Contains(car)) whichIsPlayer = 1;
            else whichIsPlayer = 2;
        }

        string FillInString(int i)
        {
            string tmp = "";
            if (i == 1)
            {
                for (var j = 0; j < ids1.Length; j++)
                {
                    tmp += ids1[j] + "\r" + flipped1[j] + "\n";
                }
            }
            else
            {
                for (var j = 0; j < ids2.Length; j++)
                {
                    tmp += ids2[j] + "\r" + flipped2[j] + "\n";
                }
            }
            return tmp;
        }
        public override string ToString()
        {
            if (user == "") return "5: ALIVE"; //wrong, so just return an ALIVE string
            string tmp = "UNCOUPLE " + user + "\t" + whichIsPlayer + "\t" + firstCarIDOld + "\t" + firstCarIDNew
                + "\t" + location1.TileX + " " + location1.TileZ + " " + location1.Location.X.ToString(CultureInfo.InvariantCulture) + " " + location1.Location.Z.ToString(CultureInfo.InvariantCulture) + " " + Travelled1.ToString(CultureInfo.InvariantCulture) + " " + Speed1.ToString(CultureInfo.InvariantCulture) + " " + trainDirection + " " + oldTrainNumber + " " + mDirection1 + "\t"
                + FillInString(1)
                + "\t" + location2.TileX + " " + location2.TileZ + " " + location2.Location.X.ToString(CultureInfo.InvariantCulture) + " " + location2.Location.Z.ToString(CultureInfo.InvariantCulture) + " " + Travelled2.ToString(CultureInfo.InvariantCulture) + " " + Speed2.ToString(CultureInfo.InvariantCulture) + " " + train2Direction + " " + newTrainNumber + " " + mDirection2 + "\t"
                + FillInString(2);
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            bool oldIDIsLead = true, newIDIsLead = true;
            if (firstCarIDNew.StartsWith("First "))
            {
                firstCarIDNew = firstCarIDNew.Replace("First ", "");
                newIDIsLead = false;
            }
            else firstCarIDNew = firstCarIDNew.Replace("Leading ", "");
            if (firstCarIDOld.StartsWith("First "))
            {
                firstCarIDOld = firstCarIDOld.Replace("First ", "");
                oldIDIsLead = false;
            }
            else firstCarIDOld = firstCarIDOld.Replace("Leading ", "");

            if (user == MPManager.GetUserName()) //received from the server, but it is about mine action of uncouple
            {
                foreach (Train t in MPManager.Simulator.Trains)
                {
                    foreach (TrainCar car in t.Cars)
                    {
                        if (car.CarID == firstCarIDOld)//got response about this train
                        {
                            t.Number = oldTrainNumber;
                            if (oldIDIsLead == true) t.LeadLocomotive = car;
                        }
                        if (car.CarID == firstCarIDNew)//got response about this train
                        {
                            t.Number = newTrainNumber;
                            if (newIDIsLead == true) t.LeadLocomotive = car;
                        }
                    }
                }

            }
            else
            {
                TrainCar lead = null;
                Train train = null;
                List<TrainCar> trainCars = null;
                bool canPlace = true;
                TrackCircuitPartialPathRoute tempRoute;
                foreach (Train t in MPManager.Simulator.Trains)
                {
                    var found = false;
                    foreach (TrainCar car in t.Cars)
                    {
                        if (car.CarID == firstCarIDOld)//got response about this train
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found == true)
                    {
                        train = t;
                        lead = train.LeadLocomotive;
                        trainCars = t.Cars;
                        List<TrainCar> tmpcars = new List<TrainCar>();
                        for (var i = 0; i < ids1.Length; i++)
                        {
                            TrainCar car = FindCar(trainCars, ids1[i]);
                            if (car == null) continue;
                            car.Flipped = flipped1[i] == 0 ? false : true;
                            tmpcars.Add(car);
                        }
                        if (tmpcars.Count == 0) return;
                        if (MPManager.IsServer()) MPManager.Instance().AddOrRemoveLocomotives(user, train, false);
                        t.Cars.Clear();
                        t.Cars.AddRange(tmpcars);
                        Traveller.TravellerDirection d1 = Traveller.TravellerDirection.Forward;
                        if (trainDirection == 1) d1 = Traveller.TravellerDirection.Backward;
                        t.RearTDBTraveller = new Traveller(MPManager.Simulator.TSectionDat, MPManager.Simulator.TDB.TrackDB.TrackNodes, location1, d1);
                        t.DistanceTravelled = Travelled1;
                        t.SpeedMpS = Speed1;
                        t.LeadLocomotive = lead;
                        t.MUDirection = (MidpointDirection)mDirection1;
                        train.ControlMode = TrainControlMode.Explorer;
                        train.CheckFreight();
                        train.InitializeBrakes();
                        tempRoute = train.CalculateInitialTrainPosition();
                        if (tempRoute.Count == 0)
                        {
                            throw new InvalidDataException("Remote train original position not clear");
                        }

                        train.SetInitialTrainRoute(tempRoute);
                        train.CalculatePositionOfCars();
                        train.ResetInitialTrainRoute(tempRoute);

                        train.CalculatePositionOfCars();
                        train.AITrainBrakePercent = 100;
                        //train may contain myself, and no other players, thus will make myself controlling this train
                        if (train.Cars.Contains(MPManager.Simulator.PlayerLocomotive))
                        {
                            MPManager.Simulator.PlayerLocomotive.Train = train;
                            //train.TrainType = Train.TRAINTYPE.PLAYER;
                            train.InitializeBrakes();
                        }
                        foreach (var c in train.Cars)
                        {
                            if (c.CarID == firstCarIDOld && oldIDIsLead) train.LeadLocomotive = c;
                            foreach (var p in MPManager.OnlineTrains.Players)
                            {
                                if (p.Value.LeadingLocomotiveID == c.CarID) p.Value.Train = train;
                            }
                        }
                        break;
                    }
                }

                if (train == null || trainCars == null) return;

                Train train2 = new Train();
                List<TrainCar> tmpcars2 = new List<TrainCar>();
                for (var i = 0; i < ids2.Length; i++)
                {
                    TrainCar car = FindCar(trainCars, ids2[i]);
                    if (car == null) continue;
                    tmpcars2.Add(car);
                    car.Flipped = flipped2[i] == 0 ? false : true;
                }
                if (tmpcars2.Count == 0) return;
                train2.Cars.Clear();
                train2.Cars.AddRange(tmpcars2);
                train2.Name = String.Concat(train.Name, Train.TotalNumber.ToString());
                train2.LeadLocomotive = null;
                train2.LeadNextLocomotive();
                train2.CheckFreight();

                //train2 may contain myself, and no other players, thus will make myself controlling this train
                /*if (train2.Cars.Contains(MPManager.Simulator.PlayerLocomotive))
                {
                    var gainControl = true;
                    foreach (var pair in MPManager.OnlineTrains.Players)
                    {
                        string check = pair.Key + " - 0";
                        foreach (var car1 in train2.Cars) if (car1.CarID.StartsWith(check)) { gainControl = false; break; }
                    }
                    if (gainControl == true) { train2.TrainType = Train.TRAINTYPE.PLAYER; train2.LeadLocomotive = MPManager.Simulator.PlayerLocomotive; }
                }*/
                Traveller.TravellerDirection d2 = Traveller.TravellerDirection.Forward;
                if (train2Direction == 1) d2 = Traveller.TravellerDirection.Backward;

                // and fix up the travellers
                train2.RearTDBTraveller = new Traveller(MPManager.Simulator.TSectionDat, MPManager.Simulator.TDB.TrackDB.TrackNodes, location2, d2);
                train2.DistanceTravelled = Travelled2;
                train2.SpeedMpS = Speed2;
                train2.MUDirection = (MidpointDirection)mDirection2;
                train2.ControlMode = TrainControlMode.Explorer;
                train2.CheckFreight();
                train2.InitializeBrakes();
                tempRoute = train2.CalculateInitialTrainPosition();
                if (tempRoute.Count == 0)
                {
                    throw new InvalidDataException("Remote train original position not clear");
                }

                train2.SetInitialTrainRoute(tempRoute);
                train2.CalculatePositionOfCars();
                train2.ResetInitialTrainRoute(tempRoute);

                train2.CalculatePositionOfCars();
                train2.AITrainBrakePercent = 100;
                if (train2.Cars.Contains(MPManager.Simulator.PlayerLocomotive))
                {
                    MPManager.Simulator.PlayerLocomotive.Train = train2;
                    //train2.TrainType = Train.TRAINTYPE.PLAYER;
                    train2.InitializeBrakes();
                }
                foreach (TrainCar car in train2.Cars)
                {
                    if (car.CarID == firstCarIDNew && newIDIsLead) train2.LeadLocomotive = car;
                    car.Train = train2;
                    foreach (var p in MPManager.OnlineTrains.Players)
                    {
                        if (car.CarID.StartsWith(p.Value.LeadingLocomotiveID))
                        {
                            p.Value.Train = car.Train;
                            //car.Train.TrainType = Train.TRAINTYPE.REMOTE;
                            break;
                        }
                    }
                }

                train.UncoupledFrom = train2;
                train2.UncoupledFrom = train;

                if (train.Cars.Contains(MPManager.Simulator.PlayerLocomotive) || train2.Cars.Contains(MPManager.Simulator.PlayerLocomotive))
                {
                    if (MPManager.Simulator.Confirmer != null)
                        MPManager.Simulator.Confirmer.Information(MPManager.Catalog.GetString("Trains uncoupled, gain back control by Alt-E"));
                }

                //if (whichIsPlayer == 0 && MPManager.OnlineTrains.findTrain(user) != null) MPManager.OnlineTrains.Players[user].Train = train;
                //else if (whichIsPlayer == 1 && MPManager.OnlineTrains.findTrain(user) != null) MPManager.OnlineTrains.Players[user].Train = train2; //the player may need to update the train it drives
                MPManager.Instance().AddOrRemoveTrain(train2, true);

                if (MPManager.IsServer())
                {
                    this.newTrainNumber = train2.Number;//we got a new train number, will tell others.
                    train2.TrainType = TrainType.Static;
                    this.oldTrainNumber = train.Number;
                    train2.LastReportedSpeed = 1;
                    if (train2.Name.Length < 4) train2.Name = String.Concat("STATIC-", train2.Name);
                    MPManager.Simulator.AI.aiListChanged = true;
                    MPManager.Instance().AddOrRemoveLocomotives(user, train, true);
                    MPManager.Instance().AddOrRemoveLocomotives(user, train2, true);
                    MPManager.BroadCast(this.ToString());//if server receives this, will tell others, including whoever sent the information
                }
                else
                {
                    train2.TrainType = TrainType.Remote;
                    train2.Number = this.newTrainNumber; //client receives a message, will use the train number specified by the server
                    train.Number = this.oldTrainNumber;
                }
                //if (MPManager.IsServer() && MPManager.Instance().AllowedManualSwitch) train2.InitializeSignals(false);
            }
        }
    }
#endregion MSGUncouple

#region MSGCouple
    public class MSGCouple : Message
    {
        string[] cars;
        string[] ids;
        int[] flipped; //if a wagon is engine
        int TrainNum;
        int RemovedTrainNum;
        int direction;
        int Lead, mDirection;
        float Travelled;
        string whoControls;

        private WorldLocation location;

        public MSGCouple(string m)
        {
            //Trace.WriteLine(m);
            int index = m.IndexOf(' '); int last = 0;
            TrainNum = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            RemovedTrainNum = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            direction = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            int tileX = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            int tileZ = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            float x = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            float z = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            location = new WorldLocation(tileX, tileZ, x, 0, z);
            Travelled = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            Lead = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            whoControls = m.Substring(0, index + 1).Trim();
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            mDirection = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            string[] areas = m.Split('\t');
            cars = new string[areas.Length - 1];//with an empty "" at end
            ids = new string[areas.Length - 1];
            flipped = new int[areas.Length - 1];
            for (var i = 0; i < cars.Length; i++)
            {
                index = areas[i].IndexOf('\"');
                last = areas[i].LastIndexOf('\"');
                cars[i] = areas[i].Substring(index + 1, last - index - 1);
                string tmp = areas[i].Remove(0, last + 1);
                tmp = tmp.Trim();
                string[] carinfo = tmp.Split('\n');
                ids[i] = carinfo[0];
                flipped[i] = int.Parse(carinfo[1]);
            }

            //Trace.WriteLine(this.ToString());

        }

        public MSGCouple(Train t, Train oldT, bool remove)
        {
            cars = new string[t.Cars.Count];
            ids = new string[t.Cars.Count];
            flipped = new int[t.Cars.Count];
            for (var i = 0; i < t.Cars.Count; i++)
            {
                cars[i] = t.Cars[i].RealWagFilePath;
                ids[i] = t.Cars[i].CarID;
                if (t.Cars[i].Flipped == true) flipped[i] = 1;
                else flipped[i] = 0;
            }
            TrainNum = t.Number;
            RemovedTrainNum = oldT.Number;
            direction = t.RearTDBTraveller.Direction == Traveller.TravellerDirection.Forward ? 0 : 1;
            location = t.RearTDBTraveller.WorldLocation;
            Travelled = t.DistanceTravelled;
            MPManager.Instance().RemoveUncoupledTrains(t); //remove the trains from uncoupled train lists
            MPManager.Instance().RemoveUncoupledTrains(oldT);
            var j = 0;
            Lead = -1;
            foreach (TrainCar car in t.Cars)
            {
                if (car == t.LeadLocomotive) { Lead = j; break; }
                j++;
            }
            whoControls = "NA";
            var index = 0;
            if (t.LeadLocomotive != null) index = t.LeadLocomotive.CarID.IndexOf(" - 0");
            if (index > 0)
            {
                whoControls = t.LeadLocomotive.CarID.Substring(0, index);
            }
            foreach (var p in MPManager.OnlineTrains.Players)
            {
                if (p.Value.Train == oldT) { p.Value.Train = t; break; }
            }
            mDirection = (int)t.MUDirection;
            if (t.Cars.Contains(MPManager.Simulator.PlayerLocomotive))
            {
                if (MPManager.Simulator.Confirmer != null)
                    MPManager.Simulator.Confirmer.Information(MPManager.Catalog.GetString("Trains coupled, hit \\ then Shift-? to release brakes"));
            }
            if (!MPManager.IsServer() || !(oldT.TrainType == TrainType.AiIncorporated))
            {
                if (MPManager.IsServer())
                {
                    MPManager.Instance().AddOrRemoveLocomotives("", oldT, false);
                    MPManager.Instance().AddOrRemoveLocomotives("", t, false);
                    var player = "";
                    foreach (var p in MPManager.OnlineTrains.Players)
                    {
                        if (p.Value.Train == t) { player = p.Key; break; }
                    }
                    MPManager.Instance().AddOrRemoveLocomotives(player, t, true);
                }
                MPManager.Instance().AddOrRemoveTrain(oldT, false); //remove the old train



            }
        }

        public override string ToString()
        {
            string tmp = "COUPLE " + TrainNum + " " + RemovedTrainNum + " " + direction + " " + location.TileX + " " + location.TileZ + " " + location.Location.X.ToString(CultureInfo.InvariantCulture) + " " + location.Location.Z.ToString(CultureInfo.InvariantCulture) + " " + Travelled.ToString(CultureInfo.InvariantCulture) + " " + Lead + " " + whoControls + " " + mDirection + " ";
            for (var i = 0; i < cars.Length; i++)
            {
                var c = cars[i];
                var index = c.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    c = c.Remove(0, index + 17);
                }//c: wagon path without folder name

                tmp += "\"" + c + "\"" + " " + ids[i] + "\n" + flipped[i] + "\t";
            }
            return " " + tmp.Length + ": " + tmp;
        }

        static TrainCar FindCar(Train t1, Train t2, string carID)
        {
            foreach (TrainCar c in t1.Cars) if (c.CarID == carID) return c;
            foreach (TrainCar c in t2.Cars) if (c.CarID == carID) return c;
            return null;
        }
        public override void HandleMsg()
        {
            if (MPManager.IsServer()) return;//server will not receive this from client
            Train train = null, train2 = null;

            foreach (Train t in MPManager.Simulator.Trains)
            {
                if (t.Number == this.TrainNum) train = t;
                if (t.Number == this.RemovedTrainNum) train2 = t;
            }

            TrainCar lead = train.LeadLocomotive;
            if (lead == null) lead = train2.LeadLocomotive;

            /*if (MPManager.Simulator.PlayerLocomotive != null && MPManager.Simulator.PlayerLocomotive.Train == train2)
            {
                Train tmp = train2; train2 = train; train = tmp; MPManager.Simulator.PlayerLocomotive.Train = train;
            }*/

            if (train == null || train2 == null) return; //did not find the trains to op on

            //if (consistDirection != 1)
            //	train.RearTDBTraveller.ReverseDirection();
            List<TrainCar> tmpCars = new List<TrainCar>();
            for (var i = 0; i < cars.Length; i++)// cars.Length-1; i >= 0; i--) {
            {
                TrainCar car = FindCar(train, train2, ids[i]);
                if (car == null) continue;
                bool flip = true;
                if (flipped[i] == 0) flip = false;
                car.Flipped = flip;
                car.CarID = ids[i];
                tmpCars.Add(car);
                car.Train = train;

            }// for each rail car
            if (tmpCars.Count == 0) return;
            //List<TrainCar> oldList = train.Cars;
            train.Cars.Clear();
            train.Cars.AddRange(tmpCars);

            train.DistanceTravelled = Travelled;
            train.MUDirection = (MidpointDirection)mDirection;
            train.RearTDBTraveller = new Traveller(MPManager.Simulator.TSectionDat, MPManager.Simulator.TDB.TrackDB.TrackNodes, location, direction == 0 ? Traveller.TravellerDirection.Forward : Traveller.TravellerDirection.Backward);
            train.CheckFreight();
            train.CalculatePositionOfCars();
            train.LeadLocomotive = null; train2.LeadLocomotive = null;
            if (Lead != -1 && Lead < train.Cars.Count) train.LeadLocomotive = train.Cars[Lead];

            if (train.LeadLocomotive == null) train.LeadNextLocomotive();

            //mine is not the leading locomotive, thus I give up the control
            if (train.LeadLocomotive != MPManager.Simulator.PlayerLocomotive)
            {
                train.TrainType = TrainType.Remote; //make the train remote controlled
            }

            if (MPManager.FindPlayerTrain(train2))
            {
                int count = 0;
                while (count < 3)
                {
                    try
                    {
                        foreach (var p in MPManager.OnlineTrains.Players)
                        {
                            if (p.Value.Train == train2) p.Value.Train = train;
                        }
                        break;
                    }
                    catch (Exception) { count++; }
                }
            }

            //update the remote user's train
            if (MPManager.FindPlayerTrain(whoControls) != null) MPManager.OnlineTrains.Players[whoControls].Train = train;
            if (train.Cars.Contains(MPManager.Simulator.PlayerLocomotive)) MPManager.Simulator.PlayerLocomotive.Train = train;

            if (MPManager.IsServer()) MPManager.Instance().AddOrRemoveLocomotives("", train2, false);
            MPManager.Instance().AddOrRemoveTrain(train2, false);


            if (train.Cars.Contains(MPManager.Simulator.PlayerLocomotive))
            {
                if (MPManager.Simulator.Confirmer != null)
                    MPManager.Simulator.Confirmer.Information(MPManager.Catalog.GetString("Trains coupled, hit \\ then Shift-? to release brakes"));
            }
        }
    }
#endregion MSGCouple

#region MSGSignalStatus
    public class MSGSignalStatus : Message
    {
        static byte[] preState;
        static SortedList<long, SignalHead> signals;
        public bool OKtoSend = false;
        static byte[] signalsStates;
        //constructor to create a message from signal data
        public MSGSignalStatus()
        {
            var i = 0;
            if (signals == null)
            {
                signals = new SortedList<long, SignalHead>();
                if (MPManager.Simulator.SignalEnvironment.Signals != null)
                {
                    foreach (var s in MPManager.Simulator.SignalEnvironment.Signals)
                    {
                        if (s != null && (s.IsSignal || s.IsSpeedSignal) && s.SignalHeads != null)
                            foreach (var h in s.SignalHeads)
                            {
                                //Trace.WriteLine(h.TDBIndex);
                                signals.Add(h.TDBIndex * 100000 + h.TrackItemIndex, h);
                            }
                    }
                }
                signalsStates = new byte[signals.Count * 2 + 2];
            }
            if (preState == null)
            {
                preState = new byte[signals.Count * 2 + 2];
                for (i = 0; i < preState.Length; i++) preState[i] = 0;
            }

            i = 0;
            foreach (var t in signals)
            {
                signalsStates[2 * i] = (byte)(t.Value.SignalIndicationState + 1);
                signalsStates[2 * i + 1] = (byte)(t.Value.DrawState + 1);
                i++;
                //msgx += (char)(((int)t.Value.state + 1) * 100 + (t.Value.draw_state + 1));
                //msgx += "" + (char)(t.Value.state + 1) + "" + (char)(t.Value.draw_state + 1);//avoid \0
            }
            OKtoSend = false;
            for (i = 0; i < signals.Count * 2; i++)
            {
                if (signalsStates[i] != preState[i]) { OKtoSend = true; }//something is different, will send
                preState[i] = signalsStates[i];
            }
            if (OKtoSend == false)
            {
                //new player added, will keep sending for a while
                if (MPManager.Simulator.GameTime - MPManager.Instance().lastPlayerAddedTime < 3 * MPManager.Instance().MPUpdateInterval) OKtoSend = true;
            }
        }

        //constructor to decode the message "m"
        public MSGSignalStatus(string m)
        {
            if (signals == null)
            {
                signals = new SortedList<long, SignalHead>();
                try
                {
                    if (MPManager.Simulator.SignalEnvironment.Signals != null)
                    {
                        foreach (var s in MPManager.Simulator.SignalEnvironment.Signals)
                        {
                            if (s != null && (s.IsSignal || s.IsSpeedSignal) && s.SignalHeads != null)
                                foreach (var h in s.SignalHeads)
                                {
                                    //Trace.WriteLine(h.TDBIndex);
                                    signals.Add(h.TDBIndex * 100000 + h.TrackItemIndex, h);
                                }
                        }
                    }
                    signalsStates = new byte[signals.Count * 2 + 128];
                }
                catch (Exception e) { signals = null; throw e; }//error, clean the list, so we can get another signal
            }
            byte[] gZipBuffer = Convert.FromBase64String(m);
            using (var memoryStream = new MemoryStream())
            {
                BitConverter.ToInt32(gZipBuffer, 0);
                memoryStream.Write(gZipBuffer, 4, gZipBuffer.Length - 4);

                memoryStream.Position = 0;
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    gZipStream.Read(signalsStates, 0, signalsStates.Length);
                }
            }
        }

        //how to handle the message?
        public override void HandleMsg() //only client will get message, thus will set states
        {
            if (MPManager.Server != null) return; //server will ignore it

            //if (signals.Count != readed/2-2) { Trace.WriteLine("Error in synchronizing signals " + signals.Count + " " + readed); return; }
            int i = 0;
            foreach (var t in signals)
            {
                t.Value.SignalIndicationState = (SignalAspectState)(signalsStates[2 * i] - 1); //we added 1 when build the message, need to subtract it out
                t.Value.DrawState = (int)(signalsStates[2 * i + 1] - 1);
                //t.Value.draw_state = t.Value.def_draw_state(t.Value.state);
                //Trace.Write(msgx[i]-48);
                i++;
            }
            //Trace.Write("\n");

        }

        public override string ToString()
        {
            byte[] buffer = signalsStates;
            var memoryStream = new MemoryStream();
            using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
            {
                gZipStream.Write(buffer, 0, buffer.Length);
            }

            memoryStream.Position = 0;

            var compressedData = new byte[memoryStream.Length];
            memoryStream.Read(compressedData, 0, compressedData.Length);

            var gZipBuffer = new byte[compressedData.Length + 4];
            Buffer.BlockCopy(compressedData, 0, gZipBuffer, 4, compressedData.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gZipBuffer, 0, 4);
            string tmp = "SIGNALSTATES " + Convert.ToBase64String(gZipBuffer); // fill in the message body here
            return " " + tmp.Length + ": " + tmp;
        }
    }
#endregion MSGSignalStatus

#region MSGLocoInfo
    public class MSGLocoInfo : Message
    {

        float EB, DB, TT, VL, CC, BC, DC, FC, I1, I2, SH, SE, LE;
        string user;
        int tnum; //train number

        //constructor to create a message from signal data
        public MSGLocoInfo(TrainCar c, string u)
        {
            MSTSLocomotive loco = (MSTSLocomotive)c;
            EB = DB = TT = VL = CC = BC = DC = FC = I1 = I2 = SH = SE = LE = 0.0f;
            if (loco is MSTSSteamLocomotive)
            {
                MSTSSteamLocomotive loco1 = (MSTSSteamLocomotive)loco;
                loco1.GetLocoInfo(ref CC, ref BC, ref DC, ref FC, ref I1, ref I2, ref SE, ref LE);
            }
            if (loco.SteamHeatController != null)
            {
                SH = loco.SteamHeatController.CurrentValue;
            }
            if (loco.EngineBrakeController != null)
            {
                EB = loco.EngineBrakeController.CurrentValue;
            }
            if (loco.DynamicBrakeController != null)
            {
                DB = loco.DynamicBrakeController.CurrentValue;
            }
            TT = loco.ThrottleController.CurrentValue;
            if (loco is MSTSElectricLocomotive)
            {
                VL = (loco as MSTSElectricLocomotive).PowerSupply.FilterVoltageV;
            }
            tnum = loco.Train.Number;
            user = u;
        }

        //constructor to decode the message "m"
        public MSGLocoInfo(string m)
        {
            string[] tmp = m.Split('\t');
            user = tmp[0].Trim();
            tnum = int.Parse(tmp[1]);
            EB = float.Parse(tmp[2], CultureInfo.InvariantCulture);
            DB = float.Parse(tmp[3], CultureInfo.InvariantCulture);
            TT = float.Parse(tmp[4], CultureInfo.InvariantCulture);
            VL = float.Parse(tmp[5], CultureInfo.InvariantCulture);
            CC = float.Parse(tmp[6], CultureInfo.InvariantCulture);
            BC = float.Parse(tmp[7], CultureInfo.InvariantCulture);
            DC = float.Parse(tmp[8], CultureInfo.InvariantCulture);
            FC = float.Parse(tmp[9], CultureInfo.InvariantCulture);
            I1 = float.Parse(tmp[10], CultureInfo.InvariantCulture);
            I2 = float.Parse(tmp[11], CultureInfo.InvariantCulture);
            SH = float.Parse(tmp[12], CultureInfo.InvariantCulture);
            SE = float.Parse(tmp[13], CultureInfo.InvariantCulture);
            LE = float.Parse(tmp[14], CultureInfo.InvariantCulture);
        }

        //how to handle the message?
        public override void HandleMsg() //only client will get message, thus will set states
        {
            foreach (Train t in MPManager.Simulator.Trains)
            {
                if (t.TrainType != TrainType.Remote && t.Number == tnum)
                {
                    foreach (var car in t.Cars)
                    {
                        if (car.CarID.StartsWith(user) && car is MSTSLocomotive)
                        {
                            updateValue((MSTSLocomotive)car);
                        }
                    }
                    return;
                }
            }
        }

        private void updateValue(MSTSLocomotive loco)
        {
            if (loco is MSTSSteamLocomotive)
            {
                MSTSSteamLocomotive loco1 = (MSTSSteamLocomotive)loco;
                loco1.GetLocoInfo(ref CC, ref BC, ref DC, ref FC, ref I1, ref I2, ref SE, ref LE);
            }
            if (loco.SteamHeatController != null)
            {
                SH = loco.SteamHeatController.CurrentValue;
            }
            if (loco.EngineBrakeController != null)
            {
                loco.EngineBrakeController.CurrentValue = EB;
                loco.EngineBrakeController.UpdateValue = 0.0f;
            }
            if (loco.DynamicBrakeController != null)
            {
                loco.DynamicBrakeController.CurrentValue = DB;
                loco.DynamicBrakeController.UpdateValue = 0.0f;
            }

            loco.ThrottleController.CurrentValue = TT;
            loco.ThrottleController.UpdateValue = 0.0f;
            if (loco is MSTSElectricLocomotive)
            {
                (loco as MSTSElectricLocomotive).PowerSupply.FilterVoltageV = VL;
            }
            loco.notificationReceived = true;
        }
        public override string ToString()
        {
            string tmp = "LOCOINFO " + user + "\t" + tnum + "\t" + EB.ToString(CultureInfo.InvariantCulture) + "\t" + DB.ToString(CultureInfo.InvariantCulture) + "\t" +
                TT.ToString(CultureInfo.InvariantCulture) + "\t" + VL.ToString(CultureInfo.InvariantCulture) + "\t" + CC.ToString(CultureInfo.InvariantCulture) + "\t" +
                BC.ToString(CultureInfo.InvariantCulture) + "\t" + DC.ToString(CultureInfo.InvariantCulture) + "\t" + FC.ToString(CultureInfo.InvariantCulture) + "\t" +
                I1.ToString(CultureInfo.InvariantCulture) + "\t" + I2.ToString(CultureInfo.InvariantCulture); // fill in the message body here
            return " " + tmp.Length + ": " + tmp;
        }
    }
#endregion MSGLocoInfo

#region MSGAvatar
    public class MSGAvatar : Message
    {
        public string user;
        public string url;
        public MSGAvatar(string m)
        {
            var tmp = m.Split('\t');
            user = tmp[0].Trim();
            url = tmp[1];
        }

        public MSGAvatar(string u, string l)
        {
            user = u;
            url = l;
        }

        public override string ToString()
        {

            string tmp = "AVATAR " + user + "\n" + url;
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            if (user == MPManager.GetUserName()) return; //avoid myself

            foreach (var p in MPManager.OnlineTrains.Players)
            {
                if (p.Key == user) p.Value.url = url;
                MPManager.Instance().OnAvatarUpdated(user, url);
            }

            if (MPManager.IsServer())
            {
                MPManager.BroadCast((new MSGAvatar(user, url)).ToString());
            }
        }

    }

#endregion MSGAvatar


#region MSGText
    //message to add new train from either a string (received message), or a Train (building a message)
    public class MSGText : MSGRequired
    {
        string msgx;
        string sender;
        string user;
        public MSGText(string m)
        {
            string[] t = m.Split('\t');
            sender = t[0].Trim();
            user = t[1].Trim();
            msgx = t[2];

        }

        public MSGText(string s, string u, string m)
        {
            sender = s.Trim();
            user = u;
            msgx = m;
        }

        public override void HandleMsg()
        {
            if (sender == MPManager.GetUserName()) return; //avoid my own msg
            string[] users = user.Split('\r');
            foreach (var name in users)
            {
                //someone may send a message with 0Server, which is intended for the server
                if (name.Trim() == MPManager.GetUserName() || (MPManager.IsServer() && name.Trim() == "0Server"))
                {
                    Trace.WriteLine("MSG from " + sender + ":" + msgx);
                    MPManager.Instance().lastSender = sender;
                    if (MPManager.Simulator.Confirmer != null) MPManager.Simulator.Confirmer.MSG(MPManager.Catalog.GetString(" From {0}: {1}", sender, msgx));
                    MPManager.Instance().OnMessageReceived(MPManager.Simulator.GameTime, sender + ": " + msgx);
                    break;
                }
            }
            if (MPManager.IsServer())//server check if need to tell others.
            {
                //Trace.WriteLine(users);
                if (users.Count() == 1 && users[0].Trim() == MPManager.GetUserName()) return;
                if (users.Count() == 1 && users[0].Trim() == "0Server") return;
                //Trace.WriteLine(this.ToString());
                MultiPlayer.MPManager.BroadCast(this.ToString());
            }
        }

        public override string ToString()
        {
            string tmp = "TEXT " + sender + "\t" + user + "\t" + msgx;
            return " " + tmp.Length + ": " + tmp;
        }
    }

#endregion MSGText

#region MSGWeather
    public class MSGWeather : Message
    {
        public int weather;
        public float overcast;
        public float pricipitation;
        public float fog;

        public MSGWeather(string m)
        {
            weather = -1; overcast = pricipitation = fog = -1;
            var tmp = m.Split(' ');
            weather = int.Parse(tmp[0]);
            overcast = float.Parse(tmp[1], CultureInfo.InvariantCulture);
            pricipitation = float.Parse(tmp[2], CultureInfo.InvariantCulture);
            fog = float.Parse(tmp[3], CultureInfo.InvariantCulture);
        }

        public MSGWeather(int w, float o, float p, float f)
        {
            weather = -1; overcast = pricipitation = fog = -1;
            if (w >= 0) weather = w;
            if (o >= 0) overcast = o;
            if (p >= 0) pricipitation = p;
            if (f >= 0) fog = f;
        }

        public override string ToString()
        {
            var tmp = "WEATHER " + weather + " " + overcast.ToString(CultureInfo.InvariantCulture) + " " + pricipitation.ToString(CultureInfo.InvariantCulture) + " " + fog.ToString(CultureInfo.InvariantCulture);
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            if (MPManager.IsServer()) return;
            if (weather >= 0)
            {
                MPManager.Instance().weather = weather;
            }
            if (overcast >= 0)
            {
                MPManager.Instance().overcastFactor = overcast;
            }
            if (pricipitation >= 0)
            {
                MPManager.Instance().pricipitationIntensity = pricipitation;
            }
            if (fog >= 0)
            {
                MPManager.Instance().fogDistance = fog;
            }
            MPManager.Instance().weatherChanged = true;
        }
    }

#endregion MSGWeather

#region MSGAider
    public class MSGAider : Message
    {
        public string user;
        public bool add;
        public MSGAider(string m)
        {
            string[] tmp = m.Split('\t');
            user = tmp[0].Trim();
            if (tmp[1].Trim() == "T") add = true; else add = false;
        }

        public MSGAider(string m, bool add1)
        {
            user = m.Trim();
            add = add1;
        }

        public override string ToString()
        {

            string tmp = "AIDER " + user + "\t" + (add == true ? "T" : "F");
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            if (MPManager.IsServer()) return;
            if (MPManager.GetUserName() == this.user && add == true)
            {
                MPManager.Instance().AmAider = true;
                if (MPManager.Simulator.Confirmer != null)
                    MPManager.Simulator.Confirmer.Information(MPManager.Catalog.GetString("You are an assistant now, will be able to handle switches and signals."));
            }
            if (MPManager.GetUserName() == this.user && add == false)
            {
                MPManager.Instance().AmAider = false;
                if (MPManager.Simulator.Confirmer != null)
                    MPManager.Simulator.Confirmer.Information(MPManager.Catalog.GetString("You are no longer an assistant."));
            }
        }

    }

#endregion MSGAider

#region MSGSignalChange
    public class MSGSignalChange : Message
    {
        int index;
        int pick;
        string sender;
        //constructor to create a message from signal data
        public MSGSignalChange(Signal signal, int p)
        {
            index = signal.Index;
            pick = p;
            sender = MPManager.GetUserName();
        }

        //constructor to decode the message "m"
        public MSGSignalChange(string m)
        {
            string[] tmp = m.Split(' ');
            sender = tmp[0].Trim();
            index = int.Parse(tmp[1]);
            pick = int.Parse(tmp[2]);
        }

        //how to handle the message?
        public override void HandleMsg() //only client will get message, thus will set states
        {
            if (MPManager.Server != null && !MPManager.Instance().aiderList.Contains(sender)) return; //client will ignore it, also if not an aider, will ignore it

            var signal = MPManager.Simulator.SignalEnvironment.Signals[index];
            switch (pick)
            {
                case 0:
                    signal.HoldState = SignalHoldState.None;
                    break;
                case 1:
                    signal.HoldState = SignalHoldState.ManualLock;
                    break;
                case 2:
                    signal.HoldState = SignalHoldState.ManualApproach;
                    foreach (var sigHead in signal.SignalHeads)
                    {
                        var drawstate1 = sigHead.DefaultDrawState(SignalAspectState.Approach_1);
                        var drawstate2 = sigHead.DefaultDrawState(SignalAspectState.Approach_2);
                        var drawstate3 = sigHead.DefaultDrawState(SignalAspectState.Approach_3);
                        if (drawstate1 > 0) { sigHead.SignalIndicationState = SignalAspectState.Approach_1; }
                        else if (drawstate2 > 0) { sigHead.SignalIndicationState = SignalAspectState.Approach_2; }
                        else { sigHead.SignalIndicationState = SignalAspectState.Approach_3; }
                        sigHead.DrawState = sigHead.DefaultDrawState(sigHead.SignalIndicationState);
                    }
                    break;
                case 3:
                    signal.HoldState = SignalHoldState.ManualPass;
                    foreach (var sigHead in signal.SignalHeads)
                    {
                        sigHead.SetLeastRestrictiveAspect();
                        sigHead.DrawState = sigHead.DefaultDrawState(sigHead.SignalIndicationState);
                    }
                    break;
                case 4:
                    signal.SetManualCallOn(true);
                    break;
            }
        }

        public override string ToString()
        {

            string tmp = "SIGNALCHANGE " + sender + " " + index + " " + pick; // fill in the message body here
            return " " + tmp.Length + ": " + tmp;
        }
    }
#endregion MSGSignalChange

#region MSGExhaust
    public class MSGExhaust : Message
    {
        class MSGExhaustItem
        {
            public string user;
            public int num;
            public int iCar;
            public float exhPart;
            public float exhMag;
            public float exhColorR, exhColorG, exhColorB;

            public MSGExhaustItem(string u, int n, int i, float eP, float eM, float eCR, float eCG, float eCB)
            {
                user = u; num = n; iCar = i; exhPart = eP; exhMag = eM; exhColorR = eCR; exhColorG = eCG; exhColorB = eCB;
            }

            public override string ToString()
            {
                return user + " " + num + " " + iCar + " " + exhPart.ToString(CultureInfo.InvariantCulture) + " " + exhMag.ToString(CultureInfo.InvariantCulture) +
                    " " + exhColorR.ToString(CultureInfo.InvariantCulture) + " " + exhColorG.ToString(CultureInfo.InvariantCulture) + " " + exhColorB.ToString(CultureInfo.InvariantCulture);
            }
        }
        List<MSGExhaustItem> items;

        public MSGExhaust(string m)
        {
            m = m.Trim();
            string[] areas = m.Split(' ');
            if (areas.Length % 8 != 0)
            {
                throw new Exception("Parsing error " + m);
            }
            try
            {
                int i = 0;
                items = new List<MSGExhaustItem>();
                for (i = 0; i < areas.Length / 8; i++)
                    items.Add(new MSGExhaustItem(areas[8 * i], int.Parse(areas[8 * i + 1]), int.Parse(areas[8 * i + 2]),
                        float.Parse(areas[8 * i + 3], CultureInfo.InvariantCulture), float.Parse(areas[8 * i + 4], CultureInfo.InvariantCulture),
                        float.Parse(areas[8 * i + 5], CultureInfo.InvariantCulture), float.Parse(areas[8 * i + 6], CultureInfo.InvariantCulture), float.Parse(areas[8 * i + 7], CultureInfo.InvariantCulture)));
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public MSGExhaust()
        {
        }

        public void AddNewItem(string u, Train t, int c)
        {
            if (items == null) items = new List<MSGExhaustItem>();
            MSTSDieselLocomotive l = t.Cars[c] as MSTSDieselLocomotive;
            items.Add(new MSGExhaustItem(u, t.Number, c, (float)l.ExhaustParticles.SmoothedValue, (float)l.ExhaustMagnitude.SmoothedValue,
                (float)l.ExhaustColorR.SmoothedValue, (float)l.ExhaustColorG.SmoothedValue, (float)l.ExhaustColorB.SmoothedValue));
        }

        public bool OKtoSend()
        {
            if (items != null && items.Count > 0) return true;
            return false;
        }

        public override string ToString()
        {
            if (items == null || items.Count == 0) return "";
            string tmp = "EXHAUST ";
            for (var i = 0; i < items.Count; i++) tmp += items[i].ToString() + " ";
            return " " + tmp.Length + ": " + tmp;
        }

        public override void HandleMsg()
        {
            foreach (MSGExhaustItem m in items)
            {
                if (m.user != MPManager.GetUserName())
                {
                    Train t = MPManager.FindPlayerTrain(m.user);
                    if (t != null && t.Cars.Count > m.iCar && t.Cars[m.iCar] is MSTSDieselLocomotive)
                    {
                        MSTSDieselLocomotive remoteDiesel = t.Cars[m.iCar] as MSTSDieselLocomotive;
                        remoteDiesel.RemoteUpdate(m.exhPart, m.exhMag, m.exhColorR, m.exhColorG, m.exhColorB);
                    }
                }
            }
        }
    }
#endregion MSGExhaust

#region MSGFlip
    //message to indicate that a train has been flipped (reverse formation)
    // message contains data before flip
    public class MSGFlip : Message
    {
        string[] cars;
        string[] ids;
        int[] flipped; //if a wagon is engine
        int TrainNum;
        int direction;
        int TileX, TileZ;
        float X, Z, Travelled;
        int mDirection;
        float speed;
        int tni;
        int count;
        int tdir;
        float len;
        int reverseMU;

        public MSGFlip(string m)
        {
            //Trace.WriteLine(m);
            int index = m.IndexOf(' '); int last = 0;
            TrainNum = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            direction = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            TileX = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            TileZ = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            X = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            Z = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            Travelled = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            mDirection = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            speed = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            tni = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            count = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            tdir = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            len = float.Parse(m.Substring(0, index + 1), CultureInfo.InvariantCulture);
            m = m.Remove(0, index + 1);
            index = m.IndexOf(' ');
            reverseMU = int.Parse(m.Substring(0, index + 1));
            m = m.Remove(0, index + 1);
            string[] areas = m.Split('\t');
            cars = new string[areas.Length - 1];//with an empty "" at end
            ids = new string[areas.Length - 1];
            flipped = new int[areas.Length - 1];
            for (var i = 0; i < cars.Length; i++)
            {
                index = areas[i].IndexOf('\"');
                last = areas[i].LastIndexOf('\"');
                cars[i] = areas[i].Substring(index + 1, last - index - 1);
                string tmp = areas[i].Remove(0, last + 1);
                tmp = tmp.Trim();
                string[] carinfo = tmp.Split('\n');
                ids[i] = carinfo[0];
                flipped[i] = int.Parse(carinfo[1]);
            }

            //Trace.WriteLine(this.ToString());

        }

        public MSGFlip(Train t, bool setMUParameters, int n)
        {
            cars = new string[t.Cars.Count];
            ids = new string[t.Cars.Count];
            flipped = new int[t.Cars.Count];
            var carGroup = Math.Min(t.Cars.Count, 20); // it is not needed to check for real flip on a full, long consist
            for (var i = 0; i < carGroup; i++)
            {
                cars[i] = t.Cars[i].RealWagFilePath;
                ids[i] = t.Cars[i].CarID;
                if (t.Cars[i].Flipped == true) flipped[i] = 1;
                else flipped[i] = 0;
            }
            TrainNum = n;
            direction = t.RearTDBTraveller.Direction == Traveller.TravellerDirection.Forward ? 1 : 0;
            TileX = t.RearTDBTraveller.TileX;
            TileZ = t.RearTDBTraveller.TileZ;
            X = t.RearTDBTraveller.X;
            Z = t.RearTDBTraveller.Z;
            Travelled = t.DistanceTravelled;
            mDirection = (int)t.MUDirection;
            speed = t.SpeedMpS;
            tni = t.RearTDBTraveller.TrackNodeIndex;
            count = t.Cars.Count;
            tdir = (int)t.RearTDBTraveller.Direction;
            len = t.Length;
            reverseMU = (setMUParameters ? 1 : 0);
        }

        public override void HandleMsg() //only client will get message, thus will set states
        {
            if (MPManager.IsServer()) return; //server will ignore it
                                              //Trace.WriteLine(this.ToString());
                                              // construct train data
            foreach (Train t in MPManager.Simulator.Trains)
            {
                if (t.Number == TrainNum)
                {
                    // Check if real flip
                    var realFlip = false;
                    // if different number of cars, most likely there was a couple/uncouple, so let's assume real flip
                    if (t.Cars.Count != count) realFlip = true;
                    else
                    {
                        for (var i = 0; i < Math.Min(20, t.Cars.Count - 1); i++)
                        {
                            var c = t.Cars[i].RealWagFilePath;
                            var index = c.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
                            if (index > 0)
                            {
                                c = c.Remove(0, index + 17);
                            }//c: wagon path without folder name
                            if (t.Cars[i].Flipped != (flipped[i] == 0 ? false : true))
                            {
                                Trace.TraceWarning("Invalid data have prevented flipping: car number {0} local flip state {1} remote flip state {2}",
                                    i, t.Cars[i].Flipped, flipped[i]);
                                return;
                            }
                            if (c.ToLower() != cars[i].ToLower())
                            {
                                Trace.TraceWarning("Invalid data have prevented flipping: car number {0} local filepath {1} remote filepath {2}",
                                    i, t.Cars[i].Flipped, flipped[i]);
                                return;
                            }
                        }
                        realFlip = true;
                    }
#if DEBUG_MULTIPLAYER
                    Trace.TraceInformation("Changing Direction");
#endif
                    if (realFlip)
                        t.ToDoUpdate(tni, TileX, TileZ, X, Z, Travelled, speed, direction, tdir, len, true, reverseMU);
                    return;
                }
            }
        }

        public override string ToString()
        {
            string tmp = "FLIP " + TrainNum + " " + direction + " " + TileX + " " + TileZ + " " + X.ToString(CultureInfo.InvariantCulture) + " " + Z.ToString(CultureInfo.InvariantCulture) + " " + Travelled.ToString(CultureInfo.InvariantCulture) + " " + mDirection + " " + 
                speed.ToString(CultureInfo.InvariantCulture) + " " + tni + " " + count + " " + tdir + " " + len.ToString(CultureInfo.InvariantCulture) + " " + reverseMU + " ";
            for (var i = 0; i < cars.Length; i++)
            {
                var c = cars[i];
                var index = c.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    c = c.Remove(0, index + 17);
                }//c: wagon path without folder name

                tmp += "\"" + c + "\"" + " " + ids[i] + "\n" + flipped[i] + "\n" + "\t";
            }
            return " " + tmp.Length + ": " + tmp;
        }
    }

#endregion MSGFlip

}