using UnityEngine;
using System.Collections.Generic;
using System;

namespace DialogSystem.Runtime.Models
{
    [CreateAssetMenu(fileName = "NewCharacter", menuName = "Dialog System/Character Profile")]
    public class CharacterProfile : ScriptableObject
    {
        public string characterName;
        public Color nameColor = Color.white; // Useful for UI text color

        [Header("Emotions")]
        public List<CharacterExpression> expressions = new List<CharacterExpression>();

        // Helper to find sprite by name
        public Sprite GetSprite(string emotionName)
        {
            var expr = expressions.Find(x => x.emotionName == emotionName);
            return expr.sprite != null ? expr.sprite : (expressions.Count > 0 ? expressions[0].sprite : null);
        }
    }

    [Serializable]
    public struct CharacterExpression
    {
        public string emotionName; // e.g., "Happy", "Sad", "Neutral"
        public Sprite sprite;
    }
}