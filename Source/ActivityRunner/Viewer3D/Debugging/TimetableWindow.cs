﻿// COPYRIGHT 2010 - 2020 by the Open Rails project.
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
using System.Drawing;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D.Debugging;
using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts.Models;
using Orts.MultiPlayer;
using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Signalling;

using Color = System.Drawing.Color;

namespace Orts.ActivityRunner.Viewer3D.Debugging
{
    public class TimetableWindow
    {
        public DispatchViewer F { get; set; } // Shortest possible abbreviation so code is easier to read.

        public TimetableWindow(DispatchViewer form)
        {
            F = form;
        }

		public void SetControls()
		{
			if (F.tWindow.SelectedIndex == 1) // 0 for Dispatch Window, 1 for Timetable Window
			{
				ShowTimetableControls(true);
				ShowDispatchControls(false);
				SetTimetableMedia();
			}
			else
			{
				ShowTimetableControls(false);
				ShowDispatchControls(true);
				SetDispatchMedia();
			}
		}

		private void ShowDispatchControls(bool dispatchView)
		{
			var multiPlayer = MPManager.IsMultiPlayer() && dispatchView;
			F.msgAll.Visible = multiPlayer;
			F.msgSelected.Visible = multiPlayer;
			F.composeMSG.Visible = multiPlayer;
			F.MSG.Visible = multiPlayer;
			F.messages.Visible = multiPlayer;
			F.AvatarView.Visible = multiPlayer;
			F.composeMSG.Visible = multiPlayer;
			F.reply2Selected.Visible = multiPlayer;
			F.chkShowAvatars.Visible = multiPlayer;
			F.chkAllowUserSwitch.Visible = multiPlayer;
			F.chkAllowNew.Visible = multiPlayer;
			F.chkBoxPenalty.Visible = multiPlayer;
			F.chkPreferGreen.Visible = multiPlayer;
			F.btnAssist.Visible = multiPlayer;
			F.btnNormal.Visible = multiPlayer;
			F.rmvButton.Visible = multiPlayer;

			if (multiPlayer)
			{
				F.chkShowAvatars.Checked = Simulator.Instance.Settings.ShowAvatar;
				F.pbCanvas.Location = new System.Drawing.Point(F.pbCanvas.Location.X, F.label1.Location.Y + 18);
				F.refreshButton.Text = "View Self";
			}

			F.chkDrawPath.Visible = dispatchView;
			F.chkPickSignals.Visible = dispatchView;
			F.chkPickSwitches.Visible = dispatchView;
			F.btnSeeInGame.Visible = dispatchView;
			F.btnFollow.Visible = dispatchView;
			F.windowSizeUpDown.Visible = dispatchView;
			F.label1.Visible = dispatchView;
			F.resLabel.Visible = dispatchView;
			F.refreshButton.Visible = dispatchView;
		}
		private void SetDispatchMedia()
		{
			F.trainFont = new Font("Arial", 14, FontStyle.Bold);
			F.sidingFont = new Font("Arial", 12, FontStyle.Bold);
			F.trainBrush = new SolidBrush(Color.Red);
			F.sidingBrush = new SolidBrush(Color.Blue);
			F.pbCanvas.BackColor = Color.White;
		}

		private void ShowTimetableControls(bool timetableView)
		{
			F.lblSimulationTimeText.Visible = timetableView;
			F.lblSimulationTime.Visible = timetableView;
			F.lblShow.Visible = timetableView;
			F.cbShowPlatforms.Visible = timetableView;
			F.cbShowPlatformLabels.Visible = timetableView;
			F.cbShowSidings.Visible = timetableView;
			F.cbShowSwitches.Visible = timetableView;
			F.cbShowSignals.Visible = timetableView;
			F.cbShowSignalState.Visible = timetableView;
			F.cbShowTrainLabels.Visible = timetableView;
			F.cbShowTrainState.Visible = timetableView;
			F.bTrainKey.Visible = timetableView;
			F.gbTrains.Visible = timetableView;
			F.rbShowActiveTrains.Visible = timetableView;
			F.rbShowAllTrains.Visible = timetableView;
			F.lblDayLightOffsetHrs.Visible = timetableView;
			F.nudDaylightOffsetHrs.Visible = timetableView;
			F.bBackgroundColor.Visible = timetableView;
		}

