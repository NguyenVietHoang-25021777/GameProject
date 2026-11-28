using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameProject.World;

namespace GameProject.Quests
{
    public enum QuestTone
    {
        Default,
        Secret,
        Danger,
        Completed
    }

    public struct QuestDisplayState
    {
        public string CategoryLabel;
        public string Title;
        public string Description;
        public QuestTone Tone;
    }

    /// <summary>
    /// Tracks active main/hidden quests by observing WorldStateManager changes
    /// and broadcasting formatted QuestDisplayState updates to the HUD.
    /// </summary>
    public class QuestTrackerManager : MonoBehaviour
    {
        [SerializeField] private WorldStateManager worldState;

        private QuestDisplayState _currentQuest;

        public QuestDisplayState CurrentQuest => _currentQuest;

        public event Action<QuestDisplayState> OnQuestUpdated;

        private void OnEnable()
        {
            worldState = WorldStateManager.Resolve();

            if (worldState != null)
            {
                worldState.OnWorldStateChanged += HandleWorldStateChanged;
            }

            RecomputeQuestState();
        }

        private void Start()
        {
            RecomputeQuestState();
        }

        private void OnDisable()
        {
            if (worldState != null)
            {
                worldState.OnWorldStateChanged -= HandleWorldStateChanged;
            }
        }

        private void HandleWorldStateChanged(string key, int value)
        {
            RecomputeQuestState();
        }

        public void RecomputeQuestState()
        {
            if (worldState == null)
            {
                worldState = WorldStateManager.Resolve();
            }

            int totalDefeated = worldState != null ? worldState.GetInt("enemies_defeated", 0) : 0;
            string sceneName = SceneManager.GetActiveScene().name;

            if (sceneName == "Zone_Village" || sceneName == "Zone_MistyForest" || sceneName == "Zone_AncientRuins")
            {
                ComputeThreeZoneQuestState(sceneName, totalDefeated);
                OnQuestUpdated?.Invoke(_currentQuest);
                return;
            }

            int trust = worldState != null ? worldState.GetInt("hermit_trust", 0) : 0;
            int ambushDefeated = worldState != null ? worldState.GetInt("ambush_enemies_defeated", 0) : 0;
            bool skillUnlocked = worldState != null && worldState.HasFlag("secret_skill_unlocked");
            bool ambushCleared = worldState != null && worldState.HasFlag("ambush_cleared");

            if (trust >= 1)
            {
                if (skillUnlocked)
                {
                    _currentQuest = new QuestDisplayState
                    {
                        CategoryLabel = "NHIỆM VỤ ẨN  •  HOÀN THÀNH",
                        Title = "Khám phá Bệ Thưởng Bí Mật",
                        Description = $"Đã lĩnh ngộ 'Thiên Lôi Trảm' (W → A → D → Space).\nQuái vật đã hạ: {totalDefeated}",
                        Tone = QuestTone.Completed
                    };
                }
                else
                {
                    _currentQuest = new QuestDisplayState
                    {
                        CategoryLabel = "NHIỆM VỤ ẨN",
                        Title = "Khám phá Bệ Thưởng Bí Mật",
                        Description = $"Lại gần Bệ Thưởng màu vàng phát sáng cạnh Ẩn Sĩ và nhấn [E] để mở khóa chiêu thức.\nQuái vật đã hạ: {totalDefeated}",
                        Tone = QuestTone.Secret
                    };
                }
            }
            else if (trust <= -1)
            {
                if (ambushCleared || ambushDefeated >= 2)
                {
                    _currentQuest = new QuestDisplayState
                    {
                        CategoryLabel = "NHIỆM VỤ ẨN  •  HOÀN THÀNH",
                        Title = "Sống sót qua cuộc phục kích",
                        Description = $"Bạn đã đẩy lùi toàn bộ quân phục kích! (2/2)\nTổng quái đã hạ: {totalDefeated}",
                        Tone = QuestTone.Completed
                    };
                }
                else
                {
                    _currentQuest = new QuestDisplayState
                    {
                        CategoryLabel = "SỰ KIỆN PHỤC KÍCH",
                        Title = "Sống sót qua cuộc phục kích",
                        Description = $"Tiêu diệt 2 kẻ phục kích vừa xuất hiện! ({ambushDefeated}/2)\nTổng quái đã hạ: {totalDefeated}",
                        Tone = QuestTone.Danger
                    };
                }
            }
            else
            {
                _currentQuest = new QuestDisplayState
                {
                    CategoryLabel = "NHIỆM VỤ HIỆN TẠI",
                    Title = "Gặp gỡ Ẩn Sĩ",
                    Description = $"Tìm Ẩn Sĩ (áo xanh dương) bên trái sân và nhấn [E] để trò chuyện.\nQuái vật đã hạ: {totalDefeated}",
                    Tone = QuestTone.Default
                };
            }

            OnQuestUpdated?.Invoke(_currentQuest);
        }

