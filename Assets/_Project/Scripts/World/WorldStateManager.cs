using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameProject.World
{
    public enum StateComparison
    {
        Equals,
        NotEquals,
        GreaterOrEqual,
        LessOrEqual,
        HasFlag,
        NotHasFlag
    }

    public enum StateMutationType
    {
        SetInt,
        AddInt,
        SetFlag,
        ClearFlag
    }

    [Serializable]
    public struct StateCondition
    {
        public string key;
        public StateComparison comparison;
        public int value;
    }

    [Serializable]
    public struct StateMutation
    {
        public string key;
        public StateMutationType mutationType;
        public int value;
    }

    [Serializable]
    public struct IntStateEntry
    {
        public string key;
        public int value;
    }

    /// <summary>
    /// Global runtime world-state store tracking numeric counters (trust, enemies_defeated, etc.)
    /// and boolean event flags, broadcasting OnWorldStateChanged on every change.
    /// </summary>
    public class WorldStateManager : MonoBehaviour
    {
        private readonly Dictionary<string, int> _intStates = new Dictionary<string, int>();
        private readonly HashSet<string> _flags = new HashSet<string>();

        public event Action<string, int> OnWorldStateChanged;

        public int GetInt(string key, int defaultValue = 0)
        {
            if (string.IsNullOrEmpty(key))
            {
                return defaultValue;
            }

            return _intStates.TryGetValue(key, out int value) ? value : defaultValue;
        }

        public void SetInt(string key, int value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (_intStates.TryGetValue(key, out int existing) && existing == value)
            {
                return;
            }

            _intStates[key] = value;
            OnWorldStateChanged?.Invoke(key, value);
        }

        public int IncrementInt(string key, int delta = 1)
        {
            int next = GetInt(key, 0) + delta;
            SetInt(key, next);
            return next;
        }

        public bool HasFlag(string key)
        {
            return !string.IsNullOrEmpty(key) && _flags.Contains(key);
        }

        public void SetFlag(string key, bool enabled = true)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            bool changed = enabled ? _flags.Add(key) : _flags.Remove(key);
            if (changed)
            {
                OnWorldStateChanged?.Invoke(key, enabled ? 1 : 0);
            }
        }

        public bool EvaluateCondition(StateCondition condition)
        {
            if (string.IsNullOrEmpty(condition.key))
            {
                return true;
            }

            switch (condition.comparison)
            {
                case StateComparison.Equals:
                    return GetInt(condition.key) == condition.value;
                case StateComparison.NotEquals:
                    return GetInt(condition.key) != condition.value;
                case StateComparison.GreaterOrEqual:
                    return GetInt(condition.key) >= condition.value;
                case StateComparison.LessOrEqual:
                    return GetInt(condition.key) <= condition.value;
                case StateComparison.HasFlag:
                    return HasFlag(condition.key);
                case StateComparison.NotHasFlag:
                    return !HasFlag(condition.key);
                default:
                    return true;
            }
        }

        public bool EvaluateAllConditions(List<StateCondition> conditions)
        {
            if (conditions == null || conditions.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                if (!EvaluateCondition(conditions[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public void ApplyMutation(StateMutation mutation)
        {
            if (string.IsNullOrEmpty(mutation.key))
            {
                return;
            }

            switch (mutation.mutationType)
            {
                case StateMutationType.SetInt:
                    SetInt(mutation.key, mutation.value);
                    break;
                case StateMutationType.AddInt:
                    IncrementInt(mutation.key, mutation.value);
                    break;
                case StateMutationType.SetFlag:
                    SetFlag(mutation.key, true);
                    break;
                case StateMutationType.ClearFlag:
                    SetFlag(mutation.key, false);
                    break;
            }
        }

        public void ApplyMutations(List<StateMutation> mutations)
        {
            if (mutations == null)
            {
                return;
            }

            for (int i = 0; i < mutations.Count; i++)
            {
                ApplyMutation(mutations[i]);
            }
        }

        public void ExportState(out List<IntStateEntry> intEntries, out List<string> flagEntries)
        {
            intEntries = new List<IntStateEntry>(_intStates.Count);
            foreach (KeyValuePair<string, int> kv in _intStates)
            {
                intEntries.Add(new IntStateEntry { key = kv.Key, value = kv.Value });
            }

            flagEntries = new List<string>(_flags);
        }

        public void ImportState(List<IntStateEntry> intEntries, List<string> flagEntries, bool notifyListeners = true)
        {
            _intStates.Clear();
            _flags.Clear();

            if (intEntries != null)
            {
                for (int i = 0; i < intEntries.Count; i++)
                {
                    if (!string.IsNullOrEmpty(intEntries[i].key))
                    {
                        _intStates[intEntries[i].key] = intEntries[i].value;
                    }
                }
            }

            if (flagEntries != null)
            {
                for (int i = 0; i < flagEntries.Count; i++)
                {
                    if (!string.IsNullOrEmpty(flagEntries[i]))
                    {
                        _flags.Add(flagEntries[i]);
                    }
                }
            }

            if (!notifyListeners)
            {
                return;
            }

            foreach (KeyValuePair<string, int> kv in _intStates)
            {
                OnWorldStateChanged?.Invoke(kv.Key, kv.Value);
            }

            foreach (string flag in _flags)
            {
                OnWorldStateChanged?.Invoke(flag, 1);
            }

            OnWorldStateChanged?.Invoke("_world_state_loaded", 1);
        }
    }
}