		private void SetTimetableMedia()
		{
			F.Name = "Timetable Window";
			F.trainFont = new Font("Segoe UI Semibold", 10, FontStyle.Regular);
			F.sidingFont = new Font("Segoe UI Semibold", 10, FontStyle.Regular);
			F.PlatformFont = new Font("Segoe UI Semibold", 10, FontStyle.Regular);
			F.SignalFont = new Font("Segoe UI Semibold", 10, FontStyle.Regular);
			F.trainBrush = new SolidBrush(Color.Red);
			F.InactiveTrainBrush = new SolidBrush(Color.DarkRed);
			F.sidingBrush = new SolidBrush(Color.Blue);
			F.PlatformBrush = new SolidBrush(Color.DarkBlue);
			F.SignalBrush = new SolidBrush(Color.DarkRed);
			F.pbCanvas.BackColor = Color.FromArgb(250, 240, 230);
		}

		private void AdjustControlLocations()
		{
			if (F.Height < 600 || F.Width < 800) return;

			if (F.oldHeight != F.Height || F.oldWidth != F.Width) //use the label "Res" as anchor point to determine the picture size
			{
				F.oldWidth = F.Width; F.oldHeight = F.Height;

				F.pbCanvas.Top = 50;
				F.pbCanvas.Width = F.label1.Left - 25;                  // 25 pixels found by trial and error
				F.pbCanvas.Height = F.Height - F.pbCanvas.Top - 45;  // 45 pixels found by trial and error

				if (F.pbCanvas.Image != null)
					F.pbCanvas.Image.Dispose();
				F.pbCanvas.Image = new Bitmap(F.pbCanvas.Width, F.pbCanvas.Height);
			}
			if (F.firstShow)
			{
				// Center the view on the player's locomotive
				var pos = Simulator.Instance.PlayerLocomotive.WorldPosition;
				var ploc = new PointF(pos.TileX * 2048 + pos.Location.X, pos.TileZ * 2048 + pos.Location.Z);
				F.ViewWindow.X = ploc.X - F.minX - F.ViewWindow.Width / 2;
				F.ViewWindow.Y = ploc.Y - F.minY - F.ViewWindow.Width / 2;
				F.firstShow = false;
			}

			// Sufficient to accommodate the whole route plus 50%
			var xRange = F.maxX - F.minX;
			var yRange = F.maxY - F.minY;
			var maxSize = (int)(((xRange > yRange) ? xRange : yRange) * 1.5);
			F.windowSizeUpDown.Maximum = (decimal)maxSize;
		}

		public void PopulateItemLists()
		{
			var previousSidingName = "";
			var previousPlatformName = "";

			foreach (var item in F.simulator.TDB.TrackDB.TrackItems)
            {
				switch (item)
				{
                case SignalItem signalItem:
                    if (signalItem.SignalObject >= 0 && signalItem.SignalObject < Simulator.Instance.SignalEnvironment.Signals.Count)
                    {
                        Signal s = Simulator.Instance.SignalEnvironment.Signals[signalItem.SignalObject];
                        if (s != null && s.IsSignal && s.SignalNormal())
                            F.signals.Add(new SignalWidget(signalItem, s));
                    }
                    break;
                case SidingItem sidingItem:
						// Sidings have 2 ends. When 2nd one is found, then average the location for a single label.
						// There is no link between these track items, so this technique attempts to pair up the names,
						// remebering also that names need not be unique (e.g. Bernina Bahn).
						if (item.ItemName == previousSidingName)
						{
							var oldLocation = F.sidings.Last().Location;
							var newLocation = new PointF(item.Location.TileX * 2048 + item.Location.Location.X, item.Location.TileZ * 2048 + item.Location.Location.Z);

							// Because these are structs, not classes, compiler won't let you overwrite them.
							// Instead create a single item which replaces the 2 platform items.
							var replacementSiding = new SidingWidget(item);

							// Give it the right-hand location
							replacementSiding.Location = GetMidPoint(oldLocation, newLocation);

							// Replace the first platform item with the replacement
							F.sidings.RemoveAt(F.sidings.Count() - 1);
							F.sidings.Add(replacementSiding);
							previousSidingName = "";
						}
						else
						{
							previousSidingName = item.ItemName;
							F.sidings.Add(new SidingWidget(item));
						}
						break;
                case PlatformItem platformItem:
						// Platforms have 2 ends. When 2nd one is found, then find the right-hand one as the location for a single label.
						// There is no link between these track items, so this technique attempts to pair up the names,
						// remebering also that names need not be unique (e.g. Bernina Bahn).
						if (item.ItemName == previousPlatformName)
						{
                            var oldLocation = F.platforms.Last().Location;
                            var newLocation = new PointF(item.Location.TileX * 2048 + item.Location.Location.X, item.Location.TileZ * 2048 + item.Location.Location.Z);

                            // Because these are structs, not classes, compiler won't let you overwrite them.
                            // Instead create a single item which replaces the 2 platform items.
                            var replacementPlatform = new PlatformWidget(item);

                            // Give it the right-hand location
                            replacementPlatform.Location = GetRightHandPoint(oldLocation, newLocation);

                            // Save the original 2 locations of the platform
                            replacementPlatform.Extent1 = oldLocation;
                            replacementPlatform.Extent2 = newLocation;

                            // Replace the first platform item with the replacement
                            F.platforms.RemoveAt(F.platforms.Count() - 1);
                            F.platforms.Add(replacementPlatform);
                            previousPlatformName = "";
						}
						else
						{
							previousPlatformName = item.ItemName;
							var newPlatform = new PlatformWidget(item);
							newPlatform.Extent1 = new PointF(item.Location.TileX * 2048 + item.Location.Location.X, item.Location.TileZ * 2048 + item.Location.Location.Z);
							F.platforms.Add(newPlatform);
						}
						break;

					default:
						break;
				}
			}
		}


