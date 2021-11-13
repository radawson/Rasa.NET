﻿namespace Rasa.Packets.MapChannel.Client
{
    using Data;
    using Memory;

    public class RequestWeaponDrawPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestWeaponDraw;

        // 0 Elements
        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
        }
    }
}