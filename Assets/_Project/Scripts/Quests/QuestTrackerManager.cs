using System;
using UnityEngine;
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
            if (worldState == null)
            {
                worldState = FindAnyObjectByType<WorldStateManager>();
            }

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
            int trust = worldState != null ? worldState.GetInt("hermit_trust", 0) : 0;
            int totalDefeated = worldState != null ? worldState.GetInt("enemies_defeated", 0) : 0;
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
    }
}
