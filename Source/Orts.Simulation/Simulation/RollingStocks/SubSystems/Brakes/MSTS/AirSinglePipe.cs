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

// Debug for Airbrake operation - Train Pipe Leak
//#define DEBUG_TRAIN_PIPE_LEAK

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts.Parsers;

namespace Orts.Simulation.RollingStocks.SubSystems.Brakes.MSTS
{
    public class AirSinglePipe : MSTSBrakeSystem
    {
        protected TrainCar Car;
        protected float HandbrakePercent;
        protected float CylPressurePSI = 64;
        protected float AutoCylPressurePSI = 64;
        protected float AuxResPressurePSI = 64;
        protected float EmergResPressurePSI = 64;
        protected float FullServPressurePSI = 50;
        protected float MaxCylPressurePSI = 64;
        protected float AuxCylVolumeRatio = 2.5f;
        protected float AuxBrakeLineVolumeRatio;
        protected float EmergResVolumeM3 = 0.07f;
        protected float RetainerPressureThresholdPSI;
        protected float ReleaseRatePSIpS = 1.86f;
        protected float MaxReleaseRatePSIpS = 1.86f;
        protected float MaxApplicationRatePSIpS = .9f;
        protected float MaxAuxilaryChargingRatePSIpS = 1.684f;
        protected float BrakeInsensitivityPSIpS = 0;
        protected float EmergResChargingRatePSIpS = 1.684f;
        protected float EmergAuxVolumeRatio = 1.4f;
        protected string DebugType = string.Empty;
        protected string RetainerDebugState = string.Empty;
        protected bool NoMRPAuxResCharging;
        protected float CylVolumeM3;


        protected bool TrainBrakePressureChanging = false;
        protected bool BrakePipePressureChanging = false;
        protected float SoundTriggerCounter = 0;
        protected float prevCylPressurePSI = 0f;
        protected float prevBrakePipePressurePSI = 0f;
        protected bool BailOffOn;


        /// <summary>
        /// EP brake holding valve. Needs to be closed (Lap) in case of brake application or holding.
        /// For non-EP brake types must default to and remain in Release.
        /// </summary>
        protected ValveState HoldingValve = ValveState.Release;

        public enum ValveState
        {
            [Description("Lap")] Lap,
            [Description("Apply")] Apply,
            [Description("Release")] Release,
            [Description("Emergency")] Emergency
        };
        protected ValveState TripleValveState = ValveState.Lap;

        public AirSinglePipe(TrainCar car)
        {
            Car = car;
            // taking into account very short (fake) cars to prevent NaNs in brake line pressures
            BrakePipeVolumeM3 = (0.032f * 0.032f * (float)Math.PI / 4f) * Math.Max ( 5.0f, (1 + car.CarLengthM)); // Using DN32 (1-1/4") pipe
            DebugType = "1P";

            // Force graduated releasable brakes. Workaround for MSTS with bugs preventing to set eng/wag files correctly for this.
            (Car as MSTSWagon).DistributorPresent |= Car.Simulator.Settings.GraduatedRelease;

            if (Car.Simulator.Settings.RetainersOnAllCars && !(Car is MSTSLocomotive))
                (Car as MSTSWagon).RetainerPositions = 4;
        }

        public override bool GetHandbrakeStatus()
        {
            return HandbrakePercent > 0;
        }

        public override void InitializeFromCopy(BrakeSystem copy)
        {
            AirSinglePipe thiscopy = (AirSinglePipe)copy;
            MaxCylPressurePSI = thiscopy.MaxCylPressurePSI;
            AuxCylVolumeRatio = thiscopy.AuxCylVolumeRatio;
            AuxBrakeLineVolumeRatio = thiscopy.AuxBrakeLineVolumeRatio;
            EmergResVolumeM3 = thiscopy.EmergResVolumeM3;
            BrakePipeVolumeM3 = thiscopy.BrakePipeVolumeM3;
            RetainerPressureThresholdPSI = thiscopy.RetainerPressureThresholdPSI;
            ReleaseRatePSIpS = thiscopy.ReleaseRatePSIpS;
            MaxReleaseRatePSIpS = thiscopy.MaxReleaseRatePSIpS;
            MaxApplicationRatePSIpS = thiscopy.MaxApplicationRatePSIpS;
            MaxAuxilaryChargingRatePSIpS = thiscopy.MaxAuxilaryChargingRatePSIpS;
            BrakeInsensitivityPSIpS = thiscopy.BrakeInsensitivityPSIpS;
            EmergResChargingRatePSIpS = thiscopy.EmergResChargingRatePSIpS;
            EmergAuxVolumeRatio = thiscopy.EmergAuxVolumeRatio;
            TwoPipes = thiscopy.TwoPipes;
            NoMRPAuxResCharging = thiscopy.NoMRPAuxResCharging;
            HoldingValve = thiscopy.HoldingValve;
        }

        // Get the brake BC & BP for EOT conditions
        public override string GetStatus(Dictionary<BrakeSystemComponent, Pressure.Unit> units)
        {
            string s = string.Format(
                " BC {0}",
                FormatStrings.FormatPressure(CylPressurePSI, Pressure.Unit.PSI, units[BrakeSystemComponent.BrakeCylinder], true));
                s += string.Format(" BP {0}", FormatStrings.FormatPressure(BrakeLine1PressurePSI, Pressure.Unit.PSI, units[BrakeSystemComponent.BrakePipe], true));
            return s;
        }

        // Get Brake information for train
        public override string GetFullStatus(BrakeSystem lastCarBrakeSystem, Dictionary<BrakeSystemComponent, Pressure.Unit> units)
        {
            string s = string.Format(" EQ {0}", FormatStrings.FormatPressure(Car.Train.EqualReservoirPressurePSIorInHg, Pressure.Unit.PSI, units[BrakeSystemComponent.EqualizingReservoir], true));
            s += string.Format(
                " BC {0}",
                FormatStrings.FormatPressure(Car.Train.HUDWagonBrakeCylinderPSI, Pressure.Unit.PSI, units[BrakeSystemComponent.BrakeCylinder], true)
            );

            s += string.Format(
                " BP {0}",                
                FormatStrings.FormatPressure(BrakeLine1PressurePSI, Pressure.Unit.PSI, units[BrakeSystemComponent.BrakePipe], true)
            );
            if (lastCarBrakeSystem != null && lastCarBrakeSystem != this)
                s += " EOT " + lastCarBrakeSystem.GetStatus(units);
            if (HandbrakePercent > 0)
                s += string.Format(" Handbrake {0:F0}%", HandbrakePercent);
            return s;
        }

