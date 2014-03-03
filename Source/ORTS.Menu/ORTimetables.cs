﻿// COPYRIGHT 2011, 2012, 2013 by the Open Rails project.
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

using System;
using System.Collections.Generic;
using System.IO;
using MSTS.Formats;
using ORTS.Formats;

namespace ORTS.Menu
{
    public class TimetableInfo
    {
        public readonly List<TTPreInfo> ORTTList = new List<TTPreInfo>();
        public readonly String Description;
        public readonly String fileName;

        // items set for use as parameters, taken from main menu
        public int AITimeHrs;
        public int AITimeMins;
        public bool AITimeRelative;  // true = time is relative, false = time is absolute
        public bool AIInPlayerDirection;
        public int Day;
        public int Season;
        public int Weather;

        // note : file is read preliminary only, extracting description and train information
        // all other information is read only when activity is started

        protected TimetableInfo(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    ORTTList.Add(new TTPreInfo(filePath));
                    Description = String.Copy(ORTTList[0].Description);
                    fileName = String.Copy(filePath);
                }
                catch
                {
                    Description = "<" + "load error:" + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
                }
            }
            else
            {
                Description = "<" + "missing:" + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
            }
        }

        protected TimetableInfo(String filePath, String directory)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    MultiTTPreInfo multiInfo = new MultiTTPreInfo(filePath, directory);
                    ORTTList = multiInfo.ORTTInfo;
                    Description = String.Copy(multiInfo.Description);
                    fileName = String.Copy(filePath);
                }
                catch
                {
                    Description = "<" + "load error:" + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
                }
            }
            else
            {
                Description = "<" + "missing:" + " " + System.IO.Path.GetFileNameWithoutExtension(filePath) + ">";
            }
        }

        public override string ToString()
        {
            return Description;
        }

        public static List<TimetableInfo> GetTimetableInfo(Folder folder, Route route)
        {
            var ORTTInfo = new List<TimetableInfo>();
            if (route != null)
            {
                var directory = System.IO.Path.Combine(route.Path, "ACTIVITIES");
                if (Directory.Exists(directory))
                {
                    foreach (var ORTimetableFile in Directory.GetFiles(directory, "*.timetable_or"))
                    {
                        try
                        {
                            ORTTInfo.Add(new TimetableInfo(ORTimetableFile));
                        }
                        catch { }
                    }

                    foreach (var ORMultitimetableFile in Directory.GetFiles(directory, "*.timetablelist_or"))
                    {
                        try
                        {
                            ORTTInfo.Add(new TimetableInfo(ORMultitimetableFile, directory));
                        }
                        catch { }
                    }
                }
            }
            return ORTTInfo;
        }
    }
}

