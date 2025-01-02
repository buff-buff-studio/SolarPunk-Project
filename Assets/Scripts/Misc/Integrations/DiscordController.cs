using System;
using Discord;
using Interface;
using Solis.Data;
using UnityEngine;

namespace Solis.Misc.Integrations
{
    /// <summary>
    /// Used to handle Discord integration.
    /// </summary>
    public class DiscordController : MonoBehaviour
    {
        public SettingsData settingsData;

        private static readonly long CLIENT_ID = 1287743540322897920;

        public static DiscordController Instance;
        public static long LobbyStartTimestamp;
        public static bool IsConnected;
        public static string Username;
        public static int PlayerCount;

        public Discord.Discord Discord;
        private bool _isInitialized = false;

        private static long USER_ID;
        private Activity _activity;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }else Instance = this;

            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
#if PLATFORM_STANDALONE_WIN
            Debug.Log("Starting Discord Rich Presence");

            if(settingsData.TryGet<bool>("discord") == false)
            {
                Debug.LogWarning("Discord Rich Presence is disabled in settings");
                IsConnected = false;
                this.enabled = false;
                return;
            }
            InitializeDiscord();
#else
            Debug.LogWarning("Discord Rich Presence is not supported on this platform");
            IsConnected = false;
            this.enabled = false;
#endif
        }

        public static void EnableDiscord(bool enable)
        {
            if (enable)
            {
                if (IsConnected) return;
                Debug.Log("Discord Rich Presence is now enabled");
                Instance.enabled = true;
                Instance.InitializeDiscord();
            }
            else
            {
                if (!IsConnected) return;
                Debug.Log("Discord Rich Presence is now disabled");
                Instance.ShutdownDiscord();
            }
        }

        private void InitializeDiscord()
        {
            if(IsConnected) return;

            try
            {
                Discord = new Discord.Discord(CLIENT_ID, (UInt64)CreateFlags.NoRequireDiscord);
                if (Discord == null)
                {
                    Debug.LogError("Failed to initialize Discord Rich Presence");
                    IsConnected = false;
                    this.enabled = false;
                    return;
                }

                var activityManager = Discord.GetActivityManager();
                var activity = new Activity
                {
                    Details = "Playing Solis",
                    State = "In Menu",
                    Assets =
                    {
                        LargeImage = "solis_logo",
                        LargeText = "*uebeti*"
                    }
                };

                Discord.SetLogHook(LogLevel.Debug, LogProblemsFunction);
                Discord.SetLogHook(LogLevel.Error, LogProblemsFunction);
                Discord.SetLogHook(LogLevel.Warn, LogProblemsFunction);
                Discord.SetLogHook(LogLevel.Info, LogProblemsFunction);

                //activityManager.RegisterCommand("solis://run --lobby");
                activityManager.UpdateActivity(activity, result =>
                {
                    if (result == Result.Ok)
                    {
                        Debug.Log("Discord Rich Presence updated successfully");
                        IsConnected = true;
                        Discord.GetUserManager().OnCurrentUserUpdate += () =>
                        {
                            var user = Discord.GetUserManager().GetCurrentUser();
                            USER_ID = user.Id;
                            Debug.Log("Discord Rich Presence connected as: " + user.Username);
                            Username = user.Username;
                        };
                    }
                    else
                    {
                        Debug.LogError("Failed to update Discord Rich Presence, result: " + result);
                        IsConnected = false;
                        this.enabled = false;
                    }
                });

                activityManager.OnActivityInvite += (ActivityActionType type, ref User user, ref Activity activity) =>
                {
                    Debug.Log("Received invite from: " + user.Username);
                    activityManager.SendRequestReply(user.Id, ActivityJoinRequestReply.Yes, result =>
                    {
                        if (result == Result.Ok)
                        {
                            Debug.Log("Invite accepted");
                        }
                        else
                            Debug.LogError("Failed to accept invite");
                    });
                };

                activityManager.OnActivityJoin += (string secret) =>
                {
                    RelayScreen.Instance.Join(Username, secret);
                    Debug.Log("Joining activity: " + secret);
                };

                activityManager.OnActivityJoinRequest += (ref User user) =>
                {
                    Debug.Log("Join request from: " + user.Username);
                    activityManager.SendRequestReply(user.Id, ActivityJoinRequestReply.Yes, result =>
                    {
                        if (result == Result.Ok)
                        {
                            Debug.Log("Join request accepted");
                        }
                        else
                            Debug.LogError("Failed to accept join request");
                    });
                };
                _isInitialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to initialize Discord Rich Presence: " + e);
                IsConnected = false;
                this.enabled = false;
                throw;
            }
        }

        private void ShutdownDiscord()
        {
            if(!IsConnected) return;

            ActivityManager activityManager = Discord.GetActivityManager();
            activityManager.ClearActivity(result =>
            {
                if (result == Result.Ok)
                {
                    Debug.Log("Discord Rich Presence cleared successfully");
                    IsConnected = false;
                }
                else
                    Debug.LogError("Failed to clear Discord Rich Presence");
            });

            IsConnected = false;
        }

        private void Update()
        {
            Discord.RunCallbacks();
        }

        private void LateUpdate()
        {
            if (!IsConnected && _isInitialized)
            {
                Discord.Dispose();
                Debug.Log("Discord Rich Presence disposed");
            }
        }

        private void OnApplicationQuit()
        {
            if(!IsConnected) return;
            Discord.Dispose();
        }

        public void LogProblemsFunction(Discord.LogLevel level, string message)
        {
            Debug.Log($"Discord:{level} - {message}");
        }

        public void SetGameActivity(CharacterType characterType, bool inLobby, string relayCode)
        {
            if(!IsConnected) return;

            Debug.Log("Updating Discord Rich Presence");

            var activityManager = Discord.GetActivityManager();
            var id = CLIENT_ID;
            var state = inLobby ? "In Lobby" : "In Game";
            var smallImage = characterType == CharacterType.Human ? "nina_icon" : "ram_icon";
            var smallText = characterType == CharacterType.Human ? "Nina" : "RAM";
            var timestamp = LobbyStartTimestamp;
            var count = PlayerCount;
            _activity = new Activity
            {
                Type = ActivityType.Playing,
                ApplicationId = id,
                Name = "Solis",
                Details = "Playing Solis",
                State = state,
                Assets =
                {
                    LargeImage = "solis_logo",
                    LargeText = "*uebeti*",
                    SmallImage = smallImage,
                    SmallText = smallText
                },
                Timestamps =
                {
                    Start = timestamp
                },
                Party =
                {
                    Id = "Solis"+relayCode,
                    Size =
                    {
                        CurrentSize = count,
                        MaxSize = 2
                    },
                    Privacy = ActivityPartyPrivacy.Public
                },
                Secrets =
                {
                    Join = relayCode,
                    Match = relayCode,
                    Spectate = relayCode
                },
                Instance = true
            };

            activityManager.UpdateActivity(_activity, result =>
            {
                if (result == Result.Ok)
                {
                    Debug.Log("Discord Rich Presence updated successfully");
                    if (string.IsNullOrEmpty(relayCode))
                    {
                        Debug.LogError("Relay code is null or empty. Cannot update activity.");
                    }
                }
                else
                {
                    ErrorResult();
                }
            });
        }

        private void ErrorResult()
        {
            IsConnected = false;
            Debug.LogError("Failed to update Discord Rich Presence");
        }

        public void SetMenuActivity()
        {
            if(!IsConnected) return;

            var activityManager = Discord.GetActivityManager();
            var activity = new Activity
            {
                ApplicationId = CLIENT_ID,
                Name = "Solis",
                Details = "Playing Solis",
                State = "In Menu",
                Assets =
                {
                    LargeImage = "solis_logo",
                    LargeText = "*uebeti*"
                }
            };

            activityManager.UpdateActivity(activity, result =>
            {
                if (result == Result.Ok)
                    Debug.Log("Discord Rich Presence updated successfully");
                else
                    ErrorResult();
            });
        }

        public void SendInvite()
        {
            if(!IsConnected) return;

            var activityManager = Discord.GetActivityManager();
            activityManager.SendInvite(USER_ID,
                ActivityActionType.Join,
                _activity.ToString(), result =>
            {
                if (result == Result.Ok)
                    Debug.Log("Invite sent successfully");
                else
                    Debug.LogError("Failed to send invite:" + result);
            });
        }

    }
}