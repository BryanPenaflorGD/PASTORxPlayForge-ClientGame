using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IntuitiveCreative
{
    public class IntuitiveAudioAdjustWindow : IntuitiveCreativeEditorWindow
    {
        [SerializeField] private AudioClip sourceClip;
        [SerializeField] private float trimStart;
        [SerializeField] private float trimEnd = 1f;
        [SerializeField] private float fadeInDuration;
        [SerializeField] private float fadeOutDuration;
        [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        [SerializeField] private bool reverseAudio;

        [SerializeField] private float playbackSpeed = 1f;
        [SerializeField] private bool trimSilence;
        [SerializeField] private float trimThreshold = 0.02f;
        [SerializeField] private bool normalizeVolume;
        [SerializeField] private float volumeIncrease;

        [SerializeField] private bool loopPreview;
        [SerializeField] private float previewVolume = 1f;

        private AudioSource previewAudioSource;
        private AudioClip previewClip;
        private bool previewDirty = true;

        private float[] waveformSamples;
        private int waveformWidth;
        private const int WaveformResolution = 1024;
        private const float MinWaveformHeight = 2f;
        private const float SoftLimiterThreshold = 0.98f;
        private const float TrimSilenceFadeOutSeconds = 0.005f;
        private const float NormalizePeakDb = -0.3f;
        private const int MaxPreviewFrames = 2000000;
        private bool waveformDirty = true;
        private Texture2D waveformTexture;
        private int waveformTextureHeight;
        private bool waveformTextureDirty = true;
        private bool allowWaveformUpdateThisFrame = true;
        private bool waveformDeferred;
        private bool wasEditingTextField;

        private Vector2 scrollPosition;

        private const string PrefPreviewVolume = "IntuitiveAudioEditor_PreviewVolume";
        [SerializeField] private bool showAdvanced;
        protected override string HeaderSubtitle => "Audio Adjust (Trim, Fade, Volume)";

        [MenuItem("Tools/Intuitive Creative/Audio Adjust", false, 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<IntuitiveAudioAdjustWindow>("Audio Adjust");
            window.minSize = new Vector2(480f, 480f);
            window.Show();
        }

        [MenuItem("Assets/Intuitive Creative/Audio Adjust", false, -100)]
        public static void EditSelectedAudioClip()
        {
            AudioClip selectedClip = Selection.activeObject as AudioClip;
            if (selectedClip != null)
            {
                OpenWithClip(selectedClip);
            }
        }

        [MenuItem("Assets/Intuitive Creative/Audio Adjust", true, -100)]
        public static bool ValidateEditSelectedAudioClip()
        {
            return Selection.activeObject is AudioClip;
        }

        [MenuItem("CONTEXT/AudioClip/Intuitive Creative/Audio Adjust", false, -1000)]
        public static void EditContextAudioClip(MenuCommand command)
        {
            AudioClip clip = command.context as AudioClip;
            if (clip != null)
            {
                OpenWithClip(clip);
            }
        }

        public static void OpenWithClip(AudioClip clip)
        {
            var window = GetWindow<IntuitiveAudioAdjustWindow>("Audio Adjust");
            window.minSize = new Vector2(480f, 480f);
            window.Initialize(clip);
            window.Show();
            window.Focus();
        }

        public void Initialize(AudioClip clip)
        {
            sourceClip = clip;
            trimStart = 0f;
            trimEnd = sourceClip != null ? sourceClip.length : 1f;
            fadeInDuration = 0f;
            fadeOutDuration = 0f;
            reverseAudio = false;

            previewVolume = EditorPrefs.GetFloat(PrefPreviewVolume, 1f);
            waveformDirty = true;
            previewDirty = true;
            waveformTextureDirty = true;
            waveformDeferred = false;
            DestroyWaveformTexture();
            wasEditingTextField = false;

            if (previewAudioSource != null)
            {
                previewAudioSource.Stop();
                DestroyPreviewClip();
                previewAudioSource.volume = previewVolume;
            }

            Repaint();
        }

        private void OnEnable()
        {
            previewVolume = EditorPrefs.GetFloat(PrefPreviewVolume, 1f);

            GameObject go = new GameObject("IntuitiveAudioPreviewPlayer");
            go.hideFlags = HideFlags.HideAndDontSave;
            previewAudioSource = go.AddComponent<AudioSource>();
            previewAudioSource.playOnAwake = false;
            previewAudioSource.loop = false;
            previewAudioSource.volume = previewVolume;

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            if (previewAudioSource != null)
            {
                DestroyImmediate(previewAudioSource.gameObject);
            }

            DestroyPreviewClip();
            DestroyWaveformTexture();
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            waveformDirty = true;
            waveformTextureDirty = true;
            previewDirty = true;
            waveformDeferred = false;
            Repaint();
        }

        private void OnGUI()
        {
            IntuitiveCreativeStyles.Ensure();
            UpdateWaveformScheduling();

            if (previewAudioSource != null)
            {
                if (Mathf.Abs(previewAudioSource.volume - previewVolume) > 0.01f)
                {
                    previewAudioSource.volume = previewVolume;
                }

                if (previewAudioSource.isPlaying)
                {
                    Repaint();
                }
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawHeader();
            bool hasClip = DrawClipWaveformSection();
            if (hasClip)
            {
                DrawTrimTab();
                DrawSaveButtons(() => SaveTrimmedOverwrite(true), () => SaveTrimmedOverwrite(false), SaveTrimmedAsCopy);
            }
            UpdateLivePreview();
            EditorGUILayout.EndScrollView();
        }

        private bool IsLongClip => sourceClip != null && sourceClip.length > 30f;

        private void UpdateWaveformScheduling()
        {
            if (!IsLongClip)
            {
                allowWaveformUpdateThisFrame = true;
                waveformDeferred = false;
                wasEditingTextField = EditorGUIUtility.editingTextField;
                return;
            }

            bool mouseUp = Event.current.type == EventType.MouseUp || Event.current.rawType == EventType.MouseUp;
            bool commitFocus = wasEditingTextField && !EditorGUIUtility.editingTextField;
            allowWaveformUpdateThisFrame = mouseUp || commitFocus || waveformSamples == null;

            if ((mouseUp || commitFocus) && waveformDeferred)
            {
                waveformDirty = true;
                waveformTextureDirty = true;
                waveformDeferred = false;
            }

            wasEditingTextField = EditorGUIUtility.editingTextField;
        }

        private void MarkAudioChanged()
        {
            previewDirty = true;

            if (IsLongClip && !allowWaveformUpdateThisFrame)
            {
                waveformDeferred = true;
            }
            else
            {
                waveformDirty = true;
                waveformTextureDirty = true;
            }
        }

        private bool DrawClipWaveformSection()
        {
            EditorGUILayout.BeginVertical(IntuitiveCreativeStyles.CardStyle);
            EditorGUILayout.LabelField("Preview", IntuitiveCreativeStyles.SectionHeader);

            EditorGUI.BeginChangeCheck();
            AudioClip newSource = (AudioClip)EditorGUILayout.ObjectField("Source Clip", sourceClip, typeof(AudioClip), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Change Source Clip");
                Initialize(newSource);
                MarkAudioChanged();
            }

            if (sourceClip == null)
            {
                EditorGUILayout.HelpBox("Assign an AudioClip to adjust.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return false;
            }

            Rect waveformRect = GUILayoutUtility.GetRect(0f, 120f, GUILayout.ExpandWidth(true));
            waveformRect.x += 4f;
            waveformRect.width -= 8f;

            EnsureWaveform((int)waveformRect.width);
            DrawWaveform(waveformRect);

            if (Event.current.type == EventType.MouseDown && waveformRect.Contains(Event.current.mousePosition))
            {
                if (previewAudioSource != null && previewClip != null)
                {
                    float clickPercent = (Event.current.mousePosition.x - waveformRect.x) / waveformRect.width;
                    clickPercent = Mathf.Clamp01(clickPercent);

                    if (!previewAudioSource.isPlaying)
                    {
                        CreatePreviewClipAndPlay();
                    }

                    if (previewClip != null)
                    {
                        previewAudioSource.time = clickPercent * previewClip.length;
                        Repaint();
                    }
                }
            }

            EditorGUILayout.Space(6f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Preview Volume", GUILayout.Width(120f));
            EditorGUI.BeginChangeCheck();
            float newVol = EditorGUILayout.Slider(previewVolume, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Change Preview Volume");
                previewVolume = newVol;
                EditorPrefs.SetFloat(PrefPreviewVolume, previewVolume);
                if (previewAudioSource != null) previewAudioSource.volume = previewVolume;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            bool newLoop = EditorGUILayout.Toggle("Loop Preview", loopPreview);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Toggle Loop Preview");
                loopPreview = newLoop;
                previewDirty = true;
            }

            EditorGUILayout.Space(6f);

            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f, 1f);

            if (GUILayout.Button(previewAudioSource != null && previewAudioSource.isPlaying ? "Stop Preview" : "Play Preview", IntuitiveCreativeStyles.PrimaryButton))
            {
                if (previewAudioSource != null && previewAudioSource.isPlaying)
                {
                    previewAudioSource.Stop();
                    DestroyPreviewClip();
                }
                else
                {
                    CreatePreviewClipAndPlay();
                }
            }

            GUI.backgroundColor = previous;

            EditorGUILayout.EndVertical();
            return true;
        }
        private void DrawTrimTab()
        {
            EditorGUILayout.BeginVertical(IntuitiveCreativeStyles.CardStyle);
            EditorGUILayout.LabelField("Adjustments", IntuitiveCreativeStyles.SectionHeader);

            if (sourceClip == null)
            {
                EditorGUILayout.HelpBox("Assign an AudioClip to adjust.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            float oldTrimStart = trimStart;
            float oldTrimEnd = trimEnd;
            float oldFadeIn = fadeInDuration;
            float oldFadeOut = fadeOutDuration;
            bool oldReverse = reverseAudio;
            float oldPlaybackSpeed = playbackSpeed;
            bool oldTrimSilence = trimSilence;
            float oldTrimThreshold = trimThreshold;
            bool oldNormalize = normalizeVolume;
            float oldVolumeIncrease = volumeIncrease;

            float clipLength = sourceClip.length;
            float newTrimStart = trimStart;
            float newTrimEnd = trimEnd;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Trim", GUILayout.Width(90f));
            EditorGUI.BeginChangeCheck();
            newTrimStart = EditorGUILayout.FloatField(newTrimStart, GUILayout.Width(60f));
            bool trimStartChanged = EditorGUI.EndChangeCheck();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.MinMaxSlider(ref newTrimStart, ref newTrimEnd, 0f, clipLength);
            bool trimSliderChanged = EditorGUI.EndChangeCheck();

            EditorGUI.BeginChangeCheck();
            newTrimEnd = EditorGUILayout.FloatField(newTrimEnd, GUILayout.Width(60f));
            bool trimEndChanged = EditorGUI.EndChangeCheck();
            EditorGUILayout.EndHorizontal();

            if (trimStartChanged || trimSliderChanged || trimEndChanged)
            {
                Undo.RecordObject(this, "Change Trim");
                trimStart = Mathf.Clamp(newTrimStart, 0f, clipLength);
                trimEnd = Mathf.Clamp(newTrimEnd, trimStart, clipLength);
                MarkAudioChanged();
            }

            float maxFade = Mathf.Max(0f, trimEnd - trimStart);
            if (fadeInDuration + fadeOutDuration > maxFade)
            {
                fadeOutDuration = Mathf.Max(0f, maxFade - fadeInDuration);
            }
            float fadeInEnd = Mathf.Clamp(trimStart + fadeInDuration, trimStart, trimEnd);
            float fadeOutStart = Mathf.Clamp(trimEnd - fadeOutDuration, fadeInEnd, trimEnd);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Fade", GUILayout.Width(90f));
            EditorGUI.BeginChangeCheck();
            float fadeInField = EditorGUILayout.FloatField(fadeInDuration, GUILayout.Width(60f));
            bool fadeInFieldChanged = EditorGUI.EndChangeCheck();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.MinMaxSlider(ref fadeInEnd, ref fadeOutStart, trimStart, trimEnd);
            bool fadeSliderChanged = EditorGUI.EndChangeCheck();

            EditorGUI.BeginChangeCheck();
            float fadeOutField = EditorGUILayout.FloatField(fadeOutDuration, GUILayout.Width(60f));
            bool fadeOutFieldChanged = EditorGUI.EndChangeCheck();
            EditorGUILayout.EndHorizontal();

            if (fadeInFieldChanged || fadeOutFieldChanged || fadeSliderChanged)
            {
                Undo.RecordObject(this, "Change Fades");

                if (fadeSliderChanged)
                {
                    fadeInDuration = Mathf.Clamp(fadeInEnd - trimStart, 0f, maxFade);
                    fadeOutDuration = Mathf.Clamp(trimEnd - fadeOutStart, 0f, maxFade - fadeInDuration);
                }
                else
                {
                    fadeInDuration = Mathf.Clamp(fadeInField, 0f, maxFade);
                    fadeOutDuration = Mathf.Clamp(fadeOutField, 0f, maxFade - fadeInDuration);
                }

                MarkAudioChanged();
            }

            EditorGUI.BeginDisabledGroup(normalizeVolume);
            EditorGUI.BeginChangeCheck();
            volumeIncrease = EditorGUILayout.Slider("Volume Adjustment", volumeIncrease, -1f, 3f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Change Volume Amount");
                MarkAudioChanged();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginChangeCheck();
            normalizeVolume = EditorGUILayout.Toggle("Normalize Volume", normalizeVolume);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Toggle Normalize");
                MarkAudioChanged();
            }

            EditorGUILayout.Space(4f);
            EditorGUI.BeginChangeCheck();
            showAdvanced = EditorGUILayout.Toggle("Advanced", showAdvanced);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Toggle Advanced");
            }

            if (showAdvanced)
            {
                EditorGUI.BeginChangeCheck();
                playbackSpeed = EditorGUILayout.Slider("Playback Speed", playbackSpeed, 0.25f, 4f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Change Playback Speed");
                    MarkAudioChanged();
                }

                EditorGUI.BeginChangeCheck();
                trimSilence = EditorGUILayout.Toggle("Trim Silence", trimSilence);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Toggle Trim Silence");
                    MarkAudioChanged();
                }

                if (trimSilence)
                {
                    EditorGUI.BeginChangeCheck();
                    trimThreshold = EditorGUILayout.Slider("Trim Threshold", trimThreshold, 0f, 1f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(this, "Change Trim Threshold");
                        MarkAudioChanged();
                    }
                }

                EditorGUI.BeginChangeCheck();
                reverseAudio = EditorGUILayout.Toggle("Reverse Audio", reverseAudio);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Toggle Reverse Audio");
                    MarkAudioChanged();
                }

                EditorGUI.BeginChangeCheck();
                fadeInCurve = EditorGUILayout.CurveField("Fade In Curve", fadeInCurve);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Change Fade In Curve");
                    MarkAudioChanged();
                }

                EditorGUI.BeginChangeCheck();
                fadeOutCurve = EditorGUILayout.CurveField("Fade Out Curve", fadeOutCurve);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Change Fade Out Curve");
                    MarkAudioChanged();
                }
            }

            if (!Mathf.Approximately(oldTrimStart, trimStart) ||
                !Mathf.Approximately(oldTrimEnd, trimEnd) ||
                !Mathf.Approximately(oldFadeIn, fadeInDuration) ||
                !Mathf.Approximately(oldFadeOut, fadeOutDuration) ||
                oldReverse != reverseAudio ||
                !Mathf.Approximately(oldPlaybackSpeed, playbackSpeed) ||
                oldTrimSilence != trimSilence ||
                !Mathf.Approximately(oldTrimThreshold, trimThreshold) ||
                oldNormalize != normalizeVolume ||
                !Mathf.Approximately(oldVolumeIncrease, volumeIncrease))
            {
                MarkAudioChanged();
            }

            EditorGUILayout.EndVertical();
        }

        private void UpdateLivePreview()
        {
            if (!previewDirty || previewAudioSource == null || !previewAudioSource.isPlaying || !loopPreview)
            {
                return;
            }

            float time = previewAudioSource.time;
            CreatePreviewClipAndPlay();
            if (previewClip != null)
            {
                previewAudioSource.time = Mathf.Clamp(time, 0f, previewClip.length);
            }

            previewDirty = false;
        }
        private void DestroyPreviewClip()
        {
            if (previewClip != null)
            {
                DestroyImmediate(previewClip);
                previewClip = null;
            }
        }

        private void CreatePreviewClipAndPlay()
        {
            if (!TryGetProcessedData(false, out float[] processedData, out int freq, out int channels)) return;

            if (!TryBuildPreviewData(processedData, channels, freq, out float[] previewData, out int previewFreq)) return;

            int sampleFrames = previewData.Length / channels;
            if (sampleFrames <= 0) return;

            DestroyPreviewClip();
            previewClip = AudioClip.Create("PreviewTrim", sampleFrames, channels, previewFreq, false);
            previewClip.SetData(previewData, 0);
            previewAudioSource.clip = previewClip;
            previewAudioSource.volume = previewVolume;
            previewAudioSource.loop = loopPreview;
            previewAudioSource.Play();
            previewDirty = false;
        }

        private static bool TryBuildPreviewData(float[] processedData, int channels, int frequency, out float[] previewData, out int previewFrequency)
        {
            previewData = processedData;
            previewFrequency = frequency;

            if (processedData == null || processedData.Length == 0 || channels <= 0 || frequency <= 0)
            {
                return false;
            }

            int totalFrames = processedData.Length / channels;
            if (totalFrames <= 0) return false;

            if (totalFrames <= MaxPreviewFrames)
            {
                return true;
            }

            int factor = Mathf.Max(2, Mathf.CeilToInt((float)totalFrames / MaxPreviewFrames));
            int outputFrames = Mathf.CeilToInt((float)totalFrames / factor);

            previewData = new float[outputFrames * channels];
            for (int frame = 0; frame < outputFrames; frame++)
            {
                int srcFrame = Mathf.Min(totalFrames - 1, frame * factor);
                int srcOffset = srcFrame * channels;
                int dstOffset = frame * channels;
                for (int ch = 0; ch < channels; ch++)
                {
                    previewData[dstOffset + ch] = processedData[srcOffset + ch];
                }
            }

            previewFrequency = Mathf.Max(1, Mathf.RoundToInt((float)frequency / factor));
            return true;
        }

        private bool TryGetProcessedData(bool showErrors, out float[] processedData, out int frequency, out int channels)
        {
            processedData = null;
            frequency = 0;
            channels = 0;

            if (sourceClip == null)
            {
                if (showErrors)
                {
                    EditorUtility.DisplayDialog("Audio Adjust", "Assign an AudioClip to process.", "OK");
                }
                return false;
            }

            frequency = sourceClip.frequency;
            channels = sourceClip.channels;

            int startFrame = Mathf.Clamp(Mathf.FloorToInt(trimStart * frequency), 0, sourceClip.samples);
            int endFrame = Mathf.Clamp(Mathf.FloorToInt(trimEnd * frequency), 0, sourceClip.samples);
            int sampleFrames = Mathf.Max(0, endFrame - startFrame);
            if (sampleFrames <= 0)
            {
                if (showErrors)
                {
                    EditorUtility.DisplayDialog("Audio Adjust", "Trim range is empty. Adjust start/end times.", "OK");
                }
                return false;
            }

            int sampleLength = sampleFrames * channels;
            float[] trimmedData = new float[sampleLength];
            sourceClip.GetData(trimmedData, startFrame);

            if (reverseAudio)
            {
                ReverseAudioInPlace(trimmedData, channels);
            }

            processedData = ProcessAudio(trimmedData, channels, frequency);
            return true;
        }

        private void SaveTrimmedAsCopy()
        {
            if (!TryGetProcessedData(true, out float[] trimmedData, out int frequency, out int channels)) return;
            if (!TryGetSourceAssetPath(out string sourcePath)) return;

            string directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
            string baseName = Path.GetFileNameWithoutExtension(sourcePath);
            string timestamp = System.DateTime.Now.ToString("yyMMddHHmmss");
            string newPath = Path.Combine(directory, baseName + "_new_" + timestamp + ".wav");
            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);

            WriteWavToPath(trimmedData, frequency, channels, newPath);
            AssetDatabase.ImportAsset(newPath);
            ApplyPcmImportSettings(newPath);
        }

        private void SaveTrimmedOverwrite(bool createBackup)
        {
            if (!TryGetProcessedData(true, out float[] trimmedData, out int frequency, out int channels)) return;

            if (!TryGetSourceAssetPath(out string sourcePath)) return;
            if (!IsWavPath(sourcePath))
            {
                EditorUtility.DisplayDialog("Overwrite Unsupported", "Overwrite works only for WAV clips. Use Save As Copy instead.", "OK");
                return;
            }

            if (!createBackup)
            {
                if (!EditorUtility.DisplayDialog("Overwrite Audio", "This will overwrite the original source file. Are you sure?", "Overwrite", "Cancel"))
                {
                    return;
                }
            }

            if (createBackup)
            {
                string timestamp = System.DateTime.Now.ToString("yyMMddHHmmss");
                string backupPath = Path.Combine(Path.GetDirectoryName(sourcePath) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(sourcePath) + "_old_" + timestamp + ".wav");
                backupPath = AssetDatabase.GenerateUniqueAssetPath(backupPath);

                if (!AssetDatabase.CopyAsset(sourcePath, backupPath))
                {
                    EditorUtility.DisplayDialog("Backup Failed", "Could not create a backup copy.", "OK");
                    return;
                }
            }

            WriteWavToPath(trimmedData, frequency, channels, sourcePath);
            AssetDatabase.ImportAsset(sourcePath);
            ApplyPcmImportSettings(sourcePath);
        }

        private bool TryGetSourceAssetPath(out string path)
        {
            path = sourceClip != null ? AssetDatabase.GetAssetPath(sourceClip) : string.Empty;
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("Save Error", "Source clip has no asset path. Use Save As Copy.", "OK");
                return false;
            }

            return true;
        }

        private static bool IsWavPath(string path)
        {
            return string.Equals(Path.GetExtension(path), ".wav", System.StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyFades(float[] data, int freq, int channels)
        {
            int totalSamples = data.Length;
            if (totalSamples == 0) return;

            int fadeInSamples = Mathf.Clamp(Mathf.FloorToInt(fadeInDuration * freq) * channels, 0, totalSamples);
            int fadeOutSamples = Mathf.Clamp(Mathf.FloorToInt(fadeOutDuration * freq) * channels, 0, totalSamples);

            if (fadeInSamples + fadeOutSamples > totalSamples)
            {
                fadeOutSamples = Mathf.Max(0, totalSamples - fadeInSamples);
            }

            for (int i = 0; i < fadeInSamples; i++)
            {
                float t = fadeInSamples == 0 ? 1f : (float)i / fadeInSamples;
                data[i] *= fadeInCurve.Evaluate(t);
            }

            int startFadeOut = totalSamples - fadeOutSamples;
            for (int i = 0; i < fadeOutSamples; i++)
            {
                float t = fadeOutSamples == 0 ? 1f : (float)i / fadeOutSamples;
                int index = startFadeOut + i;
                if (index < 0 || index >= totalSamples) continue;
                data[index] *= fadeOutCurve.Evaluate(t);
            }
        }

        private float[] ProcessAudio(float[] data, int channels, int frequency)
        {
            float[] processed = ApplyPlaybackSpeed(data, channels, playbackSpeed);
            processed = ApplyTrimSilence(processed, channels, trimSilence, trimThreshold);

            ApplyFades(processed, frequency, channels);

            if (!normalizeVolume)
            {
                float gain = 1f + volumeIncrease;
                for (int i = 0; i < processed.Length; i++)
                {
                    processed[i] *= gain;
                }
            }

            if (normalizeVolume)
            {
                float peak = 0f;
                for (int i = 0; i < processed.Length; i++)
                {
                    float abs = Mathf.Abs(processed[i]);
                    if (abs > peak) peak = abs;
                }

                if (peak > 0f)
                {
                    float targetPeak = Mathf.Pow(10f, NormalizePeakDb / 20f);
                    float scale = targetPeak / peak;
                    for (int i = 0; i < processed.Length; i++)
                    {
                        processed[i] *= scale;
                    }
                }
            }

            if (trimSilence)
            {
                ApplyAutoFadeOutInPlace(processed, channels, frequency, TrimSilenceFadeOutSeconds);
            }

            ApplySoftLimiterInPlace(processed);
            return processed;
        }

        private float[] ApplyPlaybackSpeed(float[] data, int channels, float speed)
        {
            if (Mathf.Abs(speed - 1f) < 0.0001f)
            {
                return data;
            }

            int inputFrames = data.Length / channels;
            if (inputFrames <= 1) return data;

            float safeSpeed = Mathf.Clamp(speed, 0.01f, 4f);
            int outputFrames = Mathf.Max(1, Mathf.RoundToInt(inputFrames / safeSpeed));
            float[] resampled = new float[outputFrames * channels];

            for (int frame = 0; frame < outputFrames; frame++)
            {
                float srcPos = frame * safeSpeed;
                int srcIndex = Mathf.Clamp((int)srcPos, 0, inputFrames - 1);
                int nextIndex = Mathf.Min(srcIndex + 1, inputFrames - 1);
                float t = Mathf.Clamp01(srcPos - srcIndex);

                int srcOffset = srcIndex * channels;
                int nextOffset = nextIndex * channels;
                int dstOffset = frame * channels;
                for (int ch = 0; ch < channels; ch++)
                {
                    float a = data[srcOffset + ch];
                    float b = data[nextOffset + ch];
                    resampled[dstOffset + ch] = Mathf.Lerp(a, b, t);
                }
            }

            return resampled;
        }

        private float[] ApplyTrimSilence(float[] data, int channels, bool enabled, float threshold)
        {
            if (!enabled) return data;

            float clampThreshold = Mathf.Clamp01(threshold);
            int frames = data.Length / channels;
            if (frames == 0) return data;

            int startFrame = 0;
            for (; startFrame < frames; startFrame++)
            {
                if (FrameAboveThreshold(data, channels, startFrame, clampThreshold)) break;
            }

            if (startFrame >= frames)
            {
                return new float[channels];
            }

            int endFrame = frames - 1;
            for (; endFrame > startFrame; endFrame--)
            {
                if (FrameAboveThreshold(data, channels, endFrame, clampThreshold)) break;
            }

            int newFrames = endFrame - startFrame + 1;
            float[] trimmed = new float[newFrames * channels];
            System.Array.Copy(data, startFrame * channels, trimmed, 0, trimmed.Length);
            return trimmed;
        }

        private static bool FrameAboveThreshold(float[] data, int channels, int frame, float threshold)
        {
            int offset = frame * channels;
            for (int ch = 0; ch < channels; ch++)
            {
                if (Mathf.Abs(data[offset + ch]) >= threshold) return true;
            }
            return false;
        }

        private void ApplySoftLimiterInPlace(float[] data)
        {
            float threshold = Mathf.Clamp01(SoftLimiterThreshold);
            float range = Mathf.Max(0.0001f, 1f - threshold);

            for (int i = 0; i < data.Length; i++)
            {
                float sample = data[i];
                float abs = Mathf.Abs(sample);
                if (abs <= threshold) continue;

                float excess = (abs - threshold) / range;
                float limited = threshold + range * (float)System.Math.Tanh(excess);
                data[i] = Mathf.Sign(sample) * limited;
            }
        }

        private void ApplyAutoFadeOutInPlace(float[] data, int channels, int frequency, float fadeSeconds)
        {
            if (data == null || data.Length == 0 || channels <= 0 || frequency <= 0) return;

            int frames = data.Length / channels;
            int fadeFrames = Mathf.Clamp(Mathf.RoundToInt(frequency * Mathf.Max(0.0001f, fadeSeconds)), 1, frames);
            int startFrame = frames - fadeFrames;

            for (int f = 0; f < fadeFrames; f++)
            {
                float scale = 1f - (float)(f + 1) / fadeFrames;
                int baseIndex = (startFrame + f) * channels;
                for (int ch = 0; ch < channels; ch++)
                {
                    data[baseIndex + ch] *= scale;
                }
            }
        }


        private void WriteWavToPath(float[] data, int frequency, int channels, string path)
        {
            int samples = data.Length / channels;
            AudioClip clip = AudioClip.Create("IntuitiveTrimTemp", samples, channels, frequency, false);
            clip.SetData(data, 0);

            byte[] bytes = IntuitiveAudioWavUtility.FromAudioClip(clip);
            DestroyImmediate(clip);
            File.WriteAllBytes(path, bytes);
        }

        private static void ApplyPcmImportSettings(string assetPath)
        {
            AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null) return;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        private void EnsureWaveform(int targetWidth)
        {
            if (sourceClip == null)
            {
                waveformSamples = null;
                waveformWidth = 0;
                DestroyWaveformTexture();
                return;
            }

            if (!allowWaveformUpdateThisFrame)
            {
                return;
            }

            if (!waveformDirty && waveformSamples != null)
            {
                return;
            }

            waveformWidth = WaveformResolution;
            waveformDirty = false;

            if (!TryGetProcessedData(false, out float[] processedData, out int freq, out int channels) || processedData.Length == 0)
            {
                waveformSamples = null;
                return;
            }

            int samplesPerPixel = Mathf.Max(1, processedData.Length / waveformWidth);
            waveformSamples = new float[waveformWidth];
            for (int i = 0; i < waveformWidth; i++)
            {
                float max = 0f;
                int start = i * samplesPerPixel;
                int end = Mathf.Min(start + samplesPerPixel, processedData.Length);
                for (int j = start; j < end; j += channels)
                {
                    float val = Mathf.Abs(processedData[j]);
                    if (val > max) max = val;
                }
                waveformSamples[i] = max;
            }

            waveformTextureDirty = true;
            Repaint();
        }

        private void DrawWaveform(Rect rect)
        {
            if (waveformSamples == null || waveformSamples.Length == 0) return;
            if (Event.current.type != EventType.Repaint) return;

            EnsureWaveformTexture(Mathf.RoundToInt(rect.height));
            if (waveformTexture != null)
            {
                GUI.DrawTexture(rect, waveformTexture, ScaleMode.StretchToFill, false);
            }

            if (previewAudioSource != null && previewAudioSource.isPlaying && previewClip != null)
            {
                float progress = previewAudioSource.time / previewClip.length;
                float playheadX = rect.x + (progress * rect.width);

                EditorGUI.DrawRect(new Rect(playheadX, rect.y, 2f, rect.height), IntuitiveCreativeStyles.PlayheadColor);
            }
        }

        private void EnsureWaveformTexture(int height)
        {
            if (waveformSamples == null || waveformSamples.Length == 0)
            {
                DestroyWaveformTexture();
                return;
            }

            int targetHeight = Mathf.Clamp(height, 32, 512);
            if (!waveformTextureDirty && waveformTexture != null && waveformTextureHeight == targetHeight)
            {
                return;
            }

            waveformTextureHeight = targetHeight;
            int width = waveformSamples.Length;

            if (waveformTexture == null || waveformTexture.width != width || waveformTexture.height != targetHeight)
            {
                DestroyWaveformTexture();
                waveformTexture = new Texture2D(width, targetHeight, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            Color32 background = IntuitiveCreativeStyles.WaveformBackground;
            Color32 waveColor = IntuitiveCreativeStyles.WaveformColor;
            Color32[] pixels = new Color32[width * targetHeight];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = background;
            }

            int midY = targetHeight / 2;
            for (int x = 0; x < width; x++)
            {
                float heightPixels = Mathf.Max(MinWaveformHeight, waveformSamples[x] * targetHeight);
                int half = Mathf.Clamp(Mathf.RoundToInt(heightPixels / 2f), 1, targetHeight / 2);
                int yMin = Mathf.Clamp(midY - half, 0, targetHeight - 1);
                int yMax = Mathf.Clamp(midY + half, 0, targetHeight - 1);
                for (int y = yMin; y <= yMax; y++)
                {
                    pixels[y * width + x] = waveColor;
                }
            }

            waveformTexture.SetPixels32(pixels);
            waveformTexture.Apply(false, false);
            waveformTextureDirty = false;
        }

        private void DestroyWaveformTexture()
        {
            if (waveformTexture != null)
            {
                DestroyImmediate(waveformTexture);
                waveformTexture = null;
            }

            waveformTextureHeight = 0;
            waveformTextureDirty = true;
        }

        private static void ReverseAudioInPlace(float[] data, int channels)
        {
            if (channels <= 1)
            {
                System.Array.Reverse(data);
                return;
            }

            int frames = data.Length / channels;
            int half = frames / 2;

            for (int i = 0; i < half; i++)
            {
                int leftIndex = i * channels;
                int rightIndex = (frames - 1 - i) * channels;
                for (int ch = 0; ch < channels; ch++)
                {
                    float temp = data[leftIndex + ch];
                    data[leftIndex + ch] = data[rightIndex + ch];
                    data[rightIndex + ch] = temp;
                }
            }
        }
    }
}
