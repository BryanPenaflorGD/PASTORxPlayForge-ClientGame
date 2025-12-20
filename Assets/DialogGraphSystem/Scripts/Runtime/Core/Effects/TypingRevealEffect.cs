using System.Collections;
using TMPro;
using DialogSystem.Runtime.Interfaces;
using UnityEngine;
using System;

namespace DialogSystem.Runtime.Core.Effects
{
    public sealed class TypingRevealEffect : ITextRevealEffect
    {
        private readonly string _line;
        private readonly TMP_Text _target;
        private readonly Func<float> _getCps;
        private readonly Func<char, float> _pauseFor;
        private readonly bool _doDebug;

        public bool IsCancelled { get; private set; }

        public TypingRevealEffect(
            string line,
            TMP_Text target,
            Func<float> getCps,
            Func<char, float> pauseFor,
            bool doDebug = false)
        {
            _line = line ?? string.Empty;
            _target = target;
            _getCps = getCps ?? (() => 40f);
            _pauseFor = pauseFor ?? (_ => 0f);
            _doDebug = doDebug;
        }

        public void Cancel() => IsCancelled = true;

        public void CompleteImmediately()
        {
            if (_target) _target.text = _line;
        }

        public IEnumerator Play()
        {
            if (_target == null)
                yield break;

            _target.text = string.Empty;

            float tAccum = 0f;
            int shown = 0;
            while (!IsCancelled && shown < _line.Length)
            {
                float cps = Mathf.Max(1f, _getCps());
                tAccum += Time.deltaTime * cps;

                // reveal as many chars as accumulated
                while (!IsCancelled && tAccum >= 1f && shown < _line.Length)
                {
                    tAccum -= 1f;
                    shown++;
                    _target.text = _line.Substring(0, shown);

                    // optional per-char pauses
                    float p = _pauseFor(_line[shown - 1]);
                    if (p > 0f)
                    {
                        float w = 0f;
                        while (!IsCancelled && w < p)
                        {
                            w += Time.deltaTime;
                            yield return null;
                        }
                    }
                }

                yield return null;
            }

            if (IsCancelled)
            {
                // Ensure final text is consistent when cancelled (skip line)
                CompleteImmediately();
                yield break;
            }

            // normal completion leaves full text shown by loop
        }
    }
}
