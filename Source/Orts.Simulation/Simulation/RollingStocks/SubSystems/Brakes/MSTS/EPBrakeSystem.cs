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

using Orts.Common;
using Orts.Common.Calc;
using System;
using System.Collections.Generic;

namespace Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS
{

    public class EPBrakeSystem : AirTwinPipe
    {
        public EPBrakeSystem(TrainCar car)
            : base(car)
        {
            DebugType = "EP";
        }

        public override void Update(double elapsedClockSeconds)
        {
            float demandedAutoCylPressurePSI = 0;
            if (BrakeLine3PressurePSI >= 1000f || Car.Train.BrakeLine4 < 0)
            {
                HoldingValve = ValveState.Release;
            }
            else if (Car.Train.BrakeLine4 == 0)
            {
                HoldingValve = ValveState.Lap;
            }
            else
            {
                demandedAutoCylPressurePSI = Math.Min(Math.Max(Car.Train.BrakeLine4, 0), 1) * MaxCylPressurePSI;
                HoldingValve = AutoCylPressurePSI <= demandedAutoCylPressurePSI ? ValveState.Lap : ValveState.Release;
            }

            base.Update(elapsedClockSeconds);

            if (AutoCylPressurePSI < demandedAutoCylPressurePSI)
            {
                float dp = (float)elapsedClockSeconds * MaxApplicationRatePSIpS;
                if (BrakeLine2PressurePSI - dp * AuxBrakeLineVolumeRatio / AuxCylVolumeRatio < AutoCylPressurePSI + dp)
                    dp = (BrakeLine2PressurePSI - AutoCylPressurePSI) / (1 + AuxBrakeLineVolumeRatio / AuxCylVolumeRatio);
                if (dp > demandedAutoCylPressurePSI - AutoCylPressurePSI)
                    dp = demandedAutoCylPressurePSI - AutoCylPressurePSI;
                BrakeLine2PressurePSI -= dp * AuxBrakeLineVolumeRatio / AuxCylVolumeRatio;
                AutoCylPressurePSI += dp;
            }
        }

        public override string GetFullStatus(BrakeSystem lastCarBrakeSystem, Dictionary<BrakeSystemComponent, Pressure.Unit> units)
        {
            string s = string.Format(" BC {0}", FormatStrings.FormatPressure(CylPressurePSI, Pressure.Unit.PSI, units[BrakeSystemComponent.BrakeCylinder], true));
            if (HandbrakePercent > 0)
                s += string.Format(" Handbrake {0:F0}%", HandbrakePercent);
            return s;
        }
    }
}