        private void ComputeThreeZoneQuestState(string sceneName, int totalDefeated)
        {
            bool blacksmithHelped = worldState != null && (worldState.GetInt("blacksmith_helped", 0) == 1 || worldState.HasFlag("blacksmith_helped"));
            bool merchantHostile = worldState != null && (worldState.GetInt("merchant_hostile", 0) == 1 || worldState.HasFlag("merchant_hostile"));
            bool travelerSaved = worldState != null && (worldState.GetInt("traveler_saved", 0) == 1 || worldState.HasFlag("traveler_saved"));
            bool travelerRobbed = worldState != null && (worldState.GetInt("traveler_robbed", 0) == 1 || worldState.HasFlag("traveler_robbed"));
            bool ruinsManualClaimed = worldState != null && (worldState.GetInt("ruins_manual_claimed", 0) == 1 || worldState.HasFlag("ruins_manual_claimed"));

            string zoneLabel = sceneName == "Zone_Village"
                ? "VÙNG 1  •  LÀNG KHỞI ĐẦU"
                : (sceneName == "Zone_MistyForest" ? "VÙNG 2  •  RỪNG SƯƠNG MÙ" : "VÙNG 3  •  DI TÍCH CỔ ĐẠI");

            if (ruinsManualClaimed)
            {
                _currentQuest = new QuestDisplayState
                {
                    CategoryLabel = $"{zoneLabel}  •  HOÀN THÀNH",
                    Title = "Bí Kíp Cổ Đại Đã Khai Mở",
                    Description = $"Đã lĩnh ngộ bí kíp ẩn và đối đầu Thủ Vệ Di Tích.\nQuái đã hạ: {totalDefeated}",
                    Tone = QuestTone.Completed
                };
                return;
            }

            if (travelerSaved)
            {
                _currentQuest = new QuestDisplayState
                {
                    CategoryLabel = $"{zoneLabel}  •  BÍ MẬT",
                    Title = "Tìm Bệ Bí Kíp Ẩn tại Di Tích Cổ Đại",
                    Description = $"Lữ khách đã chỉ điểm Bệ Bí Kíp Ẩn trong Zone_AncientRuins!\nQuái đã hạ: {totalDefeated}",
                    Tone = QuestTone.Secret
                };
                return;
            }

            if (travelerRobbed || merchantHostile)
            {
                _currentQuest = new QuestDisplayState
                {
                    CategoryLabel = $"{zoneLabel}  •  ÁI NGẠI",
                    Title = "Tiến vào Di Tích Cổ Đại",
                    Description = $"Hành động của bạn đã ảnh hưởng thế giới ({(travelerRobbed ? "traveler_robbed" : "merchant_hostile")}).\nQuái đã hạ: {totalDefeated}",
                    Tone = QuestTone.Danger
                };
                return;
            }

            if (sceneName == "Zone_Village")
            {
                _currentQuest = new QuestDisplayState
                {
                    CategoryLabel = zoneLabel,
                    Title = blacksmithHelped ? "Tiến vào Rừng Sương Mù" : "Trò chuyện với dân làng",
                    Description = blacksmithHelped
                        ? $"Thợ Rèn đã cảm ơn bạn. Hãy đi qua Cổng phía Bắc tới Zone_MistyForest!\nQuái đã hạ: {totalDefeated}"
                        : $"Nói chuyện với Thợ Rèn (xanh) & Thương Nhân (vàng), rồi đi qua Cổng tới Rừng Sương Mù.\nQuái đã hạ: {totalDefeated}",
                    Tone = QuestTone.Default
                };
            }
            else if (sceneName == "Zone_MistyForest")
            {
                _currentQuest = new QuestDisplayState
                {
                    CategoryLabel = zoneLabel,
                    Title = "Khám phá Rừng Sương Mù",
                    Description = $"Tìm Lữ Khách Bị Thương (WoundedTraveler), hạ Tinh Anh hoặc đi tiếp tới Di Tích Cổ Đại.\nQuái đã hạ: {totalDefeated}",
                    Tone = QuestTone.Default
                };
            }
            else
            {
                _currentQuest = new QuestDisplayState
                {
                    CategoryLabel = zoneLabel,
                    Title = "Chinh phục Thủ Vệ Di Tích",
                    Description = $"Hạ Boss RuinsGuardian (400 HP). Cứu Lữ Khách ở Rừng Sương Mù để lộ Bệ Bí Kíp Ẩn!\nQuái đã hạ: {totalDefeated}",
                    Tone = QuestTone.Default
                };
            }
        }
    }
}
