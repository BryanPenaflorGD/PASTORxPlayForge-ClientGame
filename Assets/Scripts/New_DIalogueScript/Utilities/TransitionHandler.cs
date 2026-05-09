using System.Collections;
using System;
using UnityEngine;
using UnityEngine.UI;

public class TransitionHandler : MonoBehaviour
{
    [Tooltip("A UI Image that covers the entire screen. Set its color to Black.")]
    public Image fadeImage;
    public float fadeDuration = 0.5f;

    private Coroutine currentFade;

    void Awake()
    {
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 1f; // Force black on start
            fadeImage.color = c;
            fadeImage.gameObject.SetActive(true);
        }
    }

    public void FadeToBlack(Action onComplete = null)
    {
        if (currentFade != null) StopCoroutine(currentFade);
        currentFade = StartCoroutine(FadeRoutine(1f, onComplete));
    }

    public void FadeToClear(Action onComplete = null)
    {
        if (currentFade != null) StopCoroutine(currentFade);
        currentFade = StartCoroutine(FadeRoutine(0f, onComplete));
    }

    private IEnumerator FadeRoutine(float targetAlpha, Action onComplete)
    {
        fadeImage.gameObject.SetActive(true);
        Color c = fadeImage.color;
        float startAlpha = c.a;
        float time = 0;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float t = time / fadeDuration;

            // SMOTHSTEP CURVE: This makes the fade ease-in and ease-out so it isn't so robotic/snappy
            float smoothT = t * t * (3f - 2f * t);

            c.a = Mathf.Lerp(startAlpha, targetAlpha, smoothT);
            fadeImage.color = c;
            yield return null;
        }

        c.a = targetAlpha;
        fadeImage.color = c;

        if (targetAlpha == 0f)
        {
            fadeImage.gameObject.SetActive(false);
        }

        onComplete?.Invoke();
    }
}