		/// <summary>
		/// Returns the mid-point between two locations
		/// </summary>
		/// <param name="location"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		private PointF GetMidPoint(PointF location1, PointF location2)
		{
			return new PointF()
			{
				X = (location1.X + location2.X) / 2
				,
				Y = (location1.Y + location2.Y) / 2
			};
		}

		private PointF GetRightHandPoint(PointF location1, PointF location2)
		{
			return (location1.X > location2.X) ? location1 : location2;
		}

		public void GenerateTimetableView(bool dragging = false)
		{
			AdjustControlLocations();
			ShowSimulationTime();

			if (F.pbCanvas.Image == null)
				F.InitImage();

			using (Graphics g = Graphics.FromImage(F.pbCanvas.Image))
			{
				g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				g.Clear(F.pbCanvas.BackColor);

				// Set scales. subX & subY give top-left location in meters from world origin.
				F.subX = F.minX + F.ViewWindow.X;
				F.subY = F.minY + F.ViewWindow.Y;

				// Get scale in pixels/meter
				F.xScale = F.pbCanvas.Width / F.ViewWindow.Width;
				F.yScale = F.pbCanvas.Height / F.ViewWindow.Height;
				F.xScale = F.yScale = Math.Max(F.pbCanvas.Width / F.ViewWindow.Width, F.pbCanvas.Height / F.ViewWindow.Height);

				// Set the default pen to represent 1 meter.
				var scale = (float)Math.Round((double)F.xScale);  // Round to nearest pixels/meter
				var penWidth = (int)MathHelper.Clamp(scale, 1, 3);  // Keep 1 <= width <= 3 pixels

				// Choose pens
				Pen p = F.grayPen;
				F.grayPen.Width = F.greenPen.Width = F.orangePen.Width = F.redPen.Width = penWidth;
				F.pathPen.Width = penWidth * 2;
				F.trainPen.Width = penWidth * 6;

				// First so track is drawn over the thicker platform line
				DrawPlatforms(g, penWidth);

				// Draw track
				PointF scaledA, scaledB;
				DrawTrack(g, p, out scaledA, out scaledB);

				if (dragging == false)
				{
					// Draw trains and path
					DrawTrains(g, scaledA, scaledB);

					// Keep widgetWidth <= 15 pixels
					var widgetWidth = Math.Min(penWidth * 6, 15);

					// Draw signals on top of path so they are easier to see.
					F.signalItemsDrawn.Clear();
					ShowSignals(g, scaledB, widgetWidth);

					// Draw switches
					F.switchItemsDrawn.Clear();
					ShowSwitches(g, widgetWidth);
					
					// Draw labels for sidings and platforms last so they go on top for readability
					CleanTextCells();  // Empty the listing of labels ready for adding labels again
					ShowPlatformLabels(g); // Platforms take priority over sidings and signal states
					ShowSidingLabels(g);
				}
				DrawZoomTarget(g);
			}
			F.pbCanvas.Invalidate(); // Triggers a re-paint
		}