        public override string[] GetDebugStatus(Dictionary<BrakeSystemComponent, Pressure.Unit> units)
        {
            return new string[] {
                DebugType,
                FormatStrings.FormatPressure(CylPressurePSI, Pressure.Unit.PSI, units[BrakeSystemComponent.BrakeCylinder], true),
                FormatStrings.FormatPressure(BrakeLine1PressurePSI, Pressure.Unit.PSI, units[BrakeSystemComponent.BrakePipe], true),
                FormatStrings.FormatPressure(AuxResPressurePSI, Pressure.Unit.PSI, units[BrakeSystemComponent.AuxiliaryReservoir], true),
                (Car as MSTSWagon).EmergencyReservoirPresent ? FormatStrings.FormatPressure(EmergResPressurePSI, Pressure.Unit.PSI, units[BrakeSystemComponent.EmergencyReservoir], true) : string.Empty,
                TwoPipes ? FormatStrings.FormatPressure(BrakeLine2PressurePSI, Pressure.Unit.PSI, units[BrakeSystemComponent.MainPipe], true) : string.Empty,
                (Car as MSTSWagon).RetainerPositions == 0 ? string.Empty : RetainerDebugState,
                Simulator.Catalog.GetString(TripleValveState.GetDescription()),
                string.Empty, // Spacer because the state above needs 2 columns.
                (Car as MSTSWagon).HandBrakePresent ? string.Format("{0:F0}%", HandbrakePercent) : string.Empty,
                FrontBrakeHoseConnected ? "I" : "T",
                string.Format("A{0} B{1}", AngleCockAOpen ? "+" : "-", AngleCockBOpen ? "+" : "-"),
                BleedOffValveOpen ? Simulator.Catalog.GetString("Open") : " ",//HudScroll feature requires for the last value, at least one space instead of string.Empty,
            };
        }

        public override float GetCylPressurePSI()
        {
            return CylPressurePSI;
        }

        public override float GetCylVolumeM3()
        {
            return CylVolumeM3;
        }

        public float GetFullServPressurePSI()
        {
            return FullServPressurePSI;
        }

        public float GetMaxCylPressurePSI()
        {
            return MaxCylPressurePSI;
        }

        public float GetAuxCylVolumeRatio()
        {
            return AuxCylVolumeRatio;
        }

        public float GetMaxReleaseRatePSIpS()
        {
            return MaxReleaseRatePSIpS;
        }

        public float GetMaxApplicationRatePSIpS()
        {
            return MaxApplicationRatePSIpS;
        }

        public override float GetVacResPressurePSI()
        {
            return 0;
        }

        public override float GetVacResVolume()
        {
            return 0;
        }
        public override float GetVacBrakeCylNumber()
        {
            return 0;
        }

