﻿using Orts.Common;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Entities
{
    public class PathDataPoint
    {
        private WorldLocation location;
        public ref WorldLocation Location => ref location;
        public int JunctionFlag { get; private set; }
        public int InvalidFlag { get; private set; }

        #region Properties
        //Note : these flags are not understood in all detail
        public bool IsJunction { get { return JunctionFlag == 2; } }
        public bool IsInvalid { get { return InvalidFlag == 9; } } //TODO: probably also 12 is invalid.
        #endregion

        public PathDataPoint(STFReader stf)
        {
            stf.MustMatch("(");
            location = new WorldLocation(stf.ReadInt(null), stf.ReadInt(null),
                stf.ReadFloat(STFReader.Units.None, null), stf.ReadFloat(STFReader.Units.None, null), stf.ReadFloat(STFReader.Units.None, null));
            JunctionFlag = stf.ReadInt(null);
            InvalidFlag = stf.ReadInt(null);
            stf.SkipRestOfBlock();
        }
    }

    // for an explanation, see class PATfile 
    public class PathNode
    {
        public PathFlags PathFlags { get; private set; }
        public uint NextMainNode { get; private set; }
        public uint NextSidingNode { get; private set; }
        public uint fromPDP { get; private set; }

        public bool HasNextMainNode { get { return (NextMainNode != 0xffffffff); } }
        public bool HasNextSidingNode { get { return (NextSidingNode != 0xffffffff); } }

        public int WaitTime => (int)(((uint)PathFlags >> 16) & 0xFFFF);

        public PathNode(STFReader stf)
        {
            stf.MustMatch("(");
            PathFlags = (PathFlags)stf.ReadHex(0);
            NextMainNode = stf.ReadUInt(null);
            NextSidingNode = stf.ReadUInt(null);
            fromPDP = stf.ReadUInt(null);
            stf.SkipRestOfBlock();
        }

        public PathNode(uint flags, uint nextNode, uint nextSiding, uint pdp)
        {
            PathFlags = (PathFlags)flags;
            NextMainNode = nextNode;
            NextSidingNode = nextSiding;
            fromPDP = pdp;
        }
    }
}