		/// <summary>
		/// Indicates the location around which the image is zoomed.
		/// If user drags an item of interest into this target box and zooms in, the item will remain in view.
		/// </summary>
		/// <param name="g"></param>
		private void DrawZoomTarget(Graphics g)
		{
			if (F.Dragging)
			{
				const int size = 24;
				var top = F.pbCanvas.Height / 2 - size / 2;
				var left = (int)(F.pbCanvas.Width / 2 - size / 2);
				g.DrawRectangle(F.grayPen, left, top, size, size);
			}
		}

		private void ShowSimulationTime()
		{
			var ct = TimeSpan.FromSeconds(Simulator.Instance.ClockTime);
			F.lblSimulationTime.Text = $"{ct:hh}:{ct:mm}:{ct:ss}";
		}

		private void DrawPlatforms(Graphics g, int penWidth)
		{
			if (F.cbShowPlatforms.Checked)
			{
				// Platforms can be obtrusive, so draw in solid blue only when zoomed in and fade them as we zoom out
				switch (penWidth)
				{
					case 1:
						F.PlatformPen.Color = Color.FromArgb(0, 0, 255); break;
					case 2:
						F.PlatformPen.Color = Color.FromArgb(150, 150, 255); break;
					default:
						F.PlatformPen.Color = Color.FromArgb(200, 200, 255); break;
				}

				F.PlatformPen.Width = F.grayPen.Width * 3;
				foreach (var p in F.platforms)
				{
					var scaledA = new PointF((p.Extent1.X - F.subX) * F.xScale, F.pbCanvas.Height - (p.Extent1.Y - F.subY) * F.yScale);
					var scaledB = new PointF((p.Extent2.X - F.subX) * F.xScale, F.pbCanvas.Height - (p.Extent2.Y - F.subY) * F.yScale);
					g.DrawLine(F.PlatformPen, scaledA, scaledB);
				}
			}
		}

		private void DrawTrack(Graphics g, Pen p, out PointF scaledA, out PointF scaledB)
		{
            PointF[] points = new PointF[3];
            scaledA = new PointF(0, 0);
            scaledB = new PointF(0, 0);
            PointF scaledC = new PointF(0, 0);
            foreach (var line in F.segments)
            {
                scaledA = line.A.Scale(F.xScale, F.yScale, F.subX, F.subY);
                scaledA.Y = F.pbCanvas.Height - scaledA.Y;
                scaledB = line.B.Scale(F.xScale, F.yScale, F.subX, F.subY);
                scaledB.Y = F.pbCanvas.Height - scaledB.Y;

                if ((scaledA.X < 0 && scaledB.X < 0) || (scaledA.Y < 0 && scaledB.Y < 0)) 
                    continue;

                if (line.isCurved == true)
                {
                    scaledC.X = (line.C.Location.Location.X - F.subX) * F.xScale;
                    scaledC.Y = F.pbCanvas.Height - (line.C.Location.Location.Z - F.subY) * F.yScale;
                    points[0] = scaledA; points[1] = scaledC; points[2] = scaledB;
                    g.DrawCurve(p, points);
                }
                else
                    g.DrawLine(p, scaledA, scaledB);
            }
		}

		private void ShowSwitches(Graphics g, float width)
		{
			if (F.cbShowSwitches.Checked)
				for (var i = 0; i < F.switches.Count; i++)
				{
					SwitchWidget sw = F.switches[i];

					var x = (sw.Location.X - F.subX) * F.xScale;
					var y = F.pbCanvas.Height - (sw.Location.Y - F.subY) * F.yScale;
					if (x < 0 || y < 0)
						continue;

					var scaledItem = new PointF() { X = x, Y = y };

					if (sw.Item.SelectedRoute == sw.main)
						g.FillEllipse(Brushes.Black, DispatchViewer.GetRect(scaledItem, width));
					else
						g.FillEllipse(Brushes.Gray, DispatchViewer.GetRect(scaledItem, width));

					sw.Location2D.X = scaledItem.X; sw.Location2D.Y = scaledItem.Y;
					F.switchItemsDrawn.Add(sw);
				}
		}

