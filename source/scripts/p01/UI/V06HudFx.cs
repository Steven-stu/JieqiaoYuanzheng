using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Jieqiao.UI
{
    public static class V06HudFx
    {
        private const float RiseDistance = 42f;
        private const float ShakeCycles = 3f;
        private const float NumberSeconds = 0.8f;
        private const float ShakeSeconds = 0.25f;
        private const float FlashSeconds = 0.2f;
        private const float FlySeconds = 0.22f;

        private static V06HudFxDriver _driver;
        private static V06HudFxDriver Driver
        {
            get
            {
                if (_driver != null) { return _driver; }
                var go = new GameObject("V06 HUD Effects");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _driver = go.AddComponent<V06HudFxDriver>();
                return _driver;
            }
        }

        public static void DamageNumber(RectTransform layer, Vector2 anchoredPos, int amount,
            Color color, float duration = NumberSeconds)
        {
            if (!Application.isPlaying || layer == null || !layer.gameObject.activeInHierarchy) { return; }
            var go = new GameObject("V06 Damage Number", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(layer, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(104f, 36f);
            rect.anchoredPosition = anchoredPos;
            var label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 26;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = amount > 0 ? "-" + amount : amount.ToString();
            label.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.035f, 0.043f, 0.06f, 0.92f);
            outline.effectDistance = new Vector2(1f, -1f);

            Driver.Play(null, duration, () => go != null && layer != null && layer.gameObject.activeInHierarchy,
                t =>
                {
                    rect.anchoredPosition = anchoredPos + Vector2.up * (RiseDistance * EaseOut(t));
                    var faded = color;
                    faded.a *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 1f, t));
                    label.color = faded;
                },
                () => { if (go != null) { UnityEngine.Object.Destroy(go); } });
        }

        public static void Shake(RectTransform target, float strength = 6f, float duration = ShakeSeconds)
        {
            if (!CanMove(target)) { return; }
            var driver = Driver;
            driver.Cancel(target);
            Vector2 origin = target.anchoredPosition;
            driver.Play(target, duration, () => IsActive(target),
                t => target.anchoredPosition = origin + Vector2.right *
                    (strength * Mathf.Sin(t * ShakeCycles * 2f * Mathf.PI) * (1f - t)),
                () => { if (target != null) { target.anchoredPosition = origin; } });
        }

        public static void Flash(Graphic target, Color color, float duration = FlashSeconds)
        {
            if (!Application.isPlaying || target == null || !target.isActiveAndEnabled) { return; }
            var driver = Driver;
            driver.Cancel(target);
            var renderer = target.canvasRenderer;
            var original = renderer.GetColor();
            color.a = original.a;
            driver.Play(target, duration, () => target != null && target.isActiveAndEnabled && renderer != null,
                t => renderer.SetColor(Color.LerpUnclamped(color, original, EaseOut(t))),
                () => { if (renderer != null) { renderer.SetColor(original); } });
        }

        public static void Fly(RectTransform card, Vector2 from, Vector2 to, float duration = FlySeconds)
        {
            if (!CanMove(card)) { return; }
            Driver.Play(card, duration, () => IsActive(card),
                t => card.anchoredPosition = Vector2.LerpUnclamped(from, to, EaseOut(t)),
                () => { if (card != null) { card.anchoredPosition = to; } });
        }

        private static float EaseOut(float t) { float rest = 1f - t; return 1f - rest * rest * rest; }

        private static bool IsActive(RectTransform target)
        {
            return target != null && target.gameObject.activeInHierarchy;
        }

        private static bool CanMove(RectTransform target)
        {
            if (!Application.isPlaying || !IsActive(target)) { return false; }
            foreach (var graphic in target.GetComponentsInChildren<Graphic>(true))
                if (graphic.raycastTarget) { return false; }
            return true;
        }
    }

    internal sealed class V06HudFxDriver : MonoBehaviour
    {
        private sealed class Job
        {
            public UnityEngine.Object Key;
            public Coroutine Routine;
            public Action Finish;
        }

        private readonly List<Job> _jobs = new List<Job>();

        internal void Play(UnityEngine.Object key, float duration, Func<bool> alive, Action<float> tick, Action finish)
        {
            Cancel(key);
            if (duration <= 0f || !alive()) { finish(); return; }
            var job = new Job { Key = key, Finish = finish };
            _jobs.Add(job);
            job.Routine = StartCoroutine(Animate(job, duration, alive, tick));
        }

        internal void Cancel(UnityEngine.Object key)
        {
            if (key == null) { return; }
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                var job = _jobs[i];
                if (job.Key != key) { continue; }
                if (job.Routine != null) { StopCoroutine(job.Routine); }
                _jobs.RemoveAt(i);
                job.Finish();
            }
        }

        private IEnumerator Animate(Job job, float duration, Func<bool> alive, Action<float> tick)
        {
            float elapsed = 0f;
            while (elapsed < duration && alive())
            {
                tick(elapsed / duration);
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            _jobs.Remove(job);
            job.Finish();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            foreach (var job in _jobs) { job.Finish(); }
            _jobs.Clear();
        }
    }
}
