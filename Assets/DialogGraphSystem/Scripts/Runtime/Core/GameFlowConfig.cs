using UnityEngine;
using System.Collections.Generic;

namespace DialogSystem.Runtime.Core
{
    [System.Serializable]
    public class GameStage
    {
        public string stageName; // Just for organization (e.g. "Level 1")
        public string dialogID;  // The ID used in your Dialog System
        public string quizSceneName; // The scene to load for the quiz
    }

    [CreateAssetMenu(fileName = "NewGameFlow", menuName = "Game Flow/Configuration")]
    public class GameFlowConfig : ScriptableObject
    {
        public List<GameStage> stages;
    }
}