// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SendScoreMessage.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
// </copyright>
// <summary>
//   Battle Station score HUD (N3 SendScore = 0x44483B3A).
//   Wire layout proven from live PF 4384 capture 2026-11-11 (Killzaw):
//   Identity(0,0) + Unknown=1, then ViewerInstance, Owner[4] (0=Red,1=Blue,2=Neutral),
//   SecondaryInstance, RedScore, BlueScore. Match ends at 500.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.SendScore)]
    public class SendScoreMessage : N3Message
    {
        public SendScoreMessage()
        {
            this.N3MessageType = N3MessageType.SendScore;
            this.Unknown = 0x01;
        }

        /// <summary>Receiving character instance (live capture used the local player id).</summary>
        [AoMember(0)]
        public int ViewerInstance { get; set; }

        /// <summary>Control-point owners: 0=Red, 1=Blue, 2=Neutral/unclaimed.</summary>
        [AoMember(1)]
        public int Owner0 { get; set; }

        [AoMember(2)]
        public int Owner1 { get; set; }

        [AoMember(3)]
        public int Owner2 { get; set; }

        [AoMember(4)]
        public int Owner3 { get; set; }

        /// <summary>Secondary instance from live packets (often another char on the field). 0 is accepted.</summary>
        [AoMember(5)]
        public int SecondaryInstance { get; set; }

        [AoMember(6)]
        public int RedScore { get; set; }

        [AoMember(7)]
        public int BlueScore { get; set; }
    }
}