		private void ShowSignals(Graphics g, PointF scaledB, float width)
		{
			if (F.cbShowSignals.Checked)
				foreach (var s in F.signals)
				{
					if (float.IsNaN(s.Location.X) || float.IsNaN(s.Location.Y))
						continue;
					var x = (s.Location.X - F.subX) * F.xScale;
					var y = F.pbCanvas.Height - (s.Location.Y - F.subY) * F.yScale;
					if (x < 0 || y < 0)
						continue;

					var scaledItem = new PointF() { X = x, Y = y };
					s.Location2D.X = scaledItem.X; s.Location2D.Y = scaledItem.Y;
					if (s.Signal.SignalNormal())
					{
						var color = Brushes.Lime; // bright colour for readability
						var pen = F.greenPen;
						if (s.IsProceed == 0)
						{
						}
						else if (s.IsProceed == 1)
						{
							color = Brushes.Yellow; // bright colour for readbility
							pen = F.orangePen;
						}
						else
						{
							color = Brushes.Red;
							pen = F.redPen;
						}
						g.FillEllipse(color, DispatchViewer.GetRect(scaledItem, width));
						F.signalItemsDrawn.Add(s);
						if (s.hasDir)
						{
							scaledB.X = (s.Dir.X - F.subX) * F.xScale; scaledB.Y = F.pbCanvas.Height - (s.Dir.Y - F.subY) * F.yScale;
							g.DrawLine(pen, scaledItem, scaledB);
						}
						ShowSignalState(g, scaledItem, s);
					}
				}
		}

		private void ShowSignalState(Graphics g, PointF scaledItem, SignalWidget sw)
		{
			if (F.cbShowSignalState.Checked)
			{
				var item = sw.Item as SignalItem;
				var trainNumber = sw.Signal?.EnabledTrain?.Train?.Number;
				var trainString = (trainNumber == null) ? "" : $" train: {trainNumber}";
				var offset = 0;
				var position = scaledItem;
				foreach (var signalHead in sw.Signal.SignalHeads)
				{
					offset++;
					position.X += offset * 10;
					position.Y += offset * 15;
					var text = $"  {item?.SignalObject} {signalHead.SignalType.Name} {signalHead.SignalIndicationState} {trainString}";
					scaledItem.Y = GetUnusedYLocation(scaledItem.X, F.pbCanvas.Height - (sw.Location.Y - F.subY) * F.yScale, text);
					if (scaledItem.Y >= 0f) // -1 indicates no free slot to draw label
						g.DrawString(text, F.SignalFont, F.SignalBrush, scaledItem);
				}
			}
		}

		private void ShowSidingLabels(Graphics g)
		{
			if (F.cbShowSidings.CheckState == System.Windows.Forms.CheckState.Checked)
				foreach (var s in F.sidings)
				{
					var scaledItem = new PointF();

					scaledItem.X = (s.Location.X - F.subX) * F.xScale;
					scaledItem.Y = GetUnusedYLocation(scaledItem.X, F.pbCanvas.Height - (s.Location.Y - F.subY) * F.yScale, s.Name);
					if (scaledItem.Y >= 0f) // -1 indicates no free slot to draw label
						g.DrawString(s.Name, F.sidingFont, F.sidingBrush, scaledItem);
				}
		}

		private void ShowPlatformLabels(Graphics g)
		{
			var platformMarginPxX = 5;

			if (F.cbShowPlatformLabels.CheckState == System.Windows.Forms.CheckState.Checked)
				foreach (var p in F.platforms)
				{
					var scaledItem = new PointF();
					scaledItem.X = (p.Location.X - F.subX) * F.xScale + platformMarginPxX;
					var yPixels = F.pbCanvas.Height - (p.Location.Y - F.subY) * F.yScale;

					// If track is close to horizontal, then start label search 1 row down to minimise overwriting platform line.
					if (p.Extent1.X != p.Extent2.X
						&& Math.Abs((p.Extent1.Y - p.Extent2.Y) / (p.Extent1.X - p.Extent2.X)) < 0.1)
						yPixels += DispatchViewer.spacing;

					scaledItem.Y = GetUnusedYLocation(scaledItem.X, F.pbCanvas.Height - (p.Location.Y - F.subY) * F.yScale, p.Name);
					if (scaledItem.Y >= 0f) // -1 indicates no free slot to draw label
						g.DrawString(p.Name, F.PlatformFont, F.PlatformBrush, scaledItem);
				}
		}

