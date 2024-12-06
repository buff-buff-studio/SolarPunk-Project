using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Video
{
    public class Timeline : MonoBehaviour
    {
        public float duration = 0f;
        
        [Serializable]
        public class TimeLinePoint
        {
            public string label;
            public float time;
            public RectTransform circle;
            public bool created = false;
        }
        
        public TimeLinePoint[] points;
        public int currentPoint = 0;
        public float timer = 0;

        public RectTransform outerBar;
        public RectTransform bar;
        public RectTransform marker;
        public TMP_Text label;
        public AnimationCurve size;
        public RectTransform barFill;
        public AnimationCurve sizeCircles;

        public float creationTime = 1f;
        private void OnEnable()
        {
            foreach (var point in points)
            {
                point.circle.gameObject.SetActive(true);
                point.circle.anchoredPosition = new Vector2(point.time / duration * outerBar.rect.width, 0);
            }
        }

        public void Update()
        {
            timer += Time.deltaTime;
            timer = Mathf.Clamp(timer, 0, duration);
            marker.anchoredPosition = new Vector2(timer / duration * bar.rect.width, 0);
            
            var ct = timer / creationTime;
            barFill.sizeDelta = new Vector2(Mathf.Clamp01(ct) * bar.rect.width, barFill.sizeDelta.y);
            
            marker.sizeDelta = new Vector2(24 * Mathf.Clamp01(ct * 5f), 24);

            foreach (var point in points)
            {
                var lt = point.time / duration;

                if (ct >= lt && !point.created)
                {
                    point.created = true;
                    StartCoroutine(_Plup(point.circle, 0.25f, sizeCircles));
                }
            }

            if(points[currentPoint].time <= timer)
            {
                StartCoroutine(_Plup(label.transform, 0.5f, size));
                label.text = points[currentPoint].label;
                currentPoint++;
            }
        }

        private IEnumerator _Plup(Transform t, float time, AnimationCurve curve)
        {
            var tm = 0f;
            
            while (tm < time)
            {
                tm += Time.deltaTime;
                t.localScale = Vector3.one * curve.Evaluate(tm / time);
                yield return null;
            }
            
            t.localScale = Vector3.one * curve.Evaluate(1);
        }
    }
}