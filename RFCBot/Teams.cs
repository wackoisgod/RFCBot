using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using Nett;

namespace RFCBot
{
    public class RFCBehaviour
    {
        public bool close { get; set; }
        public bool postpone { get; set; }
        public bool merge { get; set; }
        public int min { get; set; } = 50;
        public bool createmerge { get; set; }
        public List<string> defaultteams { get; set; } = new List<string>();
    }

    public struct TeamConfig
    {
        public string name { get; set; }
        public string ping { get; set; }
        public string[] members { get; set; }

        public void validate()
        {
            using (var db = new RFCContext()) {
                foreach (var m in members) {
                    var result = db.Users.Where(x => x.Login.ToLower() == m).FirstOrDefault();
                    if (result == null) {
                        Github.HandleUser(GitClient.GH.Value.GetUser(m).Result);
                        Thread.Sleep(TimeSpan.FromMilliseconds(1390));
                        Console.WriteLine($"loaded into the database user {m}");
                    }
                }
            }
        }
    }

    public struct RfcbotTeams
    {
        public string url { get; set; }
        public Dictionary<string, TeamConfig> teams { get; set; }
    }

    public class RFCTeamConfig
    {
        public Dictionary<string, RFCBehaviour> behaviors { get; set; }
        public RfcbotTeams teams { get; set; }

        public Dictionary<string, TeamConfig> cachedTeams { get; set; }

        public Dictionary<string, TeamConfig> GetTeams()
        {
            if (teams.teams != null)
                return teams.teams;

            return cachedTeams;
        }

        public List<string> TeamLabels() => GetTeams().Select(c => c.Key).ToList();

        public bool ShouldAutoCloseRFC(string repo)
        {
            bool result = false;
            RFCBehaviour rFC;

            if (behaviors.TryGetValue(repo, out rFC)) {
                result = rFC.close;
            }
            return result;
        }

        public bool ShouldAutoPostponeRFC(string repo)
        {
            bool result = false;
            RFCBehaviour rFC;

            if (behaviors.TryGetValue(repo, out rFC)) {
                result = rFC.postpone;
            }
            return result;
        }

        public int GetMinPrecent(string repo)
        {
            int result = 0;
            RFCBehaviour rFC;

            if (behaviors.TryGetValue(repo, out rFC)) {
                result = rFC.min;
            }
            return result;
        }

        public bool ShouldMerge(string repo)
        {
            bool result = false;
            RFCBehaviour rFC;

            if (behaviors.TryGetValue(repo, out rFC)) {
                result = rFC.merge;
            }
            return result;
        }

        public bool ShouldAutoCreateMerge(string repo)
        {
            bool result = false;
            RFCBehaviour rFC;

            if (behaviors.TryGetValue(repo, out rFC)) {
                result = rFC.createmerge;
            }
            return result;
        }

        public List<string> DefaultIssueLabels(string repo)
        {
            List<string> result = new List<string>();
            RFCBehaviour rFC;

            if (behaviors.TryGetValue(repo, out rFC)) {
                result.AddRange(rFC.defaultteams);
            }
            return result;
        }

        public void Update()
        {
            // if we are a remote team config then we should update it
            if (!string.IsNullOrEmpty(Teams.SETUP.Value.teams.url)) {
                // we should then request this URL and then desrialize it 
            }
        }
    }

    public class Teams
    {
        public static Lazy<RFCTeamConfig> SETUP = new Lazy<RFCTeamConfig>(() => Init());

        public static void Start()
        {
            var x = SETUP.Value;
            x.Update();
            foreach (var t in x.GetTeams()) {
                t.Value.validate();
            }
        }

        public static RFCTeamConfig Init()
        {
            if (!File.Exists("rfcbot.toml")) {
                return new RFCTeamConfig();
            }

            var config = TomlSettings.Create(cfg => cfg
                .ConfigureType<RfcbotTeams>(ct => ct
                    .WithConversionFor<TomlTable>(conv => conv
                        .FromToml((m, ti) =>
                        {
                            var result = new RfcbotTeams();
                            foreach (var kvp in ti) {
                                switch (kvp.Value.TomlType) {
                                    case TomlObjectType.String:
                                    result.url = kvp.Value.Get<string>();
                                    break;
                                    case TomlObjectType.Table: {
                                        if (result.teams == null)
                                            result.teams = new Dictionary<string, TeamConfig>();

                                        result.teams.Add(kvp.Key, kvp.Value.Get<TeamConfig>());
                                    }
                                    break;
                                }
                            }
                            return result;
                        })
                    )
                )
            );
            return Toml.ReadFile<RFCTeamConfig>("rfcbot.toml", config);
        }
    }
}