		private void DrawTrains(Graphics g, PointF scaledA, PointF scaledB)
		{
			var margin = 30 * F.xScale;   //margins to determine if we want to draw a train
			var margin2 = 5000 * F.xScale;

			//variable for drawing train path
			var mDist = 5000f; var pDist = 50; //segment length when draw path

			F.selectedTrainList.Clear();

			if (F.simulator.TimetableMode)
            {
				// Add the player's train
				if (F.simulator.PlayerLocomotive.Train is Orts.Simulation.AIs.AITrain)
					BuildSelectedTrainList(F.simulator.PlayerLocomotive.Train as Orts.Simulation.AIs.AITrain);

				// and all the other trains
				foreach (var t in F.simulator.AI.AITrains)
					BuildSelectedTrainList(t);
			}
			else
            {
				foreach (var t in F.simulator.Trains)
					BuildSelectedTrainList(t);
			}

			foreach (var t in F.selectedTrainList)
			{
				string trainName;
				WorldPosition worldPos;
				TrainCar firstCar = null;
				if (t.LeadLocomotive != null)
				{
					trainName = Train.GetTrainName(t.LeadLocomotive.CarID);
					firstCar = t.LeadLocomotive;
				}
				else if (t.Cars != null && t.Cars.Count > 0)
				{
					trainName = Train.GetTrainName(t.Cars[0].CarID);
					if (t.TrainType == TrainType.Ai)
						trainName = t.Number.ToString() + ":" + t.Name;
					firstCar = t.Cars[0];
				}
				else
					continue;

				// Draw the path, then each car of the train, then maybe the name
				var loc = t.FrontTDBTraveller.WorldLocation;
				float x, y;
				x = (loc.TileX * 2048 + loc.Location.X - F.subX) * F.xScale;
				y = F.pbCanvas.Height - (loc.TileZ * 2048 + loc.Location.Z - F.subY) * F.yScale;
				if (x < -margin2
					|| y < -margin2)
					continue;

				F.DrawTrainPath(t, F.subX, F.subY, F.pathPen, g, scaledA, scaledB, pDist, mDist);

				// If zoomed out, so train occupies less than minTrainPx pixels, then draw the train as 2 boxes.
				const int minTrainPx = 24;
				F.trainPen.Width = F.grayPen.Width * 6;
				var trainLengthM = 0f;
				var minTrainLengthM = minTrainPx / F.xScale; // If train is shorter than 24 pixels, draw the ends only and at a fixed length
				var drawWholeTrain = false;
				foreach (var car in t.Cars)
				{
					trainLengthM += car.CarLengthM;
					if (trainLengthM > minTrainLengthM)
					{
						drawWholeTrain = true;
						break;
					}
				}

				var scaledTrain = new PointF();
				foreach (var car in t.Cars)
				{
					if (drawWholeTrain == false)
						// Skip the intermediate cars
						if (car != t.Cars.First() && car != t.Cars.Last())
							continue;

					var t1 = new Traveller(t.RearTDBTraveller);
					worldPos = car.WorldPosition;
					var dist = t1.DistanceTo(worldPos.WorldLocation);
					if (dist > -1)
					{
						if (drawWholeTrain)
						{
							t1.Move(dist - 1 + car.CarLengthM / 2); // Not sure purpose of "- 1"
							x = (t1.TileX * 2048 + t1.Location.X - F.subX) * F.xScale;
							y = F.pbCanvas.Height - (t1.TileZ * 2048 + t1.Location.Z - F.subY) * F.yScale;
							if (x < -margin || y < -margin)
								continue;
							scaledTrain.X = x; scaledTrain.Y = y;
							t1.Move(-car.CarLengthM);
						}
						else    // Draw the train as 2 boxes of fixed size
						{
							F.trainPen.Width = minTrainPx / 2;
							if (car == t.Cars.First())
							{
								// Draw first half a train back from the front of the first car as abox
								t1.Move(dist - 1 + car.CarLengthM / 2); // Not sure purpose of "- 1"
								x = (t1.TileX * 2048 + t1.Location.X - F.subX) * F.xScale;
								y = F.pbCanvas.Height - (t1.TileZ * 2048 + t1.Location.Z - F.subY) * F.yScale;
								if (x < -margin || y < -margin)
									continue;
								t1.Move(-minTrainPx / F.xScale / 2);
							}
							else // car == t.Cars.Last()
							{
								// Draw half a train back from the rear of the first box
								worldPos = t.Cars.First().WorldPosition;
								dist = t1.DistanceTo(worldPos.WorldLocation);
								t1.Move(dist - 1 + t.Cars.First().CarLengthM / 2 - minTrainPx / F.xScale / 2); // Not sure purpose of "- 1"
								x = (t1.TileX * 2048 + t1.Location.X - F.subX) * F.xScale;
								y = F.pbCanvas.Height - (t1.TileZ * 2048 + t1.Location.Z - F.subY) * F.yScale;
								if (x < -margin || y < -margin)
									continue;
								t1.Move(-minTrainPx / F.xScale / 2);
							}
							scaledTrain.X = x; scaledTrain.Y = y;
						}
						x = (t1.TileX * 2048 + t1.Location.X - F.subX) * F.xScale;
						y = F.pbCanvas.Height - (t1.TileZ * 2048 + t1.Location.Z - F.subY) * F.yScale;
						if (x < -margin || y < -margin)
							continue;
						scaledA.X = x; scaledA.Y = y;

						F.trainPen.Color = (car == firstCar) ? Color.FromArgb(0, 180, 0) : Color.DarkGreen;
						g.DrawLine(F.trainPen, scaledA, scaledTrain);
					}
				}
				worldPos = firstCar.WorldPosition;
				scaledTrain.X = (worldPos.TileX * 2048 - F.subX + worldPos.Location.X) * F.xScale;
				scaledTrain.Y = -25 + F.pbCanvas.Height - (worldPos.TileZ * 2048 - F.subY + worldPos.Location.Z) * F.yScale;
				if (F.cbShowTrainLabels.Checked)
					ShowTrainNameAndState(g, scaledTrain, t, trainName);
			}
		}

