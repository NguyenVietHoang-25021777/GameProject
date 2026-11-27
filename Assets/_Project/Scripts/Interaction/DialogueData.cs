using System;
using System.Collections.Generic;
using UnityEngine;
using GameProject.World;

namespace GameProject.Interaction
{
    [Serializable]
    public class DialogueChoice
    {
        public string choiceText = "Tiếp tục";
        [Tooltip("Next DialogueNode id. Leave empty to end dialogue.")]
        public string nextNodeId;
        [Tooltip("All conditions must be true for this choice to appear.")]
        public List<StateCondition> conditions = new List<StateCondition>();
        [Tooltip("WorldStateManager mutations executed when the player selects this choice.")]
        public List<StateMutation> stateEffects = new List<StateMutation>();
    }

    [Serializable]
    public class DialogueNode
    {
        public string nodeId = "start";
        public string speakerOverride;
        [TextArea(2, 5)]
        public string bodyText;
        public List<DialogueChoice> choices = new List<DialogueChoice>();
    }

    /// <summary>
    /// ScriptableObject defining a branching NPC conversation integrated with WorldStateManager.
    /// </summary>
    [CreateAssetMenu(fileName = "Dialogue_New", menuName = "GameProject/Dialogue Data")]
    public class DialogueData : ScriptableObject
    {
        public string speakerName = "NPC";
        public string startNodeId = "greeting";
        public List<DialogueNode> nodes = new List<DialogueNode>();

        public DialogueNode GetNode(string nodeId)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].nodeId == nodeId)
                {
                    return nodes[i];
                }
            }

            return null;
        }
    }
}
