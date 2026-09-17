using System.Collections.Generic;
using NSOKHODO.Client;
using NSOKHODO.Core;
using NSOKHODO.Models;

namespace NSOKHODO.Controller
{
    public class PartyHandler
    {
        public GameStateManager State { get; private set; }

        public string LastInviteFrom { get; private set; }
        public int LastInviteCharId { get; private set; }

        public PartyHandler(GameStateManager state)
        {
            State = state;
        }

        public void HandleInvite(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                LastInviteCharId = r.ReadInt();
                LastInviteFrom = r.ReadUTF();
            }
            catch { }
        }

        public void HandleUpdate(NsoMessage msg)
        {
            try
            {
                var r = msg.Reader;
                State.PartyLocked = r.ReadBoolean();
                var members = new List<PartyMember>();

                while (r.Available > 0)
                {
                    var m = new PartyMember();
                    m.CharId = r.ReadInt();
                    m.ClassId = r.ReadByte();
                    m.Name = r.ReadUTF();
                    members.Add(m);
                }

                State.PartyMembers = members;
                State.IsInParty = members.Count > 0;
            }
            catch { }
        }

        public void HandleDisband(NsoMessage msg)
        {
            State.PartyMembers.Clear();
            State.IsInParty = false;
            State.PartyLocked = false;
        }
    }
}
