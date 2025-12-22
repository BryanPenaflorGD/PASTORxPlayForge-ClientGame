using UnityEngine;
using System;

namespace DialogSystem.Runtime.Models
{
    // 1. Position Enum
    public enum VNPosition
    {
        FarLeft,
        Left,
        Center,
        Right,
        FarRight
    }

    // 2. State Enum (New!)
    public enum VNCharacterState
    {
        Normal,     // Fully lit
        Dimmed,     // Darker/Gray (Active listener)
        Hidden      // Completely invisible
    }

    // 3. Character Entry Data
    [Serializable]
    public class VNCharacterEntry
    {
        public string characterName;
        public VNPosition position;
        public Sprite expression;
        public bool flipX;

        // The new State field
        public VNCharacterState state = VNCharacterState.Normal;
    }
}