        private void BuildSelectedTrainList(Train t)
        {
            if (F.rbShowAllTrains.Checked)
                F.selectedTrainList.Add(t);

            if (F.rbShowActiveTrains.Checked)
                if (t is Simulation.AIs.AITrain && IsActiveTrain(t as Simulation.AIs.AITrain))
                    F.selectedTrainList.Add(t);
        }

        private bool IsActiveTrain(Simulation.AIs.AITrain t)
		{
			if (t == null)
				return false;
			return (t.MovementState != AiMovementState.Static && !(t.TrainType == TrainType.AiIncorporated && !t.IncorporatingTrain.IsPathless)) || t.TrainType == TrainType.Player;
		}

		private void ShowTrainNameAndState(Graphics g, PointF scaledItem, Train t, string trainName)
		{
			if (F.simulator.TimetableMode)
			{
				var tTTrain = t as Orts.Simulation.Timetables.TTTrain;
				if (tTTrain != null)
				{
					// Remove name of timetable, e.g.: ":SCE"
					var lastPos = trainName.LastIndexOf(":");
					var shortName = (lastPos > 0) ? trainName.Substring(0, lastPos) : trainName;

					if (IsActiveTrain(tTTrain))
					{
						if (F.cbShowTrainState.Checked)
						{
							// 4:AI mode, 6:Mode, 7:Auth, 9:Signal, 12:Path
							var status = tTTrain.GetStatus(F.Viewer.MilepostUnitsMetric);

							// Add in fields 4 and 7
							status = tTTrain.AddMovementState(status, F.Viewer.MilepostUnitsMetric);

							var statuses = $"{status[4]} {status[6]} {status[7]} {status[9]}";

							// Add path if it contains any deadlock information
							if (ContainsDeadlockIndicators(status[12]))
								statuses += status[12];

							g.DrawString($"{shortName} {statuses}", F.trainFont, F.trainBrush, scaledItem);
						}
						else
							g.DrawString(shortName, F.trainFont, F.trainBrush, scaledItem);
					}
					else
						g.DrawString(shortName, F.trainFont, F.InactiveTrainBrush, scaledItem);
				}
			}
			else
				g.DrawString(trainName, F.trainFont, F.trainBrush, scaledItem);
		}

