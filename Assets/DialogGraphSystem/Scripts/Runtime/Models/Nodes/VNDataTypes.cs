using UnityEngine;
using System;

namespace DialogSystem.Runtime.Models
{
    public enum VNPosition { FarLeft, Left, Center, Right, FarRight }

    public enum VNCharacterState { Normal, Dimmed, Hidden }

    [Serializable]
    public class VNCharacterEntry
    {
        public string characterName;
        public VNPosition position;

        // [CHANGED] Instead of a static Sprite, we use an Animator
        public RuntimeAnimatorController animatorController;
        public string animationName; // e.g. "Idle", "Talk_Happy"

        public bool flipX;
        public VNCharacterState state = VNCharacterState.Normal;
    }
}