        public override void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "wagon(brakecylinderpressureformaxbrakebrakeforce": MaxCylPressurePSI = AutoCylPressurePSI = stf.ReadFloatBlock(STFReader.Units.PressureDefaultPSI, null); break;
                case "wagon(triplevalveratio": AuxCylVolumeRatio = stf.ReadFloatBlock(STFReader.Units.None, null); break;
                case "wagon(brakedistributorreleaserate":
                case "wagon(maxreleaserate": MaxReleaseRatePSIpS = ReleaseRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null); break;
                case "wagon(brakedistributorapplicationrate":
                case "wagon(maxapplicationrate": MaxApplicationRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null); break;
                case "wagon(maxauxilarychargingrate": MaxAuxilaryChargingRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null); break;
                case "wagon(emergencyreschargingrate": EmergResChargingRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null); break;
                case "wagon(emergencyresvolumemultiplier": EmergAuxVolumeRatio = stf.ReadFloatBlock(STFReader.Units.None, null); break;
                case "wagon(emergencyrescapacity": EmergResVolumeM3 = (float)Size.Volume.FromFt3(stf.ReadFloatBlock(STFReader.Units.VolumeDefaultFT3, null)); break;
                
                // OpenRails specific parameters
                case "wagon(brakepipevolume": BrakePipeVolumeM3 = (float)Size.Volume.FromFt3(stf.ReadFloatBlock(STFReader.Units.VolumeDefaultFT3, null)); break;
                case "wagon(ortsbrakeinsensitivity": BrakeInsensitivityPSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null); break;
            }
        }

        public override void Save(BinaryWriter outf)
        {
            outf.Write(BrakeLine1PressurePSI);
            outf.Write(BrakeLine2PressurePSI);
            outf.Write(BrakeLine3PressurePSI);
            outf.Write(HandbrakePercent);
            outf.Write(ReleaseRatePSIpS);
            outf.Write(RetainerPressureThresholdPSI);
            outf.Write(AutoCylPressurePSI);
            outf.Write(AuxResPressurePSI);
            outf.Write(EmergResPressurePSI);
            outf.Write(FullServPressurePSI);
            outf.Write((int)TripleValveState);
            outf.Write(FrontBrakeHoseConnected);
            outf.Write(AngleCockAOpen);
            outf.Write(AngleCockBOpen);
            outf.Write(BleedOffValveOpen);
            outf.Write((int)HoldingValve);
            outf.Write(CylVolumeM3);
            outf.Write(BailOffOn);
        }

        public override void Restore(BinaryReader inf)
        {
            BrakeLine1PressurePSI = inf.ReadSingle();
            BrakeLine2PressurePSI = inf.ReadSingle();
            BrakeLine3PressurePSI = inf.ReadSingle();
            HandbrakePercent = inf.ReadSingle();
            ReleaseRatePSIpS = inf.ReadSingle();
            RetainerPressureThresholdPSI = inf.ReadSingle();
            AutoCylPressurePSI = inf.ReadSingle();
            AuxResPressurePSI = inf.ReadSingle();
            EmergResPressurePSI = inf.ReadSingle();
            FullServPressurePSI = inf.ReadSingle();
            TripleValveState = (ValveState)inf.ReadInt32();
            FrontBrakeHoseConnected = inf.ReadBoolean();
            AngleCockAOpen = inf.ReadBoolean();
            AngleCockBOpen = inf.ReadBoolean();
            BleedOffValveOpen = inf.ReadBoolean();
            HoldingValve = (ValveState)inf.ReadInt32();
            CylVolumeM3 = inf.ReadSingle();
            BailOffOn = inf.ReadBoolean();
        }

        public override void Initialize(bool handbrakeOn, float maxPressurePSI, float fullServPressurePSI, bool immediateRelease)
        {
            // reducing size of Emergency Reservoir for short (fake) cars
            if (Car.Simulator.Settings.CorrectQuestionableBrakingParams && Car.CarLengthM <= 1)
            EmergResVolumeM3 = Math.Min (0.02f, EmergResVolumeM3);

            // In simple brake mode set emergency reservoir volume, override high volume values to allow faster brake release.
            if (Car.Simulator.Settings.SimpleControlPhysics && EmergResVolumeM3 > 2.0)
                EmergResVolumeM3 = 0.7f;

            BrakeLine1PressurePSI = Car.Train.EqualReservoirPressurePSIorInHg;
            BrakeLine2PressurePSI = Car.Train.BrakeLine2PressurePSI;
            BrakeLine3PressurePSI = 0;
            if ((Car as MSTSWagon).EmergencyReservoirPresent || maxPressurePSI > 0)
                EmergResPressurePSI = maxPressurePSI;
            FullServPressurePSI = fullServPressurePSI;
            AutoCylPressurePSI = immediateRelease ? 0 : Math.Min((maxPressurePSI - BrakeLine1PressurePSI) * AuxCylVolumeRatio, MaxCylPressurePSI);
            AuxResPressurePSI = AutoCylPressurePSI == 0 ? (maxPressurePSI > BrakeLine1PressurePSI ? maxPressurePSI : BrakeLine1PressurePSI)
                : Math.Max(maxPressurePSI - AutoCylPressurePSI / AuxCylVolumeRatio, BrakeLine1PressurePSI);
            TripleValveState = ValveState.Lap;
            HoldingValve = ValveState.Release;
            HandbrakePercent = handbrakeOn & (Car as MSTSWagon).HandBrakePresent ? 100 : 0;
            SetRetainer(RetainerSetting.Exhaust);
            MSTSLocomotive loco = Car as MSTSLocomotive;
            if (loco != null) 
            {
                loco.MainResPressurePSI = loco.MaxMainResPressurePSI;
            }

            if (EmergResVolumeM3 > 0 && EmergAuxVolumeRatio > 0 && BrakePipeVolumeM3 > 0)
                AuxBrakeLineVolumeRatio = EmergResVolumeM3 / EmergAuxVolumeRatio / BrakePipeVolumeM3;
            else
                AuxBrakeLineVolumeRatio = 3.1f;
                     
            CylVolumeM3 = EmergResVolumeM3 / EmergAuxVolumeRatio / AuxCylVolumeRatio;
        }

        /// <summary>
        /// Used when initial speed > 0
        /// </summary>
        public override void InitializeMoving ()
        {
            var emergResPressurePSI = EmergResPressurePSI;
            Initialize(false, 0, FullServPressurePSI, true);
            EmergResPressurePSI = emergResPressurePSI;
        }

        public override void LocoInitializeMoving() // starting conditions when starting speed > 0
        {
        }

        public virtual void UpdateTripleValveState(float controlPressurePSI)
        {
            if (BrakeLine1PressurePSI < FullServPressurePSI - 1)
                TripleValveState = ValveState.Emergency;
            else if (BrakeLine1PressurePSI > AuxResPressurePSI + 1)
                TripleValveState = ValveState.Release;
            else if (TripleValveState == ValveState.Emergency && BrakeLine1PressurePSI > AuxResPressurePSI)
                TripleValveState = ValveState.Release;
            else if (EmergResPressurePSI > 70 && BrakeLine1PressurePSI > EmergResPressurePSI * 0.97f) // UIC regulation: for 5 bar systems, release if > 4.85 bar
                TripleValveState = ValveState.Release;
            else if (TripleValveState != ValveState.Emergency && BrakeLine1PressurePSI < AuxResPressurePSI - 1)
                TripleValveState = ValveState.Apply;
            else if (TripleValveState == ValveState.Apply && BrakeLine1PressurePSI >= AuxResPressurePSI)
                TripleValveState = ValveState.Lap;
        }

        public override void Update(double elapsedClockSeconds)
        {
            //ValveState prevTripleValueState = TripleValveState;

            // Emergency reservoir's second role (in OpenRails) is to act as a control reservoir,
            // maintaining a reference control pressure for graduated release brake actions.
            // Thus this pressure must be set even in brake systems ER not present otherwise. It just stays static in this case.
            float threshold = Math.Max(RetainerPressureThresholdPSI,
                (Car as MSTSWagon).DistributorPresent ? (EmergResPressurePSI - BrakeLine1PressurePSI) * AuxCylVolumeRatio : 0);

            if (BleedOffValveOpen)
            {
                if (AuxResPressurePSI < 0.01f && AutoCylPressurePSI < 0.01f && BrakeLine1PressurePSI < 0.01f && (EmergResPressurePSI < 0.01f || !(Car as MSTSWagon).EmergencyReservoirPresent))
                {
                    BleedOffValveOpen = false;
                }
                else
                {
                    AuxResPressurePSI -= (float)elapsedClockSeconds * MaxApplicationRatePSIpS;
                    if (AuxResPressurePSI < 0)
                        AuxResPressurePSI = 0;
                    AutoCylPressurePSI -= (float)elapsedClockSeconds * MaxReleaseRatePSIpS;
                    if (AutoCylPressurePSI < 0)
                        AutoCylPressurePSI = 0;
                    if ((Car as MSTSWagon).EmergencyReservoirPresent)
                    {
                        EmergResPressurePSI -= (float)elapsedClockSeconds * EmergResChargingRatePSIpS;
                        if (EmergResPressurePSI < 0)
                            EmergResPressurePSI = 0;
                    }
                    TripleValveState = ValveState.Release;
                }
            }
            else
                UpdateTripleValveState(threshold);

            // triple valve is set to charge the brake cylinder
            if (TripleValveState == ValveState.Apply || TripleValveState == ValveState.Emergency)
            {
                float dp = (float)elapsedClockSeconds * MaxApplicationRatePSIpS;
                if (AuxResPressurePSI - dp / AuxCylVolumeRatio < AutoCylPressurePSI + dp)
                    dp = (AuxResPressurePSI - AutoCylPressurePSI) * AuxCylVolumeRatio / (1 + AuxCylVolumeRatio);
                if (TwoPipes && dp > threshold - AutoCylPressurePSI)
                    dp = threshold - AutoCylPressurePSI;
                if (AutoCylPressurePSI + dp > MaxCylPressurePSI)
                    dp = MaxCylPressurePSI - AutoCylPressurePSI;
                if (BrakeLine1PressurePSI > AuxResPressurePSI - dp / AuxCylVolumeRatio && !BleedOffValveOpen)
                    dp = (AuxResPressurePSI - BrakeLine1PressurePSI) * AuxCylVolumeRatio;
                if (dp < 0)
                    dp = 0;

                AuxResPressurePSI -= dp / AuxCylVolumeRatio;
                AutoCylPressurePSI += dp;

                if (TripleValveState == ValveState.Emergency && (Car as MSTSWagon).EmergencyReservoirPresent)
                {
                    dp = (float)elapsedClockSeconds * MaxApplicationRatePSIpS;
                    if (EmergResPressurePSI - dp < AuxResPressurePSI + dp * EmergAuxVolumeRatio)
                        dp = (EmergResPressurePSI - AuxResPressurePSI) / (1 + EmergAuxVolumeRatio);
                    EmergResPressurePSI -= dp;
                    AuxResPressurePSI += dp * EmergAuxVolumeRatio;
                }
            }

            // triple valve set to release pressure in brake cylinder and EP valve set
            if (TripleValveState == ValveState.Release && HoldingValve == ValveState.Release)
            {
                if (AutoCylPressurePSI > threshold)
                {
                    AutoCylPressurePSI -= (float)elapsedClockSeconds * ReleaseRatePSIpS;
                    if (AutoCylPressurePSI < threshold)
                        AutoCylPressurePSI = threshold;
                }

                if ((Car as MSTSWagon).EmergencyReservoirPresent)
				{
                    if (!(Car as MSTSWagon).DistributorPresent && AuxResPressurePSI < EmergResPressurePSI && AuxResPressurePSI < BrakeLine1PressurePSI)
					{
						float dp = (float)elapsedClockSeconds * EmergResChargingRatePSIpS;
						if (EmergResPressurePSI - dp < AuxResPressurePSI + dp * EmergAuxVolumeRatio)
							dp = (EmergResPressurePSI - AuxResPressurePSI) / (1 + EmergAuxVolumeRatio);
						if (BrakeLine1PressurePSI < AuxResPressurePSI + dp * EmergAuxVolumeRatio)
							dp = (BrakeLine1PressurePSI - AuxResPressurePSI) / EmergAuxVolumeRatio;
						EmergResPressurePSI -= dp;
						AuxResPressurePSI += dp * EmergAuxVolumeRatio;
					}
					if (AuxResPressurePSI > EmergResPressurePSI)
					{
						float dp = (float)elapsedClockSeconds * EmergResChargingRatePSIpS;
						if (EmergResPressurePSI + dp > AuxResPressurePSI - dp * EmergAuxVolumeRatio)
							dp = (AuxResPressurePSI - EmergResPressurePSI) / (1 + EmergAuxVolumeRatio);
						EmergResPressurePSI += dp;
						AuxResPressurePSI -= dp * EmergAuxVolumeRatio;
					}
				}
                if (AuxResPressurePSI < BrakeLine1PressurePSI && (!TwoPipes || NoMRPAuxResCharging || BrakeLine2PressurePSI < BrakeLine1PressurePSI) && !BleedOffValveOpen)
                {
                    float dp = (float)elapsedClockSeconds * MaxAuxilaryChargingRatePSIpS; // Change in pressure for train brake pipe.
                    if (AuxResPressurePSI + dp > BrakeLine1PressurePSI - dp * AuxBrakeLineVolumeRatio)
                        dp = (BrakeLine1PressurePSI - AuxResPressurePSI) / (1 + AuxBrakeLineVolumeRatio);
                    AuxResPressurePSI += dp;
                    BrakeLine1PressurePSI -= dp * AuxBrakeLineVolumeRatio;  // Adjust the train brake pipe pressure
                }
                if (AuxResPressurePSI > BrakeLine1PressurePSI) // Allow small flow from auxiliary reservoir to brake pipe so the triple valve is not sensible to small pressure variations when in release position
                {
                    float dp = (float)elapsedClockSeconds * BrakeInsensitivityPSIpS;
                    if (AuxResPressurePSI - dp < BrakeLine1PressurePSI + dp * AuxBrakeLineVolumeRatio)
                        dp = (AuxResPressurePSI - BrakeLine1PressurePSI) / (1 + AuxBrakeLineVolumeRatio);
                    AuxResPressurePSI -= dp;
                    BrakeLine1PressurePSI += dp * AuxBrakeLineVolumeRatio;
                }
            }
            if (TwoPipes
                && !NoMRPAuxResCharging
                && AuxResPressurePSI < BrakeLine2PressurePSI
                && AuxResPressurePSI < EmergResPressurePSI
                && (BrakeLine2PressurePSI > BrakeLine1PressurePSI || TripleValveState != ValveState.Release) && !BleedOffValveOpen)
            {
                float dp = (float)elapsedClockSeconds * MaxAuxilaryChargingRatePSIpS;
                if (AuxResPressurePSI + dp > BrakeLine2PressurePSI - dp * AuxBrakeLineVolumeRatio)
                    dp = (BrakeLine2PressurePSI - AuxResPressurePSI) / (1 + AuxBrakeLineVolumeRatio);
                AuxResPressurePSI += dp;
                BrakeLine2PressurePSI -= dp * AuxBrakeLineVolumeRatio;
            }

            if (Car is MSTSLocomotive && (Car as MSTSLocomotive).PowerOn)
            {
                var loco = Car as MSTSLocomotive;
                BailOffOn = false;
                if ((loco.Train.LeadLocomotiveIndex >= 0 && ((MSTSLocomotive)loco.Train.Cars[loco.Train.LeadLocomotiveIndex]).BailOff) || loco.DynamicBrakeAutoBailOff && loco.Train.MUDynamicBrakePercent > 0 && loco.DynamicBrakeForceCurves == null)
                    BailOffOn = true;
                else if (loco.DynamicBrakeAutoBailOff && loco.Train.MUDynamicBrakePercent > 0 && loco.DynamicBrakeForceCurves != null)
                {
                    var dynforce = loco.DynamicBrakeForceCurves.Get(1.0f, loco.AbsSpeedMpS);  // max dynforce at that speed
                    if ((loco.MaxDynamicBrakeForceN == 0 && dynforce > 0) || dynforce > loco.MaxDynamicBrakeForceN * 0.6)
                        BailOffOn = true;
                }
                if (BailOffOn)
                    AutoCylPressurePSI -= MaxReleaseRatePSIpS * (float)elapsedClockSeconds;
            }

            if (AutoCylPressurePSI < 0)
                AutoCylPressurePSI = 0;
            if (AutoCylPressurePSI < BrakeLine3PressurePSI) // Brake Cylinder pressure will be the greater of engine brake pressure or train brake pressure
                CylPressurePSI = BrakeLine3PressurePSI;
            else
                CylPressurePSI = AutoCylPressurePSI;

            // Record HUD display values for brake cylinders depending upon whether they are wagons or locomotives/tenders (which are subject to their own engine brakes)   
            if (Car.WagonType == MSTSWagon.WagonTypes.Engine || Car.WagonType == MSTSWagon.WagonTypes.Tender)
            {
                Car.Train.HUDLocomotiveBrakeCylinderPSI = CylPressurePSI;
                Car.Train.HUDWagonBrakeCylinderPSI = Car.Train.HUDLocomotiveBrakeCylinderPSI;  // Initially set Wagon value same as locomotive, will be overwritten if a wagon is attached
            }
            else
            {
                // Record the Brake Cylinder pressure in first wagon, as EOT is also captured elsewhere, and this will provide the two extremeties of the train
                // Identifies the first wagon based upon the previously identified UiD 
                if (Car.UiD == Car.Train.FirstCarUiD)
                {
                    Car.Train.HUDWagonBrakeCylinderPSI = CylPressurePSI;
                }

            }

            // If wagons are not attached to the locomotive, then set wagon BC pressure to same as locomotive in the Train brake line
            if (!Car.Train.WagonsAttached &&  (Car.WagonType == MSTSWagon.WagonTypes.Engine || Car.WagonType == MSTSWagon.WagonTypes.Tender) ) 
            {
                Car.Train.HUDWagonBrakeCylinderPSI = CylPressurePSI;
            }

            float f;
            if (!Car.BrakesStuck)
            {
                f = Car.MaxBrakeForceN * Math.Min(CylPressurePSI / MaxCylPressurePSI, 1);
                if (f < Car.MaxHandbrakeForceN * HandbrakePercent / 100)
                    f = Car.MaxHandbrakeForceN * HandbrakePercent / 100;
            }
            else f = Math.Max(Car.MaxBrakeForceN, Car.MaxHandbrakeForceN / 2); 
            Car.BrakeRetardForceN = f * Car.BrakeShoeRetardCoefficientFrictionAdjFactor; // calculates value of force applied to wheel, independent of wheel skid
            if (Car.BrakeSkid) // Test to see if wheels are skiding to excessive brake force
            {
                Car.BrakeForceN = f * Car.SkidFriction;   // if excessive brakeforce, wheel skids, and loses adhesion
            }
            else
            {
                Car.BrakeForceN = f * Car.BrakeShoeCoefficientFrictionAdjFactor; // In advanced adhesion model brake shoe coefficient varies with speed, in simple model constant force applied as per value in WAG file, will vary with wheel skid.
            }

            // sound trigger checking runs every half second, to avoid the problems caused by the jumping BrakeLine1PressurePSI value, and also saves cpu time :)
            if (SoundTriggerCounter >= 0.5f)
            {
                SoundTriggerCounter = 0f;
                if ( Math.Abs(AutoCylPressurePSI - prevCylPressurePSI) > 0.1f) //(AutoCylPressurePSI != prevCylPressurePSI)
                {
                    if (!TrainBrakePressureChanging)
                    {
                        if (AutoCylPressurePSI > prevCylPressurePSI)
                            Car.SignalEvent(TrainEvent.TrainBrakePressureIncrease);
                        else
                            Car.SignalEvent(TrainEvent.TrainBrakePressureDecrease);
                        TrainBrakePressureChanging = !TrainBrakePressureChanging;
                    }

                }
                else if (TrainBrakePressureChanging)
                {
                    TrainBrakePressureChanging = !TrainBrakePressureChanging;
                    Car.SignalEvent(TrainEvent.TrainBrakePressureStoppedChanging);
                }

                if ( Math.Abs(BrakeLine1PressurePSI - prevBrakePipePressurePSI) > 0.1f /*BrakeLine1PressurePSI > prevBrakePipePressurePSI*/)
                {
                    if (!BrakePipePressureChanging)
                    {
                        if (BrakeLine1PressurePSI > prevBrakePipePressurePSI)
                            Car.SignalEvent(TrainEvent.BrakePipePressureIncrease);
                        else
                            Car.SignalEvent(TrainEvent.BrakePipePressureDecrease);
                        BrakePipePressureChanging = !BrakePipePressureChanging;
                    }

                }
                else if (BrakePipePressureChanging)
                {
                    BrakePipePressureChanging = !BrakePipePressureChanging;
                    Car.SignalEvent(TrainEvent.BrakePipePressureStoppedChanging);
                }
                prevCylPressurePSI = AutoCylPressurePSI;
                prevBrakePipePressurePSI = BrakeLine1PressurePSI;
            }
            SoundTriggerCounter = SoundTriggerCounter + (float)elapsedClockSeconds;
        }

        public override void PropagateBrakePressure(double elapsedClockSeconds)
        {
            PropagateBrakeLinePressures(elapsedClockSeconds, Car, TwoPipes);
        }

        protected static void PropagateBrakeLinePressures(double elapsedClockSeconds, TrainCar trainCar, bool twoPipes)
        {
            // Brake pressures are calculated on the lead locomotive first, and then propogated along each wagon in the consist.
            
            var train = trainCar.Train;
            var lead = trainCar as MSTSLocomotive;
            var brakePipeTimeFactorS = lead == null ? 0.0015f : lead.BrakePipeTimeFactorS;
            int nSteps = (int)(elapsedClockSeconds / brakePipeTimeFactorS + 1);
            float TrainPipeTimeVariationS = (float)elapsedClockSeconds / nSteps;
            float TrainPipeLeakLossPSI = lead == null ? 0.0f : (TrainPipeTimeVariationS * lead.TrainBrakePipeLeakPSIorInHgpS);
            // Propagate brake line (1) data if pressure gradient disabled
            if (lead != null && lead.BrakePipeChargingRatePSIorInHgpS >= 1000)
            {   // pressure gradient disabled
                if (lead.BrakeSystem.BrakeLine1PressurePSI < train.EqualReservoirPressurePSIorInHg)
                {
                    var dp1 = train.EqualReservoirPressurePSIorInHg - lead.BrakeSystem.BrakeLine1PressurePSI;
                    lead.MainResPressurePSI -= dp1 * lead.BrakeSystem.BrakePipeVolumeM3 / lead.MainResVolumeM3;
                }
                foreach (TrainCar car in train.Cars)
                {
                    if (car.BrakeSystem.BrakeLine1PressurePSI >= 0)
                        car.BrakeSystem.BrakeLine1PressurePSI = train.EqualReservoirPressurePSIorInHg;
                }
            }
            else
            {   // approximate pressure gradient in train pipe line1
                float serviceTimeFactor = lead != null ? lead.TrainBrakeController != null && lead.TrainBrakeController.EmergencyBraking ? lead.BrakeEmergencyTimeFactorS : lead.BrakeServiceTimeFactorS : 0;
                for (int i = 0; i < nSteps; i++)
                {

                    if (lead != null)
                    {
                        // Allow for leaking train air brakepipe
                        if (lead.BrakeSystem.BrakeLine1PressurePSI - TrainPipeLeakLossPSI > 0 && lead.TrainBrakePipeLeakPSIorInHgpS != 0) // if train brake pipe has pressure in it, ensure result will not be negative if loss is subtracted
                        {
                            lead.BrakeSystem.BrakeLine1PressurePSI -= TrainPipeLeakLossPSI;
                        }

                        if (lead.TrainBrakeController.TrainBrakeControllerState != ControllerState.Neutral)
                        {
                            // Charge train brake pipe - adjust main reservoir pressure, and lead brake pressure line to maintain brake pipe equal to equalising resevoir pressure - release brakes
                            if (lead.BrakeSystem.BrakeLine1PressurePSI < train.EqualReservoirPressurePSIorInHg)
                            {
                                // Calculate change in brake pipe pressure between equalising reservoir and lead brake pipe
                                float chargingRatePSIpS = lead.BrakePipeChargingRatePSIorInHgpS;
                                if (lead.TrainBrakeController.TrainBrakeControllerState == ControllerState.FullQuickRelease || lead.TrainBrakeController.TrainBrakeControllerState == ControllerState.Overcharge)
                                {
                                    chargingRatePSIpS = lead.BrakePipeQuickChargingRatePSIpS;
                                }
                                float PressureDiffEqualToPipePSI = TrainPipeTimeVariationS * chargingRatePSIpS; // default condition - if EQ Res is higher then Brake Pipe Pressure

                                if (lead.BrakeSystem.BrakeLine1PressurePSI + PressureDiffEqualToPipePSI > train.EqualReservoirPressurePSIorInHg)
                                    PressureDiffEqualToPipePSI = train.EqualReservoirPressurePSIorInHg - lead.BrakeSystem.BrakeLine1PressurePSI;

                                if (lead.BrakeSystem.BrakeLine1PressurePSI + PressureDiffEqualToPipePSI > lead.MainResPressurePSI)
                                    PressureDiffEqualToPipePSI = lead.MainResPressurePSI - lead.BrakeSystem.BrakeLine1PressurePSI;

                                if (PressureDiffEqualToPipePSI < 0)
                                    PressureDiffEqualToPipePSI = 0;

                                // Adjust brake pipe pressure based upon pressure differential
                                if (lead.TrainBrakePipeLeakPSIorInHgpS == 0)  // Train pipe leakage disabled (ie. No Train Leakage parameter present in ENG file)
                                {
                                    lead.BrakeSystem.BrakeLine1PressurePSI += PressureDiffEqualToPipePSI;
                                    lead.MainResPressurePSI -= PressureDiffEqualToPipePSI * lead.BrakeSystem.BrakePipeVolumeM3 / lead.MainResVolumeM3;
                                }
                                else
                                // Train pipe leakage is enabled (ie. ENG file parameter present)
                                {
                                    // If pipe leakage and brake control valve is in LAP position then pipe is connected to main reservoir and maintained at equalising pressure from reservoir
                                    // All other brake states will have the brake pipe connected to the main reservoir, and therefore leakage will be compenstaed by air from main reservoir
                                    // Modern self lap brakes will maintain pipe pressure using air from main reservoir

                                    if (lead.TrainBrakeController.TrainBrakeControllerState != ControllerState.Lap)
                                    {
                                        lead.BrakeSystem.BrakeLine1PressurePSI += PressureDiffEqualToPipePSI;  // Increase brake pipe pressure to cover loss
                                        lead.MainResPressurePSI = lead.MainResPressurePSI - (PressureDiffEqualToPipePSI * lead.BrakeSystem.BrakePipeVolumeM3 / lead.MainResVolumeM3);   // Decrease main reservoir pressure
                                    }
                                    // else in LAP psoition brake pipe is isolated, and thus brake pipe pressure decreases, but reservoir remains at same pressure
                                }
                            }
                            // reduce pressure in lead brake line if brake pipe pressure is above equalising pressure - apply brakes
                            else if (lead.BrakeSystem.BrakeLine1PressurePSI > train.EqualReservoirPressurePSIorInHg)
                            {
                                float ServiceVariationFactor = (1 - TrainPipeTimeVariationS / serviceTimeFactor);
                                ServiceVariationFactor = MathHelper.Clamp(ServiceVariationFactor, 0.05f, 1.0f); // Keep factor within acceptable limits - prevent value from going negative
                                lead.BrakeSystem.BrakeLine1PressurePSI *= ServiceVariationFactor;
                            }
                        }

                        train.LeadPipePressurePSI = lead.BrakeSystem.BrakeLine1PressurePSI;  // Keep a record of current train pipe pressure in lead locomotive
                    }
                    
                    // Propogate lead brake line pressure from lead locomotive along the train to each car
                    TrainCar car0 = train.Cars[0];
                    float p0 = car0.BrakeSystem.BrakeLine1PressurePSI;
                    float brakePipeVolumeM30 = car0.BrakeSystem.BrakePipeVolumeM3;
                    train.TotalTrainBrakePipeVolumeM3 = 0.0f; // initialise train brake pipe volume
#if DEBUG_TRAIN_PIPE_LEAK

                    Trace.TraceInformation("======================================= Train Pipe Leak (AirSinglePipe) ===============================================");
                    Trace.TraceInformation("Before:  CarID {0}  TrainPipeLeak {1} Lead BrakePipe Pressure {2}", trainCar.CarID, lead.TrainBrakePipeLeakPSIpS, lead.BrakeSystem.BrakeLine1PressurePSI);
                    Trace.TraceInformation("Brake State {0}", lead.TrainBrakeController.TrainBrakeControllerState);
                    Trace.TraceInformation("Main Resevoir {0} Compressor running {1}", lead.MainResPressurePSI, lead.CompressorIsOn);

#endif
                    foreach (TrainCar car in train.Cars)               
                    {
                        train.TotalTrainBrakePipeVolumeM3 += car.BrakeSystem.BrakePipeVolumeM3; // Calculate total brake pipe volume of train

                        float p1 = car.BrakeSystem.BrakeLine1PressurePSI;
                        if (car != train.Cars[0] && car.BrakeSystem.FrontBrakeHoseConnected && car.BrakeSystem.AngleCockAOpen && car0.BrakeSystem.AngleCockBOpen)
                        {
                            // Based on the principle of pressure equualization between adjacent cars
                            // First, we define a variable storing the pressure diff between cars, but limited to a maximum flow rate depending on pipe characteristics
                            // The sign in the equation determines the direction of air flow.
                            float TrainPipePressureDiffPropogationPSI = (p0>p1 ? -1 : 1) * Math.Min(TrainPipeTimeVariationS * Math.Abs(p1 - p0) / brakePipeTimeFactorS, Math.Abs(p1 - p0));

                            // Air flows from high pressure to low pressure, until pressure is equal in both cars.
                            // Brake pipe volumes of both cars are taken into account, so pressure increase/decrease is proportional to relative volumes.
                            // If TrainPipePressureDiffPropagationPSI equals to p1-p0 the equalization is achieved in one step.
                            car.BrakeSystem.BrakeLine1PressurePSI -= TrainPipePressureDiffPropogationPSI * brakePipeVolumeM30 / (brakePipeVolumeM30 + car.BrakeSystem.BrakePipeVolumeM3);
                            car0.BrakeSystem.BrakeLine1PressurePSI += TrainPipePressureDiffPropogationPSI * car.BrakeSystem.BrakePipeVolumeM3 / (brakePipeVolumeM30 + car.BrakeSystem.BrakePipeVolumeM3);
                        }
                        
                        if (!car.BrakeSystem.FrontBrakeHoseConnected)  // Car front brake hose not connected
                        {
                            if (car.BrakeSystem.AngleCockAOpen) //  AND Front brake cock opened
                            {
                                car.BrakeSystem.BrakeLine1PressurePSI -= TrainPipeTimeVariationS * p1 / brakePipeTimeFactorS;
                                if (car.BrakeSystem.BrakeLine1PressurePSI < 0)
                                    car.BrakeSystem.BrakeLine1PressurePSI = 0;
                            }

                            if (car0.BrakeSystem.AngleCockBOpen && car != car0) //  AND Rear cock of wagon opened, and car is not the first wagon
                            {
                                car0.BrakeSystem.BrakeLine1PressurePSI -= TrainPipeTimeVariationS * p0 / brakePipeTimeFactorS;
                                if (car.BrakeSystem.BrakeLine1PressurePSI < 0)
                                    car.BrakeSystem.BrakeLine1PressurePSI = 0;
                            }
                        }
                        if (car == train.Cars[train.Cars.Count - 1] && car.BrakeSystem.AngleCockBOpen) // Last car in train and rear cock of wagon open
                        {
                            car.BrakeSystem.BrakeLine1PressurePSI -= TrainPipeTimeVariationS * p1 / brakePipeTimeFactorS;
                            if (car.BrakeSystem.BrakeLine1PressurePSI < 0)
                                car.BrakeSystem.BrakeLine1PressurePSI = 0;
                        }
                        p0 = car.BrakeSystem.BrakeLine1PressurePSI;
                        car0 = car;
                        brakePipeVolumeM30 = car0.BrakeSystem.BrakePipeVolumeM3;
                    }
#if DEBUG_TRAIN_PIPE_LEAK
                    Trace.TraceInformation("After: Lead Brake Pressure {0}", lead.BrakeSystem.BrakeLine1PressurePSI);
#endif
                }
            }

            // Propagate main reservoir pipe (2) and engine brake pipe (3) data
            (int first, int last) = train.FindLeadLocomotives();
            float sumpv = 0;
            float sumv = 0;
            int continuousFromInclusive = 0;
            int continuousToExclusive = train.Cars.Count;
            for (int i = 0; i < train.Cars.Count; i++)
            {
                BrakeSystem brakeSystem = train.Cars[i].BrakeSystem;
                if (i < first && (!train.Cars[i + 1].BrakeSystem.FrontBrakeHoseConnected || !brakeSystem.AngleCockBOpen || !train.Cars[i + 1].BrakeSystem.AngleCockAOpen || !train.Cars[i].BrakeSystem.TwoPipes))
                {
                    if (continuousFromInclusive < i + 1)
                    {
                        sumv = sumpv = 0;
                        continuousFromInclusive = i + 1;
                    }
                    continue;
                }
                if (i > last && i > 0 && (!brakeSystem.FrontBrakeHoseConnected || !brakeSystem.AngleCockAOpen || !train.Cars[i - 1].BrakeSystem.AngleCockBOpen || !train.Cars[i].BrakeSystem.TwoPipes))
                {
                    if (continuousToExclusive > i)
                        continuousToExclusive = i;
                    continue;
                }

                // Collect main reservoir pipe (2) data
                if (first <= i && i <= last || twoPipes && continuousFromInclusive <= i && i < continuousToExclusive)
                {
                    sumv += brakeSystem.BrakePipeVolumeM3;
                    sumpv += brakeSystem.BrakePipeVolumeM3 * brakeSystem.BrakeLine2PressurePSI;
                    var eng = train.Cars[i] as MSTSLocomotive;
                    if (eng != null)
                    {
                        sumv += eng.MainResVolumeM3;
                        sumpv += eng.MainResVolumeM3 * eng.MainResPressurePSI;
                    }
                }

                // Collect and propagate engine brake pipe (3) data
                // This appears to be calculating the engine brake cylinder pressure???
                if (i < first || i > last)
                {
                    brakeSystem.BrakeLine3PressurePSI = 0;
                }
                else
                {
                    if (lead != null)
                    {
                        float p = brakeSystem.BrakeLine3PressurePSI;
                        if (p > 1000)
                            p -= 1000;
                        var prevState = lead.EngineBrakeState;
                        if (p < train.BrakeLine3PressurePSI && p < lead.MainResPressurePSI )  // Apply the engine brake as the pressure decreases
                        {
                            float dp = (float)elapsedClockSeconds * lead.EngineBrakeApplyRatePSIpS / (last - first + 1);
                            if (p + dp > train.BrakeLine3PressurePSI)
                                dp = train.BrakeLine3PressurePSI - p;
                            p += dp;
                            lead.EngineBrakeState = ValveState.Apply;
                            sumpv -= dp * brakeSystem.GetCylVolumeM3() / lead.MainResVolumeM3;
                        }
                        else if (p > train.BrakeLine3PressurePSI)  // Release the engine brake as the pressure increases in the brake cylinder
                        {
                            float dp = (float)elapsedClockSeconds * lead.EngineBrakeReleaseRatePSIpS / (last - first + 1);
                            if (p - dp < train.BrakeLine3PressurePSI)
                                dp = p - train.BrakeLine3PressurePSI;
                            p -= dp;
                            lead.EngineBrakeState = ValveState.Release;
                        }
                        else  // Engine brake does not change
                            lead.EngineBrakeState = ValveState.Lap;
                        if (lead.EngineBrakeState != prevState)
                            switch (lead.EngineBrakeState)
                            {
                                case ValveState.Release: lead.SignalEvent(TrainEvent.EngineBrakePressureIncrease); break;
                                case ValveState.Apply: lead.SignalEvent(TrainEvent.EngineBrakePressureDecrease); break;
                                case ValveState.Lap: lead.SignalEvent(TrainEvent.EngineBrakePressureStoppedChanging); break;
                            }
                        brakeSystem.BrakeLine3PressurePSI = p;
                    }
                }
            }
            if (sumv > 0)
                sumpv /= sumv;

            if (!train.Cars[continuousFromInclusive].BrakeSystem.FrontBrakeHoseConnected && train.Cars[continuousFromInclusive].BrakeSystem.AngleCockAOpen
                || (continuousToExclusive == train.Cars.Count || !train.Cars[continuousToExclusive].BrakeSystem.FrontBrakeHoseConnected) && train.Cars[continuousToExclusive - 1].BrakeSystem.AngleCockBOpen)
                sumpv = 0;

            // Propagate main reservoir pipe (2) data
            train.BrakeLine2PressurePSI = sumpv;
            for (int i = 0; i < train.Cars.Count; i++)
            {
                if (first <= i && i <= last || twoPipes && continuousFromInclusive <= i && i < continuousToExclusive)
                {
                    train.Cars[i].BrakeSystem.BrakeLine2PressurePSI = sumpv;
                    if (sumpv != 0 && train.Cars[i] is MSTSLocomotive)
                        (train.Cars[i] as MSTSLocomotive).MainResPressurePSI = sumpv;
                }
                else
                    train.Cars[i].BrakeSystem.BrakeLine2PressurePSI = train.Cars[i] is MSTSLocomotive ? (train.Cars[i] as MSTSLocomotive).MainResPressurePSI : 0;
            }
        }

        public override float InternalPressure(float realPressure)
        {
            return realPressure;
        }

        public override void SetRetainer(RetainerSetting setting)
        {
            switch (setting)
            {
                case RetainerSetting.Exhaust:
                    RetainerPressureThresholdPSI = 0;
                    ReleaseRatePSIpS = MaxReleaseRatePSIpS;
                    RetainerDebugState = "EX";
                    break;
                case RetainerSetting.HighPressure:
                    if ((Car as MSTSWagon).RetainerPositions > 0)
                    {
                        RetainerPressureThresholdPSI = 20;
                        ReleaseRatePSIpS = (50 - 20) / 90f;
                        RetainerDebugState = "HP";
                    }
                    break;
                case RetainerSetting.LowPressure:
                    if ((Car as MSTSWagon).RetainerPositions > 3)
                    {
                        RetainerPressureThresholdPSI = 10;
                        ReleaseRatePSIpS = (50 - 10) / 60f;
                        RetainerDebugState = "LP";
                    }
                    else if ((Car as MSTSWagon).RetainerPositions > 0)
                    {
                        RetainerPressureThresholdPSI = 20;
                        ReleaseRatePSIpS = (50 - 20) / 90f;
                        RetainerDebugState = "HP";
                    }
                    break;
                case RetainerSetting.SlowDirect:
                    RetainerPressureThresholdPSI = 0;
                    ReleaseRatePSIpS = (50 - 10) / 86f;
                    RetainerDebugState = "SD";
                    break;
            }
        }

        public override void SetHandbrakePercent(float percent)
        {
            if (!(Car as MSTSWagon).HandBrakePresent)
            {
                HandbrakePercent = 0;
                return;
            }
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            HandbrakePercent = percent;
        }

        public override void AISetPercent(float percent)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            Car.Train.EqualReservoirPressurePSIorInHg = 90 - (90 - FullServPressurePSI) * percent / 100;
        }

        // used when switching from autopilot to player driven mode, to move from default values to values specific for the trainset
        public void NormalizePressures(float maxPressurePSI)
        {
            if (AuxResPressurePSI > maxPressurePSI) AuxResPressurePSI = maxPressurePSI;
            if (BrakeLine1PressurePSI > maxPressurePSI) BrakeLine1PressurePSI = maxPressurePSI;
            if (EmergResPressurePSI > maxPressurePSI) EmergResPressurePSI = maxPressurePSI;
        }

        public override bool IsBraking()
        {
            if (AutoCylPressurePSI > MaxCylPressurePSI * 0.3)
            return true;
            return false;
        }

        //Corrects MaxCylPressure (e.g 380.eng) when too high
        public override void CorrectMaxCylPressurePSI(MSTSLocomotive loco)
        {
            if (MaxCylPressurePSI > loco.TrainBrakeController.MaxPressurePSI - MaxCylPressurePSI / AuxCylVolumeRatio)
            {
                MaxCylPressurePSI = loco.TrainBrakeController.MaxPressurePSI * AuxCylVolumeRatio / (1 + AuxCylVolumeRatio);
            }
        }
    }
}
