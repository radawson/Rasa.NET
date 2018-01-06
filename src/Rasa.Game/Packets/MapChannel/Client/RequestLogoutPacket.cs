﻿namespace Rasa.Packets.MapChannel.Client
{
    using Data;
    using Memory;

    public class RequestLogoutPacket : PythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestLogout;
        
        // 0 Elements
        public override void Read(PythonReader pr)
        {
        }

        public override void Write(PythonWriter pw)
        {
        }
    }
}