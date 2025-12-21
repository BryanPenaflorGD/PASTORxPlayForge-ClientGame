using UnityEngine;
using System;

namespace DialogSystem.Runtime.Models
{
    // 1. The 5 specific positions you requested
    public enum VNPosition
    {
        FarLeft,
        Left,
        Center,
        Right,
        FarRight
    }

    // 2. The data for one character on screen
    [Serializable]
    public class VNCharacterEntry
    {
        public string characterName; // Optional, for reference
        public VNPosition position;  // Where they stand
        public Sprite expression;    // The gesture/sprite
        public bool flipX;           // Useful if a character looks the wrong way
    }
}