using System.Collections.Generic;
using NSOKHODO.Client;

namespace NSOKHODO.Config
{
    public class AppConfig
    {
        public List<AccountConfig> Accounts { get; set; }

        public AppConfig()
        {
            Accounts = new List<AccountConfig>();
        }
    }
}