		/*
		 * # section is claimed by a train which is waiting for a signal.
		 * & section is occupied by more than one train.
		 * deadlock info (always linked to a switch node):
		 * · * possible deadlock location - start of a single track section shared with a train running in opposite direction.
		 * · ^ active deadlock - train from opposite direction is occupying or has reserved at least part of the common
		 *     single track section. Train will be stopped at this location – generally at the last signal ahead of this node.
		 * · ~ active deadlock at that location for other train - can be significant as this other train can block this
		 *     train’s path.
		*/
		private static readonly char[] DeadlockIndicators = "#&*^~".ToCharArray();

		public static bool ContainsDeadlockIndicators(string text)
		{
			return text.IndexOfAny(DeadlockIndicators) >= 0;
		}

		// The canvas is split into equally pitched rows. 
		// Each row has an array of 4 slots with StartX, EndX positions and a count of how many slots have been filled.
		// Arrays are used instead of lists to avoid delays for memory management.
		private void CleanTextCells()
		{
			if (F.alignedTextY == null || F.alignedTextY.Length != F.pbCanvas.Height / DispatchViewer.spacing) //first time to put text, or the text height has changed
			{
				F.alignedTextY = new Vector2[F.pbCanvas.Height / DispatchViewer.spacing][];
				F.alignedTextNum = new int[F.pbCanvas.Height / DispatchViewer.spacing];
				for (var i = 0; i < F.pbCanvas.Height / DispatchViewer.spacing; i++)
					F.alignedTextY[i] = new Vector2[5]; //each line has at most 5 slots
			}
			for (var i = 0; i < F.pbCanvas.Height / DispatchViewer.spacing; i++)
				F.alignedTextNum[i] = 0;
		}

		// Returns a vertical position for the text that doesn't clash or returns -1
		// If the preferred space for text is occupied, then the slot above (-ve Y) is tested, then 2 sltos above, then 1 below.
		private float GetUnusedYLocation(float startX, float wantY, string name)
		{
			const float noFreeSlotFound = -1f;

			var desiredPositionY = (int)(wantY / DispatchViewer.spacing);  // The positionY of the ideal row for the text.
			var endX = startX + name.Length * F.trainFont.Size;
			//out of drawing area
			if (endX < 0)
				return noFreeSlotFound;

			int positionY = desiredPositionY;
			while (positionY >= 0 && positionY < F.alignedTextY.Length)
			{
				//if the line contains no text yet, put it there
				if (F.alignedTextNum[positionY] == 0)
					return SaveLabelLocation(startX, endX, positionY);

				bool conflict = false;

				//check if it intersects with any labels already in this row
				for (var col = 0; col < F.alignedTextNum[positionY]; col++)
				{
					var v = F.alignedTextY[positionY][col];
					//check conflict with a text, v.X is the start of the text, v.Y is the end of the text
					if ((endX >= v.X && startX <= v.Y))
					{
						conflict = true;
						break;
					}
				}

				if (conflict)
				{
					positionY--; // Try a different row: -1, -2, +2, +1

					if (positionY - desiredPositionY <= -2) // Cannot move up (-ve Y), so try to move it down (+ve Y)
						positionY = desiredPositionY + 2;   // Try +2 then +1

					if (positionY == desiredPositionY) // Back to original position again
						return noFreeSlotFound;
				}
				else
				{
					// Check that row has an unused column in its fixed size array
					if (F.alignedTextNum[positionY] >= F.alignedTextY[positionY].Length)
						return noFreeSlotFound;

					return SaveLabelLocation(startX, endX, positionY);
				}
			}
			return noFreeSlotFound;
		}

		private float SaveLabelLocation(float startX, float endX, int positionY)
		{
			// add start and end location for the new label
			F.alignedTextY[positionY][F.alignedTextNum[positionY]] = new Vector2 { X = startX, Y = endX };

			F.alignedTextNum[positionY]++;

			return positionY * DispatchViewer.spacing;
		}
	}
}
