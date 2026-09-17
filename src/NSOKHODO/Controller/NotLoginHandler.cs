using NSOKHODO.Client;
using NSOKHODO.Core;
using NSOKHODO.Protocol;

namespace NSOKHODO.Controller
{
    public class NotLoginHandler
    {
        public GameStateManager State { get; private set; }

        public NotLoginHandler(GameStateManager state)
        {
            State = state;
        }

        public void Handle(sbyte sub, NsoMessage msg)
        {
            switch (sub)
            {
                case SubCmd.NotLogin.LOGIN:
                    // Login result
                    break;
                default:
                    break;
            }
        }
    }
}
